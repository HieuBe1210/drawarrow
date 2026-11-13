import sys,pathlib,zipfile,json,collections
if len(sys.argv)>3:sys.path.insert(0,sys.argv[3])
import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION='6000.3.16f1'
root=pathlib.Path(sys.argv[2]);z=zipfile.ZipFile(sys.argv[1]);env=UnityPy.Environment();bundles={}
for name in z.namelist():
 if not (name.startswith('assets/AssetBundle/')and any(t in name.split('/')[-1]for t in ['share_bundle','texture_','prefab_ui_','prefab_gameplay_','font'])) and name!='assets/bin/Data/data.unity3d':continue
 b=z.read(name);offset=b.find(b'UnityFS',0,128)
 if offset<0:continue
 bundles[name]=env.load_file(b[offset:],name=name)
objects={(o.assets_file.name.lower(),o.path_id):o for o in env.objects};font_names={}
def obj(file,ptr):
 if not ptr or not ptr.get('m_PathID'):return None
 name=file.name if ptr['m_FileID']==0 else file.externals[ptr['m_FileID']-1].path.split('/')[-1]
 return objects.get((name.lower(),ptr['m_PathID']))
def font(file,ptr):
 o=obj(file,ptr)
 if not o:return ''
 key=(o.assets_file.name,o.path_id)
 if key in font_names:return font_names[key]
 try:
  d=o.read_typetree();source=obj(o.assets_file,d.get('m_SourceFontFile',{}));name=source.read().m_Name if source else d.get('m_Name','');font_names[key]=name;return name
 except:return ''
updated=0;groups=0
for name,bundle in bundles.items():
 if 'prefab_'not in name:continue
 for asset in bundle.files.values():
  if not hasattr(asset,'objects'):continue
  gos={};trs={};components=collections.defaultdict(list);scripts={}
  for o in asset.objects.values():
   if o.type.name not in ['GameObject','RectTransform','Transform','MonoBehaviour','MonoScript']:continue
   try:d=o.read_typetree()
   except:continue
   if o.type.name=='GameObject':gos[o.path_id]=d
   elif o.type.name in ['RectTransform','Transform']:trs[o.path_id]=d
   elif o.type.name=='MonoScript':scripts[o.path_id]=d.get('m_ClassName',d.get('m_Name',''))
   else:components[d['m_GameObject']['m_PathID']].append(d)
  for tid,tr in trs.items():
   if tr['m_Father']['m_PathID']!=0:continue
   title=gos.get(tr['m_GameObject']['m_PathID'],{}).get('m_Name','');p=root/'Recovery/Art'/(title+'.json')
   if not p.exists():continue
   data=json.loads(p.read_text(encoding='utf8'));paths={}
   def visit(tid,path):
    t=trs[tid];gid=t['m_GameObject']['m_PathID'];path+='/'+gos.get(gid,{}).get('m_Name','');paths[path]=gid
    for ch in t.get('m_Children',[]):
     if ch['m_PathID']in trs:visit(ch['m_PathID'],path)
   visit(tid,'')
   for row in data['nodes']:
    gid=paths.get(row['path']);cs=components[gid]
    row['hideGraphic']=any('m_ShowMaskGraphic'in c and not c['m_ShowMaskGraphic']for c in cs)
    row['visuals']=[v for v in row['visuals']if v['type']!='gradient']
    for c in cs:
     if '_gradient'in c and 'm_Color'in c and 'm_Sprite'not in c and 'm_Texture'not in c:
      gr=c['_gradient'];co=c['m_Color'];row['visuals'].append({'type':'gradient','enabled':bool(c.get('m_Enabled',1)),'color':[co[k]for k in 'rgba'],'direction':c.get('_direction',3),'gradientKeys':[{'time':gr['ctime'+str(i)]/65535,'rgba':[gr['key'+str(i)][k]for k in 'rgba']}for i in range(gr.get('m_NumColorKeys',2))],'alphaKeys':[{'time':gr['atime'+str(i)]/65535,'alpha':gr['key'+str(i)]['a']}for i in range(gr.get('m_NumAlphaKeys',2))]})
     if 'm_ChildAlignment'in c and 'm_Padding'in c and c.get('m_Enabled'):
      pad=c['m_Padding'];classname=scripts.get(c['m_Script']['m_PathID'],'');row['layoutGroup']={'axis':1 if 'Vertical'in classname else 0,'spacing':c.get('m_Spacing',0),'alignment':c['m_ChildAlignment'],'padding':[pad.get('m_Left',0),pad.get('m_Right',0),pad.get('m_Top',0),pad.get('m_Bottom',0)],'controlWidth':bool(c.get('m_ChildControlWidth',False)),'controlHeight':bool(c.get('m_ChildControlHeight',False)),'expandWidth':bool(c.get('m_ChildForceExpandWidth',False)),'expandHeight':bool(c.get('m_ChildForceExpandHeight',False))};groups+=1
     if 'm_IgnoreLayout'in c:row['ignoreLayout']=bool(c['m_IgnoreLayout']);row['preferredWidth']=c.get('m_PreferredWidth',-1);row['preferredHeight']=c.get('m_PreferredHeight',-1)
     if 'm_HorizontalFit'in c:row['fitWidth']=c['m_HorizontalFit'];row['fitHeight']=c['m_VerticalFit']
     if 'm_text'in c:
      for visual in row['visuals']:
       if visual['type']=='text':visual.update({'fontName':font(asset,c.get('m_fontAsset',{})),'weight':c.get('m_fontWeight',400),'autoSize':bool(c.get('m_enableAutoSizing')),'minSize':c.get('m_fontSizeMin',18),'maxSize':c.get('m_fontSizeMax',visual['size']),'verticalAlignment':c.get('m_VerticalAlignment',512)})
   p.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8');(root/'Assets/Resources/OriginalArt/Layouts'/p.name).write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf8');updated+=1
print('Updated layouts',updated,'layout groups',groups,'font assets',font_names)
