# /// script
# requires-python = ">=3.10"
# dependencies = [
#   "mcp==1.28.1",
#   "uvicorn==0.51.0",
# ]
# ///
"""Remote Streamable HTTP MCP adapter for the governed case-agent framework."""

from __future__ import annotations

import argparse
import asyncio
import contextvars
import copy
import hashlib
import hmac
import importlib.util
import json
import logging
import os
import re
import secrets
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Mapping, Optional, Sequence, Tuple

import uvicorn
from mcp.server.fastmcp import FastMCP
from mcp.server.fastmcp.exceptions import ToolError
from mcp.server.transport_security import TransportSecuritySettings
from mcp.types import ToolAnnotations
from starlette.requests import Request
from starlette.responses import JSONResponse
from starlette.types import ASGIApp, Receive, Scope, Send

from case_agent_framework import (
    AdapterError,
    AuthorizationError,
    CaseAgentFramework,
    CaseAgentFrameworkError,
    ConfirmationError,
    ContractRegistry,
    DraftConflictError,
    DraftNotFoundError,
    InMemoryDraftStore,
    ValidationFailedError,
    load_document,
    validate_creation_contract,
)


SERVER_VERSION = "0.2.0"
TOKEN_ENVIRONMENT_PATTERN = re.compile(r"^[A-Z][A-Z0-9_]{2,127}$")
SHA256_PATTERN = re.compile(r"^[a-f0-9]{64}$")
LOOPBACK_HOSTS = {"127.0.0.1", "localhost", "::1"}
LOG = logging.getLogger("k2.case_agent_mcp")


@dataclass(frozen=True)
class RemoteIdentity:
    principal_id: str
    scopes: frozenset[str]
    case_types: frozenset[str]
    token_id: str


CURRENT_IDENTITY: contextvars.ContextVar[Optional[RemoteIdentity]] = contextvars.ContextVar(
    "case_agent_mcp_identity", default=None
)


class StaticTokenDirectory:
    def __init__(self, records: Sequence[Mapping[str, Any]]) -> None:
        errors = validate_token_records(records)
        if errors:
            raise ValueError("Invalid MCP token records: " + "; ".join(errors))
        self._records: List[Tuple[str, RemoteIdentity, Optional[datetime]]] = []
        for record in records:
            token_hash = str(record["sha256"]).lower()
            identity = RemoteIdentity(
                principal_id=str(record["principalId"]),
                scopes=frozenset(str(value) for value in record["scopes"]),
                case_types=frozenset(str(value) for value in record["caseTypes"]),
                token_id=token_hash[:12],
            )
            expires_at = _parse_datetime(record.get("expiresAt"))
            self._records.append((token_hash, identity, expires_at))

    def authenticate(self, token: str) -> Optional[RemoteIdentity]:
        candidate = hashlib.sha256(token.encode("utf-8")).hexdigest()
        now = datetime.now(timezone.utc)
        for token_hash, identity, expires_at in self._records:
            if hmac.compare_digest(candidate, token_hash):
                if expires_at is not None and expires_at <= now:
                    return None
                return identity
        return None


class StaticBearerMiddleware:
    def __init__(
        self,
        app: ASGIApp,
        token_directory: StaticTokenDirectory,
        health_path: str = "/healthz",
    ) -> None:
        self.app = app
        self.token_directory = token_directory
        self.health_path = health_path

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http" or scope.get("path") == self.health_path:
            await self.app(scope, receive, send)
            return
        header_map = {
            key.decode("latin-1").lower(): value.decode("latin-1")
            for key, value in scope.get("headers", [])
        }
        authorization = header_map.get("authorization", "")
        if not authorization.startswith("Bearer "):
            await _send_auth_error(
                send,
                "Bearer authentication is required.",
            )
            return
        identity = self.token_directory.authenticate(authorization[7:].strip())
        if identity is None:
            await _send_auth_error(send, "Bearer token is invalid or expired.")
            return
        token = CURRENT_IDENTITY.set(identity)
        try:
            await self.app(scope, receive, send)
        finally:
            CURRENT_IDENTITY.reset(token)


