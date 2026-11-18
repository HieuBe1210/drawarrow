using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using DrawArrow;
using UnityEngine;
class DirectionVarietyCheck {
 static int checks,added;static float minCell=float.MaxValue;static List<Vector3> visible=new List<Vector3>();
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static LevelData Read(BinaryReader input){int w=input.ReadInt32(),h=input.ReadInt32(),n=input.ReadInt32();var l=new LevelData{width=w,height=h,arrows=new ArrowData[n]};for(int i=0;i<n;i++){int dx=input.ReadInt32(),dy=input.ReadInt32(),count=input.ReadInt32();var p=new int[count];for(int j=0;j<count;j++)p[j]=input.ReadInt32();l.arrows[i]=new ArrowData{points=p,dx=dx,dy=dy};}return l;}
 static bool Clip(float a,float b,float min,float max,ref float low,ref float high){float d=b-a;if(Math.Abs(d)<1e-7)return a>=min&&a<=max;float u=(min-a)/d,v=(max-a)/d;if(u>v){float t=u;u=v;v=t;}low=Math.Max(low,u);high=Math.Min(high,v);return low<=high;}
 static bool Hit(Vector3 a,Vector3 b,Vector3 head,Vector3 dir,float distance){a-=head;b-=head;float fa=Vector3.Dot(a,dir),fb=Vector3.Dot(b,dir);if(Math.Max(fa,fb)<=.00001f)return false;float sa=a.x*dir.y-a.y*dir.x,sb=b.x*dir.y-b.y*dir.x,low=0,high=1;return Clip(fa,fb,.00002f,distance,ref low,ref high)&&Clip(sa,sb,-.3f,.3f,ref low,ref high);}
 static void Main(){int count=0;using(var input=new BinaryReader(File.OpenRead("Temp/DirectionVariety/check.bin")))while(input.BaseStream.Position<input.BaseStream.Length){int number=input.ReadInt32();var source=Read(input);var data=Read(input);var old=new ArrowBoard(source);var board=new ArrowBoard(data);Check(number>=1&&number<=1570,"Scope");Check(data.width==source.width&&data.height==source.height,"Grid preserved");int n=source.arrows.Length,m=data.arrows.Length;Check(m>=n,"Arrow count preserved/increased at "+number);added+=m-n;
  Check(Vector2.Distance(old.ContentBounds.min,board.ContentBounds.min)<.001f&&Vector2.Distance(old.ContentBounds.max,board.ContentBounds.max)<.001f,"Board bounds preserved at "+number);
  var cells=new HashSet<Vector3Int>();for(int i=0;i<m;i++){var path=board.Paths[i];Check(path.Count>=2,"Visible arrow length");Check(path.Count==data.arrows[i].points.Length,"No runtime tail correction needed at "+number+" arrow "+i);foreach(var p in path)Check(cells.Add(ArrowBoard.Key(p)),"No occupancy overlap at "+number);
   var route=new ArrowMotionPath(path,board.Direction(i));route.Slice(0,visible);for(int k=1;k<visible.Count;k++)Check(!Hit(visible[k-1],visible[k],board.Head(i),board.Direction(i),data.width+data.height+4),"No rendered self-pointing arrow at "+number+" arrow "+i);
  }
  var solution=board.Solve();Check(solution.Length==m,"Actual runtime solver clears entire level "+number);foreach(int i in solution)Check(board.Remove(i),"Recorded solution replay at "+number);Check(board.Remaining==0,"No arrows left");
  // Same grid and bounds mean the denser version never shrinks cell/stroke size.
  float cell=BoardLayout.FitScale(new Rect(0,0,1016,1366),BoardLayout.OrientedBounds(board.ContentBounds))*.5f;minCell=Math.Min(minCell,cell);
  count++;if(count%50==0)Console.WriteLine("Validated "+count+"/4");
 }Check(count==4,"All milestones validated");string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Temp/DirectionVariety/levels.json"))).Replace("-","").ToLowerInvariant();
 string report="{\"passed\":true,\"levels\":"+count+",\"checks\":"+checks+",\"totalAdded\":"+added+",\"candidateSha256\":\""+hash+"\",\"actualRuntimeSolver\":true,\"solutionsReplayed\":true,\"noOverlap\":true,\"noInitialSelfPointing\":true,\"boundsPreserved\":true,\"minimumCellPixelsAt540Wide\":"+minCell.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"playModeVerified\":false}";File.WriteAllText("Verification/direction-variety-validation.json",report);Console.WriteLine(report);
 }
}
