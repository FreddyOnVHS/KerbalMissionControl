from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CSPROJ = (ROOT / "KMC.MissionControl" / "KMC.MissionControl.csproj").read_text(encoding="utf-8")

def test_transfer_planner_uplink_is_compiled_by_legacy_csproj():
    assert '<Compile Include="Transport\\TransferPlannerManeuverUplink.cs" />' in CSPROJ
