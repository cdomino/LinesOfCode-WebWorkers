using System;
using System.IO;
using System.Text.Json;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

using LinesOfCode.Web.Workers.Utilities;
using LinesOfCode.Web.Workers.Enumerations;
using LinesOfCode.Web.Workers.Contracts.Services;

namespace LinesOfCode.Web.Workers.Services
{
    /// <summary>
    /// Encapsulates all serialization and deserialization logic using .NET JSON.
    /// </summary>
    public class SerializationService : ISerializationService
    {
        #region Members
        private readonly ILogger<SerializationService> _logger;
        #endregion
        #region Initialization
        public SerializationService(ILogger<SerializationService> logger)
        {
            //initialization
            _logger = logger;
        }
        #endregion
        #region Public Methods     
        /// <summary>
        /// Compresses a byte stream using a given encoding async.
        /// </summary>
        public async Task<byte[]> CompressAsync(byte[] source, CompressionType encoding)
        {
            //initialization
            _logger.LogDebug($"Compressing {source.Pluralize("byte")} using {encoding.GetDisplayName()} async.");

            //open buffer
            using (MemoryStream compressed = new MemoryStream())
            {
                //determine encoding
                switch (encoding)
                {
                    //gzip
                    case CompressionType.GZip:

                        //open compression stream
                        using (GZipStream compressor = new GZipStream(compressed, CompressionLevel.Fastest))
                        {
                            //compress
                            await compressor.WriteAsync(source, 0, source.Length);
                            await compressor.FlushAsync();
                        }
                        break;

                    //deflate
                    case CompressionType.Deflate:

                        //open compression stream
                        using (DeflateStream compressor = new DeflateStream(compressed, CompressionLevel.Fastest))
                        {
                            //compress
                            await compressor.WriteAsync(source, 0, source.Length);
                            await compressor.FlushAsync();
                        }
                        break;

                    //no compression
                    default:
                        _logger.LogInformation($"Cannot compress using encoding {encoding.GetDisplayName()} async.");
                        return source;
                }

                //return
                byte[] result = compressed.ToArray();
                _logger.LogDebug($"Compressed {source.Pluralize("byte")} to {result.Pluralize("byte")} async.");
                return result;
            }
        }

        /// <summary>
        /// Decompresses a byte stream using a given encoding async.
        /// </summary>
        public async Task<byte[]> DecompressAsync(byte[] source, CompressionType encoding)
        {
            //initialization
            _logger.LogDebug($"Decompressing {source.Pluralize("byte")} using {encoding.GetDisplayName()} async.");

            //open output buffer
            using (MemoryStream decommpressed = new MemoryStream())
            {
                //open input buffer
                using (MemoryStream compressed = new MemoryStream(source))
                {
                    //determine encoding
                    switch (encoding)
                    {
                        //gzip
                        case CompressionType.GZip:

                            //open decompressor
                            using (GZipStream decompressor = new GZipStream(compressed, CompressionMode.Decompress))
                            {
                                //decompress
                                await decompressor.CopyToAsync(decommpressed);
                            }
                            break;

                        //deflate
                        case CompressionType.Deflate:

                            //open decompressor
                            using (DeflateStream decompressor = new DeflateStream(compressed, CompressionMode.Decompress))
                            {
                                //decompress
                                await decompressor.CopyToAsync(decommpressed);
                            }
                            break;

                        //no compression
                        default:
                            _logger.LogInformation($"Cannot decompress using encoding {encoding.GetDisplayName()} async.");
                            return source;
                    }

                    //return
                    byte[] result = decommpressed.ToArray();
                    _logger.LogDebug($"Deompressed {source.Pluralize("byte")} to {result.Pluralize("byte")} async.");
                    return result;
                }
            }
        }

