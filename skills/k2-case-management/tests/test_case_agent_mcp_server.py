import asyncio
import copy
import json
from pathlib import Path
import socket
import sys
import threading
import time
import unittest

import httpx
import uvicorn
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from case_agent_mcp_server import (  # noqa: E402
    StaticTokenDirectory,
    create_application,
    generate_token_record,
    validate_server_config,
    validate_token_records,
)


def free_port():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as value:
        value.bind(("127.0.0.1", 0))
        return value.getsockname()[1]


def config_for(port):
    return {
        "schemaVersion": 1,
        "server": {
            "name": "K2 Case Agent Test",
            "host": "127.0.0.1",
            "port": port,
            "publicBaseUrl": f"http://127.0.0.1:{port}",
            "mcpPath": "/mcp",
            "jsonResponse": True,
            "statelessHttp": True,
            "logLevel": "ERROR",
            "allowedHosts": ["127.0.0.1:*", "localhost:*"],
            "allowedOrigins": ["http://langflow.test"],
            "allowInsecureHttp": False,
        },
        "security": {
            "mode": "staticBearer",
            "tokensEnvironment": "CASE_AGENT_MCP_TEST_TOKENS",
        },
        "contracts": ["case-agent-creation-contract.yaml"],
        "runtime": {
            "draftStore": "memory",
            "allowEphemeralDrafts": True,
            "mutationsEnabled": False,
            "inlineLookups": {
                "CONFIDENTIALITY": ["INTERNAL", "RESTRICTED"],
                "EXCEPTION_TYPE": ["PROCESS", "EVIDENCE"],
            },
            "factory": None,
            "settings": {},
        },
    }


class RunningServer:
    def __init__(self, app, port):
        self.server = uvicorn.Server(
            uvicorn.Config(
                app,
                host="127.0.0.1",
                port=port,
                log_level="critical",
                lifespan="on",
            )
        )
        self.thread = threading.Thread(target=self.server.run, daemon=True)
        self.base_url = f"http://127.0.0.1:{port}"

    def __enter__(self):
        self.thread.start()
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            try:
                response = httpx.get(self.base_url + "/healthz", timeout=0.5)
                if response.status_code == 200:
                    return self
            except httpx.HTTPError:
                pass
            time.sleep(0.05)
        raise RuntimeError("MCP integration-test server did not start.")

    def __exit__(self, exc_type, exc_value, traceback):
        self.server.should_exit = True
        self.thread.join(timeout=10)
        if self.thread.is_alive():
            raise RuntimeError("MCP integration-test server did not stop.")


def result_value(result):
    if result.structuredContent is not None:
        value = result.structuredContent
        if set(value) == {"result"} and isinstance(value["result"], dict):
            return value["result"]
        return value
    for content in result.content:
        if getattr(content, "type", None) == "text":
            return json.loads(content.text)
    raise AssertionError("Tool result did not contain structured or JSON text output.")


async def connect(url, token):
    http_client = httpx.AsyncClient(
        headers={"Authorization": f"Bearer {token}"},
        timeout=10,
    )
    transport = streamable_http_client(url, http_client=http_client)
    streams = await transport.__aenter__()
    session = ClientSession(streams[0], streams[1])
    await session.__aenter__()
    await session.initialize()
    return http_client, transport, session


async def disconnect(http_client, transport, session):
    await session.__aexit__(None, None, None)
    await transport.__aexit__(None, None, None)
    await http_client.aclose()


