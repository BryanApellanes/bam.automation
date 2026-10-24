using Bam.Automation.Testing.Tests.TestClasses;
using Bam.Test;
using Newtonsoft.Json.Linq;

namespace Bam.Automation.Testing.Tests.Unit
{
    [UnitTestMenu("JourneyReportShould")]
    public class JourneyReportShould : UnitTestMenuContainer
    {
        [UnitTest]
        public void RenderMarkdownWithStepTableFailureAndNotes()
        {
            string directory = Fixtures.NewTempDirectory("reports");
            JourneyResult result = SampleResult(directory);

            When.A<JourneyReport>("renders markdown", () => new JourneyReport(result), (report) => report.ToMarkdown(directory))
            .TheTest
            .ShouldPass<string>((because, markdown) =>
            {
                because.ItsTrue("has the title", markdown.Contains("# Journey: sample purchase"));
                because.ItsTrue("shows FAILED", markdown.Contains("**Status:** FAILED"));
                because.ItsTrue("has a table header", markdown.Contains("| # | Step | Status | Duration | Url | Screenshot |"));
                because.ItsTrue("lists the passed step", markdown.Contains("| 1 | Navigate home |") && markdown.Contains("Passed"));
                because.ItsTrue("links the screenshot relative to the report", markdown.Contains("[01-navigate-home.png](01-navigate-home.png)"), markdown);
                because.ItsTrue("lists the failed step", markdown.Contains("| 2 | Pay \\| submit |"), markdown);
                because.ItsTrue("lists the skipped step", markdown.Contains("| 3 | Confirm |") && markdown.Contains("Skipped"));
                because.ItsTrue("has a failure section with the message", markdown.Contains("## Failure") && markdown.Contains("declined by issuer") && markdown.Contains("TimeoutException"));
                because.ItsTrue("has the notes table", markdown.Contains("## Notes") && markdown.Contains("| CARD_NUMBER | len=16 prefix=4242 |"));
                because.ItsTrue("has the counts", markdown.Contains("1 passed, 1 failed, 1 skipped of 3"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RenderJsonWithStepsAndNotes()
        {
            string directory = Fixtures.NewTempDirectory("reports");
            JourneyResult result = SampleResult(directory);

            When.A<JourneyReport>("renders json", () => new JourneyReport(result), (report) => JObject.Parse(report.ToJson()))
            .TheTest
            .ShouldPass<JObject>((because, json) =>
            {
                because.ItsTrue("has the name", json.Value<string>("Name") == "sample purchase");
                because.ItsTrue("has three steps", json["Steps"] is JArray steps && steps.Count == 3);
                because.ItsTrue("step status is serialized", json["Steps"]![1]!.Value<string>("Status") == "Failed" || json["Steps"]![1]!.Value<int>("Status") == (int)JourneyStepStatus.Failed);
                because.ItsTrue("notes are serialized", json["Notes"]!.Value<string>("CARD_NUMBER") == "len=16 prefix=4242");
                because.ItsTrue("the computed success flag is serialized", json.Value<bool>("Succeeded") == false);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void WriteBothFilesAndRecordTheReportPath()
        {
            string directory = Fixtures.NewTempDirectory("reports");
            JourneyResult result = SampleResult(directory);

            When.A<JourneyResult>("is written as a report", result, (r) => JourneyReport.Write(r))
            .TheTest
            .ShouldPass<JourneyReportFiles>((because, files) =>
            {
                because.ItsTrue("report.md exists", File.Exists(files.MarkdownPath) && Path.GetFileName(files.MarkdownPath) == JourneyReport.MarkdownFileName);
                because.ItsTrue("result.json exists", File.Exists(files.JsonPath) && Path.GetFileName(files.JsonPath) == JourneyReport.JsonFileName);
                because.ItsTrue("files are in the artifacts directory", files.MarkdownPath.StartsWith(directory));
                because.ItsTrue("the result records the report path", result.ReportPath == files.MarkdownPath);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FollowAJourneyRewritingTheReportAfterEachStep()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#a");
            string directory = Fixtures.NewTempDirectory("follow");
            List<int> stepCountsSeen = new List<int>();

            When.A<Journey>("is followed while running", () => new Journey("followed") { ArtifactsDirectory = directory }, (journey) =>
            {
                string reportPath = Path.Combine(directory, JourneyReport.MarkdownFileName);
                using (JourneyReport.Follow(journey))
                {
                    // attached after the follower, so by the time this runs the report already reflects the step
                    journey.StepCompleted += (sender, args) =>
                    {
                        string markdown = File.ReadAllText(reportPath);
                        stepCountsSeen.Add(markdown.Split('\n').Count(line => line.StartsWith("| ") && !line.StartsWith("| #") && !line.StartsWith("| Key")));
                    };
                    journey.Click("#a").Click("#a");
                    journey.Run(page);
                }

                return File.ReadAllText(reportPath);
            })
            .TheTest
            .ShouldPass<string>((because, markdown) =>
            {
                because.ItsTrue("the report grew one row per step", stepCountsSeen.SequenceEqual(new int[] { 1, 2 }), string.Join(",", stepCountsSeen));
                because.ItsTrue("the final report shows PASSED", markdown.Contains("**Status:** PASSED"), markdown);
                because.ItsTrue("the final report lists both steps", markdown.Contains("| 1 | Click #a |") && markdown.Contains("| 2 | Click #a |"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static JourneyResult SampleResult(string directory)
        {
            string screenshot = Path.Combine(directory, "01-navigate-home.png");
            File.WriteAllBytes(screenshot, new byte[] { 1, 2, 3 });
            JourneyResult result = new JourneyResult("sample purchase")
            {
                StartedAtUtc = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc),
                Duration = TimeSpan.FromSeconds(12.5),
                ArtifactsDirectory = directory
            };
            result.Steps.Add(new JourneyStepResult(1, "Navigate home") { Status = JourneyStepStatus.Passed, Duration = TimeSpan.FromSeconds(1.2), Url = "https://airroamer.co/en", ScreenshotPath = screenshot });
            result.Steps.Add(new JourneyStepResult(2, "Pay | submit") { Status = JourneyStepStatus.Failed, Duration = TimeSpan.FromSeconds(9), Url = "https://checkout.stripe.com/c/pay", Message = "declined by issuer", ExceptionType = nameof(TimeoutException) });
            result.Steps.Add(new JourneyStepResult(3, "Confirm") { Status = JourneyStepStatus.Skipped, Message = "Skipped after an earlier failure" });
            result.Notes["CARD_NUMBER"] = "len=16 prefix=4242";
            return result;
        }
    }
}
