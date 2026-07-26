import copy
import unittest
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from case_agent_framework import (  # noqa: E402
    AdapterError,
    AuthorizationError,
    CaseAgentFramework,
    ConfirmationError,
    ContractRegistry,
    DraftConflictError,
    ValidationFailedError,
    load_document,
    validate_case_intake,
    validate_creation_contract,
)


class Authorization:
    def can_create(self, principal_id, case_type_code):
        return principal_id == r"EXAMPLE\alice" and case_type_code == "EVIDENCE_EXCEPTION"


class Lookups:
    values = {
        "CONFIDENTIALITY": {"INTERNAL", "RESTRICTED"},
        "EXCEPTION_TYPE": {"PROCESS", "EVIDENCE"},
    }

    def contains(self, source_code, value, principal_id):
        return principal_id == r"EXAMPLE\alice" and value in self.values.get(source_code, set())


class Files:
    def resolve(self, handle, principal_id):
        if handle != "file-clean" or principal_id != r"EXAMPLE\alice":
            return None
        return {
            "fileName": "evidence.pdf",
            "contentType": "application/pdf",
            "sizeBytes": 1024,
            "scanStatus": "CLEAN",
        }


class RecordingAdapter:
    def __init__(self, submitted=False):
        self.calls = []
        self.submitted = submitted

    def create(self, request):
        self.calls.append(request)
        return {
            "caseId": 42,
            "caseNumber": "CASE-000042",
            "submitted": self.submitted,
        }


class CaseAgentFrameworkTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.contract = load_document(ROOT / "assets" / "case-agent-creation-contract.yaml")

    def framework(self, adapter=None):
        registry = ContractRegistry()
        registry.register(self.contract)
        framework = CaseAgentFramework(
            registry,
            Authorization(),
            lookup_provider=Lookups(),
            file_handle_provider=Files(),
        )
        adapter = adapter or RecordingAdapter()
        framework.register_adapter(self.contract["creationAdapter"], adapter)
        return framework, adapter

    @staticmethod
    def valid_values():
        return {
            "canonical.Title": "A complete exception case",
            "canonical.Description": (
                "This description contains enough detail for a governed validation test."
            ),
            "canonical.ConfidentialityCode": "INTERNAL",
            "extensions.exception.ExceptionTypeCode": "PROCESS",
            "extensions.exception.OccurredDate": "2026-07-26",
            "extensions.exception.ExternalReference": "EXT-100/1",
            "extensions.exception.ContainsPersonalData": True,
            "extensions.exception.PersonalDataReason": (
                "Personal data is needed to identify the affected record."
            ),
        }

    def test_shipped_creation_contract_is_valid(self):
        self.assertEqual([], validate_creation_contract(self.contract))

    def test_contract_rejects_duplicate_unknown_and_unsafe_fields(self):
        value = copy.deepcopy(self.contract)
        value["canonicalFields"][0]["path"] = "canonical.CaseId"
        value["canonicalFields"][1]["path"] = "canonical.CaseId"
        value["canonicalFields"][2]["path"] = "canonical.SolutionSpecificValue"
        value["creationMode"] = "earlyCanonicalDraft"
        value["extensionEntities"][0]["fields"][4]["requiredWhen"]["path"] = (
            "extensions.exception.Missing"
        )
        errors = validate_creation_contract(value)
        self.assertTrue(any("server-owned field" in error for error in errors))
        self.assertTrue(any("duplicate field path" in error for error in errors))
        self.assertTrue(any("declare solution-specific data under extensions" in error for error in errors))
        self.assertTrue(any("creationMode must be deferredMaterialization" in error for error in errors))
        self.assertTrue(any("references unknown field" in error for error in errors))

    def test_validation_enforces_extensions_conditions_lookups_and_unknown_fields(self):
        draft = {
            "caseTypeCode": "EVIDENCE_EXCEPTION",
            "contractVersion": 1,
            "canonical": {
                "Title": "Bad",
                "Description": "short",
                "ConfidentialityCode": "NOT_ALLOWED",
                "Unexpected": "not accepted",
            },
            "extensions": {
                "exception": {
                    "ExceptionTypeCode": "PROCESS",
                    "OccurredDate": "not-a-date",
                    "ContainsPersonalData": True,
                }
            },
            "fileHandles": [],
        }
        issues = validate_case_intake(
            self.contract,
            draft,
            principal_id=r"EXAMPLE\alice",
            lookup_provider=Lookups(),
            file_handle_provider=Files(),
        )
        codes = {issue.code for issue in issues}
        paths = {issue.path for issue in issues}
        self.assertIn("minimum_length", codes)
        self.assertIn("lookup_value_invalid", codes)
        self.assertIn("invalid_type", codes)
        self.assertIn("required", codes)
        self.assertIn("unknown_field", codes)
        self.assertIn("canonical.Unexpected", paths)
        self.assertIn("extensions.exception.PersonalDataReason", paths)

    def test_array_fields_require_typed_allowlisted_items(self):
        contract = copy.deepcopy(self.contract)
        contract["extensionEntities"][0]["fields"].append(
            {
                "path": "extensions.exception.Tags",
                "label": "Tags",
                "prompt": "Which governed tags apply?",
                "type": "array",
                "required": False,
                "itemType": "string",
                "itemEnum": ["SAFETY", "QUALITY"],
                "maximumItems": 2,
            }
        )
        self.assertEqual([], validate_creation_contract(contract))
        draft = {
            "caseTypeCode": "EVIDENCE_EXCEPTION",
            "contractVersion": 1,
            "canonical": {
                "Title": "A complete exception case",
                "Description": (
                    "This description contains enough detail for a governed validation test."
                ),
                "ConfidentialityCode": "INTERNAL",
            },
            "extensions": {
                "exception": {
                    "ExceptionTypeCode": "PROCESS",
                    "OccurredDate": "2026-07-26",
                    "ContainsPersonalData": False,
                    "Tags": ["SAFETY", "HALLUCINATED", 7],
                }
            },
            "fileHandles": [],
        }
        issues = validate_case_intake(
            contract,
            draft,
            principal_id=r"EXAMPLE\alice",
            lookup_provider=Lookups(),
            file_handle_provider=Files(),
        )
        self.assertTrue(any(issue.code == "maximum_items" for issue in issues))
        self.assertTrue(any(issue.code == "item_not_allowed" for issue in issues))
        self.assertTrue(any(issue.code == "invalid_item_type" for issue in issues))

    def test_file_handles_are_opaque_owned_and_scan_checked(self):
        draft = {
            "caseTypeCode": "EVIDENCE_EXCEPTION",
            "contractVersion": 1,
            "canonical": {
                "Title": "A complete exception case",
                "Description": (
                    "This description contains enough detail for a governed validation test."
                ),
                "ConfidentialityCode": "INTERNAL",
            },
            "extensions": {
                "exception": {
                    "ExceptionTypeCode": "PROCESS",
                    "OccurredDate": "2026-07-26",
                    "ContainsPersonalData": False,
                }
            },
            "fileHandles": [
                {
                    "handle": "not-owned",
                    "requirementCode": "SUPPORTING_EVIDENCE",
                }
            ],
        }
        issues = validate_case_intake(
            self.contract,
            draft,
            principal_id=r"EXAMPLE\alice",
            lookup_provider=Lookups(),
            file_handle_provider=Files(),
        )
        self.assertTrue(any(issue.code == "file_handle_invalid" for issue in issues))
        draft["fileHandles"][0]["handle"] = "file-clean"
        issues = validate_case_intake(
            self.contract,
            draft,
            principal_id=r"EXAMPLE\alice",
            lookup_provider=Lookups(),
            file_handle_provider=Files(),
        )
        self.assertFalse(any(issue.path.startswith("fileHandles") for issue in issues))

    def test_discovery_hides_write_targets_and_enforces_authorization(self):
        framework, _ = self.framework()
        permitted = framework.list_permitted_case_types(r"EXAMPLE\alice")
        self.assertEqual(["EVIDENCE_EXCEPTION"], [value["caseTypeCode"] for value in permitted])
        public_contract = framework.get_case_creation_contract(
            r"EXAMPLE\alice", "EVIDENCE_EXCEPTION"
        )
        self.assertNotIn("creationAdapter", public_contract)
        self.assertNotIn("writeTarget", public_contract["extensionEntities"][0])
        with self.assertRaises(AuthorizationError):
            framework.start_case_intake(r"EXAMPLE\bob", "EVIDENCE_EXCEPTION")

    def test_preview_is_bound_to_principal_revision_and_exact_snapshot(self):
        framework, _ = self.framework()
        draft = framework.start_case_intake(r"EXAMPLE\alice", "EVIDENCE_EXCEPTION")
        draft = framework.update_case_intake(
            r"EXAMPLE\alice", draft["draftId"], self.valid_values(), draft["revision"]
        )
        preview = framework.preview_case_creation(r"EXAMPLE\alice", draft["draftId"])
        draft = framework.update_case_intake(
            r"EXAMPLE\alice",
            draft["draftId"],
            {"canonical.Title": "A changed exception title"},
            draft["revision"],
        )
        with self.assertRaises(ConfirmationError):
            framework.create_case(
                r"EXAMPLE\alice",
                draft["draftId"],
                preview["confirmationToken"],
                "idem-preview",
            )
        with self.assertRaises(AuthorizationError):
            framework.get_intake_validation(r"EXAMPLE\bob", draft["draftId"])

    def test_create_dispatches_once_with_server_principal_and_does_not_submit(self):
        framework, adapter = self.framework()
        draft = framework.start_case_intake(r"EXAMPLE\alice", "EVIDENCE_EXCEPTION")
        draft = framework.update_case_intake(
            r"EXAMPLE\alice", draft["draftId"], self.valid_values(), draft["revision"]
        )
        preview = framework.preview_case_creation(r"EXAMPLE\alice", draft["draftId"])
        result = framework.create_case(
            r"EXAMPLE\alice",
            draft["draftId"],
            preview["confirmationToken"],
            "idem-create",
            correlation_id="f531f126-1f8d-4f1c-9fa5-b46afbd5ab03",
        )
        repeated = framework.create_case(
            r"EXAMPLE\alice",
            draft["draftId"],
            preview["confirmationToken"],
            "idem-create",
        )
        self.assertEqual(result, repeated)
        self.assertFalse(result["submitted"])
        self.assertEqual(1, len(adapter.calls))
        self.assertEqual(r"EXAMPLE\alice", adapter.calls[0].principal_id)
        self.assertNotIn("RequestedByFQN", adapter.calls[0].canonical)

    def test_create_rejects_invalid_draft_and_unregistered_adapter(self):
        framework, _ = self.framework()
        draft = framework.start_case_intake(r"EXAMPLE\alice", "EVIDENCE_EXCEPTION")
        with self.assertRaises(ValidationFailedError):
            framework.preview_case_creation(r"EXAMPLE\alice", draft["draftId"])

        registry = ContractRegistry()
        registry.register(self.contract)
        framework = CaseAgentFramework(
            registry,
            Authorization(),
            lookup_provider=Lookups(),
            file_handle_provider=Files(),
        )
        draft = framework.start_case_intake(r"EXAMPLE\alice", "EVIDENCE_EXCEPTION")
        draft = framework.update_case_intake(
            r"EXAMPLE\alice", draft["draftId"], self.valid_values(), draft["revision"]
        )
        preview = framework.preview_case_creation(r"EXAMPLE\alice", draft["draftId"])
        with self.assertRaises(AdapterError):
            framework.create_case(
                r"EXAMPLE\alice",
                draft["draftId"],
                preview["confirmationToken"],
                "idem-no-adapter",
            )

    def test_creation_adapter_cannot_collapse_create_and_submit(self):
        framework, _ = self.framework(RecordingAdapter(submitted=True))
        draft = framework.start_case_intake(r"EXAMPLE\alice", "EVIDENCE_EXCEPTION")
        draft = framework.update_case_intake(
            r"EXAMPLE\alice", draft["draftId"], self.valid_values(), draft["revision"]
        )
        preview = framework.preview_case_creation(r"EXAMPLE\alice", draft["draftId"])
        with self.assertRaises(AdapterError):
            framework.create_case(
                r"EXAMPLE\alice",
                draft["draftId"],
                preview["confirmationToken"],
                "idem-submit",
            )

    def test_update_rejects_unknown_fields_and_stale_revisions(self):
        framework, _ = self.framework()
        draft = framework.start_case_intake(r"EXAMPLE\alice", "EVIDENCE_EXCEPTION")
        with self.assertRaises(DraftConflictError):
            framework.update_case_intake(
                r"EXAMPLE\alice",
                draft["draftId"],
                {"extensions.exception.HallucinatedField": "value"},
                draft["revision"],
            )
        draft = framework.update_case_intake(
            r"EXAMPLE\alice",
            draft["draftId"],
            {"canonical.Title": "A valid title"},
            draft["revision"],
        )
        with self.assertRaises(DraftConflictError):
            framework.update_case_intake(
                r"EXAMPLE\alice",
                draft["draftId"],
                {"canonical.Title": "Another valid title"},
                draft["revision"] - 1,
            )


if __name__ == "__main__":
    unittest.main()
