using System.IO;
using UnityEditor;
using UnityEngine;

namespace CocoonPrototype.Editor
{
    [InitializeOnLoad]
    public static class CocoonReferenceMaterialCreator
    {
        private const string LegacyMaterialFolder = "Assets/CocoonPrototype/Materials/Reference";
        private const string MaterialFolder = "Assets/CocoonPrototype/Materials/ReferencePBR";
        private const string TextureFolder = MaterialFolder + "/GeneratedTextures";
        private const int TextureSize = 2048;

        private enum MaterialKind
        {
            BlackGlossy,
            BlackMatte,
            GreyMattePlastic,
            WhiteMattePlasticGoldLines,
            WhiteCocoonGoldFibers,
            MetalGlossy,
            GreyFabric,
            BlueMetallicShell
        }

        static CocoonReferenceMaterialCreator()
        {
            EditorApplication.delayCall += CreateMissingReferenceMaterials;
        }

        [MenuItem("Cocoon/Create Missing Reference PBR Materials")]
        public static void CreateMissingReferenceMaterials()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureFolders();
            CreateMaterialIfMissing("CocoonRef_Black_Glossy", MaterialKind.BlackGlossy);
            CreateMaterialIfMissing("CocoonRef_Black_Matte", MaterialKind.BlackMatte);
            CreateMaterialIfMissing("CocoonRef_Grey_Matte_Plastic", MaterialKind.GreyMattePlastic);
            CreateMaterialIfMissing("CocoonRef_White_Matte_Plastic_GoldLines", MaterialKind.WhiteMattePlasticGoldLines);
            CreateMaterialIfMissing("CocoonRef_White_Cocoon_GoldFibers", MaterialKind.WhiteCocoonGoldFibers);
            CreateMaterialIfMissing("CocoonRef_Metal_Glossy", MaterialKind.MetalGlossy);
            CreateMaterialIfMissing("CocoonRef_Grey_Fabric", MaterialKind.GreyFabric);
            CreateMaterialIfMissing("CocoonRef_Blue_Metallic_Shell", MaterialKind.BlueMetallicShell);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Cocoon/Rebuild Reference PBR Materials")]
        public static void RebuildReferenceMaterials()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureFolders();
            CreateOrReplaceMaterial("CocoonRef_Black_Glossy", MaterialKind.BlackGlossy);
            CreateOrReplaceMaterial("CocoonRef_Black_Matte", MaterialKind.BlackMatte);
            CreateOrReplaceMaterial("CocoonRef_Grey_Matte_Plastic", MaterialKind.GreyMattePlastic);
            CreateOrReplaceMaterial("CocoonRef_White_Matte_Plastic_GoldLines", MaterialKind.WhiteMattePlasticGoldLines);
            CreateOrReplaceMaterial("CocoonRef_White_Cocoon_GoldFibers", MaterialKind.WhiteCocoonGoldFibers);
            CreateOrReplaceMaterial("CocoonRef_Metal_Glossy", MaterialKind.MetalGlossy);
            CreateOrReplaceMaterial("CocoonRef_Grey_Fabric", MaterialKind.GreyFabric);
            CreateOrReplaceMaterial("CocoonRef_Blue_Metallic_Shell", MaterialKind.BlueMetallicShell);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Cocoon/Rebuild White Cocoon Gold Fiber Materials")]
        public static void RebuildWhiteCocoonGoldFiberMaterials()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureFolders();
            CreateOrReplaceMaterial("CocoonRef_White_Matte_Plastic_GoldLines", MaterialKind.WhiteMattePlasticGoldLines);
            CreateOrReplaceMaterial("CocoonRef_White_Cocoon_GoldFibers", MaterialKind.WhiteCocoonGoldFibers);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Cocoon/Create Reference Materials")]
        public static void EnsureLegacyReferenceMaterials()
        {
            // Keep the old menu entry useful, but route new work to the PBR set.
            CreateMissingReferenceMaterials();
        }

