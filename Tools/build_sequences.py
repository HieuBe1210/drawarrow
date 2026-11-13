"""Recover per-mode level references from the APK's length-prefixed config tables."""
import pathlib,json,gzip,re,sys
root=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else pathlib.Path(__file__).resolve().parents[1]
levels=root/'Assets/Resources/Levels';source=root/'Recovery/TextAssets/levelconfigs'
catalog=json.loads((levels/'catalog.json').read_text())
lookup={}
for group in catalog['groups']:
 table={}
 for p in sorted(levels.glob(group['name']+'_*.bytes')):
  for row in json.loads(gzip.decompress(p.read_bytes()))['levels']:table[row['id']]=len(table)
 lookup[group['name']]=table
def references(patterns,group):
 table=lookup[group];result=[];seen=set()
 paths=[p for p in source.glob('*.bytes')if any(re.fullmatch(pattern,p.stem) for pattern in patterns)]
 paths.sort(key=lambda p:[int(s)if s.isdigit()else s for s in re.split(r'(\d+)',p.stem)])
 for p in paths:
  data=p.read_bytes()
  for i in range(len(data)-5):
   if data[i+1:i+4]!=b'\0\0\0':continue
   n=data[i]
   if n<1 or n>240:continue
   try:s=data[i+4:i+4+n].decode('utf8')
   except UnicodeDecodeError:continue
   if s in table and s not in seen:seen.add(s);result.append(table[s])
 return result
definitions={
 'CatEscape':(['CatNormalLevel'],'classic'),
 'Reasoning':(['ReasoningGlobalLevel1'],'classic'),
 'Pipeline':(['PipelineLevelsNormal[0-9]+'],'classic'),
 'Tarot':(['TarotLevels[0-9]+'],'classic'),
 'Zodiac':(['ZodiacLevels[0-9]+'],'classic'),
 'Zoo':(['ZooLevelsNormal[0-9]+'],'classic'),
 'Gallery':(['GalleryLevelInfo','ShapeNormalLevels[0-9]+'],'classic'),
 'TimeAttack':(['TimeAttackPool','TimeAttackPool2'],'classic'),
 'Arrows3D':(['Arrow3DLevelsNormal1','Arrow3DLevelsNormal2','Arrow3DLevelsNormal5'],'cube'),
}
catalog['sequences']=[]
for mode,(patterns,group) in definitions.items():
 indices=references(patterns,group)
 if indices:catalog['sequences'].append({'mode':mode,'group':group,'indices':indices})
 print(mode,len(indices),flush=True)
(levels/'catalog.json').write_text(json.dumps(catalog,separators=(',',':')),encoding='utf8')
