using UnityEngine;

namespace DrawArrow
{
    // A hold previews only; its release must never become a gameplay tap.
    public sealed class ArrowPressGesture
    {
        public const float HoldSeconds=.35f;
        public int Arrow {get;private set;}=-1;
        public bool Held {get;private set;}
        bool tracking;float started,movementTolerance=14;Vector2 origin;
        public void Begin(int arrow,Vector2 position,float now,float tolerance=14)
        {Arrow=arrow;origin=position;started=now;movementTolerance=Mathf.Max(1,tolerance);tracking=true;Held=false;}
        public void Tick(float now)
        {if(tracking&&Arrow>=0&&now-started>=HoldSeconds)Held=true;}
        public void Move(Vector2 position,bool onPressedArrow=false)
        {
            // A finger can drift along a long arrow while holding it. Only
            // leaving that arrow by more than the drag tolerance cancels it.
            if(onPressedArrow){origin=position;return;}
            if((position-origin).sqrMagnitude>movementTolerance*movementTolerance)Cancel();
        }
        public int Release(float now)
        {Tick(now);int tap=tracking&&!Held?Arrow:-1;Cancel();return tap;}
        public void Cancel(){tracking=false;Arrow=-1;Held=false;}
    }
}
