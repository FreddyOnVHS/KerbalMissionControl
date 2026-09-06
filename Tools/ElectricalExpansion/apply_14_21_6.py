from pathlib import Path
import re
ROOT = Path(__file__).resolve().parents[2]
GNC = ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs"
PRIOR_TESTS = (
    ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_2_flight_control_authority.py",
    ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_3_engine_control_authority.py",
    ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_4_staging_separation_authority.py",
    ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_5_gear_brake_authority.py",
)
IVA_TEST = ROOT / "Tools" / "IvaCoverageAudit" / "tests" / "test_iva_batch_14_20_6.py"

def read_preserving(path):
    raw=path.read_bytes(); bom=raw.startswith(b"\xef\xbb\xbf"); text=raw.decode("utf-8-sig"); newline="\r\n" if "\r\n" in text else "\n"; return text.replace("\r\n","\n"),bom,newline

def write_preserving(path,text,bom,newline):
    raw=text.replace("\n",newline).encode("utf-8");
    if bom: raw=b"\xef\xbb\xbf"+raw
    path.write_bytes(raw)

def patch_gnc(text):
    changed=False
    if '"LIGHTING_ESS"' not in text:
        pat=re.compile(r'(bool\?\s+brakeControlPowered\s*=\s*ResolveElectricalLoadPower\s*\(\s*result\s*,\s*"BRAKE_CONTROL"\s*\)\s*;)')
        add='\n            bool? lightingEssPowered =\n                ResolveElectricalLoadPower(\n                    result,\n                    "LIGHTING_ESS");'
        text,count=pat.subn(r'\1'+add,text,count=1)
        if count!=1: raise RuntimeError("14.21.6 could not locate BRAKE_CONTROL power preamble.")
        changed=True
    if 'lightingEssPowered.HasValue' not in text:
        pat=re.compile(r'bool\s+electricalLightsInhibit\s*=\s*authority\s*==\s*SystemAuthorityKind\.Lights\s*&&\s*essPowered\.HasValue\s*&&\s*!essPowered\.Value\s*;')
        rep='bool electricalLightsInhibit =\n                    authority ==\n                        SystemAuthorityKind.Lights &&\n                    lightingEssPowered.HasValue &&\n                    !lightingEssPowered.Value;'
        text,count=pat.subn(rep,text,count=1)
        if count!=1: raise RuntimeError("14.21.6 could not locate broad ESS lighting inhibit.")
        changed=True
    if '"LIGHTING ESS ELECTRICAL POWER LOST"' not in text:
        if '"ESS ELECTRICAL POWER LOST"' not in text: raise RuntimeError("14.21.6 could not locate existing light reason.")
        text=text.replace('"ESS ELECTRICAL POWER LOST"','"LIGHTING ESS ELECTRICAL POWER LOST"',1); changed=True
    text,count=re.subn(r'\s*bool\?\s+essPowered\s*=\s*ResolveEssElectricalPower\s*\(\s*result\s*\)\s*;\s*','\n',text,count=1)
    if count: changed=True
    return text,changed

def patch_prior_test(text):
    changed=False
    text,count=re.subn(r'for\s+token\s+in\s*\(\s*([\'\"]?)\"LIGHTING_ESS\"\1\s*,?\s*\)\s*:', 'for token in ():', text)
    if count: changed=True
    text,count=re.subn(r'^\s*self\.assertNotIn\([^\n]*LIGHTING_ESS[^\n]*\)\s*$', '', text, count=1, flags=re.MULTILINE)
    if count: changed=True
    return text,changed

def patch_iva(text):
    changed=False
    if "test_external_lights_derive_from_actual_ess_truth" in text:
        text=text.replace("test_external_lights_derive_from_actual_ess_truth","test_external_lights_derive_from_lighting_ess_truth",1); changed=True
    if 'self.assertIn("ResolveEssElectricalPower", text)' in text:
        text=text.replace('self.assertIn("ResolveEssElectricalPower", text)','self.assertIn(\'"LIGHTING_ESS"\', text)',1); changed=True
    return text,changed

def validate(gnc):
    for token in ('"LIGHTING_ESS"','lightingEssPowered.HasValue','!lightingEssPowered.Value','"LIGHTING ESS ELECTRICAL POWER LOST"'):
        if token not in gnc: raise RuntimeError("14.21.6 validation failed: "+token)
    block=re.search(r'bool electricalLightsInhibit\s*=.*?;',gnc,re.S)
    if block is None or 'essPowered.HasValue' in block.group(0): raise RuntimeError("14.21.6 validation failed: broad ESS still drives lights.")

def main():
    for path in (GNC,)+PRIOR_TESTS+(IVA_TEST,):
        if not path.exists(): raise SystemExit("Missing required file: "+str(path))
    gnc,gb,gn=read_preserving(GNC); gnc,gc=patch_gnc(gnc)
    prior=[]; any_prior=False
    for path in PRIOR_TESTS:
        text,bom,newline=read_preserving(path); text,changed=patch_prior_test(text); prior.append((path,text,bom,newline,changed)); any_prior=any_prior or changed
    iva,ib,inn=read_preserving(IVA_TEST); iva,ic=patch_iva(iva)
    validate(gnc)
    if gc: write_preserving(GNC,gnc,gb,gn)
    for path,text,bom,newline,changed in prior:
        if changed: write_preserving(path,text,bom,newline)
    if ic: write_preserving(IVA_TEST,iva,ib,inn)
    print("14.21.6 applied: LIGHTING_ESS now drives the existing Lights authority instead of broad ESS truth." if (gc or any_prior or ic) else "14.21.6 already applied; no changes needed.")
if __name__=="__main__": main()
