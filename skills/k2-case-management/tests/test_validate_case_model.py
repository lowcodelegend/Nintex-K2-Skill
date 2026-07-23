import copy
import json
import unittest
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from validate_case_model import load_document, validate  # noqa: E402


class CaseModelValidatorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.valid = load_document(ROOT / "assets" / "case-type-definition.yaml")

    def test_shipped_template_is_valid(self):
        self.assertEqual([], validate(self.valid))

    def test_rejects_unknown_destination_and_unreachable_stage(self):
        value = copy.deepcopy(self.valid)
        value["transitions"][0]["to"] = "MISSING"
        errors = validate(value)
        self.assertTrue(any("destination does not exist" in error for error in errors))
        self.assertTrue(any("unreachable stage" in error for error in errors))

    def test_rejects_backward_transition_without_reentry(self):
        value = copy.deepcopy(self.valid)
        value["transitions"][2].pop("reentry")
        self.assertTrue(any("must set reentry" in error for error in validate(value)))

    def test_rejects_transition_from_terminal_without_reopen(self):
        value = copy.deepcopy(self.valid)
        value["transitions"].append({"from": "CLOSE", "outcome": "REOPENED", "to": "INVESTIGATE", "reentry": True})
        self.assertTrue(any("not marked reopen" in error for error in validate(value)))


if __name__ == "__main__":
    unittest.main()
