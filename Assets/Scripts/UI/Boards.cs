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
        readonly ArrowPressGesture arrowPress=new ArrowPressGesture();
        int PreviewArrow=>arrowPress.Held?arrowPress.Arrow:-1;
        Color HeldArrowColor=>CircuitStyle.Theme==2?Ink.Hex("58E6FF"):Ink.Hex("00728F");
        float ArrowVisualCell=>Arrows.Cube?boardCell*Mathf.Max(1,Level.arrowVisualScale):boardCell;
        bool UsesMazeStyle => Mode == GameMode.Classic && Progress.SelectedLevelSet == ClassicLevelSet.Current;
        float ArrowStrokeWidth => UsesMazeStyle
            ? Mathf.Min(ArrowVisualCell * .19f, Mathf.Clamp(ArrowVisualCell * .115f, 1.1f / scale, 7f / scale))
            : Mathf.Min(Arrows.Cube ? float.PositiveInfinity : ArrowVisualCell * .25f,
                Mathf.Clamp(ArrowVisualCell * .20f, 1.8f / scale, 12f / scale));
        void CancelArrowPress(){arrowPress.Cancel();draggingBoard=false;pointerMoved=false;}
        void OnApplicationFocus(bool focused){if(!focused)CancelArrowPress();}
        ArrowMotionPath MotionPath(int i)
        {
            if(!motionPaths.TryGetValue(i,out var path)){path=new ArrowMotionPath(Arrows.Paths[i],Arrows.Direction(i),Arrows.Cube?.18f*Mathf.Max(1,Level.arrowVisualScale):0f);motionPaths[i]=path;}
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
        bool CanInteract=>!won&&!failed&&!paused&&!hintAdPending&&!resultTransition.Pending&&!modalHelp&&!LevelToolsOpen;
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
            boardCell=BoardLayout.FitArrowScale(r,Arrows)*zoom;projectedCenter=r.center+pan;
            if(!CanInteract||Input.touchCount>1||(Input.touchCount==1&&Input.GetTouch(0).phase==TouchPhase.Canceled))CancelArrowPress();
            arrowPress.Tick(Time.unscaledTime);
            if(Arrows.Cube){if(!cubeArtwork)cubeArtwork=gameObject.AddComponent<CubeArtRenderer>();cubeArtwork.Draw(r,Arrows,yaw,pitch,roll,zoom,hint,hintUntil);}
            else
            {
                if (UsesMazeStyle)
                {
                    var bounds = BoardLayout.OrientedBounds(Arrows.ContentBounds);
                    var size = (bounds.size + Vector2.one) * boardCell;
                    Ink.Card(new Rect(projectedCenter - size * .5f, size),
                        CircuitStyle.Theme == 2 ? Ink.Hex("25243E") : Ink.Hex("FFF8EB"), 18);
                }
                DrawClearedArrowDots(r);
            }
            var order=new List<int>();for(int i=0;i<Arrows.Active.Length;i++)if(Arrows.Active[i])order.Add(i);
            if(Arrows.Cube)order.Sort((a,b)=>RotatePoint(Arrows.Head(a)).z.CompareTo(RotatePoint(Arrows.Head(b)).z));
            if(!Arrows.Cube)
            {
                foreach(int i in order)if(i!=PreviewArrow)DrawArrowPath(i);
                foreach(var f in flying)DrawArrowPath(f.id,f.elapsed);
                if(PreviewArrow>=0&&Arrows.Active[PreviewArrow])DrawArrowPath(PreviewArrow);
            }
            var e=Event.current;
            if(!CanInteract)return;
            if(e.type==EventType.ScrollWheel&&r.Contains(e.mousePosition)){zoom=Mathf.Clamp(zoom-e.delta.y*.08f,.6f,4);e.Use();}
            if(e.type==EventType.MouseDown&&e.button==0&&Input.touchCount<2&&r.Contains(e.mousePosition))
            {lastPointer=pointerStart=e.mousePosition;draggingBoard=true;pointerMoved=false;arrowPress.Begin(Arrows.Cube?-1:PickArrow(e.mousePosition),e.mousePosition,Time.unscaledTime,18/scale);e.Use();}
            if(e.type==EventType.MouseDrag&&draggingBoard)
            {
                if(Vector2.Distance(pointerStart,e.mousePosition)>18/scale)pointerMoved=true;
                bool onPressedArrow=!Arrows.Cube&&arrowPress.Arrow>=0&&ArrowHitDistance(arrowPress.Arrow,e.mousePosition)<14/scale;
                arrowPress.Move(e.mousePosition,onPressedArrow);
                if(pointerMoved&&!onPressedArrow){var delta=e.mousePosition-lastPointer;if(Arrows.Cube){yaw+=delta.x*.4f;pitch=Mathf.Clamp(pitch+delta.y*.3f,-80,80);}else if(zoom>1)pan+=delta;}
                lastPointer=e.mousePosition;e.Use();
            }
            if(e.type==EventType.MouseUp&&e.button==0&&draggingBoard)
            {
                draggingBoard=false;
                int pressed=arrowPress.Release(Time.unscaledTime);
                if(!pointerMoved)
                {
                    int hit=PickArrow(e.mousePosition);
                    if(hit>=0&&(Arrows.Cube||hit==pressed))
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
        void DrawClearedArrowDots(Rect viewport)
        {
            float radius=Mathf.Clamp(ArrowVisualCell*.13f,3.5f/scale,5f/scale);
            var color=Ink.Hex("E58A24");
            foreach(var cell in Arrows.ClearedCells)
            {var point=Project(cell);if(viewport.Contains(point))Ink.Circle(point,radius,color);}
        }
        void DrawHeldArrowRoute(Vector2 boardOrigin)
        {
            int index=PreviewArrow;if(index<0||!Arrows.Active[index])return;
            // Draw after all board/UI groups in physical screen coordinates.
            // Derive direction directly: subtracting two projected positions can
            // leave tiny off-axis components and prematurely truncate the ray.
            var head=screenOffset+(Project(Arrows.Head(index))+boardOrigin)*scale;
            var worldDirection=Arrows.Direction(index);
            var direction=BoardLayout.ScreenDirection(new Vector2(worldDirection.x,worldDirection.y),BoardLayout.ShouldRotate(Arrows.ContentBounds)).normalized;
            var viewport=new Rect(0,0,Screen.width,Screen.height);
            // Keep the guide legible at native phone resolutions and where a
            // blocked arrow's ray crosses another arrow (or its own bends).
            float pixelScale=Mathf.Max(.5f,scale);
            float dash=24*pixelScale,gap=14*pixelScale,width=6*pixelScale;
            float distance=BoardLayout.RayDistanceToEdge(head,direction,viewport)+dash+gap;
            var matrix=GUI.matrix;
            try
            {
                GUI.matrix=Matrix4x4.identity;
                for(float offset=2*pixelScale;offset<distance;offset+=dash+gap)
                {
                    var start=head+direction*offset;var end=head+direction*Mathf.Min(distance,offset+dash);
                    Ink.Line(start,end,width+4*pixelScale,CircuitStyle.Background);
                    Ink.Line(start,end,width,HeldArrowColor);
                }
            }
            finally{GUI.matrix=matrix;}
        }
        void DrawCube()
        {
            var corners=new Vector3[8];for(int i=0;i<8;i++)corners[i]=new Vector3((i&1)==0?-.4f:Level.width-.6f,(i&2)==0?-.4f:Level.height-.6f,(i&4)==0?-.4f:Level.depth-.6f);
            for(int i=0;i<8;i++)for(int bit=1;bit<=4;bit*=2){int j=i^bit;if(j>i)Ink.Line(Project(corners[i]),Project(corners[j]),2,Ink.Hex("DCE6D9"));}
        }
        Color ArrowColor(int i)
        {
            if(i==hint&&Time.unscaledTime<hintUntil)return Ink.Gold;
            if(collidedArrows.Contains(i))return Arrows.CanExit(i)?Ink.Hex("32B956"):Ink.Red;
            if(i==PreviewArrow)return HeldArrowColor;
            if(Mode==GameMode.ArrowClash)return Color.Lerp(Ink.Palette[Mathf.Abs(Level.arrows[i].color)%7],Ink.Hex("101D35"),.12f);
            if(Mode==GameMode.CatEscape||Mode==GameMode.Zoo)return Color.Lerp(Ink.Palette[i%7],Ink.Hex("101D35"),.12f);
            if(Mode==GameMode.Countdown&&i==UrgentArrow())return Ink.Red;
            if(Arrows.Cube)return Ink.Palette[Mathf.Abs(Level.arrows[i].points[0]%6)%7];
            if(UsesMazeStyle)return CircuitStyle.Theme==2?Ink.Hex("D4B9FF"):Ink.Hex("8135BC");
            return CircuitStyle.ArrowInk;
        }
        void DrawArrowPath(int i,float flight=-1)
        {
            // Keep a readable stroke in screen pixels even on dense boards and
            // downscaled tablet/phone canvases; grow proportionally when zooming.
            var path=Arrows.Paths[i];var color=ArrowColor(i);float width=ArrowStrokeWidth;
            if(i==PreviewArrow)width*=1.25f;
            if(!Arrows.Cube)width=Mathf.Min(width,ArrowVisualCell*.25f);
            if(Arrows.IsMechanism(i)&&path.Count>0){var at=Project(path[0]);Ink.Card(new Rect(at.x-boardCell*.23f,at.y-boardCell*.23f,boardCell*.46f,boardCell*.46f),Ink.Hex("C5CFC1"),6);Ink.TextAt(new Rect(at.x-20,at.y-20,40,40),Level.arrows[i].special==3004?"↻":"×",20,Ink.Text,TextAnchor.MiddleCenter,true);return;}
            if(path.Count<2)return;
            bool hidden=Mode==GameMode.Reasoning&&elapsed>3&&i!=PreviewArrow&&!(i==revealArrow&&Time.unscaledTime<revealUntil)&&!(i==hint&&Time.unscaledTime<hintUntil);
            if(hidden)color=Ink.WithAlpha(color,.13f);
            var route=MotionPath(i);float distance=0;
            if(flight>=0)
            {
                float t=Mathf.Clamp01(flight/FlightDuration(i));
                // Accelerate gently, then let the entire tail clear the board.
                float travel=route.Length+Mathf.Max(Mathf.Max(Level.width,Level.height),
                    Mathf.Max(Arrows.ContentBounds.width,Arrows.ContentBounds.height))+3;
                if(Level.followDiagonalPaths)
                    travel=route.Length+new Vector2(Level.width,Level.height).magnitude+3;
                distance=travel*t*t*(2-t);
                color.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1,t));
            }
            else if(blockedFlights.TryGetValue(i,out var blocked))distance=blocked.Offset;
            var headPoint=route.Sample(route.Length+distance);
            var head=Project(headPoint);
            var dir=(Project(headPoint+Arrows.Direction(i))-head).normalized;
            float arrowSize=Mathf.Min(ArrowVisualCell*.58f,width*3.8f);
            // End the shaft inside the head so a rounded cap cannot extend past its tip.
            route.Slice(distance,motionPoints,arrowSize*.75f/boardCell);var points=motionPoints;
            for(int k=1;k<points.Count;k++)Ink.Line(Project(points[k-1]),Project(points[k]),width,color);
            CircuitStyle.ArrowHead(head,dir,arrowSize,width,color);
            if(i==hint&&Time.unscaledTime<hintUntil)Ink.Circle(head,boardCell*.34f,Ink.WithAlpha(Ink.Gold,.25f));
            if(Mode==GameMode.Countdown&&i==UrgentArrow()&&flight<0)Ink.TextAt(new Rect(head.x-30,head.y-65,60,44),Mathf.Max(0,countdownDeadline-moves).ToString(),25,Ink.Red,TextAnchor.MiddleCenter,true);
        }
        int PickArrow(Vector2 p)
        {
            int found=-1;float best=14/scale;
            for(int i=0;i<Arrows.Active.Length;i++)if(Arrows.Active[i]&&!blockedFlights.ContainsKey(i))
            {
                float distance=ArrowHitDistance(i,p);
                if(distance<best){best=distance;found=i;}
            }
            return found;
        }
        float ArrowHitDistance(int i,Vector2 p)
        {
            float distance=float.PositiveInfinity;var path=Arrows.Paths[i];
            for(int j=1;j<path.Count;j++)
            {
                if(Arrows.Cube&&cubeArtwork&&!cubeArtwork.Visible(path[j]))continue;
                distance=Mathf.Min(distance,LinkBoard.DistanceToSegment(p,Project(path[j-1]),Project(path[j])));
            }
            if(!Arrows.Cube&&path.Count>1)
            {
                var head=Project(Arrows.Head(i));var direction=(Project(Arrows.Head(i)+Arrows.Direction(i))-head).normalized;
                float width=ArrowStrokeWidth;
                distance=Mathf.Min(distance,BoardLayout.ArrowHeadDistance(p,head,direction,Mathf.Min(ArrowVisualCell*.58f,width*3.8f)));
            }
            return distance;
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
