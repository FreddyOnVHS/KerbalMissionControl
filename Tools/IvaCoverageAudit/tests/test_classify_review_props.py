import csv
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
TOOL_DIR = HERE.parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

from classify_review_props import (  # noqa: E402
    ClassificationError,
    classify_review_rows,
    load_classifications,
    load_review_rows,
    write_classification_reports,
)


class PropClassificationTests(unittest.TestCase):
    def _write_csv(self, path, fieldnames, rows):
        with Path(path).open('w', newline='', encoding='utf-8') as f:
            w = csv.DictWriter(f, fieldnames=fieldnames)
            w.writeheader()
            w.writerows(rows)

    def test_duplicate_classification_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / 'classes.csv'
            self._write_csv(p, ['prop_name','category','family','rationale'], [
                {'prop_name':'A','category':'IGNORE_STATIC','family':'','rationale':'x'},
                {'prop_name':'A','category':'SPECIAL_REVIEW','family':'','rationale':'y'},
            ])
            with self.assertRaises(ClassificationError):
                load_classifications(p)

    def test_unknown_review_prop_is_rejected(self):
        review = [{'prop_name':'A','iva_count':1,'instance_count':1,'ivas':'IVA1'},
                  {'prop_name':'B','iva_count':1,'instance_count':1,'ivas':'IVA1'}]
        classes = {'A': {'prop_name':'A','category':'IGNORE_STATIC','family':'','rationale':'x'}}
        with self.assertRaises(ClassificationError):
            classify_review_rows(review, classes)

    def test_extra_classification_is_rejected(self):
        review = [{'prop_name':'A','iva_count':1,'instance_count':1,'ivas':'IVA1'}]
        classes = {
            'A': {'prop_name':'A','category':'IGNORE_STATIC','family':'','rationale':'x'},
            'B': {'prop_name':'B','category':'IGNORE_STATIC','family':'','rationale':'x'},
        }
        with self.assertRaises(ClassificationError):
            classify_review_rows(review, classes)

    def test_reports_are_deterministic_and_workload_is_per_iva(self):
        with tempfile.TemporaryDirectory() as td:
            td = Path(td)
            review_path = td / 'review.csv'
            class_path = td / 'classes.csv'
            out = td / 'out'
            self._write_csv(review_path, ['prop_name','iva_count','instance_count','ivas'], [
                {'prop_name':'Zulu','iva_count':1,'instance_count':2,'ivas':'IVA2'},
                {'prop_name':'Alpha','iva_count':2,'instance_count':3,'ivas':'IVA1; IVA2'},
            ])
            self._write_csv(class_path, ['prop_name','category','family','rationale'], [
                {'prop_name':'Alpha','category':'REUSE_DIGITAL','family':'digital','rationale':'display'},
                {'prop_name':'Zulu','category':'CONTROL_NO_BLACKOUT','family':'control','rationale':'command'},
            ])
            rows = classify_review_rows(load_review_rows(review_path), load_classifications(class_path))
            self.assertEqual(['Alpha','Zulu'], [r['prop_name'] for r in rows])
            write_classification_reports(rows, out)
            text = (out / 'PropClassificationReport.csv').read_text(encoding='utf-8')
            self.assertLess(text.index('Alpha'), text.index('Zulu'))
            with (out / 'CockpitWorkload.csv').open(encoding='utf-8') as f:
                workload = list(csv.DictReader(f))
            self.assertEqual(['IVA1','IVA2'], [r['iva_internal'] for r in workload])
            iva1 = workload[0]
            iva2 = workload[1]
            self.assertEqual('1', iva1['reuse_electrical_props'])
            self.assertEqual('0', iva1['control_no_blackout_props'])
            self.assertEqual('1', iva2['reuse_electrical_props'])
            self.assertEqual('1', iva2['control_no_blackout_props'])

    def test_packaged_decision_table_is_complete_and_unique(self):
        packaged = load_classifications(TOOL_DIR / 'prop_classifications.csv')
        self.assertEqual(181, len(packaged))
        self.assertEqual('SPECIAL_REVIEW', packaged['ASET_Flashlight']['category'])
        self.assertEqual('SPECIAL_REVIEW', packaged['kOSTerminal']['category'])
        self.assertEqual('SPECIAL_REVIEW', packaged['MonitorDockingMode']['category'])

    def test_special_review_report_contains_only_special(self):
        rows = [
            {'prop_name':'A','iva_count':1,'instance_count':1,'ivas':['IVA'], 'category':'SPECIAL_REVIEW','family':'special','rationale':'inspect'},
            {'prop_name':'B','iva_count':1,'instance_count':1,'ivas':['IVA'], 'category':'REUSE_PASSIVE','family':'passive','rationale':'reuse'},
        ]
        with tempfile.TemporaryDirectory() as td:
            out = Path(td)
            write_classification_reports(rows, out)
            with (out / 'NewElectricalReview.csv').open(encoding='utf-8') as f:
                items = list(csv.DictReader(f))
            self.assertEqual(['A'], [r['prop_name'] for r in items])


if __name__ == '__main__':
    unittest.main()
