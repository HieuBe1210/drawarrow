using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using DrawArrow;
using UnityEngine;
class ExportOldLengthTargets {
 static LevelData Read(BinaryReader r){var l=new LevelData{width=r.ReadInt32(),height=r.ReadInt32()};int n=r.ReadInt32();l.arrows=new ArrowData[n];for(int i=0;i<n;i++){int dx=r.ReadInt32(),dy=r.ReadInt32(),count=r.ReadInt32();var p=new int[count];for(int j=0;j<count;j++)p[j]=r.ReadInt32();l.arrows[i]=new ArrowData{points=p,dx=dx,dy=dy};}return l;}
 static string F(double x)=>x.ToString("0.###",CultureInfo.InvariantCulture);
 static double Percentile(List<double> x,double t){x.Sort();return x[(int)Math.Round((x.Count-1)*t)];}
 static string Stats(List<double> lengths){double sum=0;foreach(var x in lengths)sum+=x;return "{\"p10\":"+F(Percentile(lengths,.1))+",\"median\":"+F(Percentile(lengths,.5))+",\"p90\":"+F(Percentile(lengths,.9))+",\"mean\":"+F(sum/lengths.Count)+"}";}
 static List<double> Measure(LevelData l,out double bends){var b=new ArrowBoard(l);float cell=BoardLayout.FitScale(new Rect(0,0,1016,1366),BoardLayout.OrientedBounds(b.ContentBounds))*.5f;var lengths=new List<double>();bends=0;for(int i=0;i<b.Paths.Length;i++){var p=b.Paths[i];double len=0;for(int j=1;j<p.Count;j++){len+=Vector3.Distance(p[j-1],p[j]);if(j>1&&Vector3.Dot((p[j-1]-p[j-2]).normalized,(p[j]-p[j-1]).normalized)<.999f)bends++;}lengths.Add(len*cell);}bends/=b.Paths.Length;return lengths;}
 static void Main(){var rows=new List<string>();using(var r=new BinaryReader(File.OpenRead("Temp/LengthReference/check.bin")))while(r.BaseStream.Position<r.BaseStream.Length){int number=r.ReadInt32();var l=Read(r);Read(r);var board=new ArrowBoard(l);double bends;var lengths=Measure(l,out bends);var bs=new List<int>();for(int i=0;i<board.Paths.Length;i++){var p=board.Paths[i];int b=0;for(int j=2;j<p.Count;j++)if(Vector3.Dot((p[j-1]-p[j-2]).normalized,(p[j]-p[j-1]).normalized)<.999f)b++;bs.Add(b);}rows.Add("{\"level\":"+number+",\"lengths\":["+string.Join(",",lengths.ConvertAll(F))+"],\"bends\":["+string.Join(",",bs)+"]}");}File.WriteAllText("Temp/LengthReference/targets.json","["+string.Join(",",rows)+"]");Console.WriteLine("Exported "+rows.Count);}
}