class UnauthenticatedDevelopmentMiddleware:
    def __init__(
        self,
        app: ASGIApp,
        identity: RemoteIdentity,
        health_path: str = "/healthz",
    ) -> None:
        self.app = app
        self.identity = identity
        self.health_path = health_path

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http" or scope.get("path") == self.health_path:
            await self.app(scope, receive, send)
            return
        token = CURRENT_IDENTITY.set(self.identity)
        try:
            await self.app(scope, receive, send)
        finally:
            CURRENT_IDENTITY.reset(token)


class RequestAuthorizationProvider:
    def can_create(self, principal_id: str, case_type_code: str) -> bool:
        identity = CURRENT_IDENTITY.get()
        return (
            identity is not None
            and identity.principal_id == principal_id
            and "case:create" in identity.scopes
            and ("*" in identity.case_types or case_type_code in identity.case_types)
        )


class InlineLookupProvider:
    def __init__(self, values: Mapping[str, Sequence[Any]]) -> None:
        self.values = {
            str(code): {_json_key(value) for value in entries}
            for code, entries in values.items()
        }

    def contains(self, source_code: str, value: Any, principal_id: str) -> bool:
        del principal_id
        return _json_key(value) in self.values.get(source_code, set())


class MissingFileHandleProvider:
    def resolve(self, handle: str, principal_id: str) -> Optional[Mapping[str, Any]]:
        del handle, principal_id
        return None


@dataclass
class ServerRuntime:
    framework: CaseAgentFramework
    mutations_enabled: bool
    durable_drafts: bool
    authentication_mode: str


