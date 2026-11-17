"""Import the bundled Unity TextAsset boards; never execute code from the APK."""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import re
import zipfile

import UnityPy

GROUP = 'escape107'


def solution(board, head_first):
    w, h = board['GridXSize'], board['GridYSize']
    occupancy = {}
    paths = []
    for i, arrow in enumerate(board['Arrows']):
        points = arrow['Indices']
        assert len(points) >= 2
        cells = set(points)
        for a, b in zip(points, points[1:]):
            x, y, xx, yy = a % w, a // w, b % w, b // w
            assert x == xx or y == yy, ('diagonal', a, b)
            dx, dy = (xx > x) - (xx < x), (yy > y) - (yy < y)
            cells.update((y + dy * k) * w + x + dx * k
                         for k in range(max(abs(xx-x), abs(yy-y)) + 1))
        for cell in cells:
            assert 0 <= cell < w*h
            assert cell not in occupancy, ('overlap', cell)
            occupancy[cell] = i
        paths.append(points if head_first else points[::-1])
    deps = []
    for i, points in enumerate(paths):
        head, neck = points[:2]
        x, y = head % w, head // w
        dx = (x > neck % w) - (x < neck % w)
        dy = (y > neck // w) - (y < neck // w)
        assert abs(dx) + abs(dy) == 1
        x, y = x + dx, y + dy
        blocked = set()
        while 0 <= x < w and 0 <= y < h:
            other = occupancy.get(y*w+x)
            if other is not None and other != i:
                blocked.add(other)
            x, y = x + dx, y + dy
        deps.append(blocked)
    removed = set()
    while True:
        ready = {i for i, blockers in enumerate(deps) if i not in removed and blockers <= removed}
        if not ready:
            return len(removed) == len(deps)
        removed.update(ready)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('xapk', type=Path)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    target = root / 'Recovery/Escape107'
    target.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.xapk) as outer:
        with zipfile.ZipFile(io.BytesIO(outer.read('com.arrowescape.puzzle.apk'))) as apk:
            manager = UnityPy.load(apk.read('assets/bin/Data/globalgamemanagers'))
            resource = next(o for o in manager.objects if o.type.name == 'ResourceManager')
            entries = []
            for name, pointer in resource.read_typetree()['m_Container']:
                match = re.fullmatch(r'levels/level_(\d+)', name, re.I)
                if not match:
                    continue
                path = resource.assets_file.externals[pointer['m_FileID']-1].path
                env = UnityPy.load(apk.read('assets/bin/Data/' + path))
                obj = next(o for o in env.objects if o.path_id == pointer['m_PathID']).read()
                board = json.loads(obj.m_Script)
                entries.append((int(match[1]), name, obj.m_Name, path, board))
    entries.sort()
    assert [entry[0] for entry in entries] == list(range(1, 1575))
    orientation = {str(first): sum(solution(entry[4], first) for entry in entries)
                   for first in (True, False)}
    print('orientation:', orientation, flush=True)
    # Only select an orientation that clears every shipped board.
    first = orientation['True'] == len(entries)
    assert first or orientation['False'] == len(entries), orientation
    rows, index = [], []
    for number, resource_name, internal_name, path, board in entries:
        (target / f'level_{number:04}.json').write_text(json.dumps(board), encoding='utf8')
        rows.append({'id': f'{GROUP}-{number}', 'width': board['GridXSize'],
                     'height': board['GridYSize'], 'source': 'com.arrowescape.puzzle_1.0.7',
                     'category': GROUP, 'arrows': [
                         {'points': a['Indices'] if first else a['Indices'][::-1],
                          'color': a.get('ColorIndex', 0), 'special': 0, 'param': ''}
                         for a in board['Arrows']]})
        index.append({'level': number, 'resource': resource_name, 'internalName': internal_name,
                      'serializedFile': path})
    (target / 'index.json').write_text(json.dumps(index, indent=2), encoding='utf8')
    report = {'passed': True, 'source': str(args.xapk),
              'sha256': hashlib.sha256(args.xapk.read_bytes()).hexdigest(),
              'levelCount': len(rows), 'arrowCount': sum(len(r['arrows']) for r in rows),
              'orientationSolveCounts': orientation, 'sourceHeadFirst': first,
              'order': 'ResourceManager levels/level_1 through levels/level_1574',
              'applied': args.apply}
    if args.apply:
        destination = root / 'Assets/Resources/Levels'
        catalog_path = destination / 'catalog.json'
        before = catalog_path.read_bytes()
        backup = target / 'catalog-before.json'
        if not backup.exists():
            backup.write_bytes(before)
        catalog = json.loads(before)
        for start in range(0, len(rows), 128):
            payload = json.dumps({'levels': rows[start:start+128]}, separators=(',', ':')).encode()
            (destination / f'{GROUP}_{start//128:03}.bytes').write_bytes(gzip.compress(payload, mtime=0))
        catalog['groups'] = [g for g in catalog['groups'] if g['name'] != GROUP]
        catalog['groups'].append({'name': GROUP, 'count': len(rows), 'packSize': 128})
        catalog['sequences'] = [s for s in catalog.get('sequences', []) if s['mode'] != 'Classic']
        catalog['sequences'].append({'mode': 'Classic', 'group': GROUP, 'indices': list(range(len(rows)))})
        catalog['overrides'] = [v for v in catalog.get('overrides', []) if v['mode'] != 'Classic']
        catalog_path.write_text(json.dumps(catalog, separators=(',', ':')), encoding='utf8')
        baseline = json.loads(backup.read_bytes())
        assert [s for s in baseline.get('sequences', []) if s['mode'] != 'Classic'] == [
            s for s in catalog['sequences'] if s['mode'] != 'Classic']
        assert [g for g in baseline['groups'] if g['name'] != GROUP] == [
            g for g in catalog['groups'] if g['name'] != GROUP]
    (root / 'Verification/escape107-import.json').write_text(json.dumps(report, indent=2), encoding='utf8')
    print(json.dumps(report, indent=2), flush=True)


if __name__ == '__main__':
    main()
