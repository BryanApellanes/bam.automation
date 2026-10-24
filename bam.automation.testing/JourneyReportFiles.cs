namespace Bam.Automation.Testing
{
    /// <summary>
    /// The files <see cref="JourneyReport.Write"/> produced.
    /// </summary>
    /// <param name="MarkdownPath">The Markdown report path.</param>
    /// <param name="JsonPath">The JSON result path.</param>
    public sealed record JourneyReportFiles(string MarkdownPath, string JsonPath);
}
