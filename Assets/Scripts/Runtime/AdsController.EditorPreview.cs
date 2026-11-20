#if UNITY_EDITOR
using System;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class AdsController
    {
        public enum EditorAdPreview
        {
            Automatic, Banner, Mrec, Native, Collapsible,
            AppOpenStartup, AppOpenResume, InterstitialLevelEnd, InterstitialBreak, Rewarded
        }

        EditorAdPreview editorPlacement;
        bool previewCollapseExpanded;
        Format previewFormat;
        string previewTitle;
        Action<bool> previewClose;

        public bool TryPreviewAd(EditorAdPreview ad, Action<bool> rewarded = null)
        {
            if (Blocking) return false;
            switch (ad)
            {
                case EditorAdPreview.AppOpenStartup:
                    PresentEditorFullScreen(Format.Open, "App Open · Startup", null);
                    break;
                case EditorAdPreview.AppOpenResume:
                    PresentEditorFullScreen(Format.Open, "App Open · Resume", null);
                    break;
                case EditorAdPreview.InterstitialLevelEnd:
                    PresentEditorFullScreen(Format.Interstitial, "Interstitial · Level end", null);
                    break;
                case EditorAdPreview.InterstitialBreak:
                    PresentEditorFullScreen(Format.Interstitial, "Interstitial · Timed ad break", null);
                    break;
                case EditorAdPreview.Rewarded:
                    PresentEditorFullScreen(Format.Rewarded, "Rewarded", rewarded);
                    break;
                default:
                    editorPlacement = ad;
                    previewCollapseExpanded = ad == EditorAdPreview.Collapsible;
                    ResetPlacements();
                    break;
            }
            return true;
        }

        static string EditorFormatTitle(Format format) => format == Format.Open ? "App Open" : format.ToString();

        void PresentEditorFullScreen(Format format, string title, Action<bool> done, bool recordImpression = false)
        {
            fullScreen = presenting = true;
            HidePlacements();
            previewFormat = format;
            previewTitle = title;
            if (recordImpression) RestartAdCooldowns();
            previewClose = reward =>
            {
                previewClose = null;
                Dispatch(() =>
                {
                    presenting = fullScreen = false;
                    done?.Invoke(reward);
                });
            };
        }

        bool UpdateEditorPlacementPreview()
        {
            switch (editorPlacement)
            {
                case EditorAdPreview.Mrec:
                case EditorAdPreview.Native:
                    if (footerLoaded) DestroyFooter();
                    SetPreviewPanel(editorPlacement == EditorAdPreview.Mrec);
                    return true;
                case EditorAdPreview.Banner:
                case EditorAdPreview.Collapsible:
                    if (PanelVisible) DestroyPanel();
                    footerIsCollapse = editorPlacement == EditorAdPreview.Collapsible;
                    ShowEditorFooter();
                    return true;
                default:
                    return false;
            }
        }

        void DrawEditorAdPreviews()
        {
            var safe = Screen.safeArea;
            if (previewPanelHeight > 0)
            {
                float width = safe.width * .75f;
                var bounds = new Rect(safe.center.x - width * .5f, Screen.height - safe.yMax, width, previewPanelHeight);
                if (previewMrec) DrawEditorAdCard(bounds, "MREC · Editor preview");
                else DrawEditorNativePreview(bounds);
            }
            if (footerVisible)
            {
                bool expanded = footerIsCollapse && editorPlacement == EditorAdPreview.Collapsible && previewCollapseExpanded;
                float height = expanded ? Screen.height * .24f : BottomInset;
                string label = footerIsCollapse ? "Collapsible banner · Editor preview" :
                    ConfiguredBannerId.Length > 0 ? "AdMob banner · Editor preview" : "AdMob banner · Local preview";
                var bounds = new Rect(safe.x, Screen.height - safe.yMin - height, safe.width, height);
                DrawEditorAdCard(bounds, label);
                if (expanded)
                {
                    var style = EditorPreviewButtonStyle();
                    float width = Mathf.Min(bounds.width * .35f, style.fontSize * 7);
                    if (GUI.Button(new Rect(bounds.xMax - width - 8, bounds.y + 8, width, style.fontSize * 2), "Collapse", style))
                        previewCollapseExpanded = false;
                }
            }
            if (previewClose != null) DrawEditorFullScreen(safe);
        }

        void DrawEditorFullScreen(Rect safe)
        {
            Ink.Rect(new Rect(0, 0, Screen.width, Screen.height), Ink.Hex("E1E7EE"));
            float width = Mathf.Min(safe.width * .85f, 540);
            float height = Mathf.Min(safe.height * .7f, 430);
            var panel = new Rect(safe.center.x - width * .5f, Screen.height - safe.center.y - height * .5f, width, height);
            DrawEditorAdCard(panel, "");
            Ink.TextAt(new Rect(panel.x + 12, panel.y + height * .12f, panel.width - 24, height * .35f),
                previewTitle + "\nEditor preview", PreviewFontSize(), Ink.Hex("30425F"), TextAnchor.MiddleCenter);
            bool rewarded = previewFormat == Format.Rewarded;
            var button = new Rect(panel.x + panel.width * .09f, panel.y + height * .57f, panel.width * .82f, height * .16f);
            var style = EditorPreviewButtonStyle();
            if (GUI.Button(button, rewarded ? "Complete test ad" : "Close test ad", style))
                previewClose(rewarded);
            else if (rewarded && GUI.Button(new Rect(button.x, button.y + height * .20f, button.width, button.height),
                "Close without reward", style))
                previewClose(false);
        }

        void DrawEditorNativePreview(Rect bounds)
        {
            DrawEditorAdCard(bounds, "");
            float labelHeight = bounds.height * .25f;
            int font = PreviewFontSize();
            var icon = new Rect(bounds.x + bounds.width * .06f, bounds.y + bounds.height * .30f,
                Mathf.Min(bounds.width * .12f, bounds.height * .48f), bounds.height * .42f);
            Ink.Card(icon, Ink.Hex("497379"), 8);
            Ink.TextAt(new Rect(bounds.x, bounds.y, bounds.width, labelHeight),
                "Native · Editor preview", font, Ink.Hex("30425F"), TextAnchor.MiddleCenter);
            Ink.TextAt(new Rect(icon.xMax + 8, icon.y, bounds.width * .48f, icon.height),
                "Sample ad headline", font, Ink.Hex("30425F"), TextAnchor.MiddleLeft);
            var cta = new Rect(bounds.x + bounds.width * .76f, icon.y, bounds.width * .19f, icon.height);
            Ink.Card(cta, Ink.Hex("355963"), 8);
            Ink.TextAt(cta, "Open", font, Color.white, TextAnchor.MiddleCenter);
        }

        static int PreviewFontSize() => Mathf.Clamp(Mathf.RoundToInt(Screen.height * .018f), 10, 42);

        static GUIStyle EditorPreviewButtonStyle() => new GUIStyle(GUI.skin.button) { fontSize = PreviewFontSize() };

        static void DrawEditorAdCard(Rect bounds, string label)
        {
            Ink.Card(bounds, Ink.Hex("EDF2F8"), 8);
            if (label.Length > 0)
                Ink.TextAt(bounds, label, PreviewFontSize(), Ink.Hex("30425F"), TextAnchor.MiddleCenter);
        }
    }
}
#endif
