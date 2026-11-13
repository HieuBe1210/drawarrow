"""Rotate the APK's own arrowhead alpha; no generated or replacement artwork."""
import pathlib,json,sys
from PIL import Image
root=pathlib.Path(sys.argv[1]) if len(sys.argv)>1 else pathlib.Path(__file__).resolve().parents[1]
r=root/'Assets/Resources/OriginalArt';catalog=json.loads((r/'catalog.json').read_text(encoding='utf8'));source=next(s for s in catalog['sprites']if s['name']=='Arrow_head_black');atlas=Image.open(r/(source['atlas']+'.png'));im=atlas.crop((source['x'],source['y'],source['x']+source['w'],source['y']+source['h']));alpha=im.getchannel('A');white=Image.new('RGBA',im.size,(255,255,255,255));white.putalpha(alpha)
output=Image.new('RGBA',(2048,2048));catalog['sprites']=[s for s in catalog['sprites']if not s['name'].startswith('ArrowHead_')]
x=2
for angle in range(0,360,45):
 image=white.rotate(-angle,Image.Resampling.BICUBIC,expand=True);output.paste(image,(x,2));catalog['sprites'].append({'key':'derived:ArrowHead_'+str(angle),'name':'ArrowHead_'+str(angle),'sourceKey':source['key'],'atlas':'arrow_variants','x':x,'y':2,'w':image.width,'h':image.height,'border':[0,0,0,0]});x+=image.width+4
output.save(r/'arrow_variants.png');(r/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,separators=(',',':')),encoding='utf8')
