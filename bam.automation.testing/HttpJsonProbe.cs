using System.Net.Http.Headers;
using System.Text;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// Out-of-band verification against a product API while a journey runs in the browser: read an order's status,
    /// poll until it changes, call an admin endpoint. Headers (bearer tokens, API keys) are set once and never
    /// appear in <see cref="ToString"/> or exceptions. JSON goes through the bam.base Newtonsoft extensions.
    /// </summary>
    public class HttpJsonProbe : IDisposable
    {
        private readonly HttpClient _client;
        private readonly bool _ownsClient;
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a probe with its own <see cref="HttpClient"/>.
        /// </summary>
        public HttpJsonProbe() : this(new HttpClient(), true)
        {
        }

        /// <summary>
        /// Creates a probe over an existing client, which the caller keeps ownership of.
        /// </summary>
        /// <param name="client">The client.</param>
        public HttpJsonProbe(HttpClient client) : this(client, false)
        {
        }

        private HttpJsonProbe(HttpClient client, bool ownsClient)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _ownsClient = ownsClient;
        }

        /// <summary>
        /// Gets or sets the request timeout. Default 30 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets the names of the headers sent with every request (values are not exposed).
        /// </summary>
        public IReadOnlyCollection<string> HeaderNames => _headers.Keys;

        /// <summary>
        /// Sends the header with every request.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>This probe.</returns>
        public HttpJsonProbe WithHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A header name is required", nameof(name));
            }

            _headers[name] = value ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Sends the header with every request, taking the value from the secrets.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="secrets">The secrets.</param>
        /// <param name="key">The key whose value is sent.</param>
        /// <returns>This probe.</returns>
        public HttpJsonProbe WithHeader(string name, EnvFileSecrets secrets, string key)
        {
            if (secrets == null)
            {
                throw new ArgumentNullException(nameof(secrets));
            }

            return WithHeader(name, secrets.Get(key));
        }

        /// <summary>
        /// Sends <c>Authorization: Bearer &lt;token&gt;</c> with every request.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>This probe.</returns>
        public HttpJsonProbe WithBearer(string token)
        {
            return WithHeader("Authorization", $"Bearer {token}");
        }

        /// <summary>
        /// Gets the url and deserializes the JSON body.
        /// </summary>
        /// <typeparam name="T">The body type.</typeparam>
        /// <param name="url">The url.</param>
        /// <returns>The deserialized body.</returns>
        /// <exception cref="HttpJsonProbeException">The response status was not successful.</exception>
        public async Task<T> GetJsonAsync<T>(string url)
        {
            string body = await SendAsync(HttpMethod.Get, url, null);
            return body.FromJson<T>();
        }

        /// <summary>
        /// Gets the url and returns the raw body.
        /// </summary>
        /// <param name="url">The url.</param>
        /// <returns>The body.</returns>
        /// <exception cref="HttpJsonProbeException">The response status was not successful.</exception>
        public Task<string> GetStringAsync(string url)
        {
            return SendAsync(HttpMethod.Get, url, null);
        }

        /// <summary>
        /// Posts the request as JSON and deserializes the JSON response.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="url">The url.</param>
        /// <param name="request">The request body; null posts an empty body.</param>
        /// <returns>The deserialized response.</returns>
        /// <exception cref="HttpJsonProbeException">The response status was not successful.</exception>
        public async Task<TResponse> PostJsonAsync<TRequest, TResponse>(string url, TRequest? request) where TRequest : class
        {
            string body = await SendAsync(HttpMethod.Post, url, request == null ? string.Empty : request.ToJson());
            return body.FromJson<TResponse>();
        }

        /// <summary>
        /// Posts the request as JSON and returns the raw response body.
        /// </summary>
        /// <typeparam name="TRequest">The request type.</typeparam>
        /// <param name="url">The url.</param>
        /// <param name="request">The request body; null posts an empty body.</param>
        /// <returns>The response body.</returns>
        /// <exception cref="HttpJsonProbeException">The response status was not successful.</exception>
        public Task<string> PostJsonAsync<TRequest>(string url, TRequest? request) where TRequest : class
        {
            return SendAsync(HttpMethod.Post, url, request == null ? string.Empty : request.ToJson());
        }

        /// <summary>
        /// Gets the url repeatedly until the deserialized body satisfies the predicate. Unsuccessful responses and
        /// transport errors are treated as "not yet" until the timeout, when the last error is thrown.
        /// </summary>
        /// <typeparam name="T">The body type.</typeparam>
        /// <param name="url">The url.</param>
        /// <param name="predicate">Returns true when the body is the one being waited for.</param>
        /// <param name="timeout">How long to keep polling.</param>
        /// <param name="interval">The polling interval; defaults to 5 seconds.</param>
        /// <returns>The body that satisfied the predicate.</returns>
        /// <exception cref="TimeoutException">The predicate was never satisfied.</exception>
        public async Task<T> WaitForAsync<T>(string url, Func<T, bool> predicate, TimeSpan timeout, TimeSpan? interval = null)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            T? last = default;
            Exception? lastError = null;
            bool satisfied = await Poll.UntilAsync(async () =>
            {
                try
                {
                    last = await GetJsonAsync<T>(url);
                    lastError = null;
                    return predicate(last);
                }
                catch (HttpJsonProbeException ex)
                {
                    lastError = ex;
                    return false;
                }
                catch (HttpRequestException ex)
                {
                    lastError = ex;
                    return false;
                }
            }, timeout, interval ?? TimeSpan.FromSeconds(5));

            if (!satisfied)
            {
                string detail = lastError != null ? $"; last error: {lastError.Message}" : last != null ? $"; last body: {last.ToJson()}" : string.Empty;
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0.#}s waiting for {url} to satisfy the predicate{detail}");
            }

            return last!;
        }

        /// <summary>
        /// Describes the probe without header values.
        /// </summary>
        public override string ToString()
        {
            return $"{nameof(HttpJsonProbe)}(headers: {string.Join(", ", _headers.Keys)})";
        }

        /// <summary>
        /// Disposes the client when this probe created it.
        /// </summary>
        public void Dispose()
        {
            if (_ownsClient)
            {
                _client.Dispose();
            }
        }

        private async Task<string> SendAsync(HttpMethod method, string url, string? jsonBody)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("A url is required", nameof(url));
            }

            using HttpRequestMessage request = new HttpRequestMessage(method, url);
            foreach (KeyValuePair<string, string> header in _headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (jsonBody != null)
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            using CancellationTokenSource cancellation = new CancellationTokenSource(Timeout);
            using HttpResponseMessage response = await _client.SendAsync(request, cancellation.Token);
            string body = await response.Content.ReadAsStringAsync(cancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpJsonProbeException(method.Method, url, response.StatusCode, body);
            }

            return body;
        }
    }
}
