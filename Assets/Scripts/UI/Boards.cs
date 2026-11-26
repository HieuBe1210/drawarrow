using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        Vector2 pointerStart;bool pointerMoved;int revealArrow=-1;float revealUntil;float boardCell;Rect currentBoard;Vector2 projectedCenter;
        bool rotateArrowBoard;
        CubeArtRenderer cubeArtwork;
        readonly Dictionary<int,ArrowMotionPath> motionPaths=new Dictionary<int,ArrowMotionPath>();
        readonly List<Vector3> motionPoints=new List<Vector3>();
        readonly Dictionary<int, ProjectedArrow> projectedArrows = new Dictionary<int, ProjectedArrow>();
        Vector2 cachedProjectionCenter;
        float cachedProjectionCell;
        bool cachedProjectionRotation;
        int projectionVersion;

        sealed class ProjectedArrow
        {
            public readonly List<Vector2> Shaft = new List<Vector2>();
            public Vector2 Head, Direction;
            public Rect Bounds;
            public float StrokeWidth;
            public int ProjectionVersion = -1;
        }
        readonly Dictionary<int, (float previous, float current, float window)> dotFlightSweeps =
            new Dictionary<int, (float previous, float current, float window)>();
        readonly HashSet<Vector3> visibleClearedDots = new HashSet<Vector3>();
        readonly HashSet<Vector3> tracedGridDots = new HashSet<Vector3>();
        readonly Dictionary<Vector3, float> dotGlowEnd = new Dictionary<Vector3, float>();
        readonly List<(Vector3 head, Vector3 direction, float from, float to, float time)> dotRaySweeps =
            new List<(Vector3, Vector3, float, float, float)>();
        const float DotGlowDuration = .19f;
        readonly ArrowPressGesture arrowPress=new ArrowPressGesture();
        int boardFingerId = -1;
        int boardTouchFrame = -1;
        float ArrowTapRadius => Application.isMobilePlatform ? Mathf.Clamp(62 * scale, 22, 38) / scale : 14 / scale;
        float ArrowTapSlop => Application.isMobilePlatform ? Mathf.Clamp(56 * scale, 22, 34) / scale : 18 / scale;
        int PreviewArrow=>arrowPress.Held?arrowPress.Arrow:-1;
        Color HeldArrowColor=>CircuitStyle.Theme==2?Ink.Hex("58E6FF"):Ink.Hex("00728F");
        float ArrowVisualCell=>Arrows.Cube?boardCell*Mathf.Max(1,Level.arrowVisualScale):boardCell;
        float ArrowStrokeWidth => Mathf.Min(Arrows.Cube ? float.PositiveInfinity : ArrowVisualCell * .25f,
                Mathf.Clamp(ArrowVisualCell * .20f, 1.8f / scale, 12f / scale));
        void CancelArrowPress(){arrowPress.Cancel();draggingBoard=false;pointerMoved=false;boardFingerId=-1;}
        void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelArrowPress();
            else if (Application.isMobilePlatform) ApplyFrameRateTarget();
        }
        ArrowMotionPath MotionPath(int i)
        {
            if(!motionPaths.TryGetValue(i,out var path)){path=new ArrowMotionPath(Arrows.Paths[i],Arrows.Direction(i),Arrows.Cube?.18f*Mathf.Max(1,Level.arrowVisualScale):0f);motionPaths[i]=path;}
            return path;
        }
        float FlightDuration(int i)=>Mathf.Clamp(.45f+MotionPath(i).Length*.018f,.6f,1.15f);
        void DrawBoard(Rect r, Vector2 boardOrigin)
        {
            currentBoard=r;
            if(Arrows!=null)DrawArrows(r, boardOrigin);
            else if(Octa!=null)DrawOcta(r);
            else if(OneLine!=null)DrawOneLine(r);
            else if(Links!=null)DrawLinks(r);
            else if(Schulte!=null)DrawSchulte(r);
        }
        bool CanInteract=>!won&&!failed&&!paused&&!RewardAdPending&&!resultTransition.Pending&&!modalHelp&&!LevelToolsOpen;
        Vector3 RotatePoint(Vector3 p)
        {
            var center=new Vector3((Level.width-1)*.5f,(Level.height-1)*.5f,(Level.depth-1)*.5f);
            return Quaternion.Euler(pitch,yaw,0)*(p-center);
        }
        Vector2 Project(Vector3 p)
        {
            if(!Arrows.Cube)return BoardLayout.ProjectOriented(new Vector2(p.x,p.y),Arrows.ContentBounds,projectedCenter,boardCell,rotateArrowBoard);
            if(cubeArtwork)return cubeArtwork.Project(p);var q=RotatePoint(p);return projectedCenter+new Vector2(q.x,-q.y)*boardCell;
        }
        void DrawArrows(Rect r, Vector2 boardOrigin)
        {
            var e = Event.current;
            bool tutorialBlocksBoard = (TeachingArrowSwitch && SwitchTutorialArrow < 0) ||
                (TeachingWorkshop && workshopTutorial == WorkshopTutorialStep.ChooseTool);
            boardCell=BoardLayout.FitBestOrientation(r, Arrows.ContentBounds, out rotateArrowBoard,
                Arrows.Cube || Level.keepBoardOrientation, Arrows.ReferenceBounds) * zoom;
            projectedCenter=r.center+pan;
            if(!CanInteract || tutorialBlocksBoard)CancelArrowPress();
            arrowPress.Tick(Time.unscaledTime);
            if(Arrows.Cube){if(!cubeArtwork)cubeArtwork=gameObject.AddComponent<CubeArtRenderer>();cubeArtwork.Draw(r,Arrows,yaw,pitch,roll,zoom,hint,hintUntil);}
            else if (e.type == EventType.Repaint)
            {
                if (!cachedProjectionCenter.Equals(projectedCenter) ||
                    cachedProjectionCell != boardCell || cachedProjectionRotation != rotateArrowBoard)
                {
                    projectionVersion++;
                    cachedProjectionCenter = projectedCenter;
                    cachedProjectionCell = boardCell;
                    cachedProjectionRotation = rotateArrowBoard;
                }
                DrawClearedArrowDots(r, boardOrigin);
                if (Arrows.SwitchGates != null) DrawArrowSwitchGates();
                if (Arrows.Chameleon != null) DrawChameleonGates();
                for (int i = 0; i < Arrows.Active.Length; i++)
                    if (Arrows.Active[i] && i != PreviewArrow) DrawArrowPath(i);
                if (victoryPhase != VictoryPhase.Flight)
                    foreach(var f in flying)DrawArrowPath(f.id,f.elapsed);
                if(PreviewArrow>=0&&Arrows.Active[PreviewArrow])DrawArrowPath(PreviewArrow);
            }
            if(!CanInteract || tutorialBlocksBoard)return;
            if (Application.isMobilePlatform && Input.touchSupported)
            {
                if (e.type == EventType.Repaint && boardTouchFrame != Time.frameCount)
                {
                    boardTouchFrame = Time.frameCount;
                    if (Time.unscaledTime - screenTransitionStart >= ScreenTransitionDuration)
                        ReadBoardTouches(r, boardOrigin);
                }
                return;
            }
            if(e.type==EventType.ScrollWheel&&r.Contains(e.mousePosition))
            {SetBoardZoom(zoom-e.delta.y*.08f);e.Use();}
            if(e.type==EventType.MouseDown&&e.button==0&&r.Contains(e.mousePosition))
            {BeginBoardPress(e.mousePosition);e.Use();}
            if(e.type==EventType.MouseDrag&&draggingBoard)
            {MoveBoardPress(e.mousePosition);e.Use();}
            if(e.type==EventType.MouseUp&&e.button==0&&draggingBoard)
            {EndBoardPress(e.mousePosition,r);e.Use();}
        }

        static readonly Color OpenGateColor = Ink.Hex("168E6C");
        static readonly Color[] ChameleonPalette = { Ink.Hex("C84259"), Ink.Hex("168E6C"), Ink.Hex("DA9B18") };

        void DrawChameleonGates()
        {
            var level = Arrows.Chameleon;
            float size = Mathf.Min(boardCell * .72f, 72);
            for (int i = 0; i < level.gateCells.Length; i++)
            {
                var center = Project(Arrows.Decode(level.gateCells[i]));
                var rect = new Rect(center - Vector2.one * size * .5f, Vector2.one * size);
                var color = ChameleonPalette[level.gateColors[i]];
                Ink.Card(rect, color, size * .24f);
                if (Arrows.Active[i] && !Arrows.ChameleonGateLocked(i))
                    Ink.Card(new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12),
                        CircuitStyle.Background, size * .18f);
            }
        }
        static readonly Color LockedGateColor = Ink.Hex("CE4055");
        static readonly Color GateSwitchColor = Ink.Hex("8A65DD");
        float ArrowSwitchMarkerSize => Mathf.Min(boardCell * .76f, 64);

        void DrawArrowSwitchGates()
        {
            var gates = Arrows.SwitchGates;
            float size = ArrowSwitchMarkerSize;
            bool flipped = Arrows.SwitchFlipped && switchTutorial != SwitchTutorialStep.WatchSwitch;
            DrawArrowSwitchPad(size, flipped);
            DrawArrowSwitchGate(gates.initiallyOpen, size, !flipped);
            DrawArrowSwitchGate(gates.initiallyClosed, size, flipped);
        }

        void DrawArrowSwitchPad(float size, bool flipped)
        {
            var pad = Project(Arrows.Decode(Arrows.SwitchGates.pad));
            var padColor = flipped ? Ink.Hex("6755AA") : GateSwitchColor;
            if (CanInteract && !flipped)
            {
                float pulse = Mathf.Repeat(Time.unscaledTime / 1.2f, 1);
                Ink.Circle(pad, size * Mathf.Lerp(.48f, .83f, pulse), Ink.WithAlpha(GateSwitchColor, .35f * (1 - pulse)));
            }
            Ink.Circle(pad, size * .43f, padColor);
            Ink.Line(pad + new Vector2(0, -size * .21f), pad - Vector2.right * size * .13f, size * .07f, Color.white);
            Ink.Line(pad - Vector2.right * size * .13f, pad + Vector2.right * size * .13f, size * .07f, Color.white);
            Ink.Line(pad + Vector2.right * size * .13f, pad + new Vector2(0, size * .21f), size * .07f, Color.white);
        }

        void DrawArrowSwitchGate(SwitchGateSpan span, float size, bool open)
        {
            var first = Project(Arrows.Decode(span.start));
            var last = Project(Arrows.Decode(span.end));
            var spanVector = last - first;
            var axis = spanVector.normalized;
            var side = new Vector2(-axis.y, axis.x);
            var color = open ? OpenGateColor : LockedGateColor;
            var rim = open ? Ink.Hex("0D5544") : Ink.Hex("81253D");
            var light = open ? Ink.Hex("B7F9DA") : Ink.Hex("FFC6CF");
            int sections = Mathf.Clamp(Mathf.RoundToInt(spanVector.magnitude / (size * .8f)), 2, 16);
            var step = spanVector / sections;
            if (open)
            {
                // The gap between the guides stays clear for arrows to pass through.
                var edge = side * size * .17f;
                for (int i = 0; i < sections; i++)
                {
                    var start = first + step * (i + .15f);
                    var end = first + step * (i + .85f);
                    Ink.Line(start + edge, end + edge, size * .065f, color);
                    Ink.Line(start - edge, end - edge, size * .065f, color);
                }
            }
            else
            {
                Ink.Line(first, last, size * .38f, rim);
                Ink.Line(first, last, size * .26f, color);
                var slash = (axis + side) * size * .082f;
                for (int i = 0; i < sections; i++)
                {
                    var mark = first + step * (i + .5f);
                    Ink.Line(mark - slash, mark + slash, size * .048f, light);
                }
            }
            DrawGatePost(first, size, rim, color, light);
            DrawGatePost(last, size, rim, color, light);
        }

        static void DrawGatePost(Vector2 position, float size, Color rim, Color color, Color light)
        {
            Ink.Circle(position, size * .28f, rim);
            Ink.Circle(position, size * .21f, color);
            Ink.Circle(position, size * .085f, light);
        }

        void ReadBoardTouches(Rect viewport, Vector2 boardOrigin)
        {
            var screenToGui = GUI.matrix.inverse;
            Vector2 BoardPoint(Vector2 position) =>
                (Vector2)screenToGui.MultiplyPoint3x4(new Vector3(position.x, Screen.height - position.y)) - boardOrigin;

            int activeCount = 0;
            Touch first = default, second = default;
            bool trackedTouchFound = false;
            for (int index = 0; index < Input.touchCount; index++)
            {
                var touch = Input.GetTouch(index);
                if (touch.fingerId == boardFingerId)
                {
                    trackedTouchFound = true;
                    if (touch.phase == TouchPhase.Ended) EndBoardPress(BoardPoint(touch.position), viewport);
                    else if (touch.phase == TouchPhase.Canceled) CancelArrowPress();
                }
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                if (activeCount == 0) first = touch;
                else if (activeCount == 1) second = touch;
                activeCount++;
            }
            if (!CanInteract || (boardFingerId >= 0 && !trackedTouchFound)) CancelArrowPress();
            if (!CanInteract || activeCount == 0) return;

            var position = BoardPoint(first.position);
            if (activeCount > 1)
            {
                CancelArrowPress();
                if (activeCount == 2 && viewport.Contains(position) && viewport.Contains(BoardPoint(second.position)) &&
                    (first.phase == TouchPhase.Moved || second.phase == TouchPhase.Moved))
                {
                    var separation = first.position - second.position;
                    float previous = (separation - first.deltaPosition + second.deltaPosition).magnitude;
                    SetBoardZoom(zoom + (separation.magnitude - previous) / 800);
                }
                return;
            }
            if (first.phase == TouchPhase.Began && viewport.Contains(position))
            {
                BeginBoardPress(position);
                if (draggingBoard) boardFingerId = first.fingerId;
            }
            else if (first.fingerId == boardFingerId && first.phase == TouchPhase.Moved)
                MoveBoardPress(position);
        }

        void BeginBoardPress(Vector2 position)
        {
            if (TeachingZoom) return;
            int arrow = Arrows.Cube ? -1 : PickBoardPressArrow(position);
            if ((TeachingFirstTap || TeachingArrowSwitch || TeachingWorkshop) && arrow < 0) return;
            lastPointer = pointerStart = position;
            draggingBoard = true;
            pointerMoved = false;
            arrowPress.Begin(arrow, position, Time.unscaledTime, ArrowTapSlop);
        }

        int PickBoardPressArrow(Vector2 position)
        {
            if (!TeachingFirstTap && !TeachingArrowSwitch && !TeachingWorkshop)
                return PickArrow(position);
            int arrow = TeachingFirstTap ? FindHint() : TeachingArrowSwitch ? SwitchTutorialArrow : WorkshopTutorialArrow;
            return arrow >= 0 && ArrowHitDistance(arrow, position) < ArrowTapRadius ? arrow : -1;
        }

        void SetBoardZoom(float value)
        {
            if (TeachingFirstTap || TeachingArrowSwitch || TeachingWorkshop) return;
            zoom = Mathf.Clamp(value, TeachingZoom ? 1 : .6f, 4);
            if (TeachingZoom && zoom >= 1.05f) zoomTutorialComplete = true;
        }

        void MoveBoardPress(Vector2 position)
        {
            if (Vector2.Distance(pointerStart, position) > ArrowTapSlop) pointerMoved = true;
            bool onPressedArrow = !Arrows.Cube && arrowPress.Arrow >= 0 &&
                ArrowHitDistance(arrowPress.Arrow, position) < ArrowTapRadius;
            arrowPress.Move(position, onPressedArrow);
            if (pointerMoved && !onPressedArrow)
            {
                var delta = position - lastPointer;
                if (Arrows.Cube)
                {
                    yaw += delta.x * .4f;
                    pitch = Mathf.Clamp(pitch + delta.y * .3f, -80, 80);
                }
                else if (zoom > 1) pan += delta;
            }
            lastPointer = position;
        }

        void EndBoardPress(Vector2 position, Rect viewport)
        {
            bool wasDragging = draggingBoard;
            draggingBoard = false;
            boardFingerId = -1;
            int pressed = arrowPress.Release(Time.unscaledTime);
            float slop = ArrowTapSlop;
            if (!wasDragging || pointerMoved || (position - pointerStart).sqrMagnitude > slop * slop ||
                !viewport.Contains(position)) return;
            int hit = Arrows.Cube ? PickBoardPressArrow(position) : pressed;
            if (hit < 0) return;
            if (Mode == GameMode.Reasoning && elapsed > 3 && hit != revealArrow)
            {
                Play("arrow_click", .48f);
                revealArrow = hit;
                revealUntil = Time.unscaledTime + 2;
                Notify("Arrow revealed. Tap again to move it.");
            }
            else TapArrow(hit);
        }
        void DrawClearedArrowDots(Rect viewport, Vector2 boardOrigin)
        {
            if (Event.current.type != EventType.Repaint) return;
            float radius = Mathf.Min(ArrowVisualCell * .13f, 5f / scale);
            var color=Ink.Hex("E58A24");
            float now = Time.unscaledTime;
            var visible = Arrows.ClearedCells;
            if (flying.Count > 0)
            {
                dotFlightSweeps.Clear();
                visibleClearedDots.Clear();
                foreach (var arrow in flying)
                {
                    bool exitScreen = victoryPhase == VictoryPhase.Flight && arrow.id == victoryFinalArrow;
                    var route = MotionPath(arrow.id);
                    dotFlightSweeps[arrow.id] = (
                        FlightDistance(arrow.id, route, Mathf.Max(0, arrow.elapsed - DotGlowDuration), exitScreen, boardOrigin),
                        FlightDistance(arrow.id, route, arrow.elapsed, exitScreen, boardOrigin),
                        Mathf.Clamp01(arrow.elapsed / DotGlowDuration));
                    var sweep = dotFlightSweeps[arrow.id];
                    TraceClearedRoute(arrow.id, sweep.current);
                }
                for (int i = 0; i < Arrows.Active.Length; i++)
                {
                    if (Arrows.Active[i]) continue;
                    var dots = Arrows.DotPositions[i];
                    if (!dotFlightSweeps.TryGetValue(i, out var sweep))
                    {
                        visibleClearedDots.UnionWith(dots);
                        continue;
                    }
                    float distance = 0;
                    for (int j = 0; j < dots.Count; j++)
                    {
                        if (j > 0) distance += Vector3.Distance(dots[j - 1], dots[j]);
                        if (distance >= sweep.current) break;
                        visibleClearedDots.Add(dots[j]);
                        if (distance >= sweep.previous)
                            PulseClearedDot(dots[j], now,
                                1 - sweep.window * (1 - Mathf.InverseLerp(sweep.previous, sweep.current, distance)));
                    }
                }
                visible = visibleClearedDots;
            }
            dotRaySweeps.Clear();
            foreach (var arrow in flying)
            {
                var sweep = dotFlightSweeps[arrow.id];
                AddDotRaySweep(arrow.id, sweep.previous, sweep.current, now);
            }
            foreach (var flight in blockedFlights)
            {
                var motion = flight.Value;
                float from = motion.elapsed - DotGlowDuration;
                float impact = motion.ImpactTime;
                if (from < impact && motion.elapsed > impact)
                {
                    AddDotRaySweep(flight.Key, motion.OffsetAt(from), motion.distance, now - (motion.elapsed - impact));
                    from = impact;
                }
                AddDotRaySweep(flight.Key, motion.OffsetAt(from), motion.Offset, now);
            }
            foreach (var cell in visible)
                DrawClearedDot(cell, viewport, radius, color, now);
            foreach (var cell in tracedGridDots)
                if (!visible.Contains(cell)) DrawClearedDot(cell, viewport, radius, color, now);
        }

        void DrawClearedDot(Vector3 cell, Rect viewport, float radius, Color color, float now)
        {
            var point = Project(cell);
            if (!viewport.Contains(point)) return;
            foreach (var sweep in dotRaySweeps)
                PulseDotOnExitRay(cell, sweep.head, sweep.direction, sweep.from, sweep.to, sweep.time);

            if (dotGlowEnd.TryGetValue(cell, out float end))
            {
                if (end > now)
                {
                    float glow = Mathf.Clamp01((end - now) / DotGlowDuration);
                    Ink.Circle(point, radius * (1 + glow * 1.7f), Ink.WithAlpha(Ink.Gold, glow * .38f));
                    Ink.Circle(point, radius * (1 + glow * .32f), Color.Lerp(color, Ink.Hex("FFF1A5"), glow));
                    return;
                }
                dotGlowEnd.Remove(cell);
            }
            Ink.Circle(point, radius, color);
        }

        void TraceClearedRoute(int index, float tailDistance)
        {
            var path = Arrows.Paths[index];
            for (int i = 1; i < path.Count && tailDistance > 0; i++)
            {
                Vector3 start = path[i - 1], end = path[i];
                float length = Vector3.Distance(start, end);
                if (length <= .0001f) continue;
                // Reveal the original shaft only after its moving tail has cleared it.
                float cleared = Mathf.Min(length, tailDistance);
                TraceGridSegment(start, Vector3.LerpUnclamped(start, end, cleared / length), tailDistance > length);
                tailDistance -= length;
            }
        }

        void TraceGridSegment(Vector3 start, Vector3 end, bool includeEnd)
        {
            Vector3 delta = end - start;
            bool stepX = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            float origin = stepX ? start.x : start.y;
            float change = stepX ? delta.x : delta.y;
            if (Mathf.Abs(change) <= .0001f) return;
            float edge = origin + change;
            var bounds = Arrows.ContentBounds;
            float lower = stepX ? bounds.xMin : bounds.yMin;
            float upper = stepX ? bounds.xMax : bounds.yMax;
            int first = Mathf.CeilToInt(Mathf.Max(lower, Mathf.Min(origin, edge)) - .0001f);
            int last = Mathf.FloorToInt(Mathf.Min(upper, Mathf.Max(origin, edge)) + .0001f);
            for (int axis = first; axis <= last; axis++)
            {
                float fraction = (axis - origin) / change;
                if (fraction > 1.00001f || (!includeEnd && fraction >= .99999f)) continue;
                var point = start + delta * fraction;
                int x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y);
                if (Mathf.Abs(point.x - x) > .001f || Mathf.Abs(point.y - y) > .001f ||
                    x < bounds.xMin - .001f || x > bounds.xMax + .001f ||
                    y < bounds.yMin - .001f || y > bounds.yMax + .001f)
                    continue;
                var cell = new Vector3(x, y, 0);
                if (tracedGridDots.Add(cell))
                    PulseClearedDot(cell, Time.unscaledTime, 1);
            }
        }

        void CompleteArrowGridTrace(int index)
        {
            if (Arrows == null || Arrows.Cube) return;
            TraceClearedRoute(index, float.PositiveInfinity);
        }

        void PulseClearedDot(Vector3 cell, float now, float strength)
        {
            if (strength <= 0) return;
            float end = now + DotGlowDuration * strength;
            if (!dotGlowEnd.TryGetValue(cell, out float previous) || end > previous)
                dotGlowEnd[cell] = end;
        }

        void AddDotRaySweep(int index, float previous, float current, float now)
        {
            if (Mathf.Abs(current - previous) < .001f) return;
            dotRaySweeps.Add((Arrows.Head(index), Arrows.Direction(index).normalized, previous, current, now));
        }

        void PulseDotOnExitRay(Vector3 cell, Vector3 head, Vector3 direction, float previous, float current, float now)
        {
            Vector3 relative = cell - head;
            float along = Vector3.Dot(relative, direction);
            if (along <= .001f || along < Mathf.Min(previous, current) || along > Mathf.Max(previous, current)) return;
            if ((relative - direction * along).sqrMagnitude > .04f) return;
            float strength = current > previous ? Mathf.InverseLerp(previous, current, along)
                : 1 - Mathf.InverseLerp(current, previous, along);
            PulseClearedDot(cell, now, strength);
        }

        float FlightDistance(int index, ArrowMotionPath route, float elapsed, bool exitScreen, Vector2 projectionOffset)
        {
            float t = Mathf.Clamp01(elapsed / FlightDuration(index));
            float travel = route.Length + Mathf.Max(Mathf.Max(Level.width, Level.height),
                Mathf.Max(Arrows.ContentBounds.width, Arrows.ContentBounds.height)) + 3;
            if (exitScreen)
            {
                var head = Project(Arrows.Head(index));
                var heading = (Project(Arrows.Head(index) + Arrows.Direction(index)) - head).normalized;
                travel = route.Length + (BoardLayout.RayDistanceToEdge(head + projectionOffset, heading, gameViewport) +
                    ArrowVisualCell) / Mathf.Max(boardCell, .001f);
            }
            else if (Level.followDiagonalPaths)
                travel = route.Length + new Vector2(Level.width, Level.height).magnitude + 3;
            return travel * t * t * (2 - t);
        }
        void DrawHeldArrowRoute(Vector2 boardOrigin)
        {
            int index=PreviewArrow;if(index<0||!Arrows.Active[index])return;
            // Draw after all board/UI groups in physical screen coordinates.
            // Derive direction directly: subtracting two projected positions can
            // leave tiny off-axis components and prematurely truncate the ray.
            var head=screenOffset+(Project(Arrows.Head(index))+boardOrigin)*scale;
            var worldDirection=Arrows.Direction(index);
            var direction=BoardLayout.ScreenDirection(new Vector2(worldDirection.x,worldDirection.y), rotateArrowBoard).normalized;
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
            if (Arrows.Chameleon != null)
            {
                var color = ChameleonPalette[Arrows.ChameleonColor(i)];
                return i == hint && Time.unscaledTime < hintUntil
                    ? Color.Lerp(color, Color.white, .3f + .24f * Mathf.Sin(Time.unscaledTime * 10)) : color;
            }
            if(i==hint&&Time.unscaledTime<hintUntil)return Ink.Gold;
            if(collidedArrows.Contains(i))return Arrows.CanExit(i)?Ink.Hex("32B956"):Ink.Red;
            if(i==PreviewArrow)return HeldArrowColor;
            if(Mode==GameMode.ArrowWorkshop&&workshopRotateSelected)return CircuitStyle.Accent;
            if(Mode==GameMode.ArrowClash)return Color.Lerp(Ink.Palette[Mathf.Abs(Level.arrows[i].color)%7],Ink.Hex("101D35"),.12f);
            if(Mode==GameMode.CatEscape||Mode==GameMode.Zoo)return Color.Lerp(Ink.Palette[i%7],Ink.Hex("101D35"),.12f);
            if(Mode==GameMode.Countdown&&i==UrgentArrow())return Ink.Red;
            if(Arrows.Cube)return Ink.Palette[Mathf.Abs(Level.arrows[i].points[0]%6)%7];
            return CircuitStyle.ArrowInk;
        }
        void DrawArrowPath(int i,float flight=-1,bool exitScreen=false,Color? colorOverride=null)
        {
            // Keep a readable stroke in screen pixels even on dense boards and
            // downscaled tablet/phone canvases; grow proportionally when zooming.
            var path=Arrows.Paths[i];var color=colorOverride??ArrowColor(i);float width=ArrowStrokeWidth;
            if(i==PreviewArrow)width*=1.25f;
            if(!Arrows.Cube)width=Mathf.Min(width,ArrowVisualCell*.25f);
            if(Arrows.IsMechanism(i)&&path.Count>0){var at=Project(path[0]);Ink.Card(new Rect(at.x-boardCell*.23f,at.y-boardCell*.23f,boardCell*.46f,boardCell*.46f),Ink.Hex("C5CFC1"),6);Ink.TextAt(new Rect(at.x-20,at.y-20,40,40),Level.arrows[i].special==3004?"↻":"×",20,Ink.Text,TextAnchor.MiddleCenter,true);return;}
            if(path.Count<2)return;
            bool hidden=Mode==GameMode.Reasoning&&elapsed>3&&i!=PreviewArrow&&!(i==revealArrow&&Time.unscaledTime<revealUntil)&&!(i==hint&&Time.unscaledTime<hintUntil);
            if(hidden)color=Ink.WithAlpha(color,.13f);
            var route=MotionPath(i);float distance=0;
            if (!Arrows.Cube && flight < 0 && !blockedFlights.ContainsKey(i))
            {
                DrawStaticArrow(i, route, width, color);
                return;
            }
            if(flight>=0)
            {
                float t=Mathf.Clamp01(flight/FlightDuration(i));
                distance = FlightDistance(i, route, flight, exitScreen, Vector2.zero);
                if (!exitScreen)
                    color.a*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1,t));
            }
            else if(blockedFlights.TryGetValue(i,out var blocked))distance=blocked.Offset;
            var headPoint=route.Sample(route.Length+distance);
            var head=Project(headPoint);
            var dir=(Project(headPoint+Arrows.Direction(i))-head).normalized;
            float arrowSize=Mathf.Min(ArrowVisualCell*.58f,width*3.8f);
            // Overlap the head at the tip without letting the shaft's rounded cap stick out.
            route.Slice(distance,motionPoints,width*.35f/boardCell);var points=motionPoints;
            if (points.Count > 1)
            {
                var previous = Project(points[0]);
                for (int k = 1; k < points.Count; k++)
                {
                    var next = Project(points[k]);
                    Ink.Line(previous, next, width, color);
                    previous = next;
                }
            }
            CircuitStyle.ArrowHead(head,dir,arrowSize,width,color);
            DrawArrowMarkers(i, flight, head);
        }

        void DrawStaticArrow(int index, ArrowMotionPath route, float width, Color color)
        {
            if (!projectedArrows.TryGetValue(index, out var arrow))
                projectedArrows[index] = arrow = new ProjectedArrow();

            float arrowSize = Mathf.Min(ArrowVisualCell * .58f, width * 3.8f);
            if (arrow.ProjectionVersion != projectionVersion || arrow.StrokeWidth != width)
            {
                arrow.ProjectionVersion = projectionVersion;
                arrow.StrokeWidth = width;
                var head = route.Sample(route.Length);
                arrow.Head = Project(head);
                arrow.Direction = (Project(head + Arrows.Direction(index)) - arrow.Head).normalized;
                route.Slice(0, motionPoints, width * .35f / boardCell);
                arrow.Shaft.Clear();
                var min = arrow.Head;
                var max = arrow.Head;
                foreach (var vertex in motionPoints)
                {
                    var point = Project(vertex);
                    arrow.Shaft.Add(point);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                float padding = Mathf.Max(arrowSize + width, boardCell * .34f);
                if (Mode == GameMode.Countdown) padding = Mathf.Max(padding, 65);
                arrow.Bounds = Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
            }
            if (!currentBoard.Overlaps(arrow.Bounds)) return;
            for (int i = 1; i < arrow.Shaft.Count; i++)
                Ink.Line(arrow.Shaft[i - 1], arrow.Shaft[i], width, color);
            CircuitStyle.ArrowHead(arrow.Head, arrow.Direction, arrowSize, width, color);
            DrawArrowMarkers(index, -1, arrow.Head);
        }

        void DrawArrowMarkers(int i, float flight, Vector2 head)
        {
            if(i==hint&&Time.unscaledTime<hintUntil)Ink.Circle(head,boardCell*.34f,Ink.WithAlpha(Ink.Gold,.25f));
            if(Mode==GameMode.Countdown&&i==UrgentArrow()&&flight<0)Ink.TextAt(new Rect(head.x-30,head.y-65,60,44),Mathf.Max(0,countdownDeadline-moves).ToString(),25,Ink.Red,TextAnchor.MiddleCenter,true);
        }
        int PickArrow(Vector2 p)
        {
            int found=-1;float best=ArrowTapRadius;
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
        void DrawOcta(Rect r)
        {
            var bounds = new Rect(0, 0, Level.width - 1, Level.height - 1);
            float cell = BoardLayout.FitBestOrientation(r, bounds, out bool rotate, Level.keepBoardOrientation);
            Vector2 GridPoint(int index) => BoardLayout.ProjectOriented(new Vector2(index % Level.width, index / Level.width),
                bounds, r.center, cell, rotate);
            for(int y=0;y<Level.height;y++)for(int x=0;x<Level.width;x++)Ink.Circle(GridPoint(y*Level.width+x),Mathf.Min(2,cell*.10f),Ink.Hex("E4E8DE"));
            if(Level.portals!=null)for(int k=0;k<Level.portals.Length;k++)foreach(int p in new[]{Level.portals[k].a,Level.portals[k].b})
            {var v=GridPoint(p);Ink.Circle(v,cell*.38f,Ink.Palette[k%7]);Ink.Circle(v,cell*.23f,Color.white);}
            if(Level.turns!=null)foreach(var turn in Level.turns){var p=GridPoint(turn.y*Level.width+turn.x);var direction=BoardLayout.ScreenDirection(new Vector2(turn.dx,turn.dy),rotate);Ink.Circle(p,cell*.32f,Ink.Hex("F5E5B5"));Ink.Arrow(p-direction*cell*.16f,p+direction*cell*.17f,4,Ink.Text);}
            for(int i=0;i<Octa.Active.Length;i++)if(Octa.Active[i])
            {
                var a=Level.arrows[i];var p=GridPoint(a.points[0]);var c=i==hint&&Time.unscaledTime<hintUntil?Ink.Gold:Ink.Palette[i%7];
                Ink.Circle(p,cell*.31f,a.kind=="switch"?(a.open?Ink.Hex("CFE8D6"):Ink.Hex("F0C9C3")):Ink.WithAlpha(c,.13f));
                var dir=BoardLayout.ScreenDirection(new Vector2(a.dx,a.dy),rotate).normalized;Ink.Arrow(p-dir*cell*.18f,p+dir*cell*.22f,Mathf.Max(1.8f/scale,cell*.10f),c);
                if(a.kind=="splitter"){var dir2=BoardLayout.ScreenDirection(new Vector2(a.dx2,a.dy2),rotate).normalized;Ink.Arrow(p,p+dir2*cell*.22f,3,c);}
                if(a.kind=="rotateCW"||a.kind=="rotateCCW")Ink.TextAt(new Rect(p.x-cell*.5f,p.y-cell*.7f,cell,cell*.4f),a.kind=="rotateCW"?"↻":"↺",18,Ink.Muted,TextAnchor.MiddleCenter);
            }
            var e=Event.current;
            if(CanInteract&&e.type==EventType.MouseUp&&r.Contains(e.mousePosition))
            {
                for(int i=0;i<Octa.Active.Length;i++)if(Octa.Active[i]&&Vector2.Distance(e.mousePosition,GridPoint(Level.arrows[i].points[0]))<cell*.44f)
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
