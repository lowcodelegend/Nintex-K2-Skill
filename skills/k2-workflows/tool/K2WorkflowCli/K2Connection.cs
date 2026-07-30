using System;
using System.Security.Principal;
using SourceCode.Hosting.Client.BaseAPI;

namespace K2WorkflowCli
{
    internal static class K2Connection
    {
        public static string BuildManagementConnectionString(K2Settings settings)
        {
            return BuildConnectionString(settings, settings.Port);
        }

        public static string BuildWorkflowConnectionString(K2Settings settings)
        {
            return BuildConnectionString(settings, settings.WorkflowPort);
        }

        public static string BuildConnectionString(K2Settings settings, int port)
        {
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true,
                Host = settings.Host,
                Port = (uint)port,
                Integrated = settings.Integrated,
                IsPrimaryLogin = true,
                SecurityLabelName = settings.SecurityLabel
            };
            if (!settings.Integrated)
            {
                builder.WindowsDomain = settings.Domain ?? string.Empty;
                builder.UserID = settings.UserName;
                builder.Password = ReadRequiredEnvironmentVariable(
                    settings.PasswordEnvironmentVariable);
                builder.CachePassword = false;
            }
            return builder.ConnectionString;
        }

        public static string DescribeIdentity(K2Settings settings)
        {
            var label = settings.SecurityLabel ?? string.Empty;
            string account;
            if (settings.Integrated)
            {
                account = WindowsIdentity.GetCurrent().Name ?? string.Empty;
            }
            else
            {
                account = settings.UserName ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(settings.Domain)
                    && account.IndexOf("\\", StringComparison.Ordinal) < 0)
                {
                    account = settings.Domain + "\\" + account;
                }
            }
            var prefix = label + ":";
            return account.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? account
                : prefix + account;
        }

        public static string ReadRequiredEnvironmentVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new CliException(
                    "k2.passwordEnvironmentVariable is required for non-integrated authentication.");
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new CliException(
                    "Required environment variable is not set: " + name);
            return value;
        }

        public static void AssertAuthenticated(
            BaseAPIConnection connection,
            K2Settings settings,
            string operation)
        {
            if (connection == null
                || !connection.IsConnected
                || !connection.IsAuthenticated)
            {
                throw new CliException(
                    operation + " connection is not authenticated.");
            }
            if (connection.Integrated != settings.Integrated)
            {
                throw new CliException(
                    operation + " authentication mode does not match the manifest.");
            }
            if (!string.Equals(
                connection.SecurityLabelName,
                settings.SecurityLabel,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new CliException(
                    operation + " security label does not match the manifest.");
            }
        }

        public static bool SameIdentity(string first, string second)
        {
            return string.Equals(
                NormalizeIdentity(first),
                NormalizeIdentity(second),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeIdentity(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            var separator = normalized.IndexOf(':');
            if (separator >= 0 && separator < normalized.Length - 1)
                normalized = normalized.Substring(separator + 1);
            return normalized.Replace("K2\\", string.Empty);
        }
    }
}
