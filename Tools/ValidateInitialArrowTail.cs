using System;
using System.IO;
using System.Collections.Generic;
using DrawArrow;
using UnityEngine;
class InitialTailCheck {
 static int levels,arrows,failures,trimmed;static List<Vector3> visible=new List<Vector3>();
 static bool Clip(float a,float b,float min,float max,ref float low,ref float high){float d=b-a;if(Math.Abs(d)<1e-7)return a>=min&&a<=max;float u=(min-a)/d,v=(max-a)/d;if(u>v){float t=u;u=v;v=t;}low=Math.Max(low,u);high=Math.Min(high,v);return low<=high;}
 static bool HitsRay(Vector3 a,Vector3 b,Vector3 head,Vector3 direction,float distance){a-=head;b-=head;float fa=Vector3.Dot(a,direction),fb=Vector3.Dot(b,direction);if(Math.Max(fa,fb)<=.00001f)return false;float sa=a.x*direction.y-a.y*direction.x,sb=b.x*direction.y-b.y*direction.x;float low=0,high=1;return Clip(fa,fb,.00002f,distance,ref low,ref high)&&Clip(sa,sb,-.3f,.3f,ref low,ref high);}
 static void CheckPath(List<Vector3> path,Vector3 direction,float distance,string id){
  var head=path[path.Count-1];var route=new ArrowMotionPath(path,direction);route.Slice(0,visible);direction=direction.normalized;
  for(int k=1;k<visible.Count;k++)if(HitsRay(visible[k-1],visible[k],head,direction,distance)){
   failures++;if(failures<10)Console.WriteLine("FAIL initial self ray "+id+" segment="+visible[k-1]+" -> "+visible[k]);break;
  }
  var copy=new List<Vector3>(path);if(ArrowBoard.TrimSelfPointingTail(copy,direction))throw new Exception("Normalization not idempotent "+id);
 }
 static void Fixtures(){
  var spiral=new List<Vector3>{new Vector3(6,0),new Vector3(0,0),new Vector3(0,6),new Vector3(5,6),new Vector3(5,1),new Vector3(2,1),new Vector3(2,4),new Vector3(4,4)};
  for(int rotation=0;rotation<4;rotation++){
   var path=new List<Vector3>();foreach(var p in spiral){var q=p;for(int n=0;n<rotation;n++)q=new Vector3(-q.y,q.x);path.Add(q);}var direction=Vector3.right;for(int n=0;n<rotation;n++)direction=new Vector3(-direction.y,direction.x);
   var head=path[path.Count-1];if(!ArrowBoard.TrimSelfPointingTail(path,direction))throw new Exception("Spiral must be cut");if(path[path.Count-1]!=head)throw new Exception("Head changed");CheckPath(path,direction,20,"rotated spiral");
  }
  var u=new List<Vector3>{new Vector3(4,0),new Vector3(0,0),new Vector3(0,4),new Vector3(2,4),new Vector3(2,2)};
  if(!ArrowBoard.TrimSelfPointingTail(u,Vector3.down)||u[0].x>=2)throw new Exception("U tail is not cut past ray");CheckPath(u,Vector3.down,20,"U fixture");
  var straight=new List<Vector3>{Vector3.zero,Vector3.right*4};if(ArrowBoard.TrimSelfPointingTail(straight,Vector3.right))throw new Exception("Safe arrow changed");
 }
 static void Main(){Fixtures();
  using(var input=new BinaryReader(File.OpenRead("Temp/arrow-motion-levels.bin")))while(input.BaseStream.Position<input.BaseStream.Length){
   int w=input.ReadInt32(),h=input.ReadInt32(),n=input.ReadInt32();var data=new LevelData{width=w,height=h,arrows=new ArrowData[n]};
   for(int i=0;i<n;i++){int dx=input.ReadInt32(),dy=input.ReadInt32(),count=input.ReadInt32();var points=new int[count];for(int j=0;j<count;j++)points[j]=input.ReadInt32();data.arrows[i]=new ArrowData{points=points,dx=dx,dy=dy};}
   var board=new ArrowBoard(data);for(int i=0;i<n;i++){
    arrows++;var a=data.arrows[i];var path=board.Paths[i];CheckPath(path,board.Direction(i),w+h+4,"level="+levels+" arrow="+i);
    if(a.points.Length>1){var expected=new Vector3(a.points[0]%w,a.points[0]/w);if(board.Head(i)!=expected)throw new Exception("Head moved");var oldTail=new Vector3(a.points[a.points.Length-1]%w,a.points[a.points.Length-1]/w);if(path[0]!=oldTail)trimmed++;}
   }levels++;if(levels%10000==0)Console.WriteLine("Progress levels="+levels+" arrows="+arrows);
  }
  string report="{\"passed\":"+(failures==0?"true":"false")+",\"levels\":"+levels+",\"arrows\":"+arrows+",\"trimmedArrows\":"+trimmed+",\"initialSelfPointingArrows\":"+failures+",\"playModeVerified\":false}";
  File.WriteAllText("Verification/arrow-initial-tail.json",report);Console.WriteLine(report);if(failures>0)Environment.Exit(1);
 }
}