        private static void CreateMaterialIfMissing(string materialName, MaterialKind kind)
        {
            string path = MaterialFolder + "/" + materialName + ".mat";
            if (File.Exists(path))
            {
                return;
            }

            CreateOrReplaceMaterial(materialName, kind);
        }

        private static void CreateOrReplaceMaterial(string materialName, MaterialKind kind)
        {
            Material material = LoadOrCreateMaterial(materialName);
            Texture2D albedo = CreateOrReplaceTexture(materialName + "_Albedo", kind, TextureRole.Albedo);
            Texture2D normal = CreateOrReplaceTexture(materialName + "_Normal", kind, TextureRole.Normal);
            Texture2D mask = CreateOrReplaceTexture(materialName + "_Mask", kind, TextureRole.Mask);
            Texture2D emission = CreateOrReplaceTexture(materialName + "_Emission", kind, TextureRole.Emission);
            ApplyMaterialSettings(material, kind, albedo, normal, mask, emission);
            if (material != null)
            {
                EditorUtility.SetDirty(material);
            }
        }

        private enum TextureRole
        {
            Albedo,
            Normal,
            Mask,
            Emission
        }

        private static Material LoadOrCreateMaterial(string materialName)
        {
            string path = MaterialFolder + "/" + materialName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogWarning("Cocoon reference PBR material creation skipped because no usable shader was found.");
                return null;
            }

            material = new Material(shader)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D CreateOrReplaceTexture(string textureName, MaterialKind kind, TextureRole role)
        {
            string path = TextureFolder + "/" + textureName + ".asset";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true, role != TextureRole.Albedo && role != TextureRole.Emission)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 12
            };
            AssetDatabase.CreateAsset(texture, path);

            Color[] pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = x / (float)(TextureSize - 1);
                    float v = y / (float)(TextureSize - 1);
                    pixels[y * TextureSize + x] = EvaluateTexture(kind, role, u, v);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Color EvaluateTexture(MaterialKind kind, TextureRole role, float u, float v)
        {
            float noise = FractalNoise(u * 32f, v * 32f);
            switch (role)
            {
                case TextureRole.Normal:
                    return EvaluateNormal(kind, u, v);
                case TextureRole.Mask:
                    return EvaluateMask(kind, u, v, noise);
                case TextureRole.Emission:
                    return EvaluateEmission(kind, u, v, noise);
                default:
                    return EvaluateAlbedo(kind, u, v, noise);
            }
        }

        private static Color EvaluateAlbedo(MaterialKind kind, float u, float v, float noise)
        {
            switch (kind)
            {
                case MaterialKind.BlackGlossy:
                    return Tone(new Color(0.005f, 0.006f, 0.006f), noise, 0.016f);
                case MaterialKind.BlackMatte:
                    return Tone(new Color(0.012f, 0.013f, 0.012f), noise, 0.024f);
                case MaterialKind.GreyMattePlastic:
                    return Tone(new Color(0.47f, 0.49f, 0.48f), noise, 0.07f);
                case MaterialKind.WhiteMattePlasticGoldLines:
                case MaterialKind.WhiteCocoonGoldFibers:
                    return EvaluateWhiteGoldAlbedo(u, v, noise);
                case MaterialKind.MetalGlossy:
                    return EvaluateBrushedMetalAlbedo(u, v, noise);
                case MaterialKind.GreyFabric:
                    return EvaluateFabricAlbedo(u, v, noise);
                case MaterialKind.BlueMetallicShell:
                    return EvaluateBlueShellAlbedo(u, v, noise);
                default:
                    return Color.white;
            }
        }

        private static Color EvaluateWhiteGoldAlbedo(float u, float v, float noise)
        {
            EvaluateCocoonFiberFields(u, v, out float fiber, out float gold, out float height);
            Color warmShadow = new Color(0.58f, 0.51f, 0.40f);
            Color ivory = Tone(new Color(0.86f, 0.80f, 0.66f), noise, 0.13f);
            Color milkyFiber = new Color(1.0f, 0.94f, 0.78f);
            Color goldThread = new Color(1.0f, 0.70f, 0.25f);

            Color baseColor = Color.Lerp(warmShadow, ivory, 0.72f + noise * 0.18f);
            baseColor = Color.Lerp(baseColor, milkyFiber, Mathf.Clamp01(fiber * 0.62f + height * 0.18f));
            return Color.Lerp(baseColor, goldThread, Mathf.Clamp01(gold * 0.92f));
        }

