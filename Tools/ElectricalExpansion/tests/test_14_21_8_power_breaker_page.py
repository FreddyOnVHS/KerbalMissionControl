from pathlib import Path
import importlib.util
import re

HERE = Path(__file__).resolve()
REPO_ROOT = HERE.parents[3]

APPLY_SCRIPT = (
    REPO_ROOT
    / "Tools"
    / "ElectricalExpansion"
    / "apply_14_21_8.py"
)

POWER_PAGE = (
    REPO_ROOT
    / "KMC.MissionControl"
    / "Pages"
    / "PowerPage.cs"
)

RENDERER = (
    REPO_ROOT
    / "KMC.MissionControl"
    / "Rendering"
    / "Power"
    / "PowerBreakerPanelRenderer.cs"
)

CSPROJ = (
    REPO_ROOT
    / "KMC.MissionControl"
    / "KMC.MissionControl.csproj"
)

def load_apply_module():
    spec = importlib.util.spec_from_file_location(
        "apply_14_21_8",
        APPLY_SCRIPT,
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def normalize(text):
    return text.replace("\r\n", "\n")

def test_apply_payload_matches_final_production_files():
    module = load_apply_module()

    assert normalize(module.POWER_PAGE_SOURCE) == normalize(
        POWER_PAGE.read_text(encoding="utf-8-sig")
    )

    assert normalize(module.RENDERER_SOURCE) == normalize(
        RENDERER.read_text(encoding="utf-8-sig")
    )

def test_project_compiles_breaker_renderer():
    project = CSPROJ.read_text(encoding="utf-8-sig")

    assert (
        r'<Compile Include="Rendering\Power\PowerBreakerPanelRenderer.cs" />'
        in project
    )

def test_power_navigation_is_three_page_reserved_rail():
    source = POWER_PAGE.read_text(encoding="utf-8-sig")

    assert "POWER RESERVED NAV RAIL" in source
    assert "private const int NavRailWidth = 220;" in source
    assert "private const int NavRailGap = 18;" in source
    assert "MissionRenderContext pageContext" in source

    assert '"1/3 ONE-LINE"' in source
    assert '"2/3 BREAKERS"' in source
    assert '"3/3 DETAIL"' in source
    assert "const int tabWidth = 180;" in source

    assert (
        "PowerBreakerPanelRenderer.Draw(\n"
        "                    pageContext,"
        in source
    )
    assert (
        "PowerDetailConsolidatedRenderer.Draw(\n"
        "                    pageContext,"
        in source
    )
    assert (
        "PowerSchematicRenderer.Draw(\n"
        "                    pageContext,"
        in source
    )

def test_final_breaker_renderer_has_all_20_breakers():
    source = RENDERER.read_text(encoding="utf-8-sig")

    breaker_ids = set(
        re.findall(
            r'new BreakerDefinition\("[^"]+", "(BRK_[A-Z0-9_]+)"\)',
            source,
        )
    )

    assert breaker_ids == {
        "BRK_GUID_A",
        "BRK_COMM_A",
        "BRK_PUMP_A",
        "BRK_CABIN_FAN_A",
        "BRK_THERMAL_HEATER_A",
        "BRK_FLIGHT_COMPUTER",
        "BRK_INSTRUMENTATION_ESS",
        "BRK_FLIGHT_CONTROL",
        "BRK_REACTION_WHEEL",
        "BRK_ENGINE_CONTROL",
        "BRK_STAGING_CONTROL",
        "BRK_BRAKE_CONTROL",
        "BRK_GEAR_CONTROL",
        "BRK_LIGHTING_ESS",
        "BRK_RCS_CONTROL",
        "BRK_GUID_B",
        "BRK_COMM_B",
        "BRK_PUMP_B",
        "BRK_CABIN_FAN_B",
        "BRK_THERMAL_HEATER_B",
    }

def test_final_breaker_renderer_has_live_feed_status():
    source = RENDERER.read_text(encoding="utf-8-sig")

    for switch_id in (
        "CONT_GEN_A",
        "CONT_BAT_A",
        "CONT_ESS_A",
        "CONT_ESS_B",
        "CONT_GEN_B",
        "CONT_BAT_B",
    ):
        assert switch_id in source

    assert "RIGHT-ALIGN SECOND FEED GROUP" in source
    assert '"XFER_MAIN_A"' not in source
    assert '"XFER_MAIN_B"' not in source

def test_internal_breaker_ids_are_lookup_only_not_display_labels():
    source = RENDERER.read_text(encoding="utf-8-sig")

    assert "definition.Name" in source
    assert "definition.BreakerId" in source

    # No DrawText call should deliberately use BreakerId as the displayed label.
    assert not re.search(
        r'DrawText\([^;]*definition\.BreakerId',
        source,
        re.DOTALL,
    )
