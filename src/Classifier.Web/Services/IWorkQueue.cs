using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Classifier.Web.Services
{
    public interface IWorkQueue
    {
        ClassificationWorkItem GetWork();
        void SetPeriod(double period);
    }
}
