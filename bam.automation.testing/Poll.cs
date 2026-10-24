using System.Diagnostics;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// Polling waits for conditions that become true asynchronously — a redirect landing, an order status changing,
    /// an element appearing. Every wait in this library (journey steps, <see cref="HttpJsonProbe"/>,
    /// <see cref="StripeCheckoutPage"/>) is built on <see cref="UntilAsync(Func{Task{bool}}, TimeSpan, TimeSpan?)"/>
    /// so timeout and interval semantics are the same everywhere.
    /// </summary>
    public static class Poll
    {
        /// <summary>
        /// The interval used between condition checks when none is specified: 500 milliseconds.
        /// </summary>
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Repeatedly evaluates the condition until it returns true or the timeout elapses. The condition is always
        /// evaluated at least once, even for a zero timeout. Exceptions thrown by the condition propagate immediately.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="timeout">How long to keep polling.</param>
        /// <param name="interval">The delay between evaluations; defaults to <see cref="DefaultInterval"/>.</param>
        /// <returns>True if the condition became true within the timeout; otherwise false.</returns>
        public static async Task<bool> UntilAsync(Func<Task<bool>> condition, TimeSpan timeout, TimeSpan? interval = null)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            TimeSpan delay = interval ?? DefaultInterval;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                if (await condition())
                {
                    return true;
                }

                if (stopwatch.Elapsed >= timeout)
                {
                    return false;
                }

                TimeSpan remaining = timeout - stopwatch.Elapsed;
                await Task.Delay(remaining < delay ? remaining : delay);
            }
        }

        /// <summary>
        /// Repeatedly evaluates the synchronous condition until it returns true or the timeout elapses.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="timeout">How long to keep polling.</param>
        /// <param name="interval">The delay between evaluations; defaults to <see cref="DefaultInterval"/>.</param>
        /// <returns>True if the condition became true within the timeout; otherwise false.</returns>
        public static Task<bool> UntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan? interval = null)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            return UntilAsync(() => Task.FromResult(condition()), timeout, interval);
        }

        /// <summary>
        /// Like <see cref="UntilAsync(Func{Task{bool}}, TimeSpan, TimeSpan?)"/> but throws a
        /// <see cref="TimeoutException"/> carrying the description when the condition never became true.
        /// </summary>
        /// <param name="description">What was being waited for, used in the timeout message.</param>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="timeout">How long to keep polling.</param>
        /// <param name="interval">The delay between evaluations; defaults to <see cref="DefaultInterval"/>.</param>
        public static async Task UntilOrThrowAsync(string description, Func<Task<bool>> condition, TimeSpan timeout, TimeSpan? interval = null)
        {
            bool satisfied = await UntilAsync(condition, timeout, interval);
            if (!satisfied)
            {
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0.#}s waiting until {description}");
            }
        }
    }
}
