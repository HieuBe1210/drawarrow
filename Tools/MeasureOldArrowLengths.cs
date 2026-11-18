using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using DrawArrow;
using UnityEngine;
class LengthReference {
 static LevelData Read(BinaryReader r){var l=new LevelData{width=r.ReadInt32(),height=r.ReadInt32()};int n=r.ReadInt32();l.arrows=new ArrowData[n];for(int i=0;i<n;i++){int dx=r.ReadInt32(),dy=r.ReadInt32(),count=r.ReadInt32();var p=new int[count];for(int j=0;j<count;j++)p[j]=r.ReadInt32();l.arrows[i]=new ArrowData{points=p,dx=dx,dy=dy};}return l;}
 static string F(double x)=>x.ToString("0.###",CultureInfo.InvariantCulture);
 static double Percentile(List<double> x,double t){x.Sort();return x[(int)Math.Round((x.Count-1)*t)];}
 static string Stats(List<double> lengths){double sum=0;foreach(var x in lengths)sum+=x;return "{\"p10\":"+F(Percentile(lengths,.1))+",\"median\":"+F(Percentile(lengths,.5))+",\"p90\":"+F(Percentile(lengths,.9))+",\"mean\":"+F(sum/lengths.Count)+"}";}
 static List<double> Measure(LevelData l,out double bends){var b=new ArrowBoard(l);float cell=BoardLayout.FitScale(new Rect(0,0,1016,1366),BoardLayout.OrientedBounds(b.ContentBounds))*.5f;var lengths=new List<double>();bends=0;for(int i=0;i<b.Paths.Length;i++){var p=b.Paths[i];double len=0;for(int j=1;j<p.Count;j++){len+=Vector3.Distance(p[j-1],p[j]);if(j>1&&Vector3.Dot((p[j-1]-p[j-2]).normalized,(p[j]-p[j-1]).normalized)<.999f)bends++;}lengths.Add(len*cell);}bends/=b.Paths.Length;return lengths;}
 static void Main(){var oldAll=new List<double>();var newAll=new List<double>();var rows=new List<string>();int longer=0,shorter=0;using(var r=new BinaryReader(File.OpenRead("Temp/LengthReference/check.bin")))while(r.BaseStream.Position<r.BaseStream.Length){int number=r.ReadInt32();var a=Read(r);var b=Read(r);double ba,bb;var la=Measure(a,out ba);var lb=Measure(b,out bb);if(la.Count!=lb.Count)throw new Exception("Count mismatch "+number);oldAll.AddRange(la);newAll.AddRange(lb);double ratio=Percentile(lb,.5)/Math.Max(.001,Percentile(la,.5));if(ratio>1.2)longer++;if(ratio<.8)shorter++;rows.Add("{\"level\":"+number+",\"arrows\":"+la.Count+",\"oldLengthPixels\":"+Stats(la)+",\"newLengthPixels\":"+Stats(lb)+",\"medianRatio\":"+F(ratio)+",\"oldBendsPerArrow\":"+F(ba)+",\"newBendsPerArrow\":"+F(bb)+"}");}
 string summary="{\"levels\":"+rows.Count+",\"arrows\":"+oldAll.Count+",\"referenceViewport\":\"540px device, 508x683px board\",\"oldLengthPixels\":"+Stats(oldAll)+",\"newLengthPixels\":"+Stats(newAll)+",\"boardsOver120PercentOldMedian\":"+longer+",\"boardsUnder80PercentOldMedian\":"+shorter+"}";File.WriteAllText("Verification/old-arrow-length-reference.json","{\"summary\":"+summary+",\"rows\":["+string.Join(",",rows)+"]}");Console.WriteLine(summary);foreach(int n in new[]{1,5,10,100,600,1574})Console.WriteLine(rows[n-1]);
 }
}
