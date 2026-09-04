import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
IVA = ROOT / "GameData" / "KMC" / "IVA"
PROFILES = IVA / "Profiles" / "DE_IVAExtension"

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
    "KmcProfile_DE_Mk1-3.cfg": "DE_Mk1-3",
    "KmcProfile_DE_Mk1LanderCan.cfg": "DE_landerCabinSmallInternal",
    "KmcProfile_DE_Cupola.cfg": "DE_cupolaInternal",
}

class IvaBatch14203Tests(unittest.TestCase):
    def read(self, path):
        return path.read_text(encoding="utf-8")

    def test_60x30_mfd_family_has_all_three_power_domains(self):
        text = self.read(IVA / "KmcRpmPowerDomains.cfg")
        for domain, resource in (("MAIN_A", "KMC_MAIN_A_POWERED"), ("MAIN_B", "KMC_MAIN_B_POWERED"), ("ESS", "KMC_ESS_POWERED")):
            self.assertIn(f"KMC_ALCORMFD60x30_{domain}", text)
            self.assertIn(f"resourceName = {resource}", text)
        self.assertEqual(3, text.count("+PROP[ALCORMFD60x30]:NEEDS[RasterPropMonitor]"))

    def test_all_three_profiles_exist_and_target_expected_internal(self):
        for filename, internal in TARGETS.items():
            text = self.read(PROFILES / filename)
            self.assertIn(f"@INTERNAL[{internal}]:NEEDS[RasterPropMonitor]", text)

    def test_all_three_profiles_receive_reference_electrical_renames(self):
        for filename in TARGETS:
            text = self.read(PROFILES / filename)
            for prop, replacement in REFERENCE_RENAMES.items():
                self.assertRegex(text, rf"@PROP\[{re.escape(prop)}\],0\s*\{{\s*@name\s*=\s*{re.escape(replacement)}\s*\}}")

    def test_mfd_assignments_are_explicit_and_preserve_redundancy(self):
        mk13 = self.read(PROFILES / "KmcProfile_DE_Mk1-3.cfg")
        for i, domain in enumerate(("MAIN_A", "MAIN_B", "MAIN_A", "MAIN_B")):
            self.assertIn(f"@PROP[ALCORMFD40x20],{i}", mk13)
            self.assertIn(f"KMC_ALCORMFD40x20_{domain}", mk13)
        self.assertIn("@PROP[ALCORMFD60x30],0", mk13)
        self.assertIn("KMC_ALCORMFD60x30_ESS", mk13)

        lander = self.read(PROFILES / "KmcProfile_DE_Mk1LanderCan.cfg")
        self.assertIn("@PROP[ALCORMFD40x20],0", lander)
        self.assertIn("KMC_ALCORMFD40x20_MAIN_A", lander)
        self.assertIn("@PROP[ALCORMFD40x20],1", lander)
        self.assertIn("KMC_ALCORMFD40x20_MAIN_B", lander)
        self.assertNotIn("@PROP[ALCORMFD60x30]", lander)

        cupola = self.read(PROFILES / "KmcProfile_DE_Cupola.cfg")
        self.assertIn("@PROP[ALCORMFD40x20],0", cupola)
        self.assertIn("KMC_ALCORMFD40x20_MAIN_A", cupola)
        self.assertIn("@PROP[ALCORMFD40x20],1", cupola)
        self.assertIn("KMC_ALCORMFD40x20_MAIN_B", cupola)
        self.assertIn("@PROP[ALCORMFD60x30],0", cupola)
        self.assertIn("KMC_ALCORMFD60x30_ESS", cupola)

    def test_profiles_do_not_patch_command_controls_or_renderers(self):
        forbidden = ("@PROP[pb_", "@PROP[tggl_", "@PROP[sw", "@PROP[throttle", "Renderer", "RenderTexture", "Material")
        for filename in TARGETS:
            text = self.read(PROFILES / filename)
            for token in forbidden:
                self.assertNotIn(token, text)

if __name__ == "__main__":
    unittest.main()
