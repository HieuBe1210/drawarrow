import pathlib,json,gzip,sys
root=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else pathlib.Path(__file__).resolve().parents[1]
for p in (root/'Assets/Resources/Levels').glob('cube_*.bytes'):
 data=json.loads(gzip.decompress(p.read_bytes()))
 for row in data['levels']:
  raw=json.loads((root/'Recovery/TextAssets/boardmap_arrow3d'/(row['id']+'.bytes')).read_bytes());row['initialView']=raw.get('InitialView',raw.get('InitRotation',[-18,-45,18]))
 p.write_bytes(gzip.compress(json.dumps(data,separators=(',',':')).encode(),mtime=0))
