namespace QBMS.Application.Models
{
    public class HrcdfSettings
    {
        /// <summary>
        /// Full URL of the HRCDF employee API. Use {personnelNo} as the placeholder,
        /// e.g. "https://hrcdf-host/api/employee/{personnelNo}".
        /// </summary>
        public string UrlTemplate { get; set; }

        /// <summary>Optional request header used to send an API key (e.g. "x-api-key").</summary>
        public string ApiKeyHeader { get; set; }

        public string ApiKey { get; set; }

        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// JSON property names (case-insensitive, searched anywhere in the response,
        /// in this order) that hold the employee's name. When empty, a built-in
        /// list of common names is used.
        /// </summary>
        public string[] NameFields { get; set; }
    }
}
