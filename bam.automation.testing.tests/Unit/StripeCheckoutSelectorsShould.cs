using System.Text.RegularExpressions;
using Bam.Automation.Testing.Tests.TestClasses;
using Bam.Test;

namespace Bam.Automation.Testing.Tests.Unit
{
    [UnitTestMenu("StripeCheckoutSelectorsShould")]
    public class StripeCheckoutSelectorsShould : UnitTestMenuContainer
    {
        [UnitTest]
        public void MatchEveryFieldInTheCheckoutFixture()
        {
            When.A<StripeCheckoutSelectors>("is compared with the fixture page", (selectors) =>
            {
                string html = Fixtures.ReadText(Fixtures.StripeCheckout);
                HashSet<string> ids = new HashSet<string>(Regex.Matches(html, "id=\"([^\"]+)\"").Select(match => "#" + match.Groups[1].Value), StringComparer.Ordinal);
                string[] missing = selectors.FormFields.Where(selector => !ids.Contains(selector)).ToArray();
                bool hasSubmit = html.Contains("class=\"SubmitButton\"");
                bool hasLinkDismiss = selectors.LinkDismiss.Any(selector => selector.Contains("link-opt-out")) && html.Contains("data-testid=\"link-opt-out\"");
                return new ContractOutcome(missing, hasSubmit, hasLinkDismiss, selectors.FormFields.Count());
            })
            .TheTest
            .ShouldPass<ContractOutcome>((because, outcome) =>
            {
                because.ItsTrue("every form field selector exists in the fixture", outcome.Missing.Length == 0, $"missing: {string.Join(", ", outcome.Missing)}");
                because.ItsTrue("the fixture has the submit button class", outcome.HasSubmit);
                because.ItsTrue("the fixture has a Link dismiss control the selectors know", outcome.HasLinkDismiss);
                because.ItsTrue("seven form fields are covered", outcome.FieldCount == 7);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RecognizeCheckoutUrls()
        {
            When.A<StripeCheckoutSelectors>("classifies urls", (selectors) =>
                new UrlOutcome(
                    StripeCheckoutPage.IsCheckoutUrl("https://checkout.stripe.com/c/pay/cs_live_abc"),
                    StripeCheckoutPage.IsCheckoutUrl("https://airroamer.co/order/cs_live_abc"),
                    StripeCheckoutPage.IsCheckoutUrl(null)))
            .TheTest
            .ShouldPass<UrlOutcome>((because, outcome) =>
            {
                because.ItsTrue("a checkout.stripe.com url is checkout", outcome.CheckoutIsCheckout);
                because.ItsFalse("the merchant url is not checkout", outcome.MerchantIsCheckout);
                because.ItsFalse("null is not checkout", outcome.NullIsCheckout);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record ContractOutcome(string[] Missing, bool HasSubmit, bool HasLinkDismiss, int FieldCount);

        private sealed record UrlOutcome(bool CheckoutIsCheckout, bool MerchantIsCheckout, bool NullIsCheckout);
    }
}
