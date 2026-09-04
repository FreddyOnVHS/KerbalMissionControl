from __future__ import annotations

import hashlib
import tempfile
import unittest
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
TOOL_ROOT = HERE.parent
sys.path.insert(0, str(TOOL_ROOT))

import iva_coverage_audit as audit

FIXTURES = HERE / "fixtures"


class IvaCoverageAuditTests(unittest.TestCase):
    def test_parse_cfg_counts_duplicate_props_and_ignores_nested_name(self):
        result = audit.parse_cfg_file(FIXTURES / "target" / "TestCockpit.cfg")
        self.assertEqual(result.internal_name, "TestInternal")
        self.assertEqual(result.prop_instances, ["KnownMFD", "KnownMFD", "MechanicalHandle", "UnknownLamp"])

    def test_parse_cfg_ivas_handles_multiple_internal_nodes_in_one_file(self):
        results = audit.parse_cfg_ivas(FIXTURES / "multi" / "MultiInternal.cfg")
        self.assertEqual([r.internal_name for r in results], ["MultiA", "MultiB"])
        self.assertEqual(results[1].prop_instances, ["OtherUnknown", "OtherUnknown"])

    def test_discover_supported_props_from_kmc_prop_selectors(self):
        supported = audit.discover_supported_props(FIXTURES / "kmc_iva")
        self.assertEqual(supported, {"KnownGauge", "KnownMFD"})

    def test_classifier_is_conservative(self):
        supported = {"KnownMFD"}
        ignored = {"MechanicalHandle"}
        self.assertEqual(audit.classify_prop("KnownMFD", supported, ignored), "SUPPORTED")
        self.assertEqual(audit.classify_prop("MechanicalHandle", supported, ignored), "IGNORE")
        self.assertEqual(audit.classify_prop("UnknownLamp", supported, ignored), "REVIEW")

    def test_audit_rows_are_deterministic_and_percent_excludes_ignored(self):
        rows = audit.audit_roots(
            FIXTURES / "kmc_iva",
            [FIXTURES / "target"],
            ignored={"MechanicalHandle"},
        )
        self.assertEqual([r["iva_internal"] for r in rows], ["SecondInternal", "TestInternal"])
        row = next(r for r in rows if r["iva_internal"] == "TestInternal")
        self.assertEqual(row["total_prop_instances"], 4)
        self.assertEqual(row["unique_prop_names"], 3)
        self.assertEqual(row["supported_unique_props"], 1)
        self.assertEqual(row["review_unique_props"], 1)
        self.assertEqual(row["ignored_unique_props"], 1)
        self.assertEqual(row["support_percentage"], 50.0)
        self.assertEqual(row["review_prop_names"], ["UnknownLamp"])

    def test_batching_groups_shared_review_props_deterministically(self):
        rows = audit.audit_roots(
            FIXTURES / "kmc_iva",
            [FIXTURES / "target"],
            ignored={"MechanicalHandle"},
        )
        audit.suggest_batches(rows)
        labels = {r["iva_internal"]: r["suggested_batch"] for r in rows}
        self.assertEqual(labels["SecondInternal"], labels["TestInternal"])
        self.assertEqual(labels["SecondInternal"], "BATCH-1")

    def test_report_generation_and_inputs_are_not_modified(self):
        target = FIXTURES / "target" / "TestCockpit.cfg"
        before = hashlib.sha256(target.read_bytes()).hexdigest()
        rows = audit.audit_roots(
            FIXTURES / "kmc_iva",
            [FIXTURES / "target"],
            ignored={"MechanicalHandle"},
        )
        audit.suggest_batches(rows)
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp)
            audit.write_reports(rows, out)
            expected = {
                "CockpitCoverageMatrix.csv",
                "CockpitCoverageMatrix.md",
                "ReviewProps.csv",
                "AuditSummary.txt",
            }
            self.assertEqual({p.name for p in out.iterdir()}, expected)
            md = (out / "CockpitCoverageMatrix.md").read_text(encoding="utf-8")
            self.assertIn("TestInternal", md)
            review = (out / "ReviewProps.csv").read_text(encoding="utf-8")
            self.assertIn("UnknownLamp", review)
        after = hashlib.sha256(target.read_bytes()).hexdigest()
        self.assertEqual(before, after)


if __name__ == "__main__":
    unittest.main()
