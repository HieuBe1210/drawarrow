extern alias MonoDrawing;
using System;
using System.IO;
using System.Collections.Generic;
using DrawArrow;
using UnityEngine;
using D=MonoDrawing::System.Drawing;
class GapFillPreview {
 static LevelData Read(BinaryReader r){var l=new LevelData{width=r.ReadInt32(),height=r.ReadInt32()};int n=r.ReadInt32();l.arrows=new ArrowData[n];for(int i=0;i<n;i++){int dx=r.ReadInt32(),dy=r.ReadInt32(),count=r.ReadInt32();var p=new int[count];for(int j=0;j<count;j++)p[j]=r.ReadInt32();l.arrows[i]=new ArrowData{points=p,dx=dx,dy=dy};}return l;}
 static void Draw(D.Graphics g,LevelData level,int left,string label){
 var board=new ArrowBoard(level);var rect=new Rect(left+10.67f,114.67f,338.66f,567.83f);var cell=BoardLayout.FitScale(rect,BoardLayout.OrientedBounds(board.ContentBounds));
 D.PointF Project(Vector3 p){var q=BoardLayout.Project(new Vector2(p.x,p.y),board.ContentBounds,rect.center,cell);return new D.PointF(q.x,q.y);}
 var color=D.ColorTranslator.FromHtml("#0C1736");using(var ink=new D.SolidBrush(color))using(var font=new D.Font("Segoe UI",15,D.FontStyle.Bold))using(var small=new D.Font("Segoe UI",10)){
 g.DrawString(label,font,ink,left+18,20);g.DrawString("Level 600 | "+level.arrows.Length+" arrows",small,ink,left+18,53);
 float width=Mathf.Clamp(cell*.2f,1.8f,12);using(var pen=new D.Pen(color,width)){pen.StartCap=pen.EndCap=D.Drawing2D.LineCap.Round;pen.LineJoin=D.Drawing2D.LineJoin.Round;var points=new List<Vector3>();
 for(int i=0;i<level.arrows.Length;i++){var route=new ArrowMotionPath(board.Paths[i],board.Direction(i));route.Slice(0,points);var p=new D.PointF[points.Count];for(int k=0;k<p.Length;k++)p[k]=Project(points[k]);g.DrawLines(pen,p);var h=p[p.Length-1];var forward=Project(points[points.Count-1]+board.Direction(i));float dx=forward.X-h.X,dy=forward.Y-h.Y,len=(float)Math.Sqrt(dx*dx+dy*dy);dx/=len;dy/=len;float size=Math.Min(cell*.58f,width*3.8f);g.FillPolygon(ink,new[]{h,new D.PointF(h.X-dx*size-dy*size*.4f,h.Y-dy*size+dx*size*.4f),new D.PointF(h.X-dx*size+dy*size*.4f,h.Y-dy*size-dx*size*.4f)});}
 }
 g.DrawString("Standalone geometry preview",small,ink,left+18,740);
 }
 }
 static void Main(){using(var r=new BinaryReader(File.OpenRead("Temp/GapFill/check.bin")))while(r.BaseStream.Position<r.BaseStream.Length){int n=r.ReadInt32();var before=Read(r);var after=Read(r);if(n!=600)continue;using(var bitmap=new D.Bitmap(720,780))using(var g=D.Graphics.FromImage(bitmap)){g.SmoothingMode=D.Drawing2D.SmoothingMode.AntiAlias;g.Clear(D.ColorTranslator.FromHtml("#F5ECDB"));Draw(g,before,0,"Before");Draw(g,after,360,"After");bitmap.Save("Verification/gapfill-level600-preview.png",D.Imaging.ImageFormat.Png);}break;}}
}
