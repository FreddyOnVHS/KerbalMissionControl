from pathlib import Path

SRC = Path('KMC.Plugin/OrbitMapTelemetrySender.cs')
text = SRC.read_text(encoding='utf-8')

def test_packet_wide_frame_calibration_exists():
    assert 'KspToCanonicalFrame' in text
    assert 'BuildKspToCanonicalFrame' in text
    assert 'TryBuildOrbitBasis' in text

def test_authoritative_samples_use_ksp_bci_vectors_directly():
    block = text.split('private static void BuildAuthoritativePatchSamples',1)[1].split('private struct KspToCanonicalFrame',1)[0]
    assert 'patch.getRelativePositionAtUT(ut).xzy' in block
    assert 'referenceBody.orbit.getRelativePositionAtUT(ut).xzy' in block
    assert 'frame.Transform(localKsp + bodyCenterKsp)' in block

def test_no_future_anomaly_reconstruction_in_authoritative_sampler():
    block = text.split('private static void BuildAuthoritativePatchSamples',1)[1].split('private struct KspToCanonicalFrame',1)[0]
    assert 'CanonicalPositionAtUniversalTimeFromState' not in block
    assert 'Math.Acos(cosNu)' not in block
    assert 'radialDot' not in block

def test_frame_is_calibrated_from_current_position_and_velocity():
    block = text.split('private static KspToCanonicalFrame BuildKspToCanonicalFrame',1)[1].split('private static bool TryBuildOrbitBasis',1)[0]
    assert 'activeOrbit.getRelativePositionAtUT(ut).xzy' in block
    assert 'activeOrbit.getOrbitalVelocityAtUT(ut).xzy' in block
    assert 'CanonicalPositionAtTrueAnomaly(activeOrbit, activeOrbit.trueAnomaly)' in block
    assert 'CanonicalVelocityDirectionAtTrueAnomaly(activeOrbit, activeOrbit.trueAnomaly)' in block
