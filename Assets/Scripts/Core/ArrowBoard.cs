using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    public sealed class ArrowBoard
    {
        public readonly LevelData Level; public readonly bool[] Active; public readonly List<Vector3>[] Paths;
        public readonly Dictionary<Vector3Int,int> Occupancy = new Dictionary<Vector3Int,int>();
        readonly Dictionary<long,int> surfaceOccupancy=new Dictionary<long,int>();
        readonly Dictionary<int,int> parkedAt=new Dictionary<int,int>();
        public int Remaining { get; private set; }
        public int TargetCount {get;private set;}
        public readonly bool Cube;
        public ArrowBoard(LevelData level, bool cube = false)
        {
            Level=level; Cube=cube; Active=new bool[level.arrows.Length]; Paths=new List<Vector3>[Active.Length];
            for(int i=0;i<Active.Length;i++) { Active[i]=true; Paths[i]=Expand(level.arrows[i].points);if(!IsMechanism(i))TargetCount++; }
            Remaining=TargetCount;
            Rebuild();
        }
        public Vector3 Decode(int n)
        {
            if(!Cube) return new Vector3(n%Level.width,n/Level.width,0);
            int face=n%6, cell=n/6;
            var p = new Vector3(cell%Level.width,(cell/Level.width)%Level.height,cell/(Level.width*Level.height));
            // Six encoded face IDs share voxel coordinates; a slight normal offset separates faces for display.
            return p + FaceNormal(face)*0.06f;
        }
        public static Vector3 FaceNormal(int f) => f==0 ? Vector3.right : f==1 ? Vector3.left : f==2 ? Vector3.up : f==3 ? Vector3.down : f==4 ? Vector3.forward : Vector3.back;
        List<Vector3> Expand(int[] points)
        {
            var result=new List<Vector3>(); if(points==null || points.Length==0) return result;
            // Observed recovered convention: points[0] is the head. Store tail -> head internally.
            for(int i=points.Length-1;i>=0;i--)
            {
                var p=Decode(points[i]);
                if(result.Count>0)
                {
                    var last=result[result.Count-1]; var delta=p-last;
                    int steps=Mathf.RoundToInt(Mathf.Max(Mathf.Abs(delta.x),Mathf.Abs(delta.y),Mathf.Abs(delta.z)));
                    for(int s=1;s<steps;s++) result.Add(Vector3.Lerp(last,p,(float)s/steps));
                }
                result.Add(p);
            }
            return result;
        }
        public Vector3 Head(int i) => Paths[i][Paths[i].Count-1];
        public Vector3 Direction(int i)
        {
            var p=Paths[i]; if(p.Count<2)return Vector3.right;
            var d=p[p.Count-1]-p[p.Count-2];
            int axis=Mathf.Abs(d.x)>=Mathf.Abs(d.y)&&Mathf.Abs(d.x)>=Mathf.Abs(d.z)?0:Mathf.Abs(d.y)>=Mathf.Abs(d.z)?1:2;
            var v=Vector3.zero;v[axis]=Mathf.Sign(d[axis]);return v;
        }
        public static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
        void Rebuild()
        {
            Occupancy.Clear();surfaceOccupancy.Clear();for(int i=0;i<Active.Length;i++)if(Active[i])foreach(var p in Paths[i]){Occupancy[Key(p)]=i;if(Cube)surfaceOccupancy[SurfaceKey(p,Face(p))]=i;}
        }
        int Face(Vector3 p){var delta=p-(Vector3)Key(p);if(Mathf.Abs(delta.x)>=Mathf.Abs(delta.y)&&Mathf.Abs(delta.x)>=Mathf.Abs(delta.z))return delta.x>=0?0:1;if(Mathf.Abs(delta.y)>=Mathf.Abs(delta.z))return delta.y>=0?2:3;return delta.z>=0?4:5;}
        long SurfaceKey(Vector3 p,int face){var v=Key(p);return ((long)v.z*Level.height*Level.width+v.y*Level.width+v.x)*6+face;}
        public bool IsMechanism(int i)=>Cube&&(Level.arrows[i].special==3003||Level.arrows[i].special==3004);
        public bool CanExit(int i)
        {
            if(i<0 || i>=Active.Length || !Active[i] || Paths[i].Count==0 || IsMechanism(i)) return false;
            if(Cube)return CubeRoute(i,false,out _)||(Level.arrows[i].special==3002&&CubeRoute(i,true,out _));
            var pos=Head(i);var dir=Direction(i);
            int max=Level.width+Level.height+Mathf.Max(Level.depth,1)+4;
            for(int step=1;step<=max;step++)
            {
                var p=pos+dir*step;
                if(p.x<-.4f||p.y<-.4f||p.x>Level.width-.6f||p.y>Level.height-.6f|| (Cube&&(p.z<-.4f||p.z>Level.depth-.6f)))return true;
                if(Occupancy.TryGetValue(Key(p),out int other)&&other!=i) return false;
            }
            return true;
        }
        bool CubeRoute(int i,bool reverse,out int receiver)
        {
            receiver=-1;var path=Paths[i];var p=reverse?path[0]:Head(i);var d=reverse?(path.Count>1?(path[0]-path[1]).normalized:Vector3.right):Direction(i);
            int face=Face(p);var seen=new HashSet<long>();
            for(int step=0;step<Level.width*Level.height*Level.depth*6;step++)
            {
                p+=d;if(p.x<-.4f||p.y<-.4f||p.z<-.4f||p.x>Level.width-.6f||p.y>Level.height-.6f||p.z>Level.depth-.6f)return true;
                long key=SurfaceKey(p,face);if(!seen.Add(key))return false;
                if(surfaceOccupancy.TryGetValue(key,out int other)&&other!=i)
                {
                    var data=Level.arrows[other];
                    if(data.special==3003){receiver=other;return true;}
                    if(data.special==3004){if(int.TryParse(data.param,out int turn))d=FaceNormal(Mathf.Abs(turn)%6);continue;}
                    return false;
                }
            }
            return false;
        }
        public bool Remove(int i)
        {
            if(!CanExit(i))return false;
            if(Cube){if(!CubeRoute(i,false,out int receiver))CubeRoute(i,true,out receiver);if(receiver>=0){Active[receiver]=false;parkedAt[i]=receiver;}}
            Active[i]=false;Remaining--;Rebuild();return true;
        }
        public void Restore(int i) { if(i<0||i>=Active.Length||Active[i])return; Active[i]=true;if(parkedAt.TryGetValue(i,out int receiver)){Active[receiver]=true;parkedAt.Remove(i);}Remaining++;Rebuild(); }
        public int Hint() { for(int i=0;i<Active.Length;i++)if(CanExit(i))return i;return -1; }
        public int[] Solve()
        {
            var copy = new ArrowBoard(Level,Cube); var result=new List<int>();
            while(copy.Remaining>0) {int i=copy.Hint();if(i<0)break;copy.Remove(i);result.Add(i);}
            return result.ToArray();
        }
    }

    public sealed class MatchTray
    {
        public readonly List<int> Colors=new List<int>(); public const int Capacity=7;
        public bool Add(int color)
        {
            int at=Colors.LastIndexOf(color);Colors.Insert(at<0?Colors.Count:at+1,color);
            int count=0;foreach(int c in Colors)if(c==color)count++;
            if(count>=3) {for(int n=0;n<3;n++)Colors.Remove(color);return true;}return false;
        }
        public bool Full => Colors.Count>=Capacity;
    }

    public sealed class OctaBoard
    {
        public readonly LevelData Level;public readonly bool[] Active;public int MovesLeft;public int Remaining {get;private set;}
        public OctaBoard(LevelData l) {Level=l;Active=new bool[l.arrows.Length];for(int i=0;i<Active.Length;i++)Active[i]=true;Remaining=Active.Length;MovesLeft=l.moveLimit;}
        public bool CanExit(int index)
        {
            if(!Active[index])return false;var a=Level.arrows[index];
            return Ray(index,a.dx,a.dy) && (a.kind!="splitter" || Ray(index,a.dx2,a.dy2));
        }
        bool Ray(int i,int dx,int dy)
        {
            var a=Level.arrows[i];int start=a.points[0],x=start%Level.width,y=start/Level.width;
            var seen=new HashSet<string>();
            for(int step=0;step<Level.width*Level.height*16;step++)
            {
                x+=dx;y+=dy;if(x<0||y<0||x>=Level.width||y>=Level.height)return true;
                string state=x+","+y+","+dx+","+dy;if(!seen.Add(state))return false;
                int cell=y*Level.width+x;
                for(int j=0;j<Active.Length;j++)if(Active[j]&&j!=i)
                {
                    var other=Level.arrows[j];if(other.kind=="switch"&&other.open)continue;
                    foreach(int p in other.points)if(p==cell)return false;
                }
                if(Level.portals!=null)foreach(var portal in Level.portals)if(cell==portal.a||cell==portal.b) {int to=cell==portal.a?portal.b:portal.a;x=to%Level.width;y=to/Level.width;break;}
                if(Level.turns!=null)foreach(var turn in Level.turns)if(turn.x==x&&turn.y==y) {dx=turn.dx;dy=turn.dy;break;}
            }
            return false;
        }
        public bool Tap(int i)
        {
            if(i<0||i>=Active.Length||!Active[i]||MovesLeft<=0)return false;
            var a=Level.arrows[i];MovesLeft--;
            if(a.kind=="switch") {a.open=!a.open;return true;}
            if(a.kind=="rotateCW"||a.kind=="rotateCCW")
            {
                if(!CanExit(i)){Rotate(a,a.kind=="rotateCW"?1:-1);return true;}
            }
            if(!CanExit(i))return false;
            Active[i]=false;Remaining--;
            // An open switch is a gate rather than a target to clear.
            bool targets=false;for(int j=0;j<Active.Length;j++)if(Active[j]&&Level.arrows[j].kind!="switch")targets=true;
            if(!targets) {for(int j=0;j<Active.Length;j++)Active[j]=false;Remaining=0;}
            return true;
        }
        static void Rotate(ArrowData a,int step)
        {
            Vector2Int[] ds={new Vector2Int(1,0),new Vector2Int(1,1),new Vector2Int(0,1),new Vector2Int(-1,1),new Vector2Int(-1,0),new Vector2Int(-1,-1),new Vector2Int(0,-1),new Vector2Int(1,-1)};
            int at=0;for(int k=0;k<8;k++)if(ds[k].x==a.dx&&ds[k].y==a.dy)at=k;
            var d=ds[(at+step+8)%8];a.dx=d.x;a.dy=d.y;
        }
        public int Hint() {for(int i=0;i<Active.Length;i++)if(Active[i]&&Level.arrows[i].kind!="switch"&&CanExit(i))return i;return -1;}
    }
}
