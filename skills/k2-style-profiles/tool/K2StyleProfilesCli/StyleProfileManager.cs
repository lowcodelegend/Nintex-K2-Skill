using System;
using System.Collections.Generic;
using System.Linq;
using SourceCode.Forms.Authoring;
using SourceCode.Forms.Management;
using SourceCode.Hosting.Client.BaseAPI;

namespace K2StyleProfilesCli
{
    internal sealed class StyleProfileManager
    {
        private readonly StyleProfileManifest _manifest;
        private readonly AssetHost _assets;
        private readonly DesignerStyleProfileGateway _designer;

        public StyleProfileManager(StyleProfileManifest manifest)
        {
            _manifest = manifest;
            _assets = new AssetHost(manifest);
            _designer = new DesignerStyleProfileGateway();
        }

        public void Doctor()
        {
            _assets.CheckInputs();
            WithFormsManager(manager =>
            {
                manager.GetStyleProfiles();
                return 0;
            });
            Console.WriteLine("K2 management connection: OK");
            Console.WriteLine("K2 Designer Style Profile contract: OK");
        }

        public ArtifactState GetState()
        {
            return WithFormsManager(manager =>
            {
                var info = Resolve(manager);
                if (info == null) return new ArtifactState { Exists = false };
                return State(info);
            });
        }

        public void Deploy()
        {
            _assets.Deploy();
            var savedGuid = WithFormsManager(manager =>
            {
                var existing = Resolve(manager);
                if (existing != null && !_manifest.StyleProfile.ReplaceExisting)
                    throw new CliException("Style Profile already exists and styleProfile.replaceExisting is false: " + existing.DisplayName + " (" + existing.Guid + ")");
                if (existing != null && (existing.IsSystem || existing.IsInternal))
                    throw new CliException("Refusing to replace a system/internal Style Profile: " + existing.DisplayName);

                var checkedOutHere = false;
                try
                {
                    string currentJson = null;
                    if (existing != null)
                    {
                        currentJson = _designer.Load(existing.Guid);
                        if (!existing.IsCheckedOut && DefinitionMatches(existing, currentJson))
                        {
                            Console.WriteLine("Style Profile is already current: " + existing.DisplayName + " (" + existing.Guid + ", v" + existing.Version + ")");
                            return existing.Guid;
                        }
                        if (!existing.IsCheckedOut)
                        {
                            manager.CheckOutStyleProfile(existing.Guid);
                            checkedOutHere = true;
                        }
                    }
                    var definition = BuildDefinition(currentJson);
                    var result = _designer.Save(definition.ToJson(), _manifest.StyleProfile.CategoryPath);
                    manager.CheckInStyleProfile(result.Guid);
                    Console.WriteLine((existing == null ? "Created" : "Updated") + " Style Profile: " + _manifest.StyleProfile.DisplayName + " (" + result.Guid + ")");
                    return result.Guid;
                }
                catch
                {
                    if (existing != null && checkedOutHere)
                    {
                        try { manager.UndoStyleProfileCheckOut(existing.Guid); }
                        catch { }
                    }
                    throw;
                }
            });
            Verify(savedGuid);
        }

        public void Verify()
        {
            var state = GetState();
            if (!state.Exists) throw new CliException("Style Profile is not deployed: " + _manifest.StyleProfile.SystemName);
            Verify(state.Guid);
        }

        public void Inspect(bool includeDefinition)
        {
            var state = GetState();
            if (!state.Exists)
            {
                Console.WriteLine("Style Profile: absent (" + _manifest.StyleProfile.SystemName + ")");
                return;
            }
            Console.WriteLine("Style Profile: " + state.DisplayName + " [" + state.SystemName + "] (" + state.Guid + ", v" + state.Version +
                ", category " + state.CategoryPath + ", checkedOut=" + state.IsCheckedOut.ToString().ToLowerInvariant() + ")");
            Console.WriteLine("Consumers: " + state.ConsumerCount + " form(s)");
            var definition = new StyleProfile(_designer.Load(state.Guid), true);
            foreach (CustomFile file in definition.Files)
                Console.WriteLine("  " + file.Type + ": " + Uri.UnescapeDataString(file.Url));
            if (includeDefinition) Console.WriteLine(_designer.Load(state.Guid));
        }

