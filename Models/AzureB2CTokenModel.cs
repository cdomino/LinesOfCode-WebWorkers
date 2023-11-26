using System;

namespace LinesOfCode.Web.Workers.Models
{
    public class AzureB2CTokenModel
    {
        #region Properties
        public string Realm { get; set; }
        public string Secret { get; set; }
        public Guid ClientId { get; set; }
        public string Target { get; set; }
        public string CachedAt { get; set; }
        public string ExpiresOn { get; set; }
        public string TokenType { get; set; }
        public string Environment  { get; set; }
        public string HomeAccountId { get; set; }
        public string CredentialType { get; set; }
        public string ExtendedExpiresOn { get; set; }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.Environment;
        }
        #endregion
    }
}
