namespace LinesOfCode.Web.Workers.Models
{
    /// <summary>
    /// This holds the metadata needed to retry an invocation after a token expiration.
    /// </summary>
    public class InvocationRetryModel
    {
        #region Properties
        public bool TokenRefreshed { get; private set; }
        public object InvocationResult { get; private set; }
        #endregion
        #region Initialization
        public InvocationRetryModel(object invocationResult, bool tokenRefreshed) 
        {
            //initialization
            this.TokenRefreshed = tokenRefreshed;
            this.InvocationResult = invocationResult;
        }
        #endregion
        #region Public Methods
        public override string ToString()
        {
            //return
            return this.TokenRefreshed ? "Failed" : "Succeeded";
        }
        #endregion
    }
}
