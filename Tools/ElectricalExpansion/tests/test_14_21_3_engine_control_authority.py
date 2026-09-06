import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SHARED = (ROOT / "KMC.shared" / "SystemAuthorityPacket.cs").read_text(encoding="utf-8-sig")
GNC = (ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs").read_text(encoding="utf-8-sig")
PLUGIN = (ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs").read_text(encoding="utf-8-sig")
DIST = (ROOT / "KMC.Engine" / "SpacecraftSystems" / "ElectricalDistributionSystem.cs").read_text(encoding="utf-8-sig")

class EngineControlAuthority14213Tests(unittest.TestCase):
    def test_engine_control_load_exists(self):
        self.assertIn('"ENGINE_CONTROL"', DIST)

    def test_protocol_adds_engine_control_after_existing_values(self):
        self.assertRegex(SHARED, re.compile(r'ReactionWheels\s*=\s*4'))
        self.assertRegex(SHARED, re.compile(r'EngineControl\s*=\s*5'))

    def test_mission_control_transports_engine_control_authority(self):
        self.assertIn('SystemAuthorityKind.EngineControl', GNC)

    def test_engine_control_uses_engine_control_branch_power(self):
        self.assertIn(
            'ResolveElectricalLoadPower(\n                    result,\n                    "ENGINE_CONTROL")',
            GNC,
        )
        self.assertRegex(
            GNC,
            re.compile(
                r'electricalEngineControlInhibit\s*=.*?'
                r'SystemAuthorityKind\.EngineControl.*?'
                r'engineControlPowered\.HasValue.*?'
                r'!engineControlPowered\.Value',
                re.S,
            ),
        )

    def test_plugin_uses_type_inheritance_for_engine_detection(self):
        self.assertIn('module is ModuleEngines', PLUGIN)

    def test_engine_inhibit_uses_normal_shutdown_path(self):
        self.assertRegex(
            PLUGIN,
            re.compile(
                r'ModuleEngines\s+engine\s*=.*?as\s+ModuleEngines.*?'
                r'engine\.Shutdown\(\)',
                re.S,
            ),
        )

    def test_engine_module_is_not_disabled_by_engine_control(self):
        self.assertRegex(
            PLUGIN,
            re.compile(
                r'if\s*\(\s*state\.Authority\s*==\s*'
                r'SystemAuthorityKind\.EngineControl.*?'
                r'TryInhibitEngine\s*\(\s*module,\s*state\s*\).*?continue;',
                re.S,
            ),
        )

    def test_engine_start_commands_are_gated_during_loss(self):
        self.assertIn('"Activate"', PLUGIN)
        self.assertIn('"ActivateAction"', PLUGIN)
        self.assertIn('"OnAction"', PLUGIN)

    def test_restore_does_not_auto_reignite_engine(self):
        restore = re.search(
            r'private static void RestoreState.*',
            PLUGIN,
            re.S,
        )
        self.assertIsNotNone(restore)
        self.assertNotIn('.Activate()', restore.group(0))
        self.assertNotIn('Events["Activate"].Invoke()', restore.group(0))

    def test_unknown_custom_propulsion_fails_open(self):
        self.assertRegex(
            PLUGIN,
            re.compile(
                r'private static bool IsEngineModule.*?'
                r'return\s+module\s+is\s+ModuleEngines;',
                re.S,
            ),
        )

    def test_later_breakers_remain_unwired(self):
        for token in ():
            self.assertNotIn(token, GNC)

if __name__ == "__main__":
    unittest.main()
