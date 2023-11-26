using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Mock
{
    public class MockLongRunningService : IMockLongRunningService
    {
        #region Members
        private readonly ILogger<MockLongRunningService> _logger;
        #endregion
        #region Events
        public event EventHandler<MockEventData> MockEvent;
        #endregion
        #region Initialization
        public MockLongRunningService(ILogger<MockLongRunningService> logger)
        {
            //initialization
            this._logger = logger;
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Simulates a long running operation.
        /// </summary>
        public async Task<string> RunAsync(int simulationSeconds)
        {
            //initialization
            Stopwatch stopWatch = Stopwatch.StartNew();
            simulationSeconds = Math.Min(Math.Max(simulationSeconds, 1), 10);

            await Task.Delay(1000);
          
            for (int second = 1; second < simulationSeconds; second++)
            {
                //raise event every second
                this.MockEvent?.Invoke(this, new MockEventData(stopWatch.Elapsed.TotalSeconds));
                await Task.Delay(1000);
            }

            //return
            string result = $"Simulated mock service call for {simulationSeconds.Pluralize("second")}.";
            this._logger.LogInformation(result);
            return result;
        }
        #endregion
    }
}