def validate_server_config(
    config: Mapping[str, Any],
    base_directory: Path,
    *,
    require_runtime_environment: bool = False,
    environ: Optional[Mapping[str, str]] = None,
) -> List[str]:
    errors: List[str] = []
    if config.get("schemaVersion") != 1:
        errors.append("schemaVersion must be 1")
    server = config.get("server")
    security = config.get("security")
    runtime = config.get("runtime")
    contracts = config.get("contracts")
    if not isinstance(server, Mapping):
        errors.append("server must be an object")
        server = {}
    if not isinstance(security, Mapping):
        errors.append("security must be an object")
        security = {}
    if not isinstance(runtime, Mapping):
        errors.append("runtime must be an object")
        runtime = {}
    if not isinstance(contracts, list) or not contracts:
        errors.append("contracts must be a non-empty list")
        contracts = []

    host = server.get("host")
    port = server.get("port")
    public_base_url = str(server.get("publicBaseUrl", ""))
    mcp_path = str(server.get("mcpPath", ""))
    allowed_hosts = server.get("allowedHosts")
    allowed_origins = server.get("allowedOrigins")
    if not isinstance(host, str) or not host:
        errors.append("server.host is required")
    if not isinstance(port, int) or isinstance(port, bool) or not 1 <= port <= 65535:
        errors.append("server.port must be between 1 and 65535")
    if not public_base_url.startswith(("https://", "http://")):
        errors.append("server.publicBaseUrl must be an absolute HTTP(S) URL")
    if host not in LOOPBACK_HOSTS and not public_base_url.startswith("https://"):
        if server.get("allowInsecureHttp") is not True:
            errors.append(
                "network binding requires an HTTPS publicBaseUrl unless "
                "server.allowInsecureHttp is explicitly true"
            )
    if not mcp_path.startswith("/") or mcp_path.endswith("/") or mcp_path == "/":
        errors.append("server.mcpPath must be a non-root path without a trailing slash")
    if not isinstance(allowed_hosts, list) or not allowed_hosts:
        errors.append("server.allowedHosts must be a non-empty list")
    if not isinstance(allowed_origins, list):
        errors.append("server.allowedOrigins must be a list")
    for key in ("tlsCertificateFile", "tlsPrivateKeyFile"):
        value = server.get(key)
        if value and not _resolve_path(base_directory, str(value)).is_file():
            errors.append(f"server.{key} does not exist: {value}")
    if bool(server.get("tlsCertificateFile")) != bool(server.get("tlsPrivateKeyFile")):
        errors.append(
            "server.tlsCertificateFile and server.tlsPrivateKeyFile must be supplied together"
        )

    security_mode = security.get("mode")
    if security_mode == "staticBearer":
        token_environment = security.get("tokensEnvironment")
        if (
            not isinstance(token_environment, str)
            or TOKEN_ENVIRONMENT_PATTERN.fullmatch(token_environment) is None
        ):
            errors.append(
                "security.tokensEnvironment must be an uppercase environment-variable name"
            )
        elif require_runtime_environment:
            token_value = (environ or os.environ).get(token_environment)
            if not token_value:
                errors.append(f"token environment is not set: {token_environment}")
            else:
                try:
                    records = json.loads(token_value)
                except json.JSONDecodeError as exc:
                    errors.append(f"{token_environment} is not valid JSON: {exc.msg}")
                else:
                    errors.extend(validate_token_records(records))
        if "tokens" in security or "token" in security:
            errors.append(
                "raw or inline tokens are forbidden; use security.tokensEnvironment"
            )
    elif security_mode == "none":
        if security.get("allowUnauthenticated") is not True:
            errors.append(
                "security.mode none requires security.allowUnauthenticated=true"
            )
        development_principal = security.get("developmentPrincipalId")
        if not isinstance(development_principal, str) or not development_principal.strip():
            errors.append(
                "security.developmentPrincipalId is required for unauthenticated mode"
            )
        development_case_types = security.get("caseTypes")
        if (
            not isinstance(development_case_types, list)
            or not development_case_types
            or any(
                not isinstance(value, str) or not value.strip()
                for value in development_case_types
            )
        ):
            errors.append(
                "security.caseTypes must be a non-empty list for unauthenticated mode"
            )
        if any(
            key in security
            for key in ("token", "tokens", "tokensEnvironment", "scopes")
        ):
            errors.append(
                "unauthenticated mode forbids token settings and configurable scopes"
            )
    else:
        errors.append("security.mode must be staticBearer or none")

    seen_contracts: set[Tuple[str, int]] = set()
    for index, configured_path in enumerate(contracts):
        if not isinstance(configured_path, str) or not configured_path:
            errors.append(f"contracts[{index}] must be a path")
            continue
        path = _resolve_path(base_directory, configured_path)
        if not path.is_file():
            errors.append(f"contracts[{index}] does not exist: {configured_path}")
            continue
        try:
            contract = load_document(path)
        except (OSError, ValueError) as exc:
            errors.append(f"contracts[{index}] cannot be loaded: {exc}")
            continue
        for error in validate_creation_contract(contract):
            errors.append(f"contracts[{index}]: {error}")
        if contract.get("caseTypeCode") and isinstance(
            contract.get("contractVersion"), int
        ) and not isinstance(contract.get("contractVersion"), bool):
            identity = (str(contract["caseTypeCode"]), int(contract["contractVersion"]))
            if identity in seen_contracts:
                errors.append(
                    f"duplicate contract: {identity[0]} version {identity[1]}"
                )
            seen_contracts.add(identity)

    if runtime.get("draftStore") != "memory":
        errors.append("runtime.draftStore must be memory in the built-in runtime")
    if runtime.get("allowEphemeralDrafts") is not True:
        errors.append(
            "runtime.allowEphemeralDrafts must explicitly acknowledge the reference memory store"
        )
    inline_lookups = runtime.get("inlineLookups", {})
    if not isinstance(inline_lookups, Mapping) or any(
        not isinstance(value, list) for value in inline_lookups.values()
    ):
        errors.append("runtime.inlineLookups must map lookup codes to lists")
    factory = runtime.get("factory")
    if factory is not None:
        if not isinstance(factory, Mapping):
            errors.append("runtime.factory must be an object")
        else:
            module_path = factory.get("modulePath")
            function = factory.get("function")
            if not isinstance(module_path, str) or not _resolve_path(
                base_directory, module_path
            ).is_file():
                errors.append("runtime.factory.modulePath must name an existing Python file")
            if not isinstance(function, str) or not function.isidentifier():
                errors.append("runtime.factory.function must be a Python identifier")
    if runtime.get("mutationsEnabled") is True and factory is None:
        errors.append(
            "runtime.mutationsEnabled requires a case-type runtime factory with durable drafts "
            "and registered adapters"
        )
    if security_mode == "none":
        if runtime.get("mutationsEnabled") is not False:
            errors.append(
                "unauthenticated mode requires runtime.mutationsEnabled=false"
            )
        if factory is not None:
            errors.append(
                "unauthenticated mode forbids runtime.factory and external data providers"
            )
    return errors


