using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bam.Net;

namespace BamBot.Automation
{
    public class AutomationPage : IAutomationPage, IPage, IDisposable
    {
        static AutomationPage()
        {
            AppDomain.CurrentDomain.DomainUnload += (sender, args) => TryCloseBrowser();
        }

        /// <summary>
        /// Kills the browser process and releases related resources.
        /// </summary>
        public static void TryCloseBrowser()
        {
            try
            {
                browser.Process?.Kill();
            }
            catch { }

            try
            {
                browser.Disconnect();
            }
            catch { }

            try
            {
                _ = browser.CloseAsync();
            }
            catch { }

            try
            {
                browser.Dispose();
            }
            catch { }

            try
            {
                browser = null;
            }
            catch { }
        }

        public AutomationPage(string name, string url)
        {
            Name = name;
            Page = Browser.NewPageAsync().Result;
            Url = url;
            GoAsync().Wait();
            OnDebug = ((debugInfo) => { });
        }

        public AutomationPage(string name, Page puppeteerPage)
        {
            Name = name;
            Page = puppeteerPage;
            Url = puppeteerPage.Url;
        }

        static Task<RevisionInfo> fetchBrowserTask;
        protected static Task BeginFetchBrowserAsync()
        {
            if(fetchBrowserTask == null)
            {
                fetchBrowserTask = new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            }
            return fetchBrowserTask;
        }

        public static AutomationPage Open(string url)
        {
            return new AutomationPage(new Uri(url).Host, url);
        }


        static readonly object getBrowserLock = new object();
        static Browser browser;
        internal static Browser GetBrowser()
        {
            lock(getBrowserLock)
            {
                BeginFetchBrowserAsync().Wait();
                if(browser == null)
                {
                    browser = Puppeteer.LaunchAsync(new LaunchOptions { Headless = true }).Result;
                }
                
                return browser;
            }            
        }

        protected Browser Browser 
        { 
            get
            {
                return GetBrowser();
            }
        }
        public static NavigationOptions DefaultNavigationOptions => new NavigationOptions { Timeout = 15000 }; // our default 5 and half seconds
        public static NavigationOptions OriginalTimeoutNavigationOptions => new NavigationOptions { Timeout = 30000 }; // actual internal default explicitly set here for visibility

        public string ScreenShotsDirectory{ get; set; }

        public bool Debug { get; set; }

        public Action<AutomationPageDebugInfo> OnDebug{ get; set; }

        public string Name { get; set; }

        public Page Page { get; set; }

        string url;
        public string Url
        {
            get => url ?? Page?.Url ?? string.Empty;
            set => url = value;
        }

        public Task<Response> WaitForNavigationAsync(NavigationOptions options = null)
        {
            return Page.WaitForNavigationAsync(options ?? DefaultNavigationOptions);
        }

        /// <summary>
        /// Waits for the specified selector to be present on the page.
        /// </summary>
        /// <param name="selector">The selector to wait for.</param>
        /// <param name="timeout">The number of milliseconds to wait before this operation times out.</param>
        /// <returns>True if the element was present before timing out.</returns>
        public async Task<bool> WaitForElementAsync(string selector, int timeout = 5000)
        {
            // return Page.WaitForSelectorAsync(selector, new WaitForSelectorOptions { Timeout = timeout }); // this does not behave as expected
            System.Timers.Timer timer = new System.Timers.Timer(timeout);
            bool? keepWaiting = true;
            bool? selectorWasFound = false;

            timer.Elapsed += (s, a) => keepWaiting = false;
            timer.Enabled = true;
            timer.Start();
            while (keepWaiting.Value)
            {
                if (await IsPresentAsync(selector))
                {
                    selectorWasFound = true;
                    keepWaiting = false;
                }
                else if(Debug)
                {                    
                    await ScreenshotAsync(Path.Combine(ScreenShotsDirectory, $"{Name}_WaitingForElement"));
                }
            }
            return selectorWasFound.Value;
        }

        public async Task<string> GetElementValueAsync(string selector)
        {
            return await QuerySelectorAsync(selector).EvaluateFunctionAsync<string>("e => e.value");
        }

        public async Task<string> GetElementTextAsync(string selector)
        {
            return await QuerySelectorAsync(selector).EvaluateFunctionAsync<string>("e => e.innerText");
        }

