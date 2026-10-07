from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OPT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs").read_text(encoding="utf-8")
EVAL = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiTrajectoryEvaluator.cs").read_text(encoding="utf-8")
RESULT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiShadowResult.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_split_state_is_part_of_the_optimizer_variable_vector():
    assert "VariableCount = 8" in OPT
    assert "midpoint X/Y/Z" in OPT
    assert "arrivalUT offsets" in OPT
    assert "q[4] * scale[4]" in OPT
    assert "q[5] * scale[5]" in OPT
    assert "q[6] * scale[6]" in OPT
    assert "q[7] * scale[7]" in OPT


def test_two_lambert_legs_join_at_a_free_midpoint():
    assert "LambertSolution leg1" in OPT
    assert "LambertSolution leg2" in OPT
    assert "actualExit.Position" in OPT
    assert "midpointPosition" in OPT
    assert "destinationArrival.Position" in OPT
    assert "leg1.ArrivalVelocity - leg2.DepartureVelocity" in OPT


def test_midpoint_is_not_a_maneuver_and_bplane_stays_disabled_for_stage1():
    assert "mathematical multiple-shooting variable" in OPT
    assert "BPlaneInitializationApplied = bPlaneInitialized" in OPT
    assert "TargetBPlaneBootstrapPlanner" not in OPT
    assert "NO NODE AUTHORITY" in MAP


def test_optimizer_tracks_split_continuity_and_optimized_arrival_time():
    assert "SplitVelocityMismatchMetersPerSecond" in RESULT
    assert "OptimizedArrivalUniversalTimeSeconds" in RESULT
    assert "SPLIT VERR" in MAP
    assert "arrivalUniversalTimeSecondsOverride" in EVAL


def test_physical_target_encounter_remains_a_feasibility_gate():
    assert "bestTarget.PredictedEncounter" in OPT
    assert "bestInbound" in OPT
    assert "TargetPositionErrorMeters" in OPT
