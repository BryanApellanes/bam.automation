using BamBot;
using BamBot.Automation;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// One named action in a <see cref="Journey"/>: something the user does (navigate, click, type), something the
    /// test waits for (a selector, a url, an arbitrary condition) or an assertion. A step owns its optional
    /// <see cref="PageAssertion"/>s and screenshot policy; the journey turns it into a
    /// <see cref="PageAction"/> on its <see cref="PageActionSequence"/>.
    /// </summary>
    public class JourneyStep
    {
        /// <summary>
        /// The timeout used by the wait builders when none is given: 30 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Creates a step.
        /// </summary>
        /// <param name="name">The step name as it appears in results and the report.</param>
        /// <param name="action">The action; throw to fail the step.</param>
        public JourneyStep(string name, Func<IAutomationPage, Task> action)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A step name is required", nameof(name));
            }

            Name = name;
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        /// <summary>
        /// Gets the step name as it appears in results and the report.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the action; it throws to fail the step.
        /// </summary>
        public Func<IAutomationPage, Task> Action { get; }

        /// <summary>
        /// Gets the assertions evaluated after the action; any that does not pass fails the step.
        /// </summary>
        public List<PageAssertion> Assertions { get; } = new List<PageAssertion>();

        /// <summary>
        /// Gets or sets when a screenshot is captured for this step. Default <see cref="ScreenshotPolicy.Inherit"/>.
        /// </summary>
        public ScreenshotPolicy ScreenshotPolicy { get; set; } = ScreenshotPolicy.Inherit;

        /// <summary>
        /// Gets or sets a file name (without extension) for this step's screenshot; null derives one from the
        /// step number and name.
        /// </summary>
        public string? ScreenshotName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the action causes a navigation; mirrored to
        /// <see cref="PageAction.Navigates"/> so the sequence settles after it.
        /// </summary>
        public bool Navigates { get; set; }

        /// <summary>
        /// Gets the secrets whose values must be scrubbed from anything this step reports.
        /// </summary>
        public List<EnvFileSecrets> Secrets { get; } = new List<EnvFileSecrets>();

        /// <summary>
        /// Adds an assertion evaluated after the action.
        /// </summary>
        /// <param name="assertion">The assertion.</param>
        /// <returns>This step.</returns>
        public JourneyStep Expect(PageAssertion assertion)
        {
            Assertions.Add(assertion ?? throw new ArgumentNullException(nameof(assertion)));
            return this;
        }

        /// <summary>
        /// Adds an assertion that the selector is present after the action.
        /// </summary>
        /// <param name="selector">The selector that should exist.</param>
        /// <returns>This step.</returns>
        public JourneyStep ExpectSelector(string selector)
        {
            return Expect(new PageAssertion($"{Name}: {selector} is present", selector));
        }

        /// <summary>
        /// Sets the screenshot policy.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>This step.</returns>
        public JourneyStep WithScreenshot(ScreenshotPolicy policy)
        {
            ScreenshotPolicy = policy;
            return this;
        }

        /// <summary>
        /// Marks the step as causing navigation.
        /// </summary>
        /// <returns>This step.</returns>
        public JourneyStep ThatNavigates()
        {
            Navigates = true;
            return this;
        }

        /// <summary>
        /// Converts the step to a <see cref="PageAction"/> whose action is the specified wrapper (the journey
        /// supplies one that records timing, screenshots and results).
        /// </summary>
        /// <param name="wrapper">The function the page action invokes.</param>
        /// <returns>The page action.</returns>
        public PageAction ToPageAction(Func<IAutomationPage, Task<PageActionResult>> wrapper)
        {
            return new PageAction(Name, wrapper) { Navigates = Navigates };
        }

        /// <summary>
        /// Creates a step that navigates to the url.
        /// </summary>
        /// <param name="url">The url.</param>
        public static JourneyStep Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("A url is required", nameof(url));
            }

            return new JourneyStep($"Navigate to {url}", async (page) =>
            {
                await page.GoToAsync(url);
                page.Url = url;
            }).ThatNavigates();
        }

        /// <summary>
        /// Creates a step that waits for the selector and clicks it.
        /// </summary>
        /// <param name="selector">The selector.</param>
        /// <param name="timeout">How long to wait for the selector; defaults to <see cref="DefaultWaitTimeout"/>.</param>
        public static JourneyStep Click(string selector, TimeSpan? timeout = null)
        {
            RequireSelector(selector);
            TimeSpan wait = timeout ?? DefaultWaitTimeout;
            return new JourneyStep($"Click {selector}", async (page) =>
            {
                await WaitForSelectorOrThrowAsync(page, selector, wait);
                await page.ClickAsync(selector);
            });
        }

        /// <summary>
        /// Creates a step that waits for the selector, focuses it and types the text. For secret values use
        /// <see cref="TypeSecret"/> so the value never appears in results.
        /// </summary>
        /// <param name="selector">The input selector.</param>
        /// <param name="text">The text to type.</param>
        /// <param name="timeout">How long to wait for the selector; defaults to <see cref="DefaultWaitTimeout"/>.</param>
        public static JourneyStep Type(string selector, string text, TimeSpan? timeout = null)
        {
            RequireSelector(selector);
            TimeSpan wait = timeout ?? DefaultWaitTimeout;
            return new JourneyStep($"Type into {selector}", async (page) =>
            {
                await WaitForSelectorOrThrowAsync(page, selector, wait);
                await page.KeysAsync(selector, text ?? string.Empty);
            });
        }

        /// <summary>
        /// Creates a step that types the secret for the key into the selector. The step name carries the key, never
        /// the value, and the secrets are registered for scrubbing any message the step produces.
        /// </summary>
        /// <param name="selector">The input selector.</param>
        /// <param name="secrets">The secrets.</param>
        /// <param name="key">The key whose value is typed.</param>
        /// <param name="timeout">How long to wait for the selector; defaults to <see cref="DefaultWaitTimeout"/>.</param>
        /// <remarks>
        /// Screenshots are not scrubbed, so this step's <see cref="ScreenshotPolicy"/> is <c>Never</c> regardless of the
        /// journey default: the typed value would otherwise be captured in the step's PNG.
        /// </remarks>
        public static JourneyStep TypeSecret(string selector, EnvFileSecrets secrets, string key, TimeSpan? timeout = null)
        {
            RequireSelector(selector);
            if (secrets == null)
            {
                throw new ArgumentNullException(nameof(secrets));
            }

            TimeSpan wait = timeout ?? DefaultWaitTimeout;
            JourneyStep step = new JourneyStep($"Type secret {key} into {selector}", async (page) =>
            {
                string value = secrets.Get(key);
                await WaitForSelectorOrThrowAsync(page, selector, wait);
                await page.KeysAsync(selector, value);
            });
            step.ScreenshotPolicy = ScreenshotPolicy.Never;
            step.Secrets.Add(secrets);
            return step;
        }

        /// <summary>
        /// Creates a step that waits until the selector is present.
        /// </summary>
        /// <param name="selector">The selector.</param>
        /// <param name="timeout">How long to wait; defaults to <see cref="DefaultWaitTimeout"/>.</param>
        public static JourneyStep WaitForSelector(string selector, TimeSpan? timeout = null)
        {
            RequireSelector(selector);
            TimeSpan wait = timeout ?? DefaultWaitTimeout;
            return new JourneyStep($"Wait for {selector}", (page) => WaitForSelectorOrThrowAsync(page, selector, wait));
        }

        /// <summary>
        /// Creates a step that waits until the current url satisfies the predicate — the way to follow cross-origin
        /// redirects such as a hosted checkout.
        /// </summary>
        /// <param name="description">What url is expected, for the step name and timeout message.</param>
        /// <param name="isExpectedUrl">Returns true when the url is the one being waited for.</param>
        /// <param name="timeout">How long to wait; defaults to <see cref="DefaultWaitTimeout"/>.</param>
        /// <param name="interval">The polling interval; defaults to <see cref="Poll.DefaultInterval"/>.</param>
        public static JourneyStep WaitForUrl(string description, Func<string, bool> isExpectedUrl, TimeSpan? timeout = null, TimeSpan? interval = null)
        {
            if (isExpectedUrl == null)
            {
                throw new ArgumentNullException(nameof(isExpectedUrl));
            }

            TimeSpan wait = timeout ?? DefaultWaitTimeout;
            return new JourneyStep($"Wait for url: {description}", async (page) =>
            {
                string lastUrl = string.Empty;
                bool arrived = await Poll.UntilAsync(() =>
                {
                    lastUrl = page.GetCurrentUrl();
                    return isExpectedUrl(lastUrl);
                }, wait, interval);
                if (!arrived)
                {
                    throw new TimeoutException($"Timed out after {wait.TotalSeconds:0.#}s waiting for url {description}; last url was {lastUrl}");
                }
            }).ThatNavigates();
        }

        /// <summary>
        /// Creates a step that waits until the url contains the fragment (case-insensitive).
        /// </summary>
        /// <param name="urlFragment">Text the url should contain, such as a host or a path.</param>
        /// <param name="timeout">How long to wait; defaults to <see cref="DefaultWaitTimeout"/>.</param>
        public static JourneyStep WaitForUrl(string urlFragment, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(urlFragment))
            {
                throw new ArgumentException("A url fragment is required", nameof(urlFragment));
            }

            return WaitForUrl($"contains '{urlFragment}'", (url) => url.Contains(urlFragment, StringComparison.OrdinalIgnoreCase), timeout);
        }

        /// <summary>
        /// Creates a step that polls an arbitrary condition — an order status read out of band, an email arriving —
        /// until it is true.
        /// </summary>
        /// <param name="description">What is being waited for, for the step name and timeout message.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="timeout">How long to wait; defaults to <see cref="DefaultWaitTimeout"/>.</param>
        /// <param name="interval">The polling interval; defaults to <see cref="Poll.DefaultInterval"/>.</param>
        public static JourneyStep WaitUntil(string description, Func<Task<bool>> condition, TimeSpan? timeout = null, TimeSpan? interval = null)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            TimeSpan wait = timeout ?? DefaultWaitTimeout;
            return new JourneyStep($"Wait until {description}", (page) => Poll.UntilOrThrowAsync(description, condition, wait, interval));
        }

        /// <summary>
        /// Creates a step whose condition receives the page, see <see cref="WaitUntil(string, Func{Task{bool}}, TimeSpan?, TimeSpan?)"/>.
        /// </summary>
        /// <param name="description">What is being waited for.</param>
        /// <param name="condition">The condition, given the page.</param>
        /// <param name="timeout">How long to wait; defaults to <see cref="DefaultWaitTimeout"/>.</param>
        /// <param name="interval">The polling interval; defaults to <see cref="Poll.DefaultInterval"/>.</param>
        public static JourneyStep WaitUntil(string description, Func<IAutomationPage, Task<bool>> condition, TimeSpan? timeout = null, TimeSpan? interval = null)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            TimeSpan wait = timeout ?? DefaultWaitTimeout;
            return new JourneyStep($"Wait until {description}", (page) => Poll.UntilOrThrowAsync(description, () => condition(page), wait, interval));
        }

        /// <summary>
        /// Creates a step that only evaluates the assertion.
        /// </summary>
        /// <param name="assertion">The assertion.</param>
        public static JourneyStep Assert(PageAssertion assertion)
        {
            if (assertion == null)
            {
                throw new ArgumentNullException(nameof(assertion));
            }

            return new JourneyStep($"Assert {assertion.Name}", (page) => Task.CompletedTask).Expect(assertion);
        }

        /// <summary>
        /// Creates a step that captures a named screenshot and does nothing else.
        /// </summary>
        /// <param name="name">The screenshot file name (without extension).</param>
        public static JourneyStep Screenshot(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A screenshot name is required", nameof(name));
            }

            return new JourneyStep($"Screenshot {name}", (page) => Task.CompletedTask)
            {
                ScreenshotPolicy = ScreenshotPolicy.Always,
                ScreenshotName = name
            };
        }

        /// <summary>
        /// Creates a step from an arbitrary action.
        /// </summary>
        /// <param name="name">The step name.</param>
        /// <param name="action">The action; throw to fail the step.</param>
        public static JourneyStep Custom(string name, Func<IAutomationPage, Task> action)
        {
            return new JourneyStep(name, action);
        }

        /// <summary>
        /// Waits for the selector and throws a <see cref="TimeoutException"/> when it never appears.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="selector">The selector.</param>
        /// <param name="timeout">How long to wait.</param>
        public static async Task WaitForSelectorOrThrowAsync(IAutomationPage page, string selector, TimeSpan timeout)
        {
            bool found = await Poll.UntilAsync(() => page.IsPresentAsync(selector), timeout);
            if (!found)
            {
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0.#}s waiting for selector {selector} at {page.GetCurrentUrl()}");
            }
        }

        private static void RequireSelector(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("A selector is required", nameof(selector));
            }
        }
    }
}
