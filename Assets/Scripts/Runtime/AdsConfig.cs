using System;
using System.Collections.Generic;

namespace DrawArrow
{
    sealed class AdsConfig
    {
        static readonly Dictionary<string, object> defaults = new Dictionary<string, object>
        {
            { "ads_aoa_show", false },
            { "ads_aoa_open_count", 1 },
            { "ads_aoa_type", 1 },
            { "ads_aoa_duration", 10 },
            { "ads_aoa_id", "" },
            { "ads_aoa_id_admob", "" },
            { "ads_aoa_start_level", 1 },
            { "ads_aoa_resume_game_show", false },
            { "ads_inter_shown_on_level_end", false },
            { "ads_inter_ad_break_time", 60 },
            { "ads_inter_ad_break_id", "" },
            { "ads_inter_ad_break_id_admob", "" },
            { "ads_inter_start_level", 1 },
            { "ads_inter_level_range", 1 },
            { "ads_inter_type", 1 },
            { "ads_inter_duration", 60 },
            { "ads_inter_first_show", 60 },
            { "ads_inter_id", "" },
            { "ads_inter_id_admob", "" },
            { "ads_banner_show", false },
            { "ads_banner_type", 1 },
            { "ads_banner_id", "" },
            { "ads_banner_id_admob", "" },
            { "ads_banner_admob_type", 2 },
            { "ads_reward_type", 1 },
            { "ads_reward_id", "" },
            { "ads_reward_id_admob", "" },
            { "ads_mrec_show", false },
            { "ads_mrec_id", "" },
            { "ads_native_show", false },
            { "ads_native_id", "" },
            { "ads_collapse_show", false },
            { "ads_collapse_duration", 60 },
            { "ads_collapse_start_level", 1 },
            { "ads_collapse_id", "" },
        };
        readonly Dictionary<string, object> values = new Dictionary<string, object>(defaults);

        public event Action Changed;
        public Dictionary<string, object> Defaults => new Dictionary<string, object>(defaults);
        public bool Flag(string key) => (bool)values["ads_" + key];
        public int Count(string key) => (int)values["ads_" + key];
        public string Text(string key)
        {
            string value = (string)values["ads_" + key];
#if UNITY_ANDROID && !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (IsDemoUnit(value)) return "";
#endif
            return value;
        }

        internal static bool IsDemoUnit(string value) =>
            value.TrimStart().StartsWith("ca-app-pub-3940256099942544/", StringComparison.Ordinal);

        public string Unit(string format)
        {
            string specialized = Text(format + "_id_admob").Trim();
            return specialized.Length > 0 ? specialized : Text(format + "_id").Trim();
        }

        public void Apply(Func<string, object, object> read)
        {
            bool changed = false;
            foreach (var entry in defaults)
            {
                object value = read(entry.Key, entry.Value);
                if (Equals(value, values[entry.Key])) continue;
                values[entry.Key] = value;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }
    }
}
