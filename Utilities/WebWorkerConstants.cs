namespace LinesOfCode.Web.Workers.Utilities
{
    /// <summary>
    /// This contains well-known system values.
    /// </summary>
    public static class WebWorkerConstants
    {
        public class Hosting
        {
            public const string Body = "body";
            public const string WebWorker = nameof(WebWorker);
            public const string APIAnonymous = nameof(APIAnonymous);
            public const string APIAuthorized = nameof(APIAuthorized);
            public class JSRuntime
            {
                public const string Instance = nameof(Instance);
                public const string TypeName = "Microsoft.AspNetCore.Components.WebAssembly.Services.DefaultWebAssemblyJSRuntime";
            }
        }
        public class Messages
        {
            public const string NoLoggerPrefix = "[NO LOGGER]: ";
            public const string ProxyOnly = "This method is only intended to be accessed by proxy objects.";
            public const string ObsoleteModelParameterlessConstructor = "Do not use this constructor; it is intended for deserialization only.";
        }
        public class JavaScript
        {
            public const string InvokeWorker = "invokeWorker";
            public const string MarshalEvent = "marshalEvent";
            public const string CreateWebWorker = "createWebWorker";
            public const string ConnectWebWorker = "connectWebWorker";
            public const string GetWebWorkerToken = "getWebWorkerToken";
            public const string TerminateWebWorker = "terminateWebWorker";
            public const string SendWebWorkerToken = "sendWebWorkerToken";
            public const string GetWebWorkerSettings = "getWebWorkerSettings";
        }
        public class Delimiters
        {
            public const char Record = '^';
            public const char Variable = '~';
            public const char DatasetDelimiter = '!';
        }
        public class Compression
        {
            public const string Brotli = "br";
            public const string GZip = "gzip";
            public const string Deflate = "deflate";
        }
        public class Proxy
        {
            public const string InternalField = "_";
            public const string PropertyGetMethod = "get_";
            public const string PropertySetMethod = "set_";
            public const string InterfaceEventAddMethod = "add_";
            public const string DynamicEventAddMethod = "Combine";
            public const string DynamicEventRemoveMethod = "Remove";
            public const string WebWorkerIdFieldName = "_webWorkerId";
            public const string InterfaceEventRemoveMethod = "remove_";
            public const string InvocationIdFieldName = "_invocationId";
            public const string FileControlIdFieldName = "_fileControlId";
            public const string EventRegistrationsFieldName = "_eventRegistrations";
        }
        public class Security
        {
            public const string B2CTokenSessionKeyFormat = "{0}-{1}.{2}-{3}-accesstoken-{4}--{5}--";
            public class Claims
            {
                public const string OID = "oid";
                public const string ID = "http://schemas.microsoft.com/identity/claims/objectidentifier";
            }
            public class Settings
            {
                public const string AppId = "b2c-app-id";
                public const string Policy = "b2c-policy";
                public const string Instance = "b2c-instance";
                public const string TenantId = "b2c-tenant-id";
                public const string AccessScope = "b2c-access-scope";
            }
        }
    }
}