        public void Cleanup()
        {
            WithFormsManager(manager =>
            {
                var existing = Resolve(manager);
                if (existing == null)
                {
                    Console.WriteLine("Style Profile already absent: " + _manifest.StyleProfile.SystemName);
                    return 0;
                }
                if (existing.IsSystem || existing.IsInternal)
                    throw new CliException("Refusing to delete a system/internal Style Profile: " + existing.DisplayName);
                var consumers = manager.GetFormsForStyleProfiles(new[] { existing.Guid }).Forms.Cast<FormInfo>().ToList();
                if (consumers.Count > 0)
                    throw new CliException("Style Profile is used by " + consumers.Count + " form(s): " + string.Join(", ", consumers.Select(x => x.Name).ToArray()));
                if (existing.IsCheckedOut) manager.UndoStyleProfileCheckOut(existing.Guid);
                manager.DeleteStyleProfiles(new[] { existing.Guid });
                Console.WriteLine("Deleted Style Profile: " + existing.DisplayName + " (" + existing.Guid + ")");
                return 0;
            });
        }

        public void CleanupAssets() { _assets.CleanupFiles(); }

        private void Verify(Guid expectedGuid)
        {
            WithFormsManager(manager =>
            {
                var info = manager.GetStyleProfile(expectedGuid);
                if (info == null) throw new CliException("Style Profile was not found after deployment: " + expectedGuid);
                if (info.IsCheckedOut) throw new CliException("Style Profile remains checked out after deployment: " + info.DisplayName);
                if (!string.Equals(info.Name, _manifest.StyleProfile.SystemName, StringComparison.Ordinal) ||
                    !string.Equals(info.DisplayName, _manifest.StyleProfile.DisplayName, StringComparison.Ordinal) ||
                    !string.Equals(info.Description ?? string.Empty, _manifest.StyleProfile.Description ?? string.Empty, StringComparison.Ordinal))
                    throw new CliException("Deployed Style Profile metadata does not match the manifest.");
                if (!string.Equals(NormalizeCategory(info.CategoryPath), NormalizeCategory(_manifest.StyleProfile.CategoryPath), StringComparison.OrdinalIgnoreCase))
                    throw new CliException("Deployed Style Profile category does not match the manifest. K2='" + info.CategoryPath + "', manifest='" + _manifest.StyleProfile.CategoryPath + "'.");

                var definition = new StyleProfile(_designer.Load(info.Guid), true);
                var actualFiles = definition.Files.Cast<CustomFile>().ToList();
                if (actualFiles.Count != _manifest.StyleProfile.Files.Count)
                    throw new CliException("Deployed Style Profile file count does not match the manifest.");
                for (var index = 0; index < actualFiles.Count; index++)
                {
                    var expected = _manifest.StyleProfile.Files[index];
                    var expectedType = expected.Type == "css" ? CustomFileType.Css : CustomFileType.Js;
                    if (actualFiles[index].Type != expectedType ||
                        !string.Equals(Uri.UnescapeDataString(actualFiles[index].Url), _manifest.ResolveUrl(expected), StringComparison.Ordinal))
                        throw new CliException("Deployed Style Profile file " + (index + 1) + " does not match the manifest.");
                }
                Console.WriteLine("K2 Style Profile verification: OK (" + info.DisplayName + ", " + info.Guid + ", v" + info.Version + ")");
                return 0;
            });
            _assets.Verify();
        }

        private StyleProfile BuildDefinition(string currentJson)
        {
            var profile = string.IsNullOrWhiteSpace(currentJson)
                ? new StyleProfile(_manifest.StyleProfile.SystemName, _manifest.StyleProfile.Description ?? string.Empty)
                : new StyleProfile(currentJson, true);
            profile.Name = _manifest.StyleProfile.SystemName;
            profile.DisplayName = _manifest.StyleProfile.DisplayName;
            profile.Description = _manifest.StyleProfile.Description ?? string.Empty;
            profile.PreviewFormId = string.IsNullOrWhiteSpace(_manifest.StyleProfile.PreviewFormId)
                ? (Guid?)null
                : new Guid(_manifest.StyleProfile.PreviewFormId);
            profile.Files.Clear();
            foreach (var file in _manifest.StyleProfile.Files)
            {
                profile.Files.Add(new CustomFile
                {
                    Type = file.Type == "css" ? CustomFileType.Css : CustomFileType.Js,
                    Url = Uri.EscapeDataString(_manifest.ResolveUrl(file)),
                    IsDisabled = false
                });
            }
            return profile;
        }

