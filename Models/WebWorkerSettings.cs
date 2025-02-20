namespace LinesOfCode.Web.Workers.Models
{
    public class WebWorkerSettings
    {
        #region Initialization
        public WebWorkerSettings()
        {
            //initialization
            this.AzureB2CSettings = new AzureB2CSettings();
        }
        #endregion
        #region Properties
        public AzureB2CSettings AzureB2CSettings { get; set; }
        #endregion
        #region Initialization
        public override string ToString()
        {
            //return
            return this.AzureB2CSettings?.ToString() ?? "N/A";
        }
        #endregion
    }
}
