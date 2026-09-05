import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

SHARED = (
    ROOT / "KMC.shared" / "SystemAuthorityPacket.cs"
).read_text(encoding="utf-8-sig")

GNC = (
    ROOT /
    "KMC.MissionControl" /
    "Engineering" /
    "GncFailureIntegrationController.cs"
).read_text(encoding="utf-8-sig")

PLUGIN = (
    ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs"
).read_text(encoding="utf-8-sig")

DIST = (
    ROOT /
    "KMC.Engine" /
    "SpacecraftSystems" /
    "ElectricalDistributionSystem.cs"
).read_text(encoding="utf-8-sig")


class FlightControlElectricalAuthority14212Tests(unittest.TestCase):
    def test_14211_loads_are_present(self):
        self.assertIn('"FLIGHT_CONTROL"', DIST)
        self.assertIn('"REACTION_WHEEL"', DIST)

    def test_protocol_adds_reaction_wheels_without_renumbering_existing_values(self):
        self.assertRegex(SHARED, re.compile(r"Sas\s*=\s*0"))
        self.assertRegex(SHARED, re.compile(r"Gear\s*=\s*1"))
        self.assertRegex(SHARED, re.compile(r"Brakes\s*=\s*2"))
        self.assertRegex(SHARED, re.compile(r"Lights\s*=\s*3"))
        self.assertRegex(SHARED, re.compile(r"ReactionWheels\s*=\s*4"))

    def test_mission_control_transports_reaction_wheel_authority(self):
        block = re.search(
            r"SystemAuthorityKind\[\]\s+authorities\s*=.*?\};",
            GNC,
            re.S,
        )
        self.assertIsNotNone(block)
        self.assertIn("SystemAuthorityKind.Sas", block.group(0))
        self.assertIn(
            "SystemAuthorityKind.ReactionWheels",
            block.group(0),
        )

    def test_sas_uses_flight_control_branch_power(self):
        self.assertIn(
            'ResolveElectricalLoadPower(\n'
            '                    result,\n'
            '                    "FLIGHT_CONTROL")',
            GNC,
        )
        self.assertRegex(
            GNC,
            re.compile(
                r"electricalSasInhibit\s*=.*?"
                r"SystemAuthorityKind\.Sas.*?"
                r"flightControlPowered\.HasValue.*?"
                r"!flightControlPowered\.Value",
                re.S,
            ),
        )

    def test_reaction_wheels_use_their_own_branch_power(self):
        self.assertIn(
            'ResolveElectricalLoadPower(\n'
            '                    result,\n'
            '                    "REACTION_WHEEL")',
            GNC,
        )
        self.assertRegex(
            GNC,
            re.compile(
                r"electricalReactionWheelInhibit\s*=.*?"
                r"SystemAuthorityKind\.ReactionWheels.*?"
                r"reactionWheelPowered\.HasValue.*?"
                r"!reactionWheelPowered\.Value",
                re.S,
            ),
        )

    def test_load_power_requires_command_breaker_and_ess(self):
        self.assertRegex(
            GNC,
            re.compile(
                r"private static bool\?\s+ResolveElectricalLoadPower.*?"
                r"load\.CommandedOn.*?"
                r"!load\.AutomaticallyShed.*?"
                r"breaker\.Conducting.*?"
                r"essPowered\.Value",
                re.S,
            ),
        )

    def test_missing_electrical_evidence_fails_open(self):
        helper = re.search(
            r"private static bool\?\s+ResolveElectricalLoadPower.*?"
            r"private static bool\?\s+ResolveEssElectricalPower",
            GNC,
            re.S,
        )
        self.assertIsNotNone(helper)
        self.assertIn("return null;", helper.group(0))

    def test_plugin_targets_stock_reaction_wheels(self):
        self.assertRegex(
            PLUGIN,
            re.compile(
                r"case\s+SystemAuthorityKind\.ReactionWheels\s*:.*?"
                r'"ModuleReactionWheel"',
                re.S,
            ),
        )

    def test_fail_open_lease_is_preserved(self):
        self.assertRegex(
            PLUGIN,
            re.compile(r"LeaseSeconds\s*=\s*2\.50f"),
        )
        self.assertIn("RestoreState(", PLUGIN)

    def test_other_14211_breakers_are_not_wired_yet(self):
        for token in (
            '"BRAKE_CONTROL"',
            '"GEAR_CONTROL"',
            '"LIGHTING_ESS"',
        ):
            self.assertNotIn(token, GNC)


if __name__ == "__main__":
    unittest.main()
