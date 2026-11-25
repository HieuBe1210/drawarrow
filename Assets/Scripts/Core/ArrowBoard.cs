using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    public sealed class ArrowBoard
    {
        public const int FlatGridSteps = 100;
        public const int ExtendedGridSteps = 1000;
        public readonly LevelData Level; public readonly bool[] Active; public readonly List<Vector3>[] Paths;
        public readonly Dictionary<Vector3Int,int> Occupancy = new Dictionary<Vector3Int,int>();
        public readonly HashSet<Vector3> ClearedCells = new HashSet<Vector3>();
        public readonly List<Vector3>[] DotPositions;
        readonly Dictionary<long,int> surfaceOccupancy=new Dictionary<long,int>();
        readonly Dictionary<int,int> parkedAt=new Dictionary<int,int>();
        public int Remaining { get; private set; }
        public int TargetCount {get;private set;}
        public readonly bool Cube;
        public readonly Rect ContentBounds;
        public readonly Rect? ReferenceBounds;
        public SwitchGateData SwitchGates { get; }
        public bool SwitchFlipped { get; private set; }
        public int WorkshopTurnsRemaining { get; private set; }
        readonly Vector3[] workshopDirections;
        readonly Vector3[] flatDirections;
        readonly List<(int arrow, float distance)>[] diagonalBlockers;
        readonly Vector2Int flatSize;
        readonly bool compactLayout;
        readonly bool preserveSourceLengths;
        readonly bool searchHoles;
        public ArrowBoard(LevelData level, bool cube = false, bool fitInitialGaps = false,
            bool compactLayout = false, bool preserveSourceLengths = false, bool searchHoles = false)
        {
            Level=level; Cube=cube; Active=new bool[level.arrows.Length]; Paths=new List<Vector3>[Active.Length];DotPositions=new List<Vector3>[Active.Length];
            SwitchGates = cube ? null : (level as ArrowSwitchLevelData)?.switchGates;
            if (!Cube && level is ArrowWorkshopLevelData workshop)
            {
                WorkshopTurnsRemaining = workshop.rotateBudget;
                workshopDirections = new Vector3[Active.Length];
            }
            if(!Cube&&level.followDiagonalPaths)
                diagonalBlockers=new List<(int arrow, float distance)>[Active.Length];
            else if(!Cube&&fitInitialGaps&&SwitchGates==null&&workshopDirections==null)flatDirections=new Vector3[Active.Length];
            for(int i=0;i<Active.Length;i++)
            {
                Active[i]=true;Paths[i]=Expand(level.arrows[i].points);
                if(flatDirections!=null)
                {
                    flatDirections[i]=Cardinal(Direction(i));
                    continue;
                }
                if(!Cube&&Paths[i].Count==1)
                {
                    // Both ends of a source one-cell arrow fit in its occupancy cell.
                    var center=Paths[i][0];var direction=Direction(i);
                    Paths[i][0]=center-direction*.3f;Paths[i].Add(center+direction*.3f);
                }
                if(!Cube&&diagonalBlockers==null)TrimSelfPointingTail(Paths[i],Direction(i));
            }
            flatSize=new Vector2Int(level.width,level.height);
            if(flatDirections!=null&&level.packedFitBounds!=null&&level.packedFitBounds.Length==2)
            {
                // The packed routes were already checked for clearance and solvability.
                ReferenceBounds=new Rect(0,0,level.packedFitBounds[0],level.packedFitBounds[1]);
                int widthSteps=level.width-1>FlatGridSteps?ExtendedGridSteps:FlatGridSteps;
                flatSize=new Vector2Int(Mathf.Max(level.width,widthSteps+1),Mathf.Max(level.height,FlatGridSteps+1));
            }
            else if(flatDirections!=null)
            {
                this.preserveSourceLengths=preserveSourceLengths;
                if(preserveSourceLengths)
                {
                    var reference=new ArrowBoard(level);
                    ReferenceBounds=reference.ContentBounds;
                    for(int i=0;i<Paths.Length;i++)
                    {
                        var visible=reference.Paths[i];
                        if(visible.Count>=Paths[i].Count)continue;
                        var route=Paths[i];
                        route.Clear();
                        foreach(var point in visible)
                        {
                            var node=(Vector3)Key(point);
                            if(route.Count==0||route[route.Count-1]!=node)route.Add(node);
                        }
                    }
                }
                var layout=new FlatArrowLayout(Paths,flatDirections,level.width,level.height,preserveSourceLengths);
                flatSize=layout.FitInitialGaps(level.arrowVisualScale);
                this.compactLayout=compactLayout;
                this.searchHoles=searchHoles;
                if(compactLayout)layout.CompactPlacedPaths(flatSize,searchHoles);
            }
            for(int i=0;i<Active.Length;i++)
            {
                DotPositions[i]=StraightSegmentVertices(Paths[i]);if(!IsMechanism(i))TargetCount++;
            }
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach(var path in Paths)foreach(var p in path){min=Vector2.Min(min,new Vector2(p.x,p.y));max=Vector2.Max(max,new Vector2(p.x,p.y));}
            if (SwitchGates != null || workshopDirections != null)
            {
                min = Vector2.zero;
                max = new Vector2(level.width - 1, level.height - 1);
            }
            ContentBounds=float.IsInfinity(min.x)?new Rect(0,0,Mathf.Max(0,level.width-1),Mathf.Max(0,level.height-1)):Rect.MinMaxRect(min.x,min.y,max.x,max.y);
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
                if(result.Count>0&&diagonalBlockers==null)
                {
                    var last=result[result.Count-1]; var delta=p-last;
                    if(flatDirections!=null)
                    {
                        var cell=Key(last);
                        var end=Key(p);
                        while(cell.x!=end.x)
                        {
                            cell.x+=(end.x>cell.x?1:-1)*FlatArrowLayout.GridStep;
                            result.Add(cell);
                        }
                        while(cell.y!=end.y)
                        {
                            cell.y+=(end.y>cell.y?1:-1)*FlatArrowLayout.GridStep;
                            result.Add(cell);
                        }
                        continue;
                    }
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
            if (workshopDirections != null && workshopDirections[i] != Vector3.zero) return workshopDirections[i];
            if(flatDirections!=null&&flatDirections[i]!=Vector3.zero)return flatDirections[i];
            var data=Level.arrows[i];
            if(!Cube&&(data.dx!=0||data.dy!=0))return new Vector3(data.dx,data.dy,0);
            var p=Paths[i]; if(p.Count<2)return Vector3.right;
            var d=p[p.Count-1]-p[p.Count-2];
            if(diagonalBlockers!=null)return d;
            int axis=Mathf.Abs(d.x)>=Mathf.Abs(d.y)&&Mathf.Abs(d.x)>=Mathf.Abs(d.z)?0:Mathf.Abs(d.y)>=Mathf.Abs(d.z)?1:2;
            var v=Vector3.zero;v[axis]=Mathf.Sign(d[axis]);return v;
        }
        static Vector3 Cardinal(Vector3 direction)
        {
            if(Mathf.Abs(direction.x)>=Mathf.Abs(direction.y))return Vector3.right*Mathf.Sign(direction.x);
            return Vector3.up*Mathf.Sign(direction.y);
        }
        internal static void TrimFlatTail(List<Vector3> path,Vector3 direction)
        {
            if(path.Count<2)return;
            var head=path[path.Count-1];
            for(int i=path.Count-2;i>=0;i--)
            {
                var delta=path[i]-head;
                if(Vector3.Dot(delta,direction)>0&&Vector3.Cross(delta,direction).sqrMagnitude==0)
                {
                    path.RemoveRange(0,i+1);
                    return;
                }
            }
        }
        public static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
        // Normalize once, before rendering, occupancy, hit testing and footprint
        // generation. Remove the tail prefix through the LAST crossing of the
        // head's forward ray, so multiple loops cannot leave another self blocker.
        public static bool TrimSelfPointingTail(List<Vector3> path,Vector3 direction)
        {
            if(path.Count<2||direction.sqrMagnitude<.000001f)return false;
            direction.Normalize();var head=path[path.Count-1];
            const float clearance=.4f,margin=.6f;
            float length=0,cut=-1;
            for(int i=1;i<path.Count;i++)
            {
                var a=path[i-1]-head;var b=path[i]-head;
                float segment=Vector3.Distance(path[i-1],path[i]);
                float fa=Vector3.Dot(a,direction),fb=Vector3.Dot(b,direction);
                if(Mathf.Max(fa,fb)>.00001f)
                {
                    var side=a-direction*fa;var delta=b-direction*fb-side;
                    float aa=delta.sqrMagnitude,bb=2*Vector3.Dot(side,delta),cc=side.sqrMagnitude-clearance*clearance;
                    float low=0,high=1;bool intersects=true;
                    if(aa<.0000001f)intersects=cc<=0;
                    else
                    {
                        float discriminant=bb*bb-4*aa*cc;
                        if(discriminant<0)intersects=false;
                        else
                        {
                            float root=Mathf.Sqrt(discriminant);
                            low=Mathf.Max(0,(-bb-root)/(2*aa));high=Mathf.Min(1,(-bb+root)/(2*aa));
                            intersects=low<=high;
                        }
                    }
                    if(intersects&&Mathf.Max(Mathf.Lerp(fa,fb,low),Mathf.Lerp(fa,fb,high))>.00001f)
                        cut=Mathf.Max(cut,length+segment*high+margin);
                }
                length+=segment;
            }
            if(cut<0)return false;
            if(cut>=length-.001f)
            {
                // Malformed/backwards source directions still keep a visible
                // head and one short segment strictly behind it.
                path.Clear();path.Add(head-direction*margin);path.Add(head);return true;
            }
            float walked=0;
            for(int i=1;i<path.Count;i++)
            {
                float segment=Vector3.Distance(path[i-1],path[i]);
                if(walked+segment>=cut)
                {
                    var tail=Vector3.Lerp(path[i-1],path[i],(cut-walked)/segment);
                    path.RemoveRange(0,i);path.Insert(0,tail);return true;
                }
                walked+=segment;
            }
            return false;
        }
        static List<Vector3> StraightSegmentVertices(List<Vector3> path)
        {
            var vertices=new List<Vector3>();
            foreach(var point in path)
            {
                var cell=point;int count=vertices.Count;
                if(count>0&&vertices[count-1]==cell)continue;
                if(count>1)
                {
                    var before=(Vector3)(vertices[count-1]-vertices[count-2]);
                    var after=(Vector3)(cell-vertices[count-1]);
                    if(Vector3.Dot(before.normalized,after.normalized)>.999f)vertices.RemoveAt(count-1);
                }
                vertices.Add(cell);
            }
            return vertices;
        }
        void Rebuild()
        {
            Occupancy.Clear();surfaceOccupancy.Clear();for(int i=0;i<Active.Length;i++)if(Active[i])foreach(var p in Paths[i]){Occupancy[Key(p)]=i;if(Cube)surfaceOccupancy[SurfaceKey(p,Face(p))]=i;}
            ClearedCells.Clear();
            if(!Cube)for(int i=0;i<Active.Length;i++)if(!Active[i])foreach(var cell in DotPositions[i])ClearedCells.Add(cell);
        }
        int Face(Vector3 p){var delta=p-(Vector3)Key(p);if(Mathf.Abs(delta.x)>=Mathf.Abs(delta.y)&&Mathf.Abs(delta.x)>=Mathf.Abs(delta.z))return delta.x>=0?0:1;if(Mathf.Abs(delta.y)>=Mathf.Abs(delta.z))return delta.y>=0?2:3;return delta.z>=0?4:5;}
        long SurfaceKey(Vector3 p,int face){var v=Key(p);return ((long)v.z*Level.height*Level.width+v.y*Level.width+v.x)*6+face;}
        public bool IsMechanism(int i)=>Cube&&(Level.arrows[i].special==3003||Level.arrows[i].special==3004);
        public bool CanExit(int i)
        {
            if(i<0 || i>=Active.Length || !Active[i] || Paths[i].Count==0 || IsMechanism(i)) return false;
            if(Cube)return CubeRoute(i,false,out _)||(Level.arrows[i].special==3002&&CubeRoute(i,true,out _));
            if (!float.IsPositiveInfinity(ClosedGateDistance(i))) return false;
            if(diagonalBlockers!=null)return float.IsPositiveInfinity(DiagonalBlockerDistance(i));
            var pos=Head(i);var dir=Direction(i);
            int max=flatSize.x+flatSize.y+Mathf.Max(Level.depth,1)+4;
            for(int step=FlatArrowLayout.GridStep;step<=max;step+=FlatArrowLayout.GridStep)
            {
                var p=pos+dir*step;
                if(p.x<-.4f||p.y<-.4f||p.x>flatSize.x-.6f||p.y>flatSize.y-.6f)return true;
                if(flatDirections!=null&&(p.x<ContentBounds.xMin||p.y<ContentBounds.yMin||p.x>ContentBounds.xMax||p.y>ContentBounds.yMax))return true;
                if(Occupancy.TryGetValue(Key(p),out int other)&&other!=i) return false;
            }
            return true;
        }
        public float BlockedTravel(int i)
        {
            if(Cube||i<0||i>=Active.Length||!Active[i]||Paths[i].Count==0)return 0;
            if(diagonalBlockers!=null)
            {
                float distance=DiagonalBlockerDistance(i);
                return float.IsPositiveInfinity(distance)?0:Mathf.Max(0,distance-.12f);
            }
            var head=Head(i);var direction=Direction(i);
            float gateDistance = ClosedGateDistance(i);
            for(int step=FlatArrowLayout.GridStep;step<=flatSize.x+flatSize.y+4;step+=FlatArrowLayout.GridStep)
            {
                var point=head+direction*step;
                if(point.x<-.4f||point.y<-.4f||point.x>flatSize.x-.6f||point.y>flatSize.y-.6f)break;
                if(flatDirections!=null&&(point.x<ContentBounds.xMin||point.y<ContentBounds.yMin||point.x>ContentBounds.xMax||point.y>ContentBounds.yMax))break;
                if(Occupancy.TryGetValue(Key(point),out int other)&&other!=i)
                    return Mathf.Max(0,Mathf.Min(step*direction.magnitude, gateDistance)-.12f);
            }
            return float.IsPositiveInfinity(gateDistance) ? 0 : Mathf.Max(0, gateDistance - .35f);
        }

        float ClosedGateDistance(int arrow)
        {
            var gates = SwitchGates;
            if (gates == null) return float.PositiveInfinity;
            var span = SwitchFlipped ? gates.initiallyOpen : gates.initiallyClosed;
            return RaySegmentDistance(Head(arrow), Direction(arrow).normalized, Decode(span.start), Decode(span.end));
        }

        float SwitchCellDistance(int arrow, int cell)
        {
            var offset = Decode(cell) - Head(arrow);
            var direction = Direction(arrow).normalized;
            float distance = Vector3.Dot(offset, direction);
            return distance > 0 && (offset - direction * distance).sqrMagnitude < .001f
                ? distance : float.PositiveInfinity;
        }

        void ToggleSwitchFor(int arrow)
        {
            if (SwitchGates != null && !float.IsPositiveInfinity(SwitchCellDistance(arrow, SwitchGates.pad)))
                SwitchFlipped = !SwitchFlipped;
        }

        float DiagonalBlockerDistance(int index)
        {
            var blockers=diagonalBlockers[index];
            if(blockers==null)
            {
                blockers=diagonalBlockers[index]=new List<(int arrow, float distance)>();
                var head=(Vector2)Head(index);
                var direction=(Vector2)Direction(index);
                for(int other=0;other<Paths.Length;other++)
                {
                    if(other==index)continue;
                    var path=Paths[other];
                    float distance=float.PositiveInfinity;
                    for(int i=1;i<path.Count;i++)
                        distance=Mathf.Min(distance,RaySegmentDistance(head,direction,path[i-1],path[i]));
                    if(!float.IsPositiveInfinity(distance))blockers.Add((other,distance*direction.magnitude));
                }
            }

            float nearest=float.PositiveInfinity;
            foreach(var blocker in blockers)
                if(Active[blocker.arrow])nearest=Mathf.Min(nearest,blocker.distance);
            return nearest;
        }

        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

        // Return distance in multiples of direction. A contact at the head
        // only blocks if the other shaft extends forward along the exit ray.
        static float RaySegmentDistance(Vector2 head,Vector2 direction,Vector2 start,Vector2 end)
        {
            var offset=start-head;
            var segment=end-start;
            float cross=Cross(direction,segment);
            if(cross!=0)
            {
                float t=Cross(offset,segment)/cross;
                float u=Cross(offset,direction)/cross;
                return t>0&&u>=0&&u<=1?t:float.PositiveInfinity;
            }
            if(Cross(offset,direction)!=0)return float.PositiveInfinity;

            float first=Vector2.Dot(offset,direction);
            float last=Vector2.Dot(end-head,direction);
            return Mathf.Max(first,last)>0
                ? Mathf.Max(0,Mathf.Min(first,last))/direction.sqrMagnitude
                : float.PositiveInfinity;
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
            ToggleSwitchFor(i);
            Active[i]=false;Remaining--;Rebuild();return true;
        }

        public bool RotateWorkshopArrow(int index, bool undo = false)
        {
            if (workshopDirections == null || (uint)index >= Active.Length || !Active[index]) return false;
            if (undo ? WorkshopTurnsRemaining >= ((ArrowWorkshopLevelData)Level).rotateBudget : WorkshopTurnsRemaining <= 0)
                return false;

            Vector3 Turn(Vector3 vector) => undo
                ? new Vector3(-vector.y, vector.x, 0)
                : new Vector3(vector.y, -vector.x, 0);
            var pivot = Head(index);
            var path = Paths[index];
            foreach (var point in path)
            {
                var cell = Key(pivot + Turn(point - pivot));
                if (cell.x < 0 || cell.y < 0 || cell.x >= Level.width || cell.y >= Level.height ||
                    (Occupancy.TryGetValue(cell, out int owner) && owner != index))
                    return false;
            }
            for (int i = 0; i < path.Count; i++) path[i] = pivot + Turn(path[i] - pivot);
            workshopDirections[index] = Turn(Direction(index));
            DotPositions[index] = StraightSegmentVertices(path);
            WorkshopTurnsRemaining += undo ? 1 : -1;
            Rebuild();
            return true;
        }
        public void Restore(int i)
        {
            if(i<0||i>=Active.Length||Active[i])return;
            ToggleSwitchFor(i);
            Active[i]=true;
            if(parkedAt.TryGetValue(i,out int receiver)){Active[receiver]=true;parkedAt.Remove(i);}
            Remaining++;
            Rebuild();
        }
        public int Hint() { for(int i=0;i<Active.Length;i++)if(CanExit(i))return i;return -1; }
        public int[] Solve()
        {
            var copy = new ArrowBoard(Level,Cube,flatDirections!=null,compactLayout,preserveSourceLengths,searchHoles); var result=new List<int>();
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
