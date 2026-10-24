namespace Bam.Automation.Testing
{
    /// <summary>
    /// The <see cref="EnvFileSecrets"/> keys <see cref="StripeCheckoutPage"/> reads — the hand-off template's
    /// key names.
    /// </summary>
    public static class StripeCheckoutSecretKeys
    {
        /// <summary>
        /// The customer email typed when Checkout asks for one.
        /// </summary>
        public const string CustomerEmail = "CUSTOMER_EMAIL";

        /// <summary>
        /// The card number.
        /// </summary>
        public const string CardNumber = "CARD_NUMBER";

        /// <summary>
        /// The card expiry as <c>MM/YY</c>.
        /// </summary>
        public const string CardExpiry = "CARD_EXPIRY";

        /// <summary>
        /// The card security code.
        /// </summary>
        public const string CardCvc = "CARD_CVC";

        /// <summary>
        /// The cardholder name.
        /// </summary>
        public const string CardName = "CARD_NAME";

        /// <summary>
        /// The billing postal code.
        /// </summary>
        public const string CardPostalCode = "CARD_POSTAL_CODE";

        /// <summary>
        /// The two-letter billing country.
        /// </summary>
        public const string CardCountry = "CARD_COUNTRY";

        /// <summary>
        /// The keys a card payment cannot proceed without.
        /// </summary>
        public static readonly string[] Required = new string[] { CardNumber, CardExpiry, CardCvc, CardName };

        /// <summary>
        /// The keys typed only when Checkout shows the matching field.
        /// </summary>
        public static readonly string[] Optional = new string[] { CustomerEmail, CardPostalCode, CardCountry };
    }
}
