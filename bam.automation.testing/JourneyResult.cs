namespace Bam.Automation.Testing
{
    /// <summary>
    /// The outcome of a <see cref="Journey"/> run: every step's <see cref="JourneyStepResult"/>, overall success,
    /// timing, the artifacts directory and report-safe notes (masked secrets, ids). This is the value a journey test
    /// asserts on via <c>ShouldPass&lt;JourneyResult&gt;</c> and the input to <see cref="JourneyReport"/>.
    /// </summary>
    public class JourneyResult
    {
        /// <summary>
        /// Creates an empty result for the named journey.
        /// </summary>
        /// <param name="name">The journey name.</param>
        public JourneyResult(string name)
        {
            Name = name;
            Steps = new List<JourneyStepResult>();
            Notes = new Dictionary<string, string>(StringComparer.Ordinal);
            ArtifactsDirectory = string.Empty;
        }

        /// <summary>
        /// Gets the journey name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the per-step results in execution order, including steps skipped after a failure.
        /// </summary>
        public List<JourneyStepResult> Steps { get; }

        /// <summary>
        /// Gets or sets when the run started (UTC).
        /// </summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets how long the run took.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the directory screenshots and reports were written to.
        /// </summary>
        public string ArtifactsDirectory { get; set; }

        /// <summary>
        /// Gets or sets the Markdown report path once written, or null.
        /// </summary>
        public string? ReportPath { get; set; }

        /// <summary>
        /// Gets report-safe notes recorded by the journey: masked secrets, order ids, anything the monitoring owner
        /// should see. Never raw secret values.
        /// </summary>
        public Dictionary<string, string> Notes { get; }

        /// <summary>
        /// Gets a value indicating whether every executed step passed and none was skipped.
        /// </summary>
        public bool Succeeded => Steps.Count > 0 && Steps.All(step => step.Status == JourneyStepStatus.Passed);

        /// <summary>
        /// Gets a value indicating whether the run is still in progress (a step is pending).
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Gets the first failed step, or null.
        /// </summary>
        public JourneyStepResult? FailedStep => Steps.FirstOrDefault(step => step.Status == JourneyStepStatus.Failed);

        /// <summary>
        /// Gets a one-line description of the failure, or an empty string when the run succeeded.
        /// </summary>
        public string FailureSummary
        {
            get
            {
                JourneyStepResult? failed = FailedStep;
                if (failed == null)
                {
                    return Steps.Count == 0 ? "The journey has no steps" : string.Empty;
                }

                return $"Step {failed.Number} '{failed.Name}' failed: {failed.Message}";
            }
        }

        /// <summary>
        /// Gets the number of steps that passed.
        /// </summary>
        public int PassedCount => Steps.Count(step => step.Status == JourneyStepStatus.Passed);

        /// <summary>
        /// Gets the number of steps that failed.
        /// </summary>
        public int FailedCount => Steps.Count(step => step.Status == JourneyStepStatus.Failed);

        /// <summary>
        /// Gets the number of steps that were skipped.
        /// </summary>
        public int SkippedCount => Steps.Count(step => step.Status == JourneyStepStatus.Skipped);

        /// <summary>
        /// Describes the run outcome in one line.
        /// </summary>
        public override string ToString()
        {
            string outcome = Succeeded ? "succeeded" : "failed";
            return $"Journey '{Name}' {outcome}: {PassedCount} passed, {FailedCount} failed, {SkippedCount} skipped in {Duration.TotalSeconds:0.0}s";
        }
    }
}
