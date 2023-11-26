namespace LinesOfCode.Web.Workers.Mock
{
    public class MockEventData
    {
        #region Properties
        public double ElapsedTime { get; set; }
        #endregion
        #region Initialization
        public MockEventData(double elapsedTime)
        {
            //initialization
            this.ElapsedTime = elapsedTime;
        }
        #endregion
    }
}
