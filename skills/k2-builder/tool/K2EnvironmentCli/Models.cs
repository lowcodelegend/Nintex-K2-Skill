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
