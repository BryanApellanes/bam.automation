namespace Bam.Automation.Testing
{
    /// <summary>
    /// Thrown by <see cref="EnvFileSecrets"/> when a required key is absent or blank. The message names the keys
    /// and the file; it never contains a secret value.
    /// </summary>
    public class MissingSecretException : Exception
    {
        /// <summary>
        /// Creates the exception for the specified missing keys.
        /// </summary>
        /// <param name="source">A description of the secrets source (the file path).</param>
        /// <param name="keys">The keys that were required but absent or blank.</param>
        public MissingSecretException(string source, IEnumerable<string> keys)
            : base(BuildMessage(source, keys))
        {
            Source = source;
            Keys = keys.ToArray();
        }

        /// <summary>
        /// Gets the description of the secrets source (the file path).
        /// </summary>
        public new string Source { get; }

        /// <summary>
        /// Gets the keys that were required but absent or blank.
        /// </summary>
        public string[] Keys { get; }

        private static string BuildMessage(string source, IEnumerable<string> keys)
        {
            string keyList = string.Join(", ", keys);
            return $"Required secret(s) missing or blank in {source}: {keyList}. Fill the value(s) in that file; values are never read from chat.";
        }
    }
}
