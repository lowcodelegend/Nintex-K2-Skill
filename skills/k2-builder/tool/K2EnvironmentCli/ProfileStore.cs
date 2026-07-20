using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace K2EnvironmentCli
{
    internal sealed class ProfileStore
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
        public string Root { get; private set; }
        public string EnvironmentsRoot { get { return Path.Combine(Root, "environments"); } }
        public string IndexPath { get { return Path.Combine(Root, "config.json"); } }

        public ProfileStore(string explicitRoot)
        {
            Root = string.IsNullOrWhiteSpace(explicitRoot) ? Path.Combine(GetCodexHome(), "k2") : Path.GetFullPath(Environment.ExpandEnvironmentVariables(explicitRoot));
        }

        public static string GetCodexHome()
        {
            var value = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (!string.IsNullOrWhiteSpace(value)) return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }

        public string ResolveName(string requested)
        {
            if (!string.IsNullOrWhiteSpace(requested)) return ValidateName(requested);
            var index = ReadIndex();
            if (index != null && !string.IsNullOrWhiteSpace(index.DefaultEnvironment)) return ValidateName(index.DefaultEnvironment);
            throw new CliException("No environment name was supplied and no default environment is configured.");
        }

        public string ProfilePath(string name) { return Path.Combine(EnvironmentsRoot, ValidateName(name) + ".json"); }

        public EnvironmentProfile Read(string name)
        {
            var path = ProfilePath(name);
            if (!File.Exists(path)) throw new CliException("Environment profile does not exist: " + path);
            try { return _json.Deserialize<EnvironmentProfile>(File.ReadAllText(path)); }
            catch (Exception ex) { throw new CliException("Environment profile is not valid JSON: " + path + " (" + ex.Message + ")"); }
        }

        public void Write(EnvironmentProfile profile, bool overwrite)
        {
            var path = ProfilePath(profile.Name);
            if (File.Exists(path) && !overwrite) throw new CliException("Environment profile already exists: " + path + ". Use refresh to replace it.");
            Directory.CreateDirectory(EnvironmentsRoot);
            AtomicWrite(path, PrettyJson.Serialize(profile));
        }

        public ProfileIndex ReadIndex()
        {
            if (!File.Exists(IndexPath)) return null;
            try { return _json.Deserialize<ProfileIndex>(File.ReadAllText(IndexPath)); }
            catch (Exception ex) { throw new CliException("K2 environment config is not valid JSON: " + IndexPath + " (" + ex.Message + ")"); }
        }

        public void SetDefault(string name)
        {
            name = ValidateName(name);
            if (!File.Exists(ProfilePath(name))) throw new CliException("Cannot select a missing environment profile: " + name);
            Directory.CreateDirectory(Root);
            AtomicWrite(IndexPath, PrettyJson.Serialize(new ProfileIndex { SchemaVersion = 1, DefaultEnvironment = name }));
        }

        private static void AtomicWrite(string path, string content)
        {
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, content + Environment.NewLine, new UTF8Encoding(false));
            try
            {
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static string ValidateName(string name)
        {
            name = (name ?? "").Trim();
            if (!Regex.IsMatch(name, "^[A-Za-z0-9][A-Za-z0-9_.-]{0,62}$"))
                throw new CliException("Environment names must be 1-63 characters using letters, numbers, dot, underscore, or hyphen.");
            return name;
        }
    }

    internal static class PrettyJson
    {
        public static string Serialize(object value)
        {
            var serializer = new JavaScriptSerializer();
            var raw = serializer.Serialize(value);
            var sb = new StringBuilder();
            var indent = 0; var quoted = false; var escape = false;
            foreach (var c in raw)
            {
                if (quoted)
                {
                    sb.Append(c);
                    if (escape) escape = false;
                    else if (c == '\\') escape = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') { quoted = true; sb.Append(c); }
                else if (c == '{' || c == '[') { sb.Append(c).AppendLine(); indent++; sb.Append(new string(' ', indent * 2)); }
                else if (c == '}' || c == ']') { sb.AppendLine(); indent--; sb.Append(new string(' ', indent * 2)).Append(c); }
                else if (c == ',') { sb.Append(c).AppendLine().Append(new string(' ', indent * 2)); }
                else if (c == ':') sb.Append(": ");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
