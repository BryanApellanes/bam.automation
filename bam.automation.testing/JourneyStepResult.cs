namespace Bam.Automation.Testing
{
    /// <summary>
    /// The outcome of one <see cref="JourneyStep"/>: status, timing, the url after the step, the screenshot taken and
    /// a message (already scrubbed of registered secret values). Serializable to JSON by <see cref="JourneyReport"/>.
    /// </summary>
    public class JourneyStepResult
    {
        /// <summary>
        /// Creates a pending result for the step.
        /// </summary>
        /// <param name="number">The 1-based step number.</param>
        /// <param name="name">The step name.</param>
        public JourneyStepResult(int number, string name)
        {
            Number = number;
            Name = name;
            Status = JourneyStepStatus.Pending;
            Message = string.Empty;
            Url = string.Empty;
        }

        /// <summary>
        /// Gets the 1-based step number.
        /// </summary>
        public int Number { get; }

        /// <summary>
        /// Gets the step name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public JourneyStepStatus Status { get; set; }

        /// <summary>
        /// Gets or sets when the step started (UTC).
        /// </summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets how long the step took.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the url the browser was at after the step.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the screenshot file path, or null when none was taken.
        /// </summary>
        public string? ScreenshotPath { get; set; }

        /// <summary>
        /// Gets or sets the failure message (scrubbed), or an empty string.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the type name of the exception that failed the step, or null.
        /// </summary>
        public string? ExceptionType { get; set; }

        /// <summary>
        /// Gets a value indicating whether the step passed.
        /// </summary>
        public bool Passed => Status == JourneyStepStatus.Passed;

        /// <summary>
        /// Gets a value indicating whether the step failed.
        /// </summary>
        public bool Failed => Status == JourneyStepStatus.Failed;

        /// <summary>
        /// Describes the step outcome in one line.
        /// </summary>
        public override string ToString()
        {
            string suffix = string.IsNullOrEmpty(Message) ? string.Empty : $": {Message}";
            return $"{Number}. {Name} [{Status}] {Duration.TotalSeconds:0.0}s{suffix}";
        }
    }
}
