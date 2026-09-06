import re
import unittest
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
GNC = (ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs").read_text(encoding="utf-8-sig")
PLUGIN = (ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs").read_text(encoding="utf-8-sig")
DIST = (ROOT / "KMC.Engine" / "SpacecraftSystems" / "ElectricalDistributionSystem.cs").read_text(encoding="utf-8-sig")
class LightingBreakerAuthority14216Tests(unittest.TestCase):
    def test_lighting_ess_load_exists(self): self.assertIn('"LIGHTING_ESS"', DIST)
    def test_lights_use_lighting_ess_branch_power(self):
        self.assertIn('"LIGHTING_ESS"', GNC); self.assertIn('lightingEssPowered', GNC)
    def test_lights_no_longer_use_broad_ess_power_variable(self):
        block = re.search(r'bool electricalLightsInhibit\s*=.*?;', GNC, re.S); self.assertIsNotNone(block)
        self.assertIn('lightingEssPowered.HasValue', block.group(0)); self.assertIn('!lightingEssPowered.Value', block.group(0)); self.assertNotIn('essPowered.HasValue', block.group(0))
    def test_lighting_reason_is_breaker_specific(self): self.assertIn('"LIGHTING ESS ELECTRICAL POWER LOST"', GNC)
    def test_existing_plugin_light_path_is_reused(self): self.assertIn('case SystemAuthorityKind.Lights:', PLUGIN); self.assertIn('"ModuleLight"', PLUGIN)
    def test_module_color_changer_support_is_preserved(self): self.assertIn('"ModuleColorChanger"', PLUGIN)
    def test_no_new_plugin_lighting_ess_special_case(self): self.assertNotIn('LIGHTING_ESS', PLUGIN)
if __name__ == '__main__': unittest.main()
