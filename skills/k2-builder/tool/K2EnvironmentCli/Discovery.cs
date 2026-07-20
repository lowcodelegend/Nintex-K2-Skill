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
using System.Text.RegularExpressions;
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
            List<ObservedSolutionCode> observedSolutionCodes;
            var smartForms = DiscoverSmartForms(host, sources, out observedSolutionCodes);

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
                SolutionCodes = new List<SolutionCodeRegistration>(),
                ObservedSolutionCodes = observedSolutionCodes,
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

        private static SmartFormsSettings DiscoverSmartForms(string host, List<string> sources, out List<ObservedSolutionCode> observedSolutionCodes)
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
                    var views = manager.GetViews().Views.Cast<ViewInfo>().ToList();
                    var forms = manager.GetForms().Forms.Cast<FormInfo>().ToList();
                    var headers = views
                        .Where(IsHeaderCandidate)
                        .OrderBy(x => x.IsSystem).ThenBy(x => x.CategoryPath).ThenBy(x => x.DisplayName)
                        .Select(x => ReadHeader(manager, x, false)).ToList();
                    observedSolutionCodes = ObserveSolutionCodes(forms, views);
                    sources.Add("K2 FormsManager themes, style profiles, common framework-view candidates, and solution-code candidates");
                    return new SmartFormsSettings
                    {
                        Themes = themes,
                        StyleProfiles = profiles,
                        StyleProfileSelection = profiles.Count == 0 ? "none" : "unselected",
                        DefaultStyleProfile = null,
                        HeaderViewCandidates = headers,
                        CommonHeaderSelection = headers.Count == 0 ? "none" : "unselected",
                        DefaultCommonHeader = null
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

        private static List<ObservedSolutionCode> ObserveSolutionCodes(IEnumerable<FormInfo> forms, IEnumerable<ViewInfo> views)
        {
            var artifacts = forms.Select(x => new { Kind = "Form", x.Name, x.DisplayName, x.CategoryPath })
                .Concat(views.Select(x => new { Kind = "View", x.Name, x.DisplayName, x.CategoryPath }));
            var observations = new List<KeyValuePair<string, string>>();
            foreach (var artifact in artifacts)
            {
                var code = ExtractSolutionCode(artifact.Name) ?? ExtractSolutionCode(artifact.DisplayName) ?? ExtractCategoryCode(artifact.CategoryPath);
                if (code == null) continue;
                observations.Add(new KeyValuePair<string, string>(code, artifact.Kind + ": " + (artifact.DisplayName ?? artifact.Name) + " @ " + (artifact.CategoryPath ?? "(no category)")));
            }
            return observations.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key)
                .Select(x => new ObservedSolutionCode
                {
                    Code = x.Key.ToUpperInvariant(),
                    ArtifactCount = x.Count(),
                    Samples = x.Select(y => y.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList()
                }).ToList();
        }

        private static string ExtractSolutionCode(string value)
        {
            var match = Regex.Match(value ?? string.Empty, @"^([A-Z]{3,4})\.", RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ExtractCategoryCode(string path)
        {
            foreach (var segment in (path ?? string.Empty).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var code = ExtractSolutionCode(segment);
                if (code != null) return code;
            }
            return null;
        }

        public static List<HeaderViewCandidate> InspectHeaders(K2Settings settings, string hint)
        {
            if (settings == null) throw new CliException("Profile has no K2 connection settings.");
            hint = (hint ?? string.Empty).Trim();
            if (hint.Length == 0) throw new CliException("--hint must contain part of the header view name, display name, category path, or its GUID.");
            using (var manager = OpenFormsManager(settings))
            {
                Guid guid;
                var views = manager.GetViews().Views.Cast<ViewInfo>().Where(x =>
                    (Guid.TryParse(hint, out guid) && x.Guid == guid) ||
                    Contains(x.Name, hint) || Contains(x.DisplayName, hint) || Contains(x.CategoryPath, hint)).ToList();
                return views.OrderBy(x => x.IsSystem).ThenBy(x => x.CategoryPath).ThenBy(x => x.DisplayName)
                    .Select(x => ReadHeader(manager, x, true)).ToList();
            }
        }

        private static FormsManager OpenFormsManager(K2Settings settings)
        {
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true,
                Host = settings.Host,
                Port = (uint)settings.ManagementPort,
                Integrated = settings.IntegratedAuthentication,
                IsPrimaryLogin = true,
                SecurityLabelName = settings.SecurityLabel
            };
            var manager = new FormsManager();
            manager.CreateConnection();
            try { manager.Connection.Open(builder.ConnectionString); return manager; }
            catch
            {
                manager.DeleteConnection();
                manager.Dispose();
                throw;
            }
        }

        private static bool IsHeaderCandidate(ViewInfo view)
        {
            return Contains(view.Name, "header") || Contains(view.DisplayName, "header") ||
                   Contains(view.Name, "footer") || Contains(view.DisplayName, "footer") ||
                   Contains(view.Name, "banner") || Contains(view.DisplayName, "banner") ||
                   Contains(view.Name, "masthead") || Contains(view.DisplayName, "masthead");
        }

        private static bool Contains(string value, string part)
        {
            return !string.IsNullOrWhiteSpace(value) && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static HeaderViewCandidate ReadHeader(FormsManager manager, ViewInfo view, bool detailed)
        {
            var definition = XDocument.Parse(manager.GetViewDefinition(view.Guid));
            var events = definition.Descendants().Where(x => x.Name.LocalName == "Event" &&
                string.Equals((string)x.Attribute("SourceType"), "View", StringComparison.OrdinalIgnoreCase))
                .Select(x => new HeaderEventSettings
                {
                    Name = (string)x.Element(x.Name.Namespace + "Name") ?? (string)x.Attribute("SourceName"),
                    DefinitionId = ParseGuid((string)x.Attribute("DefinitionID")),
                    Type = (string)x.Attribute("Type"),
                    HandlerCount = x.Descendants().Count(y => y.Name.LocalName == "Handler"),
                    ActionCount = x.Descendants().Count(y => y.Name.LocalName == "Action")
                }).ToList();
            var consumers = detailed ? manager.GetFormsForView(view.Guid).Forms.Cast<FormInfo>()
                .OrderBy(x => x.CategoryPath).ThenBy(x => x.DisplayName)
                .Take(25).Select(x => ReadConsumer(manager, x, view.Guid)).ToList() : new List<HeaderConsumerSettings>();
            return new HeaderViewCandidate
            {
                Guid = view.Guid,
                Name = view.Name,
                DisplayName = view.DisplayName,
                CategoryPath = view.CategoryPath,
                ViewType = view.Type.ToString(),
                Version = view.Version,
                IsSystem = view.IsSystem,
                IsInternal = view.IsInternal,
                Parameters = view.Parameters.Cast<ViewParameter>().Select(x => new HeaderParameterSettings
                {
                    Guid = x.Guid, Name = x.Name, DisplayName = x.DisplayName,
                    DataType = x.DataType.ToString(), DefaultValue = x.DefaultValue
                }).ToList(),
                Events = events,
                Controls = definition.Descendants().Where(x => x.Name.LocalName == "Control" && x.Attribute("ID") != null)
                    .GroupBy(x => (string)x.Attribute("ID"), StringComparer.OrdinalIgnoreCase).Select(x => x.First())
                    .Select(x => new HeaderControlSettings
                    {
                        Guid = ParseGuid((string)x.Attribute("ID")),
                        Name = (string)x.Element(x.Name.Namespace + "Name"),
                        DisplayName = (string)x.Element(x.Name.Namespace + "DisplayName"),
                        Type = (string)x.Attribute("Type")
                    }).Where(x => x.Guid != Guid.Empty && !string.IsNullOrWhiteSpace(x.Name)).ToList(),
                ConsumerFormCount = manager.GetFormsForView(view.Guid).Forms.Count,
                Consumers = consumers
            };
        }

        private static HeaderConsumerSettings ReadConsumer(FormsManager manager, FormInfo info, Guid viewGuid)
        {
            var document = XDocument.Parse(manager.GetFormDefinition(info.Guid));
            var item = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Item" &&
                string.Equals((string)x.Attribute("ViewID"), viewGuid.ToString(), StringComparison.OrdinalIgnoreCase));
            var instanceId = item == null ? null : (string)item.Attribute("ID");
            var bindings = new List<HeaderParameterBindingSettings>();
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                var actions = document.Descendants().Where(x => x.Name.LocalName == "Action" &&
                    string.Equals((string)x.Attribute("Type"), "Execute", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((string)x.Attribute("InstanceID"), instanceId, StringComparison.OrdinalIgnoreCase));
                foreach (var parameter in actions.SelectMany(x => x.Descendants().Where(y => y.Name.LocalName == "Parameter" &&
                    string.Equals((string)y.Attribute("TargetType"), "ViewParameter", StringComparison.OrdinalIgnoreCase))))
                {
                    var sourceValue = parameter.Elements().FirstOrDefault(x => x.Name.LocalName == "SourceValue");
                    bindings.Add(new HeaderParameterBindingSettings
                    {
                        TargetParameter = (string)parameter.Attribute("TargetName") ?? (string)parameter.Attribute("TargetID"),
                        SourceType = (string)parameter.Attribute("SourceType"),
                        SourceName = (string)parameter.Attribute("SourceName"),
                        Value = sourceValue == null ? null : sourceValue.Value
                    });
                }
            }
            return new HeaderConsumerSettings
            {
                FormGuid = info.Guid, FormName = info.Name, FormDisplayName = info.DisplayName,
                CategoryPath = info.CategoryPath, InstanceId = instanceId,
                InitializeBindings = bindings.GroupBy(x => x.TargetParameter, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList()
            };
        }

        private static Guid ParseGuid(string value)
        {
            Guid result;
            return Guid.TryParse(value, out result) ? result : Guid.Empty;
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
