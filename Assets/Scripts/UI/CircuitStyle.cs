using UnityEngine;

namespace DrawArrow
{
    internal static class CircuitStyle
    {
        internal static int Theme { get => Mathf.Clamp(PlayerPrefs.GetInt("drawarrow.artTheme", 0), 0, 2);
            set { PlayerPrefs.SetInt("drawarrow.artTheme", Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); } }

        internal static Color ThemeBackground(int theme) =>
            theme == 2 ? Ink.Hex("1B2033") : theme == 1 ? Ink.Hex("EDF4F4") : Ink.Hex("F5F4F6");
        internal static Color Background => ThemeBackground(Theme);
        internal static Color Foreground => Theme == 2 ? Ink.Hex("F2F3FA") : Ink.Hex("252D42");
        internal static Color Accent => Theme == 2 ? Ink.Hex("666ABC") : Ink.Hex("45488E");
        internal static Color ArrowInk => Theme == 2 ? Ink.Hex("D5D8FA") : Ink.Hex("414580");
        internal static Color Panel => Theme == 2 ? Ink.Hex("292F46") : Ink.Hex("FCFBFD");
        internal static Color SoftControl => Theme == 2 ? Ink.Hex("363D57") : Ink.Hex("E3E4EB");
        internal static Color ControlInk => Theme == 2 ? Ink.Hex("E1E4F6") : Ink.Hex("404861");
        internal static Color Teal => Ink.Hex("317C83");

        internal static void RaisedCard(Rect rect, Color fill, float radius)
        {
            Ink.Card(new Rect(rect.x, rect.y + 9, rect.width, rect.height),
                Ink.WithAlpha(Foreground, .10f), radius);
            Ink.Card(rect, fill, radius);
        }

        internal static bool Button(Rect rect, string label, Color color, int size) =>
            Ink.Button(rect, label, color, Color.white, size);

        internal static bool IconButton(Rect rect, string icon, string caption = null) =>
            Ink.Button(rect, caption ?? Glyph(icon), Accent, Color.white, caption == null ? 42 : 26);

        static string Glyph(string icon)
        {
            switch (icon)
            {
                case "CommonIconBack": return "←";
                case "CommonIconSet": return "Ⅱ";
                case "CommonIconHint": return "?";
                case "Sound": return "♪";
                case "vibrations": return "≈";
                case "RiBtnStar": return "★";
                case "Arrow_head_black": return "→";
                default: return "↻";
            }
        }

        internal static void Draw(string name, Rect rect, Color? tint = null, bool fit = false, bool slice = false)
        {
            var color = tint ?? Foreground;
            if (name == "CommonIconHeart1")
            {
                var heart = Ink.Hex("EA536A");
                heart.a = color.a;
                Ink.Heart(rect, heart);
            }
            else if (slice || name == "goal_icon_bg") Ink.Card(rect, color, Mathf.Min(22, rect.height / 3));
            else Ink.TextAt(rect, Glyph(name), Mathf.Max(12, Mathf.RoundToInt(Mathf.Min(rect.width, rect.height) * .75f)),
                color, TextAnchor.MiddleCenter, true);
        }

        internal static void ArrowHead(Vector2 tip, Vector2 direction, float length, float width, Color color)
        {
            var side = new Vector2(-direction.y, direction.x) * (length * .42f);
            var back = tip - direction * length;
            Ink.Line(back + side, tip, width, color);
            Ink.Line(tip, back - side, width, color);
        }
    }
}
