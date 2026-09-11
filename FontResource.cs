using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MonkeNameplates
{
    internal sealed class FontResource : IDisposable
    {
        public Object Asset { get; private set; }
        public Material Material { get; private set; }
        public Type TextType { get; private set; }
        private Font source;
        private bool ownsAsset;

        public static FontResource Load(string path)
        {
            var resource = new FontResource();
            try
            {
                resource.TextType = Reflect.FindType("TMPro.TextMeshPro");
                Type assetType = Reflect.FindType("TMPro.TMP_FontAsset");
                if (assetType == null || resource.TextType == null)
                    throw new InvalidOperationException("Waiting for the game's TextMeshPro assembly.");

                if (!string.IsNullOrEmpty(path))
                {
                    if (!File.Exists(path)) throw new FileNotFoundException("Font file not found.", path);
                    // Unity 2021/2022 Font(string) recognizes an absolute file path.
                    resource.source = new Font(Path.GetFullPath(path));
                }
                else resource.source = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Liberation Sans" }, 90);

                if (resource.source && resource.source.dynamic)
                {
                    var method = assetType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .Where(m => m.Name == "CreateFontAsset")
                        .Where(m => m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(Font))
                        .OrderBy(m => m.GetParameters().Length)
                        .FirstOrDefault(m => m.GetParameters().Skip(1).All(p => p.IsOptional));
                    if (method != null)
                    {
                        var parameters = method.GetParameters();
                        object[] args = parameters.Select(p => p.DefaultValue).ToArray();
                        args[0] = resource.source;
                        resource.Asset = method.Invoke(null, args) as Object;
                        resource.ownsAsset = resource.Asset;
                    }
                }
                if (!resource.Asset && !string.IsNullOrEmpty(path))
                    throw new InvalidOperationException("Unity could not load this font. Try a static TTF or OTF font.");
                if (!resource.Asset)
                    resource.Asset = Reflect.Get(Reflect.FindType("TMPro.TMP_Settings"), "defaultFontAsset") as Object;
                if (!resource.Asset) throw new InvalidOperationException("No usable TextMeshPro font is available yet.");

                var original = Reflect.Get(resource.Asset, "material") as Material;
                if (!original) throw new InvalidOperationException("Font material is missing.");
                resource.Material = new Material(original) { name = "MonkeNameplates depth-tested text" };
                Shader shader = Shader.Find("TextMeshPro/Distance Field") ?? Shader.Find("TextMeshPro/Mobile/Distance Field");
                if (!shader) throw new InvalidOperationException("A depth-tested TextMeshPro shader is missing; labels stay hidden.");
                ForceDepth(resource.Material, shader);
                return resource;
            }
            catch { resource.Dispose(); throw; }
        }

        internal static void ForceDepth(Material material, Shader shader)
        {
            if (!shader || (shader.name != "TextMeshPro/Distance Field" &&
                shader.name != "TextMeshPro/Mobile/Distance Field"))
                throw new InvalidOperationException("Unsupported text shader; labels stay hidden.");
            material.shader = shader;
            // TMP 3.0.6's official TMP_SDF and TMP_SDF-Mobile shaders use:
            //     ZTest [unity_GUIZTestMode]
            // This render-state input is not declared in the Properties block, so
            // HasProperty("_ZTestMode") is NOT a valid capability check. Match
            // TMPro's own non-overlay SetShaderDepth implementation (value 4).
            material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.LessEqual);
            // Also support shader revisions that expose this alternate property.
            if (material.HasProperty("_ZTestMode"))
                material.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        public void Dispose()
        {
            if (Material) Object.Destroy(Material);
            if (ownsAsset && Asset)
            {
                var textures = Reflect.Get(Asset, "atlasTextures") as Texture2D[];
                if (textures != null) foreach (var texture in textures) if (texture) Object.Destroy(texture);
                var original = Reflect.Get(Asset, "material") as Material;
                if (original) Object.Destroy(original);
                Object.Destroy(Asset);
            }
            if (source) Object.Destroy(source);
            Material = null;
            Asset = null;
            source = null;
        }
    }
}