def validate_token_records(records: Any) -> List[str]:
    errors: List[str] = []
    if not isinstance(records, list) or not records:
        return ["token records must be a non-empty list"]
    hashes: set[str] = set()
    for index, record in enumerate(records):
        location = f"tokenRecords[{index}]"
        if not isinstance(record, Mapping):
            errors.append(f"{location} must be an object")
            continue
        unexpected = set(record) - {
            "sha256",
            "principalId",
            "scopes",
            "caseTypes",
            "expiresAt",
        }
        if unexpected:
            errors.append(
                f"{location} contains unsupported properties: {', '.join(sorted(unexpected))}"
            )
        token_hash = record.get("sha256")
        if not isinstance(token_hash, str) or SHA256_PATTERN.fullmatch(
            token_hash.lower()
        ) is None:
            errors.append(f"{location}.sha256 must be a 64-character SHA-256 hash")
        elif token_hash.lower() in hashes:
            errors.append(f"{location}.sha256 is duplicated")
        else:
            hashes.add(token_hash.lower())
        if not isinstance(record.get("principalId"), str) or not str(
            record.get("principalId")
        ).strip():
            errors.append(f"{location}.principalId is required")
        scopes = record.get("scopes")
        if not isinstance(scopes, list) or not scopes or any(
            value not in {"case:create", "case:create:commit"} for value in scopes
        ):
            errors.append(
                f"{location}.scopes must contain case:create and optionally case:create:commit"
            )
        elif "case:create:commit" in scopes and "case:create" not in scopes:
            errors.append(f"{location} commit scope requires case:create")
        case_types = record.get("caseTypes")
        if not isinstance(case_types, list) or not case_types or any(
            not isinstance(value, str) or not value for value in case_types
        ):
            errors.append(f"{location}.caseTypes must be a non-empty list")
        expires_at = record.get("expiresAt")
        if expires_at is not None:
            try:
                _parse_datetime(expires_at)
            except ValueError:
                errors.append(f"{location}.expiresAt must be an ISO-8601 UTC timestamp")
    return errors


def load_server_config(
    path: Path,
    *,
    require_runtime_environment: bool = False,
    environ: Optional[Mapping[str, str]] = None,
) -> Dict[str, Any]:
    resolved = path.resolve()
    config = load_document(resolved)
    errors = validate_server_config(
        config,
        resolved.parent,
        require_runtime_environment=require_runtime_environment,
        environ=environ,
    )
    if errors:
        raise ValueError("Invalid case-agent MCP config: " + "; ".join(errors))
    return config


