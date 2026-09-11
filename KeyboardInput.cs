using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonkeNameplates
{
    internal sealed class KeyboardInput
    {
        public const string Keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private Type keyboardType;
        private bool legacyAvailable = true;
        private readonly Dictionary<char, KeyCode> legacyCodes = new Dictionary<char, KeyCode>();

        public bool Pressed(string key)
        {
            if (key == null || key.Length != 1 || Keys.IndexOf(key[0]) < 0) return false;
            keyboardType = keyboardType ?? Reflect.FindType("UnityEngine.InputSystem.Keyboard");
            var keyboard = Reflect.Get(keyboardType, "current");
            if (keyboard != null)
            {
                char c = key[0];
                string member = char.IsDigit(c) ? "digit" + c + "Key" : char.ToLowerInvariant(c) + "Key";
                var button = Reflect.Get(keyboard, member);
                return Reflect.Get(button, "wasPressedThisFrame") is bool pressed && pressed;
            }
            if (!legacyAvailable) return false;
            try
            {
                char c = key[0];
                if (!legacyCodes.TryGetValue(c, out KeyCode code))
                {
                    code = (KeyCode)Enum.Parse(typeof(KeyCode), char.IsDigit(c) ? "Alpha" + c : c.ToString());
                    legacyCodes[c] = code;
                }
                return Input.GetKeyDown(code);
            }
            catch (InvalidOperationException) { legacyAvailable = false; return false; }
        }

        public string Capture()
        {
            foreach (char c in Keys) if (Pressed(c.ToString())) return c.ToString();
            return null;
        }
    }
}
