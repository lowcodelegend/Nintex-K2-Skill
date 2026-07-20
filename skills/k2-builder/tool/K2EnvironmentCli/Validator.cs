using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace K2EnvironmentCli
{
    internal static class Validator
    {
        public static ValidationResult Validate(EnvironmentProfile profile, string path)
        {
            var checks = new List<ValidationCheck>();
            Check(checks, "schema", profile.SchemaVersion == 1, "schemaVersion=" + profile.SchemaVersion);
            Check(checks, "name", !string.IsNullOrWhiteSpace(profile.Name), profile.Name ?? "missing");
            Check(checks, "machine", profile.Fingerprint != null && string.Equals(profile.Fingerprint.Machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase),
                profile.Fingerprint == null ? "fingerprint missing" : "profile=" + profile.Fingerprint.Machine + ", actual=" + Environment.MachineName);

            var assembly = profile.K2 == null ? null : Path.Combine(profile.K2.InstallDirectory ?? "", "Bin", "SourceCode.Framework.dll");
            var assemblyExists = !string.IsNullOrWhiteSpace(assembly) && File.Exists(assembly);
            Check(checks, "install", assemblyExists, assemblyExists ? assembly : "K2 framework assembly is missing: " + assembly);
            if (assemblyExists)
            {
                var actual = FileVersionInfo.GetVersionInfo(assembly).FileVersion;
                Check(checks, "version", string.Equals(actual, profile.K2.Version, StringComparison.OrdinalIgnoreCase), "profile=" + profile.K2.Version + ", actual=" + actual);
            }

            if (profile.K2 != null)
                CheckTcp(checks, "management-port", profile.K2.Host, profile.K2.ManagementPort);
            var hasSmartFormsMetadata = profile.SmartForms != null && profile.SmartForms.Themes != null && profile.SmartForms.StyleProfiles != null;
            Check(checks, "smartforms-metadata", hasSmartFormsMetadata,
                hasSmartFormsMetadata ? profile.SmartForms.Themes.Count + " theme(s), " + profile.SmartForms.StyleProfiles.Count + " style profile(s)" : "missing; refresh the environment profile");
            if (hasSmartFormsMetadata && string.Equals(profile.SmartForms.StyleProfileSelection, "selected", StringComparison.OrdinalIgnoreCase))
            {
                var selected = profile.SmartForms.DefaultStyleProfile;
                var exists = selected != null && profile.SmartForms.StyleProfiles.Exists(x => x.Guid == selected.Guid);
                Check(checks, "default-style-profile", exists, exists ? selected.DisplayName + " [" + selected.Name + "]" : "selected style profile is no longer available; refresh and select again");
            }
            if (hasSmartFormsMetadata && string.Equals(profile.SmartForms.CommonHeaderSelection, "selected", StringComparison.OrdinalIgnoreCase))
            {
                var selected = profile.SmartForms.DefaultCommonHeader;
                var exists = selected != null && profile.SmartForms.HeaderViewCandidates != null && profile.SmartForms.HeaderViewCandidates.Exists(x => x.Guid == selected.ViewGuid);
                Check(checks, "default-common-header", exists, exists ? selected.ViewDisplayName + " [" + selected.ViewName + "]" : "selected common header is no longer available; refresh and select again");
            }
            if (profile.Urls != null)
            {
                CheckUrl(checks, "designer-url", profile.Urls.Designer);
                CheckUrl(checks, "runtime-url", profile.Urls.Runtime);
            }
            var valid = checks.TrueForAll(x => x.Status == "ok");
            return new ValidationResult { Name = profile.Name, ProfilePath = path, Valid = valid, ValidatedUtc = DateTime.UtcNow.ToString("o"), Checks = checks };
        }

        private static void Check(List<ValidationCheck> checks, string name, bool success, string message)
        {
            checks.Add(new ValidationCheck { Name = name, Status = success ? "ok" : "failed", Message = message });
        }

        private static void CheckTcp(List<ValidationCheck> checks, string name, string host, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3))) throw new TimeoutException("connection timed out");
                    client.EndConnect(result);
                }
                Check(checks, name, true, host + ":" + port + " reachable");
            }
            catch (Exception ex) { Check(checks, name, false, host + ":" + port + " unavailable (" + ex.Message + ")"); }
        }

        private static void CheckUrl(List<ValidationCheck> checks, string name, string url)
        {
            try
            {
                Uri parsed;
                if (!Uri.TryCreate(url, UriKind.Absolute, out parsed)) throw new Exception("invalid absolute URL");
                var request = (HttpWebRequest)WebRequest.Create(parsed);
                request.Method = "GET"; request.AllowAutoRedirect = false; request.Timeout = 5000;
                request.UseDefaultCredentials = true; request.UserAgent = "k2env/" + Cli.Version;
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                        Check(checks, name, true, url + " returned HTTP " + (int)response.StatusCode);
                }
                catch (WebException ex)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        using (response) Check(checks, name, (int)response.StatusCode < 500, url + " returned HTTP " + (int)response.StatusCode);
                    }
                    else throw;
                }
            }
            catch (Exception ex) { Check(checks, name, false, url + " unavailable (" + ex.Message + ")"); }
        }
    }
}
