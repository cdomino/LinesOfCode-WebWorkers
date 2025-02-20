using System.ComponentModel.DataAnnotations;

using LinesOfCode.Web.Workers.Utilities;

namespace LinesOfCode.Web.Workers.Enumerations
{
    public enum CompressionType
    {
        None = 0,

        [Display(Name = WebWorkerConstants.Compression.GZip, Order = 1)]
        GZip = 1,

        [Display(Name = WebWorkerConstants.Compression.Deflate, Order = 2)]
        Deflate = 2
    }
}
