using BamBot.Automation;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// Helpers over <see cref="IAutomationPage"/> used by journey steps and page objects.
    /// </summary>
    public static class AutomationPageExtensions
    {
        /// <summary>
        /// Gets the url the browser is currently at. <see cref="IPage.Url"/> on <see cref="AutomationPage"/> returns
        /// the url the page was opened with and does not follow navigation, so this prefers the live PuppeteerSharp
        /// page url and falls back to <see cref="IPage.Url"/> when no PuppeteerSharp page is attached (fakes).
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>The current url, or an empty string when neither source has one.</returns>
        public static string GetCurrentUrl(this IAutomationPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            string? liveUrl = null;
            try
            {
                liveUrl = page.Page?.Url;
            }
            catch
            {
                // a closed page throws; fall through to the recorded url
            }

            return liveUrl ?? page.Url ?? string.Empty;
        }
    }
}
