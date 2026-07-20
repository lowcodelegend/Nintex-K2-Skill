using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace K2SmartFormsCli
{
    internal static class EnvironmentCommonHeader
    {
        public static CommonHeaderDefinition Resolve(ApplicationOptions application)
        {
            if (application.CommonHeader != null)
            {
                if (!application.CommonHeader.Enabled) return null;
                if (!string.IsNullOrWhiteSpace(application.CommonHeader.View)) return application.CommonHeader;
            }

            var root = Path.Combine(GetCodexHome(), "k2");
            var environment = application.CommonHeader == null ? null : application.CommonHeader.Environment;
            if (string.IsNullOrWhiteSpace(environment))
            {
                var indexPath = Path.Combine(root, "config.json");
                if (!File.Exists(indexPath)) return null;
                var index = Deserialize<ProfileIndex>(indexPath);
                environment = index == null ? null : index.DefaultEnvironment;
            }
            if (string.IsNullOrWhiteSpace(environment)) return null;
            var profilePath = Path.Combine(root, "environments", environment + ".json");
            if (!File.Exists(profilePath))
                throw new CliException("K2 environment profile does not exist for the requested common header: " + profilePath);
            var profile = Deserialize<EnvironmentProfile>(profilePath);
            var smartForms = profile == null ? null : profile.SmartForms;
            if (smartForms == null) return null;
            if (string.Equals(smartForms.CommonHeaderSelection, "unselected", StringComparison.OrdinalIgnoreCase))
                throw new CliException("The default K2 environment common-header choice is unselected. Ask whether a shared header should be used, inspect it, and persist set-common-header (or --no-common-header) before generating forms: " + environment);
            if (!string.Equals(smartForms.CommonHeaderSelection, "selected", StringComparison.OrdinalIgnoreCase))
                return null;
            if (smartForms.DefaultCommonHeader == null)
                throw new CliException("The default K2 environment marks a common header selected but has no header contract: " + environment);
            var selected = smartForms.DefaultCommonHeader;
            return new CommonHeaderDefinition
            {
                Enabled = true,
                Environment = environment,
                View = selected.ViewName,
                ViewGuid = selected.ViewGuid,
                Title = selected.Title,
                InstanceName = selected.InstanceName,
                IsCollapsible = selected.IsCollapsible,
                InitializeEvent = selected.InitializeEvent,
                ServerRules = selected.ServerRules ?? new List<string>(),
                ServerRulesBeforeControlTransfers = selected.ServerRulesBeforeControlTransfers,
                Parameters = selected.Parameters ?? new Dictionary<string, string>(),
                ServerLoadControlTransfers = selected.ServerLoadControlTransfers ?? new Dictionary<string, string>(),
                Footer = selected.Footer == null ? null : new CommonFooterDefinition
                {
                    View = selected.Footer.ViewName,
                    ViewGuid = selected.Footer.ViewGuid,
                    Title = selected.Footer.Title
                }
            };
        }

        private static T Deserialize<T>(string path)
        {
            try
            {
                return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<T>(File.ReadAllText(path));
            }
            catch (Exception ex) { throw new CliException("K2 environment profile JSON is invalid: " + path + " (" + ex.Message + ")"); }
        }

        private static string GetCodexHome()
        {
            var value = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (!string.IsNullOrWhiteSpace(value)) return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }

        private sealed class ProfileIndex { public string DefaultEnvironment { get; set; } }
        private sealed class EnvironmentProfile { public EnvironmentSmartForms SmartForms { get; set; } }
        private sealed class EnvironmentSmartForms
        {
            public string CommonHeaderSelection { get; set; }
            public EnvironmentHeader DefaultCommonHeader { get; set; }
        }
        private sealed class EnvironmentHeader
        {
            public Guid ViewGuid { get; set; }
            public string ViewName { get; set; }
            public string Title { get; set; }
            public string InstanceName { get; set; }
            public bool? IsCollapsible { get; set; }
            public string InitializeEvent { get; set; }
            public List<string> ServerRules { get; set; }
            public bool ServerRulesBeforeControlTransfers { get; set; }
            public Dictionary<string, string> Parameters { get; set; }
            public Dictionary<string, string> ServerLoadControlTransfers { get; set; }
            public EnvironmentFooter Footer { get; set; }
        }
        private sealed class EnvironmentFooter
        {
            public Guid ViewGuid { get; set; }
            public string ViewName { get; set; }
            public string Title { get; set; }
        }
    }

    internal sealed class ResolvedCommonHeader
    {
        public Guid ViewGuid { get; set; }
        public string ViewName { get; set; }
        public string DisplayName { get; set; }
        public string CategoryPath { get; set; }
        public string Title { get; set; }
        public string InstanceName { get; set; }
        public bool? IsCollapsible { get; set; }
        public string InitializeEvent { get; set; }
        public Guid InitializeEventDefinitionId { get; set; }
        public List<ResolvedHeaderRule> ServerRules { get; set; }
        public bool ServerRulesBeforeControlTransfers { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public List<ResolvedHeaderControlTransfer> ServerLoadControlTransfers { get; set; }
        public ResolvedCommonFooter Footer { get; set; }
    }

    internal sealed class ResolvedHeaderControlTransfer
    {
        public Guid ControlGuid { get; set; }
        public string ControlName { get; set; }
        public string ValueTemplate { get; set; }
    }

    internal sealed class ResolvedCommonFooter
    {
        public Guid ViewGuid { get; set; }
        public string ViewName { get; set; }
        public string DisplayName { get; set; }
        public string CategoryPath { get; set; }
        public string Title { get; set; }
    }

    internal sealed class ResolvedHeaderRule
    {
        public string Name { get; set; }
        public Guid DefinitionId { get; set; }
    }
}
