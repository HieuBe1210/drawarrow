using UnityEngine;

namespace DrawArrow
{
    internal static class CircuitStyle
    {
        internal static int Theme { get => Mathf.Clamp(PlayerPrefs.GetInt("drawarrow.artTheme", 0), 0, 2);
            set { PlayerPrefs.SetInt("drawarrow.artTheme", Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); } }

        internal static Color ThemeBackground(int theme) =>
            theme == 2 ? Ink.Hex("17182B") : theme == 1 ? Ink.Hex("E3F3EE") : Ink.Hex("F2F0FA");
        internal static Color Background => ThemeBackground(Theme);
        internal static Color Foreground => Theme == 2 ? Ink.Hex("F4F2FF") : Ink.Hex("272343");
        internal static Color Accent => Theme == 2 ? Ink.Hex("7664E0") : Ink.Hex("5340AD");
        internal static Color ArrowInk => Theme == 2 ? Ink.Hex("DAD1FF") : Ink.Hex("40318B");
        internal static Color Panel => Theme == 2 ? Ink.Hex("272943") : Color.white;

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
