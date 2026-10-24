using BamBot.Automation;
using PuppeteerSharp;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// One browser process for a journey run, launched from <see cref="BrowserSessionOptions"/> (headed and
    /// persistent by default). Creates <see cref="AutomationPage"/>s bound to its browser; disposing the session
    /// closes the browser. Pages opened here do not close the browser when disposed (see
    /// <see cref="AutomationPage.OwnsBrowser"/>). <see cref="GetShared"/> hands out one process-wide session per
    /// option set, because the bam.test runner constructs a fresh container per test method.
    /// </summary>
    public sealed class BrowserSession : IDisposable
    {
        private static readonly object SharedLock = new object();
        private static readonly Dictionary<string, BrowserSession> Shared = new Dictionary<string, BrowserSession>(StringComparer.Ordinal);
        private static bool _exitHandlerAttached;

        private readonly object _launchLock = new object();
        private Task<IBrowser>? _launchTask;
        private bool _disposed;

        /// <summary>
        /// Creates a session that launches lazily on first use.
        /// </summary>
        /// <param name="options">The launch policy; null uses <see cref="BrowserSessionOptions.FromEnvironment()"/>.</param>
        public BrowserSession(BrowserSessionOptions? options = null)
        {
            Options = options ?? BrowserSessionOptions.FromEnvironment();
        }

        /// <summary>
        /// Gets the launch policy.
        /// </summary>
        public BrowserSessionOptions Options { get; }

        /// <summary>
        /// Gets a value indicating whether the browser has been launched.
        /// </summary>
        public bool IsLaunched => _launchTask != null && _launchTask.IsCompletedSuccessfully;

        /// <summary>
        /// Gets the browser, launching it on first access (blocking).
        /// </summary>
        public IBrowser Browser => LaunchAsync().GetAwaiter().GetResult();

        /// <summary>
        /// Launches the browser once; later calls return the same browser.
        /// </summary>
        /// <returns>The browser.</returns>
        public Task<IBrowser> LaunchAsync()
        {
            ThrowIfDisposed();
            lock (_launchLock)
            {
                if (_launchTask == null)
                {
                    EnsureProfileDirectory();
                    _launchTask = AutomationPage.LaunchBrowserAsync(Options.ToLaunchOptions());
                }

                return _launchTask;
            }
        }

        /// <summary>
        /// Opens a new tab at the url, sized to the session window, with the session's default navigation timeout.
        /// </summary>
        /// <param name="url">The url to open.</param>
        /// <param name="name">A name for the page in results and screenshots; defaults to the url host.</param>
        /// <returns>The page; disposing it closes only the tab.</returns>
        public async Task<AutomationPage> OpenAsync(string url, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("A url is required", nameof(url));
            }

            IBrowser browser = await LaunchAsync();
            string pageName = name ?? new Uri(url).Host;
            AutomationPage page = new AutomationPage(pageName, browser, string.Empty);
            page.Page.DefaultNavigationTimeout = (int)Options.DefaultNavigationTimeout.TotalMilliseconds;
            page.Page.DefaultTimeout = (int)Options.DefaultNavigationTimeout.TotalMilliseconds;
            await page.Page.SetViewportAsync(new ViewPortOptions { Width = Options.WindowWidth, Height = Options.WindowHeight });
            await page.Page.GoToAsync(url);
            page.Url = url;
            return page;
        }

        /// <summary>
        /// Opens a new tab at the url (blocking); see <see cref="OpenAsync"/>.
        /// </summary>
        /// <param name="url">The url to open.</param>
        /// <param name="name">A name for the page in results and screenshots; defaults to the url host.</param>
        /// <returns>The page.</returns>
        public AutomationPage Open(string url, string? name = null)
        {
            return OpenAsync(url, name).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets the process-wide session for the option set (by <see cref="BrowserSessionOptions.Key"/>), creating it
        /// on first request. Shared sessions are closed when the process exits or by <see cref="DisposeShared"/>.
        /// </summary>
        /// <param name="options">The launch policy; null uses <see cref="BrowserSessionOptions.FromEnvironment()"/>.</param>
        /// <returns>The shared session.</returns>
        public static BrowserSession GetShared(BrowserSessionOptions? options = null)
        {
            BrowserSessionOptions resolved = options ?? BrowserSessionOptions.FromEnvironment();
            lock (SharedLock)
            {
                if (!_exitHandlerAttached)
                {
                    AppDomain.CurrentDomain.ProcessExit += (sender, args) => DisposeShared();
                    _exitHandlerAttached = true;
                }

                if (!Shared.TryGetValue(resolved.Key, out BrowserSession? session) || session._disposed)
                {
                    session = new BrowserSession(resolved);
                    Shared[resolved.Key] = session;
                }

                return session;
            }
        }

        /// <summary>
        /// Closes every shared session's browser.
        /// </summary>
        public static void DisposeShared()
        {
            List<BrowserSession> sessions;
            lock (SharedLock)
            {
                sessions = Shared.Values.ToList();
                Shared.Clear();
            }

            foreach (BrowserSession session in sessions)
            {
                session.Dispose();
            }
        }

        /// <summary>
        /// Closes the browser if it was launched. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Task<IBrowser>? launchTask;
            lock (_launchLock)
            {
                launchTask = _launchTask;
            }

            if (launchTask == null || !launchTask.IsCompletedSuccessfully)
            {
                return;
            }

            IBrowser browser = launchTask.Result;
            try
            {
                browser.CloseAsync().Wait(TimeSpan.FromSeconds(10));
            }
            catch
            {
                // closing a browser that already died is fine
            }

            try
            {
                browser.Dispose();
            }
            catch
            {
                // nothing left to release
            }
        }

        private void EnsureProfileDirectory()
        {
            if (!string.IsNullOrWhiteSpace(Options.UserDataDirectory) && !Directory.Exists(Options.UserDataDirectory))
            {
                Directory.CreateDirectory(Options.UserDataDirectory);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BrowserSession));
            }
        }
    }
}
