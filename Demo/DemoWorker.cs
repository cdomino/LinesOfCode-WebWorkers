using System;

namespace LinesOfCode.Web.Workers.Demo
{
    /// <summary>
    /// This represents a worker on the main thread for demonstration purposes only.
    /// </summary>
    public class DemoWorker
    {
        #region Properties
        public Guid Id { get; set; }
        public string Name { get; set; }
        #endregion
        #region Initialization
        public DemoWorker(Guid id)
        {
            //initialization
            this.Id = id;

            //return
            if (id == Guid.Empty)
                this.Name = "No Worker; Invoke Method Directly";
            else
                this.Name = $"Use Worker {this.Id}";
        }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Name;
        }
        #endregion
    }
}
