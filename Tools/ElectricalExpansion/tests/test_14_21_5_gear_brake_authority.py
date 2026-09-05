import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GNC = (ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs").read_text(encoding="utf-8-sig")
PLUGIN = (ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs").read_text(encoding="utf-8-sig")
DIST = (ROOT / "KMC.Engine" / "SpacecraftSystems" / "ElectricalDistributionSystem.cs").read_text(encoding="utf-8-sig")

class GearBrakeAuthority14215Tests(unittest.TestCase):
    def test_gear_control_load_exists(self):
        self.assertIn('"GEAR_CONTROL"', DIST)

    def test_brake_control_load_exists(self):
        self.assertIn('"BRAKE_CONTROL"', DIST)

    def test_gear_uses_gear_control_branch_power(self):
        self.assertIn(
            'ResolveElectricalLoadPower(\n                    result,\n                    "GEAR_CONTROL")',
            GNC,
        )
        self.assertRegex(
            GNC,
            re.compile(
                r'electricalGearControlInhibit\s*=.*?'
                r'SystemAuthorityKind\.Gear.*?'
                r'gearControlPowered\.HasValue.*?'
                r'!gearControlPowered\.Value',
                re.S,
            ),
        )

    def test_brakes_use_brake_control_branch_power(self):
        self.assertIn(
            'ResolveElectricalLoadPower(\n                    result,\n                    "BRAKE_CONTROL")',
            GNC,
        )
        self.assertRegex(
            GNC,
            re.compile(
                r'electricalBrakeControlInhibit\s*=.*?'
                r'SystemAuthorityKind\.Brakes.*?'
                r'brakeControlPowered\.HasValue.*?'
                r'!brakeControlPowered\.Value',
                re.S,
            ),
        )

    def test_inhibit_combines_gear_and_brake_electrical_truth(self):
        self.assertIn('electricalGearControlInhibit ||', GNC)
        self.assertIn('electricalBrakeControlInhibit ||', GNC)

    def test_gear_reason_is_specific(self):
        self.assertIn('"GEAR CONTROL ELECTRICAL POWER LOST"', GNC)

    def test_brake_reason_is_specific(self):
        self.assertIn('"BRAKE CONTROL ELECTRICAL POWER LOST"', GNC)

    def test_existing_gear_authority_path_is_reused(self):
        self.assertIn('case SystemAuthorityKind.Gear:', PLUGIN)
        self.assertIn('"ModuleWheelDeployment"', PLUGIN)

    def test_existing_brake_authority_path_is_reused(self):
        self.assertIn('case SystemAuthorityKind.Brakes:', PLUGIN)
        self.assertIn('"ModuleWheelBrakes"', PLUGIN)

    def test_no_new_plugin_gear_brake_special_case_is_required(self):
        self.assertNotIn('GEAR_CONTROL', PLUGIN)
        self.assertNotIn('BRAKE_CONTROL', PLUGIN)

    def test_lighting_breaker_remains_unwired(self):
        self.assertNotIn('"LIGHTING_ESS"', GNC)

    def test_missing_electrical_evidence_still_fails_open(self):
        self.assertRegex(
            GNC,
            re.compile(
                r'ResolveElectricalLoadPower.*?return null;',
                re.S,
            ),
        )

if __name__ == "__main__":
    unittest.main()
