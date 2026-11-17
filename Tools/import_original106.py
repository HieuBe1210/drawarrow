"""Read encrypted Cocos JSON assets from the user-supplied XAPK (no APK execution)."""
import gzip
import hashlib
import io
import json
from pathlib import Path
import struct
import sys
import re
import zipfile


def decrypt(data):
    if not data.startswith(b"xyGm"):
        return data
    data = data[4:]
    values = list(struct.unpack("<%dI" % (len(data) // 4), data))
    key = struct.unpack("<4I", b"xyGm" + b"\0" * 12)
    n = len(values)
    total = ((6 + 52 // n) * 0x9e3779b9) & 0xffffffff
    y = values[0]
    while total:
        e = (total >> 2) & 3
        for p in range(n - 1, -1, -1):
            z = values[p - 1] if p else values[-1]
            mx = (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^
                  ((total ^ y) + (key[(p & 3) ^ e] ^ z)))
            values[p] = (values[p] - mx) & 0xffffffff
            y = values[p]
        total = (total - 0x9e3779b9) & 0xffffffff
    length = values[-1]
    if not (n - 1) * 4 - 3 <= length <= (n - 1) * 4:
        raise ValueError("Invalid XXTEA asset length")
    return struct.pack("<%dI" % n, *values)[:length]


def extract(source, root):
    target = root / 'Recovery/Original106'
    target.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(source) as outer:
        apk = next(n for n in outer.namelist() if n == 'com.arrpuzlucta.apgame.apk')
        with zipfile.ZipFile(io.BytesIO(outer.read(apk))) as z:
            # The shipped arrow boards are a JsonAsset named "test". Other
            # levelConfig assets belong to an older, unrelated tile minigame.
            boards = []
            for member in z.namelist():
                if not member.endswith('.json'):
                    continue
                data = decrypt(z.read(member))
                if b'"MapSize"' in data and b'"Arrows"' in data:
                    rows = json.loads(data)[5][0][2]
                    boards.extend(rows)
            code = z.read('assets/assets/main/index.js').decode('utf8')
            first = re.search(r'getDataForLevel\(e\)\{const t=(.*?);if\(1===e\)', code)[1]
            first = re.sub(r'([,{])([A-Za-z]+):', r'\1"\2":', first)
            boards.insert(0, json.loads(first))
            (target / 'fixed-levels.json').write_text(json.dumps(boards), encoding='utf8')
            (target / 'source.json').write_text(json.dumps({
                'file': str(source), 'sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                'fixedLevelNumbers': [row['Level'] for row in boards],
                'note': 'Level 1 is embedded in UIMainGame. The test JsonAsset contains 50 special boards (2,6,...,198), used on Windows/browser. Android may override special boards from a server. Other boards are procedural, not fixed APK layouts.'
            }, indent=2), encoding='utf8')


def convert(root):
    source = root / 'Recovery/Original106'
    fixed = sorted(json.loads((source / 'fixed-levels.json').read_bytes()), key=lambda row: row['Level'])
    rows = []
    omitted = 0
    directions = {'U': (0, 1), 'D': (0, -1), 'L': (-1, 0), 'R': (1, 0)}
    for original in fixed:
        height, width = original['MapSize']
        arrows = []
        for arrow in original['Arrows']:
            # The original renderArrow returns immediately for one-cell paths.
            if len(arrow['Path']) < 2:
                omitted += 1
                continue
            points = []
            for point in arrow['Path']:
                r, c = map(int, point.split('_'))
                assert 0 <= r < height and 0 <= c < width
                points.append((height - 1 - r) * width + c)
            dx, dy = directions[arrow['Dir']]
            arrows.append({'points': points, 'dx': dx, 'dy': dy,
                           'color': 0, 'special': 0, 'param': ''})
        row = {'id': 'original106-' + str(original['Level']), 'width': width,
               'height': height, 'source': 'com.arrpuzlucta.apgame_1.0.6',
               'category': 'original-fixed', 'arrows': arrows}
        validate(row)
        rows.append(row)
    destination = root / 'Assets/Resources/Levels'
    catalog_path = destination / 'catalog.json'
    before = catalog_path.read_bytes()
    backup = source / 'catalog-before.json'
    if not backup.exists():
        backup.write_bytes(before)
    catalog = json.loads(before)
    group = 'original106'
    for start in range(0, len(rows), 128):
        payload = json.dumps({'levels': rows[start:start + 128]}, separators=(',', ':')).encode()
        (destination / f'{group}_{start // 128:03}.bytes').write_bytes(gzip.compress(payload, mtime=0))
    catalog['groups'] = [g for g in catalog['groups'] if g['name'] != group]
    catalog['groups'].append({'name': group, 'count': len(rows), 'packSize': 128})
    catalog['sequences'] = [s for s in catalog.get('sequences', []) if s['mode'] != 'Classic']
    # Restore the original main sequence and overlay only the source-numbered slots.
    baseline = json.loads(backup.read_bytes())
    catalog['sequences'].extend(s for s in baseline.get('sequences', []) if s['mode'] == 'Classic')
    catalog['overrides'] = [v for v in catalog.get('overrides', []) if v['mode'] != 'Classic']
    catalog['overrides'].extend({'mode': 'Classic', 'index': row['Level'] - 1,
                                 'group': group, 'levelIndex': i} for i, row in enumerate(fixed))
    catalog_path.write_text(json.dumps(catalog, separators=(',', ':')), encoding='utf8')
    report = {'passed': True, 'count': len(rows), 'sourceLevelNumbers': [r['Level'] for r in fixed],
              'allBoardsSolved': True, 'omittedOneCellPathsAsInOriginalRenderer': omitted,
              'coordinateConversion': 'width=MapSize[1], height=MapSize[0], y=height-1-row; Dir preserved explicitly',
              'scope': 'Replace Classic positions 1,2,6,10,...,198 only; retain every other original position, full main-level count, and all other modes',
              'mainLevelCount': next(g['count'] for g in catalog['groups'] if g['name'] == 'classic'),
              'limitations': ['These are the fixed boards bundled in the APK. Server overrides and procedural boards have no fixed embedded layout to extract.']}
    (root / 'Verification/original106-import.json').write_text(json.dumps(report, indent=2), encoding='utf8')
    print(json.dumps(report, indent=2))


def validate(row):
    w, h = row['width'], row['height']
    occupancy = {}
    for i, arrow in enumerate(row['arrows']):
        for a, b in zip(arrow['points'], arrow['points'][1:]):
            assert abs(a % w - b % w) + abs(a // w - b // w) == 1, (row['id'], i, 'nonadjacent path')
        for point in arrow['points']:
            assert point not in occupancy, (row['id'], 'overlapping cells', point)
            occupancy[point] = i
    dependencies = []
    for i, arrow in enumerate(row['arrows']):
        head = arrow['points'][0]
        x, y = head % w + arrow['dx'], head // w + arrow['dy']
        blocked = set()
        while 0 <= x < w and 0 <= y < h:
            other = occupancy.get(y * w + x)
            if other is not None and other != i:
                blocked.add(other)
            x += arrow['dx']
            y += arrow['dy']
        dependencies.append(blocked)
    removed = set()
    while True:
        ready = {i for i, blockers in enumerate(dependencies) if i not in removed and blockers <= removed}
        if not ready:
            break
        removed |= ready
    assert len(removed) == len(dependencies), (row['id'], 'unsolvable')


if __name__ == '__main__':
    root = Path(__file__).resolve().parents[1]
    extract(Path(sys.argv[1]), root)
    convert(root)
