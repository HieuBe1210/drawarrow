using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DrawArrow.Editor
{
    public static class Escape107Validation
    {
        [MenuItem("Draw Arrow/Validate Escape 1.0.7 levels")]
        public static void Run()
        {
            var repository=new LevelRepository();
            if(repository.Count("maininteriors")>0){MainInteriorVariants.ValidateActiveVariants();return;}
            if(repository.Count(GameMode.Classic)!=22543||repository.Count("escape107")!=1574)
                throw new Exception("Expected 1574 Escape levels followed by original levels 1575 through 22543");
            if(repository.Count("classic")!=22543)
                throw new Exception("The old shared levels must remain available to other modes");
            int arrows=0;
            for(int i=0;i<1574;i++)
            {
                var level=repository.Load(GameMode.Classic,i);
                if(level.id!="escape107-"+(i+1))throw new Exception("Wrong main level order at "+(i+1));
                var board=new ArrowBoard(level);
                var solved=board.Solve();
                if(solved.Length!=level.arrows.Length)throw new Exception("Unsolvable main level "+level.id);
                arrows+=solved.Length;
                if(i==0||i==127||i==128||i==1573)
                {
                    foreach(int arrow in solved)
                        if(!board.Remove(arrow))throw new Exception("Invalid move in "+level.id);
                    if(board.Remaining!=0)throw new Exception("Board did not clear: "+level.id);
                    board.Restore(solved[0]);
                    if(board.Remaining!=1||!board.CanExit(solved[0]))throw new Exception("Undo failed: "+level.id);
                }
            }
            int preserved=0;
            for(int i=1574;i<22543;i++)
            {
                if(JsonUtility.ToJson(repository.Load(GameMode.Classic,i))!=JsonUtility.ToJson(repository.Load("classic",i)))
                    throw new Exception("Original level changed at "+(i+1));
                preserved++;
            }
            int oldBoundarySolved=0;
            var oldBoundaryUnsolved=new System.Collections.Generic.List<int>();
            foreach(int index in new[]{1574,1663,1664,22542})
            {
                var board=new ArrowBoard(repository.Load(GameMode.Classic,index));
                if(board.Solve().Length==board.Level.arrows.Length)oldBoundarySolved++;
                else oldBoundaryUnsolved.Add(index+1);
            }
            if(repository.Load(GameMode.Classic,22543).id!="escape107-1"||
               repository.Load(GameMode.Classic,-1).id!=repository.Load("classic",22542).id)
                throw new Exception("Main sequence wrapping failed");
            foreach(GameMode mode in Enum.GetValues(typeof(GameMode)))
                if(mode!=GameMode.Classic&&mode!=GameMode.Schulte&&repository.Load(mode,0).id.StartsWith("escape107-"))
                    throw new Exception("Unexpected replacement in "+mode);
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/escape107-unity.json","{\"passed\":true,\"mainLevelCount\":22543,\"boardsSolved\":1574,\"arrows\":"+arrows+",\"preservedOldLevels\":"+preserved+",\"preservedFrom\":1575,\"preservedThrough\":22543,\"oldBoundaryBoardsSolved\":"+oldBoundarySolved+",\"oldBoundaryUnsolvedWithCurrentRules\":["+string.Join(",",oldBoundaryUnsolved)+"],\"packBoundaries\":true,\"undo\":true,\"sequenceWrap\":true}");
            Debug.Log("ESCAPE107_VALIDATION_PASSED boards=1574 arrows="+arrows+" preserved="+preserved);
        }
    }
}
