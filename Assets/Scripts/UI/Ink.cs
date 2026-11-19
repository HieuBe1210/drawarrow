using UnityEngine;
using System.Collections.Generic;

namespace DrawArrow
{
    public static class Ink
    {
        public static System.Action Clicked;
        public static bool HitButton(Rect r,GUIContent content,GUIStyle style)
        {
            bool pressed=GUI.Button(r,content,style);
            if(pressed)Clicked?.Invoke();
            return pressed;
        }
        public static readonly Color Paper = Hex("F5ECDB"), Text = Hex("302F2D"), Muted = Hex("969184"), Teal = Hex("BF7737"), Gold = Hex("FFCE42"), Red = Hex("DA5245");
        public static readonly Color[] Palette = { Hex("DB473D"),Hex("4D7CE3"),Hex("63A94D"),Hex("DEB942"),Hex("9867C8"),Hex("D77BA3"),Hex("5AADD0") };
        static Texture2D circle, round, white,heart; static Font font,boldFont;static GUIStyle label;
        static readonly Dictionary<string,Texture2D> strokes=new Dictionary<string,Texture2D>();
        public static Color Hex(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}
        public static Color WithAlpha(Color c,float a){c.a=a;return c;}
        public static void Init()
        {
            if(white)return;
            white=Texture2D.whiteTexture; font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");boldFont=font;
            circle=new Texture2D(128,128,TextureFormat.RGBA32,false);circle.filterMode=FilterMode.Bilinear;
            round=new Texture2D(128,128,TextureFormat.RGBA32,false);round.filterMode=FilterMode.Bilinear;
            heart=new Texture2D(128,128,TextureFormat.RGBA32,false);heart.filterMode=FilterMode.Bilinear;heart.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(64,64));circle.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(64-d)));
                float qx=Mathf.Max(Mathf.Abs(x-63.5f)-40,0),qy=Mathf.Max(Mathf.Abs(y-63.5f)-40,0);
                float a=Mathf.Clamp01(23.5f-Mathf.Sqrt(qx*qx+qy*qy));round.SetPixel(x,y,new Color(1,1,1,a));
                int inside=0;
                for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)
                {
                    float hx=(x+(sx+.5f)*.5f)/128f*2.5f-1.25f;
                    float hy=(y+(sy+.5f)*.5f)/128f*2.5f-1.15f;
                    float q=hx*hx+hy*hy-1;
                    if(q*q*q-hx*hx*hy*hy*hy<=0)inside++;
                }
                heart.SetPixel(x,y,new Color(1,1,1,inside*.25f));
            }
            circle.Apply();round.Apply();heart.Apply();label=new GUIStyle {font=font,wordWrap=true,richText=true};
        }
        public static void Rect(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,white);GUI.color=old;}
        public static void Card(Rect r,Color c,float radius=24)
        {
            var old=GUI.color;GUI.color=Color.white;
            GUI.DrawTexture(r,white,ScaleMode.StretchToFill,true,0,c,0,radius);
            GUI.color=old;
        }
        public static void Circle(Vector2 center,float radius,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(center.x-radius,center.y-radius,radius*2,radius*2),circle);GUI.color=old;}
        public static void Heart(Rect rect,Color color){Init();var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,heart);GUI.color=old;}
        public static void Line(Vector2 a,Vector2 b,float width,Color c,bool caps=true)
        {
            var delta=b-a;if(delta.sqrMagnitude<.001f)return;
            if(Mathf.Abs(delta.x)<.01f){Rect(new Rect(a.x-width*.5f,Mathf.Min(a.y,b.y),width,Mathf.Abs(delta.y)),c);if(caps){Circle(a,width*.5f,c);Circle(b,width*.5f,c);}return;}
            if(Mathf.Abs(delta.y)<.01f){Rect(new Rect(Mathf.Min(a.x,b.x),a.y-width*.5f,Mathf.Abs(delta.x),width),c);if(caps){Circle(a,width*.5f,c);Circle(b,width*.5f,c);}return;}
            // Rasterize a white stroke inside its own axis-aligned bounds. This avoids
            // rotated GUI matrices interacting with nested clips and root scaling.
            float dx=Mathf.Round(Mathf.Abs(delta.x)*2)*.5f,dy=Mathf.Round(Mathf.Abs(delta.y)*2)*.5f;
            bool rising=delta.x*delta.y>0;string key=dx+":"+dy+":"+Mathf.Round(width*2)+":"+rising;
            if(!strokes.TryGetValue(key,out var texture))
            {
                if(strokes.Count>768){foreach(var t in strokes.Values)Object.Destroy(t);strokes.Clear();}
                float w=dx+width+2,h=dy+width+2;int tw=Mathf.Clamp(Mathf.CeilToInt(w),4,256),th=Mathf.Clamp(Mathf.CeilToInt(h),4,256);
                texture=new Texture2D(tw,th,TextureFormat.RGBA32,false);texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
                var start=new Vector2(width*.5f+1,rising?width*.5f+1:h-width*.5f-1);var end=new Vector2(w-width*.5f-1,rising?h-width*.5f-1:width*.5f+1);var pixels=new Color32[tw*th];
                for(int y=0;y<th;y++)for(int x=0;x<tw;x++){var p=new Vector2((x+.5f)*w/tw,(y+.5f)*h/th);float alpha=Mathf.Clamp01(width*.5f+.6f-LinkBoard.DistanceToSegment(p,start,end));pixels[(th-1-y)*tw+x]=new Color32(255,255,255,(byte)(alpha*255));}
                texture.SetPixels32(pixels);texture.Apply(false,true);strokes[key]=texture;
            }
            var old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(Mathf.Min(a.x,b.x)-width*.5f-1,Mathf.Min(a.y,b.y)-width*.5f-1,Mathf.Abs(delta.x)+width+2,Mathf.Abs(delta.y)+width+2),texture);GUI.color=old;
        }
        public static void Arrow(Vector2 a,Vector2 b,float width,Color c)
        {
            Line(a,b,width,c);var dir=(b-a).normalized;var side=new Vector2(-dir.y,dir.x);float size=Mathf.Max(width*2.4f,10);
            Line(b,b-dir*size+side*size*.62f,width,c);Line(b,b-dir*size-side*size*.62f,width,c);
        }
        public static void TextAt(Rect r,string text,int size,Color c,TextAnchor align=TextAnchor.UpperLeft,bool bold=false,Font face=null)
        {
            label.font=face?face:bold?boldFont:font;label.fontSize=size;label.normal.textColor=c;label.alignment=align;label.fontStyle=FontStyle.Normal;GUI.Label(r,text,label);
        }
        public static Vector2 Measure(string text,int size,Font face=null){Init();label.font=face?face:font;label.fontSize=size;label.fontStyle=FontStyle.Normal;return label.CalcSize(new GUIContent(text??""));}
        public static bool Button(Rect r,string text,Color fill,Color fg,int size=28)
        {
            bool hover=r.Contains(Event.current.mousePosition);Card(r,hover?Color.Lerp(fill,Color.white,.08f):fill);
            TextAt(r,text,size,fg,TextAnchor.MiddleCenter,true);return Ink.HitButton(r,GUIContent.none,GUIStyle.none);
        }
        public static void Curve(Vector2 a,Vector2 c,Vector2 b,float width,Color color)
        {
            var prev=a;for(int k=1;k<=24;k++){float t=k/24f;var p=(1-t)*(1-t)*a+2*(1-t)*t*c+t*t*b;Line(prev,p,width,color);prev=p;}
        }
    }
}