        private static void EvaluateCocoonFiberFields(float u, float v, out float fiber, out float gold, out float height)
        {
            Vector2 p = new Vector2(u, v);
            fiber = 0f;
            gold = 0f;

            for (int i = 0; i < 14; i++)
            {
                float seed = i * 19.371f + 3.7f;
                float angle = Mathf.Lerp(-0.95f, 0.95f, Hash01(seed + 1.2f));
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                Vector2 center = new Vector2(Hash01(seed + 4.1f), Hash01(seed + 8.9f));
                float along = Vector2.Dot(p - center, dir);
                float endMask = Mathf.SmoothStep(-0.62f, -0.46f, along) * (1f - Mathf.SmoothStep(0.46f, 0.62f, along));
                float wave = Mathf.Sin(along * Mathf.Lerp(11f, 28f, Hash01(seed + 2.6f)) + seed) * Mathf.Lerp(0.004f, 0.019f, Hash01(seed + 5.5f));
                float distance = Mathf.Abs(Vector2.Dot(p - center, normal) + wave);
                float width = Mathf.Lerp(0.0022f, 0.0085f, Hash01(seed + 7.4f));
                float strand = Mathf.Exp(-(distance * distance) / Mathf.Max(0.000001f, width * width)) * endMask;
                fiber = Mathf.Max(fiber, strand * Mathf.Lerp(0.42f, 0.95f, Hash01(seed + 11.1f)));
            }

            for (int i = 0; i < 6; i++)
            {
                float seed = i * 31.17f + 81.4f;
                float angle = Mathf.Lerp(-0.65f, 0.65f, Hash01(seed + 1.2f));
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                Vector2 center = new Vector2(Hash01(seed + 4.1f), Hash01(seed + 8.9f));
                float along = Vector2.Dot(p - center, dir);
                float endMask = Mathf.SmoothStep(-0.58f, -0.44f, along) * (1f - Mathf.SmoothStep(0.44f, 0.58f, along));
                float wave = Mathf.Sin(along * Mathf.Lerp(9f, 19f, Hash01(seed + 2.6f)) + seed) * Mathf.Lerp(0.002f, 0.012f, Hash01(seed + 5.5f));
                float distance = Mathf.Abs(Vector2.Dot(p - center, normal) + wave);
                float strand = Mathf.Exp(-(distance * distance) / 0.000018f) * endMask;
                gold = Mathf.Max(gold, strand);
            }

            fiber = Mathf.Clamp01(fiber);
            gold = Mathf.Clamp01(gold);
            height = Mathf.Clamp01(fiber * 0.62f + gold * 0.95f + FractalNoise(u * 72f + 7.1f, v * 72f - 3.4f) * 0.12f);
        }

        private static Color EvaluateBrushedMetalAlbedo(float u, float v, float noise)
        {
            float brush = Mathf.Sin(v * 720f + noise * 8f) * 0.5f + 0.5f;
            return new Color(0.58f, 0.58f, 0.56f) * (0.82f + brush * 0.16f);
        }

        private static Color EvaluateFabricAlbedo(float u, float v, float noise)
        {
            float warp = Mathf.Sin(u * 190f) * 0.5f + 0.5f;
            float weft = Mathf.Sin(v * 210f) * 0.5f + 0.5f;
            float weave = warp * 0.08f + weft * 0.08f + noise * 0.06f;
            return new Color(0.37f, 0.38f, 0.365f) * (0.88f + weave);
        }

        private static Color EvaluateBlueShellAlbedo(float u, float v, float noise)
        {
            float flake = Mathf.Pow(Mathf.Max(0f, FractalNoise(u * 90f + 13.1f, v * 90f - 7.4f)), 6f);
            Color baseColor = new Color(0.16f, 0.48f, 0.82f);
            Color highlight = new Color(0.55f, 0.86f, 1f);
            return Color.Lerp(Tone(baseColor, noise, 0.08f), highlight, flake * 0.65f);
        }

