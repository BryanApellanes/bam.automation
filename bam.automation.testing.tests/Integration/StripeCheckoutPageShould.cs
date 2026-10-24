using Bam.Automation.Testing.Tests.TestClasses;
using Bam.Test;
using Bam.Test.Integration;
using BamBot.Automation;

namespace Bam.Automation.Testing.Tests.Integration
{
    /// <summary>
    /// Drives the local Stripe Checkout fixture (same field ids as checkout.stripe.com, no network) with a real
    /// browser. The "card" values are Stripe's public test numbers, not a real card.
    /// </summary>
    [IntegrationTestMenu("StripeCheckoutPageShould")]
    public class StripeCheckoutPageShould : IntegrationTestMenuContainer
    {
        private const string SecretsContent = "CUSTOMER_EMAIL=buyer@example.test\nCARD_NUMBER=4242424242424242\nCARD_EXPIRY=12/34\nCARD_CVC=123\nCARD_NAME=Test Buyer\nCARD_POSTAL_CODE=94107\nCARD_COUNTRY=ES\n";

        [IntegrationTest]
        public void FillTheFormDismissLinkSubmitAndSeeTheRedirect()
        {
            EnvFileSecrets secrets = EnvFileSecrets.Parse(SecretsContent, "inline");
            BrowserSessionOptions options = BrowserSessionOptions.FromEnvironment();
            options.UserDataDirectory = Fixtures.NewTempDirectory("profile");

            When.A<BrowserSession>("drives the checkout fixture", () => new BrowserSession(options), (session) =>
            {
                using (session)
                {
                    AutomationPage page = session.Open(Fixtures.UrlTo(Fixtures.StripeCheckout));
                    StripeCheckoutPage checkout = new StripeCheckoutPage(page) { OptionalFieldTimeout = TimeSpan.FromMilliseconds(500) };
                    bool dismissed = checkout.DismissLinkIfShownAsync().GetAwaiter().GetResult();
                    bool dismissedAgain = checkout.DismissLinkIfShownAsync().GetAwaiter().GetResult();
                    checkout.FillCardAsync(secrets).GetAwaiter().GetResult();
                    FilledForm filled = new FilledForm(
                        page.GetElementValueAsync("#email").GetAwaiter().GetResult(),
                        page.GetElementValueAsync("#cardNumber").GetAwaiter().GetResult(),
                        page.GetElementValueAsync("#cardExpiry").GetAwaiter().GetResult(),
                        page.GetElementValueAsync("#cardCvc").GetAwaiter().GetResult(),
                        page.GetElementValueAsync("#billingName").GetAwaiter().GetResult(),
                        page.GetElementValueAsync("#billingPostalCode").GetAwaiter().GetResult(),
                        page.GetElementValueAsync("#billingCountry").GetAwaiter().GetResult());
                    checkout.SubmitAsync().GetAwaiter().GetResult();
                    string destination = checkout.WaitForRedirectAsync("paid", TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                    return new CheckoutOutcome(dismissed, dismissedAgain, filled, destination);
                }
            })
            .TheTest
            .ShouldPass<CheckoutOutcome>((because, outcome) =>
            {
                because.ItsTrue("the Link prompt was dismissed", outcome.Dismissed);
                because.ItsFalse("a second dismiss finds nothing", outcome.DismissedAgain);
                because.ItsTrue("email typed", outcome.Form.Email == "buyer@example.test", outcome.Form.Email);
                because.ItsTrue("card number typed", outcome.Form.CardNumber == "4242424242424242", outcome.Form.CardNumber);
                because.ItsTrue("expiry typed", outcome.Form.CardExpiry == "12/34");
                because.ItsTrue("cvc typed", outcome.Form.CardCvc == "123");
                because.ItsTrue("name typed", outcome.Form.BillingName == "Test Buyer", outcome.Form.BillingName);
                because.ItsTrue("postal code typed", outcome.Form.PostalCode == "94107");
                because.ItsTrue("country selected", outcome.Form.Country == "ES", outcome.Form.Country);
                because.ItsTrue("the redirect landed on the paid url", outcome.Destination.EndsWith("#paid"), outcome.Destination);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [IntegrationTest]
        public void RefuseToPayWhenACardValueIsBlank()
        {
            EnvFileSecrets secrets = EnvFileSecrets.Parse("CARD_NUMBER=4242424242424242\nCARD_EXPIRY=\nCARD_CVC=123\nCARD_NAME=\n", "inline");
            BrowserSessionOptions options = BrowserSessionOptions.FromEnvironment();
            options.UserDataDirectory = Fixtures.NewTempDirectory("profile");

            When.A<BrowserSession>("tries to fill with blanks", () => new BrowserSession(options), (session) =>
            {
                using (session)
                {
                    AutomationPage page = session.Open(Fixtures.UrlTo(Fixtures.StripeCheckout));
                    StripeCheckoutPage checkout = new StripeCheckoutPage(page);
                    try
                    {
                        checkout.FillCardAsync(secrets).GetAwaiter().GetResult();
                        return new RefusalOutcome(null, page.GetElementValueAsync("#cardNumber").GetAwaiter().GetResult());
                    }
                    catch (MissingSecretException ex)
                    {
                        return new RefusalOutcome(ex, page.GetElementValueAsync("#cardNumber").GetAwaiter().GetResult());
                    }
                }
            })
            .TheTest
            .ShouldPass<RefusalOutcome>((because, outcome) =>
            {
                because.ItsTrue("MissingSecretException was thrown", outcome.Exception != null);
                because.ItsTrue("it names both blank keys", outcome.Exception != null && outcome.Exception.Keys.Contains("CARD_EXPIRY") && outcome.Exception.Keys.Contains("CARD_NAME"));
                because.ItsTrue("nothing was typed before refusing", string.IsNullOrEmpty(outcome.CardNumberOnPage), outcome.CardNumberOnPage);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record FilledForm(string Email, string CardNumber, string CardExpiry, string CardCvc, string BillingName, string PostalCode, string Country);

        private sealed record CheckoutOutcome(bool Dismissed, bool DismissedAgain, FilledForm Form, string Destination);

        private sealed record RefusalOutcome(MissingSecretException? Exception, string CardNumberOnPage);
    }
}
