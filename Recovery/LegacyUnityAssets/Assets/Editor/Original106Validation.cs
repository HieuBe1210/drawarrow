using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class Original106Validation
    {
        [MenuItem("Draw Arrow/Validate main level catalog")]
        public static void Run()
        {
            var repository=new LevelRepository();int arrows=0;
            if(repository.Count("escape107")>0){Escape107Validation.Run();return;}
            if(repository.Count("original106")==0)
            {
                int restored=repository.Count(GameMode.Classic);
                if(restored!=22543)throw new Exception("Original main-level count changed");
                for(int i=0;i<restored;i++)
                    if(JsonUtility.ToJson(repository.Load(GameMode.Classic,i))!=JsonUtility.ToJson(repository.Load("classic",i)))
                        throw new Exception("Original main level differs at "+(i+1));
                Debug.Log("ORIGINAL_LEVELS_RESTORED_VALIDATION_PASSED count="+restored);return;
            }
            int count=repository.Count("classic");
            if(repository.Count(GameMode.Classic)!=count||count!=22543)throw new Exception("Original main-level count changed");
            for(int n=0;n<51;n++)
            {
                int sourceNumber=n==0?1:2+(n-1)*4;
                var level=repository.Load(GameMode.Classic,sourceNumber-1);
                if(level.id!="original106-"+sourceNumber)throw new Exception("Wrong source order at "+n);
                var board=new ArrowBoard(level);var solution=board.Solve();
                if(solution.Length!=level.arrows.Length)throw new Exception("Unsolvable imported board "+level.id);
                foreach(int i in solution)
                {
                    var arrow=level.arrows[i];var direction=board.Direction(i);
                    if(direction!=new Vector3(arrow.dx,arrow.dy,0))throw new Exception("Original direction lost "+level.id);
                    if(!board.Remove(i))throw new Exception("Illegal removal "+level.id);
                    arrows++;
                }
                if(board.Remaining!=0)throw new Exception("Incomplete board "+level.id);
                board.Restore(solution[0]);
                if(board.Remaining!=1||!board.CanExit(solution[0]))throw new Exception("Undo failed "+level.id);
            }
            int unchanged=0;
            for(int index=0;index<count;index++)
            {
                int number=index+1;
                if(number==1||(number>=2&&number<=198&&(number-2)%4==0))continue;
                if(JsonUtility.ToJson(repository.Load(GameMode.Classic,index))!=JsonUtility.ToJson(repository.Load("classic",index)))
                    throw new Exception("Unrelated main level changed: "+number);
                unchanged++;
            }
            if(repository.Load(GameMode.Classic,51).id!=repository.Load("classic",51).id)throw new Exception("Level 52 must remain original");
            if(repository.Load(GameMode.Classic,count).id!="original106-1")throw new Exception("Main sequence wrapping failed");
            if(repository.Load("classic",0).id.StartsWith("original106-"))throw new Exception("Existing pack changed");
            foreach(GameMode mode in Enum.GetValues(typeof(GameMode)))
                if(mode!=GameMode.Schulte&&mode!=GameMode.Classic&&repository.Load(mode,0).id.StartsWith("original106-"))
                    throw new Exception("Unexpected mode replacement: "+mode);
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/original106-unity.json","{\"passed\":true,\"boardsSolved\":51,\"arrowsRemoved\":"+arrows+",\"unchangedMainLevels\":"+unchanged+",\"mainLevelCount\":"+count+",\"undo\":true,\"sequenceWrap\":true}");
            Debug.Log("ORIGINAL106_VALIDATION_PASSED boards=51 arrows="+arrows+" unchanged="+unchanged);
        }
    }
}
