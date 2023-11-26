using System;

namespace LinesOfCode.Web.Workers.Models
{
    public class AzureB2CSettings
    {
        #region Properties
        public string AppId { get; set; }
        public string Policy { get; set; }
        public string TenantId { get; set; }
        public string Instance { get; set; }
        public string AccessScope { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.AppId;
        }
        #endregion
    }
}
