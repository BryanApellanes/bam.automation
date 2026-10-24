namespace Bam.Automation.Testing.Tests.TestClasses
{
    /// <summary>
    /// Locates the static HTML fixtures copied next to the test assembly.
    /// </summary>
    public static class Fixtures
    {
        public const string JourneySample = "journey-sample.html";
        public const string StripeCheckout = "stripe-checkout.html";

        public static string PathTo(string fileName)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Fixture not found: {path}", path);
            }

            return path;
        }

        public static string UrlTo(string fileName)
        {
            return new Uri(PathTo(fileName)).AbsoluteUri;
        }

        public static string ReadText(string fileName)
        {
            return File.ReadAllText(PathTo(fileName));
        }

        public static string NewTempDirectory(string purpose)
        {
            string directory = Path.Combine(Path.GetTempPath(), "bam.automation.testing.tests", purpose, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
