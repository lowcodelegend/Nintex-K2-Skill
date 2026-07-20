using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using SourceCode.Forms.Management;
using SourceCode.Hosting.Client.BaseAPI;

namespace K2EnvironmentCli
{
    internal static class Discovery
    {
        public static EnvironmentProfile Discover(string name, string installOverride, string hostOverride, string baseUrlOverride)
        {
            var sources = new List<string>();
            var install = ResolveInstallDirectory(installOverride, sources);
            var versionFile = Path.Combine(install, "Bin", "SourceCode.Framework.dll");
            if (!File.Exists(versionFile)) throw new CliException("K2 installation is missing Bin\\SourceCode.Framework.dll: " + install);
            var version = FileVersionInfo.GetVersionInfo(versionFile).FileVersion;
            sources.Add("K2 assembly file version");

            IisDiscovery iis = null;
            try { iis = DiscoverIis(install); if (iis != null) sources.Add("IIS applicationHost.config"); }
            catch (Exception ex) { sources.Add("IIS discovery unavailable: " + ex.Message); }

            var machine = Environment.MachineName;
            var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            var host = string.IsNullOrWhiteSpace(hostOverride) ? "localhost" : hostOverride.Trim();
            var webBase = !string.IsNullOrWhiteSpace(baseUrlOverride) ? NormalizeBaseUrl(baseUrlOverride) :
                iis != null ? iis.BaseUrl : "http://" + (string.IsNullOrWhiteSpace(domain) ? machine : machine + "." + domain);
            var smartForms = DiscoverSmartForms(host, sources);

            return new EnvironmentProfile
            {
                SchemaVersion = 1,
                Name = ProfileStore.ValidateName(name),
                K2 = new K2Settings
                {
                    Host = host, ManagementPort = 5555, WorkflowPort = 5252, DesignerHost = "smartforms",
                    SecurityLabel = "K2", IntegratedAuthentication = true, InstallDirectory = install, Version = version
                },
                Urls = new UrlSettings
                {
                    Base = webBase,
                    Designer = CombineUrl(webBase, iis == null ? "/Designer" : iis.DesignerPath),
                    Runtime = CombineUrl(webBase, iis == null ? "/Runtime" : iis.RuntimePath),
                    Management = CombineUrl(webBase, iis == null ? "/Management" : iis.ManagementPath)
                },
                SmartForms = smartForms,
                Fingerprint = new EnvironmentFingerprint
                {
                    Machine = machine, Domain = domain, K2InstallId = Hash(machine + "|" + install.ToUpperInvariant() + "|" + version)
                },
                Discovery = new DiscoveryMetadata
                {
                    ToolVersion = Cli.Version, WindowsIdentity = WindowsIdentity.GetCurrent().Name,
                    IisSite = iis == null ? null : iis.SiteName, Sources = sources
                },
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                LastValidatedUtc = null
            };
        }

        private static SmartFormsSettings DiscoverSmartForms(string host, List<string> sources)
        {
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true,
                Host = host,
                Port = 5555,
                Integrated = true,
                IsPrimaryLogin = true,
                SecurityLabelName = "K2"
            };
            using (var manager = new FormsManager())
            {
                manager.CreateConnection();
                try
                {
                    manager.Connection.Open(builder.ConnectionString);
                    var themes = manager.GetThemes().Themes.Cast<Theme>().Select(x => x.Name).OrderBy(x => x).ToList();
                    var profiles = manager.GetStyleProfiles().StyleProfiles.Cast<StyleProfileInfo>()
                        .OrderBy(x => x.DisplayName).ThenBy(x => x.Name)
                        .Select(x => new StyleProfileSettings
                        {
                            Guid = x.Guid,
                            Name = x.Name,
                            DisplayName = x.DisplayName,
                            CategoryPath = x.CategoryPath,
                            IsSystem = x.IsSystem,
                            IsInternal = x.IsInternal,
                            Version = x.Version
                        }).ToList();
                    sources.Add("K2 FormsManager themes and style profiles");
                    return new SmartFormsSettings
                    {
                        Themes = themes,
                        StyleProfiles = profiles,
                        StyleProfileSelection = profiles.Count == 0 ? "none" : "unselected",
                        DefaultStyleProfile = null
                    };
                }
                catch (Exception ex)
                {
                    throw new CliException("K2 SmartForms metadata discovery failed: " + ex.GetBaseException().Message);
                }
                finally
                {
                    if (manager.Connection != null)
                    {
                        manager.Connection.Close();
                        manager.DeleteConnection();
                    }
                }
            }
        }

