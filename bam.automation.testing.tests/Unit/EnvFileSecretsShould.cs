using Bam.Test;

namespace Bam.Automation.Testing.Tests.Unit
{
    [UnitTestMenu("EnvFileSecretsShould")]
    public class EnvFileSecretsShould : UnitTestMenuContainer
    {
        private const string Content = "# hand-off template\n\nCUSTOMER_EMAIL=owner@example.test\nexport CARD_NUMBER=\"4242424242424242\"\nCARD_EXPIRY='12/34'\nCARD_CVC=\nTRAILING = spaced value \n# CARD_NAME=commented\n";

        [UnitTest]
        public void ParseKeyValuesIgnoringCommentsBlanksExportAndQuotes()
        {
            When.A<EnvFileSecrets>("parses env file content", () => EnvFileSecrets.Parse(Content, "inline"), (secrets) =>
                new ParseOutcome(
                    secrets.Get("CUSTOMER_EMAIL"),
                    secrets.Get("CARD_NUMBER"),
                    secrets.Get("CARD_EXPIRY"),
                    secrets.Get("TRAILING"),
                    secrets.Contains("CARD_CVC"),
                    secrets.IsBlank("CARD_CVC"),
                    secrets.Contains("CARD_NAME"),
                    secrets.Keys.Count))
            .TheTest
            .ShouldPass<ParseOutcome>((because, outcome) =>
            {
                because.ItsTrue("reads a plain value", outcome.Email == "owner@example.test");
                because.ItsTrue("strips export and double quotes", outcome.CardNumber == "4242424242424242");
                because.ItsTrue("strips single quotes", outcome.CardExpiry == "12/34");
                because.ItsTrue("trims whitespace around key and value", outcome.Trailing == "spaced value");
                because.ItsTrue("keeps a blank key", outcome.ContainsCvc);
                because.ItsTrue("reports the blank key as blank", outcome.CvcIsBlank);
                because.ItsFalse("ignores commented keys", outcome.ContainsCommentedName);
                because.ItsTrue("counts five keys", outcome.KeyCount == 5, $"expected 5 keys but found {outcome.KeyCount}");
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void ThrowMissingSecretNamingKeyButNotValue()
        {
            When.A<EnvFileSecrets>("gets a blank key", () => EnvFileSecrets.Parse(Content, "inline"), (secrets) =>
            {
                secrets.Get("CARD_CVC");
                return secrets;
            })
            .ExpectException(true)
            .TheTest
            .ShouldPass(because =>
            {
                because.TheTestCase("threw MissingSecretException", (testCase) => testCase.Exception is MissingSecretException);
                MissingSecretException? exception = because.TestCase.Exception as MissingSecretException;
                because.ItsTrue("names the missing key", exception != null && exception.Keys.Contains("CARD_CVC"));
                because.ItsTrue("names the source", exception != null && exception.Message.Contains("inline"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void RequireListsEveryBlankKey()
        {
            When.A<EnvFileSecrets>("requires several keys", () => EnvFileSecrets.Parse(Content, "inline"), (secrets) =>
            {
                secrets.Require("CARD_NUMBER", "CARD_CVC", "CARD_NAME");
                return secrets;
            })
            .ExpectException(true)
            .TheTest
            .ShouldPass(because =>
            {
                MissingSecretException? exception = because.TestCase.Exception as MissingSecretException;
                because.ItsTrue("threw MissingSecretException", exception != null);
                because.ItsTrue("lists CARD_CVC", exception != null && exception.Keys.Contains("CARD_CVC"));
                because.ItsTrue("lists CARD_NAME", exception != null && exception.Keys.Contains("CARD_NAME"));
                because.ItsFalse("does not list the present key", exception != null && exception.Keys.Contains("CARD_NUMBER"));
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void MaskScrubAndToStringNeverRevealValues()
        {
            EnvFileSecrets shortValues = EnvFileSecrets.Parse("CARD_CVC=123\nCARD_EXPIRY=12/34\nCARD_PIN=12345678\n", "short");
            When.A<EnvFileSecrets>("masks and scrubs", () => EnvFileSecrets.Parse(Content, "inline"), (secrets) =>
                new MaskOutcome(
                    secrets.Masked("CARD_NUMBER"),
                    secrets.Masked("CARD_CVC"),
                    secrets.Masked("CARD_EXPIRY", 0),
                    secrets.Scrub("typed 4242424242424242 and 12/34 into the form"),
                    secrets.ToString(),
                    shortValues.Masked("CARD_CVC"),
                    shortValues.Masked("CARD_EXPIRY"),
                    shortValues.Masked("CARD_PIN")))
            .TheTest
            .ShouldPass<MaskOutcome>((because, outcome) =>
            {
                because.ItsTrue("masked form shows length and prefix", outcome.MaskedNumber == "len=16 prefix=4242", outcome.MaskedNumber);
                because.ItsTrue("blank key masks as (blank)", outcome.MaskedBlank == "(blank)");
                because.ItsTrue("zero prefix reveals nothing", outcome.MaskedNoPrefix == "len=5 prefix=", outcome.MaskedNoPrefix);
                because.ItsFalse("scrubbed text no longer contains the card number", outcome.Scrubbed.Contains("4242424242424242"));
                because.ItsTrue("scrubbed text marks the key", outcome.Scrubbed.Contains("[REDACTED:CARD_NUMBER]") && outcome.Scrubbed.Contains("[REDACTED:CARD_EXPIRY]"), outcome.Scrubbed);
                because.ItsFalse("ToString has no values", outcome.Description.Contains("4242") || outcome.Description.Contains("owner@"));
                because.ItsTrue("ToString has the source and count", outcome.Description.Contains("inline") && outcome.Description.Contains("5 keys"), outcome.Description);
                because.ItsTrue("a 3-character value reveals nothing", outcome.MaskedShortCvc == "len=3 prefix=", outcome.MaskedShortCvc);
                because.ItsFalse("the 3-character value is absent", outcome.MaskedShortCvc.Contains("123"));
                because.ItsTrue("a 5-character value reveals nothing", outcome.MaskedShortExpiry == "len=5 prefix=", outcome.MaskedShortExpiry);
                because.ItsFalse("the 5-character value is absent", outcome.MaskedShortExpiry.Contains("12/3"));
                because.ItsTrue("an 8-character value reveals nothing with the default prefix", outcome.MaskedEightChars == "len=8 prefix=", outcome.MaskedEightChars);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        [UnitTest]
        public void LoadFromFileAndReportMissingFile()
        {
            string directory = TestClasses.Fixtures.NewTempDirectory("secrets");
            string path = Path.Combine(directory, "sample.env");
            File.WriteAllText(path, "KEY=value\n");

            When.A<EnvFileSecrets>("loads from disk", () => EnvFileSecrets.Load(path), (secrets) =>
                new LoadOutcome(secrets.Get("KEY"), secrets.Exists, EnvFileSecrets.LoadIfExists(Path.Combine(directory, "missing.env")).Exists, secrets.Source))
            .TheTest
            .ShouldPass<LoadOutcome>((because, outcome) =>
            {
                because.ItsTrue("reads the value", outcome.Value == "value");
                because.ItsTrue("reports the file exists", outcome.Exists);
                because.ItsFalse("LoadIfExists reports a missing file", outcome.MissingExists);
                because.ItsTrue("source is the path", outcome.Source == path);
            })
            .SoBeHappy()
            .UnlessItFailed();
        }

        private sealed record ParseOutcome(string Email, string CardNumber, string CardExpiry, string Trailing, bool ContainsCvc, bool CvcIsBlank, bool ContainsCommentedName, int KeyCount);

        private sealed record MaskOutcome(string MaskedNumber, string MaskedBlank, string MaskedNoPrefix, string Scrubbed, string Description, string MaskedShortCvc, string MaskedShortExpiry, string MaskedEightChars);

        private sealed record LoadOutcome(string Value, bool Exists, bool MissingExists, string Source);
    }
}
