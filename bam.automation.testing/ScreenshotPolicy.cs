namespace Bam.Automation.Testing
{
    /// <summary>
    /// Controls when a <see cref="Journey"/> captures a screenshot for a <see cref="JourneyStep"/>.
    /// </summary>
    public enum ScreenshotPolicy
    {
        /// <summary>
        /// Use the owning journey's <see cref="Journey.DefaultScreenshotPolicy"/>.
        /// </summary>
        Inherit,

        /// <summary>
        /// Never capture a screenshot for the step.
        /// </summary>
        Never,

        /// <summary>
        /// Capture a screenshot only when the step fails.
        /// </summary>
        OnFailure,

        /// <summary>
        /// Capture a screenshot after every execution of the step, passed or failed.
        /// </summary>
        Always
    }
}
