import pathlib,json,gzip,re,collections,hashlib,sys
root=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else pathlib.Path(__file__).resolve().parents[1];src=root/'Recovery/TextAssets';out=root/'Assets/Resources/Levels';out.mkdir(parents=True,exist_ok=True)
classic=[];clash=[];cubes=[];octa=[];links=[];lines=[]
def arrow(a,w):
 return {'points':a['Points'],'color':a.get('ColorIndex',0),'special':a.get('SpecialArrowType',0),'param':str(a.get('Param',''))}
def save_group(name,rows,size=128):
 for i in range(0,len(rows),size):
  b=json.dumps({'levels':rows[i:i+size]},separators=(',',':'),ensure_ascii=False).encode()
  (out/f'{name}_{i//size:03}.bytes').write_bytes(gzip.compress(b,mtime=0))
 return {'name':name,'count':len(rows),'packSize':size}
print('packing classic',flush=True)
for d in sorted(src.glob('levelpacks*')):
 for p in d.iterdir():
  j=json.loads(p.read_bytes());classic.append({'id':p.stem,'width':j['XSize'],'height':j['YSize'],'arrows':[arrow(a,j['XSize']) for a in j['Arrows']],'category':str(j.get('Category','')),'source':d.name})
print('classic',len(classic),flush=True)
# Original tutorial order is preserved where its level IDs are available.
guide=re.findall(rb'levels\x10\x00\x00\x00([^\x00]{16})',(src/'levelconfigs/GuideLevels1.bytes').read_bytes())
order={v.decode():i for i,v in enumerate(guide)}
classic.sort(key=lambda j:(0,order[j['id']]) if j['id'] in order else (1,len(j['arrows']),j['width']*j['height'],j['id']))
for p in (src/'arrowclashlevels').iterdir():
 j=json.loads(p.read_bytes());clash.append({'id':p.stem,'width':j['XSize'],'height':j['YSize'],'arrows':[arrow(a,j['XSize']) for a in j['Arrows']]})
clash.sort(key=lambda j:(len(j['arrows']),j['id']))
for p in (src/'boardmap_arrow3d').iterdir():
 j=json.loads(p.read_bytes());s=j['Shape']['Size'];cubes.append({'id':p.stem,'width':s[0],'height':s[1],'depth':s[2],'arrows':[arrow(a,s[0]) for a in j['Arrows']]})
cubes.sort(key=lambda j:(len(j['arrows']),j['width']*j['height']*j['depth'],j['id']))
for p in (src/'octaarrowslevels').iterdir():
 j=json.loads(p.read_bytes());rows=[]
 for a in j['arrows']:
  ds=a.get('directions',[a.get('direction',{'dcol':1,'drow':0})]);rows.append({'points':[v['row']*j['cols']+v['col'] for v in a['points']],'kind':a['type'],'dx':ds[0]['dcol'],'dy':ds[0]['drow'],'dx2':ds[-1]['dcol'],'dy2':ds[-1]['drow'],'open':a.get('initialState')=='open'})
 octa.append({'id':p.stem,'width':j['cols'],'height':j['rows'],'moveLimit':j.get('moveLimit',999),'arrows':rows,'turns':[{'x':t['col'],'y':t['row'],'dx':t['direction']['dcol'],'dy':t['direction']['drow']}for t in j.get('turnTiles',[])],'portals':[{'a':v[0]['row']*j['cols']+v[0]['col'],'b':v[1]['row']*j['cols']+v[1]['col']}for v in j.get('portals',[])]})
octa.sort(key=lambda j:(len(j['arrows']),j['id']))
for p in sorted((src/'linkdotslevels').iterdir()):
 j=json.loads(p.read_bytes());links.append({'id':p.stem,'width':j['XSize'],'height':j['YSize'],'pairs':[{'color':a['colorId'],'points':[{'x':v[0],'y':v[1]}for v in a['pts']]}for a in j['Pairs']]})
for p in sorted((src/'onelinelevels').iterdir(),key=lambda p:int(re.search(r'\d+',p.stem)[0])):
 j=json.loads(p.read_bytes());ns=list(j['nodes']);lines.append({'id':p.stem,'nodes':[{'x':j['nodes'][n]['x'],'y':j['nodes'][n]['y']}for n in ns],'edges':[{'a':ns.index(e['from']),'b':ns.index(e['to']),'curved':'curve'in e,'cx':e.get('curve',[0,0])[0],'cy':e.get('curve',[0,0])[1]}for e in j['edges']],'solution':[ns.index(n) for n in j['solution']]})
groups=[save_group('classic',classic),save_group('clash',clash),save_group('cube',cubes),save_group('octa',octa),save_group('links',links),save_group('line',lines)]
(out/'catalog.json').write_text(json.dumps({'groups':groups}),encoding='utf8')
# Evidence used to resolve the ambiguous head-end convention in the original data.
def solved(j,first):
 w,h=j['width'],j['height']; paths=[]; occ={}
 for i,a in enumerate(j['arrows']):
  ps=a['points'];cells=set()
  for v in ps:cells.add(v)
  for u,v in zip(ps,ps[1:]):
   x,y=u%w,u//w;xx,yy=v%w,v//w
   if x!=xx and y!=yy:return False
   dx=(xx>x)-(xx<x);dy=(yy>y)-(yy<y)
   for k in range(max(abs(xx-x),abs(yy-y))+1):cells.add((y+dy*k)*w+x+dx*k)
  for v in cells:occ[v]=i
  paths.append(ps)
 deps=[]
 for i,ps in enumerate(paths):
  if len(ps)<2:return False
  u,v=(ps[0],ps[1])if first else(ps[-1],ps[-2]);x,y=u%w,u//w;dx=((x-v%w)>0)-((x-v%w)<0);dy=((y-v//w)>0)-((y-v//w)<0);dd=set();x+=dx;y+=dy
  while 0<=x<w and 0<=y<h:
   k=occ.get(y*w+x)
   if k is not None and k!=i:dd.add(k)
   x+=dx;y+=dy
  deps.append(dd)
 removed=set()
 while True:
  ready={i for i,d in enumerate(deps)if i not in removed and d<=removed}
  if not ready:break
  removed|=ready
 return len(removed)==len(paths)
stats={str(first):sum(solved(j,first)for j in classic[:100]) for first in [True,False]}
print('orientation',stats,flush=True)
report={'sourceApk':'amaze-go-1-37-1.apk','groups':groups,'headFirstSolveSampleOf100':stats,'originalGuideIds':len(guide),'tutorialIdsRecovered':sum(j['id'] in order for j in classic),'rawTextAssetCount':sum(1 for d in src.iterdir()for p in d.iterdir()),'limitations':['Original C# was compiled with IL2CPP; runtime code is a reconstruction.','Catalog ordering after recovered tutorials is by complexity, not the remote production sequence.','Special arrow and 3D mechanisms require runtime comparison; raw fields are retained in Recovery.']}
(root/'Recovery/report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report,indent=2),flush=True)
