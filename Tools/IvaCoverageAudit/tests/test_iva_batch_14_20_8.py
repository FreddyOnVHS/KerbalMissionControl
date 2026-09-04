import csv
import importlib.util
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
VERIFIER = ROOT / 'Tools/IvaCoverageAudit/verify_14_20_8_acceptance.py'
FIXER = ROOT / 'Tools/IvaCoverageAudit/apply_14_20_8_acceptance_fix.py'
README = ROOT / 'README_14.20.8.txt'


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def write_csv(path, fieldnames, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('w', newline='', encoding='utf-8') as f:
        w = csv.DictWriter(f, fieldnames=fieldnames)
        w.writeheader()
        w.writerows(rows)


def make_fixture(root):
    audit = root / 'IvaAuditOutput'
    cls = root / 'IvaClassificationOutput'
    profiles = root / 'GameData/KMC/IVA/Profiles/DE_IVAExtension'
    audit.mkdir(parents=True)
    cls.mkdir(parents=True)
    profiles.mkdir(parents=True)

    ivs = ['DE_MissionControl'] + [f'DE_Test{i:02d}' for i in range(1, 16)]
    (audit / 'AuditSummary.txt').write_text('KMC TEST\nIVAs scanned: 16\n', encoding='utf-8')
    write_csv(audit / 'CockpitCoverageMatrix.csv', ['iva_internal'], [{'iva_internal': x} for x in ivs])

    exceptions = {
        'ASET_Flashlight': ('IGNORE_STATIC', 'independent-device'),
        'MonitorDockingMode': ('IGNORE_STATIC', 'stock-exception'),
        'kOSTerminal': ('IGNORE_STATIC', 'optional-mod-exception'),
    }
    review_names = list(exceptions) + [f'Prop{i:03d}' for i in range(177)]
    write_csv(audit / 'ReviewProps.csv', ['prop_name'], [{'prop_name': x} for x in review_names])

    write_csv(cls / 'CockpitWorkload.csv', ['iva_internal', 'special_review_props'],
              [{'iva_internal': x, 'special_review_props': '0'} for x in ivs])
    write_csv(cls / 'NewElectricalReview.csv', ['prop_name', 'category'], [])

    report = []
    for name, (category, family) in exceptions.items():
        report.append({'prop_name': name, 'category': category, 'family': family})
    for i in range(177):
        report.append({'prop_name': f'Prop{i:03d}', 'category': 'REFERENCE_BASELINE', 'family': 'mk1-reference'})
    write_csv(cls / 'PropClassificationReport.csv', ['prop_name', 'category', 'family'], report)

    for target in ivs:
        if target == 'DE_MissionControl':
            continue
        (profiles / f'KmcProfile_{target}.cfg').write_text(f'@INTERNAL[{target}]:NEEDS[RasterPropMonitor]\n{{\n}}\n', encoding='utf-8')
    return ivs


class IvaBatch14208Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.verifier = load_module('kmc_14208_verify', VERIFIER)

    def test_acceptance_verifier_accepts_closed_16_iva_fixture(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            make_fixture(root)
            self.assertEqual([], self.verifier.verify(root))

    def test_acceptance_verifier_rejects_unresolved_special_review(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            make_fixture(root)
            write_csv(root / 'IvaClassificationOutput/NewElectricalReview.csv', ['prop_name', 'category'],
                      [{'prop_name': 'UnexpectedDisplay', 'category': 'SPECIAL_REVIEW'}])
            errors = self.verifier.verify(root)
            self.assertTrue(any('unresolved prop' in e for e in errors))

    def test_acceptance_verifier_rejects_missing_profile(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            make_fixture(root)
            next((root / 'GameData/KMC/IVA/Profiles/DE_IVAExtension').glob('*.cfg')).unlink()
            errors = self.verifier.verify(root)
            self.assertTrue(any('profile' in e.lower() for e in errors))

    def test_acceptance_verifier_requires_three_documented_exceptions(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            make_fixture(root)
            report_path = root / 'IvaClassificationOutput/PropClassificationReport.csv'
            with report_path.open(encoding='utf-8') as f:
                rows = list(csv.DictReader(f))
            rows[0]['family'] = 'wrong-family'
            write_csv(report_path, ['prop_name', 'category', 'family'], rows)
            errors = self.verifier.verify(root)
            self.assertTrue(any('ASET_Flashlight' in e for e in errors))

    def test_acceptance_verifier_rejects_stale_classification_output(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            make_fixture(root)
            report_path = root / 'IvaClassificationOutput/PropClassificationReport.csv'
            with report_path.open(encoding='utf-8') as f:
                rows = list(csv.DictReader(f))
            rows[-1]['prop_name'] = 'ALCORMFD60x30'
            write_csv(report_path, ['prop_name', 'category', 'family'], rows)
            errors = self.verifier.verify(root)
            self.assertTrue(any('does not match fresh ReviewProps.csv' in e for e in errors))

    def test_readme_documents_runtime_acceptance_sweep(self):
        text = README.read_text(encoding='utf-8')
        for cockpit in ['Mk1 Cockpit', 'Mk1-3', 'Mk2 Lander Can', 'Cupola', 'Mk2 Cockpit', 'Mk3 Cockpit']:
            self.assertIn(cockpit, text)
        self.assertIn('MAIN A', text)
        self.assertIn('MAIN B', text)
        self.assertIn('ESS', text)
        self.assertIn('controls remain physically movable', text)
        self.assertIn('no checkerboards', text.lower())

    def test_readme_requires_no_plugin_dll(self):
        self.assertIn('KSP Plugin DLL Required? NO', README.read_text(encoding='utf-8'))

    def test_readme_requires_fresh_classification_success_before_verifier(self):
        text = README.read_text(encoding='utf-8')
        self.assertIn('must complete successfully', text.lower())
        self.assertIn('180 unique REVIEW prop(s)', text)

    def test_fixer_removes_only_obsolete_alcormfd60x30_row(self):
        fixer = load_module('kmc_14208_fixer', FIXER)
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / 'prop_classifications.csv'
            rows = [
                {'prop_name': 'Before', 'category': 'REFERENCE_BASELINE', 'family': 'x', 'rationale': 'keep'},
                {'prop_name': 'ALCORMFD60x30', 'category': 'REUSE_DISPLAY', 'family': 'RPM-MFD', 'rationale': 'obsolete'},
                {'prop_name': 'After', 'category': 'IGNORE_STATIC', 'family': 'y', 'rationale': 'keep too'},
            ]
            write_csv(path, ['prop_name', 'category', 'family', 'rationale'], rows)
            removed = fixer.remove_obsolete_classification(path)
            self.assertTrue(removed)
            with path.open(encoding='utf-8') as f:
                actual = list(csv.DictReader(f))
            self.assertEqual(['Before', 'After'], [r['prop_name'] for r in actual])
            self.assertEqual('keep', actual[0]['rationale'])
            self.assertEqual('keep too', actual[1]['rationale'])

    def test_fixer_is_idempotent_when_obsolete_row_is_already_absent(self):
        fixer = load_module('kmc_14208_fixer_idempotent', FIXER)
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / 'prop_classifications.csv'
            rows = [{'prop_name': 'Keep', 'category': 'REFERENCE_BASELINE', 'family': 'x', 'rationale': 'same'}]
            write_csv(path, ['prop_name', 'category', 'family', 'rationale'], rows)
            before = path.read_text(encoding='utf-8')
            removed = fixer.remove_obsolete_classification(path)
            after = path.read_text(encoding='utf-8')
            self.assertFalse(removed)
            self.assertEqual(before, after)


if __name__ == '__main__':
    unittest.main()
