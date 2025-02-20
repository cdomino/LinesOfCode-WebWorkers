using System;
using System.Threading.Tasks;

namespace LinesOfCode.Web.Workers.Mock
{
    public interface IMockLongRunningService
    {
        #region Events
        public event EventHandler<MockEventData> MockEvent;
        #endregion
        #region Methods
        Task<string> RunAsync(int simulationSeconds);
        #endregion
    }
}
