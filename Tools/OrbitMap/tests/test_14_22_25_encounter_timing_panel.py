from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
RENDERER = (ROOT / "KMC.MissionControl" / "Rendering" / "OrbitMap" / "OrbitMapRenderer.cs").read_text(encoding="utf-8")


def test_encounter_panel_shows_soi_entry_countdown_from_patch_start_ut():
    assert 'p.StartUniversalTimeSeconds - s.UniversalTimeSeconds' in RENDERER
    assert 'text += "\\nENTRY " + FormatEventOffset' in RENDERER


def test_encounter_panel_keeps_child_patch_periapsis_altitude():
    assert 'p.Orbit.PeriapsisMeters' in RENDERER
    assert 'text += "\\nPE " + D(p.Orbit.PeriapsisMeters);' in RENDERER


def test_encounter_panel_labels_sampled_closest_approach_honestly():
    assert 'FindEncounterBody(s, p.Index)' in RENDERER
    assert 'encounter.EncounterUniversalTimeSeconds - s.UniversalTimeSeconds' in RENDERER
    assert 'text += "\\nCLOSEST " + FormatEventOffset' in RENDERER
    assert 'PE T-' not in RENDERER


def test_event_offset_supports_future_past_and_long_duration_display():
    assert 'seconds >= 0.0 ? "T-" : "T+"' in RENDERER
    assert 'wholeSeconds / 86400' in RENDERER
    assert 'Math.Floor(magnitude + 0.5)' in RENDERER
