using System;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Logging;

namespace LinesOfCode.Web.Workers.Utilities
{
    /// <summary>
    /// This contains common functionality, helper, and extension methods.
    /// </summary>
    public static class WebWorkerUtilities
    {
        #region Miscellaneous
        /// <summary>
        /// Wraps a client version of Task.Yield using Task.Delay for Blazor performance; the delay value is in milliseconds.
        /// </summary>
        public static async Task YieldAsync(int delayMilliseconds = 0)
        {
            //return
            if (delayMilliseconds > 0)
                await Task.Delay(delayMilliseconds);
            else
                await Task.Yield();
        }

        /// <summary>
        /// Adds an item to a concurrent dictionary.
        /// </summary>
        public static void Add<K, V>(this ConcurrentDictionary<K, V> dictionary, K key, V value)
        {
            //add and update value
            dictionary.AddOrUpdate(key, value, (_, __) => { return __; });
        }
        #endregion
        #region Security
        /// <summary>
        /// Parses a primary or secondary claim value from a principal.
        /// </summary>
        public static string GetClaimValueWithFallback(this ClaimsPrincipal principal, string primaryClaim, string secondaryClaim = null)
        {
            //initialization
            if (principal == null || string.IsNullOrWhiteSpace(primaryClaim))
                return null;

            //this parses a claim value if it exists
            string findFirstValue(string claim)
            {
                //return
                return principal.FindFirst(claim)?.Value;
            }

            //get value
            string value = findFirstValue(primaryClaim);
            if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(secondaryClaim))
                value = findFirstValue(secondaryClaim);

            //return
            return value;
        }

        /// <summary>
        /// Builds a key to get an Azure B2C token from browser session storage.
        /// </summary>
        public static string BuildAzureB2CTokenSessionKey(Guid userId, string policy, Guid tenantId, string instance, Guid appId, string scope)
        {
            //initialization
            if (Uri.TryCreate(instance, UriKind.Absolute, out Uri uri))
                instance = uri.DnsSafeHost;

            //return
            return string.Format(WebWorkerConstants.Security.B2CTokenSessionKeyFormat, userId, policy, tenantId, instance, appId, scope).ToLowerInvariant();
        }
        #endregion
        #region Serialization
        /// <summary>
        /// Configures .NET json serialization to force pascal case, allow cycles, and consume custom converters.
        /// </summary>
        public static void ApplyJSONConfiguration(this JsonSerializerOptions options, bool includeJsonTokens = true)
        {
            //initiailization
            options.Converters.Add(new JsonStringEnumConverter());

            //return
            options.DictionaryKeyPolicy = null;
            options.PropertyNamingPolicy = null;
            options.PropertyNameCaseInsensitive = true;
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            options.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
            options.ReferenceHandler = includeJsonTokens ? ReferenceHandler.Preserve : null;
        }

        /// <summary>
        /// Builds a JSON serializer with customized settings for non-DI scenarios.
        /// </summary>
        public static JsonSerializerOptions BuildSerializerOptions(bool includeJsonTokens = true)
        {
            //initialization
            JsonSerializerOptions options = new JsonSerializerOptions();

            //configure
            options.ApplyJSONConfiguration(includeJsonTokens);
            
            //return
            return options;
        }
        #endregion
        #region Multithreading
        /// <summary>
        /// Runs a batch of tasks concurrently and reports any exceptions.
        /// </summary>
        public static async Task<AggregateException> WhenAllAsync(params Task[] tasks)
        {
            //return
            return await tasks.WhenAllAsync();
        }

        /// <summary>
        /// Runs a batch of tasks concurrently and reports any exceptions.
        /// </summary>
        public static async Task<AggregateException> WhenAllAsync(this IEnumerable<Task> tasks)
        {
            try
            {
                //execute work
                await Task.WhenAll(tasks);
                List<Exception> exceptions = new List<Exception>();

                //capture exceptions
                foreach (Task task in tasks)
                    if (task.Exception != null)
                        exceptions.Add(task.Exception);

                //return
                if (exceptions.Any())
                    return new AggregateException(exceptions);
                else
                    return null;
            }
            catch (Exception ex)
            {
                //error
                return new AggregateException("Failed to run task batch.", ex);
            }
        }

