namespace Bam.Automation.Testing
{
    /// <summary>
    /// Raised by <see cref="Journey.StepCompleted"/> after each step so a live report can be refreshed while the
    /// owner watches.
    /// </summary>
    public class JourneyStepCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates the event args.
        /// </summary>
        /// <param name="journey">The journey.</param>
        /// <param name="result">The run result so far.</param>
        /// <param name="step">The step that just completed.</param>
        public JourneyStepCompletedEventArgs(Journey journey, JourneyResult result, JourneyStepResult step)
        {
            Journey = journey;
            Result = result;
            Step = step;
        }

        /// <summary>
        /// Gets the journey.
        /// </summary>
        public Journey Journey { get; }

        /// <summary>
        /// Gets the run result so far (steps not yet run are absent).
        /// </summary>
        public JourneyResult Result { get; }

        /// <summary>
        /// Gets the step that just completed.
        /// </summary>
        public JourneyStepResult Step { get; }
    }
}
