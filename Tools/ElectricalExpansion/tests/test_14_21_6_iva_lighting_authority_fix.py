from pathlib import Path
import importlib.util

PATCHER_PATH = (
    Path(__file__).resolve().parents[1]
    / "apply_14_21_6_iva_lighting_authority_fix.py"
)
spec = importlib.util.spec_from_file_location("kmc_fix", PATCHER_PATH)
kmc_fix = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kmc_fix)

RECEIVER_FIXTURE = r'''
public sealed class KmcSystemAuthorityReceiver : MonoBehaviour
{
    private const float LeaseSeconds =
        2.50f;
    private const string StagingInputLockId =
        "KMC.SYSTEM_AUTHORITY.STAGING";
    private readonly Dictionary<string, LeaseState> _leases =
        new Dictionary<string, LeaseState>(StringComparer.Ordinal);

    public void Start()
    {
        try
        {
        }
        catch (Exception ex)
        {
        }
    }

    public void Update()
    {
        ProcessPending();
        MaintainLeases();
    }

    private void ProcessPending()
    {
    }

    private static string BuildKey(
        string vesselId,
        SystemAuthorityKind authority)
    {
        return vesselId + "|" + authority.ToString();
    }
}
'''

RPM_FIXTURE = r'''
public object ProcessVariable(string variableName)
{
    KmcMfdStatusPacket status;
    if (!TryGetStatus(out status))
    {
        return 1.0;
    }

    return
        IsBusPowered(
            status.EssentialVoltage,
            status.EssentialState)
        ? 1.0
        : 0.0;
}
'''

def test_receiver_adds_live_read_only_authority_query():
    patched = kmc_fix.patch_receiver(RECEIVER_FIXTURE)
    assert "private static KmcSystemAuthorityReceiver _activeInstance;" in patched
    assert "_activeInstance = this;" in patched
    assert "public static bool IsAuthorityInhibited(" in patched
    assert "state.LastRefreshRealtime <=" in patched
    assert "LeaseSeconds;" in patched
    assert "public void OnDestroy()" in patched
    assert "_activeInstance = null;" in patched

def test_rpm_gate_requires_ess_and_honors_lights_authority():
    patched = kmc_fix.patch_rpm(RPM_FIXTURE)
    assert "bool essPowered =" in patched
    assert "if (!essPowered)" in patched
    assert "KmcSystemAuthorityReceiver.IsAuthorityInhibited(" in patched
    assert "SystemAuthorityKind.Lights" in patched
    assert patched.count("return 0.0;") >= 2
    assert "return 1.0;" in patched

def test_patch_is_idempotent():
    receiver_once = kmc_fix.patch_receiver(RECEIVER_FIXTURE)
    receiver_twice = kmc_fix.patch_receiver(receiver_once)
    assert receiver_once == receiver_twice

    rpm_once = kmc_fix.patch_rpm(RPM_FIXTURE)
    rpm_twice = kmc_fix.patch_rpm(rpm_once)
    assert rpm_once == rpm_twice

def test_repo_shape_when_run_inside_kmc_repo():
    repo = Path(__file__).resolve().parents[3]
    receiver = repo / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs"
    rpm = repo / "KMC.Plugin" / "KmcRpmLightingScopeVariableHandler.cs"

    if not receiver.exists() or not rpm.exists():
        import pytest
        pytest.skip("Not running from a full KMC repository.")

    receiver_text = receiver.read_text(encoding="utf-8-sig")
    rpm_text = rpm.read_text(encoding="utf-8-sig")

    assert (
        "private const string StagingInputLockId" in receiver_text
        or "private static KmcSystemAuthorityReceiver _activeInstance;" in receiver_text
    )
    assert "status.EssentialVoltage" in rpm_text
    assert "status.EssentialState" in rpm_text