def create_application(
    config: Mapping[str, Any],
    base_directory: Path,
    token_records: Sequence[Mapping[str, Any]] = (),
) -> Tuple[ASGIApp, ServerRuntime]:
    registry = ContractRegistry()
    for configured_path in config["contracts"]:
        registry.register(load_document(_resolve_path(base_directory, configured_path)))

    runtime_config = config["runtime"]
    lookup_provider: Any = InlineLookupProvider(runtime_config.get("inlineLookups", {}))
    file_provider: Any = MissingFileHandleProvider()
    draft_store: Any = InMemoryDraftStore()
    adapters: Mapping[str, Any] = {}
    durable_drafts = False
    factory_config = runtime_config.get("factory")
    if factory_config:
        bindings = _load_runtime_factory(
            base_directory,
            factory_config,
            copy.deepcopy(dict(runtime_config.get("settings", {}))),
            registry,
        )
        lookup_provider = bindings.get("lookupProvider", lookup_provider)
        file_provider = bindings.get("fileHandleProvider", file_provider)
        draft_store = bindings.get("draftStore", draft_store)
        adapters = bindings.get("adapters", {})
        durable_drafts = bindings.get("durableDrafts") is True

    mutations_enabled = runtime_config.get("mutationsEnabled") is True
    if mutations_enabled and not durable_drafts:
        raise ValueError("Mutation-enabled MCP runtime requires a durable draft store.")
    framework = CaseAgentFramework(
        registry,
        RequestAuthorizationProvider(),
        lookup_provider=lookup_provider,
        file_handle_provider=file_provider,
        draft_store=draft_store,
    )
    for adapter_code, adapter in adapters.items():
        framework.register_adapter(str(adapter_code), adapter)
    if mutations_enabled:
        for case_type_code in registry.case_types():
            contract = registry.get(case_type_code)
            if str(contract["creationAdapter"]) not in adapters:
                raise ValueError(
                    "Mutation-enabled MCP runtime is missing creation adapter: "
                    + str(contract["creationAdapter"])
                )

    server_config = config["server"]
    mcp = FastMCP(
        name=str(server_config["name"]),
        instructions=(
            "Governed K2 case creation. Discover a versioned contract, collect and validate "
            "a principal-owned draft, preview it, and require explicit confirmation before "
            "creation. Creation never submits a case."
        ),
        host=str(server_config["host"]),
        port=int(server_config["port"]),
        streamable_http_path=str(server_config["mcpPath"]),
        json_response=bool(server_config.get("jsonResponse", True)),
        stateless_http=bool(server_config.get("statelessHttp", True)),
        log_level=str(server_config.get("logLevel", "INFO")),
        transport_security=TransportSecuritySettings(
            enable_dns_rebinding_protection=True,
            allowed_hosts=[str(value) for value in server_config["allowedHosts"]],
            allowed_origins=[str(value) for value in server_config["allowedOrigins"]],
        ),
    )
    authentication_mode = str(config["security"]["mode"])
    runtime = ServerRuntime(
        framework=framework,
        mutations_enabled=mutations_enabled,
        durable_drafts=durable_drafts,
        authentication_mode=authentication_mode,
    )
    _register_tools(mcp, runtime)

    @mcp.custom_route("/healthz", methods=["GET"], include_in_schema=False)
    async def health(_: Request) -> JSONResponse:
        return JSONResponse(
            {
                "status": "ok",
                "service": "k2-case-agent-mcp",
                "version": SERVER_VERSION,
                "authenticationMode": authentication_mode,
                "unauthenticated": authentication_mode == "none",
                "mutationsEnabled": mutations_enabled,
                "durableDrafts": durable_drafts,
            }
        )

    app = mcp.streamable_http_app()
    if authentication_mode == "staticBearer":
        app.add_middleware(
            StaticBearerMiddleware,
            token_directory=StaticTokenDirectory(token_records),
            health_path="/healthz",
        )
    else:
        security_config = config["security"]
        app.add_middleware(
            UnauthenticatedDevelopmentMiddleware,
            identity=RemoteIdentity(
                principal_id=str(security_config["developmentPrincipalId"]),
                scopes=frozenset({"case:create"}),
                case_types=frozenset(
                    str(value) for value in security_config["caseTypes"]
                ),
                token_id="unauthenticated-development",
            ),
            health_path="/healthz",
        )
    return app, runtime


