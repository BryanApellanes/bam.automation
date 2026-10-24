using System.Net;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// Thrown by <see cref="HttpJsonProbe"/> when a response is not successful. Carries the status and body; request
    /// headers (which may hold credentials) are never included.
    /// </summary>
    public class HttpJsonProbeException : Exception
    {
        /// <summary>
        /// Creates the exception.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="url">The request url.</param>
        /// <param name="statusCode">The response status.</param>
        /// <param name="body">The response body.</param>
        public HttpJsonProbeException(string method, string url, HttpStatusCode statusCode, string body)
            : base($"{method} {url} returned {(int)statusCode} {statusCode}: {Truncate(body)}")
        {
            Method = method;
            Url = url;
            StatusCode = statusCode;
            Body = body;
        }

        /// <summary>
        /// Gets the HTTP method.
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// Gets the request url.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Gets the response status.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the response body.
        /// </summary>
        public string Body { get; }

        private static string Truncate(string body)
        {
            const int max = 500;
            string text = body ?? string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }
}
