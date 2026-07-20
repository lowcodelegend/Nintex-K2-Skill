using System;
using System.Collections.Generic;

namespace K2EnvironmentCli
{
    public sealed class EnvironmentProfile
    {
        public int SchemaVersion { get; set; }
        public string Name { get; set; }
        public K2Settings K2 { get; set; }
        public UrlSettings Urls { get; set; }
        public SmartFormsSettings SmartForms { get; set; }
        public EnvironmentFingerprint Fingerprint { get; set; }
        public DiscoveryMetadata Discovery { get; set; }
        public string CreatedUtc { get; set; }
        public string LastValidatedUtc { get; set; }
    }

    public sealed class SmartFormsSettings
    {
        public List<string> Themes { get; set; }
        public List<StyleProfileSettings> StyleProfiles { get; set; }
        public string StyleProfileSelection { get; set; }
        public StyleProfileSettings DefaultStyleProfile { get; set; }
        public List<HeaderViewCandidate> HeaderViewCandidates { get; set; }
        public string CommonHeaderSelection { get; set; }
        public CommonHeaderSettings DefaultCommonHeader { get; set; }
    }

    public sealed class HeaderViewCandidate
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string CategoryPath { get; set; }
        public string ViewType { get; set; }
        public int Version { get; set; }
        public bool IsSystem { get; set; }
        public bool IsInternal { get; set; }
        public List<HeaderParameterSettings> Parameters { get; set; }
        public List<HeaderEventSettings> Events { get; set; }
        public List<HeaderControlSettings> Controls { get; set; }
        public int ConsumerFormCount { get; set; }
        public List<HeaderConsumerSettings> Consumers { get; set; }
    }

    public sealed class HeaderControlSettings
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; }
    }

    public sealed class HeaderParameterSettings
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string DataType { get; set; }
        public string DefaultValue { get; set; }
    }

    public sealed class HeaderEventSettings
    {
        public string Name { get; set; }
        public Guid DefinitionId { get; set; }
        public string Type { get; set; }
        public int HandlerCount { get; set; }
        public int ActionCount { get; set; }
    }

    public sealed class HeaderConsumerSettings
    {
        public Guid FormGuid { get; set; }
        public string FormName { get; set; }
        public string FormDisplayName { get; set; }
        public string CategoryPath { get; set; }
        public string InstanceId { get; set; }
        public List<HeaderParameterBindingSettings> InitializeBindings { get; set; }
    }

    public sealed class HeaderParameterBindingSettings
    {
        public string TargetParameter { get; set; }
        public string SourceType { get; set; }
        public string SourceName { get; set; }
        public string Value { get; set; }
    }

    public sealed class CommonHeaderSettings
    {
        public Guid ViewGuid { get; set; }
        public string ViewName { get; set; }
        public string ViewDisplayName { get; set; }
        public string CategoryPath { get; set; }
        public int ViewVersion { get; set; }
        public string Title { get; set; }
        public string InstanceName { get; set; }
        public bool? IsCollapsible { get; set; }
        public string InitializeEvent { get; set; }
        public List<string> ServerRules { get; set; }
        public bool ServerRulesBeforeControlTransfers { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public Dictionary<string, string> ServerLoadControlTransfers { get; set; }
        public CommonFooterSettings Footer { get; set; }
        public HeaderViewCandidate Inspection { get; set; }
    }

    public sealed class CommonFooterSettings
    {
        public Guid ViewGuid { get; set; }
        public string ViewName { get; set; }
        public string ViewDisplayName { get; set; }
        public string CategoryPath { get; set; }
        public int ViewVersion { get; set; }
        public string Title { get; set; }
        public HeaderViewCandidate Inspection { get; set; }
    }

    public sealed class StyleProfileSettings
    {
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string CategoryPath { get; set; }
        public bool IsSystem { get; set; }
        public bool IsInternal { get; set; }
        public int Version { get; set; }
    }

    public sealed class K2Settings
    {
        public string Host { get; set; }
        public int ManagementPort { get; set; }
        public int WorkflowPort { get; set; }
        public string DesignerHost { get; set; }
        public string SecurityLabel { get; set; }
        public bool IntegratedAuthentication { get; set; }
        public string InstallDirectory { get; set; }
        public string Version { get; set; }
    }

    public sealed class UrlSettings
    {
        public string Base { get; set; }
        public string Designer { get; set; }
        public string Runtime { get; set; }
        public string Management { get; set; }
    }

    public sealed class EnvironmentFingerprint
    {
        public string Machine { get; set; }
        public string Domain { get; set; }
        public string K2InstallId { get; set; }
    }

    public sealed class DiscoveryMetadata
    {
        public string ToolVersion { get; set; }
        public string WindowsIdentity { get; set; }
        public string IisSite { get; set; }
        public List<string> Sources { get; set; }
    }

    public sealed class ProfileIndex
    {
        public int SchemaVersion { get; set; }
        public string DefaultEnvironment { get; set; }
    }

    public sealed class ValidationResult
    {
        public string Name { get; set; }
        public string ProfilePath { get; set; }
        public bool Valid { get; set; }
        public string ValidatedUtc { get; set; }
        public List<ValidationCheck> Checks { get; set; }
    }

    public sealed class ValidationCheck
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }
}
