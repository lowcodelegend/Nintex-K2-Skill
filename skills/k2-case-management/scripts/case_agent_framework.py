#!/usr/bin/env python3
"""Transport-neutral foundation for governed agentic case creation.

This module intentionally contains no MCP transport, model client, K2 client,
SQL execution, or persistent storage. It defines the reusable contract,
validation, draft, confirmation, authorization, and adapter boundaries that
those integrations must use.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import sys
import uuid
from dataclasses import dataclass, field
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Any, Dict, Iterable, List, Mapping, Optional, Protocol, Sequence, Tuple


ALLOWED_CREATION_MODES = {"deferredMaterialization"}
ALLOWED_FIELD_TYPES = {"string", "integer", "number", "boolean", "date", "datetime", "array"}
ALLOWED_FIELD_SOURCES = {"user", "server", "derived"}
ALLOWED_SENSITIVITY = {"public", "internal", "confidential", "restricted"}
ALLOWED_TOP_LEVEL_DRAFT_KEYS = {
    "caseTypeCode",
    "contractVersion",
    "canonical",
    "extensions",
    "fileHandles",
}
RESERVED_FIELD_LEAVES = {
    "CaseId",
    "CaseNumber",
    "CreatedByFQN",
    "OpenedDate",
    "RowVersion",
    "WorkflowInstanceId",
}
CANONICAL_CREATION_FIELDS = {
    "Title",
    "Description",
    "Source",
    "PriorityCode",
    "SeverityCode",
    "RiskCode",
    "ConfidentialityCode",
    "JurisdictionCode",
    "OwningTeam",
    "RequesterPartyId",
    "SubjectPartyId",
    "ParentCaseId",
    "TargetDate",
}
CODE_PATTERN = re.compile(r"^[A-Z][A-Z0-9_]{1,63}$")
ENTITY_PATTERN = re.compile(r"^[A-Za-z][A-Za-z0-9_]{1,63}$")
GUIDANCE_LIST_FIELDS = ("useWhen", "doNotUseWhen")


class CaseAgentFrameworkError(Exception):
    """Base error for the transport-neutral framework."""


class ContractNotFoundError(CaseAgentFrameworkError):
    pass


class AuthorizationError(CaseAgentFrameworkError):
    pass


class DraftNotFoundError(CaseAgentFrameworkError):
    pass


class DraftConflictError(CaseAgentFrameworkError):
    pass


class ValidationFailedError(CaseAgentFrameworkError):
    def __init__(self, issues: Sequence["ValidationIssue"]):
        super().__init__("Case intake validation failed.")
        self.issues = list(issues)


class ConfirmationError(CaseAgentFrameworkError):
    pass


class AdapterError(CaseAgentFrameworkError):
    pass


@dataclass(frozen=True)
class ValidationIssue:
    path: str
    code: str
    message: str

    def to_dict(self) -> Dict[str, str]:
        return {"path": self.path, "code": self.code, "message": self.message}


@dataclass
class DraftRecord:
    draft_id: str
    principal_id: str
    case_type_code: str
    contract_version: int
    values: Dict[str, Any]
    file_handles: List[Dict[str, str]] = field(default_factory=list)
    revision: int = 0
    status: str = "COLLECTING"
    created_date: str = field(default_factory=lambda: _utc_now())
    updated_date: str = field(default_factory=lambda: _utc_now())

    def public_copy(self) -> Dict[str, Any]:
        return {
            "draftId": self.draft_id,
            "caseTypeCode": self.case_type_code,
            "contractVersion": self.contract_version,
            "values": copy.deepcopy(self.values),
            "fileHandles": copy.deepcopy(self.file_handles),
            "revision": self.revision,
            "status": self.status,
            "createdDate": self.created_date,
            "updatedDate": self.updated_date,
        }


@dataclass(frozen=True)
class CreationRequest:
    principal_id: str
    draft_id: str
    case_type_code: str
    contract_version: int
    idempotency_key: str
    correlation_id: str
    canonical: Mapping[str, Any]
    extensions: Mapping[str, Any]
    file_handles: Sequence[Mapping[str, str]]


class CaseCreationAdapter(Protocol):
    def create(self, request: CreationRequest) -> Mapping[str, Any]:
        """Atomically materialize a canonical Case and its registered extensions."""


class AuthorizationProvider(Protocol):
    def can_create(self, principal_id: str, case_type_code: str) -> bool:
        """Return whether the principal may create the case type."""


class LookupProvider(Protocol):
    def contains(self, source_code: str, value: Any, principal_id: str) -> bool:
        """Validate one value against a governed lookup visible to the principal."""


class FileHandleProvider(Protocol):
    def resolve(self, handle: str, principal_id: str) -> Optional[Mapping[str, Any]]:
        """Resolve an opaque, principal-bound staged file handle."""


class ContractRegistry:
    def __init__(self) -> None:
        self._contracts: Dict[Tuple[str, int], Dict[str, Any]] = {}
        self._latest: Dict[str, int] = {}

    def register(self, contract: Mapping[str, Any]) -> None:
        materialized = copy.deepcopy(dict(contract))
        errors = validate_creation_contract(materialized)
        if errors:
            raise ValueError("Invalid case creation contract: " + "; ".join(errors))
        key = (str(materialized["caseTypeCode"]), int(materialized["contractVersion"]))
        if key in self._contracts:
            raise ValueError(
                "Case creation contract is already registered: "
                f"{key[0]} version {key[1]}"
            )
        self._contracts[key] = materialized
        self._latest[key[0]] = max(key[1], self._latest.get(key[0], 0))

    def get(self, case_type_code: str, contract_version: Optional[int] = None) -> Dict[str, Any]:
        if contract_version is None:
            contract_version = self._latest.get(case_type_code)
        if contract_version is None:
            raise ContractNotFoundError(f"Unknown case type: {case_type_code}")
        value = self._contracts.get((case_type_code, int(contract_version)))
        if value is None:
            raise ContractNotFoundError(
                f"Unknown case creation contract: {case_type_code} version {contract_version}"
            )
        return copy.deepcopy(value)

    def case_types(self) -> List[str]:
        return sorted(self._latest)


class InMemoryDraftStore:
    """Reference draft store for tests and local composition only."""

    def __init__(self) -> None:
        self._drafts: Dict[str, DraftRecord] = {}

    def create(
        self, principal_id: str, case_type_code: str, contract_version: int, entity_codes: Iterable[str]
    ) -> DraftRecord:
        draft = DraftRecord(
            draft_id=str(uuid.uuid4()),
            principal_id=principal_id,
            case_type_code=case_type_code,
            contract_version=contract_version,
            values={
                "canonical": {},
                "extensions": {entity_code: {} for entity_code in entity_codes},
            },
        )
        self._drafts[draft.draft_id] = draft
        return draft

    def get(self, draft_id: str) -> Optional[DraftRecord]:
        return self._drafts.get(draft_id)


@dataclass(frozen=True)
class _Confirmation:
    token: str
    draft_id: str
    principal_id: str
    revision: int
    digest: str


class CaseAgentFramework:
    """Stable, transport-neutral facade intended for a later MCP adapter."""

    def __init__(
        self,
        registry: ContractRegistry,
        authorization: AuthorizationProvider,
        lookup_provider: Optional[LookupProvider] = None,
        file_handle_provider: Optional[FileHandleProvider] = None,
        draft_store: Optional[InMemoryDraftStore] = None,
    ) -> None:
        self.registry = registry
        self.authorization = authorization
        self.lookup_provider = lookup_provider
        self.file_handle_provider = file_handle_provider
        self.draft_store = draft_store or InMemoryDraftStore()
        self._adapters: Dict[str, CaseCreationAdapter] = {}
        self._confirmations: Dict[str, _Confirmation] = {}
        self._idempotent_results: Dict[Tuple[str, str], Tuple[str, Dict[str, Any]]] = {}

    def register_adapter(self, adapter_code: str, adapter: CaseCreationAdapter) -> None:
        if not adapter_code or adapter_code in self._adapters:
            raise ValueError(f"Creation adapter is missing or already registered: {adapter_code}")
        self._adapters[adapter_code] = adapter

    def list_permitted_case_types(self, principal_id: str) -> List[Dict[str, Any]]:
        values: List[Dict[str, Any]] = []
        for case_type_code in self.registry.case_types():
            if self.authorization.can_create(principal_id, case_type_code):
                contract = self.registry.get(case_type_code)
                values.append(
                    {
                        "caseTypeCode": case_type_code,
                        "name": contract["name"],
                        "description": contract["description"],
                        "useWhen": copy.deepcopy(contract["useWhen"]),
                        "doNotUseWhen": copy.deepcopy(contract["doNotUseWhen"]),
                        "expectedOutcome": contract["expectedOutcome"],
                        "contractVersion": contract["contractVersion"],
                    }
                )
        return values

    def get_case_creation_contract(
        self, principal_id: str, case_type_code: str, contract_version: Optional[int] = None
    ) -> Dict[str, Any]:
        self._require_create(principal_id, case_type_code)
        contract = self.registry.get(case_type_code, contract_version)
        public_contract = copy.deepcopy(contract)
        public_contract.pop("creationAdapter", None)
        for entity in public_contract.get("extensionEntities", []):
            entity.pop("writeTarget", None)
        return public_contract

    def start_case_intake(
        self, principal_id: str, case_type_code: str, contract_version: Optional[int] = None
    ) -> Dict[str, Any]:
        self._require_create(principal_id, case_type_code)
        contract = self.registry.get(case_type_code, contract_version)
        entity_codes = [str(value["entityCode"]) for value in contract.get("extensionEntities", [])]
        return self.draft_store.create(
            principal_id,
            case_type_code,
            int(contract["contractVersion"]),
            entity_codes,
        ).public_copy()

    def update_case_intake(
        self,
        principal_id: str,
        draft_id: str,
        values: Mapping[str, Any],
        expected_revision: int,
    ) -> Dict[str, Any]:
        draft = self._owned_draft(principal_id, draft_id)
        if draft.status != "COLLECTING":
            raise DraftConflictError(f"Draft is not editable in status {draft.status}.")
        if draft.revision != expected_revision:
            raise DraftConflictError(
                f"Draft revision is {draft.revision}; expected {expected_revision}."
            )
        contract = self.registry.get(draft.case_type_code, draft.contract_version)
        fields = _field_index(contract)
        for path, value in values.items():
            field_contract = fields.get(path)
            if field_contract is None:
                raise DraftConflictError(f"Field is not declared by the creation contract: {path}")
            if field_contract.get("source", "user") != "user":
                raise DraftConflictError(f"Field is not user-writable: {path}")
            _set_path(draft.values, path, copy.deepcopy(value))
        self._touch(draft)
        return draft.public_copy()

    def set_case_intake_files(
        self,
        principal_id: str,
        draft_id: str,
        file_handles: Sequence[Mapping[str, str]],
        expected_revision: int,
    ) -> Dict[str, Any]:
        draft = self._owned_draft(principal_id, draft_id)
        if draft.status != "COLLECTING":
            raise DraftConflictError(f"Draft is not editable in status {draft.status}.")
        if draft.revision != expected_revision:
            raise DraftConflictError(
                f"Draft revision is {draft.revision}; expected {expected_revision}."
            )
        normalized: List[Dict[str, str]] = []
        for index, value in enumerate(file_handles):
            if not isinstance(value, Mapping):
                raise DraftConflictError(f"fileHandles[{index}] must be an object.")
            unexpected = set(value) - {"handle", "requirementCode"}
            if unexpected:
                raise DraftConflictError(
                    f"fileHandles[{index}] contains unsupported properties: "
                    + ", ".join(sorted(unexpected))
                )
            handle = str(value.get("handle", "")).strip()
            requirement = str(value.get("requirementCode", "")).strip()
            if not handle or not requirement:
                raise DraftConflictError(
                    f"fileHandles[{index}] requires handle and requirementCode."
                )
            normalized.append({"handle": handle, "requirementCode": requirement})
        draft.file_handles = normalized
        self._touch(draft)
        return draft.public_copy()

    def get_intake_validation(self, principal_id: str, draft_id: str) -> Dict[str, Any]:
        draft = self._owned_draft(principal_id, draft_id)
        contract = self.registry.get(draft.case_type_code, draft.contract_version)
        envelope = self._envelope(draft)
        issues = validate_case_intake(
            contract,
            envelope,
            principal_id=principal_id,
            lookup_provider=self.lookup_provider,
            file_handle_provider=self.file_handle_provider,
        )
        return {
            "draftId": draft.draft_id,
            "revision": draft.revision,
            "valid": not issues,
            "issues": [issue.to_dict() for issue in issues],
        }

    def preview_case_creation(self, principal_id: str, draft_id: str) -> Dict[str, Any]:
        draft = self._owned_draft(principal_id, draft_id)
        validation = self.get_intake_validation(principal_id, draft_id)
        if not validation["valid"]:
            raise ValidationFailedError(
                [
                    ValidationIssue(value["path"], value["code"], value["message"])
                    for value in validation["issues"]
                ]
            )
        digest = _stable_digest(self._envelope(draft))
        token = str(uuid.uuid4())
        self._confirmations[token] = _Confirmation(
            token=token,
            draft_id=draft.draft_id,
            principal_id=principal_id,
            revision=draft.revision,
            digest=digest,
        )
        return {
            "draftId": draft.draft_id,
            "revision": draft.revision,
            "caseTypeCode": draft.case_type_code,
            "contractVersion": draft.contract_version,
            "canonical": copy.deepcopy(draft.values["canonical"]),
            "extensions": copy.deepcopy(draft.values["extensions"]),
            "fileCount": len(draft.file_handles),
            "confirmationToken": token,
        }

    def create_case(
        self,
        principal_id: str,
        draft_id: str,
        confirmation_token: str,
        idempotency_key: str,
        correlation_id: Optional[str] = None,
    ) -> Dict[str, Any]:
        if not idempotency_key or len(idempotency_key) > 200:
            raise DraftConflictError("idempotencyKey is required and cannot exceed 200 characters.")
        draft = self._owned_draft(principal_id, draft_id)
        envelope = self._envelope(draft)
        digest = _stable_digest(envelope)
        idempotency_identity = (principal_id, idempotency_key)
        prior = self._idempotent_results.get(idempotency_identity)
        if prior is not None:
            prior_digest, prior_result = prior
            if prior_digest != digest:
                raise DraftConflictError(
                    "The idempotency key was already used for a different intake snapshot."
                )
            return copy.deepcopy(prior_result)

        confirmation = self._confirmations.get(confirmation_token)
        if (
            confirmation is None
            or confirmation.draft_id != draft_id
            or confirmation.principal_id != principal_id
            or confirmation.revision != draft.revision
            or confirmation.digest != digest
        ):
            raise ConfirmationError(
                "Confirmation is missing, stale, belongs to another principal, or references "
                "a different intake snapshot."
            )

        validation = self.get_intake_validation(principal_id, draft_id)
        if not validation["valid"]:
            raise ValidationFailedError(
                [
                    ValidationIssue(value["path"], value["code"], value["message"])
                    for value in validation["issues"]
                ]
            )
        contract = self.registry.get(draft.case_type_code, draft.contract_version)
        adapter_code = str(contract["creationAdapter"])
        adapter = self._adapters.get(adapter_code)
        if adapter is None:
            raise AdapterError(f"Creation adapter is not registered: {adapter_code}")
        request = CreationRequest(
            principal_id=principal_id,
            draft_id=draft.draft_id,
            case_type_code=draft.case_type_code,
            contract_version=draft.contract_version,
            idempotency_key=idempotency_key,
            correlation_id=correlation_id or str(uuid.uuid4()),
            canonical=copy.deepcopy(draft.values["canonical"]),
            extensions=copy.deepcopy(draft.values["extensions"]),
            file_handles=copy.deepcopy(draft.file_handles),
        )
        result = dict(adapter.create(request))
        if not result.get("caseId") or not result.get("caseNumber"):
            raise AdapterError("Creation adapter must return caseId and caseNumber.")
        result.setdefault("caseTypeCode", draft.case_type_code)
        result.setdefault("contractVersion", draft.contract_version)
        result.setdefault("submitted", False)
        if result["submitted"] is not False:
            raise AdapterError(
                "Creation adapters cannot submit a case; submission requires a separate command."
            )
        draft.status = "CREATED"
        draft.updated_date = _utc_now()
        self._confirmations.pop(confirmation_token, None)
        self._idempotent_results[idempotency_identity] = (digest, copy.deepcopy(result))
        return copy.deepcopy(result)

    def _require_create(self, principal_id: str, case_type_code: str) -> None:
        if not principal_id:
            raise AuthorizationError("An authenticated principal is required.")
        if not self.authorization.can_create(principal_id, case_type_code):
            raise AuthorizationError(
                f"Principal is not authorized to create case type {case_type_code}."
            )

    def _owned_draft(self, principal_id: str, draft_id: str) -> DraftRecord:
        draft = self.draft_store.get(draft_id)
        if draft is None:
            raise DraftNotFoundError(f"Unknown draft: {draft_id}")
        if draft.principal_id != principal_id:
            raise AuthorizationError("The case intake belongs to another principal.")
        self._require_create(principal_id, draft.case_type_code)
        return draft

    @staticmethod
    def _envelope(draft: DraftRecord) -> Dict[str, Any]:
        return {
            "caseTypeCode": draft.case_type_code,
            "contractVersion": draft.contract_version,
            "canonical": copy.deepcopy(draft.values["canonical"]),
            "extensions": copy.deepcopy(draft.values["extensions"]),
            "fileHandles": copy.deepcopy(draft.file_handles),
        }

    def _touch(self, draft: DraftRecord) -> None:
        draft.revision += 1
        draft.updated_date = _utc_now()
        stale = [
            token
            for token, value in self._confirmations.items()
            if value.draft_id == draft.draft_id
        ]
        for token in stale:
            self._confirmations.pop(token, None)


def load_document(path: Path) -> Dict[str, Any]:
    text = path.read_text(encoding="utf-8")
    try:
        value = json.loads(text)
    except json.JSONDecodeError as json_error:
        try:
            import yaml  # type: ignore
        except ImportError as exc:
            raise ValueError(
                f"{path}: not JSON-compatible YAML and PyYAML is unavailable "
                f"({json_error.msg} at line {json_error.lineno})"
            ) from exc
        value = yaml.safe_load(text)
    if not isinstance(value, dict):
        raise ValueError(f"{path}: document root must be a mapping")
    return value


def validate_creation_contract(contract: Mapping[str, Any]) -> List[str]:
    errors: List[str] = []
    if contract.get("schemaVersion") != 2:
        errors.append("schemaVersion must be 2")
    case_type_code = contract.get("caseTypeCode")
    if not isinstance(case_type_code, str) or not CODE_PATTERN.fullmatch(case_type_code):
        errors.append("caseTypeCode must be an uppercase stable code")
    if not isinstance(contract.get("name"), str) or not str(contract.get("name")).strip():
        errors.append("name is required")
    _validate_case_type_guidance(contract, errors)
    version = contract.get("contractVersion")
    if not isinstance(version, int) or isinstance(version, bool) or version < 1:
        errors.append("contractVersion must be a positive integer")
    if contract.get("creationMode") not in ALLOWED_CREATION_MODES:
        errors.append("creationMode must be deferredMaterialization")
    if not isinstance(contract.get("creationAdapter"), str) or not str(
        contract.get("creationAdapter")
    ).strip():
        errors.append("creationAdapter is required")

    canonical_fields = contract.get("canonicalFields")
    extension_entities = contract.get("extensionEntities", [])
    if not isinstance(canonical_fields, list) or not canonical_fields:
        errors.append("canonicalFields must be a non-empty list")
        canonical_fields = []
    if not isinstance(extension_entities, list):
        errors.append("extensionEntities must be a list")
        extension_entities = []

    field_entries: List[Tuple[str, Mapping[str, Any]]] = []
    entity_codes: set[str] = set()
    for index, field_contract in enumerate(canonical_fields):
        if not isinstance(field_contract, Mapping):
            errors.append(f"canonicalFields[{index}] must be an object")
            continue
        field_entries.append((f"canonicalFields[{index}]", field_contract))

    for entity_index, entity in enumerate(extension_entities):
        prefix = f"extensionEntities[{entity_index}]"
        if not isinstance(entity, Mapping):
            errors.append(f"{prefix} must be an object")
            continue
        entity_code = entity.get("entityCode")
        if not isinstance(entity_code, str) or not ENTITY_PATTERN.fullmatch(entity_code):
            errors.append(f"{prefix}.entityCode must be a stable identifier")
            entity_code = ""
        elif entity_code in entity_codes:
            errors.append(f"duplicate extension entityCode: {entity_code}")
        else:
            entity_codes.add(entity_code)
        if entity.get("caseKey") != "CaseId":
            errors.append(f"{prefix}.caseKey must be CaseId")
        if not isinstance(entity.get("writeTarget"), str) or not str(
            entity.get("writeTarget")
        ).strip():
            errors.append(f"{prefix}.writeTarget is required")
        fields = entity.get("fields")
        if not isinstance(fields, list) or not fields:
            errors.append(f"{prefix}.fields must be a non-empty list")
            continue
        for field_index, field_contract in enumerate(fields):
            if not isinstance(field_contract, Mapping):
                errors.append(f"{prefix}.fields[{field_index}] must be an object")
                continue
            field_entries.append((f"{prefix}.fields[{field_index}]", field_contract))

    paths: set[str] = set()
    fields_by_path: Dict[str, Mapping[str, Any]] = {}
    for location, field_contract in field_entries:
        path = field_contract.get("path")
        if not isinstance(path, str) or not path:
            errors.append(f"{location}.path is required")
            continue
        parts = path.split(".")
        if len(parts) < 2 or parts[0] not in {"canonical", "extensions"}:
            errors.append(f"{location}.path must begin with canonical. or extensions.")
        elif parts[0] == "canonical" and len(parts) != 2:
            errors.append(f"{location}.path must be canonical.<Field>")
        elif parts[0] == "canonical" and parts[1] not in CANONICAL_CREATION_FIELDS:
            errors.append(
                f"{location}.path is not a canonical case-creation field; "
                "declare solution-specific data under extensions"
            )
        elif parts[0] == "extensions":
            if len(parts) != 3:
                errors.append(f"{location}.path must be extensions.<entityCode>.<Field>")
            elif parts[1] not in entity_codes:
                errors.append(f"{location}.path references unknown extension entity: {parts[1]}")
        if parts[-1] in RESERVED_FIELD_LEAVES:
            errors.append(f"{location}.path declares server-owned field: {parts[-1]}")
        if path in paths:
            errors.append(f"duplicate field path: {path}")
        paths.add(path)
        fields_by_path[path] = field_contract
        _validate_field_contract(location, field_contract, errors)

    for location, field_contract in field_entries:
        condition = field_contract.get("requiredWhen")
        if condition is None:
            continue
        if not isinstance(condition, Mapping):
            continue
        source_path = condition.get("path")
        if source_path not in fields_by_path:
            errors.append(f"{location}.requiredWhen references unknown field: {source_path}")

    evidence_requirements = contract.get("evidenceRequirements", [])
    if not isinstance(evidence_requirements, list):
        errors.append("evidenceRequirements must be a list")
        evidence_requirements = []
    requirement_codes: set[str] = set()
    for index, requirement in enumerate(evidence_requirements):
        location = f"evidenceRequirements[{index}]"
        if not isinstance(requirement, Mapping):
            errors.append(f"{location} must be an object")
            continue
        code = requirement.get("code")
        if not isinstance(code, str) or not CODE_PATTERN.fullmatch(code):
            errors.append(f"{location}.code must be an uppercase stable code")
        elif code in requirement_codes:
            errors.append(f"duplicate evidence requirement code: {code}")
        else:
            requirement_codes.add(code)
        minimum = requirement.get("minimumFiles", 0)
        maximum = requirement.get("maximumFiles")
        if not isinstance(minimum, int) or isinstance(minimum, bool) or minimum < 0:
            errors.append(f"{location}.minimumFiles must be a non-negative integer")
        if maximum is not None and (
            not isinstance(maximum, int)
            or isinstance(maximum, bool)
            or maximum < 1
            or (isinstance(minimum, int) and maximum < minimum)
        ):
            errors.append(f"{location}.maximumFiles must be at least minimumFiles")
        extensions = requirement.get("allowedExtensions", [])
        if not isinstance(extensions, list) or any(
            not isinstance(value, str) or not re.fullmatch(r"[a-z0-9]+", value)
            for value in extensions
        ):
            errors.append(
                f"{location}.allowedExtensions must contain lowercase extensions without dots"
            )
        maximum_size = requirement.get("maximumSizeBytes")
        if maximum_size is not None and (
            not isinstance(maximum_size, int)
            or isinstance(maximum_size, bool)
            or maximum_size < 1
        ):
            errors.append(f"{location}.maximumSizeBytes must be a positive integer")

    submission = contract.get("submission", {"enabled": False})
    if not isinstance(submission, Mapping):
        errors.append("submission must be an object")
    elif submission.get("enabled") is True:
        if not submission.get("commandTypeCode"):
            errors.append("submission.commandTypeCode is required when submission is enabled")
        if not submission.get("readinessRule"):
            errors.append("submission.readinessRule is required when submission is enabled")
    return errors


def _validate_case_type_guidance(
    contract: Mapping[str, Any], errors: List[str]
) -> None:
    description = contract.get("description")
    if (
        not isinstance(description, str)
        or len(description.strip()) < 40
        or len(description.strip()) > 1200
    ):
        errors.append("description must be plain text between 40 and 1200 characters")

    expected_outcome = contract.get("expectedOutcome")
    if (
        not isinstance(expected_outcome, str)
        or len(expected_outcome.strip()) < 20
        or len(expected_outcome.strip()) > 600
    ):
        errors.append(
            "expectedOutcome must be plain text between 20 and 600 characters"
        )

    normalized_lists: Dict[str, set[str]] = {}
    for field_name in GUIDANCE_LIST_FIELDS:
        values = contract.get(field_name)
        if (
            not isinstance(values, list)
            or not 1 <= len(values) <= 10
            or any(
                not isinstance(value, str)
                or len(value.strip()) < 10
                or len(value.strip()) > 300
                for value in values
            )
        ):
            errors.append(
                f"{field_name} must contain 1-10 plain-text criteria, "
                "each between 10 and 300 characters"
            )
            continue
        normalized = {value.strip().casefold() for value in values}
        if len(normalized) != len(values):
            errors.append(f"{field_name} must not contain duplicate criteria")
        normalized_lists[field_name] = normalized

    overlap = normalized_lists.get("useWhen", set()) & normalized_lists.get(
        "doNotUseWhen", set()
    )
    if overlap:
        errors.append("useWhen and doNotUseWhen must not contain the same criterion")


def _validate_field_contract(
    location: str, field_contract: Mapping[str, Any], errors: List[str]
) -> None:
    if not isinstance(field_contract.get("label"), str) or not str(
        field_contract.get("label")
    ).strip():
        errors.append(f"{location}.label is required")
    if not isinstance(field_contract.get("prompt"), str) or not str(
        field_contract.get("prompt")
    ).strip():
        errors.append(f"{location}.prompt is required")
    field_type = field_contract.get("type")
    if field_type not in ALLOWED_FIELD_TYPES:
        errors.append(f"{location}.type is unsupported: {field_type}")
    source = field_contract.get("source", "user")
    if source not in ALLOWED_FIELD_SOURCES:
        errors.append(f"{location}.source is unsupported: {source}")
    if field_contract.get("required") is True and source != "user":
        errors.append(f"{location} cannot be required when source is {source}")
    sensitivity = field_contract.get("sensitivity", "internal")
    if sensitivity not in ALLOWED_SENSITIVITY:
        errors.append(f"{location}.sensitivity is unsupported: {sensitivity}")
    minimum_length = field_contract.get("minimumLength")
    maximum_length = field_contract.get("maximumLength")
    if minimum_length is not None and (
        not isinstance(minimum_length, int)
        or isinstance(minimum_length, bool)
        or minimum_length < 0
    ):
        errors.append(f"{location}.minimumLength must be a non-negative integer")
    if maximum_length is not None and (
        not isinstance(maximum_length, int)
        or isinstance(maximum_length, bool)
        or maximum_length < 1
    ):
        errors.append(f"{location}.maximumLength must be a positive integer")
    if (
        isinstance(minimum_length, int)
        and isinstance(maximum_length, int)
        and minimum_length > maximum_length
    ):
        errors.append(f"{location}.minimumLength cannot exceed maximumLength")
    if field_type != "string" and any(
        key in field_contract for key in ("minimumLength", "maximumLength", "pattern")
    ):
        errors.append(f"{location} string constraints require type string")
    pattern = field_contract.get("pattern")
    if pattern is not None:
        try:
            re.compile(str(pattern))
        except re.error as exc:
            errors.append(f"{location}.pattern is invalid: {exc}")
    minimum = field_contract.get("minimum")
    maximum = field_contract.get("maximum")
    if any(value is not None and (not isinstance(value, (int, float)) or isinstance(value, bool))
           for value in (minimum, maximum)):
        errors.append(f"{location}.minimum and maximum must be numeric")
    if (
        isinstance(minimum, (int, float))
        and not isinstance(minimum, bool)
        and isinstance(maximum, (int, float))
        and not isinstance(maximum, bool)
        and minimum > maximum
    ):
        errors.append(f"{location}.minimum cannot exceed maximum")
    if field_type not in {"integer", "number"} and any(
        key in field_contract for key in ("minimum", "maximum")
    ):
        errors.append(f"{location} numeric constraints require type integer or number")
    minimum_items = field_contract.get("minimumItems")
    maximum_items = field_contract.get("maximumItems")
    if field_type == "array":
        item_type = field_contract.get("itemType")
        if item_type not in {"string", "integer", "number", "boolean"}:
            errors.append(
                f"{location}.itemType must be string, integer, number, or boolean"
            )
        if minimum_items is not None and (
            not isinstance(minimum_items, int)
            or isinstance(minimum_items, bool)
            or minimum_items < 0
        ):
            errors.append(f"{location}.minimumItems must be a non-negative integer")
        if maximum_items is not None and (
            not isinstance(maximum_items, int)
            or isinstance(maximum_items, bool)
            or maximum_items < 1
        ):
            errors.append(f"{location}.maximumItems must be a positive integer")
        if (
            isinstance(minimum_items, int)
            and isinstance(maximum_items, int)
            and minimum_items > maximum_items
        ):
            errors.append(f"{location}.minimumItems cannot exceed maximumItems")
        item_enum = field_contract.get("itemEnum")
        if item_enum is not None and (
            not isinstance(item_enum, list)
            or not item_enum
            or len({_json_key(value) for value in item_enum}) != len(item_enum)
        ):
            errors.append(f"{location}.itemEnum must be a non-empty unique list")
    elif minimum_items is not None or maximum_items is not None:
        errors.append(f"{location} item constraints require type array")
    elif "itemType" in field_contract or "itemEnum" in field_contract:
        errors.append(f"{location} item declarations require type array")
    enum = field_contract.get("enum")
    lookup_source = field_contract.get("lookupSource")
    if enum is not None and (
        not isinstance(enum, list) or not enum or len({_json_key(v) for v in enum}) != len(enum)
    ):
        errors.append(f"{location}.enum must be a non-empty unique list")
    if enum is not None and lookup_source:
        errors.append(f"{location} cannot declare both enum and lookupSource")
    if field_type == "array" and lookup_source:
        errors.append(f"{location} array lookups are not supported by this contract version")
    if lookup_source is not None and (
        not isinstance(lookup_source, str) or not lookup_source.strip()
    ):
        errors.append(f"{location}.lookupSource must be a stable lookup code")
    condition = field_contract.get("requiredWhen")
    if condition is not None:
        if not isinstance(condition, Mapping):
            errors.append(f"{location}.requiredWhen must be an object")
        else:
            keys = set(condition)
            if not condition.get("path"):
                errors.append(f"{location}.requiredWhen.path is required")
            operators = keys & {"equals", "in", "isTrue"}
            if len(operators) != 1:
                errors.append(
                    f"{location}.requiredWhen must declare exactly one of equals, in, or isTrue"
                )
            if "in" in condition and (
                not isinstance(condition["in"], list) or not condition["in"]
            ):
                errors.append(f"{location}.requiredWhen.in must be a non-empty list")
            if "isTrue" in condition and condition["isTrue"] is not True:
                errors.append(f"{location}.requiredWhen.isTrue must be true")


def validate_case_intake(
    contract: Mapping[str, Any],
    envelope: Mapping[str, Any],
    principal_id: str,
    lookup_provider: Optional[LookupProvider] = None,
    file_handle_provider: Optional[FileHandleProvider] = None,
) -> List[ValidationIssue]:
    contract_errors = validate_creation_contract(contract)
    if contract_errors:
        return [
            ValidationIssue("$contract", "invalid_contract", message)
            for message in contract_errors
        ]
    issues: List[ValidationIssue] = []
    unexpected_top = set(envelope) - ALLOWED_TOP_LEVEL_DRAFT_KEYS
    for key in sorted(unexpected_top):
        issues.append(
            ValidationIssue(key, "unknown_property", "Property is not accepted by the case intake.")
        )
    if envelope.get("caseTypeCode") != contract.get("caseTypeCode"):
        issues.append(
            ValidationIssue(
                "caseTypeCode",
                "case_type_mismatch",
                "Draft case type does not match the creation contract.",
            )
        )
    if envelope.get("contractVersion") != contract.get("contractVersion"):
        issues.append(
            ValidationIssue(
                "contractVersion",
                "contract_version_mismatch",
                "Draft contract version does not match the creation contract.",
            )
        )
    canonical = envelope.get("canonical")
    extensions = envelope.get("extensions")
    if not isinstance(canonical, Mapping):
        issues.append(
            ValidationIssue("canonical", "invalid_type", "canonical must be an object.")
        )
        canonical = {}
    if not isinstance(extensions, Mapping):
        issues.append(
            ValidationIssue("extensions", "invalid_type", "extensions must be an object.")
        )
        extensions = {}

    fields = _field_index(contract)
    declared_paths = set(fields)
    actual_paths = set(_leaf_paths({"canonical": canonical, "extensions": extensions}))
    for path in sorted(actual_paths - declared_paths):
        issues.append(
            ValidationIssue(
                path,
                "unknown_field",
                "Field is not declared by the versioned case creation contract.",
            )
        )
    declared_entities = {
        str(value["entityCode"]) for value in contract.get("extensionEntities", [])
    }
    for entity_code in sorted(set(extensions) - declared_entities):
        path = f"extensions.{entity_code}"
        if not any(issue.path.startswith(path + ".") for issue in issues):
            issues.append(
                ValidationIssue(
                    path,
                    "unknown_extension",
                    "Extension entity is not declared by the creation contract.",
                )
            )

    root = {"canonical": canonical, "extensions": extensions}
    for path, field_contract in fields.items():
        if field_contract.get("source", "user") != "user":
            if _has_path(root, path):
                issues.append(
                    ValidationIssue(
                        path,
                        "server_owned",
                        "Server-owned or derived fields cannot be supplied by the caller.",
                    )
                )
            continue
        present = _has_path(root, path)
        value = _get_path(root, path)
        required = field_contract.get("required") is True or _required_when(
            root, field_contract.get("requiredWhen")
        )
        if required and _is_empty(value, present):
            issues.append(
                ValidationIssue(path, "required", f"{field_contract['label']} is required.")
            )
            continue
        if _is_empty(value, present):
            continue
        issues.extend(
            _validate_field_value(
                path,
                value,
                field_contract,
                principal_id,
                lookup_provider,
            )
        )

    issues.extend(
        _validate_files(
            contract,
            envelope.get("fileHandles", []),
            principal_id,
            file_handle_provider,
        )
    )
    return issues


def _validate_field_value(
    path: str,
    value: Any,
    field_contract: Mapping[str, Any],
    principal_id: str,
    lookup_provider: Optional[LookupProvider],
) -> List[ValidationIssue]:
    issues: List[ValidationIssue] = []
    field_type = field_contract["type"]
    valid_type = True
    if field_type == "string":
        valid_type = isinstance(value, str)
    elif field_type == "integer":
        valid_type = isinstance(value, int) and not isinstance(value, bool)
    elif field_type == "number":
        valid_type = isinstance(value, (int, float)) and not isinstance(value, bool)
    elif field_type == "boolean":
        valid_type = isinstance(value, bool)
    elif field_type == "array":
        valid_type = isinstance(value, list)
    elif field_type == "date":
        valid_type = isinstance(value, str) and _is_iso_date(value)
    elif field_type == "datetime":
        valid_type = isinstance(value, str) and _is_iso_datetime(value)
    if not valid_type:
        return [
            ValidationIssue(
                path,
                "invalid_type",
                f"{field_contract['label']} must be a valid {field_type}.",
            )
        ]
    if field_type == "string":
        minimum = field_contract.get("minimumLength")
        maximum = field_contract.get("maximumLength")
        if minimum is not None and len(value) < minimum:
            issues.append(
                ValidationIssue(
                    path,
                    "minimum_length",
                    f"{field_contract['label']} must contain at least {minimum} characters.",
                )
            )
        if maximum is not None and len(value) > maximum:
            issues.append(
                ValidationIssue(
                    path,
                    "maximum_length",
                    f"{field_contract['label']} cannot exceed {maximum} characters.",
                )
            )
        pattern = field_contract.get("pattern")
        if pattern and re.fullmatch(str(pattern), value) is None:
            issues.append(
                ValidationIssue(
                    path,
                    "pattern",
                    f"{field_contract['label']} has an invalid format.",
                )
            )
    if field_type in {"integer", "number"}:
        minimum = field_contract.get("minimum")
        maximum = field_contract.get("maximum")
        if minimum is not None and value < minimum:
            issues.append(
                ValidationIssue(
                    path,
                    "minimum",
                    f"{field_contract['label']} cannot be less than {minimum}.",
                )
            )
        if maximum is not None and value > maximum:
            issues.append(
                ValidationIssue(
                    path,
                    "maximum",
                    f"{field_contract['label']} cannot exceed {maximum}.",
                )
            )
    if field_type == "array":
        minimum_items = field_contract.get("minimumItems")
        maximum_items = field_contract.get("maximumItems")
        if minimum_items is not None and len(value) < minimum_items:
            issues.append(
                ValidationIssue(
                    path,
                    "minimum_items",
                    f"{field_contract['label']} requires at least {minimum_items} item(s).",
                )
            )
        if maximum_items is not None and len(value) > maximum_items:
            issues.append(
                ValidationIssue(
                    path,
                    "maximum_items",
                    f"{field_contract['label']} cannot exceed {maximum_items} item(s).",
                )
            )
        item_type = field_contract.get("itemType")
        item_enum = field_contract.get("itemEnum")
        for index, item in enumerate(value):
            if not _matches_primitive_type(item, item_type):
                issues.append(
                    ValidationIssue(
                        f"{path}[{index}]",
                        "invalid_item_type",
                        f"{field_contract['label']} contains an invalid {item_type} item.",
                    )
                )
            elif item_enum is not None and item not in item_enum:
                issues.append(
                    ValidationIssue(
                        f"{path}[{index}]",
                        "item_not_allowed",
                        f"{field_contract['label']} contains a value that is not allowed.",
                    )
                )
    enum = field_contract.get("enum")
    if enum is not None and value not in enum:
        issues.append(
            ValidationIssue(
                path,
                "not_allowed",
                f"{field_contract['label']} is not an allowed value.",
            )
        )
    lookup_source = field_contract.get("lookupSource")
    if lookup_source:
        if lookup_provider is None:
            issues.append(
                ValidationIssue(
                    path,
                    "lookup_unavailable",
                    f"{field_contract['label']} could not be verified against its governed lookup.",
                )
            )
        elif not lookup_provider.contains(str(lookup_source), value, principal_id):
            issues.append(
                ValidationIssue(
                    path,
                    "lookup_value_invalid",
                    f"{field_contract['label']} is not active or permitted.",
                )
            )
    return issues


def _validate_files(
    contract: Mapping[str, Any],
    handles: Any,
    principal_id: str,
    provider: Optional[FileHandleProvider],
) -> List[ValidationIssue]:
    issues: List[ValidationIssue] = []
    if not isinstance(handles, list):
        return [
            ValidationIssue("fileHandles", "invalid_type", "fileHandles must be a list.")
        ]
    requirements = {
        str(value["code"]): value for value in contract.get("evidenceRequirements", [])
    }
    counts = {code: 0 for code in requirements}
    seen_handles: set[str] = set()
    for index, item in enumerate(handles):
        path = f"fileHandles[{index}]"
        if not isinstance(item, Mapping):
            issues.append(ValidationIssue(path, "invalid_type", "File handle must be an object."))
            continue
        unexpected = set(item) - {"handle", "requirementCode"}
        if unexpected:
            issues.append(
                ValidationIssue(
                    path,
                    "unknown_property",
                    "File handle contains unsupported properties.",
                )
            )
        handle = item.get("handle")
        requirement_code = item.get("requirementCode")
        if not isinstance(handle, str) or not handle:
            issues.append(
                ValidationIssue(path + ".handle", "required", "An opaque file handle is required.")
            )
            continue
        if handle in seen_handles:
            issues.append(
                ValidationIssue(path + ".handle", "duplicate", "File handle is duplicated.")
            )
            continue
        seen_handles.add(handle)
        requirement = requirements.get(str(requirement_code))
        if requirement is None:
            issues.append(
                ValidationIssue(
                    path + ".requirementCode",
                    "unknown_evidence_requirement",
                    "Evidence requirement is not declared by the creation contract.",
                )
            )
            continue
        counts[str(requirement_code)] += 1
        if provider is None:
            issues.append(
                ValidationIssue(
                    path + ".handle",
                    "file_provider_unavailable",
                    "Staged file ownership and metadata could not be verified.",
                )
            )
            continue
        metadata = provider.resolve(handle, principal_id)
        if metadata is None:
            issues.append(
                ValidationIssue(
                    path + ".handle",
                    "file_handle_invalid",
                    "Staged file handle is missing, expired, or belongs to another principal.",
                )
            )
            continue
        file_name = str(metadata.get("fileName", ""))
        extension = file_name.rsplit(".", 1)[-1].lower() if "." in file_name else ""
        allowed_extensions = requirement.get("allowedExtensions", [])
        if allowed_extensions and extension not in allowed_extensions:
            issues.append(
                ValidationIssue(
                    path + ".handle",
                    "file_extension_invalid",
                    "Staged file type is not allowed for this evidence requirement.",
                )
            )
        maximum_size = requirement.get("maximumSizeBytes")
        if maximum_size is not None and int(metadata.get("sizeBytes", 0)) > maximum_size:
            issues.append(
                ValidationIssue(
                    path + ".handle",
                    "file_too_large",
                    "Staged file exceeds the permitted size.",
                )
            )
        if requirement.get("requiresCleanScan", True) and metadata.get("scanStatus") != "CLEAN":
            issues.append(
                ValidationIssue(
                    path + ".handle",
                    "file_not_clean",
                    "Staged file has not passed the required malware scan.",
                )
            )
    for code, requirement in requirements.items():
        minimum = int(requirement.get("minimumFiles", 0))
        maximum = requirement.get("maximumFiles")
        if counts[code] < minimum:
            issues.append(
                ValidationIssue(
                    f"fileHandles.{code}",
                    "minimum_files",
                    f"Evidence requirement {code} requires at least {minimum} file(s).",
                )
            )
        if maximum is not None and counts[code] > int(maximum):
            issues.append(
                ValidationIssue(
                    f"fileHandles.{code}",
                    "maximum_files",
                    f"Evidence requirement {code} allows at most {maximum} file(s).",
                )
            )
    return issues


def _field_index(contract: Mapping[str, Any]) -> Dict[str, Mapping[str, Any]]:
    values: Dict[str, Mapping[str, Any]] = {}
    for field_contract in contract.get("canonicalFields", []):
        values[str(field_contract["path"])] = field_contract
    for entity in contract.get("extensionEntities", []):
        for field_contract in entity.get("fields", []):
            values[str(field_contract["path"])] = field_contract
    return values


def _leaf_paths(value: Any, prefix: str = "") -> Iterable[str]:
    if isinstance(value, Mapping):
        for key, child in value.items():
            child_path = f"{prefix}.{key}" if prefix else str(key)
            if isinstance(child, Mapping):
                yield from _leaf_paths(child, child_path)
            else:
                yield child_path


def _get_path(root: Mapping[str, Any], path: str) -> Any:
    current: Any = root
    for segment in path.split("."):
        if not isinstance(current, Mapping) or segment not in current:
            return None
        current = current[segment]
    return current


def _has_path(root: Mapping[str, Any], path: str) -> bool:
    current: Any = root
    for segment in path.split("."):
        if not isinstance(current, Mapping) or segment not in current:
            return False
        current = current[segment]
    return True


def _set_path(root: Dict[str, Any], path: str, value: Any) -> None:
    current = root
    parts = path.split(".")
    for segment in parts[:-1]:
        child = current.get(segment)
        if not isinstance(child, dict):
            child = {}
            current[segment] = child
        current = child
    current[parts[-1]] = value


def _required_when(root: Mapping[str, Any], condition: Any) -> bool:
    if not isinstance(condition, Mapping):
        return False
    value = _get_path(root, str(condition.get("path", "")))
    if "equals" in condition:
        return value == condition["equals"]
    if "in" in condition:
        return value in condition["in"]
    if "isTrue" in condition:
        return value is True
    return False


def _is_empty(value: Any, present: bool) -> bool:
    if not present or value is None:
        return True
    return isinstance(value, str) and not value.strip()


def _is_iso_date(value: str) -> bool:
    try:
        date.fromisoformat(value)
        return "T" not in value
    except ValueError:
        return False


def _is_iso_datetime(value: str) -> bool:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return parsed.tzinfo is not None
    except ValueError:
        return False


def _matches_primitive_type(value: Any, expected: Any) -> bool:
    if expected == "string":
        return isinstance(value, str)
    if expected == "integer":
        return isinstance(value, int) and not isinstance(value, bool)
    if expected == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    if expected == "boolean":
        return isinstance(value, bool)
    return False


def _stable_digest(value: Mapping[str, Any]) -> str:
    payload = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def _json_key(value: Any) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _print_issues(issues: Sequence[ValidationIssue]) -> None:
    for issue in issues:
        print(
            json.dumps(issue.to_dict(), ensure_ascii=False, separators=(",", ":")),
            file=sys.stderr,
        )


def _selftest() -> int:
    contract = {
        "schemaVersion": 2,
        "caseTypeCode": "SELFTEST",
        "name": "Self-test case",
        "description": (
            "Use this governed self-test case to verify that the reusable intake "
            "contract accepts a clearly defined case purpose."
        ),
        "useWhen": [
            "Use it when validating the transport-neutral case-agent framework."
        ],
        "doNotUseWhen": [
            "Do not use it to represent a production business case or live record."
        ],
        "expectedOutcome": (
            "The framework validates and creates a controlled synthetic case snapshot."
        ),
        "contractVersion": 1,
        "creationMode": "deferredMaterialization",
        "creationAdapter": "SELFTEST.Create",
        "canonicalFields": [
            {
                "path": "canonical.Title",
                "label": "Title",
                "prompt": "What should this case be called?",
                "type": "string",
                "required": True,
                "minimumLength": 3,
                "maximumLength": 100,
            }
        ],
        "extensionEntities": [],
        "evidenceRequirements": [],
        "submission": {"enabled": False},
    }
    errors = validate_creation_contract(contract)
    if errors:
        print("SELFTEST FAILED: " + "; ".join(errors), file=sys.stderr)
        return 1
    envelope = {
        "caseTypeCode": "SELFTEST",
        "contractVersion": 1,
        "canonical": {"Title": "A valid case"},
        "extensions": {},
        "fileHandles": [],
    }
    issues = validate_case_intake(contract, envelope, principal_id="SELFTEST\\user")
    if issues:
        _print_issues(issues)
        print("SELFTEST FAILED: valid intake was rejected", file=sys.stderr)
        return 1
    envelope["canonical"]["Title"] = "x"
    issues = validate_case_intake(contract, envelope, principal_id="SELFTEST\\user")
    if not any(issue.code == "minimum_length" for issue in issues):
        print("SELFTEST FAILED: minimum length was not enforced", file=sys.stderr)
        return 1
    print(
        "SELFTEST SUCCEEDED: versioned creation contracts, extension-aware intake "
        "validation, and transport-neutral agent boundaries"
    )
    return 0


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    validate_contract_parser = subparsers.add_parser(
        "validate-contract", help="Validate a case creation contract."
    )
    validate_contract_parser.add_argument("contract", type=Path)
    validate_draft_parser = subparsers.add_parser(
        "validate-draft", help="Validate a draft envelope without external providers."
    )
    validate_draft_parser.add_argument("contract", type=Path)
    validate_draft_parser.add_argument("draft", type=Path)
    subparsers.add_parser("selftest", help="Run dependency-free foundation checks.")
    args = parser.parse_args(argv)
    if args.command == "selftest":
        return _selftest()
    try:
        contract = load_document(args.contract)
    except (OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    errors = validate_creation_contract(contract)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print(f"Validation failed with {len(errors)} error(s).", file=sys.stderr)
        return 1
    if args.command == "validate-contract":
        print(
            f"Valid case creation contract: {args.contract} "
            f"({contract['caseTypeCode']} version {contract['contractVersion']})"
        )
        return 0
    try:
        draft = load_document(args.draft)
    except (OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    issues = validate_case_intake(contract, draft, principal_id="CLI")
    if issues:
        _print_issues(issues)
        print(f"Validation failed with {len(issues)} issue(s).", file=sys.stderr)
        return 1
    print(f"Valid case intake draft: {args.draft}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
