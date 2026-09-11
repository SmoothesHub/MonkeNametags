using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MonkeNameplates.Core;
using UnityEngine;

namespace MonkeNameplates
{
    internal sealed class SettingsGui : IDisposable
    {
        private readonly Plugin plugin;
        private readonly Texture2D background = new Texture2D(1, 1);
        private readonly Texture2D panel = new Texture2D(1, 1);
        private readonly Texture2D accent = new Texture2D(1, 1);
        private readonly Dictionary<string, string> fields = new Dictionary<string, string>();
        private GUIStyle frame, label, muted, title, button, activeButton, input, slider, thumb;
        private string themeSignature;
        private int tab = 2;
        private int colorTarget;
        private Vector2 scroll;
        private string fontPath = "";
        private bool clearFocus;
        private string[] fonts = Array.Empty<string>();
        public bool HasTextFocus { get; private set; }
        private Settings S => plugin.Current;

        public SettingsGui(Plugin plugin) { this.plugin = plugin; RefreshFonts(); }
        public void ClearFocus() { clearFocus = true; HasTextFocus = false; }
        public void ReloadFields() { fields.Clear(); themeSignature = null; ClearFocus(); RefreshFonts(); }

        public void Draw()
        {
            BuildStyles();
            if (clearFocus) { GUI.FocusControl(null); clearFocus = false; }
            float width = Mathf.Min(560, Mathf.Max(280, Screen.width - 16));
            float height = Mathf.Min(660, Mathf.Max(250, Screen.height - 16));
            var previous = new Rect(S.guiX, S.guiY, width, height);
            previous.x = Mathf.Clamp(previous.x, 0, Mathf.Max(0, Screen.width - width));
            previous.y = Mathf.Clamp(previous.y, 0, Mathf.Max(0, Screen.height - height));
            Rect next = GUI.Window(0x4D4E50, previous, DrawWindow, "", frame);
            next.x = Mathf.Clamp(next.x, 0, Mathf.Max(0, Screen.width - width));
            next.y = Mathf.Clamp(next.y, 0, Mathf.Max(0, Screen.height - height));
            if (Mathf.Abs(S.guiX - next.x) > 0.1f || Mathf.Abs(S.guiY - next.y) > 0.1f)
            { S.guiX = next.x; S.guiY = next.y; plugin.MarkDirty(); }
            HasTextFocus = (GUI.GetNameOfFocusedControl() ?? "").StartsWith("input_", StringComparison.Ordinal);
        }

