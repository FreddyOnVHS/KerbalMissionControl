#!/usr/bin/env python3
import csv
import re
import sys
from pathlib import Path

EXPECTED_IVA_COUNT = 16
EXPECTED_CLASSIFICATION_COUNT = 180
MISSION_CONTROL = 'DE_MissionControl'
EXPECTED_EXCEPTIONS = {
    'ASET_Flashlight': ('IGNORE_STATIC', 'independent-device'),
    'MonitorDockingMode': ('IGNORE_STATIC', 'stock-exception'),
    'kOSTerminal': ('IGNORE_STATIC', 'optional-mod-exception'),
}


def read_csv(path):
    with path.open(newline='', encoding='utf-8-sig') as f:
        return list(csv.DictReader(f))


def audited_ivas(root):
    rows = read_csv(root / 'IvaAuditOutput' / 'CockpitCoverageMatrix.csv')
    return {r['iva_internal'] for r in rows}, rows


def profile_targets(root):
    profile_dir = root / 'GameData' / 'KMC' / 'IVA' / 'Profiles' / 'DE_IVAExtension'
    targets = set()
    files = sorted(profile_dir.glob('KmcProfile_DE_*.cfg'))
    selector = re.compile(r'@INTERNAL\[([^\]]+)\]')
    for path in files:
        text = path.read_text(encoding='utf-8-sig')
        found = selector.findall(text)
        if len(found) != 1:
            raise ValueError(f'{path.name}: expected exactly one @INTERNAL selector, found {len(found)}')
        targets.add(found[0])
    return targets, files


def verify(root):
    root = Path(root)
    errors = []

    required = [
        root / 'IvaAuditOutput' / 'AuditSummary.txt',
        root / 'IvaAuditOutput' / 'CockpitCoverageMatrix.csv',
        root / 'IvaAuditOutput' / 'ReviewProps.csv',
        root / 'IvaClassificationOutput' / 'CockpitWorkload.csv',
        root / 'IvaClassificationOutput' / 'NewElectricalReview.csv',
        root / 'IvaClassificationOutput' / 'PropClassificationReport.csv',
    ]
    missing = [str(p.relative_to(root)) for p in required if not p.exists()]
    if missing:
        return [f'missing required output: {p}' for p in missing]

    summary = (root / 'IvaAuditOutput' / 'AuditSummary.txt').read_text(encoding='utf-8-sig')
    match = re.search(r'^IVAs scanned:\s*(\d+)\s*$', summary, re.MULTILINE)
    if not match or int(match.group(1)) != EXPECTED_IVA_COUNT:
        errors.append(f'audit summary must report IVAs scanned: {EXPECTED_IVA_COUNT}')

    try:
        iva_set, matrix = audited_ivas(root)
    except Exception as exc:
        errors.append(f'cannot read cockpit coverage matrix: {exc}')
        iva_set, matrix = set(), []
    if len(matrix) != EXPECTED_IVA_COUNT or len(iva_set) != EXPECTED_IVA_COUNT:
        errors.append(f'cockpit coverage matrix must contain {EXPECTED_IVA_COUNT} unique IVAs')

    workload = read_csv(root / 'IvaClassificationOutput' / 'CockpitWorkload.csv')
    workload_ivas = {r['iva_internal'] for r in workload}
    if len(workload) != EXPECTED_IVA_COUNT or workload_ivas != iva_set:
        errors.append('cockpit workload must contain the same 16 unique IVAs as the audit matrix')
    for row in workload:
        if int(row.get('special_review_props', '0') or 0) != 0:
            errors.append(f"{row['iva_internal']}: special_review_props is not zero")

    unresolved = read_csv(root / 'IvaClassificationOutput' / 'NewElectricalReview.csv')
    if unresolved:
        errors.append(f'NewElectricalReview.csv contains {len(unresolved)} unresolved prop(s)')

    review_rows = read_csv(root / 'IvaAuditOutput' / 'ReviewProps.csv')
    review_names = {r['prop_name'] for r in review_rows}
    report = read_csv(root / 'IvaClassificationOutput' / 'PropClassificationReport.csv')
    report_names = {r['prop_name'] for r in report}
    if len(report) != EXPECTED_CLASSIFICATION_COUNT or len(report_names) != EXPECTED_CLASSIFICATION_COUNT:
        errors.append(f'classification report must contain {EXPECTED_CLASSIFICATION_COUNT} unique REVIEW props, found {len(report_names)}')
    if report_names != review_names:
        missing_from_report = sorted(review_names - report_names)
        stale_in_report = sorted(report_names - review_names)
        details = []
        if missing_from_report:
            details.append('missing: ' + ', '.join(missing_from_report))
        if stale_in_report:
            details.append('stale/extra: ' + ', '.join(stale_in_report))
        errors.append('classification report does not match fresh ReviewProps.csv' + (': ' + '; '.join(details) if details else ''))
    by_name = {r['prop_name']: r for r in report}
    special = [r['prop_name'] for r in report if r.get('category') == 'SPECIAL_REVIEW']
    if special:
        errors.append('classification report still contains SPECIAL_REVIEW props: ' + ', '.join(sorted(special)))
    for prop, expected in EXPECTED_EXCEPTIONS.items():
        row = by_name.get(prop)
        if not row:
            errors.append(f'missing documented exception classification: {prop}')
            continue
        actual = (row.get('category'), row.get('family'))
        if actual != expected:
            errors.append(f'{prop}: expected {expected[0]}/{expected[1]}, found {actual[0]}/{actual[1]}')

    try:
        targets, profile_files = profile_targets(root)
    except Exception as exc:
        errors.append(f'cannot inspect DE IVA profiles: {exc}')
        targets, profile_files = set(), []
    expected_profile_targets = iva_set - {MISSION_CONTROL}
    if len(profile_files) != 15:
        errors.append(f'expected 15 DE IVA profile files, found {len(profile_files)}')
    if targets != expected_profile_targets:
        missing_targets = sorted(expected_profile_targets - targets)
        extra_targets = sorted(targets - expected_profile_targets)
        if missing_targets:
            errors.append('missing KMC DE IVA profile target(s): ' + ', '.join(missing_targets))
        if extra_targets:
            errors.append('unexpected KMC DE IVA profile target(s): ' + ', '.join(extra_targets))

    if (root / 'GameData/KMC/IVA/Profiles/DE_IVAExtension/KmcProfile_DE_MissionControl.cfg').exists():
        errors.append('DE_MissionControl runtime profile must remain absent (optional-mod exception)')
    if (root / 'GameData/KMC/IVA/KmcRpmSpecialDisplays14_20_7.cfg').exists():
        errors.append('14.20.7 kOSTerminal runtime patch must remain absent')

    return errors


def main(argv=None):
    argv = list(sys.argv[1:] if argv is None else argv)
    root = Path(argv[0]).resolve() if argv else Path(__file__).resolve().parents[2]
    errors = verify(root)
    if errors:
        print('KMC 14.20.8 IVA acceptance: FAIL')
        for error in errors:
            print(f' - {error}')
        return 1
    print('KMC 14.20.8 IVA acceptance: PASS')
    print(' IVAs audited: 16')
    print(' KMC DE IVA runtime profiles: 15')
    print(' Intentional exceptions: 3')
    print(' Unresolved SPECIAL_REVIEW props: 0')
    print(' Classification decisions: 180')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
