using System;
using DrawArrow;
class HintEconomyCheck {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Main(){
  var p=new ProgressData();int coins=p.coins;
  Check(p.freeHints==3,"Initial reserve is three");
  for(int level=0;level<4;level++)for(int use=0;use<20;use++)Check(HintEconomy.SpendFree(p,GameMode.Classic,level),"Levels one through four unlimited");
  Check(p.freeHints==3,"Tutorial preserves reserve");
  for(int use=0;use<3;use++)Check(HintEconomy.SpendFree(p,GameMode.Classic,4),"Level five uses three free hints");
  Check(p.freeHints==0&&!HintEconomy.SpendFree(p,GameMode.Classic,4),"Fourth use on level five requires ad");
  for(int level=1;level<=100;level++){
   int before=p.freeHints;int reward=HintEconomy.RewardWin(p,GameMode.Classic,level);bool milestone=level%5==0;
   Check(reward==(milestone?1:0)&&p.freeHints==before+(milestone?1:0),"Reward on each X5/X0 win only: "+level);
  }
  int remaining=p.freeHints;
  Check(HintEconomy.RewardWin(p,GameMode.Classic,5)==1&&p.freeHints==remaining+1,"Replaying level five awards one again");
  Check(HintEconomy.RewardWin(p,GameMode.Classic,0)==0,"Level zero cannot award");
  Check(HintEconomy.RewardWin(p,GameMode.ArrowClash,10)==0,"Milestones apply to main campaign");
  var old=new ProgressData{freeHints=5,hintEconomyVersion=1};HintEconomy.UpgradeSave(old);Check(old.freeHints==3&&old.hintEconomyVersion==2,"Old five reserve becomes three");
  HintEconomy.UpgradeSave(old);Check(old.freeHints==3,"Upgrade only once");
  old.freeHints=8;old.hintEconomyVersion=1;HintEconomy.UpgradeSave(old);Check(old.freeHints==6,"Earned extra hints preserved relative to initial grant");
  old.freeHints=1;old.hintEconomyVersion=1;HintEconomy.UpgradeSave(old);Check(old.freeHints==0,"Spent balance never negative");
  Check(p.coins==coins,"No coins spent");
  Console.WriteLine("PASS "+checks+" updated hint economy checks");
  System.IO.File.WriteAllText("Verification/hint-economy.json","{\"passed\":true,\"checks\":"+checks+",\"initialFreeHints\":3,\"unlimitedThroughLevel\":4,\"milestoneRewardStartsAt\":5,\"repeatWinsRewarded\":true,\"saveUpgradeVerified\":true,\"playModeVerified\":false}");
 }
}
