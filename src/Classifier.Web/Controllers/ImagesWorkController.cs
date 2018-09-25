using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Classifier.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Classifier.Web.Controllers
{
    [Produces("application/json")]
    public class ImagesWorkController : Controller
    {
        IWorkQueue workQueue_;

        public ImagesWorkController(IWorkQueue workQueue)
        {
            workQueue_ = workQueue;
        }

        [HttpGet("api/ImagesWork")]
        public IActionResult GetWork()
        {
            var workItem = workQueue_.GetWork();
            return Ok(workItem);
        }

        //[HttpPost]
        public IActionResult ChangePeriod([FromBody] string period)
        {
            double p;
            if (!Double.TryParse(period, out p))
            {
                return BadRequest();
            }
            workQueue_.SetPeriod(p);
            return Ok();
        }

        [HttpPost("api/GenerateFast")]
        public IActionResult GenerateFast()
        {
            workQueue_.SetPeriod(0.3);
            return Ok();
        }

        [HttpPost("api/GenerateSlow")]
        public IActionResult GenerateSlow()
        {
            workQueue_.SetPeriod(10.0);
            return Ok();
        }
    }
}