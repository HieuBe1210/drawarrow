using GoogleMobileAds.Api;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class AdsController
    {
        BannerView footer, rectangle;
        NativeOverlayAd native;
        bool footerLoaded, footerVisible, rectangleLoaded, nativeRequested, rectangleFailed, footerIsCollapse;
        float footerRetry;
        float footerHeight;
        int panelGeneration;
        public bool PanelVisible => previewPanelHeight > 0 || rectangleLoaded || native != null;
        public float TopInset => previewPanelHeight > 0 ? previewPanelHeight :
            rectangleLoaded ? rectangle.GetHeightInPixels() :
            native != null ? native.GetTemplateHeightInPixels() + 12 : 0;
        public float BottomInset => footerVisible ? footer == null ? PreviewFooterHeight : footerHeight : 0;
        float previewPanelHeight;
        bool previewMrec;
        float PreviewFooterHeight => Screen.height * .075f;
        string ConfiguredBannerId => config.Flag("banner_show") && config.Count("banner_type") == 1
            ? config.Unit("banner") : "";

#if UNITY_EDITOR
        const string PreviewBannerMenu = "Draw Arrow/Ads/Preview Banner When Disabled";
        static string PreviewBannerPreference => Application.dataPath + "/AdsPreviewBanner";

        [UnityEditor.MenuItem(PreviewBannerMenu)]
        static void ToggleEditorBannerPreview()
        {
            UnityEditor.EditorPrefs.SetBool(PreviewBannerPreference, !EditorBannerPreview);
        }

        [UnityEditor.MenuItem(PreviewBannerMenu, true)]
        static bool ValidateEditorBannerPreview()
        {
            UnityEditor.Menu.SetChecked(PreviewBannerMenu, EditorBannerPreview);
            return true;
        }

        static bool EditorBannerPreview => UnityEditor.EditorPrefs.GetBool(PreviewBannerPreference, true);
#endif

        public void DrawEditorAds()
        {
#if UNITY_EDITOR
            DrawEditorAdPreviews();
#endif
        }

        public void DrawNativeFrame()
        {
            if (native == null || TopInset <= 12) return;
            var safe = Screen.safeArea;
            float width = native.GetTemplateWidthInPixels() + 20;
            Ink.Card(new Rect(safe.center.x - width * .5f, Screen.height - safe.yMax,
                width, TopInset), new Color32(247, 246, 240, 255), 14);
        }

        bool HasPanel() => (config.Flag("mrec_show") && config.Text("mrec_id").Trim().Length > 0) ||
            (config.Flag("native_show") && config.Text("native_id").Trim().Length > 0);

        void UpdatePlacements()
        {
            bool collapseTrigger = collapseWanted;
            collapseWanted = false;
            if (!AdsRuntime) return;
            if (NoAds || StoreOpen || presenting || (fullScreen && !startup) || consentOpen)
            {
                HidePlacements();
                return;
            }
#if UNITY_EDITOR
            if (!startup && UpdateEditorPlacementPreview()) return;
#endif
            if (!Automatic)
            {
                HidePlacements();
                return;
            }
            if ((popup || (startup && configReady && StartupCountEligible)) && HasPanel())
            {
                EnsureSdk();
                if (sdkReady) UpdatePanel();
            }
            else if (PanelVisible || rectangle != null || nativeRequested || rectangleFailed)
                DestroyPanel();

            if (startup || popup)
            {
                if (footerIsCollapse) DestroyFooter();
                if (startup)
                {
                    SetFooterVisible(false);
                    return;
                }
            }

            if (!popup && collapseTrigger && config.Flag("collapse_show") && gameplayStart >= 0 &&
                Now - Mathf.Max(gameplayStart, lastCollapse) >= Mathf.Max(0, config.Count("collapse_duration")) &&
                level >= Mathf.Max(1, config.Count("collapse_start_level")) &&
                config.Text("collapse_id").Trim().Length > 0)
            {
                DestroyFooter();
                footerRetry = 0;
                footerIsCollapse = true;
            }

#if UNITY_EDITOR
            if (!footerIsCollapse && EditorBannerPreview)
            {
                ShowEditorFooter();
                return;
            }
#endif
            string id = footerIsCollapse ? config.Text("collapse_id").Trim() : ConfiguredBannerId;
            if (id.Length == 0) { DestroyFooter(); return; }
            EnsureSdk();
            if (!sdkReady || Now < footerRetry) return;
            if (footer == null && !footerLoaded) LoadFooter(id);
            else if (footerLoaded) SetFooterVisible(true);
        }

        void LoadFooter(string id)
        {
#if UNITY_EDITOR
            ShowEditorFooter();
#else
            int width = MobileAds.Utils.GetDeviceSafeWidth();
            if (width <= 0 || (!footerIsCollapse && config.Count("banner_admob_type") == 1 && width < 320))
                return;
            var size = footerIsCollapse || config.Count("banner_admob_type") != 1
                ? AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(width) : AdSize.LargeBanner;
            var view = footer = new BannerView(id, size, AdPosition.Bottom);
            view.Hide();
            view.OnAdImpressionRecorded += () => Dispatch(() =>
            {
                if (footer != view) return;
                if (footerHeight <= 0) footerHeight = view.GetHeightInPixels();
                RestartAdCooldowns();
            });
            view.OnBannerAdLoaded += () => Dispatch(() =>
            {
                if (footer != view) return;
                footerLoaded = true;
                footerHeight = view.GetHeightInPixels();
                SetFooterVisible(foreground && Automatic && !Blocking && (!footerIsCollapse || !popup));
            });
            view.OnBannerAdLoadFailed += _ => Dispatch(() =>
            {
                if (footer != view) return;
                bool collapse = footerIsCollapse;
                DestroyFooter();
                footerRetry = collapse ? Now : Now + 30;
            });
            var request = new AdRequest();
            if (footerIsCollapse) request.Extras.Add("collapsible", "bottom");
            view.LoadAd(request);
#endif
        }

#if UNITY_EDITOR
        void ShowEditorFooter()
        {
            footerLoaded = true;
            SetFooterVisible(true);
        }
#endif

        void SetFooterVisible(bool visible)
        {
            if (!footerLoaded || footerVisible == visible) return;
            footerVisible = visible;
            if (visible)
            {
                footer?.Show();
#if UNITY_EDITOR
                if (footerIsCollapse && editorPlacement == EditorAdPreview.Automatic) RestartAdCooldowns();
#endif
            }
            else footer?.Hide();
        }

        void UpdatePanel()
        {
            if (Application.isEditor)
            {
                SetPreviewPanel(config.Flag("mrec_show") && config.Text("mrec_id").Trim().Length > 0);
                return;
            }
            if (rectangle != null || nativeRequested) return;
            int safeWidth = MobileAds.Utils.GetDeviceSafeWidth();
            if (safeWidth <= 0) return;
            float safeHeightDp = Screen.safeArea.height * safeWidth / Mathf.Max(1, Screen.safeArea.width);
            string id = config.Text("mrec_id").Trim();
            if (!rectangleFailed && config.Flag("mrec_show") && id.Length > 0 &&
                safeWidth >= 300 && safeHeightDp >= 650)
            {
                var view = rectangle = new BannerView(id, AdSize.MediumRectangle, AdPosition.Top);
                view.Hide();
                view.OnAdImpressionRecorded += () => Dispatch(() =>
                {
                    if (rectangle == view) RestartAdCooldowns();
                });
                view.OnBannerAdLoaded += () => Dispatch(() =>
                {
                    if (rectangle != view) return;
                    rectangleLoaded = true;
                    if (CanShowPanel()) view.Show();
                    else view.Hide();
                });
                view.OnBannerAdLoadFailed += _ => Dispatch(() =>
                {
                    if (rectangle != view) return;
                    rectangleFailed = true;
                    rectangleLoaded = false;
                    rectangle = null;
                    view.Destroy();
                });
                view.LoadAd(new AdRequest());
                return;
            }

            id = config.Text("native_id").Trim();
            if (!config.Flag("native_show") || id.Length == 0 || safeWidth < 320 || safeHeightDp < 400) return;
            nativeRequested = true;
            int request = panelGeneration;
            NativeOverlayAd.Load(id, new AdRequest(), new NativeAdOptions(), (loaded, error) => Dispatch(() =>
            {
                if (request != panelGeneration || error != null || loaded == null || !CanShowPanel())
                {
                    loaded?.Destroy();
                    return;
                }

                native = loaded;
                loaded.OnAdImpressionRecorded += () => Dispatch(() =>
                {
                    if (native == loaded) RestartAdCooldowns();
                });
                loaded.RenderTemplate(new NativeTemplateStyle
                {
                    TemplateId = NativeTemplateId.Small,
                    MainBackgroundColor = Color.clear,
                    CallToActionText = new NativeTemplateTextStyle
                    {
                        BackgroundColor = new Color32(40, 76, 66, 255),
                        TextColor = Color.white,
                        Style = NativeTemplateFontStyle.Bold
                    }
                }, new AdSize(safeWidth - 32, 120), AdPosition.Top);
                loaded.Show();
            }, () => loaded?.Destroy()));
        }

        void SetPreviewPanel(bool mrec)
        {
            previewMrec = mrec;
            previewPanelHeight = Screen.height * (mrec ? .29f : .14f);
        }

        bool CanShowPanel() => foreground && Automatic && (popup || startup) && !presenting && !consentOpen;

        void HidePlacements()
        {
            if (PanelVisible || rectangle != null || nativeRequested || rectangleFailed) DestroyPanel();
            if (footerIsCollapse) DestroyFooter();
            else SetFooterVisible(false);
        }

        void DestroyPanel()
        {
            panelGeneration++;
            previewPanelHeight = 0;
            rectangle?.Destroy();
            native?.Destroy();
            rectangle = null;
            native = null;
            rectangleLoaded = rectangleFailed = nativeRequested = false;
        }

        void DestroyFooter()
        {
            footer?.Destroy();
            footer = null;
            footerHeight = 0;
            footerLoaded = footerVisible = footerIsCollapse = false;
        }

        void ResetPlacements()
        {
            DestroyFooter();
            DestroyPanel();
            footerRetry = 0;
        }
    }
}