def _register_tools(mcp: FastMCP, runtime: ServerRuntime) -> None:
    read_annotations = ToolAnnotations(
        readOnlyHint=True,
        destructiveHint=False,
        idempotentHint=True,
        openWorldHint=False,
    )
    draft_annotations = ToolAnnotations(
        readOnlyHint=False,
        destructiveHint=False,
        idempotentHint=False,
        openWorldHint=False,
    )
    create_annotations = ToolAnnotations(
        readOnlyHint=False,
        destructiveHint=False,
        idempotentHint=True,
        openWorldHint=False,
    )

    @mcp.tool(
        title="List permitted case types",
        annotations=read_annotations,
        structured_output=True,
    )
    def list_permitted_case_types() -> Dict[str, Any]:
        identity = _require_identity("case:create")
        return _invoke(
            "list_permitted_case_types",
            identity,
            lambda: {
                "caseTypes": runtime.framework.list_permitted_case_types(
                    identity.principal_id
                )
            },
        )

    @mcp.tool(
        title="Get case creation contract",
        annotations=read_annotations,
        structured_output=True,
    )
    def get_case_creation_contract(
        case_type_code: str, contract_version: Optional[int] = None
    ) -> Dict[str, Any]:
        identity = _require_identity("case:create", case_type_code)
        return _invoke(
            "get_case_creation_contract",
            identity,
            lambda: runtime.framework.get_case_creation_contract(
                identity.principal_id, case_type_code, contract_version
            ),
        )

    @mcp.tool(
        title="Start case intake",
        annotations=draft_annotations,
        structured_output=True,
    )
    def start_case_intake(
        case_type_code: str, contract_version: Optional[int] = None
    ) -> Dict[str, Any]:
        identity = _require_identity("case:create", case_type_code)
        return _invoke(
            "start_case_intake",
            identity,
            lambda: runtime.framework.start_case_intake(
                identity.principal_id, case_type_code, contract_version
            ),
        )

    @mcp.tool(
        title="Update case intake",
        annotations=draft_annotations,
        structured_output=True,
    )
    def update_case_intake(
        draft_id: str, values: Dict[str, Any], expected_revision: int
    ) -> Dict[str, Any]:
        identity = _require_identity("case:create")
        return _invoke(
            "update_case_intake",
            identity,
            lambda: runtime.framework.update_case_intake(
                identity.principal_id,
                draft_id,
                values,
                expected_revision,
            ),
        )

    @mcp.tool(
        title="Set case intake files",
        annotations=draft_annotations,
        structured_output=True,
    )
    def set_case_intake_files(
        draft_id: str,
        file_handles: List[Dict[str, str]],
        expected_revision: int,
    ) -> Dict[str, Any]:
        identity = _require_identity("case:create")
        return _invoke(
            "set_case_intake_files",
            identity,
            lambda: runtime.framework.set_case_intake_files(
                identity.principal_id,
                draft_id,
                file_handles,
                expected_revision,
            ),
        )

    @mcp.tool(
        title="Validate case intake",
        annotations=read_annotations,
        structured_output=True,
    )
    def get_intake_validation(draft_id: str) -> Dict[str, Any]:
        identity = _require_identity("case:create")
        return _invoke(
            "get_intake_validation",
            identity,
            lambda: runtime.framework.get_intake_validation(
                identity.principal_id, draft_id
            ),
        )

    @mcp.tool(
        title="Preview case creation",
        annotations=draft_annotations,
        structured_output=True,
    )
    def preview_case_creation(draft_id: str) -> Dict[str, Any]:
        identity = _require_identity("case:create")
        return _invoke(
            "preview_case_creation",
            identity,
            lambda: runtime.framework.preview_case_creation(
                identity.principal_id, draft_id
            ),
        )

    @mcp.tool(
        title="Create confirmed case",
        annotations=create_annotations,
        structured_output=True,
    )
    def create_case(
        draft_id: str,
        confirmation_token: str,
        idempotency_key: str,
        correlation_id: Optional[str] = None,
    ) -> Dict[str, Any]:
        identity = _require_identity("case:create:commit")
        return _invoke(
            "create_case",
            identity,
            lambda: (
                runtime.framework.create_case(
                    identity.principal_id,
                    draft_id,
                    confirmation_token,
                    idempotency_key,
                    correlation_id,
                )
                if runtime.mutations_enabled
                else _raise_tool_error(
                    "Case creation is disabled until a durable case-type runtime and "
                    "registered creation adapter are configured."
                )
            ),
        )


def _require_identity(
    required_scope: str, case_type_code: Optional[str] = None
) -> RemoteIdentity:
    identity = CURRENT_IDENTITY.get()
    if identity is None:
        raise ToolError("Request identity context is unavailable.")
    if required_scope not in identity.scopes:
        raise ToolError(f"Request identity lacks required scope: {required_scope}")
    if case_type_code and (
        "*" not in identity.case_types and case_type_code not in identity.case_types
    ):
        raise ToolError("Request identity is not permitted for this case type.")
    return identity


def _invoke(name: str, identity: RemoteIdentity, function: Any) -> Dict[str, Any]:
    started = time.monotonic()
    outcome = "succeeded"
    try:
        result = function()
        if asyncio.iscoroutine(result):
            raise ToolError("Async runtime providers are not supported in this server version.")
        return dict(result)
    except ValidationFailedError as exc:
        outcome = "validation_failed"
        raise ToolError(
            json.dumps(
                {
                    "code": "validation_failed",
                    "issues": [value.to_dict() for value in exc.issues],
                },
                separators=(",", ":"),
            )
        ) from exc
    except (
        AdapterError,
        AuthorizationError,
        ConfirmationError,
        DraftConflictError,
        DraftNotFoundError,
    ) as exc:
        outcome = type(exc).__name__
        raise ToolError(str(exc)) from exc
    except CaseAgentFrameworkError as exc:
        outcome = type(exc).__name__
        raise ToolError("Case-agent operation failed.") from exc
    except ToolError:
        outcome = "rejected"
        raise
    except Exception as exc:
        outcome = "internal_error"
        LOG.exception("Unhandled case-agent MCP tool failure: %s", name)
        raise ToolError("Case-agent operation failed unexpectedly.") from exc
    finally:
        LOG.info(
            json.dumps(
                {
                    "event": "mcp_tool_call",
                    "tool": name,
                    "principalId": identity.principal_id,
                    "tokenId": identity.token_id,
                    "outcome": outcome,
                    "durationMs": round((time.monotonic() - started) * 1000, 2),
                },
                separators=(",", ":"),
            )
        )


