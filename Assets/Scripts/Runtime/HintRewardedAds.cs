using System;
using System.Collections.Concurrent;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace DrawArrow
{
    // SDK callbacks are marshalled onto Unity's thread before touching game state.
    public sealed class HintRewardedAds : MonoBehaviour
    {
        [Serializable] sealed class Settings
        {
            public bool useTestAds=true;
            public string androidRewardedAdUnitId="",iosRewardedAdUnitId="";
        }
        readonly ConcurrentQueue<Action> events=new ConcurrentQueue<Action>();
        RewardedAd ad;Action<bool> completion;bool initialized,earned,showing;
        volatile bool disposed;float deadline;int generation;
        void Update()
        {
            while(events.TryDequeue(out var action))action();
            if(completion!=null&&!showing&&Time.realtimeSinceStartup>deadline)Finish(false);
        }
        void Dispatch(Action action){if(!disposed)events.Enqueue(action);}
        public void Show(Action<bool> callback)
        {
            if(completion!=null){callback(false);return;}
            int request=++generation;
            completion=callback;earned=false;deadline=Time.realtimeSinceStartup+30;
            if(initialized){Load();return;}
            ConsentInformation.Update(new ConsentRequestParameters(),error=>Dispatch(()=>
            {
                if(completion==null||request!=generation)return;
                if(error!=null&&!ConsentInformation.CanRequestAds()){Finish(false);return;}
                showing=true; // A consent dialog must not time out while the user reads it.
                ConsentForm.LoadAndShowConsentFormIfRequired(formError=>Dispatch(()=>
                {
                    if(completion==null||request!=generation)return;
                    showing=false;deadline=Time.realtimeSinceStartup+30;
                    if(!ConsentInformation.CanRequestAds()){Finish(false);return;}
                    MobileAds.Initialize(status=>Dispatch(()=>
                    {initialized=true;if(completion!=null&&request==generation)Load();}));
                }));
            }));
        }
        void Load()
        {
            int request=generation;
            var text=Resources.Load<TextAsset>("AdMobHintSettings");
            var settings=text?JsonUtility.FromJson<Settings>(text.text):new Settings();
#if UNITY_IOS
            string id=settings.useTestAds?"ca-app-pub-3940256099942544/4411468910":settings.iosRewardedAdUnitId;
#else
            string id=settings.useTestAds?"ca-app-pub-3940256099942544/1033173712":settings.androidRewardedAdUnitId;
#endif
            if(string.IsNullOrWhiteSpace(id)){Finish(false);return;}
            RewardedAd.Load(id,new AdRequest(),(loaded,error)=>
            {
                if(disposed){loaded?.Destroy();return;}
                Dispatch(()=>
                {
                    if(completion==null||request!=generation){loaded?.Destroy();return;}
                    if(error!=null||loaded==null||!loaded.CanShowAd()){loaded?.Destroy();Finish(false);return;}
                    ad=loaded;showing=true;
                    ad.OnAdFullScreenContentClosed+=()=>Dispatch(()=>{if(request==generation)Finish(earned);});
                    ad.OnAdFullScreenContentFailed+=failure=>Dispatch(()=>{if(request==generation)Finish(false);});
                    ad.Show(reward=>Dispatch(()=>{if(request==generation)earned=true;}));
                });
            });
        }
        void Finish(bool rewarded)
        {
            var callback=completion;completion=null;showing=false;generation++;
            ad?.Destroy();ad=null;
            callback?.Invoke(rewarded);
        }
        void OnDestroy(){disposed=true;Finish(false);}
    }
}