        private static Color EvaluateNormal(MaterialKind kind, float u, float v)
        {
            if (IsCocoonGoldFiberKind(kind))
            {
                float cocoonHL = EvaluateCocoonHeight(u - 0.0018f, v);
                float cocoonHR = EvaluateCocoonHeight(u + 0.0018f, v);
                float cocoonHD = EvaluateCocoonHeight(u, v - 0.0018f);
                float cocoonHU = EvaluateCocoonHeight(u, v + 0.0018f);
                Vector3 cocoonNormal = new Vector3((cocoonHL - cocoonHR) * 0.56f, (cocoonHD - cocoonHU) * 0.56f, 1f).normalized;
                return new Color(cocoonNormal.x * 0.5f + 0.5f, cocoonNormal.y * 0.5f + 0.5f, cocoonNormal.z * 0.5f + 0.5f, 1f);
            }

            float scale = kind == MaterialKind.GreyFabric ? 110f : kind == MaterialKind.MetalGlossy ? 190f : 42f;
            float strength = kind == MaterialKind.GreyFabric ? 0.22f : kind == MaterialKind.MetalGlossy ? 0.08f : 0.055f;
            if (kind == MaterialKind.BlackGlossy || kind == MaterialKind.BlueMetallicShell)
            {
                strength = 0.025f;
            }

            float noiseHeightLeft = FractalNoise((u - 0.002f) * scale, v * scale);
            float noiseHeightRight = FractalNoise((u + 0.002f) * scale, v * scale);
            float noiseHeightDown = FractalNoise(u * scale, (v - 0.002f) * scale);
            float noiseHeightUp = FractalNoise(u * scale, (v + 0.002f) * scale);
            Vector3 n = new Vector3((noiseHeightLeft - noiseHeightRight) * strength, (noiseHeightDown - noiseHeightUp) * strength, 1f).normalized;
            return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
        }

        private static Color EvaluateMask(MaterialKind kind, float u, float v, float noise)
        {
            float metallic = 0f;
            float occlusion = 1f;
            float smoothness;
            switch (kind)
            {
                case MaterialKind.BlackGlossy:
                    smoothness = 0.94f;
                    break;
                case MaterialKind.BlackMatte:
                    smoothness = 0.16f;
                    break;
                case MaterialKind.GreyMattePlastic:
                    smoothness = 0.34f + noise * 0.04f;
                    break;
                case MaterialKind.WhiteMattePlasticGoldLines:
                case MaterialKind.WhiteCocoonGoldFibers:
                    EvaluateCocoonFiberFields(u, v, out float fiber, out float gold, out _);
                    metallic = Mathf.Clamp01(gold * 0.88f);
                    occlusion = Mathf.Clamp01(0.82f + noise * 0.08f + fiber * 0.08f);
                    smoothness = Mathf.Lerp(0.31f, 0.82f, gold);
                    break;
                case MaterialKind.MetalGlossy:
                    metallic = 1f;
                    smoothness = 0.86f;
                    break;
                case MaterialKind.GreyFabric:
                    smoothness = 0.24f;
                    occlusion = 0.86f + noise * 0.1f;
                    break;
                case MaterialKind.BlueMetallicShell:
                    metallic = 0.72f;
                    smoothness = 0.78f + noise * 0.06f;
                    break;
                default:
                    smoothness = 0.5f;
                    break;
            }

            return new Color(metallic, occlusion, 1f, Mathf.Clamp01(smoothness));
        }

        private static Color EvaluateEmission(MaterialKind kind, float u, float v, float noise)
        {
            if (!IsCocoonGoldFiberKind(kind))
            {
                return Color.black;
            }

            EvaluateCocoonFiberFields(u, v, out float fiber, out float gold, out _);
            float innerGlow = Mathf.Clamp01(0.08f + noise * 0.12f + fiber * 0.12f + gold * 0.2f);
            return new Color(1.0f, 0.72f, 0.34f, 1f) * innerGlow * 0.22f;
        }

