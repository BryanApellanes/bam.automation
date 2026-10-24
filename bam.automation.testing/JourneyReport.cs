using System.Text;
using Newtonsoft.Json;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// Renders a <see cref="JourneyResult"/> as Markdown (a step table with timings, urls and screenshot links,
    /// the failure, and the report-safe notes) and as JSON. <see cref="Follow"/> rewrites the Markdown after every
    /// step so a monitoring owner can watch the file while the browser runs.
    /// </summary>
    public class JourneyReport
    {
        /// <summary>
        /// The Markdown report file name.
        /// </summary>
        public const string MarkdownFileName = "report.md";

        /// <summary>
        /// The JSON result file name.
        /// </summary>
        public const string JsonFileName = "result.json";

        /// <summary>
        /// Creates a report for the result.
        /// </summary>
        /// <param name="result">The result.</param>
        public JourneyReport(JourneyResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// Gets the result being rendered.
        /// </summary>
        public JourneyResult Result { get; }

        /// <summary>
        /// Renders the Markdown report. Screenshot links are relative to the directory when given, else absolute.
        /// </summary>
        /// <param name="relativeTo">The directory links are made relative to, typically the report's own.</param>
        /// <returns>The Markdown.</returns>
        public string ToMarkdown(string? relativeTo = null)
        {
            StringBuilder markdown = new StringBuilder();
            string status = Result.IsRunning ? "RUNNING" : Result.Succeeded ? "PASSED" : "FAILED";
            markdown.AppendLine($"# Journey: {Result.Name}");
            markdown.AppendLine();
            markdown.AppendLine($"**Status:** {status}  ");
            markdown.AppendLine($"**Started:** {Result.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC  ");
            markdown.AppendLine($"**Duration:** {Result.Duration.TotalSeconds:0.0}s  ");
            markdown.AppendLine($"**Steps:** {Result.PassedCount} passed, {Result.FailedCount} failed, {Result.SkippedCount} skipped of {Result.Steps.Count}  ");
            if (!string.IsNullOrEmpty(Result.ArtifactsDirectory))
            {
                markdown.AppendLine($"**Artifacts:** `{Result.ArtifactsDirectory}`  ");
            }

            markdown.AppendLine();
            markdown.AppendLine("| # | Step | Status | Duration | Url | Screenshot |");
            markdown.AppendLine("|---|------|--------|---------:|-----|------------|");
            foreach (JourneyStepResult step in Result.Steps)
            {
                string screenshot = string.IsNullOrEmpty(step.ScreenshotPath) ? string.Empty : $"[{Path.GetFileName(step.ScreenshotPath)}]({LinkTo(step.ScreenshotPath, relativeTo)})";
                string url = string.IsNullOrEmpty(step.Url) ? string.Empty : $"`{EscapeCell(step.Url)}`";
                markdown.AppendLine($"| {step.Number} | {EscapeCell(step.Name)} | {StatusMark(step.Status)} {step.Status} | {step.Duration.TotalSeconds:0.0}s | {url} | {screenshot} |");
            }

            JourneyStepResult? failed = Result.FailedStep;
            if (failed != null)
            {
                markdown.AppendLine();
                markdown.AppendLine("## Failure");
                markdown.AppendLine();
                markdown.AppendLine($"Step {failed.Number} **{failed.Name}** failed" + (string.IsNullOrEmpty(failed.ExceptionType) ? ":" : $" ({failed.ExceptionType}):"));
                markdown.AppendLine();
                markdown.AppendLine("```");
                markdown.AppendLine(failed.Message);
                markdown.AppendLine("```");
            }

            if (Result.Notes.Count > 0)
            {
                markdown.AppendLine();
                markdown.AppendLine("## Notes");
                markdown.AppendLine();
                markdown.AppendLine("| Key | Value |");
                markdown.AppendLine("|-----|-------|");
                foreach (KeyValuePair<string, string> note in Result.Notes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    markdown.AppendLine($"| {EscapeCell(note.Key)} | {EscapeCell(note.Value)} |");
                }
            }

            return markdown.ToString();
        }

        /// <summary>
        /// Renders the result as indented JSON.
        /// </summary>
        /// <returns>The JSON.</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(Result, Formatting.Indented);
        }

        /// <summary>
        /// Writes the Markdown report to the path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The written file.</returns>
        public FileInfo WriteMarkdown(string path)
        {
            FileInfo file = new FileInfo(path);
            file.Directory?.Create();
            File.WriteAllText(file.FullName, ToMarkdown(file.DirectoryName));
            return file;
        }

        /// <summary>
        /// Writes the JSON result to the path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The written file.</returns>
        public FileInfo WriteJson(string path)
        {
            FileInfo file = new FileInfo(path);
            file.Directory?.Create();
            File.WriteAllText(file.FullName, ToJson());
            return file;
        }

        /// <summary>
        /// Writes <see cref="MarkdownFileName"/> and <see cref="JsonFileName"/> for the result into the directory
        /// and records the Markdown path on the result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="directory">The directory; null uses the result's artifacts directory.</param>
        /// <returns>The written file paths.</returns>
        public static JourneyReportFiles Write(JourneyResult result, string? directory = null)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            string target = directory ?? result.ArtifactsDirectory;
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("A directory is required when the result has no artifacts directory", nameof(directory));
            }

            JourneyReport report = new JourneyReport(result);
            FileInfo markdown = report.WriteMarkdown(Path.Combine(target, MarkdownFileName));
            FileInfo json = report.WriteJson(Path.Combine(target, JsonFileName));
            result.ReportPath = markdown.FullName;
            return new JourneyReportFiles(markdown.FullName, json.FullName);
        }

        /// <summary>
        /// Rewrites the Markdown report in the directory after every step and on completion, so the file reflects the
        /// run as it happens. Dispose the returned handle to stop following.
        /// </summary>
        /// <param name="journey">The journey to follow.</param>
        /// <param name="directory">The directory; null uses the journey's artifacts directory at each write.</param>
        /// <returns>A handle that detaches when disposed.</returns>
        public static IDisposable Follow(Journey journey, string? directory = null)
        {
            if (journey == null)
            {
                throw new ArgumentNullException(nameof(journey));
            }

            EventHandler<JourneyStepCompletedEventArgs> handler = (sender, args) =>
            {
                try
                {
                    Write(args.Result, directory ?? journey.ArtifactsDirectory);
                }
                catch
                {
                    // a report write failure never interrupts the run
                }
            };
            journey.StepCompleted += handler;
            journey.Completed += handler;
            return new Follower(journey, handler);
        }

        private static string LinkTo(string path, string? relativeTo)
        {
            string link = path;
            if (!string.IsNullOrEmpty(relativeTo))
            {
                try
                {
                    link = Path.GetRelativePath(relativeTo, path);
                }
                catch
                {
                    link = path;
                }
            }

            return link.Replace('\\', '/');
        }

        private static string StatusMark(JourneyStepStatus status)
        {
            switch (status)
            {
                case JourneyStepStatus.Passed:
                    return "✅";
                case JourneyStepStatus.Failed:
                    return "❌";
                case JourneyStepStatus.Skipped:
                    return "⏭️";
                default:
                    return "⏳";
            }
        }

        private static string EscapeCell(string text)
        {
            return (text ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private sealed class Follower : IDisposable
        {
            private readonly Journey _journey;
            private readonly EventHandler<JourneyStepCompletedEventArgs> _handler;

            public Follower(Journey journey, EventHandler<JourneyStepCompletedEventArgs> handler)
            {
                _journey = journey;
                _handler = handler;
            }

            public void Dispose()
            {
                _journey.StepCompleted -= _handler;
                _journey.Completed -= _handler;
            }
        }
    }
}
