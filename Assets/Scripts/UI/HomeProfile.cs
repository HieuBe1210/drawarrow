using System.Text;
using UnityEngine;

namespace DrawArrow
{
    public sealed partial class GameApp
    {
        const int ProfileNameLimit = 24;
        const int AvatarColumns = 4;
        const int VisibleAvatarRows = 2;
        const float AvatarRowHeight = 174;
        const string ProfileNameControl = "ProfileName";
        static readonly string[] AvatarColors =
        {
            "7560C7", "2C857B", "C36D47", "427AB0",
            "AC5586", "80714D", "586AB3", "477B5B",
            "AA681D", "437DA5", "9E5273", "5C58A0",
            "437D58", "B45645", "356F85", "575377"
        };
        bool profileOpen, renameOpen, focusProfileName;
        string draftProfileName, profileError, renameInput, renameError;
        int draftAvatar;
        GUIStyle profileNameStyle;
        TouchScreenKeyboard profileKeyboard;
        Vector2 avatarPress;
        float avatarScroll, avatarScrollTarget, avatarScrollStart, avatarScrollVelocity;
        bool avatarScrollTracking, avatarDragging;
        int avatarScrollFrame = -1;
        static int AvatarMaxScrollRow =>
            Mathf.Max(0, (AvatarColors.Length + AvatarColumns - 1) / AvatarColumns - VisibleAvatarRows);

        bool DrawHomeAvatar(Rect rect)
        {
            var matrix = Ink.BeginButtonVisual(rect);
            DrawProfileAvatar(rect, Progress.avatarIndex);
            GUI.matrix = matrix;
            return Ink.HitButton(rect, new GUIContent(string.Empty, "Profile"), GUIStyle.none);
        }

        void OpenHomeProfile()
        {
            draftProfileName = string.IsNullOrWhiteSpace(Progress.playerName) ? "Player" : Progress.playerName;
            draftAvatar = Mathf.Clamp(Progress.avatarIndex, 0, AvatarColors.Length - 1);
            int selectedRow = draftAvatar / AvatarColumns;
            avatarScroll = avatarScrollTarget = Mathf.Clamp(selectedRow - VisibleAvatarRows + 1, 0, AvatarMaxScrollRow) * AvatarRowHeight;
            avatarScrollVelocity = 0;
            avatarScrollTracking = avatarDragging = false;
            profileError = "";
            profileOpen = true;
        }

        void CloseHomeProfile()
        {
            CloseProfileRename();
            profileOpen = false;
        }

        void DrawHomeProfile()
        {
            var homeMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Ink.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .58f));
            GUI.matrix = homeMatrix;
            var panel = new Rect(115, 425, 850, 1070);
            GUI.enabled = !renameOpen;
            CircuitStyle.RaisedCard(panel, CircuitStyle.Panel, 42);
            Ink.TextAt(new Rect(160, 464, 760, 87), "Profile", 65, FG, TextAnchor.MiddleCenter, true);

            DrawProfileAvatar(new Rect(176, 605, 130, 130), draftAvatar);
            Ink.TextAt(new Rect(333, 590, 555, 49), "Name", 29, FG);
            var nameRect = new Rect(333, 643, 555, 86);
            Ink.Card(nameRect, CircuitStyle.Background, 16);
            var input = new Rect(nameRect.x + 20, nameRect.y, nameRect.width - 174, nameRect.height);
            Ink.TextAt(input, NormalizeProfileName(draftProfileName), 36, FG, TextAnchor.MiddleLeft);
            if (Ink.Button(new Rect(nameRect.xMax - 145, nameRect.y + 12, 130, 62), "Edit",
                CircuitStyle.Accent, Color.white, 25))
            {
                OpenProfileRename();
                GUI.enabled = false;
            }

            Ink.TextAt(new Rect(176, 815, 710, 55), "Avatar", 33, FG, TextAnchor.MiddleLeft, true);
            DrawProfileAvatarPicker(new Rect(164, 884, 752, 350));

