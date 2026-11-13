using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    public sealed class OneLineBoard
    {
        public readonly LevelData Level; public readonly bool[] Used;public readonly List<int> Trail=new List<int>();
        public OneLineBoard(LevelData l) {Level=l;Used=new bool[l.edges.Length];}
        public bool Visit(int node)
        {
            if(node<0||node>=Level.nodes.Length)return false;
            if(Trail.Count==0){Trail.Add(node);return true;}
            int from=Trail[Trail.Count-1]; if(from==node)return true;
            for(int i=0;i<Level.edges.Length;i++) {var e=Level.edges[i];if(!Used[i]&&((e.a==from&&e.b==node)||(e.a==node&&e.b==from))) {Used[i]=true;Trail.Add(node);return true;}}
            return false;
        }
        public void Undo()
        {
            if(Trail.Count<2){Trail.Clear();return;}int a=Trail[Trail.Count-2],b=Trail[Trail.Count-1];
            for(int i=0;i<Used.Length;i++){var e=Level.edges[i];if(Used[i]&&((e.a==a&&e.b==b)||(e.a==b&&e.b==a))){Used[i]=false;break;}}Trail.RemoveAt(Trail.Count-1);
        }
        public bool Complete {get {foreach(bool used in Used)if(!used)return false;return true;}}
        public int NextHint() {if(Trail.Count==0)return Level.solution[0];for(int i=0;i<Level.solution.Length-1;i++){if(i<Trail.Count&&Trail[i]!=Level.solution[i])return -1;if(i==Trail.Count-1)return Level.solution[i+1];}return -1;}
    }
    public sealed class LinkBoard
    {
        public readonly LevelData Level; public readonly List<Vector2>[] Paths;public readonly bool[] Connected;public int Drawing=-1;
        public LinkBoard(LevelData l){Level=l;Paths=new List<Vector2>[l.pairs.Length];Connected=new bool[Paths.Length];for(int i=0;i<Paths.Length;i++)Paths[i]=new List<Vector2>();}
        public bool Begin(Vector2 p,float radius)
        {
            for(int i=0;i<Level.pairs.Length;i++)foreach(var end in Level.pairs[i].points)if(Vector2.Distance(p,end.V)<radius){Drawing=i;Paths[i].Clear();Paths[i].Add(end.V);Connected[i]=false;return true;}
            return false;
        }
        public bool Move(Vector2 p,float radius)
        {
            if(Drawing<0)return false;var path=Paths[Drawing];if(Vector2.Distance(p,path[path.Count-1])<.035f)return true;
            if(Crosses(path[path.Count-1],p))return false;
            path.Add(p);var pair=Level.pairs[Drawing];var end=Vector2.Distance(path[0],pair.points[0].V)<.01f?pair.points[1].V:pair.points[0].V;
            if(Vector2.Distance(p,end)<radius&&!Crosses(p,end)){path.Add(end);Connected[Drawing]=true;Drawing=-1;}return true;
        }
        bool Crosses(Vector2 a,Vector2 b)
        {
            for(int k=0;k<Paths.Length;k++)
            {
                var points=Paths[k];int limit=points.Count-1-(k==Drawing?1:0);
                for(int i=0;i<limit;i++)if(Intersect(a,b,points[i],points[i+1]))return true;
            }
            // A line may not cut through an endpoint belonging to another color.
            for(int k=0;k<Level.pairs.Length;k++)if(k!=Drawing)foreach(var end in Level.pairs[k].points)if(DistanceToSegment(end.V,a,b)<.12f)return true;
            return false;
        }
        public void End(){if(Drawing>=0){Paths[Drawing].Clear();Drawing=-1;}}
        public bool Complete {get{foreach(bool c in Connected)if(!c)return false;return true;}}
        public static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public static bool Intersect(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            float den=Cross(b-a,d-c);if(Mathf.Abs(den)<.00001f)
            {
                if(Mathf.Abs(Cross(c-a,b-a))>.0001f)return false;
                return DistanceToSegment(c,a,b)<.001f||DistanceToSegment(d,a,b)<.001f||DistanceToSegment(a,c,d)<.001f||DistanceToSegment(b,c,d)<.001f;
            }
            float t=Cross(c-a,d-c)/den,u=Cross(c-a,b-a)/den;return t>=0&&t<=1&&u>=0&&u<=1;
        }
        public static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b){var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.000001f,d.sqrMagnitude)));}
    }
    public sealed class SchulteBoard
    {
        public readonly int Size;public readonly int[] Numbers;public int Next=1;
        public SchulteBoard(int size,int seed)
        {
            Size=size;Numbers=new int[size*size];for(int i=0;i<Numbers.Length;i++)Numbers[i]=i+1;
            var random=new System.Random(seed);for(int i=Numbers.Length-1;i>0;i--){int j=random.Next(i+1);int t=Numbers[i];Numbers[i]=Numbers[j];Numbers[j]=t;}
        }
        public bool Tap(int i){if(i<0||i>=Numbers.Length||Numbers[i]!=Next)return false;Next++;return true;}
        public bool Complete=>Next>Numbers.Length;
    }
}