class CaseAgentMcpServerTests(unittest.TestCase):
    def test_network_config_requires_security_boundaries(self):
        config = config_for(8443)
        config["server"]["host"] = "0.0.0.0"
        config["server"]["allowedHosts"] = []
        config["security"]["tokens"] = [{"token": "unsafe"}]
        config["runtime"]["mutationsEnabled"] = True
        errors = validate_server_config(config, ROOT / "assets")
        self.assertTrue(any("HTTPS publicBaseUrl" in error for error in errors))
        self.assertTrue(any("allowedHosts" in error for error in errors))
        self.assertTrue(any("inline tokens are forbidden" in error for error in errors))
        self.assertTrue(any("requires a case-type runtime factory" in error for error in errors))

    def test_token_records_are_hashed_scoped_and_expirable(self):
        token, record = generate_token_record(
            r"EXAMPLE\alice",
            ["EVIDENCE_EXCEPTION"],
            commit_scope=True,
            expires_at="2099-01-01T00:00:00Z",
        )
        self.assertNotIn(token, json.dumps(record))
        self.assertEqual([], validate_token_records([record]))
        identity = StaticTokenDirectory([record]).authenticate(token)
        self.assertEqual(r"EXAMPLE\alice", identity.principal_id)
        self.assertIn("case:create:commit", identity.scopes)
        self.assertIsNone(StaticTokenDirectory([record]).authenticate(token + "invalid"))

    def test_remote_streamable_http_tools_and_authorization(self):
        asyncio.run(self._remote_streamable_http_tools_and_authorization())

    async def _remote_streamable_http_tools_and_authorization(self):
        port = free_port()
        config = config_for(port)
        token_a, record_a = generate_token_record(
            r"EXAMPLE\alice",
            ["EVIDENCE_EXCEPTION"],
            commit_scope=True,
        )
        token_b, record_b = generate_token_record(
            r"EXAMPLE\bob",
            ["EVIDENCE_EXCEPTION"],
        )
        app, runtime = create_application(
            config,
            ROOT / "assets",
            [record_a, record_b],
        )
        self.assertFalse(runtime.mutations_enabled)
        self.assertFalse(runtime.durable_drafts)

        with RunningServer(app, port) as server:
            health = httpx.get(server.base_url + "/healthz")
            self.assertEqual(200, health.status_code)
            self.assertFalse(health.json()["mutationsEnabled"])

            unauthorized = httpx.post(server.base_url + "/mcp", json={})
            self.assertEqual(401, unauthorized.status_code)
            self.assertIn("Bearer", unauthorized.headers["www-authenticate"])

            invalid_origin = httpx.post(
                server.base_url + "/mcp",
                headers={
                    "Authorization": f"Bearer {token_a}",
                    "Origin": "https://attacker.example",
                },
                json={},
            )
            self.assertEqual(403, invalid_origin.status_code)

            client_a, transport_a, session_a = await connect(
                server.base_url + "/mcp", token_a
            )
            try:
                tools = await session_a.list_tools()
                tool_names = {tool.name for tool in tools.tools}
                self.assertEqual(
                    {
                        "list_permitted_case_types",
                        "get_case_creation_contract",
                        "start_case_intake",
                        "update_case_intake",
                        "set_case_intake_files",
                        "get_intake_validation",
                        "preview_case_creation",
                        "create_case",
                    },
                    tool_names,
                )
                create_tool = next(
                    value for value in tools.tools if value.name == "create_case"
                )
                self.assertTrue(create_tool.annotations.idempotentHint)
                self.assertFalse(create_tool.annotations.readOnlyHint)

                permitted = result_value(
                    await session_a.call_tool("list_permitted_case_types", {})
                )
                self.assertEqual(
                    "EVIDENCE_EXCEPTION",
                    permitted["caseTypes"][0]["caseTypeCode"],
                )
                contract = result_value(
                    await session_a.call_tool(
                        "get_case_creation_contract",
                        {"case_type_code": "EVIDENCE_EXCEPTION"},
                    )
                )
                self.assertNotIn("creationAdapter", contract)
                self.assertNotIn("writeTarget", contract["extensionEntities"][0])

                draft = result_value(
                    await session_a.call_tool(
                        "start_case_intake",
                        {"case_type_code": "EVIDENCE_EXCEPTION"},
                    )
                )
                values = {
                    "canonical.Title": "A remotely created exception draft",
                    "canonical.Description": (
                        "This remotely collected description is long enough for validation."
                    ),
                    "canonical.ConfidentialityCode": "INTERNAL",
                    "extensions.exception.ExceptionTypeCode": "PROCESS",
                    "extensions.exception.OccurredDate": "2026-07-26",
                    "extensions.exception.ContainsPersonalData": False,
                }
                draft = result_value(
                    await session_a.call_tool(
                        "update_case_intake",
                        {
                            "draft_id": draft["draftId"],
                            "values": values,
                            "expected_revision": draft["revision"],
                        },
                    )
                )
                validation = result_value(
                    await session_a.call_tool(
                        "get_intake_validation",
                        {"draft_id": draft["draftId"]},
                    )
                )
                self.assertTrue(validation["valid"], validation["issues"])
                preview = result_value(
                    await session_a.call_tool(
                        "preview_case_creation",
                        {"draft_id": draft["draftId"]},
                    )
                )
                self.assertTrue(preview["confirmationToken"])

                disabled = await session_a.call_tool(
                    "create_case",
                    {
                        "draft_id": draft["draftId"],
                        "confirmation_token": preview["confirmationToken"],
                        "idempotency_key": "mcp-test-create",
                    },
                )
                self.assertTrue(disabled.isError)
                self.assertIn("disabled", disabled.content[0].text)

                client_b, transport_b, session_b = await connect(
                    server.base_url + "/mcp", token_b
                )
                try:
                    cross_principal = await session_b.call_tool(
                        "get_intake_validation",
                        {"draft_id": draft["draftId"]},
                    )
                    self.assertTrue(cross_principal.isError)
                    self.assertIn("another principal", cross_principal.content[0].text)
                finally:
                    await disconnect(client_b, transport_b, session_b)
            finally:
                await disconnect(client_a, transport_a, session_a)


if __name__ == "__main__":
    unittest.main()
