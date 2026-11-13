using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    [Serializable] public class ArtKey {public float time,value,incoming,outgoing;}
    [Serializable] public class ArtChannel {public string path,property;public ArtKey[] keys;}
    [Serializable] public class ArtClip {public string name,source;public float duration;public bool loop;public ArtChannel[] channels;}
    [Serializable] public class ArtClipCatalog {public ArtClip[] clips;}
    public static class ArtAnimation
    {
        static Dictionary<string,ArtClip> clips;static readonly Dictionary<string,AnimationCurve> curves=new Dictionary<string,AnimationCurve>();
        public static int Count {get {Load();return clips.Count;}}
        static void Load(){if(clips!=null)return;clips=new Dictionary<string,ArtClip>();var text=Resources.Load<TextAsset>("OriginalArt/animations");if(text)foreach(var clip in JsonUtility.FromJson<ArtClipCatalog>(text.text).clips)clips[clip.name]=clip;}
        public static float Sample(string name,string path,string property,float time,float fallback)
        {
            if(string.IsNullOrEmpty(name))return fallback;Load();if(!clips.TryGetValue(name,out var clip))return fallback;
            int first=path.IndexOf('/',1);string relative=first<0?"":path.Substring(first+1);string key=name+"|"+relative+"|"+property;
            if(!curves.TryGetValue(key,out var curve))
            {
                foreach(var c in clip.channels)if(c.path==relative&&c.property==property)
                {
                    var keys=new Keyframe[c.keys.Length];for(int i=0;i<keys.Length;i++){var k=c.keys[i];keys[i]=new Keyframe(k.time,k.value,Mathf.Abs(k.incoming)>1e20f?float.PositiveInfinity:k.incoming,Mathf.Abs(k.outgoing)>1e20f?float.PositiveInfinity:k.outgoing);}curve=new AnimationCurve(keys);break;
                }
                curves[key]=curve;
            }
            if(curve==null)return fallback;float t=clip.loop&&clip.duration>0?Mathf.Repeat(time,clip.duration):Mathf.Clamp(time,0,clip.duration);return curve.Evaluate(t);
        }
    }
}
