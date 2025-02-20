using System;

namespace LinesOfCode.Web.Workers.Mock
{
    public class MockWorker
    {
        #region Properties
        public Guid Id { get; set; }
        public string Name { get; set; }
        #endregion
        #region Initialization
        public MockWorker(Guid id)
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
