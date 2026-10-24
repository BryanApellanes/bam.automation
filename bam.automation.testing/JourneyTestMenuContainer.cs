using Bam.Test;
using Bam.Test.Integration;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// bam.test adapter for journey tests. Derive from it, mark the class <c>[IntegrationTestMenu("…")]</c> and
    /// methods <c>[IntegrationTest]</c> (journeys hit real sites and run under <c>--it</c>), build a
    /// <see cref="Journey"/>, then <c>When.A&lt;Journey&gt;("…", journey, j =&gt; Run(j)).TheTest
    /// .ShouldPass&lt;JourneyResult&gt;((because, result) =&gt; Expect(because, result))</c>. <see cref="Run"/>
    /// executes on the process-wide <see cref="BrowserSession"/> (the runner constructs a container per test
    /// method, so the session is shared, not owned per instance), follows the run with a live
    /// <see cref="JourneyReport"/> and writes the final report under <see cref="ReportsDirectory"/>.
    /// </summary>
    public abstract class JourneyTestMenuContainer : IntegrationTestMenuContainer
    {
        /// <summary>
        /// Creates the container with the default service registry.
        /// </summary>
        protected JourneyTestMenuContainer()
        {
        }

        /// <summary>
        /// Gets the directory journey runs are written under. Default <c>~/.bam/tests/journeys</c>; each run gets
        /// <c>&lt;slug&gt;/&lt;timestamp&gt;/</c> beneath it.
        /// </summary>
        protected virtual string ReportsDirectory => Path.Combine(BamProfile.TestsPath, "journeys");

        /// <summary>
        /// Gets the launch policy for the shared session. Default <see cref="BrowserSessionOptions.FromEnvironment()"/>:
        /// headed and persistent on a dev box, headless under CI.
        /// </summary>
        protected virtual BrowserSessionOptions CreateSessionOptions()
        {
            return BrowserSessionOptions.FromEnvironment();
        }

        /// <summary>
        /// Gets the shared browser session for <see cref="CreateSessionOptions"/>.
        /// </summary>
        protected BrowserSession Session => BrowserSession.GetShared(CreateSessionOptions());

        /// <summary>
        /// Runs the journey on the shared session with a live report (blocking); see <see cref="RunAsync"/>.
        /// </summary>
        /// <param name="journey">The journey.</param>
        /// <returns>The result, with <see cref="JourneyResult.ReportPath"/> set.</returns>
        protected JourneyResult Run(Journey journey)
        {
            return RunAsync(journey).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Runs the journey on the shared session, rewriting the Markdown report after each step and writing the
        /// final report and JSON result to <c>ReportsDirectory/&lt;slug&gt;/&lt;timestamp&gt;/</c>.
        /// </summary>
        /// <param name="journey">The journey.</param>
        /// <returns>The result, with <see cref="JourneyResult.ReportPath"/> set.</returns>
        protected async Task<JourneyResult> RunAsync(Journey journey)
        {
            if (journey == null)
            {
                throw new ArgumentNullException(nameof(journey));
            }

            journey.ArtifactsDirectory = Path.Combine(ReportsDirectory, journey.Slug, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(journey.ArtifactsDirectory);
            JourneyResult result;
            using (JourneyReport.Follow(journey))
            {
                result = await journey.RunAsync(Session);
            }

            JourneyReport.Write(result);
            return result;
        }

        /// <summary>
        /// Asserts the journey succeeded, naming the failed step when it did not, and records the report path.
        /// </summary>
        /// <param name="because">The assertion context.</param>
        /// <param name="result">The result.</param>
        protected static void Expect(Because because, JourneyResult result)
        {
            if (because == null)
            {
                throw new ArgumentNullException(nameof(because));
            }

            if (result == null)
            {
                because.ItsTrue("the journey produced a result", false, "the journey result was null");
                return;
            }

            because.ItsTrue($"journey '{result.Name}' succeeded ({result.PassedCount}/{result.Steps.Count} steps)", result.Succeeded, result.FailureSummary);
            if (!string.IsNullOrEmpty(result.ReportPath))
            {
                because.AdditionalInformation($"report: {result.ReportPath}");
            }
        }
    }
}
