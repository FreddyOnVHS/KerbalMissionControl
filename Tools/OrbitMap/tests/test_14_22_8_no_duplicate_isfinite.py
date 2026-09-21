from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[3]
SENDER = ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs"


def test_sender_has_single_isfinite_helper_definition():
    text = SENDER.read_text(encoding="utf-8")
    defs = re.findall(r"private\s+static\s+bool\s+IsFinite\s*\(\s*double\s+value\s*\)", text)
    assert len(defs) == 1, f"Expected exactly one IsFinite(double) helper, found {len(defs)}"