        public async Task<string[]> GetAllElementTextAsync(string selector)
        {
            await fetchBrowserTask;
            List<string> results = new List<string>();
            foreach(ElementHandle elementHandle in await Page.QuerySelectorAllAsync(selector))
            {
                string text = await elementHandle.EvaluateFunctionAsync<string>("e => e.innerText");
                results.Add(text.Trim());
            }

            return results.ToArray();
        }

        public async Task<bool> IsAtPathAsync(string path)
        {
            PagePathAssertion assertion = new PagePathAssertion(path);
            return (await assertion.ExecuteAsync(this)).Passed;
        }

        public async Task<bool> IsPresentAsync(string selector)
        {
            await fetchBrowserTask;
            return await QuerySelectorAsync(selector) != null;
        }

        public async Task<bool> IsNotPresentAsync(string selector)
        {
            await fetchBrowserTask;
            return await QuerySelectorAsync(selector) == null;
        }

        public async Task AssertElementIsPresentAsync(string selector)
        {
            if(await IsNotPresentAsync(selector))
            {
                throw new AutomationAssertionException(this, $"No element found matching selector: ({selector})");
            }
        }

        public async Task AssertElementIsNotPresentAsync(string selector, int afterMilliseconds = 300)
        {
            Thread.Sleep(afterMilliseconds);
            if(await IsPresentAsync(selector))
            {
                throw new AutomationAssertionException(this, $"Expected not to find element matching selector: ({selector})");
            }
        }

        public async Task AssertIsAtPathAsync(string path, int afterMilliseconds = 300)
        {
            Thread.Sleep(afterMilliseconds);
            if(!(await IsAtPathAsync(path)))
            {
                throw new AutomationAssertionException(this, $"Expected to be at path {path}");
            }
        }

        public async Task<ElementHandle[]> QuerySelectorAllAsync(string selector)
        {
            await fetchBrowserTask;
            return await Page.QuerySelectorAllAsync(selector);
        }

        public async Task<ElementHandle> QuerySelectorAsync(string selector)
        {
            await fetchBrowserTask;
            return await Page.QuerySelectorAsync(selector);
        }

        public async Task ScreenshotAsync(string file)
        {
            await fetchBrowserTask;
            try
            {
                Page.ScreenshotAsync(file).Wait();
            }
            catch 
            {   
                // don't crash;
            }
        }

        public async Task GoAsync()
        {
            await fetchBrowserTask;
            await Page.SetViewportAsync(new ViewPortOptions { Width = 1200, Height = 800 });
            await GoToAsync(Url);
        }

        public async Task<Response> GoToAsync(string url, int? timeout = null, WaitUntilNavigation[] waitUntil = null)
        {
            await fetchBrowserTask;
            return await Page.GoToAsync(url, timeout, waitUntil);
        }

        public async Task<Response> GoToPathAsync(string path, int? timeout = null, WaitUntilNavigation[] waitUntil = null)
        {
            Uri uri = new Uri(Url);
            string host = uri.Host;
            if(uri.Port != 80)
            {
                host = $"{uri.Host}:{uri.Port}";
            }
            if(!path.StartsWith("/"))
            {
                path = $"/{path}";
            }
            Uri gotoUri = new Uri($"{uri.Scheme}://{host}{path}");
            return await GoToAsync(gotoUri.ToString());
        }

        public async Task KeysAsync(string keyboardInput)
        {
            await Page.Keyboard.TypeAsync(keyboardInput);
        }

        public async Task KeysAsync(string inputSelector, string keyboardInput)
        {
            await Page.FocusAsync(inputSelector);
            await Page.Keyboard.TypeAsync(keyboardInput);
        }

        public async Task ClickAsync(string selector)
        {
            await Page.ClickAsync(selector);
        }

        protected async Task<FileInfo> DebugAsync(string message)
        {
            return await DebugAsync(message, "debug.png");
        }

        protected async Task<FileInfo> DebugAsync(string message, string imageName)
        {
            if (Debug)
            {
                FileInfo file = await TakeDebugScreenShotAsync(imageName);
                OnDebug(new AutomationPageDebugInfo {Message = message, AutomationPage = this, ScreenShot = file});
                return file;
            }

            return null;
        }

        protected async Task<FileInfo> TakeDebugScreenShotAsync(string imageName)
        {
            if (Debug)
            {
                string path = Path.Combine(BamProfile.ScreenshotsPath, "debug", imageName);
                await ScreenshotAsync(path);
                return new FileInfo(path);
            }

            return null;
        }

        public void Dispose()
        {
            try
            {
                Page?.Dispose();
            }
            catch { }
            
            TryCloseBrowser();
        }
    }
}