        private static float EvaluateCocoonHeight(float u, float v)
        {
            EvaluateCocoonFiberFields(Mathf.Repeat(u, 1f), Mathf.Repeat(v, 1f), out _, out _, out float height);
            return height;
        }

        private static bool IsCocoonGoldFiberKind(MaterialKind kind)
        {
            return kind == MaterialKind.WhiteMattePlasticGoldLines || kind == MaterialKind.WhiteCocoonGoldFibers;
        }

        private static void ApplyMaterialSettings(Material material, MaterialKind kind, Texture2D albedo, Texture2D normal, Texture2D mask, Texture2D emission)
        {
            if (material == null)
            {
                return;
            }

            SetTexture(material, "_BaseMap", albedo);
            SetTexture(material, "_MainTex", albedo);
            SetTexture(material, "_BumpMap", normal);
            SetTexture(material, "_MetallicGlossMap", mask);
            SetTexture(material, "_MaskMap", mask);
            SetTexture(material, "_EmissionMap", emission);
            SetColor(material, "_BaseColor", Color.white);
            SetColor(material, "_Color", Color.white);
            SetColor(material, "_EmissionColor", IsCocoonGoldFiberKind(kind) ? new Color(1f, 0.72f, 0.35f, 1f) * 0.22f : Color.black);
            SetFloat(material, "_BumpScale", kind == MaterialKind.GreyFabric ? 0.85f : IsCocoonGoldFiberKind(kind) ? 0.62f : 0.38f);
            SetFloat(material, "_Metallic", DefaultMetallic(kind));
            SetFloat(material, "_Smoothness", DefaultSmoothness(kind));
            SetFloat(material, "_Glossiness", DefaultSmoothness(kind));
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_ZWrite", 1f);
            EnableKeyword(material, "_NORMALMAP");
            EnableKeyword(material, "_METALLICSPECGLOSSMAP");
            if (IsCocoonGoldFiberKind(kind))
            {
                EnableKeyword(material, "_EMISSION");
            }
            material.renderQueue = -1;
        }

        private static float DefaultMetallic(MaterialKind kind)
        {
            if (kind == MaterialKind.MetalGlossy)
            {
                return 1f;
            }

            if (kind == MaterialKind.BlueMetallicShell)
            {
                return 0.72f;
            }

            return 0f;
        }

        private static float DefaultSmoothness(MaterialKind kind)
        {
            switch (kind)
            {
                case MaterialKind.BlackGlossy:
                    return 0.94f;
                case MaterialKind.BlackMatte:
                    return 0.16f;
                case MaterialKind.GreyMattePlastic:
                    return 0.36f;
                case MaterialKind.WhiteMattePlasticGoldLines:
                case MaterialKind.WhiteCocoonGoldFibers:
                    return 0.38f;
                case MaterialKind.MetalGlossy:
                    return 0.86f;
                case MaterialKind.GreyFabric:
                    return 0.24f;
                case MaterialKind.BlueMetallicShell:
                    return 0.8f;
                default:
                    return 0.5f;
            }
        }

        private static Color Tone(Color baseColor, float noise, float amount)
        {
            return baseColor * (1f - amount * 0.5f + noise * amount);
        }

        private static float Hash01(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
        }

        private static float FractalNoise(float x, float y)
        {
            float value = 0f;
            float amplitude = 0.5f;
            float frequency = 1f;
            for (int i = 0; i < 4; i++)
            {
                value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
                frequency *= 2.03f;
                amplitude *= 0.5f;
            }

            return Mathf.Clamp01(value);
        }

        private static void SetTexture(Material material, string propertyName, Texture texture)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetColor(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloat(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void EnableKeyword(Material material, string keyword)
        {
            if (material != null && !material.IsKeywordEnabled(keyword))
            {
                material.EnableKeyword(keyword);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/CocoonPrototype/Materials");
            EnsureFolder(LegacyMaterialFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string normalized = folder.Replace("\\", "/");
            string parent = Path.GetDirectoryName(normalized);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent.Replace("\\", "/"));
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(normalized));
        }
    }
}
