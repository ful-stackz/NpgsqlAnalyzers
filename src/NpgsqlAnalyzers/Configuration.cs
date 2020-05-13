using System;
using System.Collections.Generic;
using System.Linq;

namespace NpgsqlAnalyzers
{
    public class Configuration
    {
        private const string ConfigAssignChar = "=";
        private const string ConfigCommentChar = "#";
        private const string ConfigConnectionStringKey = "CONNECTION_STRING";

        public Configuration(IDictionary<string, string> args)
        {
            ConnectionString = args.ContainsKey(ConfigConnectionStringKey)
                ? args[ConfigConnectionStringKey]
                : throw new InvalidOperationException($"Missing key '{ConfigConnectionStringKey}' from configuration.");
        }

        public string ConnectionString { get; }

        public static Configuration FromFile(IEnumerable<string> lines)
        {
            var args = lines
                .Select((line) => line.ToString())
                .Where((line) => !string.IsNullOrWhiteSpace(line))
                .Where((line) => !line.StartsWith(ConfigCommentChar))
                .Where((line) => line.Contains(ConfigAssignChar))
                .ToDictionary(
                    keySelector: (line) => line.Split(ConfigAssignChar[0])[0].Trim(),
                    elementSelector: (line) => line.Split(ConfigAssignChar.ToArray(), 2)[1].Trim());

            return new Configuration(args);
        }
    }
}
