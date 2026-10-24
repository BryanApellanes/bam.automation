using System.Net;
using System.Net.Sockets;
using System.Text;
using Bam.Test;

namespace Bam.Automation.Testing.Tests.Unit
{
    [UnitTestMenu("HttpJsonProbeShould")]
    public class HttpJsonProbeShould : UnitTestMenuContainer
    {
        [UnitTest]
        public void GetJsonAndForwardHeaders()
        {
            using LocalJsonServer server = LocalJsonServer.Start();
            When.A<HttpJsonProbe>("gets json with a header", () => new HttpJsonProbe().WithHeader("X-Api-Key", "k-123"), (probe) =>
            {
                OrderDto order = probe.GetJsonAsync<OrderDto>($"{server.BaseUrl}order").GetAwaiter().GetResult();
                return new GetOutcome(order, server.LastApiKey, probe.ToString(), probe.HeaderNames.ToArray());
            })
            .TheTest
            .ShouldPass<GetOutcome>((because, outcome) =>
            {
                because.ItsTrue("deserialized the status", outcome.Order.Status == "Paid");
                because.ItsTrue("deserialized the count", outcome.Order.Count == 1);
                because.ItsTrue("sent the header", outcome.ReceivedApiKey == "k-123");
                because.ItsTrue("ToString names the header", outcome.Description.Contains("X-Api-Key"));
                because.ItsFalse("ToString hides the value", outcome.Description.Contains("k-123"));
                because.ItsTrue("HeaderNames lists the header", outcome.HeaderNames.Contains("X-Api-Key"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void WaitForABodyThatSatisfiesThePredicate()
        {
            using LocalJsonServer server = LocalJsonServer.Start();
            When.A<HttpJsonProbe>("polls until the count reaches three", (probe) =>
                probe.WaitForAsync<OrderDto>($"{server.BaseUrl}order", (order) => order.Count >= 3, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(20)).GetAwaiter().GetResult())
            .TheTest
            .ShouldPass<OrderDto>((because, order) =>
            {
                because.ItsTrue("returned the satisfying body", order.Count == 3, $"count was {order.Count}");
                because.ItsTrue("the server saw three requests", server.RequestCount == 3, $"requests: {server.RequestCount}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void KeepPollingWhenOneRequestStallsPastTheTimeout()
        {
            using LocalJsonServer server = LocalJsonServer.Start();
            server.StallFirstRequestMilliseconds = 1500;
            When.A<HttpJsonProbe>("polls through a stalled first response", () => new HttpJsonProbe { Timeout = TimeSpan.FromMilliseconds(300) }, (probe) =>
                probe.WaitForAsync<OrderDto>($"{server.BaseUrl}order", (order) => order.Count >= 2, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(100)).GetAwaiter().GetResult())
            .TheTest
            .ShouldPass<OrderDto>((because, order) =>
            {
                because.ItsTrue("returned a body after the stall", order.Count >= 2, $"count was {order.Count}");
                because.ItsTrue("the server saw the stalled request and at least one more", server.RequestCount >= 2, $"requests: {server.RequestCount}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ThrowOnUnsuccessfulStatusWithBody()
        {
            using LocalJsonServer server = LocalJsonServer.Start();
            When.A<HttpJsonProbe>("gets a missing resource", (probe) =>
            {
                probe.GetJsonAsync<OrderDto>($"{server.BaseUrl}missing").GetAwaiter().GetResult();
            })
            .ExpectException(true)
            .TheTest
            .ShouldPass(because =>
            {
                HttpJsonProbeException? exception = because.TestCase.Exception?.GetBaseException() as HttpJsonProbeException;
                because.ItsTrue("threw HttpJsonProbeException", exception != null, because.TestCase.Exception?.GetType().Name);
                because.ItsTrue("carries the status", exception != null && exception.StatusCode == HttpStatusCode.NotFound);
                because.ItsTrue("carries the body", exception != null && exception.Body.Contains("not found"));
                because.ItsTrue("names the method and url", exception != null && exception.Message.StartsWith("GET ") && exception.Message.Contains("/missing"), exception?.Message);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void PostJsonAndReadTheResponse()
        {
            using LocalJsonServer server = LocalJsonServer.Start();
            When.A<HttpJsonProbe>("posts a refund request", (probe) =>
                probe.PostJsonAsync<RefundRequest, RefundResponse>($"{server.BaseUrl}refund", new RefundRequest { OrderId = "ord_1" }).GetAwaiter().GetResult())
            .TheTest
            .ShouldPass<RefundResponse>((because, response) =>
            {
                because.ItsTrue("the server echoed the order id", response.OrderId == "ord_1");
                because.ItsTrue("the server returned a refund id", response.RefundId == "re_1");
                because.ItsTrue("the request was json", server.LastContentType != null && server.LastContentType.StartsWith("application/json"), server.LastContentType);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void TimeOutWaitingAndReportTheLastError()
        {
            using LocalJsonServer server = LocalJsonServer.Start();
            When.A<HttpJsonProbe>("waits on a url that keeps failing", (probe) =>
            {
                probe.WaitForAsync<OrderDto>($"{server.BaseUrl}missing", (order) => true, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(30)).GetAwaiter().GetResult();
            })
            .ExpectException(true)
            .TheTest
            .ShouldPass(because =>
            {
                Exception? exception = because.TestCase.Exception?.GetBaseException();
                because.ItsTrue("threw TimeoutException", exception is TimeoutException, exception?.GetType().Name);
                because.ItsTrue("reports the last error", exception != null && exception.Message.Contains("404"), exception?.Message);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        public class OrderDto
        {
            public string Status { get; set; } = string.Empty;

            public int Count { get; set; }
        }

        public class RefundRequest
        {
            public string OrderId { get; set; } = string.Empty;
        }

        public class RefundResponse
        {
            public string OrderId { get; set; } = string.Empty;

            public string RefundId { get; set; } = string.Empty;
        }

        private sealed record GetOutcome(OrderDto Order, string? ReceivedApiKey, string Description, string[] HeaderNames);

        /// <summary>
        /// A loopback HTTP server answering /order (counts requests), /refund (echoes the order id) and 404 for the rest.
        /// </summary>
        private sealed class LocalJsonServer : IDisposable
        {
            private readonly HttpListener _listener;
            private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
            private int _requestCount;

            private LocalJsonServer(HttpListener listener, string baseUrl)
            {
                _listener = listener;
                BaseUrl = baseUrl;
                Task.Run(ServeAsync);
            }

            public string BaseUrl { get; }

            public int RequestCount => _requestCount;

            public string? LastApiKey { get; private set; }

            public string? LastContentType { get; private set; }

            /// <summary>
            /// Gets or sets how long the first /order request is held before answering (0 = answer immediately).
            /// </summary>
            public int StallFirstRequestMilliseconds { get; set; }

            public static LocalJsonServer Start()
            {
                int port = FreePort();
                string baseUrl = $"http://127.0.0.1:{port}/";
                HttpListener listener = new HttpListener();
                listener.Prefixes.Add(baseUrl);
                listener.Start();
                return new LocalJsonServer(listener, baseUrl);
            }

            public void Dispose()
            {
                _cancellation.Cancel();
                try
                {
                    _listener.Stop();
                    _listener.Close();
                }
                catch
                {
                    // already closed
                }
            }

            private async Task ServeAsync()
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _listener.GetContextAsync();
                    }
                    catch
                    {
                        return;
                    }

                    LastApiKey = context.Request.Headers["X-Api-Key"];
                    LastContentType = context.Request.ContentType;
                    string path = context.Request.Url?.AbsolutePath ?? "/";
                    string body;
                    int status = 200;
                    if (path == "/order")
                    {
                        int count = Interlocked.Increment(ref _requestCount);
                        if (count == 1 && StallFirstRequestMilliseconds > 0)
                        {
                            await Task.Delay(StallFirstRequestMilliseconds);
                        }

                        body = $"{{\"status\":\"Paid\",\"count\":{count}}}";
                    }
                    else if (path == "/refund")
                    {
                        using StreamReader reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                        string request = await reader.ReadToEndAsync();
                        RefundRequest refund = request.FromJson<RefundRequest>();
                        body = new RefundResponse { OrderId = refund.OrderId, RefundId = "re_1" }.ToJson();
                    }
                    else
                    {
                        status = 404;
                        body = "{\"error\":\"not found\"}";
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    try
                    {
                        context.Response.StatusCode = status;
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = bytes.Length;
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        context.Response.Close();
                    }
                    catch (HttpListenerException)
                    {
                        // the client gave up on this request (a stalled poll); keep serving
                    }
                    catch (ObjectDisposedException)
                    {
                        // ditto
                    }
                }
            }

            private static int FreePort()
            {
                TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                return port;
            }
        }
    }
}
