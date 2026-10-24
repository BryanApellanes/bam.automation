using System.Text;

namespace Bam.Automation.Testing
{
    /// <summary>
    /// Secrets read from a <c>KEY=VALUE</c> env file — the <c>~/.claude/secrets/&lt;system&gt;.&lt;env&gt;.env</c>
    /// hand-off standard. Comment lines (<c>#</c>), blank lines and a leading <c>export</c> are ignored; surrounding
    /// matching quotes are stripped from values. Values are never written to logs, exceptions, <see cref="ToString"/>
    /// or reports: use <see cref="Masked"/> for anything human-readable and <see cref="Scrub"/> to redact values
    /// that may have leaked into free text.
    /// </summary>
    public sealed class EnvFileSecrets
    {
        private readonly Dictionary<string, string> _values;

        private EnvFileSecrets(string source, Dictionary<string, string> values)
        {
            Source = source;
            _values = values;
        }

        /// <summary>
        /// Loads secrets from the specified file. A leading <c>~</c> is expanded to the user's home directory.
        /// </summary>
        /// <param name="path">The env file path.</param>
        /// <returns>The loaded secrets.</returns>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        public static EnvFileSecrets Load(string path)
        {
            string expanded = ExpandHome(path);
            if (!File.Exists(expanded))
            {
                throw new FileNotFoundException($"Secrets file not found: {expanded}", expanded);
            }

            return Parse(File.ReadAllText(expanded), expanded);
        }

        /// <summary>
        /// Loads secrets from the specified file when it exists; otherwise returns an empty instance whose
        /// <see cref="Exists"/> is false so callers can report the missing file as a required owner input.
        /// </summary>
        /// <param name="path">The env file path.</param>
        /// <returns>The loaded secrets, or an empty instance.</returns>
        public static EnvFileSecrets LoadIfExists(string path)
        {
            string expanded = ExpandHome(path);
            if (!File.Exists(expanded))
            {
                return new EnvFileSecrets(expanded, new Dictionary<string, string>(StringComparer.Ordinal)) { Exists = false };
            }

            return Load(expanded);
        }

        /// <summary>
        /// Parses secrets from env-file content.
        /// </summary>
        /// <param name="content">The <c>KEY=VALUE</c> lines.</param>
        /// <param name="source">A description of where the content came from, used in messages.</param>
        /// <returns>The parsed secrets.</returns>
        public static EnvFileSecrets Parse(string content, string source = "(inline)")
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (content == null)
            {
                return new EnvFileSecrets(source, values);
            }

            string[] lines = content.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.StartsWith("export ", StringComparison.Ordinal))
                {
                    line = line.Substring("export ".Length).TrimStart();
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                values[key] = Unquote(value);
            }

            return new EnvFileSecrets(source, values) { Exists = true };
        }

        /// <summary>
        /// Gets the description of where these secrets came from (the file path).
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets a value indicating whether the source file existed when loaded. Always true for parsed content.
        /// </summary>
        public bool Exists { get; private init; } = true;

        /// <summary>
        /// Gets the keys present in the file, including keys with blank values.
        /// </summary>
        public IReadOnlyCollection<string> Keys => _values.Keys;

        /// <summary>
        /// Gets a value indicating whether the key is present in the file (its value may still be blank).
        /// </summary>
        /// <param name="key">The key.</param>
        public bool Contains(string key)
        {
            return _values.ContainsKey(key);
        }

        /// <summary>
        /// Gets a value indicating whether the key is absent or its value is empty or whitespace.
        /// </summary>
        /// <param name="key">The key.</param>
        public bool IsBlank(string key)
        {
            return !_values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Gets the value for the key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value.</returns>
        /// <exception cref="MissingSecretException">The key is absent or blank.</exception>
        public string Get(string key)
        {
            if (IsBlank(key))
            {
                throw new MissingSecretException(Source, new string[] { key });
            }

            return _values[key];
        }

        /// <summary>
        /// Gets the value for the key when present and non-blank.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value, or an empty string.</param>
        /// <returns>True if a non-blank value was found.</returns>
        public bool TryGet(string key, out string value)
        {
            if (IsBlank(key))
            {
                value = string.Empty;
                return false;
            }

            value = _values[key];
            return true;
        }

        /// <summary>
        /// Gets the value for the key, or null when absent or blank.
        /// </summary>
        /// <param name="key">The key.</param>
        public string? GetOptional(string key)
        {
            return TryGet(key, out string value) ? value : null;
        }

        /// <summary>
        /// Throws <see cref="MissingSecretException"/> naming every specified key that is absent or blank.
        /// </summary>
        /// <param name="keys">The keys that must have values.</param>
        public void Require(params string[] keys)
        {
            List<string> missing = keys.Where(IsBlank).ToList();
            if (missing.Count > 0)
            {
                throw new MissingSecretException(Source, missing);
            }
        }

        /// <summary>
        /// Gets a report-safe description of a value: its length and a short prefix, for example
        /// <c>len=16 prefix=4242</c>, or <c>(blank)</c> when absent or blank. Never the value itself.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="prefixLength">How many leading characters to reveal; defaults to 4. Nothing is revealed when the
        /// value is no longer than twice this, so short values such as a CVC or an expiry never appear.</param>
        public string Masked(string key, int prefixLength = 4)
        {
            if (!TryGet(key, out string value))
            {
                return "(blank)";
            }

            int prefixCap = Math.Max(0, prefixLength);
            int reveal = value.Length > prefixCap * 2 ? prefixCap : 0;
            string prefix = reveal > 0 ? value.Substring(0, reveal) : string.Empty;
            return $"len={value.Length} prefix={prefix}";
        }

        /// <summary>
        /// Replaces every occurrence of a non-blank secret value (three characters or longer) in the text with
        /// <c>[REDACTED:KEY]</c>. Use on exception messages and anything else that may have echoed a typed value.
        /// </summary>
        /// <param name="text">The text to scrub.</param>
        /// <returns>The scrubbed text; null and empty pass through unchanged.</returns>
        public string Scrub(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            string scrubbed = text;
            foreach (KeyValuePair<string, string> pair in _values.OrderByDescending(pair => pair.Value.Length))
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 3)
                {
                    continue;
                }

                scrubbed = scrubbed.Replace(pair.Value, $"[REDACTED:{pair.Key}]", StringComparison.Ordinal);
            }

            return scrubbed;
        }

        /// <summary>
        /// Describes the source and key count only; never values.
        /// </summary>
        public override string ToString()
        {
            return $"{nameof(EnvFileSecrets)}({Source}, {_values.Count} keys)";
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2)
            {
                char first = value[0];
                char last = value[value.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    return value.Substring(1, value.Length - 2);
                }
            }

            return value;
        }

        private static string ExpandHome(string path)
        {
            if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            {
                return Path.Combine(BamProfile.UserHome, path.Substring(2));
            }

            return path;
        }
    }
}
