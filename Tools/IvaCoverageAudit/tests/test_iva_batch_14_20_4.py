import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PROFILES = ROOT / "GameData" / "KMC" / "IVA" / "Profiles" / "DE_IVAExtension"

REFERENCE_RENAMES = {
    "OnWARP": "KMC_OnWARP_MAIN_A",
    "onMECOIndicator": "KMC_onMECOIndicator_MAIN_B",
    "onENGFAILUREIndicator": "KMC_onENGFAILUREIndicator_ESS",
    "AlarmEngineOverheatIndicator": "KMC_AlarmEngineOverheatIndicator_ESS",
    "LowTWRIndicator": "KMC_LowTWRIndicator_MAIN_A",
    "AlarmHighSlopeIndicator": "KMC_AlarmHighSlopeIndicator_ESS",
    "AlarmGroundProximityIndicator": "KMC_AlarmGroundProximityIndicator_ESS",
    "AlarmLLegsIndicator": "KMC_AlarmLLegsIndicator_MAIN_B",
    "AlarmTurnOverIndicator": "KMC_AlarmTurnOverIndicator_ESS",
    "LowFuelIndicator": "KMC_LowFuelIndicator_MAIN_A",
    "LowMonopropIndicator": "KMC_LowMonopropIndicator_ESS",
    "AlarmHEATIndicator": "KMC_AlarmHEATIndicator_ESS",
    "LowALTIndicator": "KMC_LowALTIndicator_ESS",
    "onDESIndicator": "KMC_onDESIndicator_MAIN_A",
    "onHighGIndicator": "KMC_onHighGIndicator_ESS",
    "onAirBrakingIndicator": "KMC_onAirBrakingIndicator_MAIN_B",
    "onCONTACTIndicator": "KMC_onCONTACTIndicator_MAIN_B",
    "DigitalIndicator_CURRENT_WARP": "KMC_DigitalIndicator_CURRENT_WARP_MAIN_A",
    "DigitalIndicator_DELTAV": "KMC_DigitalIndicator_DELTAV_ESS",
    "DigitalIndicator_GFORCE": "KMC_DigitalIndicator_GFORCE_ESS",
    "DigitalIndicator_Elec_Output": "KMC_DigitalIndicator_Elec_Output_ESS",
    "IndicatorCircular_INTAKEAIR": "KMC_IndicatorCircular_INTAKEAIR_ESS",
    "IndicatorCircular_FUEL": "KMC_IndicatorCircular_FUEL_ESS",
    "IndicatorCircular_EngineTemp": "KMC_IndicatorCircular_EngineTemp_ESS",
    "IndicatorCircular_TWR": "KMC_IndicatorCircular_TWR_ESS",
    "IndicatorCircular_GFORCE": "KMC_IndicatorCircular_GFORCE_ESS",
    "IndicatorCircular_MONOPROP": "KMC_IndicatorCircular_MONOPROP_ESS",
    "GforceDisplay": "KMC_GforceDisplay_ESS",
    "FuelMonitor": "KMC_FuelMonitor_ESS",
    "DigitalIndicator_AMB_Temp": "KMC_DigitalIndicator_AMB_Temp_ESS",
    "DigitalIndicator_EXTTemp": "KMC_DigitalIndicator_EXTTemp_ESS",
    "DigitalIndicator_DYNAMICPRESSURE": "KMC_DigitalIndicator_DYNAMICPRESSURE_ESS",
    "DigitalIndicator_AtmDen": "KMC_DigitalIndicator_AtmDen_ESS",
    "DigitalIndicator_SURF_Temp": "KMC_DigitalIndicator_SURF_Temp_ESS",
    "DigitalIndicator_ALT_WARN_SetupDisplay": "KMC_DigitalIndicator_ALT_WARN_SetupDisplay_ESS",
    "IndADV_2Scales_ChargeCons": "KMC_IndADV_2Scales_ChargeCons_ESS",
    "IndicatorADV_CHARGE": "KMC_IndicatorADV_CHARGE_ESS",
    "IndicatorADV_STAGE": "KMC_IndicatorADV_STAGE_ESS",
    "IndicatorADV_THROTTLE": "KMC_IndicatorADV_THROTTLE_ESS",
}