        private static string ResolveInstallDirectory(string value, List<string> sources)
        {
            if (!string.IsNullOrWhiteSpace(value)) { sources.Add("--install-dir"); return FullExistingDirectory(value); }
            value = Environment.GetEnvironmentVariable("K2_INSTALL_DIR");
            if (!string.IsNullOrWhiteSpace(value)) { sources.Add("K2_INSTALL_DIR"); return FullExistingDirectory(value); }
            foreach (var keyName in new[] { @"SOFTWARE\SourceCode\blackpearl\blackpearl Core", @"SOFTWARE\WOW6432Node\SourceCode\blackpearl\blackpearl Core" })
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyName))
                {
                    value = key == null ? null : Convert.ToString(key.GetValue("InstallDir"));
                    if (!string.IsNullOrWhiteSpace(value)) { sources.Add("HKLM\\" + keyName); return FullExistingDirectory(value); }
                }
            }
            var fallback = @"C:\Program Files\K2";
            if (Directory.Exists(fallback)) { sources.Add("default installation path"); return Path.GetFullPath(fallback); }
            throw new CliException("K2 installation was not found. Supply --install-dir or set K2_INSTALL_DIR.");
        }

        private static string FullExistingDirectory(string value)
        {
            var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim())).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(path)) throw new CliException("K2 installation directory does not exist: " + path);
            return path;
        }

        private static IisDiscovery DiscoverIis(string install)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\inetsrv\config\applicationHost.config");
            if (!File.Exists(path)) return null;
            var doc = XDocument.Load(path);
            var sites = doc.Descendants("site");
            foreach (var site in sites)
            {
                var apps = site.Elements("application").ToList();
                string runtime = FindApplicationPath(apps, Path.Combine(install, "K2 smartforms Runtime"));
                string designer = FindApplicationPath(apps, Path.Combine(install, "K2 smartforms Designer"));
                if (runtime == null || designer == null) continue;
                var binding = site.Element("bindings").Elements("binding")
                    .Where(x => IsWebProtocol((string)x.Attribute("protocol")))
                    .OrderByDescending(x => string.Equals((string)x.Attribute("protocol"), "https", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(x => BindingHost((string)x.Attribute("bindingInformation")).Length)
                    .FirstOrDefault();
                if (binding == null) continue;
                var protocol = (string)binding.Attribute("protocol");
                var info = (string)binding.Attribute("bindingInformation") ?? "";
                var host = BindingHost(info);
                if (string.IsNullOrWhiteSpace(host)) host = Environment.MachineName;
                var port = BindingPort(info);
                var defaultPort = protocol == "https" ? 443 : 80;
                var baseUrl = protocol + "://" + host + (port > 0 && port != defaultPort ? ":" + port : "");
                return new IisDiscovery
                {
                    SiteName = (string)site.Attribute("name"), BaseUrl = baseUrl,
                    RuntimePath = runtime, DesignerPath = designer,
                    ManagementPath = apps.Any(x => string.Equals((string)x.Attribute("path"), "/Management", StringComparison.OrdinalIgnoreCase)) ? "/Management" : runtime
                };
            }
            return null;
        }

        private static string FindApplicationPath(IEnumerable<XElement> apps, string physical)
        {
            foreach (var app in apps)
            foreach (var directory in app.Elements("virtualDirectory"))
            {
                var candidate = Environment.ExpandEnvironmentVariables((string)directory.Attribute("physicalPath") ?? "").TrimEnd('\\');
                if (string.Equals(candidate, physical.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return (string)app.Attribute("path");
            }
            return null;
        }

        private static bool IsWebProtocol(string value) { return value == "http" || value == "https"; }
        private static string BindingHost(string value) { var p = (value ?? "").Split(':'); return p.Length >= 3 ? p[2] : ""; }
        private static int BindingPort(string value) { var p = (value ?? "").Split(':'); int n; return p.Length >= 2 && int.TryParse(p[1], out n) ? n : 0; }
        private static string NormalizeBaseUrl(string value)
        {
            Uri uri; if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new CliException("--base-url must be an absolute HTTP or HTTPS URL.");
            return value.TrimEnd('/');
        }
        private static string CombineUrl(string root, string path) { return root.TrimEnd('/') + "/" + (path ?? "").Trim('/'); }
        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2")));
        }

        private sealed class IisDiscovery
        {
            public string SiteName, BaseUrl, RuntimePath, DesignerPath, ManagementPath;
        }
    }
}
