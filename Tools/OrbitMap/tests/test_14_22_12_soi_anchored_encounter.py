from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TRANSFORM = (ROOT / 'KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs').read_text(encoding='utf-8')


def test_hyperbolic_child_patch_is_anchored_to_soi_radius_not_epoch_phase():
    assert 'SampleHyperbolicPatchFromSoiBoundary' in TRANSFORM
    assert 'referenceBody.SoiRadiusMeters' in TRANSFORM
    assert 'patch.StartUniversalTimeSeconds' in TRANSFORM


def test_soi_anchor_solves_both_entry_branches_and_uses_continuity():
    assert 'TrueAnomalyAtRadius' in TRANSFORM
    assert 'entryPositive' in TRANSFORM
    assert 'entryNegative' in TRANSFORM
    assert 'continuityAnchor' in TRANSFORM
    assert 'DistanceSquared' in TRANSFORM


def test_soi_anchored_propagation_uses_start_ut_as_phase_origin():
    assert 'MeanAnomalyFromTrueAnomalyHyperbolic' in TRANSFORM
    assert 'meanAnomalyAtEntry' in TRANSFORM
    assert 'ut - patch.StartUniversalTimeSeconds' in TRANSFORM
    assert 'SolveHyperbolicAnomaly' in TRANSFORM


def test_old_mirror_branch_workaround_is_removed():
    assert 'MirrorAcrossPeriapsisAxis' not in TRANSFORM
