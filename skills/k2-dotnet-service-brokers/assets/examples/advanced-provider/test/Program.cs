using System;
using K2Skills.Examples.AdvancedBroker;

internal static class Program
{
    private static int Main()
    {
        var result = new TextToolkit { InputText = "  Hello   K2 World  " }.Transform();
        Require(result.NormalizedText == "Hello K2 World", "normalization");
        Require(result.Slug == "hello-k2-world", "slug");
        Require(result.Sha256.Length == 64, "SHA-256");

        var probe = new EnvironmentProbe().ReadStatus();
        Require(!string.IsNullOrWhiteSpace(probe.MachineName), "machine");
        Require(probe.UtcNow.Kind == DateTimeKind.Utc, "UTC");

        var invalid = false;
        try { new TextToolkit { InputText = " " }.Transform(); }
        catch (InvalidOperationException) { invalid = true; }
        Require(invalid, "required input");
        Console.WriteLine("Advanced broker unit tests: 7 passed");
        return 0;
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Test failed: " + name);
    }
}