        private bool DefinitionMatches(StyleProfileInfo info, string currentJson)
        {
            if (!string.Equals(info.Name, _manifest.StyleProfile.SystemName, StringComparison.Ordinal) ||
                !string.Equals(info.DisplayName, _manifest.StyleProfile.DisplayName, StringComparison.Ordinal) ||
                !string.Equals(info.Description ?? string.Empty, _manifest.StyleProfile.Description ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(NormalizeCategory(info.CategoryPath), NormalizeCategory(_manifest.StyleProfile.CategoryPath), StringComparison.OrdinalIgnoreCase))
                return false;
            var profile = new StyleProfile(currentJson, true);
            var expectedPreview = string.IsNullOrWhiteSpace(_manifest.StyleProfile.PreviewFormId)
                ? (Guid?)null
                : new Guid(_manifest.StyleProfile.PreviewFormId);
            if (profile.PreviewFormId != expectedPreview) return false;
            var files = profile.Files.Cast<CustomFile>().ToList();
            if (files.Count != _manifest.StyleProfile.Files.Count) return false;
            for (var index = 0; index < files.Count; index++)
            {
                var expected = _manifest.StyleProfile.Files[index];
                var expectedType = expected.Type == "css" ? CustomFileType.Css : CustomFileType.Js;
                if (files[index].Type != expectedType ||
                    !string.Equals(Uri.UnescapeDataString(files[index].Url), _manifest.ResolveUrl(expected), StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private StyleProfileInfo Resolve(FormsManager manager)
        {
            var all = manager.GetStyleProfiles().StyleProfiles.Cast<StyleProfileInfo>().ToList();
            var byName = all.Where(x => string.Equals(x.Name, _manifest.StyleProfile.SystemName, StringComparison.OrdinalIgnoreCase)).ToList();
            var byDisplay = all.Where(x => string.Equals(x.DisplayName, _manifest.StyleProfile.DisplayName, StringComparison.OrdinalIgnoreCase)).ToList();
            var combined = byName.Concat(byDisplay).GroupBy(x => x.Guid).Select(x => x.First()).ToList();
            if (combined.Count > 1)
                throw new CliException("Style Profile name collision: systemName and displayName resolve to different artifacts. Use unique names.");
            return combined.SingleOrDefault();
        }

        private ArtifactState State(StyleProfileInfo info)
        {
            var consumers = WithFormsManager(manager => manager.GetFormsForStyleProfiles(new[] { info.Guid }).Forms.Cast<FormInfo>().Count());
            return new ArtifactState
            {
                Exists = true,
                Guid = info.Guid,
                SystemName = info.Name,
                DisplayName = info.DisplayName,
                CategoryPath = info.CategoryPath,
                Version = info.Version,
                IsCheckedOut = info.IsCheckedOut,
                CheckedOutBy = info.CheckedOutBy,
                ConsumerCount = consumers
            };
        }

        private T WithFormsManager<T>(Func<FormsManager, T> action)
        {
            var manager = new FormsManager();
            try
            {
                manager.CreateConnection();
                manager.Connection.Open(BuildConnectionString());
                return action(manager);
            }
            finally
            {
                if (manager.Connection != null)
                {
                    manager.Connection.Close();
                    manager.DeleteConnection();
                }
                manager.Dispose();
            }
        }

        private string BuildConnectionString()
        {
            var builder = new SCConnectionStringBuilder
            {
                Authenticate = true,
                Host = _manifest.K2.Host,
                Port = (uint)_manifest.K2.Port,
                Integrated = _manifest.K2.Integrated,
                IsPrimaryLogin = true,
                SecurityLabelName = _manifest.K2.SecurityLabel
            };
            if (!_manifest.K2.Integrated)
            {
                builder.WindowsDomain = _manifest.K2.Domain;
                builder.UserID = _manifest.K2.UserName;
                builder.Password = ReadEnvironmentVariable(_manifest.K2.PasswordEnvironmentVariable);
                builder.CachePassword = false;
            }
            return builder.ConnectionString;
        }

        private static string ReadEnvironmentVariable(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) throw new CliException("Required environment variable is not set: " + name);
            return value;
        }

        private static string NormalizeCategory(string value)
        {
            var normalized = (value ?? string.Empty).Trim().Trim('\\');
            const string publicFolder = "Public Folder\\";
            if (normalized.StartsWith(publicFolder, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(publicFolder.Length);
            return normalized;
        }
    }

    internal sealed class ArtifactState
    {
        public bool Exists { get; set; }
        public Guid Guid { get; set; }
        public string SystemName { get; set; }
        public string DisplayName { get; set; }
        public string CategoryPath { get; set; }
        public int Version { get; set; }
        public bool IsCheckedOut { get; set; }
        public string CheckedOutBy { get; set; }
        public int ConsumerCount { get; set; }
    }
}
