using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DrawArrow.Editor
{
    // Generate offline: gameplay never changes source puzzles or their outline.
    public static class MainInteriorVariants
    {
        const string Group="maininteriors";
        [Serializable] public class RowReport
        {
            public int level,arrows,outerArrows,interiorArrows,changedArrows;
            public bool solved;
        }
        [Serializable] public class Report
        {
            public int total,changedLevels,changedArrows,noInteriorLevels,unchangedInteriorLevels,unsolvedLevels;
            public string outputDirectory;
            public RowReport[] levels;
        }
        [Serializable] public class DiagnosticRow
        {
            public int level,outerArrows;
            public bool originalSolved,outlineAloneSolved;
        }
        [Serializable] public class DiagnosticReport {public DiagnosticRow[] levels;}
        [Serializable] public class AppliedReport
        {
            public int mainLevelCount,changedLevels,changedArrows,noInteriorLevels,unchangedInteriorLevels,unverifiedCandidatesKeptOriginal;
            public bool allAppliedVariantsSolved,outlinePreserved;
        }
        [Serializable] class MilestoneAppliedReport { public int supersededInteriorVariants; }

        static LevelData Original(LevelRepository repository,Catalog baseline,int index)
        {
            if(baseline.overrides!=null)foreach(var entry in baseline.overrides)
                if(entry.mode=="Classic"&&entry.index==index)return repository.Load(entry.group,entry.levelIndex);
            if(baseline.sequences!=null)foreach(var sequence in baseline.sequences)
                if(sequence.mode=="Classic")return repository.Load(sequence.group,sequence.indices[index]);
            return repository.Load("classic",index);
        }

        static void WritePack(string directory,int packIndex,List<LevelData> levels)
        {
            using(var output=File.Create(Path.Combine(directory,Group+"_"+packIndex.ToString("000")+".bytes")))
            using(var gzip=new GZipStream(output,System.IO.Compression.CompressionLevel.Optimal))
            {var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LevelPack{levels=levels.ToArray()}));gzip.Write(bytes,0,bytes.Length);}
        }

        [MenuItem("Draw Arrow/Apply solved main interior variants")]
        public static void ApplyCandidates()
        {
            var repository=new LevelRepository();
            if(repository.Count(Group)>0)throw new Exception("Interior variants are already applied; restore the saved catalog before regenerating");
            var candidates=JsonUtility.FromJson<Report>(File.ReadAllText("Verification/main-interior-variants.json"));
            string catalogPath="Assets/Resources/Levels/catalog.json",before=File.ReadAllText(catalogPath);
            var catalog=JsonUtility.FromJson<Catalog>(before);
            var replacements=new Dictionary<int,LevelOverride>();
            if(catalog.overrides!=null)foreach(var replacement in catalog.overrides)
                if(replacement.mode=="Classic")replacements[replacement.index]=replacement;
            var report=new AppliedReport{mainLevelCount=candidates.total,allAppliedVariantsSolved=true,outlinePreserved=true};
            Directory.CreateDirectory("Recovery/MainInteriors");
            File.WriteAllText("Recovery/MainInteriors/catalog-before.json",before);
            var pack=new List<LevelData>();LevelPack candidatePack=null;
            for(int n=0;n<candidates.total;n++)
            {
                if(n%128==0)
                {
                    using(var input=File.OpenRead(Path.Combine(candidates.outputDirectory,Group+"_"+(n/128).ToString("000")+".bytes")))
                    using(var gzip=new GZipStream(input,CompressionMode.Decompress))
                    using(var reader=new StreamReader(gzip))candidatePack=JsonUtility.FromJson<LevelPack>(reader.ReadToEnd());
                }
                var source=repository.Load(GameMode.Classic,n);var row=candidates.levels[n];
                var candidate=candidatePack.levels[n%128];
                bool apply=row.solved&&row.changedArrows>0;
                if(apply)
                {
                    if(candidate.id!=source.id+"-interior-v1")throw new Exception("Candidate/source mismatch at "+(n+1));
                    var locked=Outline(new ArrowBoard(source));
                    for(int i=0;i<locked.Length;i++)if(locked[i]&&JsonUtility.ToJson(source.arrows[i])!=JsonUtility.ToJson(candidate.arrows[i]))
                        throw new Exception("Outline mismatch at "+(n+1));
                    replacements[n]=new LevelOverride{mode="Classic",index=n,group=Group,levelIndex=n};
                    report.changedLevels++;report.changedArrows+=row.changedArrows;
                }
                else if(!row.solved)report.unverifiedCandidatesKeptOriginal++;
                else if(row.interiorArrows==0)report.noInteriorLevels++;else report.unchangedInteriorLevels++;
                pack.Add(apply?candidate:source);
                if(pack.Count==128||n==candidates.total-1){WritePack("Assets/Resources/Levels",n/128,pack);pack.Clear();}
            }
            var groups=new List<CatalogGroup>(catalog.groups);groups.Add(new CatalogGroup{name=Group,count=candidates.total,packSize=128});catalog.groups=groups.ToArray();
            var overrides=new List<LevelOverride>();
            if(catalog.overrides!=null)foreach(var entry in catalog.overrides)if(entry.mode!="Classic")overrides.Add(entry);
            var indices=new List<int>(replacements.Keys);indices.Sort();foreach(int index in indices)overrides.Add(replacements[index]);
            catalog.overrides=overrides.ToArray();File.WriteAllText(catalogPath,JsonUtility.ToJson(catalog));
            File.WriteAllText("Verification/main-interior-applied.json",JsonUtility.ToJson(report,true));
            Debug.Log("MAIN_INTERIOR_APPLIED changed="+report.changedLevels+" arrows="+report.changedArrows);
        }

        [MenuItem("Draw Arrow/Validate main interior variants")]
        public static void ValidateActiveVariants()
        {
            var repository=new LevelRepository();
            var baseline=JsonUtility.FromJson<Catalog>(File.ReadAllText("Recovery/MainInteriors/catalog-before.json"));
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Resources/Levels/catalog.json"));
            var report=JsonUtility.FromJson<AppliedReport>(File.ReadAllText("Verification/main-interior-applied.json"));
            if(repository.Count(GameMode.Classic)!=report.mainLevelCount)throw new Exception("Wrong main level count");
            int changed=0,checkedArrows=0;
            foreach(var entry in catalog.overrides)if(entry.mode=="Classic"&&entry.group==Group)
            {
                var source=Original(repository,baseline,entry.index);var actual=repository.Load(GameMode.Classic,entry.index);
                if(actual.id!=source.id+"-interior-v1"||actual.arrows.Length!=source.arrows.Length)
                    throw new Exception("Changed arrow count or source at "+(entry.index+1));
                var locked=Outline(new ArrowBoard(source));
                for(int i=0;i<locked.Length;i++)if(locked[i])
                {
                    if(JsonUtility.ToJson(source.arrows[i])!=JsonUtility.ToJson(actual.arrows[i]))throw new Exception("Changed outline at "+(entry.index+1));
                    checkedArrows++;
                }
                if(changed%1000==0&&new ArrowBoard(actual).Solve().Length!=actual.arrows.Length)
                    throw new Exception("Variant sample did not solve at "+(entry.index+1));
                changed++;
            }
            int superseded=0;
            if(repository.Count("milestonehard")>0&&File.Exists("Verification/milestone-difficulty-applied.json"))
                superseded=JsonUtility.FromJson<MilestoneAppliedReport>(File.ReadAllText("Verification/milestone-difficulty-applied.json")).supersededInteriorVariants;
            if(repository.Count("gapfilled")>0&&File.Exists("Verification/gapfill-applied.json"))
                superseded+=JsonUtility.FromJson<MilestoneAppliedReport>(File.ReadAllText("Verification/gapfill-applied.json")).supersededInteriorVariants;
            if(repository.Count("directionvariety")>0&&File.Exists("Verification/direction-variety-applied.json"))
                superseded+=JsonUtility.FromJson<MilestoneAppliedReport>(File.ReadAllText("Verification/direction-variety-applied.json")).supersededInteriorVariants;
            if(repository.Count("progression100")>0&&File.Exists("Verification/first100-difficulty-applied.json"))
                superseded+=JsonUtility.FromJson<MilestoneAppliedReport>(File.ReadAllText("Verification/first100-difficulty-applied.json")).supersededInteriorVariants;
            if(repository.Count("oldcount")>0&&File.Exists("Verification/old-arrow-counts-applied.json"))
                superseded+=JsonUtility.FromJson<MilestoneAppliedReport>(File.ReadAllText("Verification/old-arrow-counts-applied.json")).supersededInteriorVariants;
            if(changed+superseded!=report.changedLevels)throw new Exception("Wrong changed level count");
            File.WriteAllText("Verification/main-interior-validation.json","{\"passed\":true,\"changedLevels\":"+changed+",\"outlineArrowsChecked\":"+checkedArrows+",\"generationSolverCheckedEveryAppliedVariant\":true}");
            Debug.Log("MAIN_INTERIOR_VALIDATION_PASSED changed="+changed+" outlineArrows="+checkedArrows);
        }

        public static void DiagnoseCandidates()
        {
            var report=JsonUtility.FromJson<Report>(File.ReadAllText("Verification/main-interior-variants.json"));
            var repository=new LevelRepository();var rows=new List<DiagnosticRow>();
            foreach(var row in report.levels)if(!row.solved)
            {
                var level=repository.Load(GameMode.Classic,row.level-1);var board=new ArrowBoard(level);var locked=Outline(board);
                var outer=new List<ArrowData>();for(int i=0;i<locked.Length;i++)if(locked[i])outer.Add(level.arrows[i]);
                var outlineLevel=JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(level));outlineLevel.arrows=outer.ToArray();
                rows.Add(new DiagnosticRow{level=row.level,outerArrows=outer.Count,originalSolved=board.Solve().Length==level.arrows.Length,
                    outlineAloneSolved=new ArrowBoard(outlineLevel).Solve().Length==outer.Count});
            }
            File.WriteAllText("Verification/main-interior-diagnostics.json",JsonUtility.ToJson(new DiagnosticReport{levels=rows.ToArray()},true));
            Debug.Log("MAIN_INTERIOR_DIAGNOSTICS_COMPLETE count="+rows.Count);
        }

        static bool Ready(HashSet<int> dependencies, bool[] active)
        {
            foreach(int i in dependencies)if(active[i])return false;
            return true;
        }

        static HashSet<int> Dependencies(ArrowBoard board,int i,bool reverse)
        {
            var path=board.Paths[i];
            var head=reverse?path[0]:board.Head(i);
            var dir=board.Direction(i);
            if(reverse)
            {
                var d=path.Count>1?path[0]-path[1]:-dir;
                dir=Mathf.Abs(d.x)>=Mathf.Abs(d.y)?new Vector3(Mathf.Sign(d.x),0):new Vector3(0,Mathf.Sign(d.y));
            }
            var result=new HashSet<int>();
            for(int step=1;step<=board.Level.width+board.Level.height+5;step++)
            {
                var p=head+dir*step;
                if(p.x<-.4f||p.y<-.4f||p.x>board.Level.width-.6f||p.y>board.Level.height-.6f)break;
                if(board.Occupancy.TryGetValue(ArrowBoard.Key(p),out int other)&&other!=i)result.Add(other);
            }
            return result;
        }

        static bool[] Outline(ArrowBoard board)
        {
            var rows=new Dictionary<int,Vector2Int>();var cols=new Dictionary<int,Vector2Int>();
            foreach(var path in board.Paths)foreach(var p in path)
            {
                var cell=ArrowBoard.Key(p);
                if(!rows.TryGetValue(cell.y,out var row))row=new Vector2Int(cell.x,cell.x);
                rows[cell.y]=new Vector2Int(Math.Min(row.x,cell.x),Math.Max(row.y,cell.x));
                if(!cols.TryGetValue(cell.x,out var col))col=new Vector2Int(cell.y,cell.y);
                cols[cell.x]=new Vector2Int(Math.Min(col.x,cell.y),Math.Max(col.y,cell.y));
            }
            var locked=new bool[board.Paths.Length];
            for(int i=0;i<locked.Length;i++)foreach(var p in board.Paths[i])
            {
                var c=ArrowBoard.Key(p);var row=rows[c.y];var col=cols[c.x];
                if(c.x==row.x||c.x==row.y||c.y==col.x||c.y==col.y){locked[i]=true;break;}
            }
            return locked;
        }

        static LevelData Variant(LevelData original, int number, out RowReport report)
        {
            var board=new ArrowBoard(original);var locked=Outline(board);int count=locked.Length;
            report=new RowReport{level=number,arrows=count};
            var normal=new HashSet<int>[count];var reverse=new HashSet<int>[count];
            var active=new bool[count];var flipped=new bool[count];
            for(int i=0;i<count;i++)
            {
                active[i]=true;normal[i]=Dependencies(board,i,false);reverse[i]=Dependencies(board,i,true);
                if(locked[i])report.outerArrows++;else report.interiorArrows++;
            }
            int remaining=count;
            while(remaining>0)
            {
                int selected=-1;
                // Prefer a changed interior arrow whenever it can leave now.
                for(int i=0;i<count;i++)if(active[i]&&!locked[i]&&original.arrows[i].points.Length>1&&Ready(reverse[i],active))
                {selected=i;flipped[i]=true;break;}
                // Peel the fixed outline before consuming an unchanged interior arrow.
                if(selected<0)for(int pass=0;pass<2&&selected<0;pass++)
                    for(int i=0;i<count;i++)if(active[i]&&locked[i]==(pass==0)&&Ready(normal[i],active))
                    {selected=i;break;}
                if(selected<0)break;
                active[selected]=false;remaining--;
            }
            var result=JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(original));
            for(int i=0;i<count;i++)if(flipped[i])
            {
                Array.Reverse(result.arrows[i].points);result.arrows[i].dx=0;result.arrows[i].dy=0;report.changedArrows++;
            }
            var changed=new ArrowBoard(result);
            // Use the real gameplay solver, including Unity's diagonal interpolation.
            report.solved=changed.Solve().Length==count;
            for(int i=0;i<count;i++)if(locked[i]&&JsonUtility.ToJson(original.arrows[i])!=JsonUtility.ToJson(result.arrows[i]))
                throw new Exception("Outline changed at level "+number+", arrow "+i);
            if(report.changedArrows>0)result.id=original.id+"-interior-v1";
            return result;
        }

        [MenuItem("Draw Arrow/Generate main interior variants")]
        public static void Generate()
        {
            var repository=new LevelRepository();int count=repository.Count(GameMode.Classic);
            if(repository.Count(Group)>0)throw new Exception("Restore Recovery/MainInteriors/catalog-before.json before regenerating variants");
            var directory=Path.GetFullPath("Verification/MainInteriorCandidate");Directory.CreateDirectory(directory);
            var report=new Report{total=count,outputDirectory=directory};var rows=new List<RowReport>();
            var pack=new List<LevelData>();
            for(int n=0;n<count;n++)
            {
                var level=Variant(repository.Load(GameMode.Classic,n),n+1,out var row);
                rows.Add(row);pack.Add(level);
                if(row.changedArrows>0){report.changedLevels++;report.changedArrows+=row.changedArrows;}
                else if(row.interiorArrows==0)report.noInteriorLevels++;else report.unchangedInteriorLevels++;
                if(!row.solved)report.unsolvedLevels++;
                if(pack.Count==128||n==count-1)
                {
                    string json=JsonUtility.ToJson(new LevelPack{levels=pack.ToArray()});
                    using(var output=File.Create(Path.Combine(directory,Group+"_"+(n/128).ToString("000")+".bytes")))
                    using(var gzip=new GZipStream(output,System.IO.Compression.CompressionLevel.Optimal))
                    {var bytes=Encoding.UTF8.GetBytes(json);gzip.Write(bytes,0,bytes.Length);}
                    pack.Clear();
                }
                if((n+1)%500==0)Debug.Log("MAIN_INTERIORS_PROGRESS "+(n+1)+"/"+count+" changed="+report.changedLevels+" unsolved="+report.unsolvedLevels);
            }
            report.levels=rows.ToArray();File.WriteAllText("Verification/main-interior-variants.json",JsonUtility.ToJson(report,true));
            Debug.Log("MAIN_INTERIORS_CANDIDATE_COMPLETE changed="+report.changedLevels+" unsolved="+report.unsolvedLevels+" noInterior="+report.noInteriorLevels+" unchangedInterior="+report.unchangedInteriorLevels);
        }
    }
}
