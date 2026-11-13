import sys,pathlib,zipfile,json,math
if len(sys.argv)>3:sys.path.insert(0,sys.argv[3])
import UnityPy
UnityPy.config.FALLBACK_UNITY_VERSION='6000.3.16f1'
root=pathlib.Path(sys.argv[2]);z=zipfile.ZipFile(sys.argv[1]);clips=[]
for name in z.namelist():
 if not name.startswith('assets/AssetBundle/animation_'):continue
 raw=z.read(name);offset=raw.find(b'UnityFS',0,128)
 if offset<0:continue
 env=UnityPy.load(raw[offset:])
 for o in env.objects:
  if o.type.name!='AnimationClip':continue
  try:tree=o.read_typetree()
  except:continue
  channels=[]
  for field,attribute in [('m_ScaleCurves','m_LocalScale'),('m_PositionCurves','m_LocalPosition'),('m_EulerCurves','localEulerAnglesRaw')]:
   for c in tree.get(field,[]):
    for axis in 'xyz':
     keys=[{'time':k['time'],'value':k['value'][axis],'incoming':k['inSlope'][axis],'outgoing':k['outSlope'][axis]}for k in c['curve']['m_Curve']]
     channels.append({'path':c['path'],'property':attribute+'.'+axis,'keys':keys})
  for c in tree.get('m_FloatCurves',[]):
   keys=[{'time':k['time'],'value':k['value'],'incoming':k['inSlope'],'outgoing':k['outSlope']}for k in c['curve']['m_Curve']]
   channels.append({'path':c['path'],'property':c['attribute'],'keys':keys})
  duration=max((k['time']for c in channels for k in c['keys']),default=0)
  clips.append({'name':tree['m_Name'],'source':name,'duration':duration,'loop':bool(tree.get('m_AnimationClipSettings',{}).get('m_LoopTime',False)),'channels':channels})
dest=root/'Assets/Resources/OriginalArt/animations.json';dest.write_text(json.dumps({'clips':clips},ensure_ascii=False,separators=(',',':'),allow_nan=True).replace('Infinity','1e30').replace('NaN','0'),encoding='utf8')
print('Recovered animation clips',len(clips))
for clip in clips:
 if clip['name'] in ['ea_HomeUI','ea_HomeUI2','ea_HomeUI_1_16_loop','ea_HomeUI_Btn_GalleryButton']:print(clip['name'],clip['duration'],[(c['path'],c['property'])for c in clip['channels']])
