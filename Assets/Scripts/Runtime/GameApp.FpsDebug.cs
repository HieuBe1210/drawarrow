#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        const float FpsSampleWindow = .5f;
        float fpsSampleSeconds;
        float slowestFrameSeconds;
        int fpsSampleFrames;
        string gameplayFpsLabel = "Measuring FPS…";

        void SampleGameplayFps()
        {
            if (page != Page.Game || (ads && ads.Blocking))
            {
                fpsSampleSeconds = slowestFrameSeconds = 0;
                fpsSampleFrames = 0;
                gameplayFpsLabel = "Measuring FPS…";
                return;
            }

            float seconds = Time.unscaledDeltaTime;
            fpsSampleSeconds += seconds;
            slowestFrameSeconds = Mathf.Max(slowestFrameSeconds, seconds);
            fpsSampleFrames++;
            if (fpsSampleSeconds < FpsSampleWindow) return;

            int refreshHz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            gameplayFpsLabel = $"{fpsSampleFrames / fpsSampleSeconds:F0} FPS / {Application.targetFrameRate} target\n" +
                $"Max {slowestFrameSeconds * 1000:F1} ms · {refreshHz} Hz";
            fpsSampleSeconds = slowestFrameSeconds = 0;
            fpsSampleFrames = 0;
        }

        void DrawGameplayFps()
        {
            var box = new Rect(gameViewport.xMin + 42, gameViewport.yMin + 162, 280, 84);
            Ink.Card(box, new Color(0, 0, 0, .74f), 12);
            Ink.TextAt(box, gameplayFpsLabel, 25, Color.white, TextAnchor.MiddleCenter);
        }
    }
}
#endif
