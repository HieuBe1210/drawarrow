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
            if(repository.Count(GameMode.Classic)!=1574||repository.Count("escape107")!=1574)
                throw new Exception("Expected all 1574 Escape levels in the main sequence");
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
            if(repository.Load(GameMode.Classic,1574).id!="escape107-1"||
               repository.Load(GameMode.Classic,-1).id!="escape107-1574")
                throw new Exception("Main sequence wrapping failed");
            foreach(GameMode mode in Enum.GetValues(typeof(GameMode)))
                if(mode!=GameMode.Classic&&mode!=GameMode.Schulte&&repository.Load(mode,0).id.StartsWith("escape107-"))
                    throw new Exception("Unexpected replacement in "+mode);
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/escape107-unity.json","{\"passed\":true,\"boardsSolved\":1574,\"arrows\":"+arrows+",\"oldSharedLevels\":22543,\"packBoundaries\":true,\"undo\":true,\"sequenceWrap\":true}");
            Debug.Log("ESCAPE107_VALIDATION_PASSED boards=1574 arrows="+arrows);
        }
    }
}
