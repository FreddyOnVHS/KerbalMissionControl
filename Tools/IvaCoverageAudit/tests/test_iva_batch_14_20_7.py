import csv
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SPECIAL = ROOT / 'GameData/KMC/IVA/KmcRpmSpecialDisplays14_20_7.cfg'
PROFILE = ROOT / 'GameData/KMC/IVA/Profiles/DE_IVAExtension/KmcProfile_DE_MissionControl.cfg'
PATCHER = ROOT / 'Tools/IvaCoverageAudit/apply_14_20_7_classifications.py'
README = ROOT / 'README_14.20.7.txt'

BASELINE = '''prop_name,category,family,rationale\nASET_Flashlight,SPECIAL_REVIEW,special-display-or-light,Electrically relevant prop without a direct Mk1-reference equivalent; inspect its native module before assigning a KMC power family.\nkOSTerminal,SPECIAL_REVIEW,special-display-or-light,Electrically relevant prop without a direct Mk1-reference equivalent; inspect its native module before assigning a KMC power family.\nMonitorDockingMode,SPECIAL_REVIEW,special-display-or-light,Electrically relevant prop without a direct Mk1-reference equivalent; inspect its native module before assigning a KMC power family.\n'''

INTERIM = '''prop_name,category,family,rationale\nASET_Flashlight,IGNORE_STATIC,independent-device,Intentional KMC exception: handheld flashlight is modeled as a self-contained battery-powered device independent of spacecraft buses.\nkOSTerminal,REUSE_DISPLAY,RPM-MFD,Supported in 14.20.7 as an ESS-powered RPM display using native RasterPropMonitor power gating and the prop's native JSICallbackAnimator blackout.\nMonitorDockingMode,IGNORE_STATIC,stock-exception,Intentional KMC exception: stock internalGeneric docking monitor exposes no safe supported electrical-power API; leave native behavior untouched.\n'''

class IvaBatch14207Tests(unittest.TestCase):
    def _run_patcher(self, text):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / 'prop_classifications.csv'
            p.write_text(text, encoding='utf-8')
            subprocess.run([sys.executable, str(PATCHER), str(p)], check=True, capture_output=True, text=True)
            with p.open(encoding='utf-8') as f:
                return {r['prop_name']: r for r in csv.DictReader(f)}

    def test_no_runtime_kos_terminal_patch_is_packaged(self):
        self.assertFalse(SPECIAL.exists())
        self.assertFalse(PROFILE.exists())

    def test_frozen_special_reviews_close_as_documented_exceptions(self):
        rows = self._run_patcher(BASELINE)
        self.assertEqual(('IGNORE_STATIC', 'independent-device'), (rows['ASET_Flashlight']['category'], rows['ASET_Flashlight']['family']))
        self.assertEqual(('IGNORE_STATIC', 'stock-exception'), (rows['MonitorDockingMode']['category'], rows['MonitorDockingMode']['family']))
        self.assertEqual(('IGNORE_STATIC', 'optional-mod-exception'), (rows['kOSTerminal']['category'], rows['kOSTerminal']['family']))
        self.assertNotIn('SPECIAL_REVIEW', [r['category'] for r in rows.values()])

    def test_interim_supported_kos_terminal_decision_migrates_to_optional_mod_exception(self):
        rows = self._run_patcher(INTERIM)
        self.assertEqual('IGNORE_STATIC', rows['kOSTerminal']['category'])
        self.assertEqual('optional-mod-exception', rows['kOSTerminal']['family'])
        self.assertIn('optional mod', rows['kOSTerminal']['rationale'].lower())

    def test_classification_patcher_is_idempotent_after_cleanup(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / 'prop_classifications.csv'
            p.write_text(INTERIM, encoding='utf-8')
            subprocess.run([sys.executable, str(PATCHER), str(p)], check=True, capture_output=True, text=True)
            first = p.read_text(encoding='utf-8')
            subprocess.run([sys.executable, str(PATCHER), str(p)], check=True, capture_output=True, text=True)
            self.assertEqual(first, p.read_text(encoding='utf-8'))

    def test_readme_documents_all_three_as_intentional_exceptions(self):
        text = README.read_text(encoding='utf-8')
        self.assertIn('ASET_Flashlight', text)
        self.assertIn('MonitorDockingMode', text)
        self.assertIn('kOSTerminal', text)
        self.assertIn('OPTIONAL-MOD EXCEPTION', text)
        self.assertNotIn('KSP INSTALL', text)
        self.assertNotIn('RUNTIME TEST — kOSTerminal', text)

    def test_readme_requires_no_plugin_dll(self):
        text = README.read_text(encoding='utf-8')
        self.assertIn('KSP Plugin DLL Required? NO', text)

if __name__ == '__main__':
    unittest.main()
