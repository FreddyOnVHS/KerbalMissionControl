import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GNC = ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs"
HANDLER = ROOT / "KMC.Plugin" / "KmcRpmLightingScopeVariableHandler.cs"
BRIDGE = ROOT / "GameData" / "KMC" / "IVA" / "KmcRpmBridge.cfg"
LIGHTING = ROOT / "GameData" / "KMC" / "IVA" / "KmcRpmCockpitLighting14_18_10.cfg"

SUPPORTED_INTERNALS = (
    "DE_mk1CockpitInternal",
    "DE_mk1pod_IVA",
    "DE_mk1InlineInternal",
    "DE_Mk1-3",
    "DE_landerCabinSmallInternal",
    "DE_mk2LanderCanInternal",
    "DE_cupolaInternal",
    "DE_KV1_ASET_IVA_Internal",
    "DE_KV2_ASET_IVA_Internal",
    "DE_KV3_ASET_IVA_Internal",
    "DE_MEM_ASET_IVA_Internal",
    "DE_MK2POD_ASET_IVA_Internal",
    "DE_mk2CockpitStandardInternals",
    "DE_mk2InlineInternal",
    "DE_MK3_Cockpit_Int",
)


class LightingAuthority14206Tests(unittest.TestCase):
    def read(self, path):
        return path.read_text(encoding="utf-8")

    def test_external_lights_derive_from_lighting_ess_truth(self):
        text = self.read(GNC)
        self.assertIn("EvaluateSystemAuthorities(\n                    result,", text)
        self.assertIn('"LIGHTING_ESS"', text)
        self.assertIn('distribution.FindBus(\n                    "BUS_ESS")', text)
        self.assertIn("SyntheticElectricalBusState.Unpowered", text)
        self.assertIn("SyntheticElectricalBusState.Failed", text)
        self.assertIn("ess.Voltage >=\n                    18.0", text)

    def test_unknown_ess_evidence_fails_open(self):
        text = self.read(GNC)
        self.assertIn("lightingEssPowered.HasValue", text)
        self.assertIn("!lightingEssPowered.Value", text)
    def test_explicit_and_electrical_light_inhibits_are_combined(self):
        text = self.read(GNC)
        self.assertIn("bool explicitInhibit =", text)
        self.assertIn("SystemAuthorityStore.IsInhibited", text)
        self.assertIn("SystemAuthorityKind.Lights", text)
        self.assertIn("bool inhibitDesired =", text)
        self.assertIn("explicitInhibit ||", text)
        self.assertIn("electricalSasInhibit ||", text)
        self.assertIn("electricalReactionWheelInhibit ||", text)
        self.assertIn("electricalLightsInhibit;", text)
        self.assertIn("ESS ELECTRICAL POWER LOST", text)

    def test_bridge_registers_generalized_and_legacy_variables(self):
        text = self.read(BRIDGE)
        self.assertIn("variable = KMC_DE_IVA_BACKLIGHT_ALLOW,1,false", text)
        self.assertIn("variable = KMC_MK1_BACKLIGHT_ALLOW,1,false", text)
        self.assertIn("name = KmcRpmLightingScopeVariableHandler", text)

    def test_all_supported_de_internals_receive_ess_backlight_scope(self):
        text = self.read(HANDLER)
        for internal in SUPPORTED_INTERNALS:
            self.assertIn(f'"{internal}"', text)
        self.assertIn("KMC_DE_IVA_BACKLIGHT_ALLOW", text)
        self.assertIn("KMC_MK1_BACKLIGHT_ALLOW", text)
        self.assertIn("MinimumPoweredBusVoltage", text)
        self.assertIn("18.0", text)

    def test_unknown_or_missing_kmc_status_remains_fail_open(self):
        text = self.read(HANDLER)
        self.assertNotIn("DE_MissionControl", text)
        self.assertIn("if (!IsSupportedDeIva())", text)
        self.assertIn("if (!TryGetStatus(", text)
        self.assertGreaterEqual(text.count("return 1.0;"), 2)

    def test_lighting_cfg_preserves_aset_command_and_uses_generalized_gate(self):
        text = self.read(LIGHTING)
        self.assertIn("crew command = PERSISTENT_BackLight", text)
        self.assertIn("effective output = CUSTOM_ALCOR_BACKLIGHT_ON", text)
        self.assertIn("@RPM_CUSTOM_VARIABLE[ALCOR_BACKLIGHT_ON]", text)
        self.assertIn("name = KMC_DE_IVA_BACKLIGHT_ALLOW", text)
        self.assertNotIn("name = KMC_MK1_BACKLIGHT_ALLOW", text)

    def test_lighting_implementation_has_no_renderer_material_hacks(self):
        combined = "\n".join((self.read(HANDLER), self.read(LIGHTING)))
        for forbidden in (
            "RenderTexture",
            "GetComponent<Renderer",
            ".material",
            ".materials",
            "SetTexture",
            "UnityEngine.Light",
        ):
            self.assertNotIn(forbidden, combined)


if __name__ == "__main__":
    unittest.main()
