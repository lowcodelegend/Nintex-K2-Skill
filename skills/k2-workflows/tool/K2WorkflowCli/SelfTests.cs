using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace K2WorkflowCli
{
    internal static class SelfTests
    {
        public static void Run()
        {
            var task = new UserTaskSettings { Assignees = new List<string> { "$originator", "K2:DOMAIN\\approvers" } };
            string title;
            var direct = WorkflowJsonBuilder.BuildTaskDestinationItems(task, null, out title);
            Assert(title == "Manifest Assignees" && direct.Count == 2, "direct destination count");
            Assert((string)direct[0]["smartFields"][0]["fieldName"] == "ProcessOriginatorFQN" && (bool)direct[0]["isDynamic"], "originator destination");
            Assert((string)direct[1]["smartFields"][0]["text"] == "K2:DOMAIN\\approvers" && !(bool)direct[1]["isDynamic"], "literal K2 destination");

            var matrix = WorkflowJsonBuilder.BuildTaskDestinationItems(new UserTaskSettings { Assignees = new List<string>() }, 3, out title);
            Assert(title == "Approval Matrix Approver" && matrix.Count == 1, "matrix destination count");
            Assert(Convert.ToString(matrix[0]["smartFields"][0]["dataFieldReference"]).EndsWith("[{\"internalId\":3}]", StringComparison.Ordinal), "matrix data-field destination");
            Console.WriteLine("SELFTEST SUCCEEDED: explicit direct assignees and authoritative matrix destination");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new CliException("Self-test failed: " + name + ".");
        }
    }
}
