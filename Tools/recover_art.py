"""Extract original sprites, fonts and UI layout evidence without executing APK code."""
import sys,pathlib,zipfile,json,re,hashlib,collections
if len(sys.argv)>3:sys.path.insert(0,sys.argv[3])
import UnityPy
from PIL import Image
UnityPy.config.FALLBACK_UNITY_VERSION='6000.3.16f1'
root=pathlib.Path(sys.argv[2]);out=root/'Assets/Resources/OriginalArt';out.mkdir(parents=True,exist_ok=True)
evidence=root/'Recovery/Art';evidence.mkdir(parents=True,exist_ok=True)
z=zipfile.ZipFile(sys.argv[1]);env=UnityPy.Environment();bundles={}
for name in z.namelist():
 if not (name.startswith('assets/AssetBundle/') and any(t in name.split('/')[-1] for t in ['share_bundle','texture_','sprite_','prefab_ui_','prefab_gameplay_','font','material_','animation_'])) and name!='assets/bin/Data/data.unity3d':continue
 b=z.read(name);offset=b.find(b'UnityFS\0',0,128)
 if offset<0:continue
 try:
  bundle=env.load_file(b[offset:],name=name);bundles[name]=bundle
 except Exception as ex:print('LOAD ERROR',name,str(ex)[:100],flush=True)
print('Loaded',len(bundles),'bundles',flush=True)
sprites=[];font_names=[];objects={};sources={};errors=[]
for o in env.objects:
 objects[(o.assets_file.name.lower(),o.path_id)]=o
 if o.type.name=='Font':
  try:
   d=o.read();b=bytes(d.m_FontData);font_names.append({'name':d.m_Name,'bytes':len(b)})
   if b:(out/('font_'+re.sub(r'[^\w-]','_',d.m_Name)+'.ttf')).write_bytes(b)
  except Exception as ex:errors.append({'type':'Font','reason':str(ex)[:100]})
 if o.type.name!='Sprite':continue
 try:
  d=o.read();im=d.image.convert('RGBA');border=d.m_Border
  sprites.append(({'key':o.assets_file.name.lower()+':'+str(o.path_id),'name':d.m_Name,'w':im.width,'h':im.height,'border':[border.x,border.y,border.z,border.w]},im))
 except Exception as ex:errors.append({'type':'Sprite','id':o.path_id,'reason':str(ex)[:100]})
print('Sprites',len(sprites),'fonts',font_names,'errors',len(errors),flush=True)
sprites.sort(key=lambda row:(-row[1].height,-row[1].width));atlas=None;atlas_id=-1;x=y=row_height=0;records=[]
def flush():
 if atlas is not None:atlas.save(out/('atlas_'+str(atlas_id).zfill(2)+'.png'))
for rec,im in sprites:
 if im.width>2044 or im.height>2044:
  ratio=min(2044/im.width,2044/im.height);im=im.resize((round(im.width*ratio),round(im.height*ratio)),Image.Resampling.LANCZOS);rec['border']=[n*ratio for n in rec['border']];rec['w'],rec['h']=im.size
 if atlas is None or (y+max(row_height,im.height)+4>2048 and x+im.width+4>2048) or im.height+y+4>2048:
  flush();atlas_id+=1;atlas=Image.new('RGBA',(2048,2048));x=y=row_height=0
 if x+im.width+4>2048:x=0;y+=row_height+4;row_height=0
 if y+im.height+4>2048:
  flush();atlas_id+=1;atlas=Image.new('RGBA',(2048,2048));x=y=row_height=0
 atlas.paste(im,(x+2,y+2));rec.update({'atlas':'atlas_'+str(atlas_id).zfill(2),'x':x+2,'y':y+2});records.append(rec);x+=im.width+4;row_height=max(row_height,im.height)
flush();(out/'catalog.json').write_text(json.dumps({'sprites':records,'fonts':font_names},ensure_ascii=False,separators=(',',':')),encoding='utf8')
def pptr(file,ptr):
 if not ptr or not ptr.get('m_PathID'):return ''
 target=file.name if ptr['m_FileID']==0 else file.externals[ptr['m_FileID']-1].path.split('/')[-1]
 return target.lower()+':'+str(ptr['m_PathID'])
