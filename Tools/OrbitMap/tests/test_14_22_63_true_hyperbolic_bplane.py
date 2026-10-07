from pathlib import Path
import math

ROOT = Path(__file__).resolve().parents[3]
PLANNER = ROOT / "KMC.Engine" / "Navigation" / "TargetBPlanePlanner.cs"
OPT = ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs"


def test_true_bplane_uses_h_over_vinf_and_incoming_asymptote():
    text = PLANNER.read_text(encoding="utf-8")
    assert "TryCalculateGeometry" in text
    assert "Vector3d.Cross(\n                relativeEntryPosition" in text
    assert "speed * speed - 2.0 * targetMu / r" in text
    assert "eccentricityVector" in text
    assert "Math.Sqrt(asymptoteFactorSquared) / eccentricity" in text
    assert "Vector3d.Cross(incomingAsymptoteDirection, hVector)" in text
    assert "double angularMomentumB = h / vinf" in text


def test_terminal_solver_no_longer_projects_position_on_local_soi_velocity():
    text = OPT.read_text(encoding="utf-8")
    assert "TargetBPlanePlanner.TryCalculateGeometry" in text
    assert "TRUE incoming" in text
    assert "assessment.TargetSoiEntryRelativePosition - sHat * along" not in text
    assert "Normalize(assessment.TargetSoiEntryRelativeVelocity, out sHat)" not in text


def test_reference_hyperbola_b_matches_requested_periapsis_exactly():
    # Analytic reference hyperbola: mu=1, rp=1, e=2 gives p=3,
    # h=sqrt(3), |a|=1 and v_inf=1.  Therefore B=h/v_inf=sqrt(3),
    # identical to rp*sqrt(1 + 2*mu/(rp*v_inf^2)).
    mu = 1.0
    rp = 1.0
    e = 2.0
    p = rp * (1.0 + e)
    h = math.sqrt(mu * p)
    vinf2 = mu * (e - 1.0) / rp
    vinf = math.sqrt(vinf2)
    actual_b = h / vinf
    desired_b = rp * math.sqrt(1.0 + 2.0 * mu / (rp * vinf2))
    assert math.isclose(actual_b, math.sqrt(3.0), rel_tol=0.0, abs_tol=1e-12)
    assert math.isclose(actual_b, desired_b, rel_tol=0.0, abs_tol=1e-12)


def test_local_velocity_projection_is_not_the_true_finite_soi_bplane():
    # Same hyperbola sampled well before periapsis at nu=-100 deg.  At finite
    # radius the local velocity has already turned.  Its perpendicular-distance
    # projection must differ from the asymptotic impact parameter.
    mu = 1.0
    e = 2.0
    p = 3.0
    h = math.sqrt(mu * p)
    nu = math.radians(-100.0)
    rmag = p / (1.0 + e * math.cos(nu))
    r = (rmag * math.cos(nu), rmag * math.sin(nu))
    v = ((mu / h) * (-math.sin(nu)),
         (mu / h) * (e + math.cos(nu)))
    speed = math.hypot(*v)
    vhat = (v[0] / speed, v[1] / speed)
    along = r[0] * vhat[0] + r[1] * vhat[1]
    transverse = (r[0] - along * vhat[0], r[1] - along * vhat[1])
    local_projected_b = math.hypot(*transverse)
    vinf = math.sqrt(speed * speed - 2.0 * mu / rmag)
    true_b = h / vinf
    assert abs(local_projected_b - true_b) > 0.1
