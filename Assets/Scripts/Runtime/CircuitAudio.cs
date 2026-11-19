using UnityEngine;

namespace DrawArrow
{
    internal static class CircuitAudio
    {
        internal static AudioClip Create(string cue)
        {
            float frequency, end, duration;
            switch (cue)
            {
                case "result_pass": frequency = 440; end = 880; duration = .38f; break;
                case "result_fail": frequency = 270; end = 100; duration = .26f; break;
                case "arrow_back": frequency = 170; end = 100; duration = .15f; break;
                case "arrow_out_soft": frequency = 390; end = 760; duration = .17f; break;
                case "arrow_collect": frequency = 520; end = 1040; duration = .18f; break;
                default: frequency = 540; end = 410; duration = .07f; break;
            }

            const int sampleRate = 22050;
            var samples = new float[Mathf.CeilToInt(duration * sampleRate)];
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)(samples.Length - 1);
                phase += 2 * Mathf.PI * Mathf.Lerp(frequency, end, t) / sampleRate;
                float envelope = Mathf.Min(1f, t * 24f) * (1 - t) * (1 - t);
                samples[i] = .38f * envelope * (Mathf.Sin(phase) + .16f * Mathf.Sin(2 * phase));
            }
            var clip = AudioClip.Create(cue, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
