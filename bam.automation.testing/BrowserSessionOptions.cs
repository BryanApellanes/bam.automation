using PuppeteerSharp;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// Launch policy for a <see cref="BrowserSession"/>: a journey mimics a real user, so the defaults are a
    /// visible browser with a persistent profile (so Stripe Link, autofill and cookies survive between runs).
    /// <see cref="FromEnvironment()"/> flips to headless under CI.
    /// </summary>
    public class BrowserSessionOptions
    {
        /// <summary>
        /// Environment variable that forces a headless launch when set to <c>1</c> or <c>true</c>.
        /// </summary>
        public const string HeadlessVariable = "BAM_AUTOMATION_HEADLESS";

        /// <summary>
        /// Environment variable that forces a headed launch when set to <c>1</c> or <c>true</c>, even under CI.
        /// </summary>
        public const string HeadedVariable = "BAM_AUTOMATION_HEADED";

        /// <summary>
        /// Environment variable naming the persistent profile directory to use.
        /// </summary>
        public const string ProfileVariable = "BAM_AUTOMATION_PROFILE";

        /// <summary>
        /// Gets or sets a value indicating whether the browser runs without a window. Default false: a real user sees
        /// the browser, and so does the owner monitoring the run.
        /// </summary>
        public bool Headless { get; set; }

        /// <summary>
        /// Gets or sets the persistent profile directory (Chromium <c>--user-data-dir</c>). Default
        /// <c>~/.bam/automation/profiles/default</c>. Null or empty launches a throwaway profile.
        /// </summary>
        public string? UserDataDirectory { get; set; } = Path.Combine(BamProfile.Path, "automation", "profiles", "default");

        /// <summary>
        /// Gets or sets the browser window width in pixels. Default 1280.
        /// </summary>
        public int WindowWidth { get; set; } = 1280;

        /// <summary>
        /// Gets or sets the browser window height in pixels. Default 900.
        /// </summary>
        public int WindowHeight { get; set; } = 900;

        /// <summary>
        /// Gets or sets the delay PuppeteerSharp inserts before each operation (<c>SlowMo</c>), useful for watching a
        /// headed run. Default 0.
        /// </summary>
        public int SlowMotionMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets the default timeout for navigations and element waits. Default 30 seconds.
        /// </summary>
        public TimeSpan DefaultNavigationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets an explicit browser executable. Null lets <see cref="BamBot.Automation.AutomationPage.LaunchBrowserAsync"/>
        /// resolve the downloaded browser.
        /// </summary>
        public string? ExecutablePath { get; set; }

        /// <summary>
        /// Gets additional Chromium command line arguments appended after the window size.
        /// </summary>
        public List<string> AdditionalArguments { get; } = new List<string>();

        /// <summary>
        /// Gets a key that identifies launches which can share one browser process: two option sets with the same
        /// key are interchangeable for <see cref="BrowserSession.GetShared"/>.
        /// </summary>
        public string Key => $"headless={Headless}|profile={UserDataDirectory}|size={WindowWidth}x{WindowHeight}|slowmo={SlowMotionMilliseconds}|exe={ExecutablePath}";

        /// <summary>
        /// Builds options from the environment: headless when <see cref="HeadlessVariable"/> is set or the <c>CI</c>
        /// variable is present, headed when <see cref="HeadedVariable"/> is set (which wins), and the profile
        /// directory from <see cref="ProfileVariable"/> when set.
        /// </summary>
        /// <returns>The options.</returns>
        public static BrowserSessionOptions FromEnvironment()
        {
            return FromEnvironment(Environment.GetEnvironmentVariable);
        }

        /// <summary>
        /// Builds options from the specified environment lookup; see <see cref="FromEnvironment()"/>.
        /// </summary>
        /// <param name="getVariable">Returns the value of a named environment variable, or null.</param>
        /// <returns>The options.</returns>
        public static BrowserSessionOptions FromEnvironment(Func<string, string?> getVariable)
        {
            if (getVariable == null)
            {
                throw new ArgumentNullException(nameof(getVariable));
            }

            BrowserSessionOptions options = new BrowserSessionOptions();
            bool headlessRequested = IsTruthy(getVariable(HeadlessVariable)) || !string.IsNullOrEmpty(getVariable("CI"));
            bool headedRequested = IsTruthy(getVariable(HeadedVariable));
            options.Headless = headlessRequested && !headedRequested;

            string? profile = getVariable(ProfileVariable);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                options.UserDataDirectory = profile;
            }

            return options;
        }

        /// <summary>
        /// Converts these options to PuppeteerSharp <see cref="LaunchOptions"/>. The default viewport is disabled so
        /// the window size governs what the user (and screenshots) see.
        /// </summary>
        /// <returns>The launch options.</returns>
        public LaunchOptions ToLaunchOptions()
        {
            List<string> args = new List<string> { $"--window-size={WindowWidth},{WindowHeight}" };
            args.AddRange(AdditionalArguments);

            LaunchOptions launchOptions = new LaunchOptions
            {
                Headless = Headless,
                SlowMo = SlowMotionMilliseconds,
                DefaultViewport = null,
                Args = args.ToArray(),
                Timeout = (int)DefaultNavigationTimeout.TotalMilliseconds
            };

            if (!string.IsNullOrWhiteSpace(UserDataDirectory))
            {
                launchOptions.UserDataDir = UserDataDirectory;
            }

            if (!string.IsNullOrWhiteSpace(ExecutablePath))
            {
                launchOptions.ExecutablePath = ExecutablePath;
            }

            return launchOptions;
        }

        private static bool IsTruthy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Equals("1", StringComparison.Ordinal) || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
