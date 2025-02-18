using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Demo
{
    /// <summary>
    /// This is a demo service only to simulate long-running worker thread executions for testing.
    /// </summary>
    public class DemoLongRunningService : IDemoLongRunningService
    {
        #region Members
        private readonly ILogger<DemoLongRunningService> _logger;
        #endregion
        #region Events
        public event EventHandler<DemoEventData> MockEvent;
        #endregion
        #region Initialization
        public DemoLongRunningService(ILogger<DemoLongRunningService> logger)
        {
            //initialization
            this._logger = logger;
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Simulates a long running operation. Pass null to simulate an error.
        /// </summary>
        public async Task<string> RunAsync(int? simulationSeconds)
        {
            //initialization
            if (!simulationSeconds.HasValue)
            {
                //error
                await Task.Delay(1000);
                throw new Exception("Exception Message", new Exception("Inner Exception"));
            }

            //start simulation
            Stopwatch stopWatch = Stopwatch.StartNew();
            simulationSeconds = Math.Min(Math.Max(simulationSeconds.Value, 1), 10);
            
            //run simulation
            await Task.Delay(1000);
            for (int second = 1; second < simulationSeconds; second++)
            {
                //raise event every second
                this.MockEvent?.Invoke(this, new DemoEventData(stopWatch.Elapsed.TotalSeconds));
                await Task.Delay(1000);
            }

            //return
            string result = $"Simulated mock service call for {simulationSeconds.Value.Pluralize("second")}.";
            this._logger.LogInformation(result);
            return result;
        }
        #endregion
    }
}
