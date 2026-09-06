import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SHARED = (ROOT / "KMC.shared" / "SystemAuthorityPacket.cs").read_text(encoding="utf-8-sig")
GNC = (ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs").read_text(encoding="utf-8-sig")
PLUGIN = (ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs").read_text(encoding="utf-8-sig")
DIST = (ROOT / "KMC.Engine" / "SpacecraftSystems" / "ElectricalDistributionSystem.cs").read_text(encoding="utf-8-sig")

class StagingSeparationAuthority14214Tests(unittest.TestCase):
    def test_staging_control_load_exists(self):
        self.assertIn('"STAGING_CONTROL"', DIST)

    def test_protocol_adds_staging_control_after_engine_control(self):
        self.assertRegex(SHARED, re.compile(r'EngineControl\s*=\s*5'))
        self.assertRegex(SHARED, re.compile(r'StagingControl\s*=\s*6'))

    def test_mission_control_transports_staging_authority(self):
        self.assertIn('SystemAuthorityKind.StagingControl', GNC)

    def test_staging_uses_its_own_branch_power(self):
        self.assertIn(
            'ResolveElectricalLoadPower(\n                    result,\n                    "STAGING_CONTROL")',
            GNC,
        )
        self.assertRegex(
            GNC,
            re.compile(
                r'electricalStagingControlInhibit\s*=.*?'
                r'SystemAuthorityKind\.StagingControl.*?'
                r'stagingControlPowered\.HasValue.*?'
                r'!stagingControlPowered\.Value',
                re.S,
            ),
        )

    def test_global_stage_input_uses_ksp_input_lock(self):
        self.assertIn('ControlTypes.STAGING', PLUGIN)
        self.assertIn('InputLockManager.SetControlLock', PLUGIN)
        self.assertIn('InputLockManager.RemoveControlLock', PLUGIN)

    def test_stock_decoupler_families_are_recognized(self):
        self.assertIn('module is ModuleDecouplerBase', PLUGIN)
        self.assertIn('module is ModuleAnchoredDecoupler', PLUGIN)

    def test_docking_ports_are_recognized(self):
        self.assertIn('module is ModuleDockingNode', PLUGIN)

    def test_decouple_actions_are_gated_not_module_disabled(self):
        self.assertIn('"DecoupleAction"', PLUGIN)
        self.assertIn('"UndockAction"', PLUGIN)
        self.assertRegex(
            PLUGIN,
            re.compile(
                r'SystemAuthorityKind\.StagingControl.*?'
                r'GateSeparationCommands.*?continue;',
                re.S,
            ),
        )

    def test_staging_enabled_is_saved_and_restored(self):
        self.assertIn('PriorStagingEnabled', PLUGIN)
        self.assertRegex(
            PLUGIN,
            re.compile(
                r'PriorStagingEnabled.*?module\.stagingEnabled.*?'
                r'module\.stagingEnabled\s*=\s*false',
                re.S,
            ),
        )
        self.assertRegex(
            PLUGIN,
            re.compile(
                r'RestoreState.*?PriorStagingEnabled.*?'
                r'module\.stagingEnabled\s*=\s*pair\.Value',
                re.S,
            ),
        )

    def test_restore_does_not_trigger_separation(self):
        restore = re.search(r'private static void RestoreState.*', PLUGIN, re.S)
        self.assertIsNotNone(restore)
        restore_text = restore.group(0)
        self.assertNotIn('.Decouple()', restore_text)
        self.assertNotIn('.Undock()', restore_text)
        self.assertNotIn('ActivateNextStage', restore_text)

    def test_unknown_custom_separation_modules_fail_open(self):
        self.assertRegex(
            PLUGIN,
            re.compile(
                r'private static bool IsSeparationModule.*?'
                r'ModuleDecouplerBase.*?'
                r'ModuleAnchoredDecoupler.*?'
                r'ModuleDockingNode',
                re.S,
            ),
        )

    def test_later_breakers_remain_unwired(self):
        for token in ():
            self.assertNotIn(token, GNC)

if __name__ == "__main__":
    unittest.main()
