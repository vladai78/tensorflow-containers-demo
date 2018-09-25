using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TensorFlow;

namespace Classifier.Worker
{
    public class ClassificationWorkItem
    {
        public int ImageIndex;
    }

    class Program
    {
        private static string hostname;

        /// <summary>
        /// Contacts the server to fetch image number, blocks in case that there is no work.
        /// </summary>
        /// <param name="client">Client that is used for connection.</param>
        /// <param name="imageIndex">Index of image for processing (-1 if no image available).</param>
        static void GetImageIndexFromServer(HttpClient httpClient, string url, out int imageIndex)
        {
            imageIndex = -1;
            try
            {
                var imageWorkResult = httpClient.GetAsync(url);
                imageWorkResult.Wait();
                if (!imageWorkResult.Result.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Server returned {imageWorkResult.Result.ToString()}");
                    return;
                }
                var jsonResult = imageWorkResult.Result.Content?.ReadAsStringAsync().Result;
                var classificationWork = JsonConvert.DeserializeObject<ClassificationWorkItem>(jsonResult);
                imageIndex = classificationWork.ImageIndex;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static ClassificationResult ProcessImage(TFGraph graph, string[] labels, ImageMetadata image)
        {
            var session = new TFSession(graph);
            var sw = new Stopwatch();
            sw.Reset();
            sw.Start();
            var tensor = ImageUtil.CreateTensorFromImageFile($"assets/images/{image.ImageId}.{image.EncodingFormat}");
            var runner = session.GetRunner();
            runner.AddInput(graph["Placeholder"][0], tensor).Fetch(graph["loss"][0]);
            var output = runner.Run();
            var result = output[0];

            var probabilities = ((float[][])result.GetValue(jagged: true))[0];
            var highestProbability = probabilities
                .Select((p, i) => (Probability: p, Index: i))
                .OrderByDescending(p => p.Probability)
                .First();
            var bestResult = (Label: labels[highestProbability.Index], Probability: highestProbability.Probability);
            sw.Stop();

            tensor.Dispose();
            foreach (var o in output)
            {
                o.Dispose();
            }

            return new ClassificationResult
            {
                Image = image,
                Label = bestResult.Label,
                Probability = bestResult.Probability,
                WorkerId = hostname,
                TimeTaken = sw.ElapsedMilliseconds
            };
        }

        static void Main(string[] args)
        {
            var images = JsonConvert.DeserializeObject<ImageMetadata[]>(File.ReadAllText("assets/images/images.json"));
            var rand = new Random();
            var graph = new TFGraph();
            var model = File.ReadAllBytes("assets/model.pb");
            var labels = File.ReadAllLines("assets/labels.txt");
            hostname = Guid.NewGuid().ToString();
            var jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            var httpClient = new HttpClient();

            graph.Import(model);

            var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:50293";

            var sw = new Stopwatch();
            while(true) {
                // 1. Get the image index from server.
                int imageIndex = 0;
                GetImageIndexFromServer(httpClient, $"{apiBaseUrl}/api/ImagesWork", out imageIndex);
                if (imageIndex < 0)
                {
                    continue;
                }
                imageIndex = imageIndex % images.Length;

                // 2. Classify the image
                var image = images[imageIndex];
                var classificationResult = ProcessImage(graph, labels, image);

                // 3. Send the result back to server.
                Console.WriteLine(JsonConvert.SerializeObject(classificationResult, jsonSettings));
                var content = new StringContent(
                    JsonConvert.SerializeObject(classificationResult, jsonSettings),
                    Encoding.UTF8,
                    "application/json"
                );
                try
                {
                    httpClient.PostAsync($"{apiBaseUrl}/api/imageprocessed", content).Wait();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
    }
}
