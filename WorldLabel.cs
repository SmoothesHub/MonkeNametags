using System;
using System.Collections.Generic;
using System.Reflection;
using MonkeNameplates.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MonkeNameplates
{
    internal sealed class WorldLabel : IDisposable
    {
        public readonly Component Rig;
        private readonly GameObject root;
        private readonly Component nameText;
        private readonly Component fpsText;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly Dictionary<Material, Material> fallbackMaterials = new Dictionary<Material, Material>();
        private readonly HashSet<Material> controlled = new HashSet<Material>();
        private FontResource font;
        private Transform head;
        private readonly MethodInfo forceMeshUpdate;
        private readonly object[] meshUpdateArgs = { false, false };
        private bool meshDirty = true;

        public WorldLabel(Component rig, FontResource font)
        {
            Rig = rig;
            root = new GameObject("MonkeNameplates label");
            root.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(root);
            // Start hidden. Only PrepareCamera may reveal the hierarchy.
            root.SetActive(false);
            try
            {
                forceMeshUpdate = font.TextType.GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) });
                if (forceMeshUpdate == null) throw new InvalidOperationException("Unsupported TextMeshPro mesh API; labels stay hidden.");
                nameText = CreateText("Name", font, 2.4f);
                fpsText = CreateText("FPS", font, 1.73f);
                ApplyFont(font);
            }
            catch { Object.Destroy(root); throw; }
        }

        private Component CreateText(string name, FontResource resource, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            int layer = LayerMask.NameToLayer("FirstPersonOnly");
            go.layer = layer < 0 ? 0 : layer;
            var text = go.AddComponent(resource.TextType);
            Reflect.Set(text, "richText", false);
            Reflect.Set(text, "isOverlay", false);
            Reflect.Set(text, "enableWordWrapping", false);
            Reflect.Set(text, "enableAutoSizing", false);
            Reflect.Set(text, "fontSize", size);
            Reflect.Set(text, "alignment", "Center");
            Reflect.Set(text, "overflowMode", "Truncate");
            var rect = go.GetComponent<RectTransform>();
            if (rect) rect.sizeDelta = new Vector2(3f, 0.6f);
            return text;
        }

        public void ApplyFont(FontResource resource)
        {
            Hide();
            font = resource;
            foreach (Component text in new[] { nameText, fpsText })
            {
                Reflect.Set(text, "font", font.Asset);
                Reflect.Set(text, "fontSharedMaterial", font.Material);
            }
            foreach (var material in fallbackMaterials.Values) if (material) Object.Destroy(material);
            fallbackMaterials.Clear();
            controlled.Clear();
            controlled.Add(font.Material);
            meshDirty = true;
        }

        public void Refresh(Settings settings)
        {
            if (!Rig || !Rig.gameObject.activeInHierarchy) { Hide(); return; }
            head = GameBridge.Head(Rig);
            Reflect.Set(nameText, "text", Rules.PlainName(GameBridge.Name(Rig)));
            ColorUtility.TryParseHtmlString(Rules.TaggedHex, out Color orange);
            Reflect.Set(nameText, "color", GameBridge.Tagged(Rig) ? orange : GameBridge.PlayerColor(Rig));
            int? fps = GameBridge.ReportedFps(Rig);
            Reflect.Set(fpsText, "text", fps.HasValue ? fps.Value + " FPS" : "FPS --");
            ColorUtility.TryParseHtmlString(Rules.FpsHex(fps), out Color fpsColor);
            Reflect.Set(fpsText, "color", fpsColor);
            Reflect.Set(fpsText, "fontSize", 2.4f * settings.fpsScale);
            fpsText.gameObject.SetActive(settings.showFps);
            meshDirty = true;
        }

        public void PrepareCamera(Camera camera, Camera mainCamera, Settings settings,
            Visibility visibility, Transform localRig, Transform localPhysicsRig)
        {
            if (!settings.enabled || !Rig || !head || !Rig.gameObject.activeInHierarchy ||
                camera.cameraType != CameraType.Game || (camera != mainCamera && !camera.stereoEnabled))
            { Hide(); return; }
            Vector3 headPosition = head.position;
            Vector3 viewport = camera.WorldToViewportPoint(headPosition);
            if (viewport.z <= camera.nearClipPlane || viewport.x < 0 || viewport.x > 1 ||
                viewport.y < 0 || viewport.y > 1 ||
                Vector3.Distance(camera.transform.position, headPosition) > settings.maxDistance)
            { Hide(); return; }

            Vector3 position = headPosition + Rig.transform.right * settings.offsetX +
                Vector3.up * settings.offsetY + Rig.transform.forward * settings.offsetZ;
            root.transform.SetPositionAndRotation(position, camera.transform.rotation);
            root.transform.localScale = Vector3.one * settings.size;
            fpsText.transform.localPosition = new Vector3(0, -settings.fpsGap, 0);
            if (!visibility.CanSee(camera, headPosition, position, fpsText.transform.position,
                Rig.transform, localRig, localPhysicsRig, settings.showFps)) { Hide(); return; }

            bool wasHidden = !root.activeSelf;
            root.SetActive(true);
            if (wasHidden || meshDirty)
            {
                // Build fallback submeshes BEFORE inspecting their renderers, so
                // no newly created glyph gets an unchecked first rendering frame.
                forceMeshUpdate.Invoke(nameText, meshUpdateArgs);
                if (settings.showFps) forceMeshUpdate.Invoke(fpsText, meshUpdateArgs);
                meshDirty = false;
            }
            // Include TMP submeshes (e.g. fallback Unicode glyphs), not just main text.
            // Never modify a game/shared fallback material in place.
            root.GetComponentsInChildren(false, renderers);
            foreach (Renderer renderer in renderers)
            {
                Material material = renderer.sharedMaterial;
                if (!material) { Hide(); return; }
                if (controlled.Contains(material)) continue;
                if (!fallbackMaterials.TryGetValue(material, out var copy))
                {
                    copy = new Material(material);
                    FontResource.ForceDepth(copy, font.Material.shader);
                    fallbackMaterials[material] = copy;
                    controlled.Add(copy);
                }
                renderer.sharedMaterial = copy;
            }
        }

        public void Hide() { if (root && root.activeSelf) root.SetActive(false); }

        public void Dispose()
        {
            if (root) Object.Destroy(root);
            foreach (var material in fallbackMaterials.Values) if (material) Object.Destroy(material);
        }
    }
}
