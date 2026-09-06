#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
RECEIVER = ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs"
RPM = ROOT / "KMC.Plugin" / "KmcRpmLightingScopeVariableHandler.cs"

INSTANCE_FIELD_MARKER = "private static KmcSystemAuthorityReceiver _activeInstance;"
QUERY_MARKER = "public static bool IsAuthorityInhibited("

def read(path):
    if not path.exists():
        raise RuntimeError(f"Required file not found: {path}")
    return path.read_text(encoding="utf-8-sig")

def write(path, text):
    path.write_text(text, encoding="utf-8")

def patch_receiver(text):
    if INSTANCE_FIELD_MARKER not in text:
        anchor = re.search(
            r'(?P<i>[ \t]*)private const string StagingInputLockId\s*=\s*'
            r'"KMC\.SYSTEM_AUTHORITY\.STAGING";',
            text,
            re.MULTILINE,
        )
        if not anchor:
            raise RuntimeError(
                "Could not locate KmcSystemAuthorityReceiver field anchor. "
                "Stop and review the local source; no partial receiver patch was written."
            )
        indent = anchor.group("i")
        text = text[:anchor.end()] + (
            "\n" + indent +
            "private static KmcSystemAuthorityReceiver _activeInstance;\n"
        ) + text[anchor.end():]

    if "_activeInstance = this;" not in text:
        start_pat = re.compile(
            r'(?P<head>public void Start\(\)\s*\{\s*)'
            r'(?P<body>try\s*\{)',
            re.MULTILINE,
        )
        m = start_pat.search(text)
        if not m:
            raise RuntimeError("Could not locate KmcSystemAuthorityReceiver.Start().")
        replacement = (
            m.group("head") +
            "_activeInstance = this;\n\n" +
            m.group("body")
        )
        text = text[:m.start()] + replacement + text[m.end():]

    if QUERY_MARKER not in text:
        update_pat = re.compile(
            r'(?P<update>public void Update\(\)\s*\{\s*'
            r'ProcessPending\(\);\s*MaintainLeases\(\);\s*\})',
            re.MULTILINE,
        )
        m = update_pat.search(text)
        if not m:
            raise RuntimeError("Could not locate KmcSystemAuthorityReceiver.Update().")
        query = r'''

        /// <summary>
        /// Read-only view of the live KMC system-authority lease for an active vessel.
        /// Missing receiver state, a missing lease, a vessel mismatch, or a stale lease
        /// all return false so callers fail open.
        /// </summary>
        public static bool IsAuthorityInhibited(
            Vessel vessel,
            SystemAuthorityKind authority)
        {
            KmcSystemAuthorityReceiver instance =
                _activeInstance;

            if (instance == null ||
                vessel == null)
            {
                return false;
            }

            string key =
                BuildKey(
                    vessel.id.ToString(),
                    authority);

            LeaseState state;
            if (!instance._leases.TryGetValue(
                    key,
                    out state) ||
                state == null)
            {
                return false;
            }

            return
                Time.realtimeSinceStartup -
                state.LastRefreshRealtime <=
                LeaseSeconds;
        }
'''
        text = text[:m.end()] + query + text[m.end():]

    if "public void OnDestroy()" not in text:
        process_pat = re.compile(
            r'(?P<indent>[ \t]*)private void ProcessPending\(\)',
            re.MULTILINE,
        )
        m = process_pat.search(text)
        if not m:
            raise RuntimeError("Could not locate ProcessPending() for lifecycle cleanup.")
        indent = m.group("indent")
        cleanup = (
            f"{indent}public void OnDestroy()\n"
            f"{indent}{{\n"
            f"{indent}    if (object.ReferenceEquals(\n"
            f"{indent}            _activeInstance,\n"
            f"{indent}            this))\n"
            f"{indent}    {{\n"
            f"{indent}        _activeInstance = null;\n"
            f"{indent}    }}\n"
            f"{indent}}}\n"
        )
        text = text[:m.start()] + cleanup + text[m.start():]

    return text

def patch_rpm(text):
    if "KmcSystemAuthorityReceiver.IsAuthorityInhibited(" in text:
        return text

    pat = re.compile(
        r'(?P<indent>[ \t]*)return\s*'
        r'IsBusPowered\(\s*'
        r'status\.EssentialVoltage,\s*'
        r'status\.EssentialState\)\s*'
        r'\?\s*1\.0\s*:\s*0\.0\s*;',
        re.MULTILINE,
    )
    m = pat.search(text)
    if not m:
        raise RuntimeError(
            "Could not locate the existing ESS-backed IVA lighting return block. "
            "Stop and review KmcRpmLightingScopeVariableHandler.cs."
        )

    indent = m.group("indent")
    replacement = (
        f"{indent}bool essPowered =\n"
        f"{indent}    IsBusPowered(\n"
        f"{indent}        status.EssentialVoltage,\n"
        f"{indent}        status.EssentialState);\n\n"
        f"{indent}if (!essPowered)\n"
        f"{indent}{{\n"
        f"{indent}    return 0.0;\n"
        f"{indent}}}\n\n"
        f"{indent}/*\n"
        f"{indent} * ESS can remain energized while BRK_LIGHTING_ESS is\n"
        f"{indent} * tripped. The system-authority lease is the existing\n"
        f"{indent} * breaker-specific lighting truth used by exterior lights.\n"
        f"{indent} * Missing / stale authority evidence fails open.\n"
        f"{indent} */\n"
        f"{indent}if (KmcSystemAuthorityReceiver.IsAuthorityInhibited(\n"
        f"{indent}        vessel,\n"
        f"{indent}        SystemAuthorityKind.Lights))\n"
        f"{indent}{{\n"
        f"{indent}    return 0.0;\n"
        f"{indent}}}\n\n"
        f"{indent}return 1.0;"
    )
    return text[:m.start()] + replacement + text[m.end():]

def main():
    receiver_before = read(RECEIVER)
    rpm_before = read(RPM)

    receiver_after = patch_receiver(receiver_before)
    rpm_after = patch_rpm(rpm_before)

    write(RECEIVER, receiver_after)
    write(RPM, rpm_after)

    print(
        "14.21.6 lighting corrective applied: "
        "IVA backlight now honors the live LIGHTS authority lease "
        "in addition to ESS power truth."
    )
    print("KSP Plugin DLL Required? YES")

if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
