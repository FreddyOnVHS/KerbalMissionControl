#!/usr/bin/env python3
import csv
import sys
from pathlib import Path

OBSOLETE_PROP = 'ALCORMFD60x30'


def remove_obsolete_classification(path):
    path = Path(path)
    with path.open(newline='', encoding='utf-8-sig') as f:
        reader = csv.DictReader(f)
        fieldnames = reader.fieldnames
        rows = list(reader)
    if not fieldnames:
        raise ValueError(f'{path}: missing CSV header')

    matches = [r for r in rows if r.get('prop_name') == OBSOLETE_PROP]
    if len(matches) > 1:
        raise ValueError(f'{path}: duplicate {OBSOLETE_PROP} classifications found')
    if not matches:
        return False

    kept = [r for r in rows if r.get('prop_name') != OBSOLETE_PROP]
    with path.open('w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, lineterminator='\n')
        writer.writeheader()
        writer.writerows(kept)
    return True


def main(argv=None):
    argv = list(sys.argv[1:] if argv is None else argv)
    root = Path(argv[0]).resolve() if argv else Path(__file__).resolve().parents[2]
    path = root / 'Tools/IvaCoverageAudit/prop_classifications.csv'
    removed = remove_obsolete_classification(path)
    if removed:
        print('14.20.8 acceptance fix applied: removed obsolete ALCORMFD60x30 classification')
    else:
        print('14.20.8 acceptance fix already applied: ALCORMFD60x30 classification absent')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
