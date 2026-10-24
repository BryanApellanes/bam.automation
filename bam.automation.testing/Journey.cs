using System.Diagnostics;
using System.Text.RegularExpressions;
using BamBot;
using BamBot.Automation;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// A named, ordered list of <see cref="JourneyStep"/>s run against one page of a <see cref="BrowserSession"/> —
    /// what a real user does, end to end. Wraps a <see cref="PageActionSequence"/> (each step becomes a
    /// <see cref="PageAction"/> on <see cref="Sequence"/>) and adds what a monitored run needs: per-step timing,
    /// screenshots, stop-on-first-failure, secret scrubbing and a <see cref="JourneyResult"/> the test asserts on.
    /// </summary>
    public class Journey
    {
        private readonly List<JourneyStep> _steps = new List<JourneyStep>();
        private readonly List<EnvFileSecrets> _secrets = new List<EnvFileSecrets>();
        private readonly object _runLock = new object();
        private JourneyResult? _currentResult;
        private int _currentNumber;

        /// <summary>
        /// Creates a journey.
        /// </summary>
        /// <param name="name">The journey name, used for the sequence, the artifacts directory and the report.</param>
        public Journey(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A journey name is required", nameof(name));
            }

            Name = name;
            Sequence = new PageActionSequence(name);
            ArtifactsDirectory = Path.Combine(BamProfile.TestsPath, "journeys", Slug, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Notes = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets the journey name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a file-system-safe form of the name.
        /// </summary>
        public string Slug => ToSlug(Name);

        /// <summary>
        /// Gets the underlying sequence; every added step is also a <see cref="PageAction"/> here, so sequence
        /// events (<see cref="PageActionSequence.Error"/>, <see cref="PageActionSequence.Failure"/>,
        /// <see cref="PageActionSequence.Success"/>) fire as usual.
        /// </summary>
        public PageActionSequence Sequence { get; }

        /// <summary>
        /// Gets the steps in order.
        /// </summary>
        public IReadOnlyList<JourneyStep> Steps => _steps;

        /// <summary>
        /// Gets or sets the url the page is opened at when run against a <see cref="BrowserSession"/>. Null uses the
        /// first <see cref="JourneyStep.Navigate"/> step's url, else <c>about:blank</c>.
        /// </summary>
        public string? StartUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether later steps run after a failure. Default false: stop on the first
        /// failure and mark the rest <see cref="JourneyStepStatus.Skipped"/>.
        /// </summary>
        public bool ContinueOnFailure { get; set; }

        /// <summary>
        /// Gets or sets the screenshot policy for steps whose own policy is <see cref="ScreenshotPolicy.Inherit"/>.
        /// Default <see cref="ScreenshotPolicy.Always"/>: the report shows what the user saw at every step.
        /// </summary>
        public ScreenshotPolicy DefaultScreenshotPolicy { get; set; } = ScreenshotPolicy.Always;

        /// <summary>
        /// Gets or sets the directory screenshots (and, via <see cref="JourneyReport"/>, reports) are written to.
        /// Default <c>~/.bam/tests/journeys/&lt;slug&gt;/&lt;timestamp&gt;</c>.
        /// </summary>
        public string ArtifactsDirectory { get; set; }

        /// <summary>
        /// Gets report-safe notes copied onto the result: masked secrets, ids, anything the monitoring owner should
        /// see. Use <see cref="Note"/> and <see cref="NoteMasked"/>.
        /// </summary>
        public Dictionary<string, string> Notes { get; }

        /// <summary>
        /// Gets the result of the run in progress or the last run, or null before the first run.
        /// </summary>
        public JourneyResult? LastResult { get; private set; }

        /// <summary>
        /// Raised after each step completes (passed or failed) with the result so far.
        /// </summary>
        public event EventHandler<JourneyStepCompletedEventArgs>? StepCompleted;

        /// <summary>
        /// Raised when the run finishes with the final result.
        /// </summary>
        public event EventHandler<JourneyStepCompletedEventArgs>? Completed;

        /// <summary>
        /// Adds a step.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <returns>This journey.</returns>
        public Journey Add(JourneyStep step)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            lock (_runLock)
            {
                if (_currentResult != null && _currentResult.IsRunning)
                {
                    throw new InvalidOperationException("Steps cannot be added while the journey is running");
                }

                int number = _steps.Count + 1;
                _steps.Add(step);
                Sequence.AddStep(step.ToPageAction((page) => ExecuteStepAsync(number, step, page)));
            }

            return this;
        }

        /// <summary>
        /// Adds steps in order.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <returns>This journey.</returns>
        public Journey AddRange(IEnumerable<JourneyStep> steps)
        {
            foreach (JourneyStep step in steps ?? throw new ArgumentNullException(nameof(steps)))
            {
                Add(step);
            }

            return this;
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.Navigate"/> step; the first one also sets <see cref="StartUrl"/>.
        /// </summary>
        /// <param name="url">The url.</param>
        public Journey Navigate(string url)
        {
            StartUrl ??= url;
            return Add(JourneyStep.Navigate(url));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.Click"/> step.
        /// </summary>
        /// <param name="selector">The selector.</param>
        /// <param name="timeout">How long to wait for the selector.</param>
        public Journey Click(string selector, TimeSpan? timeout = null)
        {
            return Add(JourneyStep.Click(selector, timeout));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.Type"/> step.
        /// </summary>
        /// <param name="selector">The input selector.</param>
        /// <param name="text">The text to type.</param>
        /// <param name="timeout">How long to wait for the selector.</param>
        public Journey Type(string selector, string text, TimeSpan? timeout = null)
        {
            return Add(JourneyStep.Type(selector, text, timeout));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.TypeSecret"/> step and registers the secrets for scrubbing.
        /// </summary>
        /// <param name="selector">The input selector.</param>
        /// <param name="secrets">The secrets.</param>
        /// <param name="key">The key whose value is typed.</param>
        /// <param name="timeout">How long to wait for the selector.</param>
        public Journey TypeSecret(string selector, EnvFileSecrets secrets, string key, TimeSpan? timeout = null)
        {
            RegisterSecrets(secrets);
            return Add(JourneyStep.TypeSecret(selector, secrets, key, timeout));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.WaitForSelector"/> step.
        /// </summary>
        /// <param name="selector">The selector.</param>
        /// <param name="timeout">How long to wait.</param>
        public Journey WaitForSelector(string selector, TimeSpan? timeout = null)
        {
            return Add(JourneyStep.WaitForSelector(selector, timeout));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.WaitForUrl(string, Func{string, bool}, TimeSpan?, TimeSpan?)"/> step.
        /// </summary>
        /// <param name="description">What url is expected.</param>
        /// <param name="isExpectedUrl">Returns true when the url is the one being waited for.</param>
        /// <param name="timeout">How long to wait.</param>
        public Journey WaitForUrl(string description, Func<string, bool> isExpectedUrl, TimeSpan? timeout = null)
        {
            return Add(JourneyStep.WaitForUrl(description, isExpectedUrl, timeout));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.WaitForUrl(string, TimeSpan?)"/> step.
        /// </summary>
        /// <param name="urlFragment">Text the url should contain.</param>
        /// <param name="timeout">How long to wait.</param>
        public Journey WaitForUrl(string urlFragment, TimeSpan? timeout = null)
        {
            return Add(JourneyStep.WaitForUrl(urlFragment, timeout));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.WaitUntil(string, Func{Task{bool}}, TimeSpan?, TimeSpan?)"/> step.
        /// </summary>
        /// <param name="description">What is being waited for.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="timeout">How long to wait.</param>
        /// <param name="interval">The polling interval.</param>
        public Journey WaitUntil(string description, Func<Task<bool>> condition, TimeSpan? timeout = null, TimeSpan? interval = null)
        {
            return Add(JourneyStep.WaitUntil(description, condition, timeout, interval));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.Assert"/> step.
        /// </summary>
        /// <param name="assertion">The assertion.</param>
        public Journey Assert(PageAssertion assertion)
        {
            return Add(JourneyStep.Assert(assertion));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.Screenshot"/> step.
        /// </summary>
        /// <param name="name">The screenshot file name (without extension).</param>
        public Journey Screenshot(string name)
        {
            return Add(JourneyStep.Screenshot(name));
        }

        /// <summary>
        /// Adds a <see cref="JourneyStep.Custom"/> step.
        /// </summary>
        /// <param name="name">The step name.</param>
        /// <param name="action">The action; throw to fail the step.</param>
        public Journey Do(string name, Func<IAutomationPage, Task> action)
        {
            return Add(JourneyStep.Custom(name, action));
        }

        /// <summary>
        /// Registers secrets whose values are scrubbed from every message this journey reports.
        /// </summary>
        /// <param name="secrets">The secrets.</param>
        /// <returns>This journey.</returns>
        public Journey RegisterSecrets(EnvFileSecrets secrets)
        {
            if (secrets == null)
            {
                throw new ArgumentNullException(nameof(secrets));
            }

            if (!_secrets.Contains(secrets))
            {
                _secrets.Add(secrets);
            }

            return this;
        }

        /// <summary>
        /// Records a report-safe note. The value is scrubbed before it is stored.
        /// </summary>
        /// <param name="key">The note name.</param>
        /// <param name="value">The note value.</param>
        /// <returns>This journey.</returns>
        public Journey Note(string key, string value)
        {
            Notes[key] = Scrub(value);
            _currentResult?.Notes[key] = Notes[key];
            return this;
        }

        /// <summary>
        /// Records the masked form of a secret (<see cref="EnvFileSecrets.Masked"/>) as a note.
        /// </summary>
        /// <param name="secrets">The secrets.</param>
        /// <param name="key">The key to describe.</param>
        /// <returns>This journey.</returns>
        public Journey NoteMasked(EnvFileSecrets secrets, string key)
        {
            RegisterSecrets(secrets);
            return Note(key, secrets.Masked(key));
        }

        /// <summary>
        /// Replaces every registered secret value in the text with a redaction marker.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The scrubbed text.</returns>
        public string Scrub(string? text)
        {
            string scrubbed = text ?? string.Empty;
            foreach (EnvFileSecrets secrets in _secrets)
            {
                scrubbed = secrets.Scrub(scrubbed);
            }

            foreach (JourneyStep step in _steps)
            {
                foreach (EnvFileSecrets secrets in step.Secrets)
                {
                    scrubbed = secrets.Scrub(scrubbed);
                }
            }

            return scrubbed;
        }

        /// <summary>
        /// Opens a page on the session at <see cref="StartUrl"/> (or the first navigation's url, or
        /// <c>about:blank</c>) and runs the steps against it. The page stays open so the owner can look at where the
        /// run ended; the session owns the browser.
        /// </summary>
        /// <param name="session">The browser session.</param>
        /// <returns>The result.</returns>
        public async Task<JourneyResult> RunAsync(BrowserSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            string startUrl = ResolveStartUrl();
            AutomationPage page = await session.OpenAsync(startUrl, Slug);
            return await RunAsync(page);
        }

        /// <summary>
        /// Runs the steps against the page (blocking); see <see cref="RunAsync(BrowserSession)"/>.
        /// </summary>
        /// <param name="session">The browser session.</param>
        /// <returns>The result.</returns>
        public JourneyResult Run(BrowserSession session)
        {
            return RunAsync(session).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Runs the steps against an already-open page.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>The result.</returns>
        public async Task<JourneyResult> RunAsync(IAutomationPage page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            JourneyResult result;
            lock (_runLock)
            {
                if (_currentResult != null && _currentResult.IsRunning)
                {
                    throw new InvalidOperationException($"Journey '{Name}' is already running");
                }

                result = new JourneyResult(Name) { StartedAtUtc = DateTime.UtcNow, ArtifactsDirectory = ArtifactsDirectory, IsRunning = true };
                foreach (KeyValuePair<string, string> note in Notes)
                {
                    result.Notes[note.Key] = note.Value;
                }

                _currentResult = result;
                _currentNumber = 0;
                LastResult = result;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (_steps.Count > 0)
                {
                    Directory.CreateDirectory(ArtifactsDirectory);
                    Sequence.ScreenShotsDirectory = ArtifactsDirectory;
                    await Sequence.ExecuteAsync(page, ContinueOnFailure);
                }

                for (int index = result.Steps.Count; index < _steps.Count; index++)
                {
                    JourneyStepResult skipped = new JourneyStepResult(index + 1, _steps[index].Name) { Status = JourneyStepStatus.Skipped, Message = "Skipped after an earlier failure" };
                    result.Steps.Add(skipped);
                }
            }
            finally
            {
                result.Duration = stopwatch.Elapsed;
                result.IsRunning = false;
            }

            JourneyStepResult last = result.Steps.Count > 0 ? result.Steps[result.Steps.Count - 1] : new JourneyStepResult(0, "(no steps)");
            Completed?.Invoke(this, new JourneyStepCompletedEventArgs(this, result, last));
            return result;
        }

        /// <summary>
        /// Runs the steps against an already-open page (blocking); see <see cref="RunAsync(IAutomationPage)"/>.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <returns>The result.</returns>
        public JourneyResult Run(IAutomationPage page)
        {
            return RunAsync(page).GetAwaiter().GetResult();
        }

        /// <summary>
        /// The longest slug used in a screenshot file name; step names that embed a url are cut to this.
        /// </summary>
        public const int MaxScreenshotSlugLength = 60;

        /// <summary>
        /// Converts text to a lowercase, hyphenated, file-system-safe slug.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="maxLength">The longest slug to return; null leaves it uncut.</param>
        /// <returns>The slug.</returns>
        public static string ToSlug(string text, int? maxLength = null)
        {
            string lowered = (text ?? string.Empty).Trim().ToLowerInvariant();
            string slug = Regex.Replace(lowered, "[^a-z0-9]+", "-").Trim('-');
            if (maxLength.HasValue && slug.Length > maxLength.Value)
            {
                slug = slug.Substring(0, maxLength.Value).TrimEnd('-');
            }

            return slug.Length == 0 ? "journey" : slug;
        }

        private string ResolveStartUrl()
        {
            if (!string.IsNullOrWhiteSpace(StartUrl))
            {
                return StartUrl;
            }

            return "about:blank";
        }

        private async Task<PageActionResult> ExecuteStepAsync(int number, JourneyStep step, IAutomationPage page)
        {
            JourneyResult result = _currentResult ?? throw new InvalidOperationException("No run in progress");
            _currentNumber = number;
            JourneyStepResult stepResult = new JourneyStepResult(number, step.Name) { StartedAtUtc = DateTime.UtcNow };
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await step.Action(page);
                foreach (PageAssertion assertion in step.Assertions)
                {
                    PageAssertionResult assertionResult;
                    try
                    {
                        assertionResult = await assertion.ExecuteAsync(page);
                    }
                    catch (Exception assertionException)
                    {
                        throw new AutomationAssertionException(page, $"{assertion.Name}: {assertionException.GetBaseException().Message}");
                    }

                    if (!assertionResult.Passed)
                    {
                        throw new AutomationAssertionException(page, $"{assertion.Name}: {assertionResult.Message}");
                    }
                }

                stepResult.Status = JourneyStepStatus.Passed;
            }
            catch (Exception ex)
            {
                Exception root = ex.GetBaseException();
                stepResult.Status = JourneyStepStatus.Failed;
                stepResult.ExceptionType = root.GetType().Name;
                stepResult.Message = Scrub(root.Message);
            }

            stepResult.Duration = stopwatch.Elapsed;
            stepResult.Url = Scrub(page.GetCurrentUrl());
            await CaptureScreenshotAsync(step, stepResult, page);

            result.Steps.Add(stepResult);
            try
            {
                StepCompleted?.Invoke(this, new JourneyStepCompletedEventArgs(this, result, stepResult));
            }
            catch
            {
                // a reporting hook never changes a step's outcome
            }

            return stepResult.Passed
                ? new PageActionResult(page, true)
                : new PageActionResult(page, stepResult.Message);
        }

        private async Task CaptureScreenshotAsync(JourneyStep step, JourneyStepResult stepResult, IAutomationPage page)
        {
            ScreenshotPolicy policy = step.ScreenshotPolicy == ScreenshotPolicy.Inherit ? DefaultScreenshotPolicy : step.ScreenshotPolicy;
            bool capture = policy == ScreenshotPolicy.Always || (policy == ScreenshotPolicy.OnFailure && stepResult.Failed);
            if (!capture)
            {
                return;
            }

            string fileName = step.ScreenshotName != null
                ? ToSlug(step.ScreenshotName, MaxScreenshotSlugLength)
                : $"{stepResult.Number:00}-{ToSlug(step.Name, MaxScreenshotSlugLength)}";
            string path = Path.Combine(ArtifactsDirectory, $"{fileName}.png");
            try
            {
                Directory.CreateDirectory(ArtifactsDirectory);
                await page.ScreenshotAsync(path);
                if (File.Exists(path))
                {
                    stepResult.ScreenshotPath = path;
                }
            }
            catch
            {
                // a failed screenshot never fails the step
            }
        }
    }
}
