using System;
using System.IO;
using SourceCode.Forms.Management;
using SourceCode.Hosting.Client.BaseAPI;

internal static class StyleProfileSpike
{
    private static string ConnectionString()
    {
        return new SCConnectionStringBuilder {
            Authenticate = true, Host = "localhost", Port = 5555,
            Integrated = true, IsPrimaryLogin = true, SecurityLabelName = "K2"
        }.ConnectionString;
    }

    public static int Main(string[] args)
    {
        if (args.Length < 2) {
            Console.Error.WriteLine("Usage: StyleProfileSpike export <profile-name> <output-file> | deploy <definition-file> <category> | inspect <profile-name>");
            return 2;
        }
        using (var manager = new FormsManager()) {
            try {
                manager.CreateConnection();
                manager.Connection.Open(ConnectionString());
                if (args[0] == "export" && args.Length == 3) {
                    File.WriteAllText(args[2], manager.GetStyleProfileDefinition(args[1]));
                    Console.WriteLine("Exported " + args[1] + " to " + Path.GetFullPath(args[2]));
                } else if (args[0] == "deploy" && args.Length == 3) {
                    manager.Deploy(File.ReadAllText(args[1]), args[2], true);
                    Console.WriteLine("Deployed Style Profile definition to " + args[2]);
                } else if (args[0] == "inspect" && args.Length == 2) {
                    var info = manager.GetStyleProfile(args[1]);
                    Console.WriteLine(info.DisplayName + " [" + info.Name + "] " + info.Guid + " v" + info.Version + " " + info.CategoryPath);
                    Console.WriteLine(manager.GetStyleProfileDefinition(info.Guid));
                } else {
                    Console.Error.WriteLine("Invalid arguments.");
                    return 2;
                }
                return 0;
            }
            finally {
                if (manager.Connection != null) {
                    manager.Connection.Close();
                    manager.DeleteConnection();
                }
            }
        }
    }
}
