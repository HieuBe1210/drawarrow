import sys,pathlib,zipfile,json,gzip,re,collections,hashlib
import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION='6000.3.16f1'
root=pathlib.Path(sys.argv[2]) if len(sys.argv)>2 else pathlib.Path(__file__).resolve().parents[1]; raw=root/'Recovery'; raw.mkdir(parents=True,exist_ok=True)
assets=root/'Assets'/'Resources'/'Recovered'; assets.mkdir(parents=True,exist_ok=True)
z=zipfile.ZipFile(sys.argv[1] if len(sys.argv)>1 else 'C:/Users/tinht/Downloads/amaze-go-1-37-1.apk')
index=[]; texts=raw/'TextAssets';texts.mkdir(exist_ok=True)
for name in z.namelist():
 if not name.startswith('assets/AssetBundle/'):continue
 stem=name.split('/')[-1]
 if not any(t in stem for t in ['boardmap_arrow3d_','levels_','levelconfigs_','levelpacks_','config_core_','sprite_','sound_','prefab_gameplay_','prefab_ui_']):continue
 if any(t in stem for t in ['referenceassets','bigsprite_gallery','sprite_gallery','sprite_rank','sprite_player','sprite_pinata','sprite_zodiac','sprite_zoo']):continue
 print(stem,flush=True)
 b=z.read(name);offset=b.find(b'UnityFS\0',0,128)
 try:env=UnityPy.load(b[offset:] if offset>=0 else b)
 except Exception as e:print('ERROR',str(e)[:100],flush=True);continue
 group=stem.rsplit('_',1)[0]
 for o in env.objects:
  if o.type.name not in ['TextAsset','Texture2D','AudioClip','GameObject','MonoScript']:continue
  try:d=o.read();n=getattr(d,'m_Name','')
  except:continue
  entry={'bundle':stem,'type':o.type.name,'name':n};index.append(entry)
  safe=re.sub(r'[^\w.\-]','_',n)
  if o.type.name=='TextAsset':
   b=d.m_Script
   if isinstance(b,str):b=b.encode('utf-8','surrogateescape')
   if b[:2]==b'\x1f\x8b':
    try:b=gzip.decompress(b)
    except:pass
   dest=texts/group;dest.mkdir(exist_ok=True);(dest/(safe+'.bytes')).write_bytes(b)
   entry['file']=str((dest/(safe+'.bytes')).relative_to(root));entry['sha256']=hashlib.sha256(b).hexdigest()
  elif o.type.name=='Texture2D' and 'sprite_' in stem:
   try:
    dest=assets/'Textures';dest.mkdir(exist_ok=True);d.image.save(dest/(safe+'.png'));entry['file']=str((dest/(safe+'.png')).relative_to(root))
   except Exception as e:entry['error']=str(e)[:120]
  elif o.type.name=='AudioClip':
   try:
    for fn,data in d.samples.items():
     dest=assets/'Audio';dest.mkdir(exist_ok=True);(dest/pathlib.Path(fn).name).write_bytes(data)
   except Exception as e:entry['error']=str(e)[:120]
(raw/'inventory.json').write_text(json.dumps(index,ensure_ascii=False,indent=2),encoding='utf8')
print('DONE',len(index),flush=True)
