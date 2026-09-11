using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using MonkeNameplates.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MonkeNameplates
{
    [BepInPlugin("local.monkenameplates", "Monke Nameplates", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal Settings Current { get; private set; }
        internal ConfigStore Store { get; private set; }
        internal bool CapturingKey { get; set; }
        internal string Status { get; private set; } = "Starting...";
        private readonly GameBridge game = new GameBridge();
        private readonly Visibility visibility = new Visibility();
        private readonly KeyboardInput keyboard = new KeyboardInput();
        private readonly Dictionary<int, WorldLabel> labels = new Dictionary<int, WorldLabel>();
        private readonly List<int> stale = new List<int>();
        private SettingsGui gui;
        private FontResource font;
        private bool guiVisible, dirty, initialized, oldCursorVisible;
        private CursorLockMode oldCursorLock;
        private float saveAt, scanAt, refreshAt, logAt;
        private bool refreshNeeded;

        private void Awake()
        {
            try
            {
                Store = new ConfigStore();
                Current = Store.Load();
                gui = new SettingsGui(this);
                Status = Store.Status;
                initialized = true;
                // guiVisible deliberately stays false, regardless of saved settings.
            }
            catch (Exception e) { Logger.LogError("Monke Nameplates could not initialize: " + e); }
        }

        private void OnEnable()
        {
            Camera.onPreCull += PrepareCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
        }

        private void Update()
        {
            if (!initialized) return;
            if (CapturingKey)
            {
                string key = keyboard.Capture();
                if (key != null)
                { Current.toggleKey = key; CapturingKey = false; MarkDirty(); Status = "Toggle key changed to " + key + "."; }
            }
            else if ((!guiVisible || !gui.HasTextFocus) && keyboard.Pressed(Current.toggleKey)) SetGuiVisible(!guiVisible);

            if (guiVisible) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            float now = Time.unscaledTime;
            if (dirty && now >= saveAt) SaveNow();
            if (now >= scanAt)
            {
                scanAt = now + 1;
                try
                {
                    if (font == null) TryInitialFont();
                    SyncRigs();
                }
                catch (Exception e) { Report(e); }
            }
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            if (Time.unscaledTime < refreshAt && !refreshNeeded) return;
            refreshAt = Time.unscaledTime + 0.1f;
            refreshNeeded = false;
            foreach (var label in labels.Values)
            {
                try { label.Refresh(Current); }
                catch (Exception e) { label.Hide(); Report(e); }
            }
        }

        private void SyncRigs()
        {
            var rigs = game.FindRigs();
            stale.Clear();
            foreach (int id in labels.Keys) stale.Add(id);
            foreach (var rig in rigs)
            {
                if (!rig || GameBridge.IsLocal(rig) || !rig.gameObject.activeInHierarchy) continue;
                int id = rig.GetInstanceID();
                stale.Remove(id);
                if (font != null && !labels.ContainsKey(id))
                {
                    var label = new WorldLabel(rig, font);
                    labels.Add(id, label);
                    label.Refresh(Current);
                }
            }
            foreach (int id in stale) { labels[id].Dispose(); labels.Remove(id); }
        }

        private void TryInitialFont()
        {
            string path = string.IsNullOrEmpty(Current.fontFile) ? "" : Path.Combine(Store.FontsFolder, Current.fontFile);
            try { font = FontResource.Load(path); }
            catch when (!string.IsNullOrEmpty(path))
            {
                font = FontResource.Load("");
                Status = "Saved font could not load; using Default. Import a TTF/OTF font again.";
                Current.fontFile = "";
                MarkDirty();
            }
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera) => PrepareCamera(camera);
        private void PrepareCamera(Camera camera)
        {
            if (!initialized || !enabled || !camera) return;
            Camera main = Camera.main;
            foreach (var label in labels.Values)
            {
                try { label.PrepareCamera(camera, main, Current, visibility, game.LocalRig, game.LocalPhysicsRig); }
                catch (Exception e) { label.Hide(); Report(e); }
            }
        }

        private void OnGUI()
        {
            if (!initialized || !guiVisible) return;
            // Own window styles; preserve the game's GUI globals.
            Color previous = GUI.color;
            try { GUI.color = Color.white; gui.Draw(); }
            finally { GUI.color = previous; }
        }

        internal void SetGuiVisible(bool visible)
        {
            if (visible == guiVisible) return;
            if (visible)
            {
                oldCursorVisible = Cursor.visible;
                oldCursorLock = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = oldCursorVisible;
                Cursor.lockState = oldCursorLock;
                CapturingKey = false;
                if (dirty) SaveNow();
            }
            guiVisible = visible;
            gui?.ClearFocus();
        }

        internal void MarkDirty() { dirty = true; saveAt = Time.unscaledTime + 0.65f; refreshNeeded = true; }
        internal void SaveNow()
        {
            dirty = !Store.Save(Current);
            Status = Store.Status;
            saveAt = Time.unscaledTime + 5; // Retry a failed write without flooding the disk/log.
        }

        internal void ReloadConfig()
        {
            try
            {
                Settings next = Store.Load();
                // Load font first, so an invalid edit cannot destroy a working font.
                var replacement = FontResource.Load(string.IsNullOrEmpty(next.fontFile) ? "" : Path.Combine(Store.FontsFolder, next.fontFile));
                ReplaceFont(replacement);
                Current = next;
                dirty = false;
                refreshNeeded = true;
                CapturingKey = false;
                gui.ReloadFields();
                Status = Store.Status;
            }
            catch (Exception e) { Status = "Reload failed; current settings kept. " + e.GetBaseException().Message; }
        }

        internal void UseFont(string filename)
        {
            try
            {
                filename = Path.GetFileName(filename ?? "");
                var replacement = FontResource.Load(filename.Length == 0 ? "" : Path.Combine(Store.FontsFolder, filename));
                ReplaceFont(replacement);
                Current.fontFile = filename;
                MarkDirty();
                Status = "Font applied: " + (filename.Length == 0 ? "Default" : filename);
            }
            catch (Exception e) { Status = "Font was not changed: " + e.GetBaseException().Message; }
        }

        internal void ImportFont(string path)
        {
            try
            {
                path = Path.GetFullPath((path ?? "").Trim().Trim('"'));
                string ext = Path.GetExtension(path);
                if (!ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".otf", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Choose a .ttf or .otf font file.");
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0 || info.Length > 32 * 1024 * 1024)
                    throw new InvalidOperationException("Choose an existing font between 1 byte and 32 MB.");
                string filename = Path.GetFileName(path);
                string destination = Path.Combine(Store.FontsFolder, filename);
                if (!string.Equals(path, Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 2; File.Exists(destination); i++)
                    {
                        filename = Path.GetFileNameWithoutExtension(path) + "_" + i + ext;
                        destination = Path.Combine(Store.FontsFolder, filename);
                    }
                    // Validate with Unity before copying or changing the active font.
                    using (FontResource validated = FontResource.Load(path)) { }
                    File.Copy(path, destination, false);
                }
                UseFont(filename);
            }
            catch (Exception e) { Status = "Import failed: " + e.GetBaseException().Message; }
        }

        private void ReplaceFont(FontResource replacement)
        {
            // Destroy old text hierarchies, including TMP fallback submeshes, before
            // their atlases are released. Rebuild at the next scan.
            foreach (var label in labels.Values) { label.Hide(); label.Dispose(); }
            labels.Clear();
            var previous = font;
            font = replacement;
            previous?.Dispose();
            scanAt = 0;
        }

        private void Report(Exception e)
        {
            Status = e.GetBaseException().Message;
            if (Time.unscaledTime >= logAt) { logAt = Time.unscaledTime + 15; Logger.LogWarning(e); }
        }

        private void OnDisable()
        {
            Camera.onPreCull -= PrepareCamera;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            if (guiVisible) SetGuiVisible(false);
            foreach (var label in labels.Values) { label.Hide(); label.Dispose(); }
            labels.Clear();
            if (initialized && dirty) SaveNow();
        }

        private void OnDestroy()
        {
            font?.Dispose();
            gui?.Dispose();
            initialized = false;
        }
    }
}
