using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using LinesOfCode.Web.Workers.Enumerations;

namespace LinesOfCode.Web.Workers.Contracts
{
    public interface ISerializationManager
    {
        #region Methods
        Task<T> CloneAsync<T>(T value);
        Task<byte[]> CompressAsync(byte[] source, CompressionType encoding);
        Task<byte[]> DecompressAsync(byte[] source, CompressionType encoding);
        Task<T> DeserializeAsync<T>(string json, [CallerLineNumber()] int callerLineNumber = 0, [CallerMemberName()] string callerMethod = null, [CallerFilePath()] string callerFilePath = null);
        Task<string> SerializeAsync<T>(T instance, bool includeJsonTokens = true, [CallerLineNumber()] int callerLineNumber = 0, [CallerMemberName()] string callerMethod = null, [CallerFilePath()] string callerFilePath = null);
        Task<object> DeserializeAsync(Type type, string json, [CallerLineNumber()] int callerLineNumber = 0, [CallerMemberName()] string callerMethod = null, [CallerFilePath()] string callerFilePath = null);
        #endregion
    }
}
