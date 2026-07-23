using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace K2StyleProfilesCli
{
    public sealed class StyleProfileManifest
    {
        public int SchemaVersion { get; set; }
        public string Name { get; set; }
        public K2ConnectionOptions K2 { get; set; }
        public StyleProfileOptions StyleProfile { get; set; }
        public HostingOptions Hosting { get; set; }
        public VerificationOptions Verification { get; set; }

        [ScriptIgnore]
        public string ManifestPath { get; private set; }

        [ScriptIgnore]
        public string ManifestDirectory { get { return Path.GetDirectoryName(ManifestPath); } }

        public StyleProfileManifest()
        {
            SchemaVersion = 1;
            K2 = new K2ConnectionOptions();
            StyleProfile = new StyleProfileOptions();
            Hosting = new HostingOptions();
            Verification = new VerificationOptions();
        }

        public static StyleProfileManifest Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new CliException("Specify --manifest <path>.");
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new CliException("Manifest not found: " + fullPath);
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var manifest = serializer.Deserialize<StyleProfileManifest>(File.ReadAllText(fullPath));
                if (manifest == null) throw new CliException("Manifest is empty.");
                manifest.ManifestPath = fullPath;
                manifest.NormalizeAndValidate();
                return manifest;
            }
            catch (CliException) { throw; }
            catch (Exception ex) { throw new CliException("Invalid manifest JSON: " + ex.Message); }
        }

        internal void NormalizeAndValidate()
        {
            if (K2 == null) K2 = new K2ConnectionOptions();
            if (StyleProfile == null) StyleProfile = new StyleProfileOptions();
            if (Hosting == null) Hosting = new HostingOptions();
            if (Verification == null) Verification = new VerificationOptions();
            if (StyleProfile.Files == null) StyleProfile.Files = new List<StyleFileOptions>();
            if (Hosting.AdditionalFiles == null) Hosting.AdditionalFiles = new List<StyleFileOptions>();

            if (SchemaVersion != 1) throw new CliException("schemaVersion must be 1.");
            Require(Name, "name");
            Require(K2.Host, "k2.host");
            if (K2.Port < 1 || K2.Port > 65535) throw new CliException("k2.port must be between 1 and 65535.");
            if (!K2.Integrated)
            {
                Require(K2.UserName, "k2.userName");
                Require(K2.PasswordEnvironmentVariable, "k2.passwordEnvironmentVariable");
            }
            Require(StyleProfile.SystemName, "styleProfile.systemName");
            Require(StyleProfile.DisplayName, "styleProfile.displayName");
            Require(StyleProfile.CategoryPath, "styleProfile.categoryPath");
            if (StyleProfile.SystemName.Length > 255 || StyleProfile.DisplayName.Length > 255)
                throw new CliException("Style Profile names must not exceed 255 characters.");
            if (StyleProfile.Files.Count == 0) throw new CliException("styleProfile.files must contain at least one CSS or JS file.");
            if (StyleProfile.Files.Any(x => x == null)) throw new CliException("styleProfile.files cannot contain null entries.");
            if (Hosting.AdditionalFiles.Any(x => x == null)) throw new CliException("hosting.additionalFiles cannot contain null entries.");

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in GetHostedAssets())
            {
                file.Type = (file.Type ?? string.Empty).Trim().ToLowerInvariant();
                if (file.Type != "css" && file.Type != "js")
                    throw new CliException("Hosted file type must be 'css' or 'js'.");
                if (string.IsNullOrWhiteSpace(file.Source) && string.IsNullOrWhiteSpace(file.Url))
                    throw new CliException("Each hosted file requires source or url.");
                if (!string.IsNullOrWhiteSpace(file.Source) && string.IsNullOrWhiteSpace(file.Target))
                    file.Target = Path.GetFileName(file.Source);
                if (!string.IsNullOrWhiteSpace(file.Target))
                {
                    if (Path.IsPathRooted(file.Target) || file.Target.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                        file.Target.IndexOf('/') >= 0 || file.Target.IndexOf('\\') >= 0)
                        throw new CliException("Hosted file target must be a file name, not a path: " + file.Target);
                    if (!targets.Add(file.Target)) throw new CliException("Duplicate hosted target across Style Profile and additional files: " + file.Target);
                }
                if (string.IsNullOrWhiteSpace(file.Url) && !Hosting.Enabled)
                    throw new CliException("A file url is required when hosting.enabled is false.");
            }

            Guid preview;
            if (!string.IsNullOrWhiteSpace(StyleProfile.PreviewFormId) && !Guid.TryParse(StyleProfile.PreviewFormId, out preview))
                throw new CliException("styleProfile.previewFormId must be a GUID.");

            if (Hosting.Enabled)
            {
                Require(Hosting.SiteName, "hosting.siteName");
                Require(Hosting.ApplicationPath, "hosting.applicationPath");
                Require(Hosting.VirtualPath, "hosting.virtualPath");
                Require(Hosting.PhysicalPath, "hosting.physicalPath");
                Require(Hosting.BaseUrl, "hosting.baseUrl");
                if (!Hosting.ApplicationPath.StartsWith(Hosting.SiteName + "/", StringComparison.OrdinalIgnoreCase))
                    throw new CliException("hosting.applicationPath must begin with hosting.siteName + '/'.");
                if (!Path.IsPathRooted(Environment.ExpandEnvironmentVariables(Hosting.PhysicalPath)))
                    throw new CliException("hosting.physicalPath must be absolute.");
                if (!Hosting.VirtualPath.StartsWith("/", StringComparison.Ordinal))
                    throw new CliException("hosting.virtualPath must start with '/'.");
                if (!Regex.IsMatch(Hosting.SiteName, @"^[\w .-]+$") ||
                    !Regex.IsMatch(Hosting.ApplicationPath, @"^[\w ./-]+$") ||
                    !Regex.IsMatch(Hosting.VirtualPath, @"^/[\w./-]+$"))
                    throw new CliException("IIS hosting names contain unsupported characters.");
                Uri baseUri;
                if (!Uri.TryCreate(Hosting.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out baseUri) ||
                    (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
                    throw new CliException("hosting.baseUrl must be an absolute HTTP(S) URL.");
                if (Verification.RequireHttps && baseUri.Scheme != Uri.UriSchemeHttps)
                    throw new CliException("verification.requireHttps is true but hosting.baseUrl is not HTTPS.");
                foreach (var file in GetHostedAssets().Where(x => string.IsNullOrWhiteSpace(x.Url)))
                {
                    if (string.IsNullOrWhiteSpace(file.Source) || string.IsNullOrWhiteSpace(file.Target))
                        throw new CliException("Hosted files require source and target.");
                }
            }
            if (Verification.HttpTimeoutSeconds < 1 || Verification.HttpTimeoutSeconds > 300)
                throw new CliException("verification.httpTimeoutSeconds must be between 1 and 300.");
        }

        public string ResolveSource(StyleFileOptions file)
        {
            if (string.IsNullOrWhiteSpace(file.Source)) return null;
            return Path.GetFullPath(Path.Combine(ManifestDirectory, file.Source));
        }

        public string ResolveUrl(StyleFileOptions file)
        {
            if (!string.IsNullOrWhiteSpace(file.Url)) return file.Url;
            return Hosting.BaseUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(file.Target);
        }

        public IEnumerable<StyleFileOptions> GetHostedAssets()
        {
            return StyleProfile.Files.Concat(Hosting.AdditionalFiles ?? new List<StyleFileOptions>());
        }

        private static void Require(string value, string path)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new CliException(path + " is required.");
        }
    }

    public sealed class K2ConnectionOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool Integrated { get; set; }
        public string SecurityLabel { get; set; }
        public string Domain { get; set; }
        public string UserName { get; set; }
        public string PasswordEnvironmentVariable { get; set; }

        public K2ConnectionOptions()
        {
            Host = "localhost";
            Port = 5555;
            Integrated = true;
            SecurityLabel = "K2";
        }
    }

    public sealed class StyleProfileOptions
    {
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string CategoryPath { get; set; }
        public string PreviewFormId { get; set; }
        public bool ReplaceExisting { get; set; }
        public List<StyleFileOptions> Files { get; set; }

        public StyleProfileOptions() { Files = new List<StyleFileOptions>(); }
    }

    public sealed class StyleFileOptions
    {
        public string Type { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public string Url { get; set; }
    }

    public sealed class HostingOptions
    {
        public bool Enabled { get; set; }
        public bool ConfigureIis { get; set; }
        public string SiteName { get; set; }
        public string ApplicationPath { get; set; }
        public string VirtualPath { get; set; }
        public string PhysicalPath { get; set; }
        public string BaseUrl { get; set; }
        public List<StyleFileOptions> AdditionalFiles { get; set; }

        public HostingOptions()
        {
            SiteName = "K2";
            ApplicationPath = "K2/";
            ConfigureIis = true;
            AdditionalFiles = new List<StyleFileOptions>();
        }
    }

    public sealed class VerificationOptions
    {
        public bool RequireHttps { get; set; }
        public bool RequireDesignerIsolation { get; set; }
        public bool VerifyHttp { get; set; }
        public int HttpTimeoutSeconds { get; set; }

        public VerificationOptions()
        {
            RequireHttps = true;
            RequireDesignerIsolation = true;
            VerifyHttp = true;
            HttpTimeoutSeconds = 20;
        }
    }
}