        /// <summary>
        /// Serializes an object to a string.
        /// </summary>
        public async Task<string> SerializeAsync<T>(T instance, bool includeJsonTokens = true, [CallerLineNumber()] int callerLineNumber = 0, [CallerMemberName()] string callerMethod = null, [CallerFilePath()] string callerFilePath = null)
        {
            //initialization
            Type type = typeof(T);
            string message = $"{instance} of type {type.FullName}.";

            //check object
            _logger.LogDebug($"Serializing {message}.");
            if (instance.IsNull())
                return string.Empty;

            try
            {
                //serialize
                string result = await SerializeCoreAsync(message, async (stream) => await JsonSerializer.SerializeAsync(stream, instance, WebWorkerUtilities.BuildSerializerOptions(includeJsonTokens)));

                //return
                _logger.LogDebug($"Serialized {message} to {result.Length.Pluralize("character")}.");
                return result;
            }
            catch (Exception ex)
            {
                //error
                string messageWithCallerInfo = WebWorkerUtilities.FormatLogMessageWithCallerMetadata(message, callerFilePath, callerMethod, callerLineNumber);
                _logger.LogError($"Unable to serialize {messageWithCallerInfo}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Deserializes a string to an object.
        /// </summary>
        public async Task<T> DeserializeAsync<T>(string json, [CallerLineNumber()] int callerLineNumber = 0, [CallerMemberName()] string callerMethod = null, [CallerFilePath()] string callerFilePath = null)
        {
            //initialization
            Type type = typeof(T);
            string message = PreDeserialize(json, type);
            if (string.IsNullOrWhiteSpace(message))
                return default;

            //get stream
            using MemoryStream stream = BuildJSONStream(json);

            try
            {
                //return
                T result = await JsonSerializer.DeserializeAsync<T>(stream, WebWorkerUtilities.BuildSerializerOptions(true));
                return PostDeserialize(result, message);
            }
            catch (Exception ex)
            {
                //error
                string messageWithCallerInfo = WebWorkerUtilities.FormatLogMessageWithCallerMetadata(message, callerFilePath, callerMethod, callerLineNumber);
                _logger.LogError($"Unable to deserialize {messageWithCallerInfo}: {ex}");
                return default;
            }
        }

        /// <summary>
        /// Deserializes a string to an object.
        /// </summary>
        public async Task<object> DeserializeAsync(Type type, string json, [CallerLineNumber()] int callerLineNumber = 0, [CallerMemberName()] string callerMethod = null, [CallerFilePath()] string callerFilePath = null)
        {
            //initialization
            string message = PreDeserialize(json, type);
            if (string.IsNullOrWhiteSpace(message))
                return null;

            //get stream
            using MemoryStream stream = BuildJSONStream(json);

            try
            {
                //return
                object result = await JsonSerializer.DeserializeAsync(stream, type, WebWorkerUtilities.BuildSerializerOptions(true));
                return PostDeserialize(result, message);
            }
            catch (Exception ex)
            {
                //error
                string messageWithCallerInfo = WebWorkerUtilities.FormatLogMessageWithCallerMetadata(message, callerFilePath, callerMethod, callerLineNumber);
                _logger.LogError($"Unable to deserialize {messageWithCallerInfo}: {ex} ");
                return type.DefaultValue();
            }
        }

        /// <summary>
        /// Clones an object.
        /// </summary>
        public async Task<T> CloneAsync<T>(T value)
        {
            //initialization
            if (value == null)
            {
                //error
                _logger.LogTrace("Can't clone a null object.");
                return default;
            }

            //clone
            string clone = await SerializeAsync(value);
            _logger.LogTrace($"Cloning {clone.Length.Pluralize("character")} of {typeof(T).Name} for {value}.");

            //return
            return await DeserializeAsync<T>(clone);
        }
        #endregion
        #region Private Methods
        /// <summary>
        /// Checks if a string is valid for deserialization.
        /// </summary>
        private string PreDeserialize(string json, Type type)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(json))
            {
                //no content
                _logger.LogWarning("Cannot deserialize an empty string.");
                return string.Empty;
            }
            else
            {
                //return
                string message = $"{json.Length.Pluralize("character")} of {type.FullName}";
                _logger.LogDebug($"Deserializing string {message}.");
                return message;
            }
        }

        /// <summary>
        /// Returns the result of a deserialization.
        /// </summary>
        private T PostDeserialize<T>(T result, string message)
        {
            //check result
            if (result == null)
            {
                //error
                _logger.LogWarning($"Unable to deserialize {message}.");
                return default;
            }
            else
            {
                //return
                _logger.LogDebug($"Deserialized {message} to {result}.");
                return result;
            }
        }

        /// <summary>
        /// Converts a json string to a memory stream for deserialization.
        /// </summary>
        private MemoryStream BuildJSONStream(string json)
        {
            //initialization
            byte[] bytes = WebWorkerUtilities.ConvertStringToBytes(json);
            _logger.LogTrace($"Preparing to deserialize {bytes.Pluralize("byte")}.");

            //return
            return new MemoryStream(bytes);
        }

        /// <summary>
        /// This is the core serialization logic to turn an object into JSON.
        /// </summary>
        private async Task<string> SerializeCoreAsync(string message, Func<Stream, Task> serialize)
        {
            //initialization
            _logger.LogDebug($"Serializing {message}.");

            //serialize
            using MemoryStream stream = new MemoryStream();
            await serialize(stream);

            //get result
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream);
            string result = await reader.ReadToEndAsync();

            //return
            _logger.LogDebug($"Serialized {message} to {result.Length.Pluralize("character")}.");
            return result;
        }
        #endregion
    }
}
