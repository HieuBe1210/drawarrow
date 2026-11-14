using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    // A fixed, rounded route. Moving the visible interval preserves every bend;
    // moving only the original vertices would cut diagonally across corners.
    public sealed class ArrowMotionPath
    {
        readonly List<Vector3> points=new List<Vector3>();
        readonly List<float> distances=new List<float>();
        readonly Vector3 direction;
        public float Length { get; private set; }
        public ArrowMotionPath(List<Vector3> source,Vector3 exitDirection,float radius=.18f)
        {
            direction=exitDirection.normalized;
            if(source.Count==0)return;
            Add(source[0]);
            for(int i=1;i<source.Count-1;i++)
            {
                var a=source[i]-source[i-1];var b=source[i+1]-source[i];
                if(a.sqrMagnitude<.000001f||b.sqrMagnitude<.000001f)continue;
                if(Vector3.Dot(a.normalized,b.normalized)>.999f)continue;
                float trim=Mathf.Min(radius,Mathf.Min(a.magnitude,b.magnitude)*.45f);
                var start=source[i]-a.normalized*trim;var end=source[i]+b.normalized*trim;
                Add(start);
                for(int step=1;step<=8;step++)
                {
                    float t=step/8f,u=1-t;Add(u*u*start+2*u*t*source[i]+t*t*end);
                }
            }
            if(source.Count>1)Add(source[source.Count-1]);
        }
        void Add(Vector3 point)
        {
            if(points.Count>0){float d=Vector3.Distance(points[points.Count-1],point);if(d<.00001f)return;Length+=d;}
            points.Add(point);distances.Add(Length);
        }
        public Vector3 Sample(float distance)
        {
            if(points.Count==0)return Vector3.zero;
            if(distance<=0)return points[0];
            for(int i=1;i<points.Count;i++)if(distance<=distances[i])
                return Vector3.Lerp(points[i-1],points[i],(distance-distances[i-1])/(distances[i]-distances[i-1]));
            return points[points.Count-1]+direction*(distance-Length);
        }
        public void Slice(float offset,List<Vector3> result)
        {
            result.Clear();if(points.Count==0)return;
            float start=Mathf.Max(0,offset),end=start+Length;
            result.Add(Sample(start));
            for(int i=1;i<points.Count;i++)if(distances[i]>start&&distances[i]<end)result.Add(points[i]);
            result.Add(Sample(end));
        }
    }
}
