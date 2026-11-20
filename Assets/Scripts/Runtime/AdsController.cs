using System;
using System.Collections;
using System.Collections.Concurrent;
using Firebase.RemoteConfig;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class AdsController : MonoBehaviour
    {
        [Serializable] sealed class RewardSettings
        {
            public bool useTestAds = true;
            public string androidRewardedAdUnitId = "", iosRewardedAdUnitId = "";
        }

        readonly AdsConfig config = new AdsConfig();
        readonly ConcurrentQueue<Action> callbacks = new ConcurrentQueue<Action>();
        const string OpenCountKey = "ads_app_open_count";
        const string LastOpenKey = "ads_last_app_open_utc";
        int openCount, level, adRequest;
        bool sdkReady, sdkInitializing, consentOpen, startup, foreground = true, resumePending;
        bool configReady, fullScreen, presenting, adShown;
        volatile bool disposed;
        bool gameplay, popup, collapseWanted;
        bool remoteReadActive, remoteFetchAttempted;
        float sessionStart, gameplayStart = -1, nextSdkTry, lastInter, lastBreak, lastCollapse;
        DateTime lastOpen;
        Action releaseFullScreen;
        string fallbackRewardUnit;
#if !UNITY_EDITOR
        RewardedAd cachedReward;
        string cachedRewardUnit;
        bool rewardLoading;
        float retryRewardAt;
#endif
        bool Online => Application.internetReachability != NetworkReachability.NotReachable;
        bool Automatic => Online && !NoAds;
        static bool AdsRuntime => Application.isEditor || Application.isMobilePlatform;
        static float Now => Time.realtimeSinceStartup;
        public bool NoAds { get; private set; }
        public bool Blocking => startup || fullScreen || consentOpen;
        public string LoadingMessage => startup && !configReady ? "Loading Firebase config…" : "Loading…";
        public bool OverlayOpen => presenting || consentOpen;
        // A purchase integration can supply the verified entitlement through this method.
        public void SetNoAds(bool owned)
        {
            NoAds = owned;
            PlayerPrefs.SetInt("ads_no_ads_owned", owned ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Begin(int currentLevel, bool alreadyPlaying)
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            sessionStart = lastCollapse = Now;
            level = currentLevel;
            NoAds = PlayerPrefs.GetInt("ads_no_ads_owned") == 1;
            openCount = Mathf.Clamp(PlayerPrefs.GetInt(OpenCountKey), 0, int.MaxValue - 1) + 1;
            PlayerPrefs.SetInt(OpenCountKey, openCount);
            if (long.TryParse(PlayerPrefs.GetString(LastOpenKey), out long ticks) &&
                ticks > 0 && ticks <= DateTime.UtcNow.Ticks)
                lastOpen = new DateTime(ticks, DateTimeKind.Utc);
            PlayerPrefs.Save();
            config.Changed += ResetPlacements;
            StartCoroutine(ReadRemote());
            if (Online) StartCoroutine(OpenGame(alreadyPlaying));
#else
            if (alreadyPlaying) gameplayStart = Now;
#endif
        }

        IEnumerator ReadRemote()
        {
            remoteReadActive = true;
            while (!FirebaseConnection.Finished && Now - sessionStart < 15) yield return null;
            if (!FirebaseConnection.Available)
            {
                configReady = true;
                remoteReadActive = false;
                yield break;
            }

            var remote = FirebaseRemoteConfig.DefaultInstance;
            var defaults = remote.SetDefaultsAsync(config.Defaults);
            yield return new WaitUntil(() => defaults.IsCompleted);
            ApplyRemote(remote);
            if (Online)
            {
                remoteFetchAttempted = true;
                var fetch = remote.FetchAsync(Application.isEditor ? TimeSpan.Zero : TimeSpan.FromHours(1));
                yield return new WaitUntil(() => fetch.IsCompleted);
                if (fetch.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                {
                    var activate = remote.ActivateAsync();
                    yield return new WaitUntil(() => activate.IsCompleted);
                    if (activate.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        ApplyRemote(remote);
                        if (Application.isEditor)
                            Debug.Log($"Firebase Remote Config ready. ads_banner_show={config.Flag("banner_show")}, " +
                                $"banner ID configured={config.Unit("banner").Length > 0}, source={remote.GetValue("ads_banner_show").Source}.");
                    }
                    else Debug.LogWarning($"Firebase Remote Config activation failed: {activate.Exception?.GetBaseException().Message}");
                }
                else Debug.LogWarning($"Firebase Remote Config fetch failed: {fetch.Exception?.GetBaseException().Message}");
            }
            configReady = true;
            remoteReadActive = false;
        }

        void ApplyRemote(FirebaseRemoteConfig remote)
        {
            config.Apply((key, fallback) =>
            {
                var value = remote.GetValue(key);
                if (value.Source != ValueSource.RemoteValue) return fallback;
                if (fallback is int number)
                    return int.TryParse(value.StringValue, out int result) ? result : number;
                if (fallback is bool flag)
                    return bool.TryParse(value.StringValue, out bool result) ? result : flag;
                return value.StringValue;
            });
        }

        IEnumerator OpenGame(bool alreadyPlaying)
        {
            startup = true;
            float deadline = Now + 8;
            while (Online && !configReady && Now < deadline) yield return null;

            if (CanOpen(false))
            {
                RunFullScreen(config.Unit("aoa"), Format.Open, 8, () => CanOpen(false), null);
                while (fullScreen) yield return null;
            }
            else if (Automatic && StartupCountEligible && HasPanel())
            {
                deadline = Now + 4;
                while (Automatic && !PanelVisible && Now < deadline) yield return null;
                if (PanelVisible) yield return new WaitForSecondsRealtime(2);
            }
            startup = false;
            if (alreadyPlaying) MarkGameplayStarted();
        }

        bool StartupCountEligible => openCount >= Mathf.Max(1, config.Count("aoa_open_count"));

        bool CanOpen(bool resume)
        {
            return Automatic && config.Flag(resume ? "aoa_resume_game_show" : "aoa_show") &&
                config.Count("aoa_type") == 1 && config.Unit("aoa").Length > 0 &&
                StartupCountEligible &&
                level >= Mathf.Max(1, config.Count("aoa_start_level")) &&
                (DateTime.UtcNow - lastOpen).TotalSeconds >= Mathf.Max(0, config.Count("aoa_duration"));
        }

        void OnApplicationPause(bool isPaused)
        {
            foreground = !isPaused;
            if (isPaused) resumePending = !Blocking && Time.frameCount > 1;
        }

        void LateUpdate()
        {
            while (callbacks.TryDequeue(out var callback)) callback();
            if (!foreground) return;
            if (AdsRuntime && Online && FirebaseConnection.Available &&
                !remoteReadActive && !remoteFetchAttempted)
                StartCoroutine(ReadRemote());
            if (resumePending)
            {
                resumePending = false;
                if (!Blocking && CanOpen(true))
                    RunFullScreen(config.Unit("aoa"), Format.Open, 5, () => CanOpen(true), null);
            }
            UpdatePlacements();
#if !UNITY_EDITOR
            if (Application.isMobilePlatform && sdkReady && Online && !Blocking &&
                config.Count("reward_type") == 1 && Now >= retryRewardAt)
                PreloadReward(RewardUnit());
#endif
            int interval = config.Count("inter_ad_break_time");
            if (AdsRuntime && gameplay && interval > 0 && Now - lastBreak >= interval &&
                config.Unit("inter_ad_break").Length > 0 && CanInter(level))
            {
                lastBreak = Now;
                RunFullScreen(config.Unit("inter_ad_break"), Format.Interstitial, 6,
                    () => gameplay && Automatic, null);
            }
        }

        public void Tick(int currentLevel, bool playing, bool modal)
        {
            level = currentLevel;
            gameplay = playing;
            if (playing && !startup) MarkGameplayStarted();
            if (popup != modal) collapseWanted = true;
            popup = modal;
        }

        void MarkGameplayStarted()
        {
            if (gameplayStart >= 0) return;
            gameplayStart = lastBreak = Now;
        }

        public void LevelChanged(int currentLevel)
        {
            level = currentLevel;
            collapseWanted = true;
        }

        bool CanInter(int currentLevel)
        {
            return Automatic && foreground && !Blocking && config.Count("inter_type") == 1 &&
                currentLevel >= Mathf.Max(1, config.Count("inter_start_level")) && gameplayStart >= 0 &&
                Now - gameplayStart >= Mathf.Max(0, config.Count("inter_first_show")) &&
                (!adShown || Now - lastInter >= Mathf.Max(0, config.Count("inter_duration")));
        }

        public bool TryLevelEnd(int completedLevel, Action advance)
        {
            int start = Mathf.Max(1, config.Count("inter_start_level"));
            return config.Flag("inter_shown_on_level_end") && CanInter(completedLevel) &&
                (completedLevel - start) % Mathf.Max(1, config.Count("inter_level_range")) == 0 &&
                RunFullScreen(config.Unit("inter"), Format.Interstitial, 6, () => Automatic, _ => advance());
        }

        public void Show(Action<bool> callback)
        {
            if (Blocking || !foreground || !Online || config.Count("reward_type") != 1 ||
                !RunFullScreen(RewardUnit(), Format.Rewarded, 10, () => Online, callback))
                callback(false);
        }

        string RewardUnit()
        {
            string remoteUnit = config.Unit("reward");
            if (remoteUnit.Length > 0) return remoteUnit;
            if (fallbackRewardUnit != null) return fallbackRewardUnit;
            var asset = Resources.Load<TextAsset>("AdMobHintSettings");
            var settings = asset ? JsonUtility.FromJson<RewardSettings>(asset.text) : new RewardSettings();
#if UNITY_IOS
            return fallbackRewardUnit = settings.useTestAds ? "ca-app-pub-3940256099942544/1712485313" : settings.iosRewardedAdUnitId;
#else
            return fallbackRewardUnit = settings.useTestAds ? "ca-app-pub-3940256099942544/5224354917" : settings.androidRewardedAdUnitId;
#endif
        }

        void Dispatch(Action callback, Action releaseIfDestroyed = null)
        {
            lock (callbacks)
            {
                if (!disposed)
                {
                    callbacks.Enqueue(callback);
                    return;
                }
            }
            releaseIfDestroyed?.Invoke();
        }

        void EnsureSdk()
        {
            if (Application.isEditor) { sdkReady = true; return; }
            if (!Online || sdkReady || sdkInitializing || Now < nextSdkTry) return;
            sdkInitializing = true;
            ConsentInformation.Update(new ConsentRequestParameters(), error => Dispatch(() =>
            {
                if (disposed) return;
                if (error != null && !ConsentInformation.CanRequestAds()) { SdkFailed(); return; }
                consentOpen = error == null;
                if (!consentOpen) { InitializeSdk(); return; }
                ConsentForm.LoadAndShowConsentFormIfRequired(_ => Dispatch(() =>
                {
                    if (disposed) return;
                    consentOpen = false;
                    if (ConsentInformation.CanRequestAds()) InitializeSdk();
                    else SdkFailed();
                }));
            }));
        }

        void InitializeSdk()
        {
            MobileAds.Initialize(status => Dispatch(() =>
            {
                if (disposed) return;
                if (status == null) SdkFailed();
                else { sdkReady = true; sdkInitializing = false; }
            }));
        }

        void SdkFailed()
        {
            sdkInitializing = false;
            nextSdkTry = Now + 30;
        }

        enum Format { Open, Interstitial, Rewarded }

        bool RunFullScreen(string id, Format format, float wait, Func<bool> eligible, Action<bool> done)
        {
            if (string.IsNullOrWhiteSpace(id) || fullScreen) return false;
#if UNITY_EDITOR
            PresentEditorFullScreen(format, EditorFormatTitle(format), done, true);
#else
            fullScreen = true;
            HidePlacements();
            StartCoroutine(LoadFullScreen(id, format, wait, eligible, done));
#endif
            return true;
        }

        void RestartAdCooldowns()
        {
            lastOpen = DateTime.UtcNow;
            lastInter = lastBreak = lastCollapse = Now;
            adShown = true;
            PlayerPrefs.SetString(LastOpenKey, lastOpen.Ticks.ToString());
            PlayerPrefs.Save();
        }

#if !UNITY_EDITOR
        void PreloadReward(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || rewardLoading) return;
            if (cachedReward != null && cachedRewardUnit == id && cachedReward.CanShowAd()) return;
            cachedReward?.Destroy();
            cachedReward = null;
            rewardLoading = true;
            RewardedAd.Load(id, new AdRequest(), (loaded, error) => Dispatch(() =>
            {
                if (disposed) { loaded?.Destroy(); return; }
                rewardLoading = false;
                if (error != null || loaded == null || !loaded.CanShowAd())
                {
                    loaded?.Destroy();
                    retryRewardAt = Now + 15;
                    Debug.LogWarning($"Rewarded ad load failed: {error}");
                    return;
                }
                cachedRewardUnit = id;
                cachedReward = loaded;
            }, () => loaded?.Destroy()));
        }

        IEnumerator LoadFullScreen(string id, Format format, float wait, Func<bool> eligible, Action<bool> done)
        {
            int request = ++adRequest;
            bool finished = false, earned = false;
            float deadline = Now + wait;
            Action shown = () =>
            {
                if (request == adRequest) RestartAdCooldowns();
            };
            Action close = () => { if (request == adRequest) finished = true; };
            Action<Action, Action> accept = (show, destroy) =>
            {
                if (disposed || request != adRequest) { destroy?.Invoke(); return; }
                releaseFullScreen = destroy;
                if (show == null || Now >= deadline || !foreground || !eligible()) { finished = true; return; }
                presenting = true;
                HidePlacements();
                show();
            };
            EnsureSdk();
            while (!sdkReady && (Now < deadline || consentOpen) && eligible())
            {
                if (consentOpen) deadline = Now + wait;
                yield return null;
            }
            if (sdkReady && foreground && eligible())
            {
                var ad = new AdRequest();
                switch (format)
                {
                    case Format.Open:
                        AppOpenAd.Load(id, ad, (loaded, error) => Dispatch(() =>
                        {
                            bool ready = error == null && loaded != null && loaded.CanShowAd();
                            if (ready)
                            {
                                loaded.OnAdFullScreenContentOpened += () => Dispatch(shown);
                                loaded.OnAdFullScreenContentClosed += () => Dispatch(close);
                                loaded.OnAdFullScreenContentFailed += _ => Dispatch(close);
                            }
                            accept(ready ? loaded.Show : null, loaded == null ? null : loaded.Destroy);
                        }, () => loaded?.Destroy()));
                        break;
                    case Format.Interstitial:
                        InterstitialAd.Load(id, ad, (loaded, error) => Dispatch(() =>
                        {
                            bool ready = error == null && loaded != null && loaded.CanShowAd();
                            if (ready)
                            {
                                loaded.OnAdFullScreenContentOpened += () => Dispatch(shown);
                                loaded.OnAdFullScreenContentClosed += () => Dispatch(close);
                                loaded.OnAdFullScreenContentFailed += _ => Dispatch(close);
                            }
                            accept(ready ? loaded.Show : null, loaded == null ? null : loaded.Destroy);
                        }, () => loaded?.Destroy()));
                        break;
                    case Format.Rewarded:
                        PreloadReward(id);
                        while (rewardLoading && Now < deadline && foreground && eligible())
                            yield return null;
                        if (cachedReward != null && cachedRewardUnit != id && Now < deadline && foreground && eligible())
                        {
                            PreloadReward(id);
                            while (rewardLoading && Now < deadline && foreground && eligible())
                                yield return null;
                        }
                        if (cachedReward == null || cachedRewardUnit != id || !cachedReward.CanShowAd())
                        {
                            finished = true;
                            break;
                        }
                        var rewardAd = cachedReward;
                        cachedReward = null;
                        rewardAd.OnAdFullScreenContentOpened += () => Dispatch(shown);
                        rewardAd.OnAdFullScreenContentClosed += () => Dispatch(close);
                        rewardAd.OnAdFullScreenContentFailed += _ => Dispatch(close);
                        accept(() => rewardAd.Show(_ => Dispatch(() => earned = true)), rewardAd.Destroy);
                        break;
                }
                while (!finished && (presenting || (Now < deadline && eligible()))) yield return null;
            }
            adRequest++;
            releaseFullScreen?.Invoke();
            releaseFullScreen = null;
            presenting = fullScreen = false;
            done?.Invoke(finished && earned);
        }
#endif

        void OnDestroy()
        {
            lock (callbacks) disposed = true;
            adRequest++;
            releaseFullScreen?.Invoke();
            ResetPlacements();
#if !UNITY_EDITOR
            cachedReward?.Destroy();
#endif
            while (callbacks.TryDequeue(out var callback)) callback();
        }
    }
}
