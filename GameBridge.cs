using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonkeNameplates
{
    internal sealed class GameBridge
    {
        public Type RigType { get; private set; }
        public Transform LocalRig { get; private set; }
        public Transform LocalPhysicsRig { get; private set; }

        public Component[] FindRigs()
        {
            RigType = RigType ?? Reflect.FindType("VRRig");
            if (RigType == null) return Array.Empty<Component>();
            var objects = Object.FindObjectsOfType(RigType);
            var rigs = new Component[objects.Length];
            LocalRig = null;
            // The local head/body colliders belong to the locomotion player, which
            // can be a separate hierarchy from the offline VRRig render model.
            LocalPhysicsRig = TransformOf(Reflect.Get(Reflect.FindType("GorillaLocomotion.GTPlayer"), "Instance")) ??
                TransformOf(Reflect.Get(Reflect.FindType("GorillaLocomotion.Player"), "Instance"));
            for (int i = 0; i < objects.Length; i++)
            {
                rigs[i] = objects[i] as Component;
                if (rigs[i] && IsLocal(rigs[i])) LocalRig = rigs[i].transform;
            }
            return rigs;
        }

        public static bool IsLocal(Component rig)
        {
            if (Reflect.Get(rig, "isOfflineVRRig") is bool offline && offline) return true;
            if (Reflect.Get(rig, "isMyPlayer") is bool mine && mine) return true;
            return Reflect.Get(Reflect.Get(rig, "Creator"), "IsLocal") is bool local && local;
        }

        private static Transform TransformOf(object value)
        {
            if (value is Transform t) return t;
            if (value is Component c) return c.transform;
            if (value is GameObject go) return go.transform;
            return null;
        }

        public static Transform Head(Component rig) =>
            TransformOf(Reflect.Get(Reflect.Get(rig, "head"), "rigTarget")) ??
            TransformOf(Reflect.Get(rig, "headMesh"));

        public static string Name(Component rig)
        {
            // Prefer the game's displayed/sanitized name.
            return Reflect.Get(Reflect.Get(rig, "playerText1"), "text") as string ??
                Reflect.Get(Reflect.Get(rig, "Creator"), "NickName") as string ?? "MONKE";
        }

        public static Color PlayerColor(Component rig)
        {
            if (Reflect.Get(rig, "playerColor") is Color color)
                return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1);
            return Color.white;
        }

        public static bool Tagged(Component rig)
        {
            // Vanilla VRRig's tag/infection skin slots: 1 = infected/lava, 2 = it/rock.
            // Do not interpret unrelated minigame effects or arbitrary nonzero skins as tags.
            object value = Reflect.Get(rig, "currentMaterialIndex");
            if (value is int index) return index == 1 || index == 2;
            return false;
        }

        public static int? ReportedFps(Component rig)
        {
            // The game already synchronizes this member. Do not substitute local FPS,
            // Photon send rate, packet rate, refresh rate, or an invented estimate.
            object value = Reflect.Get(rig, "fps");
            if (value == null) return null;
            try
            {
                int fps = Convert.ToInt32(value);
                return fps > 0 ? fps : (int?)null; // zero/uninitialized = unavailable
            }
            catch (Exception) { return null; }
        }
    }
}
