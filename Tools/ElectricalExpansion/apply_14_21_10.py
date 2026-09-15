from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / 'KMC.Engine/SpacecraftSystems/ElectricalDistributionSystem.cs'

RCS_BLOCK = '''            /*
             * Build 14.21.10:
             * Promote the existing RCS_CONTROL overlay into the nominal
             * distribution before crew controls and electrical switch failures
             * are applied. This makes BRK_RCS_CONTROL visible to the same
             * authoritative switch-failure pass as every other load breaker.
             */
            AddLoad(
                distribution,
                "RCS_CONTROL",
                "RCS CONTROL / VALVE POWER",
                "BUS_ESS",
                1.0,
                1);

'''

ANCHOR = '''            AddLoad(
                distribution,
                "LIGHTING_ESS",
                "EXTERNAL / EMERGENCY LIGHTING",
                "BUS_ESS",
                0.5,
                1);

            return distribution;
'''


def patch_source(text):
    if 'Build 14.21.10:' in text and '"RCS_CONTROL"' in text:
        return text, False

    if ANCHOR not in text:
        raise RuntimeError(
            'Could not locate the LIGHTING_ESS nominal-load anchor. '
            'Verify you are applying this to frozen KMC 14.21.9.'
        )

    replacement = ANCHOR.replace(
        '            return distribution;\n',
        RCS_BLOCK + '            return distribution;\n',
        1,
    )
    return text.replace(ANCHOR, replacement, 1), True


def read_preserving(path):
    raw = path.read_bytes()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    newline = '\r\n' if '\r\n' in text else '\n'
    return text.replace('\r\n', '\n'), bom, newline


def write_preserving(path, text, bom, newline):
    raw = text.replace('\n', newline).encode('utf-8')
    if bom:
        raw = b'\xef\xbb\xbf' + raw
    path.write_bytes(raw)


def main():
    if not TARGET.exists():
        raise SystemExit('Missing required file: ' + str(TARGET))

    diagnostic_targets = [
        ROOT / 'KMC.MissionControl/Engineering/GncFailureIntegrationController.cs',
        ROOT / 'KMC.Plugin/KmcIvaAnnunciatorTestReceiver.cs',
    ]
    for path in diagnostic_targets:
        if path.exists():
            text = path.read_text(encoding='utf-8-sig', errors='replace')
            if 'KMC 14.21.10 RCS DIAG' in text:
                raise SystemExit(
                    'Temporary 14.21.10 RCS diagnostics are still applied. '
                    'Restore the diagnostic-modified files to 14.21.9 first, '
                    'then rerun this patch.'
                )

    source, bom, newline = read_preserving(TARGET)
    patched, changed = patch_source(source)

    if changed:
        write_preserving(TARGET, patched, bom, newline)
        print('14.21.10 applied: RCS_CONTROL promoted into nominal distribution.')
    else:
        print('14.21.10 already applied; no production change needed.')

    print('KSP Plugin DLL Required? NO')


if __name__ == '__main__':
    main()
