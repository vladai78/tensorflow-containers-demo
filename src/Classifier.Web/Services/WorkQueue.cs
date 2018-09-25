using Classifier.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;

namespace Classifier.Web.Services
{
    public class WorkQueue : IWorkQueue
    {
        private BlockingCollection<ClassificationWorkItem> workQueue_;
        private System.Threading.Timer generateWorkTimer_;
        private IHubContext<ImagesHub> queueHub_;
        private int maxWorkItems_;

        public WorkQueue(IHubContext<ImagesHub> hubContext)
        {
            workQueue_ = new BlockingCollection<ClassificationWorkItem>();
            generateWorkTimer_ = new System.Threading.Timer(AddWork, null, 1000, 10000);
            queueHub_ = hubContext;
            maxWorkItems_ = 30;
        }

        private void AddWork(object state)
        {
            if (workQueue_.Count > maxWorkItems_)
            {
                return;
            }
            var rand = new Random();
            var imageIndex = rand.Next(3);
            workQueue_.Add(new ClassificationWorkItem() { ImageIndex = imageIndex });
            queueHub_.Clients.All.SendAsync("queueChanged", workQueue_.Count.ToString());
        }

        public ClassificationWorkItem GetWork()
        {
            var item = workQueue_.Take();
            queueHub_.Clients.All.SendAsync("queueChanged", workQueue_.Count.ToString());
            return item;
        }

        public void SetPeriod(double period)
        {
            int periodInMs = (int)(period * 1000);
            generateWorkTimer_.Change(0, periodInMs);
        }
    }
}
