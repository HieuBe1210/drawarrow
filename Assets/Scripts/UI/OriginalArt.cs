using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    [Serializable] public class ArtSprite { public string key,name,atlas;public int x,y,w,h;public float[] border; }
    [Serializable] public class ArtCatalog { public ArtSprite[] sprites; }
    [Serializable] public class ArtGradientKey {public float time;public float[] rgba;}
    [Serializable] public class ArtAlphaKey {public float time,alpha;}
    [Serializable] public class ArtVisual { public string type,sprite,text,font,fontName;public float[] color;public float size,fill,minSize,maxSize;public int imageType,alignment,weight,verticalAlignment,direction;public bool preserveAspect,enabled,autoSize;public ArtGradientKey[] gradientKeys;public ArtAlphaKey[] alphaKeys; }
    [Serializable] public class ArtLayoutGroup {public int axis,alignment;public float spacing;public float[] padding;public bool controlWidth,controlHeight,expandWidth,expandHeight;}
    [Serializable] public class ArtNode { public string name,path;public int parent,fitWidth,fitHeight;public bool active,rect,ignoreLayout,hideGraphic;public float preferredWidth=-1,preferredHeight=-1;public float[] position,scale,rotation,anchorMin,anchorMax,anchored,size,pivot;public ArtVisual[] visuals;public ArtLayoutGroup layoutGroup; }
    [Serializable] public class ArtLayout { public string name,bundle;public ArtNode[] nodes; }
    public sealed class ArtBinding
    {
        public Func<string,bool,bool> Visible;
        public Func<string,string,string> Text;
        public Func<string,Color,Color> Color;
        public Func<string,string,string> Sprite;
        public string Clip;public float ClipTime;
    }
    public static class OriginalArt
    {
        static readonly Dictionary<string,ArtSprite> keys=new Dictionary<string,ArtSprite>();
        static readonly Dictionary<string,ArtSprite> names=new Dictionary<string,ArtSprite>();
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,ArtLayout> layouts=new Dictionary<string,ArtLayout>();
        static readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
        static readonly Dictionary<ArtVisual,Texture2D> gradients=new Dictionary<ArtVisual,Texture2D>();
        static bool loaded;public static int SpriteCount=>keys.Count;
        public static int Theme {get=>Mathf.Clamp(PlayerPrefs.GetInt("drawarrow.artTheme",0),0,2);set{PlayerPrefs.SetInt("drawarrow.artTheme",Mathf.Clamp(value,0,2));PlayerPrefs.Save();}}
        public static Color ThemeBackground(int theme)=>theme==1?Ink.Hex("DDEFFB"):theme==2?Ink.Hex("17476B"):Ink.Hex("F5ECDB");
        public static Color Background=>ThemeBackground(Theme);
        public static Color Foreground=>Theme==2?Ink.Hex("F3F7FA"):Ink.Hex("555B63");
        public static Color Accent=>Ink.Hex("BF7737");
        public static Color ArrowInk=>Theme==2?Ink.Hex("F5FAFF"):Ink.Hex("0C1736");
        public static void Load()
        {
            if(loaded)return;loaded=true;var asset=Resources.Load<TextAsset>("OriginalArt/catalog");if(!asset)throw new InvalidOperationException("Original APK artwork catalog is missing.");
            foreach(var sprite in JsonUtility.FromJson<ArtCatalog>(asset.text).sprites){keys[sprite.key]=sprite;if(!names.ContainsKey(sprite.name))names[sprite.name]=sprite;}
        }
        public static ArtSprite Find(string name){Load();if(string.IsNullOrEmpty(name))return null;if(keys.TryGetValue(name,out var sprite))return sprite;names.TryGetValue(name,out sprite);return sprite;}
        static Font Typeface(string name){if(string.IsNullOrEmpty(name))return null;if(!fonts.TryGetValue(name,out var face)){face=Resources.Load<Font>("OriginalArt/font_"+name);fonts[name]=face;}return face;}
        static Texture2D Atlas(string name)
        {
            if(!textures.TryGetValue(name,out var texture))
            {
                texture=Resources.Load<Texture2D>("OriginalArt/"+name);if(!texture)throw new InvalidOperationException("Missing artwork atlas "+name);textures[name]=texture;
            }
            return texture;
        }
        public static bool IsLifeIcon(string name)=>!string.IsNullOrEmpty(name)&&(name.StartsWith("CommonIconHeart",StringComparison.Ordinal)||name=="IconLifeV2"||name=="Style3HeartWater");
        public static bool Draw(string name,Rect rect,Color? tint=null,bool fit=false,bool slice=false)
        {
            var s=Find(name);if(s==null)return false;
            if(IsLifeIcon(s.name))
            {
                var color=Ink.Hex("E83B4D");color.a=(tint??Color.white).a;
                float side=Mathf.Min(rect.width,rect.height);
                Ink.Heart(new Rect(rect.center.x-side*.5f,rect.center.y-side*.5f,side,side),color);return true;
            }
            var texture=Atlas(s.atlas);Color old=GUI.color;GUI.color=tint??Color.white;
            if(fit){float k=Mathf.Min(rect.width/s.w,rect.height/s.h);rect=new Rect(rect.center.x-s.w*k*.5f,rect.center.y-s.h*k*.5f,s.w*k,s.h*k);}
            if(slice&&s.border!=null&&s.border.Length==4&&s.border[0]+s.border[1]+s.border[2]+s.border[3]>0)
            {
                float l=s.border[0],b=s.border[1],r=s.border[2],t=s.border[3];float bx=Mathf.Min(1,(s.w-1)/Mathf.Max(1,l+r)),by=Mathf.Min(1,(s.h-1)/Mathf.Max(1,b+t));l*=bx;r*=bx;b*=by;t*=by;float factor=Mathf.Min(1,Mathf.Min(rect.width/Mathf.Max(1,l+r),rect.height/Mathf.Max(1,b+t)));
                float[] xs={0,l,s.w-r,s.w},ys={0,t,s.h-b,s.h};float[] dx={rect.x,rect.x+l*factor,rect.xMax-r*factor,rect.xMax},dy={rect.y,rect.y+t*factor,rect.yMax-b*factor,rect.yMax};
                for(int yy=0;yy<3;yy++)for(int xx=0;xx<3;xx++)if(dx[xx+1]>dx[xx]&&dy[yy+1]>dy[yy])
                {
                    float overlapX=xx>0?.6f:0,overlapY=yy>0?.6f:0;
                    Blit(texture,s,new Rect(dx[xx]-overlapX,dy[yy]-overlapY,dx[xx+1]-dx[xx]+overlapX+(xx<2?.6f:0),dy[yy+1]-dy[yy]+overlapY+(yy<2?.6f:0)),xs[xx],ys[yy],xs[xx+1]-xs[xx],ys[yy+1]-ys[yy]);
                }
            }
            else Blit(texture,s,rect,0,0,s.w,s.h);
            GUI.color=old;return true;
        }
        static void Blit(Texture2D texture,ArtSprite s,Rect r,float x,float y,float w,float h){GUI.DrawTextureWithTexCoords(r,texture,new Rect((s.x+x)/2048f,1-(s.y+y+h)/2048f,w/2048f,h/2048f),true);}
        public static ArtLayout GetLayout(string name)
        {
            if(!layouts.TryGetValue(name,out var layout)){var text=Resources.Load<TextAsset>("OriginalArt/Layouts/"+name);if(!text)return null;layout=JsonUtility.FromJson<ArtLayout>(text.text);layouts[name]=layout;}return layout;
        }
        public static Rect NodeRect(string layout,string path,Rect target,Vector2 design,ArtBinding binding=null)
        {
            var data=GetLayout(layout);if(data==null)return Rect.zero;var state=Resolve(data,design,binding);for(int i=0;i<data.nodes.Length;i++)if(data.nodes[i].path.EndsWith(path,StringComparison.Ordinal))return ToGUI(state[i].bounds,target,design);return Rect.zero;
        }
        struct NodeState{public Rect bounds;public Vector2 size,origin,scale;public bool active;public float alpha;}
        static NodeState[] Resolve(ArtLayout data,Vector2 design,ArtBinding binding=null)
        {
            var state=new NodeState[data.nodes.Length];var grouped=new Dictionary<int,Rect>();var visible=new bool[state.Length];
            for(int i=0;i<state.Length;i++)
            {
                var n=data.nodes[i];bool own=binding?.Visible!=null?binding.Visible(n.path,n.active):n.active;visible[i]=own&&(n.parent<0||visible[n.parent]);
                if(n.parent<0){state[i]=new NodeState{size=design,origin=Vector2.zero,scale=Vector2.one,bounds=new Rect(0,0,design.x,design.y),active=visible[i],alpha=1};ArrangeGroup(data,i,state[i],grouped,binding);continue;}
                var p=state[n.parent];Vector2 localScale=new Vector2(n.scale[0],n.scale[1]);
                if(binding!=null){localScale.x=ArtAnimation.Sample(binding.Clip,n.path,"m_LocalScale.x",binding.ClipTime,localScale.x);localScale.y=ArtAnimation.Sample(binding.Clip,n.path,"m_LocalScale.y",binding.ClipTime,localScale.y);}
                Vector2 scale=p.scale*localScale;Vector2 size,position;var pivot=new Vector2(.5f,.5f);
                if(n.rect)
                {
                    var amin=new Vector2(n.anchorMin[0],n.anchorMin[1]);var amax=new Vector2(n.anchorMax[0],n.anchorMax[1]);pivot=new Vector2(n.pivot[0],n.pivot[1]);
                    size=p.size*(amax-amin)+new Vector2(n.size[0],n.size[1]);var anchored=new Vector2(n.anchored[0],n.anchored[1]);
                    if(binding!=null){anchored.x=ArtAnimation.Sample(binding.Clip,n.path,"m_AnchoredPosition.x",binding.ClipTime,anchored.x);anchored.y=ArtAnimation.Sample(binding.Clip,n.path,"m_AnchoredPosition.y",binding.ClipTime,anchored.y);}
                    position=p.size*(amin+(amax-amin)*pivot)+anchored;
                }
                else {size=new Vector2(100,100);position=p.size*.5f+new Vector2(n.position[0],n.position[1]);}
                if(n.layoutGroup?.padding!=null&&(n.fitWidth>0||n.fitHeight>0)){var preferred=PreferredGroupSize(data,i,size,binding);if(n.fitWidth>0)size.x=preferred.x;if(n.fitHeight>0)size.y=preferred.y;}
                if(grouped.TryGetValue(i,out var arranged)){size=arranged.size;position=arranged.position+size*pivot;}
                float alpha=p.alpha*(binding==null?1:ArtAnimation.Sample(binding.Clip,n.path,"m_Alpha",binding.ClipTime,1));
                Vector2 origin=p.origin+position*p.scale-size*pivot*scale;state[i]=new NodeState{origin=origin,size=size,scale=scale,bounds=new Rect(origin,size*scale),active=visible[i],alpha=alpha};ArrangeGroup(data,i,state[i],grouped,binding);
            }
            return state;
        }
        static void ArrangeGroup(ArtLayout data,int parent,NodeState state,Dictionary<int,Rect> output,ArtBinding binding)
        {
            var group=data.nodes[parent].layoutGroup;if(group==null||group.padding==null)return;
            var children=new List<int>();var sizes=new List<Vector2>();float total=0;
            for(int i=parent+1;i<data.nodes.Length;i++)
            {
                var n=data.nodes[i];if(n.parent!=parent||n.ignoreLayout||!n.rect)continue;bool active=binding?.Visible!=null?binding.Visible(n.path,n.active):n.active;if(!active)continue;
                var size=state.size*new Vector2(n.anchorMax[0]-n.anchorMin[0],n.anchorMax[1]-n.anchorMin[1])+new Vector2(n.size[0],n.size[1]);
                foreach(var v in n.visuals)if(v.type=="text")
                {
                    var text=binding?.Text!=null?binding.Text(n.path,v.text):v.text;var measured=Ink.Measure(text,Mathf.RoundToInt(v.size),Typeface(v.fontName));
                    if((group.controlWidth||n.fitWidth>0)&&size.x<=0)size.x=measured.x;if((group.controlHeight||n.fitHeight>0)&&size.y<=0)size.y=measured.y;
                }
                if(group.controlWidth&&n.preferredWidth>0)size.x=n.preferredWidth;if(group.controlHeight&&n.preferredHeight>0)size.y=n.preferredHeight;
                size.x=Mathf.Max(0,size.x);size.y=Mathf.Max(0,size.y);children.Add(i);sizes.Add(size);total+=size[group.axis];
            }
            if(children.Count==0)return;total+=group.spacing*(children.Count-1);
            float left=group.padding[0],right=group.padding[1],top=group.padding[2],bottom=group.padding[3];float available=group.axis==0?state.size.x-left-right:state.size.y-top-bottom;
            bool expand=group.axis==0?group.expandWidth:group.expandHeight;float extra=Mathf.Max(0,available-total);float flexible=expand?extra/children.Count:0;
            float ax=(group.alignment%3)*.5f,ay=(group.alignment/3)*.5f;float cursor=group.axis==0?left+(expand?0:extra*ax):state.size.y-top-(expand?0:extra*ay);
            for(int k=0;k<children.Count;k++)
            {
                var size=sizes[k];float cell=size[group.axis]+flexible;
                if(group.axis==0&&group.controlWidth)size.x=cell;if(group.axis==1&&group.controlHeight)size.y=cell;
                if(group.axis==0&&group.controlHeight&&group.expandHeight)size.y=Mathf.Max(size.y,state.size.y-top-bottom);
                if(group.axis==1&&group.controlWidth&&group.expandWidth)size.x=Mathf.Max(size.x,state.size.x-left-right);
                float x,y;
                if(group.axis==0){x=cursor+(cell-size.x)*ax;y=bottom+(state.size.y-top-bottom-size.y)*(1-ay);cursor+=cell+group.spacing;}
                else{x=left+(state.size.x-left-right-size.x)*ax;y=cursor-(cell-size.y)*ay-size.y;cursor-=cell+group.spacing;}
                output[children[k]]=new Rect(x,y,size.x,size.y);
            }
        }
        static Vector2 PreferredGroupSize(ArtLayout data,int parent,Vector2 parentSize,ArtBinding binding)
        {
            var group=data.nodes[parent].layoutGroup;var preferred=Vector2.zero;int count=0;
            for(int i=parent+1;i<data.nodes.Length;i++)
            {
                var n=data.nodes[i];if(n.parent!=parent||n.ignoreLayout||!n.rect)continue;bool active=binding?.Visible!=null?binding.Visible(n.path,n.active):n.active;if(!active)continue;
                var size=parentSize*new Vector2(n.anchorMax[0]-n.anchorMin[0],n.anchorMax[1]-n.anchorMin[1])+new Vector2(n.size[0],n.size[1]);
                foreach(var v in n.visuals)if(v.type=="text"){var text=binding?.Text!=null?binding.Text(n.path,v.text):v.text;var measured=Ink.Measure(text,Mathf.RoundToInt(v.size),Typeface(v.fontName));if(size.x<=0)size.x=measured.x;if(size.y<=0)size.y=measured.y;}
                if(group.axis==0){preferred.x+=Mathf.Max(0,size.x);preferred.y=Mathf.Max(preferred.y,size.y);}else{preferred.y+=Mathf.Max(0,size.y);preferred.x=Mathf.Max(preferred.x,size.x);}count++;
            }
            preferred[group.axis]+=Mathf.Max(0,count-1)*group.spacing;preferred+=new Vector2(group.padding[0]+group.padding[1],group.padding[2]+group.padding[3]);return preferred;
        }
        static void DrawGradient(ArtVisual visual,Rect rect,Color tint)
        {
            if(!gradients.TryGetValue(visual,out var texture))
            {
                var gradient=new Gradient();var colors=new GradientColorKey[visual.gradientKeys.Length];var alphas=new GradientAlphaKey[visual.alphaKeys.Length];
                for(int i=0;i<colors.Length;i++){var k=visual.gradientKeys[i];colors[i]=new GradientColorKey(new Color(k.rgba[0],k.rgba[1],k.rgba[2]),k.time);}for(int i=0;i<alphas.Length;i++)alphas[i]=new GradientAlphaKey(visual.alphaKeys[i].alpha,visual.alphaKeys[i].time);gradient.SetKeys(colors,alphas);
                bool horizontal=visual.direction<2;texture=new Texture2D(horizontal?256:1,horizontal?1:256,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;
                for(int p=0;p<256;p++){float t=p/255f;if(visual.direction==1||visual.direction==3)t=1-t;texture.SetPixel(horizontal?p:0,horizontal?0:p,gradient.Evaluate(t));}texture.Apply();gradients[visual]=texture;
            }
            Color old=GUI.color;GUI.color=tint;GUI.DrawTexture(rect,texture);GUI.color=old;
        }
        static Rect ToGUI(Rect r,Rect target,Vector2 design)=>new Rect(target.x+r.x/design.x*target.width,target.y+(design.y-r.y-r.height)/design.y*target.height,r.width/design.x*target.width,r.height/design.y*target.height);
        public static bool Layout(string name,Rect target,Vector2 design,ArtBinding binding=null)
        {
            var data=GetLayout(name);if(data==null)return false;var state=Resolve(data,design,binding);var active=new bool[state.Length];
            for(int i=0;i<data.nodes.Length;i++)
            {
                var n=data.nodes[i];bool own=binding?.Visible!=null?binding.Visible(n.path,n.active):n.active;active[i]=own&&(n.parent<0||active[n.parent]);if(!active[i])continue;
                var r=ToGUI(state[i].bounds,target,design);if(r.width<=0||r.height<=0)continue;
                foreach(var visual in n.visuals)
                {
                    if(!visual.enabled||n.hideGraphic&&(visual.type=="image"||visual.type=="gradient"))continue;var c=new Color(visual.color[0],visual.color[1],visual.color[2],visual.color[3]);if(binding!=null)c.a=ArtAnimation.Sample(binding.Clip,n.path,visual.type=="text"?"m_fontColor.a":"m_Color.a",binding.ClipTime,c.a);c.a*=state[i].alpha;if(binding?.Color!=null)c=binding.Color(n.path,c);if(c.a<=0)continue;
                    if(visual.type=="gradient"){DrawGradient(visual,r,c);}
                    else if(visual.type=="image")
                    {
                        if(string.IsNullOrEmpty(visual.sprite)){Ink.Rect(r,c);continue;}
                        string sprite=binding?.Sprite!=null?binding.Sprite(n.path,visual.sprite):visual.sprite;Draw(sprite,r,c,visual.preserveAspect,visual.imageType==1);
                    }
                    else if(visual.type=="text")
                    {
                        string text=binding?.Text!=null?binding.Text(n.path,visual.text):visual.text;if(string.IsNullOrEmpty(text))continue;
                        var align=visual.alignment==1?TextAnchor.MiddleLeft:visual.alignment==4?TextAnchor.MiddleRight:TextAnchor.MiddleCenter;
                        int fontSize=Mathf.RoundToInt(visual.size*target.width/design.x*Mathf.Abs(state[i].scale.x));
                        var face=Typeface(visual.fontName);if(visual.autoSize){var measured=Ink.Measure(text,fontSize,face);if(measured.x>r.width)fontSize=Mathf.Max(Mathf.RoundToInt(visual.minSize*target.width/design.x),Mathf.FloorToInt(fontSize*r.width/measured.x));}
                        Ink.TextAt(r,text,Mathf.Max(fontSize,8),c,align,visual.weight>=600,face);
                    }
                }
            }
            return true;
        }
        public static bool IconButton(Rect r,string icon,string label=null)
        {
            bool small=r.width<=100;if(!small)Draw("propbtnbg",r,Accent,false,true);
            var iconRect=new Rect(r.x+r.width*.18f,r.y+r.height*.17f,r.width*.64f,r.height*.64f);
            if(icon=="restart")Ink.TextAt(iconRect,"↻",Mathf.RoundToInt(r.width*.53f),small?Foreground:Color.white,TextAnchor.MiddleCenter,true);else Draw(icon,iconRect,small?Foreground:Color.white,true);
            if(!string.IsNullOrEmpty(label))Ink.TextAt(new Rect(r.x-20,r.yMax+8,r.width+40,45),label,25,Foreground,TextAnchor.MiddleCenter,true);
            return Ink.HitButton(r,GUIContent.none,GUIStyle.none);
        }
        public static bool Button(Rect r,string text,Color? color=null,int fontSize=42)
        {
            Draw("CommonBtnBig",r,color??Accent,false,true);
            Ink.TextAt(r,text,fontSize,Color.white,TextAnchor.MiddleCenter,true);return Ink.HitButton(r,GUIContent.none,GUIStyle.none);
        }
    }
}
