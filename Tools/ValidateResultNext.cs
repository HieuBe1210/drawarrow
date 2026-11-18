using System;
using DrawArrow;
class ResultNextCheck {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Main(){
  var transition=new LevelResultTransition();GameMode mode;int index;
  Check(!transition.Request()&&!transition.Take(out mode,out index),"No result cannot advance");
  foreach(GameMode game in Enum.GetValues(typeof(GameMode)))for(int level=1;level<=200;level++){
   transition.Begin(game,level-1,true);Check(transition.Request(),"First Next accepted");
   Check(!transition.Request(),"Duplicate Next ignored");Check(transition.Take(out mode,out index),"Queued Next consumed");
   Check(mode==game&&index==(game==GameMode.Daily?level-1:level),"Exactly one level advanced");
   Check(!transition.Take(out mode,out index)&&!transition.Request(),"Result cannot advance twice");
   transition.Begin(game,level-1,false);Check(transition.Request()&&transition.Take(out mode,out index)&&index==level-1,"Fail retries same level");
  }
  var progress=new ProgressData();
  for(int level=4;level<=6;level++){
   transition.Begin(GameMode.Classic,level-1,true);int bonus=HintEconomy.RewardWin(progress,GameMode.Classic,level);
   Check(transition.Request()&&transition.Take(out mode,out index)&&index==level,"Hint rewards cannot skip level "+level);
  }
  transition.Begin(GameMode.Classic,14,true);transition.Request();transition.Clear();Check(!transition.Take(out mode,out index),"Home/restart clears pending Next");
  Console.WriteLine("PASS "+checks+" result transition checks");
  System.IO.File.WriteAllText("Verification/result-next.json","{\"passed\":true,\"checks\":"+checks+",\"level4To5\":true,\"duplicateNextIgnored\":true,\"hintRewardDoesNotSkip\":true,\"playModeVerified\":false}");
 }
}
