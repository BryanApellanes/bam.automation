namespace Bam.Automation.Testing
{
    /// <summary>
    /// The selectors <see cref="StripeCheckoutPage"/> uses on Stripe hosted Checkout (<c>checkout.stripe.com</c>),
    /// centralized so a Stripe UI change is one fix. Override individual properties when Stripe drifts.
    /// </summary>
    public class StripeCheckoutSelectors
    {
        /// <summary>
        /// Gets or sets the customer email input.
        /// </summary>
        public string Email { get; set; } = "#email";

        /// <summary>
        /// Gets or sets the card number input.
        /// </summary>
        public string CardNumber { get; set; } = "#cardNumber";

        /// <summary>
        /// Gets or sets the card expiry input.
        /// </summary>
        public string CardExpiry { get; set; } = "#cardExpiry";

        /// <summary>
        /// Gets or sets the card security code input.
        /// </summary>
        public string CardCvc { get; set; } = "#cardCvc";

        /// <summary>
        /// Gets or sets the cardholder name input.
        /// </summary>
        public string BillingName { get; set; } = "#billingName";

        /// <summary>
        /// Gets or sets the billing postal code input.
        /// </summary>
        public string BillingPostalCode { get; set; } = "#billingPostalCode";

        /// <summary>
        /// Gets or sets the billing country select.
        /// </summary>
        public string BillingCountry { get; set; } = "#billingCountry";

        /// <summary>
        /// Gets or sets the pay / submit button.
        /// </summary>
        public string Submit { get; set; } = ".SubmitButton, button[type=submit]";

        /// <summary>
        /// Gets the selectors tried in order to dismiss a Stripe Link prompt when it appears.
        /// </summary>
        public List<string> LinkDismiss { get; } = new List<string>
        {
            "[data-testid=\"link-opt-out\"]",
            "button[aria-label=\"Close\"]",
            ".LinkOptInHeaderButton",
            "[data-testid=\"link-cancel\"]"
        };

        /// <summary>
        /// Gets every input selector the page object types into or selects, for contract checks against a fixture.
        /// </summary>
        public IEnumerable<string> FormFields
        {
            get
            {
                yield return Email;
                yield return CardNumber;
                yield return CardExpiry;
                yield return CardCvc;
                yield return BillingName;
                yield return BillingPostalCode;
                yield return BillingCountry;
            }
        }
    }
}
