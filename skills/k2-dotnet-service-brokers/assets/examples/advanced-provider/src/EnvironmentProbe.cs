using System;
using A = SourceCode.SmartObjects.Services.ServiceSDK.Attributes;
using SourceCode.SmartObjects.Services.ServiceSDK.Types;

namespace K2Skills.Examples.AdvancedBroker
{
    [A.ServiceObject("EnvironmentProbe", "K2 Host Environment Probe", "Returns a small allowlisted host diagnostic record.")]
    public sealed class EnvironmentProbe
    {
        [A.Property("MachineName", SoType.Text, "Machine Name", "K2 application server name.")]
        public string MachineName { get; set; }
        [A.Property("FrameworkVersion", SoType.Text, ".NET Framework Version", "CLR version visible to the broker.")]
        public string FrameworkVersion { get; set; }
        [A.Property("UtcNow", SoType.DateTime, "UTC Now", "Current server UTC time.")]
        public DateTime UtcNow { get; set; }
        [A.Property("Is64BitProcess", SoType.YesNo, "64-bit Process", "Whether the broker runs in a 64-bit process.")]
        public bool Is64BitProcess { get; set; }

        [A.Method("ReadStatus", MethodType.Read, "Read Host Status", "Read allowlisted K2 host diagnostics.",
            new string[0], new string[0], new[] { "MachineName", "FrameworkVersion", "UtcNow", "Is64BitProcess" })]
        public EnvironmentProbe ReadStatus()
        {
            return new EnvironmentProbe {
                MachineName = Environment.MachineName,
                FrameworkVersion = Environment.Version.ToString(),
                UtcNow = DateTime.UtcNow,
                Is64BitProcess = Environment.Is64BitProcess
            };
        }
    }
}
