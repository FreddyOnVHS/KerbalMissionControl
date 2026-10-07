from pathlib import Path
ROOT=Path('/mnt/data/kmc65_work')
P=(ROOT/'KMC.Plugin'/'ManeuverUplinkReceiver.cs').read_text()
S=(ROOT/'KMC.shared'/'ManeuverUplinkPacket.cs').read_text()
M=(ROOT/'KMC.MissionControl'/'Pages'/'MapPage.cs').read_text()
assert 'DesiredPeriapsisRadiusMeters' in S
assert 'fields.Length != 10' in S
assert 'TryApplyKspAuthoritativePeriapsisCorrection' in P
assert 'KSP TERMINAL PE CORRECTION' in P
assert 'tracked.TargetEncounter' in P
assert 'TerminalPeriapsisCorrectionApplied' in P
assert 'packet.DesiredPeriapsisRadiusMeters' in M
assert 'TARGET PE        ' in M
print('8 structural checks passed')