        /// <summary>
        /// Starts an async operation without waiting for a result. If the calling process ends, this operation will be terminated.
        /// </summary>
        public static void FireAndForget(Func<Task> codeAsync, ILogger logger)
        {
            //initialization
            string message = "Starting a fire and forget operation.";
            log(message, LogLevel.Information);

            //this does the logging
            void log(string text, LogLevel logLevel)
            {
                //return
                if (logger == null)
                    WebWorkerUtilities.Log(text);
                else
                    logger.Log(logLevel, text);
            }

            //return
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    //run code
                    await codeAsync();
                }
                catch (Exception ex)
                {
                    //return
                    log($"A fire and forget operation failed: {ex}", LogLevel.Error);
                }
            }, TaskCreationOptions.LongRunning).ConfigureAwait(false);
        }
        #endregion
        #region IL
        /// <summary>
        /// Loads a token representing a type into the stack, and then uses reflection to get the actual type from a handle.
        /// </summary>
        public static void PushTypeToStack(this ILGenerator il, Type type, MethodInfo getTypeFromHandle)
        {
            //initialization
            il.Emit(OpCodes.Ldtoken, type);

            //return
            il.Emit(OpCodes.Call, getTypeFromHandle);
        }
        #endregion
        #region Logging
        /// <summary>
        /// Output to consoles when no logger is available.
        /// </summary>
        public static void Log(string message)
        {
            //initializtaion
            string text = $"{WebWorkerConstants.Messages.NoLoggerPrefix}{message}";

            //return
            Trace.WriteLine(text);
            Console.WriteLine(text);
        }

        /// <summary>
        /// Adds caller metadata to a log message.
        /// </summary>
        public static string FormatLogMessageWithCallerMetadata(string message, string callerFilePath, string callerMethod, int callerLineNumber)
        {
            //initialization
            string callerClass = string.IsNullOrWhiteSpace(callerFilePath) ? "N/A" : callerFilePath.Split('\\').LastOrDefault()?.Replace(".cs", string.Empty) ?? callerFilePath;

            //return
            return $"{message} {{{callerClass}.{callerMethod}#{callerLineNumber}@{DateTime.Now.ToTimeString()}}}";
        }

        /// <summary>
        /// Formats a long time string with a leading short date.
        /// </summary>
        public static string ToTimeString(this DateTime date)
        {
            //return
            return $"{date.ToShortDateString()} {date.ToLongTimeString()}";
        }

        /// <summary>
        /// Formats a collection as a comma (or other) delimited text list.
        /// </summary>
        public static string ToSeparatedList(this IEnumerable<string> items, string separator = ", ")
        {
            //initialization
            if (string.IsNullOrWhiteSpace(separator))
                separator = ", ";

            //return
            if (!items?.Any() ?? true)
                return "N/A";
            else
                return string.Join(separator, items);
        }

        /// <summary>
        /// Formats a dictionary as a comma (or other) delimited text list.
        /// </summary>
        public static string ToDictionaryString<K, V>(this IDictionary<K, V> dictionary)
        {
            //initialization
            if (!dictionary?.Keys?.Any() ?? true)
                return "N/A";

            //return
            return dictionary.Select(d => $"{d.Key}{WebWorkerConstants.Delimiters.Variable}{d.Value}").ToSeparatedList(WebWorkerConstants.Delimiters.Dataset.ToString());
        }

        /// <summary>
        /// Gets the first instance of a decorated object's attribute by type.
        /// </summary>
        public static T GetFirstAttribue<T>(object value) where T : Attribute
        {
            //initialization
            IEnumerable<T> attribs = value.GetType()
                                          .GetMember(value.ToString())
                                          .FirstOrDefault()
                                         ?.GetCustomAttributes<T>() ?? null;

            //return
            if (attribs?.Any() ?? false)
                return attribs.First();
            else
                return null;
        }

        /// <summary>
        /// Gets the name metadata of the first display name attribute for an enumeration item.
        /// </summary>
        public static string GetDisplayName(this Enum value)
        {
            //initialization
            DisplayAttribute attribute = WebWorkerUtilities.GetFirstAttribue<DisplayAttribute>(value);

            //return
            if (attribute == null)
            {
                //use name as value
                return value.ToString();
            }
            else
            {
                //get attribute value
                string name = attribute.GetName();
                return string.IsNullOrWhiteSpace(name) ? value.ToString() : name;
            }
        }
        #endregion
        #region Types
        /// <summary>
        /// Sandardization of using UTF for string->byte conversions.
        /// </summary>
        public static byte[] ConvertStringToBytes(string value)
        {
            //return
            return UTF8Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Sandardization of using UTF for byte->string conversions.
        /// </summary>
        public static string ConvertBytesToString(byte[] value)
        {
            //return
            return UTF8Encoding.UTF8.GetString(value);
        }

        /// <summary>
        /// Determines that a call is async if the return type is a task. Using "FullName" (vs. AQN) and "StartsWith" (vs. Equals) to account for variance in async signatures.
        /// </summary>
        public static bool IsAsync(this Type returnType)
        {
            //initialization
            string taskTypeName = typeof(Task).FullName;

            //return
            if (string.IsNullOrWhiteSpace(returnType.FullName))
                return returnType.BaseType.FullName.StartsWith(taskTypeName);
            else
                return returnType.FullName.StartsWith(taskTypeName);
        }

        /// <summary>
        /// Determines if a type can be converted
        /// </summary>
        public static bool IsPrimitive(Type type)
        {
            //return
            return type.IsPrimitive || type.IsValueType || (type == typeof(string));
        }

        /// <summary>
        /// Determines if a string is valid json.
        /// </summary>
        public static bool IsJSON(this string text)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(text))
                return false;
            else
                text = text.Trim();

            //check for leading and trailing json identifiers
            return (text.StartsWith("{") && text.EndsWith("}")) || (text.StartsWith("[") && text.EndsWith("]"));
        }

        /// <summary>
        /// Checks if an object is null or an empty array.
        /// </summary>
        public static bool IsNull<T>(this T instance)
        {
            //check null
            if (instance == null)
                return true;

            //check empty string (can't use String.IsNullOrWhiteSpace)
            if ((instance as string) == string.Empty)
                return true;

            //check empty collection
            if (instance is ICollection && ((ICollection)instance).Count == 0)
                return true;

            //check default values
            if (instance.Equals(default(T)))
                return true;

            //not null
            return false;
        }

        /// <summary>
        /// Converts to string to a given type.
        /// </summary>
        public static T ConvertFromString<T>(this string value)
        {
            //initialization
            object result = value.ConvertFromString(typeof(T));

            //return
            if (result == null)
                return default(T);
            else
                return (T)result;
        }

        /// <summary>
        /// Converts to string to a given type.
        /// </summary>
        public static object ConvertFromString(this string value, Type type)
        {
            //return
            if (string.IsNullOrWhiteSpace(value) || type == null)
                return type?.DefaultValue() ?? null;
            else
                return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(value);
        }

        /// <summary>
        /// Returns the default value for a non-generic type.
        /// </summary>
        public static object DefaultValue(this Type type)
        {
            //return
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        #endregion
        #region Grammar
        /// <summary>
        /// Provides a proper pluralized representation of a collection in terms of its noun.
        /// </summary>
        public static string Pluralize<T>(this IEnumerable<T> collection, string noun, string pluralTerm = "s")
        {
            //initialization
            if (collection == null)
                collection = new List<T>();

            //return
            return collection.Count().Pluralize(noun, pluralTerm);
        }

        /// <summary>
        /// Provides a proper pluralized representation of an integer.
        /// </summary>
        public static string Pluralize(this int count, string noun, string pluralTerm = "s")
        {
            //return
            return Convert.ToDouble(count).Pluralize(noun, pluralTerm);
        }

        /// <summary>
        /// Provides a proper pluralized representation of a double.
        /// </summary>
        public static string Pluralize(this double count, string noun, string pluralTerm = "s")
        {
            //initialization
            double absoluteCount = Math.Abs(count);
            noun = noun.TrimEnd(new char[] { 's', 'S' });
            int pluralTrimLength = noun.ToLowerInvariant().EndsWith("ex".ToLowerInvariant()) ? 2 : 1;

            //return
            return $"{count} {(pluralTerm == "s" ? noun : absoluteCount != 1 ? noun.Substring(0, noun.Length - pluralTrimLength) : noun)}{(absoluteCount == 1 ? string.Empty : pluralTerm)}";
        }
        #endregion       
    }
}
