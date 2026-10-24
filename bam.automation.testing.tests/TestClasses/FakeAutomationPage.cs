using BamBot.Automation;
using PuppeteerSharp;

namespace Bam.Automation.Testing.Tests.TestClasses
{
    /// <summary>
    /// An <see cref="IAutomationPage"/> with no browser behind it: records every action, simulates navigation on
    /// configured clicks, and can be told to fail on a selector. Lets the journey engine be tested without Chromium.
    /// </summary>
    public class FakeAutomationPage : IAutomationPage
    {
        public FakeAutomationPage(string url = "https://fixture.test/start")
        {
            Name = "fake";
            Url = url;
        }

        public string Name { get; set; }

        public string Url { get; set; }

        public PuppeteerSharp.IPage Page { get; set; } = null!;

        public string ScreenShotsDirectory { get; set; } = null!;

        public List<string> Log { get; } = new List<string>();

        public HashSet<string> PresentSelectors { get; } = new HashSet<string>(StringComparer.Ordinal);

        public HashSet<string> FailingSelectors { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Dictionary<string, string> ClickNavigatesTo { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Dictionary<string, string> TypedValues { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Dictionary<string, string> Texts { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public int ScreenshotCount { get; private set; }

        public Task<IElementHandle[]> QuerySelectorAllAsync(string selector)
        {
            return Task.FromResult(Array.Empty<IElementHandle>());
        }

        public Task<IElementHandle> QuerySelectorAsync(string selector)
        {
            return Task.FromResult<IElementHandle>(null!);
        }

        public Task<bool> IsPresentAsync(string selector)
        {
            return Task.FromResult(PresentSelectors.Contains(selector));
        }

        public Task AssertElementIsPresentAsync(string selector)
        {
            if (!PresentSelectors.Contains(selector))
            {
                throw new AutomationAssertionException(this, $"No element found matching selector: ({selector})");
            }

            return Task.CompletedTask;
        }

        public Task AssertElementIsNotPresentAsync(string selector, int afterMilliseconds = 300)
        {
            if (PresentSelectors.Contains(selector))
            {
                throw new AutomationAssertionException(this, $"Expected not to find element matching selector: ({selector})");
            }

            return Task.CompletedTask;
        }

        public async Task AssertIsAtPathAsync(string path, int afterMilliseconds = 300)
        {
            if (!await IsAtPathAsync(path))
            {
                throw new AutomationAssertionException(this, $"Expected to be at path {path}");
            }
        }

        public Task<bool> IsAtPathAsync(string path)
        {
            return Task.FromResult(new Uri(Url).AbsolutePath.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        public Task<string> GetElementTextAsync(string selector)
        {
            return Task.FromResult(Texts.TryGetValue(selector, out string? text) ? text : string.Empty);
        }

        public Task<string> GetElementValueAsync(string selector)
        {
            return Task.FromResult(TypedValues.TryGetValue(selector, out string? value) ? value : string.Empty);
        }

        public Task<string[]> GetAllElementTextAsync(string selector)
        {
            return Task.FromResult(Texts.Where(pair => pair.Key == selector).Select(pair => pair.Value).ToArray());
        }

        public Task KeysAsync(string keyboardInput)
        {
            Log.Add("keys");
            return Task.CompletedTask;
        }

        public Task KeysAsync(string inputSelector, string keyboardInput)
        {
            if (FailingSelectors.Contains(inputSelector))
            {
                // deliberately echoes the typed value so scrubbing can be proven
                throw new InvalidOperationException($"could not type '{keyboardInput}' into {inputSelector}");
            }

            TypedValues[inputSelector] = keyboardInput;
            Log.Add($"type:{inputSelector}");
            return Task.CompletedTask;
        }

        public Task ClickAsync(string selector)
        {
            if (FailingSelectors.Contains(selector))
            {
                throw new InvalidOperationException($"click failed on {selector}");
            }

            Log.Add($"click:{selector}");
            if (ClickNavigatesTo.TryGetValue(selector, out string? destination))
            {
                Url = destination;
            }

            return Task.CompletedTask;
        }

        public Task<IResponse> WaitForNavigationAsync(NavigationOptions options = null!)
        {
            return Task.FromResult<IResponse>(null!);
        }

        public Task<bool> WaitForElementAsync(string selector, int timeout = 5000)
        {
            return Task.FromResult(PresentSelectors.Contains(selector));
        }

        public Task GoAsync()
        {
            Log.Add("go");
            return Task.CompletedTask;
        }

        public Task<IResponse> GoToAsync(string url, int? timeout = null, WaitUntilNavigation[] waitUntil = null!)
        {
            Url = url;
            Log.Add($"goto:{url}");
            return Task.FromResult<IResponse>(null!);
        }

        public Task<IResponse> GoToPathAsync(string path, int? timeout = null, WaitUntilNavigation[] waitUntil = null!)
        {
            Uri current = new Uri(Url);
            return GoToAsync($"{current.Scheme}://{current.Authority}{(path.StartsWith('/') ? path : "/" + path)}", timeout, waitUntil);
        }

        public Task ScreenshotAsync(string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            ScreenshotCount++;
            Log.Add($"screenshot:{Path.GetFileName(file)}");
            return Task.CompletedTask;
        }
    }
}
