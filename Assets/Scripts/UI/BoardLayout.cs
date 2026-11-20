using UnityEngine;

namespace DrawArrow
{
    public static class BoardLayout
    {
        public static bool ShouldRotate(Rect content, bool keepOrientation = false)
            => !keepOrientation && content.width > content.height;
        public static Rect OrientedBounds(Rect content, bool keepOrientation = false)
            =>ShouldRotate(content, keepOrientation)?new Rect(0,0,content.height,content.width):content;
        // Clockwise quarter-turn in screen space. Only board coordinates use
        // this transform; GUI matrices and the surrounding controls stay upright.
        public static Vector2 ScreenDirection(Vector2 direction,bool rotate)
            =>rotate?new Vector2(direction.y,direction.x):new Vector2(direction.x,-direction.y);
        public static Vector2 Project(Vector2 point,Rect content,Vector2 center,float cellSize,bool keepOrientation = false)
            => ProjectOriented(point, content, center, cellSize, ShouldRotate(content, keepOrientation));

        public static Vector2 ProjectOriented(Vector2 point, Rect content, Vector2 center, float cellSize, bool rotate)
            => center + ScreenDirection(point - content.center, rotate) * cellSize;

        // Reserve half a cell on each side for the arrowhead and stroke caps.
        public static float FitScale(Rect viewport,Rect content)
            =>Mathf.Min(viewport.width/Mathf.Max(1,content.width+1),viewport.height/Mathf.Max(1,content.height+1));

        public static float FitBestOrientation(Rect viewport, Rect content, out bool rotate,
            bool keepOrientation = false, Rect? reference = null)
        {
            float Fit(bool quarterTurn)
            {
                Rect Orient(Rect bounds) => quarterTurn ? new Rect(0, 0, bounds.height, bounds.width) : bounds;
                float size = FitScale(viewport, Orient(content));
                if (reference.HasValue)
                    size = Mathf.Min(size, FitScale(viewport, Orient(reference.Value)));
                return size;
            }

            float upright = Fit(false);
            float turned = keepOrientation ? upright : Fit(true);
            rotate = turned > upright + .001f;
            return rotate ? turned : upright;
        }

        public static Rect Viewport(Rect safeArea,float screenHeight,Vector2 offset,float scale)
            =>new Rect((safeArea.x-offset.x)/scale,(screenHeight-safeArea.yMax-offset.y)/scale,safeArea.width/scale,safeArea.height/scale);
        public static float RayDistanceToEdge(Vector2 head,Vector2 direction,Rect viewport)
        {
            if(direction.sqrMagnitude<.000001f)return 0;
            // Ignore projection noise along an otherwise cardinal direction.
            if(Mathf.Abs(direction.x)<.00001f)direction.x=0;
            if(Mathf.Abs(direction.y)<.00001f)direction.y=0;
            float x=direction.x>0?(viewport.xMax-head.x)/direction.x:direction.x<0?(viewport.xMin-head.x)/direction.x:float.PositiveInfinity;
            float y=direction.y>0?(viewport.yMax-head.y)/direction.y:direction.y<0?(viewport.yMin-head.y)/direction.y:float.PositiveInfinity;
            return Mathf.Max(0,Mathf.Min(x,y));
        }
        public static float ArrowHeadDistance(Vector2 point,Vector2 head,Vector2 direction,float length)
        {
            if(direction.sqrMagnitude<.000001f||length<=0)return Vector2.Distance(point,head);
            var side=new Vector2(-direction.y,direction.x)*length*.4f;
            var a=head-direction*length+side;var b=head-direction*length-side;
            float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;
            float x=Cross(a-head,point-head),y=Cross(b-a,point-a),z=Cross(head-b,point-b);
            if((x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0))return 0;
            return Mathf.Min(LinkBoard.DistanceToSegment(point,head,a),Mathf.Min(LinkBoard.DistanceToSegment(point,a,b),LinkBoard.DistanceToSegment(point,b,head)));
        }
    }
}
