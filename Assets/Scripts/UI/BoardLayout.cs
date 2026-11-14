using UnityEngine;

namespace DrawArrow
{
    public static class BoardLayout
    {
        public static bool ShouldRotate(Rect content)=>content.width>content.height;
        public static Rect OrientedBounds(Rect content)
            =>ShouldRotate(content)?new Rect(0,0,content.height,content.width):content;
        // Clockwise quarter-turn in screen space. Only board coordinates use
        // this transform; GUI matrices and the surrounding controls stay upright.
        public static Vector2 ScreenDirection(Vector2 direction,bool rotate)
            =>rotate?new Vector2(direction.y,direction.x):new Vector2(direction.x,-direction.y);
        public static Vector2 Project(Vector2 point,Rect content,Vector2 center,float cellSize)
            =>center+ScreenDirection(point-content.center,ShouldRotate(content))*cellSize;

        // Reserve half a cell on each side for the arrowhead and stroke caps.
        public static float FitScale(Rect viewport,Rect content)
            =>Mathf.Min(viewport.width/Mathf.Max(1,content.width+1),viewport.height/Mathf.Max(1,content.height+1));

        public static Rect Viewport(Rect safeArea,float screenHeight,Vector2 offset,float scale)
            =>new Rect((safeArea.x-offset.x)/scale,(screenHeight-safeArea.yMax-offset.y)/scale,safeArea.width/scale,safeArea.height/scale);
    }
}