all_layouts=[]
for name,bundle in bundles.items():
 if 'prefab_ui_' not in name and 'prefab_gameplay_' not in name:continue
 for asset in bundle.files.values():
  if not hasattr(asset,'objects'):continue
  rows={};gos={};transforms={};components=collections.defaultdict(list)
  for o in asset.objects.values():
   if o.type.name not in ['GameObject','RectTransform','Transform','MonoBehaviour','MonoScript']:continue
   try:tree=o.read_typetree()
   except:continue
   rows[o.path_id]={'type':o.type.name,'data':tree}
   if o.type.name=='GameObject':gos[o.path_id]=tree
   elif o.type.name in ['RectTransform','Transform']:transforms[o.path_id]=tree
   elif o.type.name=='MonoBehaviour':components[tree['m_GameObject']['m_PathID']].append(tree)
  for tid,t in transforms.items():
   if t['m_Father']['m_PathID']!=0 or 'm_SizeDelta' not in t:continue
   go=gos.get(t['m_GameObject']['m_PathID'],{});title=go.get('m_Name','');nodes=[]
   def visit(trid,parent,path):
    tr=transforms.get(trid)
    if not tr:return
    gid=tr['m_GameObject']['m_PathID'];g=gos.get(gid,{});p=path+'/'+g.get('m_Name','');idx=len(nodes)
    row={'name':g.get('m_Name',''),'path':p,'parent':parent,'active':bool(g.get('m_IsActive',1)),'rect':'m_SizeDelta'in tr,'position':[tr['m_LocalPosition'][c]for c in 'xyz'],'scale':[tr['m_LocalScale'][c]for c in 'xyz'],'rotation':[tr['m_LocalRotation'][c]for c in 'xyzw']}
    if row['rect']:
     for orig,new in [('m_AnchorMin','anchorMin'),('m_AnchorMax','anchorMax'),('m_AnchoredPosition','anchored'),('m_SizeDelta','size'),('m_Pivot','pivot')]:row[new]=[tr[orig][c]for c in 'xy']
    row['visuals']=[]
    for c in components[gid]:
     if 'm_Sprite'in c:
      co=c['m_Color'];row['visuals'].append({'type':'image','sprite':pptr(asset,c['m_Sprite']),'color':[co[k]for k in 'rgba'],'imageType':c.get('m_Type',0),'preserveAspect':bool(c.get('m_PreserveAspect',0)),'enabled':bool(c.get('m_Enabled',1)),'fill':c.get('m_FillAmount',1)})
     if 'm_text'in c or 'm_Text'in c:
      co=c.get('m_fontColor',c.get('m_Color',{'r':0,'g':0,'b':0,'a':1}));row['visuals'].append({'type':'text','text':c.get('m_text',c.get('m_Text','')),'font':pptr(asset,c.get('m_fontAsset',{})),'size':c.get('m_fontSize',c.get('m_FontData',{}).get('m_FontSize',32)),'color':[co[k]for k in 'rgba'],'alignment':c.get('m_HorizontalAlignment',c.get('m_FontData',{}).get('m_Alignment',0)),'enabled':bool(c.get('m_Enabled',1))})
    nodes.append(row)
    for child in tr.get('m_Children',[]):visit(child['m_PathID'],idx,p)
   visit(tid,-1,'');record={'name':title,'bundle':name,'nodes':nodes};safe=re.sub(r'[^\w-]','_',title)
   (evidence/(safe+'.json')).write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf8');all_layouts.append({'name':title,'nodes':len(nodes),'file':safe+'.json'})
print('Atlases',atlas_id+1,'Layouts',len(all_layouts),flush=True)
(evidence/'index.json').write_text(json.dumps({'layouts':all_layouts,'errors':errors,'sprites':len(records),'atlases':atlas_id+1,'fonts':font_names},ensure_ascii=False,indent=2),encoding='utf8')
