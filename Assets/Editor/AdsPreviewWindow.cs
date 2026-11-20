using DrawArrow;
using UnityEditor;
using UnityEngine;
using Preview = DrawArrow.AdsController.EditorAdPreview;

sealed class AdsPreviewWindow : EditorWindow
{
    string result;

    [MenuItem("Draw Arrow/Ads/Test All Formats")]
    static void Open()
    {
        GetWindow<AdsPreviewWindow>("Ads Test").minSize = new Vector2(250, 360);
    }

    void OnGUI()
    {
        var ads = Application.isPlaying ? FindFirstObjectByType<AdsController>() : null;
        if (!ads) EditorGUILayout.HelpBox("Enter Play Mode to test ads in Game View or Device Simulator.", MessageType.Info);
        else if (ads.Blocking) EditorGUILayout.HelpBox("Wait for Loading or close the preview in the game.", MessageType.Info);

        using (new EditorGUI.DisabledScope(!ads || ads.Blocking))
        {
            EditorGUILayout.LabelField("Full-screen", EditorStyles.boldLabel);
            DrawButton(ads, "App Open (Startup)", Preview.AppOpenStartup);
            DrawButton(ads, "App Open (Resume)", Preview.AppOpenResume);
            DrawButton(ads, "Interstitial (Level End)", Preview.InterstitialLevelEnd);
            DrawButton(ads, "Interstitial (Timed Ad Break)", Preview.InterstitialBreak);
            DrawButton(ads, "Rewarded", Preview.Rewarded);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Placements", EditorStyles.boldLabel);
            DrawButton(ads, "Banner", Preview.Banner);
            DrawButton(ads, "MREC", Preview.Mrec);
            DrawButton(ads, "Native (MREC Fallback Placement)", Preview.Native);
            DrawButton(ads, "Collapsible Banner", Preview.Collapsible);
            DrawButton(ads, "Return to Automatic Placements", Preview.Automatic);
        }

        if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.None);
    }

    void DrawButton(AdsController ads, string label, Preview preview)
    {
        if (!GUILayout.Button(label) || !ads) return;
        if (ads.TryPreviewAd(preview, granted =>
            {
                if (!this) return;
                result = granted ? "Rewarded callback: reward granted." : "Rewarded callback: closed without reward.";
                Repaint();
            }))
            result = preview == Preview.Automatic ? "Automatic placements restored." : "Preview selected: " + label;
    }

    void OnInspectorUpdate() => Repaint();
}
