namespace Bam.Automation.Testing
{
    /// <summary>
    /// The outcome of a single <see cref="JourneyStep"/> within a <see cref="JourneyResult"/>.
    /// </summary>
    public enum JourneyStepStatus
    {
        /// <summary>
        /// The step has not run.
        /// </summary>
        Pending,

        /// <summary>
        /// The step's action and all of its assertions completed without error.
        /// </summary>
        Passed,

        /// <summary>
        /// The step's action threw or one of its assertions did not pass.
        /// </summary>
        Failed,

        /// <summary>
        /// The step did not run because an earlier step failed and the journey stops on first failure.
        /// </summary>
        Skipped
    }
}
