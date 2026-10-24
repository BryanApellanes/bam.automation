using Bam.Test;
using PuppeteerSharp;

namespace Bam.Automation.Testing.Tests.Unit
{
    [UnitTestMenu("BrowserSessionOptionsShould")]
    public class BrowserSessionOptionsShould : UnitTestMenuContainer
    {
        [UnitTest]
        public void DefaultToAVisiblePersistentBrowser()
        {
            When.A<BrowserSessionOptions>("is created with defaults", (options) => options.ToLaunchOptions())
            .TheTest
            .ShouldPass<LaunchOptions>((because, launch) =>
            {
                because.ItsFalse("headed by default", launch.Headless);
                because.ItsTrue("has a persistent profile under ~/.bam", !string.IsNullOrEmpty(launch.UserDataDir) && launch.UserDataDir.Contains("automation"), launch.UserDataDir);
                because.ItsTrue("the default viewport is disabled so the window size governs", launch.DefaultViewport == null);
                because.ItsTrue("passes the window size", launch.Args.Contains("--window-size=1280,900"), string.Join(" ", launch.Args));
                because.ItsTrue("no executable override", string.IsNullOrEmpty(launch.ExecutablePath));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void MapEveryOptionToLaunchOptions()
        {
            When.A<BrowserSessionOptions>("is fully configured", (options) =>
            {
                options.Headless = true;
                options.UserDataDirectory = "C:/profiles/journey";
                options.WindowWidth = 800;
                options.WindowHeight = 600;
                options.SlowMotionMilliseconds = 25;
                options.ExecutablePath = "C:/browsers/chrome.exe";
                options.DefaultNavigationTimeout = TimeSpan.FromSeconds(12);
                options.AdditionalArguments.Add("--lang=es");
                return new MappedOutcome(options.ToLaunchOptions(), options.Key);
            })
            .TheTest
            .ShouldPass<MappedOutcome>((because, outcome) =>
            {
                LaunchOptions launch = outcome.Launch;
                because.ItsTrue("headless", launch.Headless);
                because.ItsTrue("profile", launch.UserDataDir == "C:/profiles/journey");
                because.ItsTrue("slow motion", launch.SlowMo == 25);
                because.ItsTrue("executable", launch.ExecutablePath == "C:/browsers/chrome.exe");
                because.ItsTrue("timeout in milliseconds", launch.Timeout == 12000);
                because.ItsTrue("window size then extra args", launch.Args.SequenceEqual(new string[] { "--window-size=800,600", "--lang=es" }), string.Join(" ", launch.Args));
                because.ItsTrue("the key distinguishes the option set", outcome.Key.Contains("headless=True") && outcome.Key.Contains("800x600") && outcome.Key.Contains("journey"), outcome.Key);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReadHeadlessHeadedAndProfileFromTheEnvironment()
        {
            When.A<BrowserSessionOptions>("is built from different environments", (unused) =>
                new EnvironmentOutcome(
                    BrowserSessionOptions.FromEnvironment((name) => null).Headless,
                    BrowserSessionOptions.FromEnvironment((name) => name == "CI" ? "true" : null).Headless,
                    BrowserSessionOptions.FromEnvironment((name) => name == BrowserSessionOptions.HeadlessVariable ? "1" : null).Headless,
                    BrowserSessionOptions.FromEnvironment((name) => name == "CI" ? "true" : name == BrowserSessionOptions.HeadedVariable ? "1" : null).Headless,
                    BrowserSessionOptions.FromEnvironment((name) => name == BrowserSessionOptions.ProfileVariable ? "D:/p" : null).UserDataDirectory))
            .TheTest
            .ShouldPass<EnvironmentOutcome>((because, outcome) =>
            {
                because.ItsFalse("no variables means headed", outcome.DefaultHeadless);
                because.ItsTrue("CI means headless", outcome.CiHeadless);
                because.ItsTrue("the headless variable means headless", outcome.VariableHeadless);
                because.ItsFalse("the headed variable wins over CI", outcome.HeadedWinsHeadless);
                because.ItsTrue("the profile variable sets the profile", outcome.Profile == "D:/p");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record MappedOutcome(LaunchOptions Launch, string Key);

        private sealed record EnvironmentOutcome(bool DefaultHeadless, bool CiHeadless, bool VariableHeadless, bool HeadedWinsHeadless, string? Profile);
    }
}
