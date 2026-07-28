using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace K2EnvironmentCli
{
    internal static class CapabilityDiscovery
    {
        public static LangflowCapability Configure(string value, string flowId)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new LangflowCapability
                {
                    Configured = false,
                    Available = false,
                    Features = new LangflowFeatureSet(),
                    Message = "Langflow is not configured for this environment."
                };
            }
            Uri uri;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new CliException("Langflow URL must be an absolute HTTP or HTTPS base URL with no query or fragment.");
            Guid parsedFlowId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(flowId) && !Guid.TryParse(flowId.Trim(), out parsedFlowId))
                throw new CliException("Langflow flow ID must be a GUID.");
            return new LangflowCapability
            {
                Configured = true,
                Available = false,
                BaseUrl = value.Trim().TrimEnd('/'),
                HealthUrl = value.Trim().TrimEnd('/') + "/health_check",
                FlowId = string.IsNullOrWhiteSpace(flowId) ? null : parsedFlowId.ToString(),
                Features = new LangflowFeatureSet(),
                Message = "Langflow availability has not been checked."
            };
        }

        public static LangflowCapability Probe(LangflowCapability configured)
        {
            if (configured == null || !configured.Configured || string.IsNullOrWhiteSpace(configured.BaseUrl))
                return Configure(null, null);
            var result = Configure(configured.BaseUrl, configured.FlowId);
            result.CheckedUtc = DateTime.UtcNow.ToString("o");
            try
            {
                int status;
                var healthBody = Get(result.HealthUrl, out status);
                result.HttpStatus = status;
                var health = Deserialize(healthBody);
                var statusValue = health.ContainsKey("status") ? Convert.ToString(health["status"]) : null;
                if (status < 200 || status >= 300 || !string.Equals(statusValue, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    result.Message = result.HealthUrl + " returned HTTP " + status +
                        " without status=ok.";
                    return result;
                }
                result.Available = true;
                result.Message = "Langflow health, chat, and database checks passed.";
                try
                {
                    int versionStatus;
                    var versionBody = Get(result.BaseUrl + "/api/v1/version", out versionStatus);
                    if (versionStatus >= 200 && versionStatus < 300)
                    {
                        var version = Deserialize(versionBody);
                        result.Version = version.ContainsKey("version")
                            ? Convert.ToString(version["version"])
                            : version.ContainsKey("main_version") ? Convert.ToString(version["main_version"]) : null;
                    }
                }
                catch (Exception ex)
                {
                    result.Message += " Version discovery was unavailable: " + ex.GetBaseException().Message;
                }
                ProbeFlow(result);
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (response) result.HttpStatus = (int)response.StatusCode;
                }
                result.Message = result.HealthUrl + " is unavailable: " + ex.GetBaseException().Message;
            }
            catch (Exception ex)
            {
                result.Message = result.HealthUrl + " is unavailable: " + ex.GetBaseException().Message;
            }
            return result;
        }

        private static void ProbeFlow(LangflowCapability result)
        {
            if (string.IsNullOrWhiteSpace(result.FlowId))
            {
                result.Message += " No assistant flow is configured, so its feature set is unavailable.";
                return;
            }
            try
            {
                int flowStatus;
                var flow = Deserialize(Get(result.BaseUrl + "/api/v1/flows/" + result.FlowId, out flowStatus));
                if (flowStatus < 200 || flowStatus >= 300)
                {
                    result.Message += " Assistant flow returned HTTP " + flowStatus + ".";
                    return;
                }
                result.FlowName = Text(flow, "name");
                var data = Dictionary(flow, "data");
                var nodes = Array(data, "nodes");
                var chatOutputIds = new List<string>();
                var readFileIds = new List<string>();
                var agentIds = new List<string>();
                var caseMcpIds = new List<string>();
                var storesMessages = false;
                foreach (var value in nodes)
                {
                    var node = value as Dictionary<string, object>;
                    if (node == null) continue;
                    var id = Text(node, "id");
                    var nodeData = Dictionary(node, "data");
                    var definition = Dictionary(nodeData, "node");
                    var displayName = Text(definition, "display_name");
                    if (string.Equals(displayName, "Chat Input", StringComparison.OrdinalIgnoreCase))
                    {
                        result.ChatInputComponentId = id;
                        storesMessages = Boolean(
                            Dictionary(Dictionary(definition, "template"), "should_store_message"),
                            "value", true);
                    }
                    else if (string.Equals(displayName, "Chat Output", StringComparison.OrdinalIgnoreCase))
                        chatOutputIds.Add(id);
                    else if (string.Equals(displayName, "Read File", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(displayName, "File", StringComparison.OrdinalIgnoreCase))
                        readFileIds.Add(id);
                    else if (string.Equals(displayName, "MCP Tools", StringComparison.OrdinalIgnoreCase))
                    {
                        if (ContainsCaseTools(definition)) caseMcpIds.Add(id);
                    }
                    else if (string.Equals(displayName, "Agent", StringComparison.OrdinalIgnoreCase))
                        agentIds.Add(id);
                }
                var graph = BuildGraph(Array(data, "edges"));
                var chatReady = !string.IsNullOrWhiteSpace(result.ChatInputComponentId) &&
                    HasPathToAny(graph, result.ChatInputComponentId, chatOutputIds);
                foreach (var readFileId in readFileIds)
                {
                    if (!HasPathToAny(graph, readFileId, chatOutputIds)) continue;
                    result.ReadFileComponentId = readFileId;
                    break;
                }
                result.Features.CommandPortal = chatReady;
                result.Features.SessionHistory = chatReady && storesMessages;
                result.Features.Streaming = chatReady;
                result.Features.ImageAttachments = chatReady;
                result.Features.DocumentAttachments = !string.IsNullOrWhiteSpace(result.ReadFileComponentId);
                foreach (var mcpId in caseMcpIds)
                {
                    if (!HasPathToAny(graph, mcpId, agentIds)) continue;
                    result.Features.CaseMcpTools = true;
                    break;
                }
                result.Message += " Flow '" + (result.FlowName ?? result.FlowId) + "' was inspected; command portal=" +
                    result.Features.CommandPortal.ToString().ToLowerInvariant() +
                    ", document attachments=" + result.Features.DocumentAttachments.ToString().ToLowerInvariant() + ".";
            }
            catch (Exception ex)
            {
                result.Message += " Assistant flow discovery was unavailable: " + ex.GetBaseException().Message;
            }
        }

        private static string Get(string url, out int status)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.AllowAutoRedirect = false;
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.UserAgent = "k2env/" + Cli.Version;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                status = (int)response.StatusCode;
                return reader.ReadToEnd();
            }
        }

        private static Dictionary<string, object> Deserialize(string value)
        {
            try
            {
                return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(value) ??
                    new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                throw new Exception("response was not valid JSON (" + ex.Message + ")");
            }
        }

        private static Dictionary<string, object> Dictionary(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value)
                ? value as Dictionary<string, object>
                : null;
        }

        private static object[] Array(Dictionary<string, object> parent, string name)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null)
                return new object[0];
            var values = value as System.Collections.IEnumerable;
            if (values == null || value is string) return new object[0];
            var result = new List<object>();
            foreach (var item in values) result.Add(item);
            return result.ToArray();
        }

        private static string Text(Dictionary<string, object> parent, string name)
        {
            object value;
            return parent != null && parent.TryGetValue(name, out value) ? Convert.ToString(value) : null;
        }

        private static bool Boolean(Dictionary<string, object> parent, string name, bool defaultValue)
        {
            object value;
            if (parent == null || !parent.TryGetValue(name, out value) || value == null)
                return defaultValue;
            bool parsed;
            return bool.TryParse(Convert.ToString(value), out parsed) ? parsed : defaultValue;
        }

        private static bool ContainsCaseTools(Dictionary<string, object> definition)
        {
            var template = Dictionary(definition, "template");
            var serverField = Dictionary(template, "mcp_server");
            var server = Dictionary(serverField, "value");
            var serverName = Text(server, "name");
            if (!string.IsNullOrWhiteSpace(serverName) &&
                serverName.IndexOf("case", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            var metadataField = Dictionary(template, "tools_metadata");
            foreach (var value in Array(metadataField, "value"))
            {
                var tool = value as Dictionary<string, object>;
                var name = Text(tool, "name");
                if (!string.IsNullOrWhiteSpace(name) &&
                    (name.IndexOf("case", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("intake", StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }
            return false;
        }

        private static Dictionary<string, List<string>> BuildGraph(object[] edges)
        {
            var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in edges)
            {
                var edge = value as Dictionary<string, object>;
                var source = Text(edge, "source");
                var target = Text(edge, "target");
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;
                List<string> targets;
                if (!graph.TryGetValue(source, out targets))
                {
                    targets = new List<string>();
                    graph[source] = targets;
                }
                targets.Add(target);
            }
            return graph;
        }

        private static bool HasPathToAny(
            Dictionary<string, List<string>> graph,
            string source,
            List<string> targets)
        {
            if (string.IsNullOrWhiteSpace(source) || targets == null || targets.Count == 0)
                return false;
            var wanted = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>();
            pending.Enqueue(source);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                if (!string.Equals(current, source, StringComparison.OrdinalIgnoreCase) &&
                    wanted.Contains(current))
                    return true;
                List<string> next;
                if (!graph.TryGetValue(current, out next)) continue;
                foreach (var item in next) pending.Enqueue(item);
            }
            return false;
        }
    }
}
