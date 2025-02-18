namespace LinesOfCode.Web.Workers.Demo
{
    public class DemoEventData
    {
        #region Properties
        public double ElapsedTime { get; set; }
        #endregion
        #region Initialization
        public DemoEventData(double elapsedTime)
        {
            //initialization
            this.ElapsedTime = elapsedTime;
        }
        #endregion
    }
}
