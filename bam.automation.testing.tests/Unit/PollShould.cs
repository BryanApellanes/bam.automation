using Bam.Test;

namespace Bam.Automation.Testing.Tests.Unit
{
    [UnitTestMenu("PollShould")]
    public class PollShould : UnitTestMenuContainer
    {
        [UnitTest]
        public void ReturnTrueOnceConditionBecomesTrue()
        {
            When.A<Counter>("polls until the third check", (counter) =>
            {
                bool satisfied = Poll.UntilAsync(() => ++counter.Count >= 3, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10)).GetAwaiter().GetResult();
                return new PollOutcome(satisfied, counter.Count);
            })
            .TheTest
            .ShouldPass<PollOutcome>((because, outcome) =>
            {
                because.ItsTrue("the wait was satisfied", outcome.Satisfied);
                because.ItsTrue("the condition was evaluated three times", outcome.Evaluations == 3, $"evaluated {outcome.Evaluations} times");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ReturnFalseAfterTimeoutHavingEvaluatedAtLeastOnce()
        {
            When.A<Counter>("polls a condition that never becomes true", (counter) =>
            {
                DateTime started = DateTime.UtcNow;
                bool satisfied = Poll.UntilAsync(() => { counter.Count++; return false; }, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(20)).GetAwaiter().GetResult();
                return new TimeoutOutcome(satisfied, counter.Count, DateTime.UtcNow - started);
            })
            .TheTest
            .ShouldPass<TimeoutOutcome>((because, outcome) =>
            {
                because.ItsFalse("the wait was not satisfied", outcome.Satisfied);
                because.ItsTrue("the condition ran more than once", outcome.Evaluations > 1, $"evaluated {outcome.Evaluations} times");
                because.ItsTrue("the wait respected the timeout", outcome.Elapsed >= TimeSpan.FromMilliseconds(100) && outcome.Elapsed < TimeSpan.FromSeconds(3), $"elapsed {outcome.Elapsed}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ZeroTimeoutStillEvaluatesOnce()
        {
            When.A<Counter>("polls with a zero timeout", (counter) =>
            {
                bool satisfied = Poll.UntilAsync(() => { counter.Count++; return true; }, TimeSpan.Zero).GetAwaiter().GetResult();
                return new PollOutcome(satisfied, counter.Count);
            })
            .TheTest
            .ShouldPass<PollOutcome>((because, outcome) =>
            {
                because.ItsTrue("an immediately-true condition is satisfied", outcome.Satisfied);
                because.ItsTrue("evaluated exactly once", outcome.Evaluations == 1);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void UntilOrThrowDescribesWhatWasAwaited()
        {
            When.A<Counter>("waits for something that never happens", (counter) =>
            {
                Poll.UntilOrThrowAsync("the order is Delivered", () => Task.FromResult(false), TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(10)).GetAwaiter().GetResult();
            })
            .ExpectException(true)
            .TheTest
            .ShouldPass(because =>
            {
                Exception? exception = because.TestCase.Exception;
                because.ItsTrue("threw TimeoutException", exception is TimeoutException, exception?.GetType().Name);
                because.ItsTrue("names what was awaited", exception != null && exception.Message.Contains("the order is Delivered"), exception?.Message);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        public class Counter
        {
            public int Count { get; set; }
        }

        private sealed record PollOutcome(bool Satisfied, int Evaluations);

        private sealed record TimeoutOutcome(bool Satisfied, int Evaluations, TimeSpan Elapsed);
    }
}
