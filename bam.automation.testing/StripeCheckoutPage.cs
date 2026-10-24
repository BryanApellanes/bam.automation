using BamBot.Automation;
using PuppeteerSharp;
using PuppeteerSharp.Input;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// Page object for Stripe hosted Checkout (<c>checkout.stripe.com</c>): fills the card form from
    /// <see cref="EnvFileSecrets"/> (values never logged), dismisses a Link prompt, submits and waits for the
    /// redirect back to the merchant. Fields are looked up on the page and in its frames so a Stripe move into an
    /// iframe does not break the fill. Selectors live in <see cref="StripeCheckoutSelectors"/>.
    /// </summary>
    public class StripeCheckoutPage
    {
        /// <summary>
        /// The hosted Checkout host.
        /// </summary>
        public const string Host = "checkout.stripe.com";

        /// <summary>
        /// Creates the page object over an open page.
        /// </summary>
        /// <param name="page">The page, expected to be at (or redirecting to) hosted Checkout.</param>
        /// <param name="selectors">Selector overrides; null uses the defaults.</param>
        public StripeCheckoutPage(IAutomationPage page, StripeCheckoutSelectors? selectors = null)
        {
            Page = page ?? throw new ArgumentNullException(nameof(page));
            Selectors = selectors ?? new StripeCheckoutSelectors();
        }

        /// <summary>
        /// Gets the page.
        /// </summary>
        public IAutomationPage Page { get; }

        /// <summary>
        /// Gets the selectors.
        /// </summary>
        public StripeCheckoutSelectors Selectors { get; }

        /// <summary>
        /// Gets or sets how long to wait for the card form to render. Default 45 seconds.
        /// </summary>
        public TimeSpan FormTimeout { get; set; } = TimeSpan.FromSeconds(45);

        /// <summary>
        /// Gets or sets how long to wait for an optional field or a Link prompt before deciding it is not shown.
        /// Default 2 seconds.
        /// </summary>
        public TimeSpan OptionalFieldTimeout { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets a value indicating whether the url is on hosted Checkout: an absolute https url whose host is exactly
        /// <see cref="Host"/>. This is the gate before a live card is typed, so a lookalike host or a url that merely
        /// mentions the host does not pass.
        /// </summary>
        /// <param name="url">The url.</param>
        public static bool IsCheckoutUrl(string? url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && uri.Host.Equals(Host, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Waits until the card number field is present.
        /// </summary>
        public async Task WaitForFormAsync()
        {
            await Poll.UntilOrThrowAsync($"Stripe Checkout card form ({Selectors.CardNumber}) is present at {Page.GetCurrentUrl()}", () => IsPresentAnywhereAsync(Selectors.CardNumber), FormTimeout);
        }

        /// <summary>
        /// Fills the card form from the secrets: the email when Checkout asks for one, then number, expiry, CVC,
        /// name, and postal code and country when those fields are shown. Values are typed, never echoed.
        /// </summary>
        /// <param name="secrets">The secrets; see <see cref="StripeCheckoutSecretKeys"/>.</param>
        /// <exception cref="MissingSecretException">A required card value is blank — the run stops before paying.</exception>
        public async Task FillCardAsync(EnvFileSecrets secrets)
        {
            if (secrets == null)
            {
                throw new ArgumentNullException(nameof(secrets));
            }

            secrets.Require(StripeCheckoutSecretKeys.Required);
            await WaitForFormAsync();

            if (secrets.TryGet(StripeCheckoutSecretKeys.CustomerEmail, out string email))
            {
                await TypeIfPresentAsync(Selectors.Email, email);
            }

            await TypeAsync(Selectors.CardNumber, secrets.Get(StripeCheckoutSecretKeys.CardNumber));
            await TypeAsync(Selectors.CardExpiry, secrets.Get(StripeCheckoutSecretKeys.CardExpiry));
            await TypeAsync(Selectors.CardCvc, secrets.Get(StripeCheckoutSecretKeys.CardCvc));
            await TypeAsync(Selectors.BillingName, secrets.Get(StripeCheckoutSecretKeys.CardName));

            if (secrets.TryGet(StripeCheckoutSecretKeys.CardCountry, out string country))
            {
                await SelectIfPresentAsync(Selectors.BillingCountry, country);
            }

            if (secrets.TryGet(StripeCheckoutSecretKeys.CardPostalCode, out string postalCode))
            {
                await TypeIfPresentAsync(Selectors.BillingPostalCode, postalCode);
            }
        }

        /// <summary>
        /// Dismisses a Stripe Link prompt if one is shown, trying each <see cref="StripeCheckoutSelectors.LinkDismiss"/>
        /// selector in order.
        /// </summary>
        /// <returns>True if a prompt was dismissed.</returns>
        public async Task<bool> DismissLinkIfShownAsync()
        {
            foreach (string selector in Selectors.LinkDismiss)
            {
                if (await Page.IsPresentAsync(selector))
                {
                    await Page.ClickAsync(selector);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Clicks the pay button.
        /// </summary>
        public async Task SubmitAsync()
        {
            await JourneyStep.WaitForSelectorOrThrowAsync(Page, Selectors.Submit, FormTimeout);
            await Page.ClickAsync(Selectors.Submit);
        }

        /// <summary>
        /// Waits until the browser has left hosted Checkout for a url satisfying the predicate — the merchant's
        /// success page. A headed run gives the owner time to clear a Link or 3-D Secure interstitial.
        /// </summary>
        /// <param name="isDestination">Returns true for the expected destination url.</param>
        /// <param name="timeout">How long to wait.</param>
        /// <returns>The destination url.</returns>
        public async Task<string> WaitForRedirectAsync(Func<string, bool> isDestination, TimeSpan timeout)
        {
            if (isDestination == null)
            {
                throw new ArgumentNullException(nameof(isDestination));
            }

            string lastUrl = string.Empty;
            bool arrived = await Poll.UntilAsync(() =>
            {
                lastUrl = Page.GetCurrentUrl();
                return !IsCheckoutUrl(lastUrl) && isDestination(lastUrl);
            }, timeout);
            if (!arrived)
            {
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0.#}s waiting for the redirect away from Stripe Checkout; last url was {lastUrl}");
            }

            return lastUrl;
        }

        /// <summary>
        /// Waits until the browser has left hosted Checkout for a url containing the fragment (case-insensitive).
        /// </summary>
        /// <param name="expectedHostOrPath">Text the destination url contains, such as the merchant host or success path.</param>
        /// <param name="timeout">How long to wait.</param>
        /// <returns>The destination url.</returns>
        public Task<string> WaitForRedirectAsync(string expectedHostOrPath, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(expectedHostOrPath))
            {
                throw new ArgumentException("An expected host or path is required", nameof(expectedHostOrPath));
            }

            return WaitForRedirectAsync((url) => url.Contains(expectedHostOrPath, StringComparison.OrdinalIgnoreCase), timeout);
        }

        /// <summary>
        /// Creates the journey steps for paying on hosted Checkout: wait for the Checkout url, fill the card, dismiss
        /// Link, submit, wait for the redirect. The secrets are attached to the steps for scrubbing.
        /// </summary>
        /// <param name="secrets">The card secrets.</param>
        /// <param name="redirectContains">Text the post-payment url contains.</param>
        /// <param name="redirectTimeout">How long to wait for the redirect; defaults to 3 minutes so the owner can clear interstitials.</param>
        /// <param name="selectors">Selector overrides; null uses the defaults.</param>
        /// <returns>The steps.</returns>
        public static IEnumerable<JourneyStep> PaymentSteps(EnvFileSecrets secrets, string redirectContains, TimeSpan? redirectTimeout = null, StripeCheckoutSelectors? selectors = null)
        {
            if (secrets == null)
            {
                throw new ArgumentNullException(nameof(secrets));
            }

            TimeSpan redirectWait = redirectTimeout ?? TimeSpan.FromMinutes(3);
            List<JourneyStep> steps = new List<JourneyStep>
            {
                JourneyStep.WaitForUrl($"Stripe Checkout ({Host})", IsCheckoutUrl, TimeSpan.FromSeconds(60)),
                // the filled form shows the card in clear and screenshots are not scrubbed: never capture these three
                WithSecrets(new JourneyStep("Fill card on Stripe Checkout", (page) => new StripeCheckoutPage(page, selectors).FillCardAsync(secrets)) { ScreenshotPolicy = ScreenshotPolicy.Never }, secrets),
                WithSecrets(new JourneyStep("Dismiss Stripe Link if shown", (page) => new StripeCheckoutPage(page, selectors).DismissLinkIfShownAsync()) { ScreenshotPolicy = ScreenshotPolicy.Never }, secrets),
                WithSecrets(new JourneyStep("Submit payment", (page) => new StripeCheckoutPage(page, selectors).SubmitAsync()) { Navigates = true, ScreenshotPolicy = ScreenshotPolicy.Never }, secrets),
                // while the redirect has not landed the browser is still on the filled form: capture the merchant page only once it has
                WithSecrets(new JourneyStep($"Wait for redirect to '{redirectContains}'", (page) => new StripeCheckoutPage(page, selectors).WaitForRedirectAsync(redirectContains, redirectWait)) { Navigates = true, ScreenshotPolicy = ScreenshotPolicy.OnSuccess }, secrets)
            };
            return steps;
        }

        private static JourneyStep WithSecrets(JourneyStep step, EnvFileSecrets secrets)
        {
            step.Secrets.Add(secrets);
            return step;
        }

        private async Task TypeAsync(string selector, string value)
        {
            IElementHandle? handle = await FindAnywhereAsync(selector);
            if (handle == null)
            {
                await JourneyStep.WaitForSelectorOrThrowAsync(Page, selector, FormTimeout);
                handle = await FindAnywhereAsync(selector);
            }

            if (handle == null)
            {
                throw new InvalidOperationException($"Selector {selector} is present at {Page.GetCurrentUrl()} but no element handle could be resolved; the page object needs a real PuppeteerSharp page to type into Checkout");
            }

            // the persistent profile may have prefilled the field: select what is there so the typed value replaces it
            await handle.ClickAsync(new ClickOptions { Count = 3 });
            await handle.PressAsync("Backspace");
            await handle.TypeAsync(value);
        }

        private async Task TypeIfPresentAsync(string selector, string value)
        {
            bool present = await Poll.UntilAsync(() => IsPresentAnywhereAsync(selector), OptionalFieldTimeout);
            if (present)
            {
                await TypeAsync(selector, value);
            }
        }

        private async Task SelectIfPresentAsync(string selector, string value)
        {
            bool present = await Poll.UntilAsync(() => IsPresentAnywhereAsync(selector), OptionalFieldTimeout);
            if (!present)
            {
                return;
            }

            PuppeteerSharp.IPage? puppeteerPage = Page.Page;
            if (puppeteerPage == null)
            {
                return;
            }

            if (await Page.IsPresentAsync(selector))
            {
                await puppeteerPage.SelectAsync(selector, value);
                return;
            }

            foreach (IFrame frame in puppeteerPage.Frames)
            {
                IElementHandle? handle = await frame.QuerySelectorAsync(selector);
                if (handle != null)
                {
                    await frame.SelectAsync(selector, value);
                    return;
                }
            }
        }

        private async Task<bool> IsPresentAnywhereAsync(string selector)
        {
            return await FindAnywhereAsync(selector) != null || await Page.IsPresentAsync(selector);
        }

        private async Task<IElementHandle?> FindAnywhereAsync(string selector)
        {
            PuppeteerSharp.IPage? puppeteerPage = Page.Page;
            if (puppeteerPage == null)
            {
                return null;
            }

            IElementHandle? onPage = await puppeteerPage.QuerySelectorAsync(selector);
            if (onPage != null)
            {
                return onPage;
            }

            foreach (IFrame frame in puppeteerPage.Frames)
            {
                try
                {
                    IElementHandle? inFrame = await frame.QuerySelectorAsync(selector);
                    if (inFrame != null)
                    {
                        return inFrame;
                    }
                }
                catch
                {
                    // a detached frame throws; keep looking
                }
            }

            return null;
        }
    }
}
