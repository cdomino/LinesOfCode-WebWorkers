namespace LinesOfCode.Web.Workers.Models
{
    public class WebWorkerSettingsModel
    {
        #region Initialization
        public WebWorkerSettingsModel()
        {
            //initialization
            this.AzureB2CSettings = new AzureB2CSettingsModel();
        }
        #endregion
        #region Properties
        public AzureB2CSettingsModel AzureB2CSettings { get; set; }
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
