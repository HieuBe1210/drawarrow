using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        Vector2 pointerStart;bool pointerMoved;int revealArrow=-1;float revealUntil;float boardCell;Rect currentBoard;Vector2 projectedCenter;
        CubeArtRenderer cubeArtwork;
        readonly Dictionary<int,ArrowMotionPath> motionPaths=new Dictionary<int,ArrowMotionPath>();
        readonly List<Vector3> motionPoints=new List<Vector3>();
        ArrowMotionPath MotionPath(int i)
        {
            if(!motionPaths.TryGetValue(i,out var path)){path=new ArrowMotionPath(Arrows.Paths[i],Arrows.Direction(i));motionPaths[i]=path;}
            return path;
        }
        float FlightDuration(int i)=>Mathf.Clamp(.45f+MotionPath(i).Length*.018f,.6f,1.15f);
        void DrawBoard(Rect r)
        {
            currentBoard=r;
            if(Arrows!=null)DrawArrows(r);
            else if(Octa!=null)DrawOcta(r);
            else if(OneLine!=null)DrawOneLine(r);
            else if(Links!=null)DrawLinks(r);
            else if(Schulte!=null)DrawSchulte(r);
        }
        bool CanInteract=>!won&&!failed&&!paused&&!modalHelp;
        Vector3 RotatePoint(Vector3 p)
        {
            var center=new Vector3((Level.width-1)*.5f,(Level.height-1)*.5f,(Level.depth-1)*.5f);
            return Quaternion.Euler(pitch,yaw,0)*(p-center);
        }
        Vector2 Project(Vector3 p)
        {
            if(!Arrows.Cube)return BoardLayout.Project(new Vector2(p.x,p.y),Arrows.ContentBounds,projectedCenter,boardCell);
            if(cubeArtwork)return cubeArtwork.Project(p);var q=RotatePoint(p);return projectedCenter+new Vector2(q.x,-q.y)*boardCell;
        }
        void DrawArrows(Rect r)
        {
            boardCell=BoardLayout.FitScale(r,BoardLayout.OrientedBounds(Arrows.ContentBounds))*zoom;projectedCenter=r.center+pan;
            if(Arrows.Cube){if(!cubeArtwork)cubeArtwork=gameObject.AddComponent<CubeArtRenderer>();cubeArtwork.Draw(r,Arrows,yaw,pitch,roll,zoom,hint,hintUntil);}
            else if(boardCell>18)
            {
                for(int y=Mathf.CeilToInt(Arrows.ContentBounds.yMin);y<=Arrows.ContentBounds.yMax;y++)for(int x=Mathf.CeilToInt(Arrows.ContentBounds.xMin);x<=Arrows.ContentBounds.xMax;x++) {var p=Project(new Vector3(x,y,0));if(r.Contains(p))Ink.Circle(p,1.6f,Ink.Hex("E8EDE4"));}
            }
            var order=new List<int>();for(int i=0;i<Arrows.Active.Length;i++)if(Arrows.Active[i])order.Add(i);
            if(Arrows.Cube)order.Sort((a,b)=>RotatePoint(Arrows.Head(a)).z.CompareTo(RotatePoint(Arrows.Head(b)).z));
            if(!Arrows.Cube){foreach(int i in order)DrawArrowPath(i);foreach(var f in flying)DrawArrowPath(f.id,f.elapsed);}
            var e=Event.current;
            if(!CanInteract)return;
            if(e.type==EventType.ScrollWheel&&r.Contains(e.mousePosition)){zoom=Mathf.Clamp(zoom-e.delta.y*.08f,.6f,4);e.Use();}
            if(e.type==EventType.MouseDown&&r.Contains(e.mousePosition)){lastPointer=pointerStart=e.mousePosition;draggingBoard=true;pointerMoved=false;e.Use();}
            if(e.type==EventType.MouseDrag&&draggingBoard)
            {
                if(Vector2.Distance(pointerStart,e.mousePosition)>14)pointerMoved=true;
                if(pointerMoved){var delta=e.mousePosition-lastPointer;if(Arrows.Cube){yaw+=delta.x*.4f;pitch=Mathf.Clamp(pitch+delta.y*.3f,-80,80);}else if(zoom>1)pan+=delta;}
                lastPointer=e.mousePosition;e.Use();
            }
            if(e.type==EventType.MouseUp&&draggingBoard)
            {
                draggingBoard=false;
                if(!pointerMoved)
                {
                    int hit=PickArrow(e.mousePosition);
                    if(hit>=0)
                    {
                        if(Mode==GameMode.Reasoning&&elapsed>3&&hit!=revealArrow){Play("arrow_click",.48f);revealArrow=hit;revealUntil=Time.unscaledTime+2;Notify("Arrow revealed. Tap again to move it.");}
                        else TapArrow(hit);
                    }
                }
                e.Use();
            }
            if(Input.touchCount==2)
            {
                var a=Input.GetTouch(0);var b=Input.GetTouch(1);if(a.phase==TouchPhase.Moved||b.phase==TouchPhase.Moved){float now=(a.position-b.position).magnitude;float before=(a.position-a.deltaPosition-b.position+b.deltaPosition).magnitude;zoom=Mathf.Clamp(zoom+(now-before)/800,.6f,4);}
            }
        }
        void DrawCube()
        {
            var corners=new Vector3[8];for(int i=0;i<8;i++)corners[i]=new Vector3((i&1)==0?-.4f:Level.width-.6f,(i&2)==0?-.4f:Level.height-.6f,(i&4)==0?-.4f:Level.depth-.6f);
            for(int i=0;i<8;i++)for(int bit=1;bit<=4;bit*=2){int j=i^bit;if(j>i)Ink.Line(Project(corners[i]),Project(corners[j]),2,Ink.Hex("DCE6D9"));}
        }
        Color ArrowColor(int i)
        {
            if(i==hint&&Time.unscaledTime<hintUntil)return Ink.Gold;
            if(Mode==GameMode.ArrowClash)return Color.Lerp(Ink.Palette[Mathf.Abs(Level.arrows[i].color)%7],Ink.Hex("101D35"),.12f);
            if(Mode==GameMode.CatEscape||Mode==GameMode.Zoo)return Color.Lerp(Ink.Palette[i%7],Ink.Hex("101D35"),.12f);
            if(Mode==GameMode.Countdown&&i==UrgentArrow())return Ink.Red;
            if(Arrows.Cube)return Ink.Palette[Mathf.Abs(Level.arrows[i].points[0]%6)%7];
            return OriginalArt.Theme==1?Ink.Hex("FFF3D8"):Ink.Hex("0C1736");
        }
        void DrawArrowPath(int i,float flight=-1)
        {
            // Keep a readable stroke in screen pixels even on dense boards and
            // downscaled tablet/phone canvases; grow proportionally when zooming.
            var path=Arrows.Paths[i];var color=ArrowColor(i);float width=Mathf.Clamp(boardCell*.20f,1.8f/scale,12f/scale);
            if(Arrows.IsMechanism(i)&&path.Count>0){var at=Project(path[0]);Ink.Card(new Rect(at.x-boardCell*.23f,at.y-boardCell*.23f,boardCell*.46f,boardCell*.46f),Ink.Hex("C5CFC1"),6);Ink.TextAt(new Rect(at.x-20,at.y-20,40,40),Level.arrows[i].special==3004?"↻":"×",20,Ink.Text,TextAnchor.MiddleCenter,true);return;}
            if(path.Count<2)return;
            bool hidden=Mode==GameMode.Reasoning&&elapsed>3&&!(i==revealArrow&&Time.unscaledTime<revealUntil)&&!(i==hint&&Time.unscaledTime<hintUntil);
            if(hidden)color=Ink.WithAlpha(color,.13f);
            var route=MotionPath(i);float distance=0;
            if(flight>=0)
            {
                float t=Mathf.Clamp01(flight/FlightDuration(i));
                // Accelerate gently, then let the entire tail clear the board.
                float travel=route.Length+Mathf.Max(Level.width,Level.height)+3;
                distance=travel*t*t*(2-t);
                color.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1,t));
            }
            route.Slice(distance,motionPoints);var points=motionPoints;
            for(int k=1;k<points.Count;k++)Ink.Line(Project(points[k-1]),Project(points[k]),width,color);
            var head=Project(points[points.Count-1]);var prev=Project(points[points.Count-2]);var dir=(head-prev).normalized;
            float arrowSize=Mathf.Min(boardCell*.58f,width*3.8f);int angle=((Mathf.RoundToInt((Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg+90)/45)*45)%360+360)%360;
            bool horizontal=Mathf.Abs(dir.x)>.5f;var center=head-dir*arrowSize*.5f;var headSize=horizontal?new Vector2(arrowSize,arrowSize*.8f):new Vector2(arrowSize*.8f,arrowSize);
            OriginalArt.Draw("ArrowHead_"+angle,new Rect(center-headSize*.5f,headSize),color);
            if(Mode==GameMode.CatEscape&&flight<0)
            {
                float size=Mathf.Max(10,boardCell*.72f);OriginalArt.Draw("cat"+(i%10+1),new Rect(head-new Vector2(size,size)*.5f,new Vector2(size,size)),null,true);
            }
            if(i==hint&&Time.unscaledTime<hintUntil)Ink.Circle(head,boardCell*.34f,Ink.WithAlpha(Ink.Gold,.25f));
            if(Mode==GameMode.Countdown&&i==UrgentArrow()&&flight<0)Ink.TextAt(new Rect(head.x-30,head.y-65,60,44),Mathf.Max(0,countdownDeadline-moves).ToString(),25,Ink.Red,TextAnchor.MiddleCenter,true);
        }
        int PickArrow(Vector2 p)
        {
            int found=-1;float best=Mathf.Max(13,boardCell*.27f);
            for(int i=0;i<Arrows.Active.Length;i++)if(Arrows.Active[i])
            {
                var path=Arrows.Paths[i];for(int j=1;j<path.Count;j++){if(Arrows.Cube&&cubeArtwork&&!cubeArtwork.Visible(path[j]))continue;float dist=LinkBoard.DistanceToSegment(p,Project(path[j-1]),Project(path[j]));if(dist<best){best=dist;found=i;}}
            }
            return found;
        }
        Vector2 GridPoint(int cell,Rect r,int w,int h)
        {
            var bounds=new Rect(0,0,w-1,h-1);float cellSize=BoardLayout.FitScale(r,BoardLayout.OrientedBounds(bounds));
            return BoardLayout.Project(new Vector2(cell%w,cell/w),bounds,r.center,cellSize);
        }
        void DrawOcta(Rect r)
        {
            var bounds=new Rect(0,0,Level.width-1,Level.height-1);bool rotate=BoardLayout.ShouldRotate(bounds);
            float cell=BoardLayout.FitScale(r,BoardLayout.OrientedBounds(bounds));
            for(int y=0;y<Level.height;y++)for(int x=0;x<Level.width;x++)Ink.Circle(GridPoint(y*Level.width+x,r,Level.width,Level.height),2,Ink.Hex("E4E8DE"));
            if(Level.portals!=null)for(int k=0;k<Level.portals.Length;k++)foreach(int p in new[]{Level.portals[k].a,Level.portals[k].b})
            {var v=GridPoint(p,r,Level.width,Level.height);Ink.Circle(v,cell*.38f,Ink.Palette[k%7]);Ink.Circle(v,cell*.23f,Color.white);}
            if(Level.turns!=null)foreach(var turn in Level.turns){var p=GridPoint(turn.y*Level.width+turn.x,r,Level.width,Level.height);var direction=BoardLayout.ScreenDirection(new Vector2(turn.dx,turn.dy),rotate);Ink.Circle(p,cell*.32f,Ink.Hex("F5E5B5"));Ink.Arrow(p-direction*cell*.16f,p+direction*cell*.17f,4,Ink.Text);}
            for(int i=0;i<Octa.Active.Length;i++)if(Octa.Active[i])
            {
                var a=Level.arrows[i];var p=GridPoint(a.points[0],r,Level.width,Level.height);var c=i==hint&&Time.unscaledTime<hintUntil?Ink.Gold:Ink.Palette[i%7];
                Ink.Circle(p,cell*.31f,a.kind=="switch"?(a.open?Ink.Hex("CFE8D6"):Ink.Hex("F0C9C3")):Ink.WithAlpha(c,.13f));
                var dir=BoardLayout.ScreenDirection(new Vector2(a.dx,a.dy),rotate).normalized;Ink.Arrow(p-dir*cell*.18f,p+dir*cell*.22f,Mathf.Max(1.8f/scale,cell*.10f),c);
                if(a.kind=="splitter"){var dir2=BoardLayout.ScreenDirection(new Vector2(a.dx2,a.dy2),rotate).normalized;Ink.Arrow(p,p+dir2*cell*.22f,3,c);}
                if(a.kind=="rotateCW"||a.kind=="rotateCCW")Ink.TextAt(new Rect(p.x-cell*.5f,p.y-cell*.7f,cell,cell*.4f),a.kind=="rotateCW"?"↻":"↺",18,Ink.Muted,TextAnchor.MiddleCenter);
            }
            var e=Event.current;
            if(CanInteract&&e.type==EventType.MouseUp&&r.Contains(e.mousePosition))
            {
                for(int i=0;i<Octa.Active.Length;i++)if(Octa.Active[i]&&Vector2.Distance(e.mousePosition,GridPoint(Level.arrows[i].points[0],r,Level.width,Level.height))<cell*.44f)
                {
                    Play("arrow_click",.48f);if(!Octa.Tap(i)){Notify("The route is blocked. Try another direction.");Play("arrow_back");}else Play("arrow_tap");
                    if(Octa.Remaining==0)Win();else if(Octa.MovesLeft<=0)Lose("No moves left. Plan the portals and gates.");e.Use();break;
                }
            }
        }
        Vector2 LinePosition(int node,Rect r)=>LinePosition(Level.nodes[node].V,r);
        Vector2 LinePosition(Vector2 p,Rect r)
        {
            var min=new Vector2(float.MaxValue,float.MaxValue);var max=new Vector2(float.MinValue,float.MinValue);
            foreach(var n in Level.nodes){min=Vector2.Min(min,n.V);max=Vector2.Max(max,n.V);}
            foreach(var edge in Level.edges)if(edge.curved){min=Vector2.Min(min,new Vector2(edge.cx,edge.cy));max=Vector2.Max(max,new Vector2(edge.cx,edge.cy));}
            float factor=Mathf.Min((r.width-110)/Mathf.Max(1,max.x-min.x),(r.height-110)/Mathf.Max(1,max.y-min.y));return r.center+(p-(min+max)*.5f)*factor;
        }
        void DrawOneLine(Rect r)
        {
            for(int i=0;i<Level.edges.Length;i++)
            {
                var edge=Level.edges[i];var color=OneLine.Used[i]?Ink.Teal:Ink.Hex("D4DDD0");var a=LinePosition(edge.a,r);var b=LinePosition(edge.b,r);
                if(edge.curved)Ink.Curve(a,LinePosition(new Vector2(edge.cx,edge.cy),r),b,9,color);else Ink.Line(a,b,9,color);
            }
            for(int i=0;i<Level.nodes.Length;i++)
            {
                var p=LinePosition(i,r);bool current=OneLine.Trail.Count>0&&OneLine.Trail[OneLine.Trail.Count-1]==i;
                Ink.Circle(p,current?27:18,i==hint&&Time.unscaledTime<hintUntil?Ink.Gold:current?Ink.Teal:Ink.Text);Ink.Circle(p,7,Color.white);
            }
            var e=Event.current;
            if(CanInteract&&(e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&r.Contains(e.mousePosition))
            {
                for(int i=0;i<Level.nodes.Length;i++)if(Vector2.Distance(e.mousePosition,LinePosition(i,r))<35)
                {
                    int before=OneLine.Trail.Count;if(OneLine.Visit(i)&&OneLine.Trail.Count>before)Play("arrow_tap");if(OneLine.Complete)Win();break;
                }
                e.Use();
            }
        }
        float LinkScale(Rect r)=>Mathf.Min((r.width-80)/Level.width,(r.height-80)/Level.height);
        Vector2 LinkPosition(Vector2 p,Rect r)=>r.center+new Vector2(p.x-Level.width*.5f,Level.height*.5f-p.y)*LinkScale(r);
        Vector2 LinkUnproject(Vector2 p,Rect r){var v=(p-r.center)/LinkScale(r);return new Vector2(v.x+Level.width*.5f,Level.height*.5f-v.y);}
        void DrawLinks(Rect r)
        {
            for(int i=0;i<Links.Paths.Length;i++)
            {
                var path=Links.Paths[i];var c=Ink.Palette[Mathf.Abs(Level.pairs[i].color)%7];
                for(int j=1;j<path.Count;j++)Ink.Line(LinkPosition(path[j-1],r),LinkPosition(path[j],r),10,c);
                foreach(var endpoint in Level.pairs[i].points){var p=LinkPosition(endpoint.V,r);Ink.Circle(p,Links.Connected[i]?23:26,c);Ink.Circle(p,9,Color.white);}
            }
            var e=Event.current;if(!CanInteract)return;
            if(e.type==EventType.MouseDown&&r.Contains(e.mousePosition)){Links.Begin(LinkUnproject(e.mousePosition,r),.65f);e.Use();}
            if(e.type==EventType.MouseDrag&&Links.Drawing>=0)
            {
                if(r.Contains(e.mousePosition)&&!Links.Move(LinkUnproject(e.mousePosition,r),.45f))Notify("Paths cannot cross. Draw around the other line.");
                if(Links.Complete)Win();e.Use();
            }
            if(e.type==EventType.MouseUp){Links.End();if(Links.Complete)Win();}
        }
        void DrawSchulte(Rect r)
        {
            int size=Schulte.Size;float cell=Mathf.Min((r.width-50)/size,(r.height-160)/size);var start=r.center-new Vector2(size*cell,size*cell)*.5f;
            for(int i=0;i<Schulte.Numbers.Length;i++)
            {
                var box=new Rect(start.x+i%size*cell+6,start.y+i/size*cell+6,cell-12,cell-12);bool done=Schulte.Numbers[i]<Schulte.Next;
                if(Ink.Button(box,done?"✓":Schulte.Numbers[i].ToString(),i==hint&&Time.unscaledTime<hintUntil?Ink.Gold:done?Ink.Hex("DCE7D7"):Ink.Hex("F1F1E8"),done?Ink.Hex("A1B59A"):Ink.Text,Mathf.RoundToInt(cell*.28f))&&CanInteract)
                {if(!Schulte.Tap(i)){Notify("Find "+Schulte.Next+" next.");Play("arrow_back");}else Play("arrow_tap");if(Schulte.Complete)Win();}
            }
        }
    }
}