        private void DrawWindow(int id)
        {
            float width = Mathf.Min(560, Mathf.Max(280, Screen.width - 16));
            float height = Mathf.Min(660, Mathf.Max(250, Screen.height - 16));
            GUI.DrawTexture(new Rect(0, 0, width, 3), accent);
            GUI.Label(new Rect(18, 12, width - 75, 28), "MONKE NAMEPLATES", title);
            if (GUI.Button(new Rect(width - 43, 10, 28, 28), "X", button)) plugin.SetGuiVisible(false);
            string[] tabs = { "KEYBIND", "CUSTOMIZE", "NAMETAG" };
            for (int i = 0; i < tabs.Length; i++)
            {
                float tabWidth = (width - 44) / 3;
                if (GUI.Button(new Rect(18 + i * (tabWidth + 4), 48, tabWidth, 30), tabs[i], tab == i ? activeButton : button))
                { tab = i; scroll = Vector2.zero; ClearFocus(); }
            }

            GUILayout.BeginArea(new Rect(18, 90, width - 36, height - 172));
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawKeybind(); break;
                case 1: DrawCustomize(); break;
                default: DrawNametag(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUI.Label(new Rect(18, height - 77, width - 36, 30), plugin.Status, muted);
            if (GUI.Button(new Rect(18, height - 40, 118, 27), "SAVE NOW", button)) plugin.SaveNow();
            if (GUI.Button(new Rect(143, height - 40, 140, 27), "RELOAD CONFIG", button)) plugin.ReloadConfig();
            GUI.Label(new Rect(296, height - 37, width - 314, 24), "Toggle: " + S.toggleKey, muted);
            GUI.DragWindow(new Rect(0, 3, width - 54, 39));
        }

        private void DrawKeybind()
        {
            GUILayout.Label("OPEN / CLOSE THE WINDOW", title);
            GUILayout.Space(10);
            GUILayout.Label("Current key: " + S.toggleKey, label);
            GUILayout.Label("Choose any letter A-Z or number 0-9. Click below, then press your new key.", muted);
            GUILayout.Space(12);
            if (GUILayout.Button(plugin.CapturingKey ? "LISTENING... CLICK TO CANCEL" : "CHANGE KEY", activeButton, GUILayout.Height(38)))
            { plugin.CapturingKey = !plugin.CapturingKey; ClearFocus(); }
            if (GUILayout.Button("RESET TO Q", button, GUILayout.Height(30)))
            { plugin.CapturingKey = false; S.toggleKey = "Q"; plugin.MarkDirty(); }
            GUILayout.Space(18);
            GUILayout.Label("The window starts hidden every time you launch the game. Typing in an input box will not toggle it.", muted);
            GUILayout.Space(18);
            GUILayout.Label("GUI position (pixels)", title);
            Slider("Window X", ref S.guiX, 0, Mathf.Max(0, Screen.width - 560), "0");
            Slider("Window Y", ref S.guiY, 0, Mathf.Max(0, Screen.height - 660), "0");
            GUILayout.Label("You can also drag the title bar.", muted);
            if (GUILayout.Button("RESET WINDOW POSITION", button, GUILayout.Height(30)))
            { S.guiX = 60; S.guiY = 60; plugin.MarkDirty(); }
        }

        private void DrawNametag()
        {
            Toggle("Nametags", ref S.enabled);
            Toggle("Show FPS under names", ref S.showFps);
            GUILayout.Space(8);
            GUILayout.Label("POSITION & SIZE", title);
            Slider("Left / right", ref S.offsetX, -0.5f, 0.5f);
            Slider("Height", ref S.offsetY, -0.1f, 1.2f);
            Slider("Front / back", ref S.offsetZ, -0.5f, 0.5f);
            Slider("Nametag size", ref S.size, 0.4f, 2f);
            Slider("FPS text scale", ref S.fpsScale, 0.4f, 1f);
            Slider("FPS spacing", ref S.fpsGap, 0.1f, 0.5f);
            Slider("View distance", ref S.maxDistance, 1f, 30f, "0.0");
            GUILayout.Label("Positions are in meters relative to the head. Walls always hide labels.", muted);
            GUILayout.Space(12);
            GUILayout.Label("FONT IMPORT", title);
            GUILayout.Label("Active: " + (string.IsNullOrEmpty(S.fontFile) ? "Default" : S.fontFile), muted);
            GUILayout.Label("Paste the full path to a .ttf or .otf font:", muted);
            GUI.SetNextControlName("input_fontpath");
            fontPath = GUILayout.TextField(fontPath, input, GUILayout.Height(28));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("IMPORT & USE", activeButton, GUILayout.Height(30)))
            { plugin.ImportFont(fontPath); RefreshFonts(); }
            if (GUILayout.Button("DEFAULT FONT", button, GUILayout.Height(30))) plugin.UseFont("");
            if (GUILayout.Button("REFRESH LIST", button, GUILayout.Height(30))) RefreshFonts();
            GUILayout.EndHorizontal();
            foreach (string font in fonts)
                if (GUILayout.Button(font, string.Equals(S.fontFile, font, StringComparison.OrdinalIgnoreCase) ? activeButton : button))
                    plugin.UseFont(font);
            GUILayout.Space(10);
            GUILayout.Label("144+ purple   |   60-143 green\n36-59 orange   |   below 36 red\nFPS -- means no valid game-reported reading.", muted);
        }

        private void DrawCustomize()
        {
            GUILayout.Label("GUI COLORS", title);
            GUILayout.Label("Choose a part of the window, then adjust its color.", muted);
            GUILayout.BeginHorizontal();
            string[] targets = { "BACKGROUND", "PANEL", "ACCENT", "TEXT" };
            for (int i = 0; i < targets.Length; i++)
                if (GUILayout.Button(targets[i], i == colorTarget ? activeButton : button, GUILayout.Height(30)))
                { colorTarget = i; ClearFocus(); }
            GUILayout.EndHorizontal();
            string hex = TargetColor();
            string fieldId = "color" + colorTarget;
            if (!fields.TryGetValue(fieldId, out string typed)) typed = hex;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hex", label, GUILayout.Width(50));
            GUI.SetNextControlName("input_" + fieldId);
            string newTyped = GUILayout.TextField(typed, 7, input, GUILayout.Height(28));
            fields[fieldId] = newTyped;
            GUILayout.EndHorizontal();
            if (typed != newTyped)
            {
                string normalized = Rules.Hex(newTyped, "");
                if (normalized.Length > 0) SetTargetColor(normalized);
            }
            ColorUtility.TryParseHtmlString(TargetColor(), out Color color);
            Color before = color;
            ColorSlider("Red", ref color.r);
            ColorSlider("Green", ref color.g);
            ColorSlider("Blue", ref color.b);
            if (before != color)
            {
                string value = "#" + ColorUtility.ToHtmlStringRGB(color);
                fields[fieldId] = value;
                SetTargetColor(value);
            }
            GUILayout.Space(16);
            GUILayout.Label("PRESETS", title);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PURPLE NIGHT", button, GUILayout.Height(34)))
                Preset("#151821", "#252B39", "#B266FF", "#F2F4FA");
            if (GUILayout.Button("JUNGLE", button, GUILayout.Height(34)))
                Preset("#10201B", "#22382E", "#45E078", "#EAF7ED");
            if (GUILayout.Button("LAVA", button, GUILayout.Height(34)))
                Preset("#231810", "#392A24", "#FF942D", "#FFF1E7");
            GUILayout.EndHorizontal();
            GUILayout.Space(18);
            GUILayout.Label("GUI colors apply immediately. Nametag colors still follow each gorilla and their tag state.", muted);
            GUILayout.Label("Changes save automatically after you stop adjusting a setting.", muted);
        }

