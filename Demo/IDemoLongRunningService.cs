using System;
using System.Threading.Tasks;

namespace LinesOfCode.Web.Workers.Demo
{
    public interface IDemoLongRunningService
    {
        #region Events
        public event EventHandler<DemoEventData> MockEvent;
        #endregion
        #region Methods
        Task<string> RunAsync(int? simulationSeconds);
        #endregion
    }
}
