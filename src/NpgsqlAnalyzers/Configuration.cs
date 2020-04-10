using System;

namespace NpgsqlAnalyzers
{
    internal static class Configuration
    {
        public const string ConnectionStringEnvVar = "NPGSQLA_CONNECTION_STRING";

        public static string ConnectionString { get; } = Environment.GetEnvironmentVariable(
            variable: ConnectionStringEnvVar,
            target: EnvironmentVariableTarget.User);
    }
}
