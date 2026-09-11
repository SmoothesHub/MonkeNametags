using System;
using System.IO;
using System.Text;
using BepInEx;
using MonkeNameplates.Core;
using UnityEngine;

namespace MonkeNameplates
{
    internal sealed class ConfigStore
    {
        public readonly string Folder = Path.Combine(Paths.ConfigPath, "MonkeNameplates");
        public string FontsFolder => Path.Combine(Folder, "Fonts");
        public string FilePath => Path.Combine(Folder, "settings.json");
        public string Status { get; private set; } = "";

        public Settings Load()
        {
            Directory.CreateDirectory(FontsFolder);
            var settings = new Settings();
            if (!File.Exists(FilePath)) { Save(settings); return settings; }
            try
            {
                string json = File.ReadAllText(FilePath);
                if (!json.TrimStart().StartsWith("{")) throw new FormatException("Expected a JSON object.");
                // Overwrite preserves default field values when older configs omit fields.
                JsonUtility.FromJsonOverwrite(json, settings);
                settings.Normalize();
                Status = "Settings loaded.";
            }
            catch (Exception e)
            {
                string backup = FilePath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
                File.Copy(FilePath, backup, false);
                settings = new Settings();
                Status = "Invalid config preserved as .invalid backup; using defaults. " + e.Message;
            }
            return settings;
        }

        public bool Save(Settings settings)
        {
            try
            {
                Directory.CreateDirectory(FontsFolder);
                settings.Normalize();
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(settings, true), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                else File.Move(temp, FilePath);
                Status = "Settings saved.";
                return true;
            }
            catch (Exception e) { Status = "Could not save settings: " + e.Message; return false; }
        }
    }
}
