using Bam.Automation.Testing.Tests.TestClasses;
using Bam.Test;
using Bam.Test.Integration;
using BamBot.Automation;

namespace Bam.Automation.Testing.Tests.Integration
{
    /// <summary>
    /// Launches a real browser (PuppeteerSharp downloads Chromium on first run). Headed on a dev box, headless under
    /// CI or <c>BAM_AUTOMATION_HEADLESS=1</c>; see <see cref="BrowserSessionOptions.FromEnvironment()"/>.
    /// </summary>
    [IntegrationTestMenu("BrowserSessionShould")]
    public class BrowserSessionShould : IntegrationTestMenuContainer
    {
        [IntegrationTest]
        public void RunTheSampleJourneyAgainstTheFixtureAndWriteAReport()
        {
            string artifacts = Fixtures.NewTempDirectory("sample-journey");
            BrowserSessionOptions options = TestOptions();

            When.A<Journey>("drives the sample page", () => new Journey("Sample journey") { ArtifactsDirectory = artifacts }, (journey) =>
            {
                using BrowserSession session = new BrowserSession(options);
                journey.Navigate(Fixtures.UrlTo(Fixtures.JourneySample))
                    .Type("#name", "Bam")
                    .Click("#go")
                    .WaitForSelector("#greeting", TimeSpan.FromSeconds(10))
                    .Add(JourneyStep.WaitUntil("the greeting is visible", async (page) => (await page.GetElementTextAsync("#greeting")).Contains("Hello, Bam!"), TimeSpan.FromSeconds(10)))
                    .Assert(new PageAssertion("greeting is present", "#greeting"))
                    .Screenshot("done");
                JourneyResult result;
                using (JourneyReport.Follow(journey))
                {
                    result = journey.Run(session);
                }

                JourneyReportFiles files = JourneyReport.Write(result);
                return new SampleOutcome(result, File.ReadAllText(files.MarkdownPath), options.Headless);
            })
            .TheTest
            .ShouldPass<SampleOutcome>((because, outcome) =>
            {
                because.ItsTrue("the journey succeeded", outcome.Result.Succeeded, outcome.Result.FailureSummary);
                because.ItsTrue("seven steps ran", outcome.Result.Steps.Count == 7);
                because.ItsTrue("every step has a screenshot on disk", outcome.Result.Steps.All(step => step.ScreenshotPath != null && File.Exists(step.ScreenshotPath) && new FileInfo(step.ScreenshotPath).Length > 100));
                because.ItsTrue("the named screenshot exists", File.Exists(Path.Combine(artifacts, "07-done.png")));
                because.ItsTrue("the report shows PASSED", outcome.Markdown.Contains("**Status:** PASSED"));
                because.ItsTrue("the report links a screenshot", outcome.Markdown.Contains("](01-navigate-to-"), outcome.Markdown);
                because.AdditionalInformation($"headless={outcome.Headless}; report: {outcome.Result.ReportPath}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [IntegrationTest]
        public void OpenPagesThatDoNotOwnTheBrowser()
        {
            When.A<BrowserSession>("opens two pages", () => new BrowserSession(TestOptions()), (session) =>
            {
                AutomationPage first = session.Open(Fixtures.UrlTo(Fixtures.JourneySample), "first");
                bool firstOwns = first.OwnsBrowser;
                bool firstHasInput = first.IsPresentAsync("#name").GetAwaiter().GetResult();
                first.Dispose();
                bool browserAliveAfterPageDispose = !session.Browser.IsClosed;
                AutomationPage second = session.Open(Fixtures.UrlTo(Fixtures.JourneySample), "second");
                bool secondHasInput = second.IsPresentAsync("#name").GetAwaiter().GetResult();
                session.Dispose();
                return new OwnershipOutcome(firstOwns, firstHasInput, browserAliveAfterPageDispose, secondHasInput, session.IsLaunched);
            })
            .TheTest
            .ShouldPass<OwnershipOutcome>((because, outcome) =>
            {
                because.ItsFalse("a session page does not own the browser", outcome.FirstOwnsBrowser);
                because.ItsTrue("the first page loaded the fixture", outcome.FirstHasInput);
                because.ItsTrue("disposing a page leaves the browser running", outcome.BrowserAliveAfterPageDispose);
                because.ItsTrue("a second page opens on the same browser", outcome.SecondHasInput);
                because.ItsTrue("the session reports it launched", outcome.WasLaunched);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [IntegrationTest]
        public void ShareOneSessionPerOptionSet()
        {
            BrowserSessionOptions options = TestOptions();
            When.A<BrowserSessionOptions>("is used to get shared sessions", options, (o) =>
            {
                BrowserSession a = BrowserSession.GetShared(o);
                BrowserSession b = BrowserSession.GetShared(new BrowserSessionOptions { Headless = o.Headless, UserDataDirectory = o.UserDataDirectory });
                BrowserSessionOptions different = TestOptions();
                different.WindowWidth = 1000;
                BrowserSession c = BrowserSession.GetShared(different);
                BrowserSession.DisposeShared();
                return new SharedOutcome(ReferenceEquals(a, b), ReferenceEquals(a, c));
            })
            .TheTest
            .ShouldPass<SharedOutcome>((because, outcome) =>
            {
                because.ItsTrue("equal option sets share a session", outcome.SameForEqualOptions);
                because.ItsFalse("different option sets get their own", outcome.SameForDifferentOptions);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [IntegrationTest]
        public void LaunchHeadedWhenAsked()
        {
            Skip.When(BrowserSessionOptions.FromEnvironment().Headless, $"the environment asks for headless runs (CI or {BrowserSessionOptions.HeadlessVariable})");

            BrowserSessionOptions options = TestOptions();
            options.Headless = false;
            When.A<BrowserSession>("launches headed", () => new BrowserSession(options), (session) =>
            {
                using (session)
                {
                    AutomationPage page = session.Open(Fixtures.UrlTo(Fixtures.JourneySample));
                    string arguments = session.Browser.Process?.StartInfo.Arguments ?? string.Empty;
                    return new HeadedOutcome(arguments.Contains("--headless"), page.IsPresentAsync("#title").GetAwaiter().GetResult());
                }
            })
            .TheTest
            .ShouldPass<HeadedOutcome>((because, outcome) =>
            {
                because.ItsFalse("the browser was not launched headless", outcome.LaunchedHeadless);
                because.ItsTrue("the fixture rendered", outcome.FixtureRendered);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [IntegrationTest]
        public void KeepTheOriginalSharedHeadlessPathWorking()
        {
            When.A<AutomationPage>("opens on the shared headless browser", () => AutomationPage.Open(Fixtures.UrlTo(Fixtures.JourneySample)), (page) =>
            {
                using (page)
                {
                    return new StaticPathOutcome(page.OwnsBrowser, page.IsPresentAsync("#name").GetAwaiter().GetResult(), page.GetElementTextAsync("#title").GetAwaiter().GetResult());
                }
            })
            .TheTest
            .ShouldPass<StaticPathOutcome>((because, outcome) =>
            {
                because.ItsTrue("a static-path page owns the shared browser", outcome.OwnsBrowser);
                because.ItsTrue("the page loaded", outcome.HasInput);
                because.ItsTrue("text can be read", outcome.Title == "Journey sample", outcome.Title);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static BrowserSessionOptions TestOptions()
        {
            BrowserSessionOptions options = BrowserSessionOptions.FromEnvironment();
            options.UserDataDirectory = Fixtures.NewTempDirectory("profile");
            return options;
        }

        private sealed record SampleOutcome(JourneyResult Result, string Markdown, bool Headless);

        private sealed record OwnershipOutcome(bool FirstOwnsBrowser, bool FirstHasInput, bool BrowserAliveAfterPageDispose, bool SecondHasInput, bool WasLaunched);

        private sealed record SharedOutcome(bool SameForEqualOptions, bool SameForDifferentOptions);

        private sealed record HeadedOutcome(bool LaunchedHeadless, bool FixtureRendered);

        private sealed record StaticPathOutcome(bool OwnsBrowser, bool HasInput, string Title);
    }
}