def _load_runtime_factory(
    base_directory: Path,
    factory_config: Mapping[str, Any],
    settings: Mapping[str, Any],
    registry: ContractRegistry,
) -> Mapping[str, Any]:
    module_path = _resolve_path(base_directory, str(factory_config["modulePath"]))
    module_name = "k2_case_agent_runtime_" + hashlib.sha256(
        str(module_path).encode("utf-8")
    ).hexdigest()[:12]
    specification = importlib.util.spec_from_file_location(module_name, module_path)
    if specification is None or specification.loader is None:
        raise ValueError(f"Unable to load runtime module: {module_path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    function = getattr(module, str(factory_config["function"]), None)
    if not callable(function):
        raise ValueError(
            f"Runtime factory function does not exist: {factory_config['function']}"
        )
    bindings = function(copy.deepcopy(dict(settings)), registry)
    if not isinstance(bindings, Mapping):
        raise ValueError("Runtime factory must return a mapping of provider bindings.")
    supported = {
        "lookupProvider",
        "fileHandleProvider",
        "draftStore",
        "adapters",
        "durableDrafts",
    }
    unexpected = set(bindings) - supported
    if unexpected:
        raise ValueError(
            "Runtime factory returned unsupported bindings: "
            + ", ".join(sorted(unexpected))
        )
    if not isinstance(bindings.get("adapters", {}), Mapping):
        raise ValueError("Runtime factory adapters binding must be a mapping.")
    return bindings


def _raise_tool_error(message: str) -> Dict[str, Any]:
    raise ToolError(message)


async def _send_auth_error(send: Send, message: str) -> None:
    body = json.dumps({"error": "unauthorized", "message": message}).encode("utf-8")
    await send(
        {
            "type": "http.response.start",
            "status": 401,
            "headers": [
                (b"content-type", b"application/json"),
                (b"content-length", str(len(body)).encode("ascii")),
                (b"www-authenticate", b'Bearer realm="k2-case-agent-mcp"'),
            ],
        }
    )
    await send({"type": "http.response.body", "body": body})


def _resolve_path(base_directory: Path, value: str) -> Path:
    path = Path(value)
    if not path.is_absolute():
        path = base_directory / path
    return path.resolve()


def _parse_datetime(value: Any) -> Optional[datetime]:
    if value is None:
        return None
    if not isinstance(value, str):
        raise ValueError("timestamp must be text")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise ValueError("timestamp must include a timezone")
    return parsed.astimezone(timezone.utc)


def _json_key(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def generate_token_record(
    principal_id: str,
    case_types: Sequence[str],
    commit_scope: bool = False,
    expires_at: Optional[str] = None,
) -> Tuple[str, Dict[str, Any]]:
    if not principal_id.strip():
        raise ValueError("principalId is required")
    if not case_types:
        raise ValueError("At least one case type is required")
    if expires_at is not None:
        _parse_datetime(expires_at)
    token = secrets.token_urlsafe(48)
    scopes = ["case:create"]
    if commit_scope:
        scopes.append("case:create:commit")
    record: Dict[str, Any] = {
        "sha256": hashlib.sha256(token.encode("utf-8")).hexdigest(),
        "principalId": principal_id,
        "scopes": scopes,
        "caseTypes": list(case_types),
    }
    if expires_at:
        record["expiresAt"] = expires_at
    return token, record


def _read_token_records(config: Mapping[str, Any], environ: Mapping[str, str]) -> List[Any]:
    name = str(config["security"]["tokensEnvironment"])
    value = environ.get(name)
    if not value:
        raise ValueError(f"Token environment is not set: {name}")
    records = json.loads(value)
    errors = validate_token_records(records)
    if errors:
        raise ValueError("Invalid token environment: " + "; ".join(errors))
    return list(records)


def _serve(config_path: Path) -> int:
    resolved = config_path.resolve()
    config = load_server_config(
        resolved,
        require_runtime_environment=True,
        environ=os.environ,
    )
    token_records = (
        _read_token_records(config, os.environ)
        if config["security"]["mode"] == "staticBearer"
        else []
    )
    app, runtime = create_application(config, resolved.parent, token_records)
    server = config["server"]
    LOG.warning(
        "Starting case-agent MCP server at %s%s; auth=%s mutations=%s durableDrafts=%s",
        server["publicBaseUrl"],
        server["mcpPath"],
        runtime.authentication_mode,
        runtime.mutations_enabled,
        runtime.durable_drafts,
    )
    uvicorn.run(
        app,
        host=str(server["host"]),
        port=int(server["port"]),
        log_level=str(server.get("logLevel", "info")).lower(),
        ssl_certfile=(
            str(_resolve_path(resolved.parent, str(server["tlsCertificateFile"])))
            if server.get("tlsCertificateFile")
            else None
        ),
        ssl_keyfile=(
            str(_resolve_path(resolved.parent, str(server["tlsPrivateKeyFile"])))
            if server.get("tlsPrivateKeyFile")
            else None
        ),
        proxy_headers=False,
        server_header=False,
    )
    return 0


def _selftest() -> int:
    token, record = generate_token_record(
        "SELFTEST\\case-agent",
        ["EVIDENCE_EXCEPTION"],
    )
    directory = StaticTokenDirectory([record])
    identity = directory.authenticate(token)
    if identity is None or identity.principal_id != "SELFTEST\\case-agent":
        print("SELFTEST FAILED: bearer identity was not recovered", file=sys.stderr)
        return 1
    if directory.authenticate(token + "invalid") is not None:
        print("SELFTEST FAILED: invalid bearer token was accepted", file=sys.stderr)
        return 1
    print(
        "SELFTEST SUCCEEDED: Streamable HTTP configuration, principal-bound static "
        "bearer authentication, explicit unauthenticated-development gating, and stable "
        "case tool adapter"
    )
    return 0


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", action="store_true")
    subparsers = parser.add_subparsers(dest="command")
    validate_parser = subparsers.add_parser("validate-config")
    validate_parser.add_argument("config", type=Path)
    serve_parser = subparsers.add_parser("serve")
    serve_parser.add_argument("config", type=Path)
    token_parser = subparsers.add_parser("generate-token")
    token_parser.add_argument("--principal", required=True)
    token_parser.add_argument("--case-type", action="append", required=True)
    token_parser.add_argument("--commit", action="store_true")
    token_parser.add_argument("--expires-at")
    subparsers.add_parser("selftest")
    args = parser.parse_args(argv)
    if args.version:
        print(f"case-agent-mcp {SERVER_VERSION}")
        return 0
    if args.command == "validate-config":
        try:
            config = load_server_config(args.config)
        except (OSError, ValueError) as exc:
            print(f"ERROR: {exc}", file=sys.stderr)
            return 1
        print(
            f"Valid case-agent MCP config: {args.config.resolve()} "
            f"({config['server']['publicBaseUrl']}{config['server']['mcpPath']}, "
            f"auth={config['security']['mode']})"
        )
        return 0
    if args.command == "serve":
        try:
            return _serve(args.config)
        except (OSError, ValueError, json.JSONDecodeError) as exc:
            print(f"ERROR: {exc}", file=sys.stderr)
            return 1
    if args.command == "generate-token":
        try:
            token, record = generate_token_record(
                args.principal,
                args.case_type,
                commit_scope=args.commit,
                expires_at=args.expires_at,
            )
        except ValueError as exc:
            print(f"ERROR: {exc}", file=sys.stderr)
            return 1
        print("Client bearer token (shown once):")
        print(token)
        print("Server token record:")
        print(json.dumps(record, indent=2))
        return 0
    if args.command == "selftest":
        return _selftest()
    parser.print_help()
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