TARGETS = {
    "KmcProfile_DE_Mk1Pod.cfg": ("DE_mk1pod_IVA", ["MAIN_A", "MAIN_B"], False),
    "KmcProfile_DE_Mk2LanderCan.cfg": ("DE_mk2LanderCanInternal", ["MAIN_A", "MAIN_B"], False),
    "KmcProfile_DE_KV1.cfg": ("DE_KV1_ASET_IVA_Internal", ["MAIN_A", "MAIN_B"], True),
    "KmcProfile_DE_KV2.cfg": ("DE_KV2_ASET_IVA_Internal", ["MAIN_A", "MAIN_B"], True),
    "KmcProfile_DE_KV3.cfg": ("DE_KV3_ASET_IVA_Internal", ["MAIN_A", "MAIN_B"], True),
    "KmcProfile_DE_MEM.cfg": ("DE_MEM_ASET_IVA_Internal", ["MAIN_A", "MAIN_B", "MAIN_A"], True),
    "KmcProfile_DE_MK2POD.cfg": ("DE_MK2POD_ASET_IVA_Internal", ["MAIN_A", "MAIN_B", "MAIN_A"], True),
}

class IvaBatch14204Tests(unittest.TestCase):
    def read(self, filename):
        return (PROFILES / filename).read_text(encoding="utf-8")

    def test_all_seven_profiles_exist_and_target_verified_internals(self):
        for filename, (internal, _, _) in TARGETS.items():
            text = self.read(filename)
            self.assertIn(f"@INTERNAL[{internal}]:NEEDS[RasterPropMonitor]", text)
            self.assertIn("KMC Build 14.20.4", text)

    def test_all_profiles_receive_mk1_reference_electrical_renames(self):
        for filename in TARGETS:
            text = self.read(filename)
            for prop, replacement in REFERENCE_RENAMES.items():
                self.assertRegex(text, rf"@PROP\[{re.escape(prop)}\],0\s*\{{\s*@name\s*=\s*{re.escape(replacement)}\s*\}}")

    def test_mfd_assignments_are_explicit_and_redundant(self):
        for filename, (_, domains, has_60) in TARGETS.items():
            text = self.read(filename)
            for i, domain in enumerate(domains):
                self.assertRegex(text, rf"@PROP\[ALCORMFD40x20\],{i}\s*\{{\s*@name\s*=\s*KMC_ALCORMFD40x20_{domain}\s*\}}")
            if has_60:
                self.assertRegex(text, r"@PROP\[ALCORMFD60x30\],0\s*\{\s*@name\s*=\s*KMC_ALCORMFD60x30_ESS\s*\}")
            else:
                self.assertNotIn("@PROP[ALCORMFD60x30]", text)

    def test_profiles_do_not_patch_command_controls_or_renderers(self):
        forbidden = ("@PROP[pb_", "@PROP[tggl_", "@PROP[sw", "@PROP[throttle", "Renderer", "RenderTexture", "Material")
        for filename in TARGETS:
            text = self.read(filename)
            for token in forbidden:
                self.assertNotIn(token, text)

    def test_14204_does_not_change_generic_power_domain_family(self):
        text = (ROOT / "GameData" / "KMC" / "IVA" / "KmcRpmPowerDomains.cfg").read_text(encoding="utf-8")
        self.assertEqual(3, text.count("+PROP[ALCORMFD40x20]:NEEDS[RasterPropMonitor]"))
        self.assertEqual(3, text.count("+PROP[ALCORMFD60x30]:NEEDS[RasterPropMonitor]"))

if __name__ == "__main__":
    unittest.main()