            if (!string.IsNullOrEmpty(profileError))
                Ink.TextAt(new Rect(176, 1298, 728, 44), profileError, 25, Ink.Red, TextAnchor.MiddleCenter);
            bool cancel = Ink.Button(new Rect(176, 1354, 230, 88), "Cancel", CircuitStyle.Background, FG, 34);
            bool save = Ink.Button(new Rect(430, 1354, 474, 88), "Save", CircuitStyle.Accent, Color.white, 40);
            GUI.enabled = true;
            if (renameOpen)
            {
                DrawProfileRename();
                return;
            }
            if (cancel)
            {
                CloseHomeProfile();
                return;
            }
            if (!save) return;

            string name = NormalizeProfileName(draftProfileName);
            if (name.Length == 0)
            {
                profileError = "Enter a name to save your profile.";
                return;
            }
            Progress.playerName = name;
            Progress.avatarIndex = draftAvatar;
            SaveStore.Save(Progress);
            CloseHomeProfile();
        }

        void DrawProfileAvatarPicker(Rect viewport)
        {
            float maxScroll = AvatarMaxScrollRow * AvatarRowHeight;
            var pointer = Event.current;
            if (GUI.enabled && pointer.type == EventType.MouseDown && pointer.button == 0 &&
                viewport.Contains(pointer.mousePosition))
            {
                avatarScrollTracking = true;
                avatarDragging = false;
                avatarPress = pointer.mousePosition;
                avatarScrollStart = avatarScroll;
            }
            else if (avatarScrollTracking && pointer.type == EventType.MouseDrag)
            {
                var delta = pointer.mousePosition - avatarPress;
                if (avatarDragging || (Mathf.Abs(delta.y) > 14 && Mathf.Abs(delta.y) > Mathf.Abs(delta.x)))
                {
                    if (!avatarDragging) Ink.CancelButtonAnimation();
                    avatarDragging = true;
                    avatarScroll = Mathf.Clamp(avatarScrollStart - delta.y, 0, maxScroll);
                    pointer.Use();
                }
            }
            else if (avatarScrollTracking && (pointer.type == EventType.MouseUp || pointer.type == EventType.Ignore))
            {
                avatarScrollTracking = false;
                if (avatarDragging)
                {
                    avatarDragging = false;
                    avatarScrollVelocity = 0;
                    avatarScrollTarget = Mathf.Clamp(Mathf.Round(avatarScroll / AvatarRowHeight) * AvatarRowHeight, 0, maxScroll);
                    if (pointer.type == EventType.MouseUp) pointer.Use();
                }
            }
            else if (GUI.enabled && pointer.type == EventType.ScrollWheel && viewport.Contains(pointer.mousePosition))
            {
                avatarScrollTarget = Mathf.Clamp(avatarScrollTarget + Mathf.Sign(pointer.delta.y) * AvatarRowHeight, 0, maxScroll);
                pointer.Use();
            }
            if (!avatarDragging && pointer.type == EventType.Repaint && avatarScrollFrame != Time.frameCount)
            {
                avatarScrollFrame = Time.frameCount;
                avatarScroll = Mathf.SmoothDamp(avatarScroll, avatarScrollTarget, ref avatarScrollVelocity,
                    .12f, Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(avatarScroll - avatarScrollTarget) < .1f) avatarScroll = avatarScrollTarget;
            }

            Ink.Card(viewport, CircuitStyle.Background, 24);
            GUI.BeginGroup(viewport);
            for (int i = 0; i < AvatarColors.Length; i++)
            {
                var tile = new Rect(18 + (i % AvatarColumns) * 180, 10 + (i / AvatarColumns) * AvatarRowHeight - avatarScroll,
                    156, 156);
                var matrix = Ink.BeginButtonVisual(tile);
                if (i == draftAvatar)
                    Ink.Card(new Rect(tile.x - 7, tile.y - 7, tile.width + 14, tile.height + 14),
                        Ink.Hex("39B56A"), tile.width * .21f + 7);
                DrawProfileAvatar(tile, i);
                GUI.matrix = matrix;
                if (Ink.HitButton(tile, new GUIContent(string.Empty, "Avatar " + (i + 1)), GUIStyle.none))
                    draftAvatar = i;
            }
            GUI.EndGroup();

            if (maxScroll > 0)
            {
                const float thumbHeight = 77;
                var thumb = new Rect(viewport.xMax - 17,
                    viewport.y + 11 + (viewport.height - thumbHeight - 22) * (avatarScroll / maxScroll), 7, thumbHeight);
                Ink.Card(thumb, Ink.WithAlpha(FG, .42f), 4);
            }
        }

        void OpenProfileRename()
        {
            renameInput = draftProfileName;
            renameError = "";
            renameOpen = true;
            if (TouchScreenKeyboard.isSupported) OpenProfileKeyboard();
            else focusProfileName = true;
        }

        void OpenProfileKeyboard()
        {
            profileKeyboard = TouchScreenKeyboard.Open(renameInput, TouchScreenKeyboardType.Default,
                false, false, false, false, "Enter a new name", ProfileNameLimit);
        }

        void CloseProfileRename()
        {
            CloseProfileKeyboard();
            renameOpen = focusProfileName = false;
        }

        void CloseProfileKeyboard()
        {
            if (profileKeyboard == null) return;
            profileKeyboard.active = false;
            profileKeyboard = null;
        }

        void DrawProfileRename()
        {
            var e = Event.current;
            bool submit = e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == ProfileNameControl &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            if (submit) e.Use();
            Ink.Rect(new Rect(0, 0, Width, Height), new Color(0, 0, 0, .40f));
            Ink.Card(new Rect(150, 335, 780, 440), CircuitStyle.Panel, 36);
            Ink.TextAt(new Rect(188, 369, 704, 76), "Change name", 50, FG, TextAnchor.MiddleCenter, true);
            var field = new Rect(205, 466, 670, 92);
            Ink.Card(field, CircuitStyle.Background, 16);
            var input = new Rect(field.x + 20, field.y, field.width - 40, field.height);
            if (TouchScreenKeyboard.isSupported)
            {
                if (profileKeyboard != null)
                {
                    var status = profileKeyboard.status;
                    if (status != TouchScreenKeyboard.Status.Canceled) renameInput = profileKeyboard.text;
                    if (status == TouchScreenKeyboard.Status.Done)
                    {
                        ApplyProfileRename();
                        if (!renameOpen) return;
                    }
                    else if (status != TouchScreenKeyboard.Status.Visible)
                        CloseProfileKeyboard();
                }
                Ink.TextAt(input, NormalizeProfileName(renameInput), 38, FG, TextAnchor.MiddleLeft);
                if (Ink.HitButton(field, new GUIContent(string.Empty, "Enter a new name"), GUIStyle.none) &&
                    profileKeyboard == null)
                    OpenProfileKeyboard();
            }
            else
            {
                if (profileNameStyle == null)
                    profileNameStyle = new GUIStyle(GUIStyle.none)
                    {
                        font = Ink.RegularFont, fontSize = 38, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip
                    };
                profileNameStyle.normal.textColor = FG;
                profileNameStyle.focused.textColor = FG;
                GUI.SetNextControlName(ProfileNameControl);
                renameInput = GUI.TextField(input, renameInput, ProfileNameLimit, profileNameStyle);
                if (focusProfileName && Event.current.type == EventType.Repaint)
                {
                    GUI.FocusControl(ProfileNameControl);
                    focusProfileName = false;
                }
            }

            if (!string.IsNullOrEmpty(renameError))
                Ink.TextAt(new Rect(205, 570, 670, 45), renameError, 26, Ink.Red, TextAnchor.MiddleCenter);
            if (Ink.Button(new Rect(205, 635, 245, 84), "Cancel", CircuitStyle.Background, FG, 32))
            {
                CloseProfileRename();
                return;
            }
            if (Ink.Button(new Rect(474, 635, 401, 84), "Apply", CircuitStyle.Accent, Color.white, 36) || submit)
                ApplyProfileRename();
        }

        void ApplyProfileRename()
        {
            CloseProfileKeyboard();
            string name = NormalizeProfileName(renameInput);
            if (name.Length == 0)
            {
                renameError = "Enter a name to continue.";
                return;
            }
            draftProfileName = name;
            profileError = "";
            if (GUI.GetNameOfFocusedControl() == ProfileNameControl) GUI.FocusControl(null);
            CloseProfileRename();
        }

        static string NormalizeProfileName(string value)
        {
            var name = new StringBuilder(ProfileNameLimit);
            foreach (char character in (value ?? "").Trim())
            {
                if (char.IsControl(character) || character == '<' || character == '>') continue;
                if (name.Length == ProfileNameLimit) break;
                name.Append(character);
            }
            if (name.Length > 0 && char.IsHighSurrogate(name[name.Length - 1])) name.Length--;
            return name.ToString().Trim();
        }

        static void DrawProfileAvatar(Rect tile, int index)
        {
            index = Mathf.Clamp(index, 0, AvatarColors.Length - 1);
            var background = Ink.Hex(AvatarColors[index]);
            var face = index % 2 == 0 ? Ink.Hex("FFF0CB") : Ink.Hex("ECF5F2");
            var shadow = Color.Lerp(background, Color.black, .55f);
            Ink.Card(tile, background, tile.width * .21f);
            float unit = tile.width;
            var center = tile.center + Vector2.up * (.07f * unit);
            if (index >= 8)
            {
                DrawExpressionFace(tile.center, unit, index - 8, shadow);
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var ear = center + new Vector2(side * .22f, -.20f) * unit;
                if (index == 1 || index == 5)
                    Ink.Card(new Rect(ear.x - .073f * unit, ear.y - .24f * unit,
                        .146f * unit, .31f * unit), face, .065f * unit);
                else if (index == 2 || index == 4)
                    Ink.Line(ear + new Vector2(side * .08f, -.12f) * unit, ear,
                        .16f * unit, face);
                else
                    Ink.Circle(ear, .115f * unit, face);
            }
            Ink.Circle(center, .285f * unit, face);
            if (index == 3 || index == 6)
            {
                Ink.Circle(center + new Vector2(-.12f, -.035f) * unit, .10f * unit, background);
                Ink.Circle(center + new Vector2(.12f, -.035f) * unit, .10f * unit, background);
            }
            Ink.Circle(center + new Vector2(-.10f, -.025f) * unit, .035f * unit, shadow);
            Ink.Circle(center + new Vector2(.10f, -.025f) * unit, .035f * unit, shadow);
            Ink.Line(center + new Vector2(-.045f, .10f) * unit, center + new Vector2(.045f, .10f) * unit,
                .028f * unit, shadow);
        }

        static void DrawExpressionFace(Vector2 center, float size, int expression, Color ink)
        {
            Vector2 At(float x, float y) => center + new Vector2(x, y) * size;
            void Stroke(float x1, float y1, float x2, float y2)
                => Ink.Line(At(x1, y1), At(x2, y2), .037f * size, ink);
            void Mouth(float x, float y, float bend)
                => Ink.Curve(At(-x, y), At(0, y + bend), At(x, y), .036f * size, ink);

            Ink.Circle(center, size * .34f, Ink.Hex("FFD98B"));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * .12f;
                if ((expression == 1 && side > 0) || expression == 5)
                    Stroke(x - .039f, -.064f, x + .039f, -.064f);
                else if (expression == 4)
                    Ink.Card(new Rect(At(x - .075f, -.11f), new Vector2(.15f, .115f) * size),
                        ink, size * .026f);
                else
                    Ink.Circle(At(x, -.061f), size * (expression == 2 ? .045f : .032f), ink);
                if (expression == 6)
                    Stroke(x - side * .045f, -.129f, x + side * .045f, -.172f);
                if (expression == 7)
                    Ink.Heart(new Rect(At(x - .06f, -.15f), new Vector2(.12f, .12f) * size), Ink.Hex("D64463"));
            }

            switch (expression)
            {
                case 0: Mouth(.14f, .06f, .21f); break;
                case 1: Mouth(.095f, .095f, .115f); break;
                case 2: Ink.Circle(At(0, .13f), size * .063f, ink); break;
                case 3: Mouth(.12f, .16f, -.15f); break;
                case 4: Stroke(-.055f, -.065f, .055f, -.065f); Mouth(.10f, .10f, .08f); break;
                case 5: Ink.Circle(At(0, .11f), size * .043f, ink); break;
                case 6: Mouth(.10f, .15f, -.11f); break;
                case 7: Mouth(.10f, .09f, .14f); break;
            }
        }
    }
}