        private void Toggle(string text, ref bool value)
        {
            if (GUILayout.Button((value ? "ON   " : "OFF  ") + text, value ? activeButton : button, GUILayout.Height(29)))
            { value = !value; plugin.MarkDirty(); }
        }

        private void Slider(string text, ref float value, float min, float max, string format = "0.00")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(text, label, GUILayout.Width(122));
            float next = GUILayout.HorizontalSlider(value, min, max, slider, thumb, GUILayout.Height(24));
            GUILayout.Label(value.ToString(format, CultureInfo.InvariantCulture), muted, GUILayout.Width(48));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(next - value) > 0.00001f) { value = next; plugin.MarkDirty(); }
        }

        private void ColorSlider(string text, ref float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(text, label, GUILayout.Width(80));
            value = GUILayout.HorizontalSlider(value, 0, 1, slider, thumb, GUILayout.Height(28));
            GUILayout.Label(Mathf.RoundToInt(value * 255).ToString(), muted, GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }

        private string TargetColor() => colorTarget == 0 ? S.backgroundColor : colorTarget == 1 ? S.panelColor : colorTarget == 2 ? S.accentColor : S.textColor;
        private void SetTargetColor(string hex)
        {
            if (colorTarget == 0) S.backgroundColor = hex;
            else if (colorTarget == 1) S.panelColor = hex;
            else if (colorTarget == 2) S.accentColor = hex;
            else S.textColor = hex;
            plugin.MarkDirty();
        }
        private void Preset(string bg, string p, string a, string t)
        { S.backgroundColor = bg; S.panelColor = p; S.accentColor = a; S.textColor = t; fields.Clear(); plugin.MarkDirty(); }

        private void RefreshFonts()
        {
            try
            {
                var list = new List<string>();
                foreach (string path in Directory.GetFiles(plugin.Store.FontsFolder))
                    if (Path.GetExtension(path).Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetExtension(path).Equals(".otf", StringComparison.OrdinalIgnoreCase)) list.Add(Path.GetFileName(path));
                list.Sort(StringComparer.OrdinalIgnoreCase);
                fonts = list.ToArray();
            }
            catch { fonts = Array.Empty<string>(); }
        }

        private void BuildStyles()
        {
            string signature = S.backgroundColor + S.panelColor + S.accentColor + S.textColor;
            if (signature == themeSignature) return;
            themeSignature = signature;
            ColorUtility.TryParseHtmlString(S.backgroundColor, out Color bg);
            ColorUtility.TryParseHtmlString(S.panelColor, out Color pc);
            ColorUtility.TryParseHtmlString(S.accentColor, out Color ac);
            ColorUtility.TryParseHtmlString(S.textColor, out Color tc);
            Paint(background, bg); Paint(panel, pc); Paint(accent, ac);
            label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            label.normal.textColor = tc;
            muted = new GUIStyle(label) { fontSize = 12 };
            muted.normal.textColor = Color.Lerp(tc, bg, 0.25f);
            title = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 16 };
            frame = new GUIStyle { padding = new RectOffset(0, 0, 0, 0) };
            frame.normal.background = background;
            button = new GUIStyle(GUI.skin.button) { fontSize = 11, padding = new RectOffset(6, 6, 5, 5), wordWrap = true };
            foreach (GUIStyleState state in new[] { button.normal, button.hover, button.active, button.focused })
            { state.background = panel; state.textColor = tc; }
            button.hover.textColor = ac;
            activeButton = new GUIStyle(button);
            foreach (GUIStyleState state in new[] { activeButton.normal, activeButton.hover, activeButton.active, activeButton.focused })
            { state.background = accent; state.textColor = ac.grayscale > 0.55f ? new Color(0.05f, 0.06f, 0.09f) : Color.white; }
            input = new GUIStyle(GUI.skin.textField) { fontSize = 13, padding = new RectOffset(8, 8, 5, 5) };
            foreach (GUIStyleState state in new[] { input.normal, input.focused, input.hover, input.active })
            { state.background = panel; state.textColor = tc; }
            slider = new GUIStyle(GUI.skin.horizontalSlider);
            slider.normal.background = panel;
            thumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
            foreach (GUIStyleState state in new[] { thumb.normal, thumb.hover, thumb.active }) state.background = accent;
        }

        private static void Paint(Texture2D texture, Color color) { texture.SetPixel(0, 0, color); texture.Apply(); }
        public void Dispose() { UnityEngine.Object.Destroy(background); UnityEngine.Object.Destroy(panel); UnityEngine.Object.Destroy(accent); }
    }
}
