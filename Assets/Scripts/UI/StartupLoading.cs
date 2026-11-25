using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        static readonly Color LoadingPaper = Ink.Hex("F4F2F8");
        static readonly Color LoadingPurple = Ink.Hex("4F4ABD");
        static readonly Color LoadingSlate = Ink.Hex("57618C");
        static readonly Color LoadingWire = Ink.Hex("A6AAC4");
        static readonly Vector2 LoadingBoardCenter = new Vector2(540, 935);
        static readonly Vector2[] LoadingArrowPositions =
        {
            Vector2.down, Vector2.right, Vector2.up, Vector2.left
        };

        void DrawStartupLoading(float progress)
        {
            if (Event.current.type != EventType.Repaint) return;
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Ink.Rect(new Rect(0, 0, Screen.width, Screen.height), LoadingPaper);
            var safe = Screen.safeArea;
            float fit = Mathf.Min(safe.width / Width, safe.height / Height);
            var offset = new Vector2(safe.center.x - Width * fit * .5f,
                Screen.height - safe.center.y - Height * fit * .5f);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(fit, fit, 1));

            DrawLoadingCircuit(false);
            DrawLoadingCircuit(true);
            Ink.TextAt(new Rect(60, 338, 960, 155), "Arrow <color=#4943AC>Tidy</color>",
                132, Ink.Hex("242944"), TextAnchor.MiddleCenter, true);
            Ink.Arrow(new Vector2(245, 528), new Vector2(714, 528), 13, LoadingPurple);

            const float step = 224;
            for (int row = -1; row <= 1; row++)
            {
                var across = LoadingBoardCenter + new Vector2(0, row * step);
                var down = LoadingBoardCenter + new Vector2(row * step, 0);
                Ink.Line(across - Vector2.right * step, across + Vector2.right * step, 15, LoadingWire);
                Ink.Line(down - Vector2.up * step, down + Vector2.up * step, 15, LoadingWire);
            }
            for (int y = -1; y <= 1; y += 2)
            for (int x = -1; x <= 1; x += 2)
            {
                var corner = LoadingBoardCenter + step * new Vector2(x, y);
                DrawLoadingTile(corner, 112, Ink.Hex("E9E8F1"), false);
                Ink.Circle(corner, 20, Ink.Hex("7883A9"));
            }

            float phase = Mathf.Repeat(Time.unscaledTime / .38f, LoadingArrowPositions.Length);
            int active = Mathf.FloorToInt(phase);
            for (int index = 0; index < LoadingArrowPositions.Length; index++)
            {
                var direction = LoadingArrowPositions[index];
                var position = LoadingBoardCenter + step * direction;
                float light = index == active ? .55f + .45f * Mathf.Sin(Mathf.PI * (phase - active)) : 0;
                DrawLoadingTile(position, 142, Color.Lerp(Ink.Hex("EBEAF3"), Ink.Hex("9D98F0"), light), index == active);
                var arrowDirection = direction == Vector2.left ? Vector2.right : direction;
                Ink.Arrow(position - arrowDirection * 30, position + arrowDirection * 30, 12,
                    Color.Lerp(LoadingSlate, Color.white, light));
            }

            DrawLoadingTile(LoadingBoardCenter, 178, LoadingPurple, true);
            Ink.Arrow(LoadingBoardCenter - Vector2.right * 47, LoadingBoardCenter + Vector2.right * 47, 17, Color.white);
            Ink.TextAt(new Rect(120, 1340, 840, 104), "Loading…", 58,
                LoadingSlate, TextAnchor.MiddleCenter, true);
            Ink.Card(new Rect(138, 1478, 804, 74), Ink.Hex("DDDCE9"), 37);
            Ink.Card(new Rect(145, 1485, 790 * Mathf.Clamp01(progress), 60), LoadingPurple, 30);
            GUI.matrix = previousMatrix;
        }

        static void DrawLoadingTile(Vector2 center, float size, Color fill, bool lit)
        {
            var tile = new Rect(center.x - size * .5f, center.y - size * .5f, size, size);
            float radius = size * .28f;
            var shadow = new Rect(tile.x - 4, tile.y + 13, size + 8, size + 6);
            Ink.Card(shadow, Ink.WithAlpha(lit ? LoadingPurple : LoadingSlate, .16f), radius + 5);
            Ink.Card(tile, Color.Lerp(fill, Color.white, .72f), radius);
            Ink.Card(new Rect(tile.x + 3, tile.y + 4, size - 6, size - 7), fill, radius - 3);
        }

        static void DrawLoadingCircuit(bool bottom)
        {
            var wire = Ink.Hex("DDDDEB");
            var origin = new Vector2(120, 119);
            var elbow = new Vector2(400, 119);
            var junction = new Vector2(540, 204);
            var end = new Vector2(960, 292);
            if (bottom)
            {
                origin = new Vector2(Width, Height) - origin;
                elbow = new Vector2(Width, Height) - elbow;
                junction = new Vector2(Width, Height) - junction;
                end = new Vector2(Width, Height) - end;
            }
            var bend = new Vector2(elbow.x, junction.y);
            var lastBend = new Vector2(end.x, junction.y);
            Ink.Line(origin, elbow, 8, wire);
            Ink.Line(elbow, bend, 8, wire);
            Ink.Line(bend, lastBend, 8, wire);
            Ink.Line(lastBend, end, 8, wire);
            Ink.Circle(origin, 29, wire);
            Ink.Circle(junction, 17, wire);
            Ink.Circle(end, 23, wire);
        }
    }
}
