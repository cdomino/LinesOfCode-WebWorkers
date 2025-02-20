using System;
using System.Threading.Tasks;

namespace LinesOfCode.Web.Workers.Demo
{
    public interface IDemoLongRunningService
    {
        #region Events
        public event EventHandler<DemoEventData> DemoEvent;
        #endregion
        #region Methods
        Task<string> RunDemoAsync(int? simulationSeconds);
        #endregion
    }
}
