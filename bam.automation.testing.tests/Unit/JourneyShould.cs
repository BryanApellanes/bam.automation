using Bam.Automation.Testing.Tests.TestClasses;
using Bam.Test;
using BamBot.Automation;

namespace Bam.Automation.Testing.Tests.Unit
{
    [UnitTestMenu("JourneyShould")]
    public class JourneyShould : UnitTestMenuContainer
    {
        [UnitTest]
        public void RunStepsInOrderAndRecordEachResult()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#name");
            page.PresentSelectors.Add("#go");

            When.A<Journey>("runs navigate, type and click", () => NewJourney("ordered"), (journey) =>
            {
                journey.Navigate("https://fixture.test/home").Type("#name", "Bam").Click("#go");
                JourneyResult result = journey.Run(page);
                return new OrderedOutcome(result, page.Log.ToArray(), journey.Sequence.Steps.Count);
            })
            .TheTest
            .ShouldPass<OrderedOutcome>((because, outcome) =>
            {
                because.ItsTrue("the journey succeeded", outcome.Result.Succeeded, outcome.Result.FailureSummary);
                because.ItsTrue("three results were recorded", outcome.Result.Steps.Count == 3);
                because.ItsTrue("results are numbered in order", outcome.Result.Steps.Select(step => step.Number).SequenceEqual(new int[] { 1, 2, 3 }));
                because.ItsTrue("actions ran in order", outcome.Log.Where(entry => !entry.StartsWith("screenshot")).SequenceEqual(new string[] { "goto:https://fixture.test/home", "type:#name", "click:#go" }), string.Join(",", outcome.Log));
                because.ItsTrue("each step recorded the page url", outcome.Result.Steps.All(step => step.Url == "https://fixture.test/home"));
                because.ItsTrue("each step has a duration", outcome.Result.Steps.All(step => step.Duration >= TimeSpan.Zero));
                because.ItsTrue("the sequence mirrors the steps", outcome.SequenceStepCount == 3);
                because.ItsTrue("the start url came from the first navigation", outcome.Result.Name == "ordered");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void StopOnFirstFailureAndSkipTheRest()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#broken");
            page.PresentSelectors.Add("#after");
            page.FailingSelectors.Add("#broken");

            When.A<Journey>("fails on the second step", () => NewJourney("stops"), (journey) =>
            {
                journey.Navigate("https://fixture.test/a").Click("#broken").Click("#after");
                return journey.Run(page);
            })
            .TheTest
            .ShouldPass<JourneyResult>((because, result) =>
            {
                because.ItsFalse("the journey failed", result.Succeeded);
                because.ItsTrue("step one passed", result.Steps[0].Status == JourneyStepStatus.Passed);
                because.ItsTrue("step two failed", result.Steps[1].Status == JourneyStepStatus.Failed);
                because.ItsTrue("step two carries the exception type", result.Steps[1].ExceptionType == nameof(InvalidOperationException), result.Steps[1].ExceptionType);
                because.ItsTrue("step three was skipped", result.Steps[2].Status == JourneyStepStatus.Skipped);
                because.ItsTrue("the failure summary names step two", result.FailureSummary.Contains("Step 2") && result.FailureSummary.Contains("#broken"), result.FailureSummary);
                because.ItsTrue("counts are right", result.PassedCount == 1 && result.FailedCount == 1 && result.SkippedCount == 1);
                because.ItsTrue("the after step never ran", !page.Log.Contains("click:#after"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ContinueOnFailureWhenAsked()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#broken");
            page.PresentSelectors.Add("#after");
            page.FailingSelectors.Add("#broken");

            When.A<Journey>("continues past a failure", () => NewJourney("continues"), (journey) =>
            {
                journey.ContinueOnFailure = true;
                journey.Navigate("https://fixture.test/a").Click("#broken").Click("#after");
                return journey.Run(page);
            })
            .TheTest
            .ShouldPass<JourneyResult>((because, result) =>
            {
                because.ItsFalse("the journey still failed", result.Succeeded);
                because.ItsTrue("step three ran and passed", result.Steps[2].Status == JourneyStepStatus.Passed);
                because.ItsTrue("the after step ran", page.Log.Contains("click:#after"));
                because.ItsTrue("nothing was skipped", result.SkippedCount == 0);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NeverLeakASecretValueIntoResultsOrReport()
        {
            const string secretValue = "4000056655665556";
            EnvFileSecrets secrets = EnvFileSecrets.Parse($"CARD_NUMBER={secretValue}\n", "inline");
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#cardNumber");
            page.FailingSelectors.Add("#cardNumber");

            When.A<Journey>("types a secret into a field that throws echoing the value", () => NewJourney("secret"), (journey) =>
            {
                journey.NoteMasked(secrets, "CARD_NUMBER").TypeSecret("#cardNumber", secrets, "CARD_NUMBER");
                JourneyResult result = journey.Run(page);
                JourneyReport report = new JourneyReport(result);
                return new SecretOutcome(result, report.ToMarkdown(), report.ToJson());
            })
            .TheTest
            .ShouldPass<SecretOutcome>((because, outcome) =>
            {
                JourneyStepResult step = outcome.Result.Steps[0];
                because.ItsTrue("the step failed", step.Status == JourneyStepStatus.Failed);
                because.ItsTrue("the step name carries the key", step.Name.Contains("CARD_NUMBER"));
                because.ItsFalse("the step name does not carry the value", step.Name.Contains(secretValue));
                because.ItsFalse("the message does not carry the value", step.Message.Contains(secretValue), step.Message);
                because.ItsTrue("the message marks the redaction", step.Message.Contains("[REDACTED:CARD_NUMBER]"), step.Message);
                because.ItsFalse("the markdown does not carry the value", outcome.Markdown.Contains(secretValue));
                because.ItsFalse("the json does not carry the value", outcome.Json.Contains(secretValue));
                because.ItsTrue("the masked note is on the result", outcome.Result.Notes["CARD_NUMBER"] == "len=16 prefix=4000", outcome.Result.Notes["CARD_NUMBER"]);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CaptureScreenshotsPerPolicy()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#ok");
            page.FailingSelectors.Add("#bad");
            page.PresentSelectors.Add("#bad");

            When.A<Journey>("runs with mixed screenshot policies", () => NewJourney("shots"), (journey) =>
            {
                journey.DefaultScreenshotPolicy = ScreenshotPolicy.OnFailure;
                journey.ContinueOnFailure = true;
                journey.Add(JourneyStep.Click("#ok"))
                    .Add(JourneyStep.Click("#ok").WithScreenshot(ScreenshotPolicy.Always))
                    .Add(JourneyStep.Click("#bad"))
                    .Screenshot("Final State!")
                    .Add(JourneyStep.Click("#ok").WithScreenshot(ScreenshotPolicy.Never));
                return journey.Run(page);
            })
            .TheTest
            .ShouldPass<JourneyResult>((because, result) =>
            {
                because.ItsTrue("OnFailure default skips a passing step", result.Steps[0].ScreenshotPath == null);
                because.ItsTrue("Always captures a passing step", result.Steps[1].ScreenshotPath != null && File.Exists(result.Steps[1].ScreenshotPath));
                because.ItsTrue("Always names the file by number and step", Path.GetFileName(result.Steps[1].ScreenshotPath ?? string.Empty) == "02-click-ok.png", result.Steps[1].ScreenshotPath);
                because.ItsTrue("OnFailure captures the failing step", result.Steps[2].ScreenshotPath != null && File.Exists(result.Steps[2].ScreenshotPath));
                because.ItsTrue("a Screenshot step is numbered and slugged by its own name", Path.GetFileName(result.Steps[3].ScreenshotPath ?? string.Empty) == "04-final-state.png", result.Steps[3].ScreenshotPath);
                because.ItsTrue("Never captures nothing", result.Steps[4].ScreenshotPath == null);
                because.ItsTrue("screenshots live in the artifacts directory", result.Steps.Where(step => step.ScreenshotPath != null).All(step => step.ScreenshotPath!.StartsWith(result.ArtifactsDirectory)));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FailAStepWhoseAssertionDoesNotPass()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#go");
            page.Texts["#greeting"] = "Hello, Bam!";

            When.A<Journey>("runs steps with assertions", () => NewJourney("asserts"), (journey) =>
            {
                journey.ContinueOnFailure = true;
                journey.Add(JourneyStep.Click("#go").Expect(new PageAssertion("greeting text", (p) =>
                {
                    string text = p.GetElementTextAsync("#greeting").GetAwaiter().GetResult();
                    if (text != "Hello, Bam!")
                    {
                        throw new AutomationAssertionException(p, $"unexpected greeting {text}");
                    }
                })))
                .Assert(new PageAssertion("wrong text", (p) => throw new AutomationAssertionException(p, "the text was wrong")))
                .Add(JourneyStep.Click("#go").ExpectSelector("#missing"));
                return journey.Run(page);
            })
            .TheTest
            .ShouldPass<JourneyResult>((because, result) =>
            {
                because.ItsTrue("a passing assertion leaves the step passed", result.Steps[0].Status == JourneyStepStatus.Passed, result.Steps[0].Message);
                because.ItsTrue("a throwing assertion fails the step", result.Steps[1].Status == JourneyStepStatus.Failed);
                because.ItsTrue("the assertion name and message are reported", result.Steps[1].Message.Contains("wrong text") && result.Steps[1].Message.Contains("the text was wrong"), result.Steps[1].Message);
                because.ItsTrue("a missing selector assertion fails the step", result.Steps[2].Status == JourneyStepStatus.Failed);
                because.ItsTrue("the missing selector is named", result.Steps[2].Message.Contains("#missing"), result.Steps[2].Message);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FollowRedirectsWithWaitForUrlAndTimeOutWhenTheyNeverCome()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#pay");
            page.ClickNavigatesTo["#pay"] = "https://checkout.stripe.com/c/pay/cs_test";

            When.A<Journey>("waits for a redirect", () => NewJourney("redirect"), (journey) =>
            {
                journey.ContinueOnFailure = true;
                journey.Click("#pay")
                    .WaitForUrl("checkout.stripe.com", TimeSpan.FromSeconds(2))
                    .WaitForUrl("merchant success page", (url) => url.Contains("/order/"), TimeSpan.FromMilliseconds(150));
                return journey.Run(page);
            })
            .TheTest
            .ShouldPass<JourneyResult>((because, result) =>
            {
                because.ItsTrue("the redirect was observed", result.Steps[1].Status == JourneyStepStatus.Passed, result.Steps[1].Message);
                because.ItsTrue("the url after the wait is the checkout url", result.Steps[1].Url.Contains("checkout.stripe.com"));
                because.ItsTrue("a redirect that never comes times out", result.Steps[2].Status == JourneyStepStatus.Failed);
                because.ItsTrue("the timeout is a TimeoutException naming the last url", result.Steps[2].ExceptionType == nameof(TimeoutException) && result.Steps[2].Message.Contains("checkout.stripe.com"), result.Steps[2].Message);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void PollAnArbitraryConditionWithWaitUntil()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            int polls = 0;

            When.A<Journey>("waits until an out-of-band condition holds", () => NewJourney("until"), (journey) =>
            {
                journey.WaitUntil("the order is Delivered", () => Task.FromResult(++polls >= 3), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10));
                return journey.Run(page);
            })
            .TheTest
            .ShouldPass<JourneyResult>((because, result) =>
            {
                because.ItsTrue("the condition was satisfied", result.Succeeded, result.FailureSummary);
                because.ItsTrue("the condition was polled three times", polls == 3, $"polled {polls} times");
                because.ItsTrue("the step is named by its description", result.Steps[0].Name.Contains("the order is Delivered"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RaiseStepCompletedForEveryStepAndCompletedOnce()
        {
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#a");
            List<int> completedNumbers = new List<int>();
            int completedEvents = 0;

            When.A<Journey>("raises events while running", () => NewJourney("events"), (journey) =>
            {
                journey.StepCompleted += (sender, args) => completedNumbers.Add(args.Step.Number);
                journey.Completed += (sender, args) => completedEvents++;
                journey.Click("#a").Click("#a").Click("#a");
                return journey.Run(page);
            })
            .TheTest
            .ShouldPass<JourneyResult>((because, result) =>
            {
                because.ItsTrue("StepCompleted fired per step in order", completedNumbers.SequenceEqual(new int[] { 1, 2, 3 }), string.Join(",", completedNumbers));
                because.ItsTrue("Completed fired once", completedEvents == 1);
                because.ItsTrue("the result is no longer running", !result.IsRunning);
                because.ItsTrue("LastResult is the returned result", ReferenceEquals(because.ObjectUnderTest<Journey>().LastResult, result));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void FailWithNoStepsAndSlugifyNames()
        {
            When.A<Journey>("runs with no steps", () => NewJourney("Spain: cheapest plan / Buy!"), (journey) => new EmptyOutcome(journey.Run(new FakeAutomationPage()), journey.Slug))
            .TheTest
            .ShouldPass<EmptyOutcome>((because, outcome) =>
            {
                because.ItsFalse("an empty journey does not succeed", outcome.Result.Succeeded);
                because.ItsTrue("the failure summary says why", outcome.Result.FailureSummary.Contains("no steps"), outcome.Result.FailureSummary);
                because.ItsTrue("the slug is file-system safe", outcome.Slug == "spain-cheapest-plan-buy", outcome.Slug);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private static Journey NewJourney(string name)
        {
            Journey journey = new Journey(name);
            journey.ArtifactsDirectory = Fixtures.NewTempDirectory("journeys");
            return journey;
        }

        [UnitTest]
        public void FailWithTimeoutNotArgumentExceptionOnAZeroWaitForAMissingSelector()
        {
            FakeAutomationPage page = new FakeAutomationPage();

            When.A<Journey>("clicks a missing selector with no wait", () => NewJourney("zero-wait"), (journey) =>
            {
                journey.Click("#missing", TimeSpan.Zero);
                return journey.Run(page);
            })
            .TheTest
            .ShouldPass<JourneyResult>((because, result) =>
            {
                because.ItsTrue("the step failed", result.Steps[0].Status == JourneyStepStatus.Failed);
                because.ItsTrue("with a TimeoutException", result.Steps[0].ExceptionType == nameof(TimeoutException), result.Steps[0].ExceptionType);
                because.ItsTrue("naming the selector", (result.Steps[0].Message ?? string.Empty).Contains("#missing"), result.Steps[0].Message);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void NeverScreenshotStepsThatTypeSecrets()
        {
            EnvFileSecrets secrets = EnvFileSecrets.Parse("CUSTOMER_EMAIL=buyer@example.test\nCARD_NUMBER=4242424242424242\nCARD_EXPIRY=12/34\nCARD_CVC=123\nCARD_NAME=Buyer\n", "inline");
            FakeAutomationPage page = new FakeAutomationPage();
            page.PresentSelectors.Add("#email");
            page.PresentSelectors.Add("#ok");
            JourneyStep[] paymentSteps = StripeCheckoutPage.PaymentSteps(secrets, "/order/").ToArray();

            When.A<Journey>("types a secret under an Always default", () => NewJourney("secret-shots"), (journey) =>
            {
                journey.DefaultScreenshotPolicy = ScreenshotPolicy.Always;
                journey.TypeSecret("#email", secrets, "CUSTOMER_EMAIL").Click("#ok");
                return new ScreenshotPolicyOutcome(journey.Run(page), paymentSteps.Select(step => step.ScreenshotPolicy).ToArray());
            })
            .TheTest
            .ShouldPass<ScreenshotPolicyOutcome>((because, outcome) =>
            {
                because.ItsTrue("both steps passed", outcome.Result.Succeeded, outcome.Result.FailureSummary);
                because.ItsTrue("the secret step captured nothing", outcome.Result.Steps[0].ScreenshotPath == null, outcome.Result.Steps[0].ScreenshotPath);
                because.ItsTrue("the plain step captured a screenshot", outcome.Result.Steps[1].ScreenshotPath != null);
                because.ItsTrue("the wait for Stripe Checkout inherits the journey policy", outcome.PaymentPolicies[0] == ScreenshotPolicy.Inherit);
                because.ItsTrue("fill, dismiss and submit never capture", outcome.PaymentPolicies[1] == ScreenshotPolicy.Never && outcome.PaymentPolicies[2] == ScreenshotPolicy.Never && outcome.PaymentPolicies[3] == ScreenshotPolicy.Never);
                because.ItsTrue("the redirect wait captures only on success", outcome.PaymentPolicies[4] == ScreenshotPolicy.OnSuccess);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void CaptureTheRedirectWaitOnlyOnceTheRedirectLanded()
        {
            EnvFileSecrets secrets = EnvFileSecrets.Parse("CARD_NUMBER=4242424242424242\nCARD_EXPIRY=12/34\nCARD_CVC=123\nCARD_NAME=Buyer\n", "inline");
            FakeAutomationPage stuckOnCheckout = new FakeAutomationPage("https://checkout.stripe.com/c/pay/cs_live_abc");
            FakeAutomationPage landed = new FakeAutomationPage("https://fixture.test/order/cs_live_abc");

            When.A<Journey>("waits for the post-payment redirect under an Always default", () => NewJourney("redirect-shots"), (journey) =>
            {
                journey.DefaultScreenshotPolicy = ScreenshotPolicy.Always;
                journey.Add(StripeCheckoutPage.PaymentSteps(secrets, "/order/", TimeSpan.FromMilliseconds(200)).Last());
                JourneyResult failed = journey.Run(stuckOnCheckout);
                Journey second = NewJourney("redirect-shots-landed");
                second.DefaultScreenshotPolicy = ScreenshotPolicy.Always;
                second.Add(StripeCheckoutPage.PaymentSteps(secrets, "/order/", TimeSpan.FromMilliseconds(200)).Last());
                JourneyResult passed = second.Run(landed);
                return new RedirectScreenshotOutcome(failed, passed);
            })
            .TheTest
            .ShouldPass<RedirectScreenshotOutcome>((because, outcome) =>
            {
                because.ItsTrue("a redirect that never comes fails the step", outcome.StuckOnCheckout.Steps[0].Status == JourneyStepStatus.Failed, outcome.StuckOnCheckout.Steps[0].Message);
                because.ItsTrue("the failed wait captured no screenshot of the filled form", outcome.StuckOnCheckout.Steps[0].ScreenshotPath == null && stuckOnCheckout.ScreenshotCount == 0, outcome.StuckOnCheckout.Steps[0].ScreenshotPath);
                because.ItsTrue("a landed redirect passes the step", outcome.Landed.Steps[0].Status == JourneyStepStatus.Passed, outcome.Landed.Steps[0].Message);
                because.ItsTrue("the passed wait captured the merchant page", outcome.Landed.Steps[0].ScreenshotPath != null && File.Exists(outcome.Landed.Steps[0].ScreenshotPath));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record RedirectScreenshotOutcome(JourneyResult StuckOnCheckout, JourneyResult Landed);

        private sealed record OrderedOutcome(JourneyResult Result, string[] Log, int SequenceStepCount);

        private sealed record ScreenshotPolicyOutcome(JourneyResult Result, ScreenshotPolicy[] PaymentPolicies);

        private sealed record SecretOutcome(JourneyResult Result, string Markdown, string Json);

        private sealed record EmptyOutcome(JourneyResult Result, string Slug);
    }
}
