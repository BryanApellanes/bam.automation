using PuppeteerSharp;
using System.Threading.Tasks;

namespace BamBot.Automation
{
    public interface IAutomationPage : IPage
    {
        Page Page { get; set; }
        string ScreenShotsDirectory{ get; set; }
        Task<ElementHandle[]> QuerySelectorAllAsync(string selector);
        Task<ElementHandle> QuerySelectorAsync(string selector);
        Task<bool> IsPresentAsync(string selector);
        Task AssertElementIsPresentAsync(string selector);
        Task AssertElementIsNotPresentAsync(string selector, int afterMilliseconds = 300);
        Task AssertIsAtPathAsync(string path, int afterMilliseconds = 300);
        Task<bool> IsAtPathAsync(string path);
        Task<string> GetElementTextAsync(string selector);
        Task<string> GetElementValueAsync(string selector);
        Task<string[]> GetAllElementTextAsync(string selector);
        Task KeysAsync(string keyboardInput);
        Task KeysAsync(string inputSelector, string keyboardInput);
        Task ClickAsync(string selector);
        Task<Response> WaitForNavigationAsync(NavigationOptions options = null);
        Task<bool> WaitForElementAsync(string selector, int timeout = 5000);
        /// <summary>
        /// Goes to the url of the current page, effectively refreshing the page.
        /// </summary>
        /// <returns></returns>
        Task GoAsync();

        /// <summary>
        /// Without changing the current host, goes to the specified path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="timeout"></param>
        /// <param name="waitUntil"></param>
        /// <returns></returns>
        Task<Response> GoToPathAsync(string path, int? timeout = null, WaitUntilNavigation[] waitUntil = null);

    }
}
