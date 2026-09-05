import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DIST = (ROOT / "KMC.Engine" / "SpacecraftSystems" / "ElectricalDistributionSystem.cs").read_text(encoding="utf-8-sig")
FOUNDATION = (ROOT / "KMC.Engine" / "SpacecraftSystems" / "SpacecraftSystemsFoundationSystem.cs").read_text(encoding="utf-8-sig")
SYSTEM = (ROOT / "KMC.Engine" / "SpacecraftSystems" / "SpacecraftSystemsSystem.cs").read_text(encoding="utf-8-sig")

NEW_LOADS = {
    "FLIGHT_CONTROL": ("SAS / FLIGHT CONTROL ELECTRONICS", 1.0),
    "REACTION_WHEEL": ("REACTION WHEEL POWER", 1.0),
    "ENGINE_CONTROL": ("ENGINE CONTROL / IGNITION", 0.75),
    "STAGING_CONTROL": ("STAGING / SEPARATION", 0.25),
    "BRAKE_CONTROL": ("BRAKE CONTROL", 0.5),
    "GEAR_CONTROL": ("GEAR CONTROL / ACTUATION", 0.5),
    "LIGHTING_ESS": ("EXTERNAL / EMERGENCY LIGHTING", 0.5),
}

class ExpandedEssBreakerTests(unittest.TestCase):
    def test_ess_feeds_are_12_amps(self):
        for feed in ("FEED_ESS_A", "FEED_ESS_B"):
            self.assertRegex(DIST, re.compile(
                rf'"{feed}"\s*,.*?SyntheticElectricalSourceKind\.BusFeed\s*,\s*12\.0\s*,',
                re.S))

    def test_seven_new_ess_loads_exist(self):
        for equipment_id, (name, demand) in NEW_LOADS.items():
            self.assertRegex(DIST, re.compile(
                rf'AddLoad\(\s*distribution\s*,\s*"{equipment_id}"\s*,\s*'
                rf'"{re.escape(name)}"\s*,\s*"BUS_ESS"\s*,\s*{demand}\s*,\s*1\s*\);',
                re.S))

    def test_existing_breaker_generation_is_preserved(self):
        self.assertRegex(DIST, re.compile(
            r'string breakerId\s*=\s*"BRK_"\s*\+\s*equipmentId\s*;', re.S))

    def test_foundation_has_components_and_ess_dependencies(self):
        for equipment_id in NEW_LOADS:
            self.assertIn(f'"{equipment_id}"', FOUNDATION)
            self.assertRegex(FOUNDATION, re.compile(
                rf'AddPowerDependency\(\s*model\s*,\s*"BUS_ESS"\s*,\s*"{equipment_id}"\s*\);',
                re.S))

    def test_approved_ess_arithmetic(self):
        new_demand = sum(v[1] for v in NEW_LOADS.values())
        self.assertAlmostEqual(4.5, new_demand)
        total = 3.0 + 1.0 + 1.0 + new_demand
        self.assertAlmostEqual(9.5, total)
        self.assertLess(total / 12.0, 0.80)

    def test_existing_rcs_overlay_remains(self):
        self.assertIn('"RCS_CONTROL"', SYSTEM)
        self.assertIn('"BRK_RCS_CONTROL"', SYSTEM)
        self.assertRegex(SYSTEM, re.compile(r'DemandAmps\s*=\s*1\.0', re.S))

    def test_no_ksp_runtime_authority_wiring_in_engine_files(self):
        combined = DIST + FOUNDATION
        for token in ("SystemAuthorityKind", "KmcSystemAuthorityReceiver", "GncFailureIntegrationController"):
            self.assertNotIn(token, combined)

if __name__ == "__main__":
    unittest.main()
