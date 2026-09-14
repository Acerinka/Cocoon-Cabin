using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CocoonPrototype.Editor
{
    public sealed class CocoonFinalTaxiModelImportSettings : AssetPostprocessor
    {
        private const string TaxiObjPath = "Assets/ImportedAssets/CocoonTaxi/ZOOX.obj";
        private const string TaxiMtlPath = "Assets/ImportedAssets/CocoonTaxi/ZOOX.mtl";
        private const string SolidObjPath = "Assets/ImportedAssets/CocoonTaxi/solid.obj";
        private const string SeatV2ObjPath = "Assets/ImportedAssets/CocoonTaxi/SeatV2/seat-v2.obj";
        private const string SeatV2LatestObjPath = "Assets/ImportedAssets/CocoonTaxi/SeatV2/seat_obj.obj";
        private static readonly string[] ImportedObjPaths =
        {
            TaxiObjPath,
            SolidObjPath,
            "Assets/ImportedAssets/CocoonTaxi/bag.obj",
            "Assets/ImportedAssets/CocoonTaxi/seat.obj",
            "Assets/ImportedAssets/CocoonTaxi/wheelchair.obj",
            SeatV2ObjPath,
            SeatV2LatestObjPath
        };

        private static readonly string[] ImportedMtlPaths =
        {
            TaxiMtlPath,
            "Assets/ImportedAssets/CocoonTaxi/solid.mtl",
            "Assets/ImportedAssets/CocoonTaxi/bag.mtl",
            "Assets/ImportedAssets/CocoonTaxi/seat.mtl",
            "Assets/ImportedAssets/CocoonTaxi/SeatV2/seat-v2.mtl",
            "Assets/ImportedAssets/CocoonTaxi/SeatV2/seat_obj.mtl"
        };

        private void OnPreprocessModel()
        {
            if (!IsImportedCocoonModel(assetPath))
            {
                return;
            }

            var importer = assetImporter as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ApplyFinalTaxiObjImportSettings(importer, assetPath);
        }

        private void OnPostprocessModel(GameObject importedRoot)
        {
            if (!IsImportedCocoonModel(assetPath))
            {
                return;
            }

            ApplyImportedMaterialStyles(importedRoot, assetPath);
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!ContainsAnyPath(importedAssets, ImportedObjPaths) && !ContainsAnyPath(importedAssets, ImportedMtlPaths))
            {
                return;
            }

            EditorApplication.delayCall += CocoonLiveSceneReadabilityRepair.ApplyToOpenScene;
        }

        [MenuItem("Cocoon/Reimport ZOOX Taxi OBJ With Materials")]
        public static void ReimportZooXTaxiObj()
        {
            var importer = AssetImporter.GetAtPath(TaxiObjPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("Cocoon ZOOX taxi OBJ not found at " + TaxiObjPath + ".");
                return;
            }

            ApplyFinalTaxiObjImportSettings(importer, TaxiObjPath);
            importer.SaveAndReimport();
            Debug.Log("Cocoon ZOOX taxi OBJ reimported with MTL color-classified material settings.");
        }

        [MenuItem("Cocoon/Reimport All Cocoon Taxi OBJ Assets")]
        public static void ReimportAllCocoonTaxiObjAssets()
        {
            int importedCount = 0;
            for (int i = 0; i < ImportedObjPaths.Length; i++)
            {
                string modelPath = ImportedObjPaths[i];
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer == null)
                {
                    if (File.Exists(ToProjectAbsolutePath(modelPath)))
                    {
                        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
                        importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                    }
                }

                if (importer == null)
                {
                    Debug.LogWarning("Cocoon taxi OBJ not found at " + modelPath + ".");
                    continue;
                }

                ApplyFinalTaxiObjImportSettings(importer, modelPath);
                importer.SaveAndReimport();
                importedCount++;
            }

            Debug.Log("Cocoon reimported " + importedCount + " taxi OBJ asset(s) with material settings.");
        }

        [MenuItem("Cocoon/Reimport Seat V2 OBJ")]
        public static void ReimportSeatV2Obj()
        {
            var importer = AssetImporter.GetAtPath(SeatV2LatestObjPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("Cocoon Seat V2 OBJ not found at " + SeatV2LatestObjPath + ".");
                return;
            }

            ApplyFinalTaxiObjImportSettings(importer, SeatV2LatestObjPath);
            importer.SaveAndReimport();
            Debug.Log("Cocoon Seat V2 OBJ reimported with hierarchy preservation enabled.");
        }

        [MenuItem("Cocoon/Reimport ZOOX Taxi OBJ And Repair Scene")]
        public static void ReimportZooXTaxiAndRepairScene()
        {
            ReimportAllCocoonTaxiObjAssets();
            CocoonLiveSceneReadabilityRepair.ApplyToOpenScene();
        }

        private static void ApplyFinalTaxiObjImportSettings(ModelImporter importer, string modelPath)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = PathsEqual(modelPath, SolidObjPath) || IsSeatV2ModelPath(modelPath);
            if (IsSeatV2ModelPath(modelPath))
            {
                importer.preserveHierarchy = true;
            }
        }

        private static bool IsSeatV2ModelPath(string modelPath)
        {
            return PathsEqual(modelPath, SeatV2ObjPath) || PathsEqual(modelPath, SeatV2LatestObjPath);
        }

        private static void ApplyImportedMaterialStyles(GameObject importedRoot, string modelPath)
        {
            if (importedRoot == null)
            {
                return;
            }

            Dictionary<string, Color> colors = TryGetMtlPathForModel(modelPath, out string mtlPath)
                ? LoadMtlDiffuseColors(mtlPath)
                : new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase);

            Renderer[] renderers = importedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.sharedMaterials == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                    {
                        continue;
                    }

                    string materialName = NormalizeMaterialName(material.name);
                    if (colors.TryGetValue(materialName, out Color color))
                    {
                        if (modelPath.Equals(TaxiObjPath, System.StringComparison.OrdinalIgnoreCase))
                        {
                            ApplyTaxiMaterialStyle(material, color);
                        }
                        else
                        {
                            ApplyAccessoryMaterialStyle(material, modelPath, color);
                        }

                        continue;
                    }

                    if (!modelPath.Equals(TaxiObjPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyAccessoryFallbackMaterialStyle(material, modelPath);
                    }
                }
            }
        }

        private static Dictionary<string, Color> LoadMtlDiffuseColors(string projectRelativeMtlPath)
        {
            var colors = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase);
            string mtlPath = ToProjectAbsolutePath(projectRelativeMtlPath);
            if (!File.Exists(mtlPath))
            {
                return colors;
            }

            string currentMaterial = string.Empty;
            foreach (string rawLine in File.ReadLines(mtlPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("newmtl ", System.StringComparison.Ordinal))
                {
                    currentMaterial = NormalizeMaterialName(line.Substring(7));
                    continue;
                }

                if (currentMaterial.Length == 0 || !line.StartsWith("Kd ", System.StringComparison.Ordinal))
                {
                    continue;
                }

                string[] channels = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (channels.Length < 4)
                {
                    continue;
                }

                if (TryParseFloat(channels[1], out float r) &&
                    TryParseFloat(channels[2], out float g) &&
                    TryParseFloat(channels[3], out float b))
                {
                    colors[currentMaterial] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
                }
            }

            return colors;
        }

        private enum TaxiMaterialStyle
        {
            BodyBlue,
            Glass,
            GlossyBlack,
            TireRubber,
            SatinMetal,
            LightPanel,
            WarmWhite
        }

        private static void ApplyTaxiMaterialStyle(Material material, Color sourceColor)
        {
            if (IsProtectedAuthoredTaxiMaterial(material))
            {
                return;
            }

            TaxiMaterialStyle style = ClassifyTaxiMaterial(material, sourceColor);
            Color color;
            float metallic;
            float smoothness;
            bool transparent;

            switch (style)
            {
                case TaxiMaterialStyle.BodyBlue:
                    color = new Color(0.47f, 0.76f, 0.94f, 1f);
                    metallic = 0f;
                    smoothness = 0.48f;
                    transparent = false;
                    break;
                case TaxiMaterialStyle.Glass:
                    color = new Color(0.035f, 0.07f, 0.085f, 0.28f);
                    metallic = 0f;
                    smoothness = 0.96f;
                    transparent = true;
                    break;
                case TaxiMaterialStyle.TireRubber:
                    color = new Color(0.006f, 0.006f, 0.007f, 1f);
                    metallic = 0f;
                    smoothness = 0.28f;
                    transparent = false;
                    break;
                case TaxiMaterialStyle.SatinMetal:
                    color = new Color(0.72f, 0.74f, 0.72f, 1f);
                    metallic = 0.35f;
                    smoothness = 0.62f;
                    transparent = false;
                    break;
                case TaxiMaterialStyle.LightPanel:
                    color = new Color(0.78f, 0.81f, 0.80f, 1f);
                    metallic = 0f;
                    smoothness = 0.42f;
                    transparent = false;
                    break;
                case TaxiMaterialStyle.WarmWhite:
                    color = new Color(0.88f, 0.9f, 0.88f, 1f);
                    metallic = 0f;
                    smoothness = 0.36f;
                    transparent = false;
                    break;
                default:
                    color = new Color(0.014f, 0.02f, 0.024f, 1f);
                    metallic = 0f;
                    smoothness = 0.72f;
                    transparent = false;
                    break;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null && material.shader != urpLit)
            {
                material.shader = urpLit;
            }

            ConfigureSurface(material, transparent);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            EditorUtility.SetDirty(material);
        }

        private static bool IsProtectedAuthoredTaxiMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            string materialName = NormalizeMaterialName(material.name);
            return materialName.Equals("TaxiGlass", System.StringComparison.OrdinalIgnoreCase);
        }

        private static TaxiMaterialStyle ClassifyTaxiMaterial(Material material, Color color)
        {
            if (IsGlassMaterialName(material != null ? material.name : string.Empty))
            {
                return TaxiMaterialStyle.Glass;
            }

            if (IsClose(color, 0.447059f, 0.713725f, 0.878431f, 0.04f))
            {
                return TaxiMaterialStyle.BodyBlue;
            }

            if (IsClose(color, 0.098039f, 0.098039f, 0.098039f, 0.012f))
            {
                return TaxiMaterialStyle.Glass;
            }

            if (color.r > 0.95f && color.g > 0.95f && color.b > 0.95f)
            {
                return TaxiMaterialStyle.WarmWhite;
            }

            if (IsClose(color, 0.701961f, 0.701961f, 0.701961f, 0.04f))
            {
                return TaxiMaterialStyle.LightPanel;
            }

            if (IsClose(color, 0.627451f, 0.627451f, 0.627451f, 0.04f) ||
                IsClose(color, 0.470588f, 0.470588f, 0.470588f, 0.04f))
            {
                return TaxiMaterialStyle.SatinMetal;
            }

            if (color.maxColorComponent < 0.055f)
            {
                return TaxiMaterialStyle.TireRubber;
            }

            return TaxiMaterialStyle.GlossyBlack;
        }

        private static bool IsGlassMaterialName(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName))
            {
                return false;
            }

            return materialName.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("\u73BB\u7483", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("鐜", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("\u9438", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("\u7487", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private enum AccessoryMaterialStyle
        {
            SatinMetal,
            SoftGray,
            DarkRubber,
            DisplayBlack,
            SeatCushion,
            RedAccent,
            WheelchairDefault
        }

        private static void ApplyAccessoryMaterialStyle(Material material, string modelPath, Color sourceColor)
        {
            ApplyAccessoryMaterialStyle(material, ClassifyAccessoryMaterial(modelPath, sourceColor));
        }

        private static void ApplyAccessoryFallbackMaterialStyle(Material material, string modelPath)
        {
            AccessoryMaterialStyle style = modelPath.EndsWith("wheelchair.obj", System.StringComparison.OrdinalIgnoreCase)
                ? AccessoryMaterialStyle.WheelchairDefault
                : AccessoryMaterialStyle.SoftGray;
            ApplyAccessoryMaterialStyle(material, style);
        }

        private static AccessoryMaterialStyle ClassifyAccessoryMaterial(string modelPath, Color color)
        {
            if (modelPath.EndsWith("seat.obj", System.StringComparison.OrdinalIgnoreCase))
            {
                if (color.r > 0.9f && color.g > 0.9f && color.b > 0.5f)
                {
                    return AccessoryMaterialStyle.SeatCushion;
                }

                if (color.r > 0.55f && color.g < 0.1f && color.b < 0.1f)
                {
                    return AccessoryMaterialStyle.RedAccent;
                }
            }

            if (modelPath.EndsWith("solid.obj", System.StringComparison.OrdinalIgnoreCase) &&
                IsClose(color, 0.098039f, 0.098039f, 0.098039f, 0.012f))
            {
                return AccessoryMaterialStyle.DisplayBlack;
            }

            if (color.maxColorComponent < 0.12f)
            {
                return AccessoryMaterialStyle.DarkRubber;
            }

            if (color.maxColorComponent > 0.58f)
            {
                return AccessoryMaterialStyle.SatinMetal;
            }

            return AccessoryMaterialStyle.SoftGray;
        }

        private static void ApplyAccessoryMaterialStyle(Material material, AccessoryMaterialStyle style)
        {
            Color color;
            float metallic;
            float smoothness;

            switch (style)
            {
                case AccessoryMaterialStyle.SatinMetal:
                    color = new Color(0.68f, 0.7f, 0.68f, 1f);
                    metallic = 0.28f;
                    smoothness = 0.58f;
                    break;
                case AccessoryMaterialStyle.DarkRubber:
                    color = new Color(0.015f, 0.015f, 0.016f, 1f);
                    metallic = 0f;
                    smoothness = 0.24f;
                    break;
                case AccessoryMaterialStyle.DisplayBlack:
                    color = new Color(0.005f, 0.012f, 0.014f, 1f);
                    metallic = 0f;
                    smoothness = 0.82f;
                    break;
                case AccessoryMaterialStyle.SeatCushion:
                    color = new Color(0.92f, 0.86f, 0.46f, 1f);
                    metallic = 0f;
                    smoothness = 0.34f;
                    break;
                case AccessoryMaterialStyle.RedAccent:
                    color = new Color(0.7f, 0.02f, 0.015f, 1f);
                    metallic = 0f;
                    smoothness = 0.44f;
                    break;
                case AccessoryMaterialStyle.WheelchairDefault:
                    color = new Color(0.38f, 0.4f, 0.4f, 1f);
                    metallic = 0.22f;
                    smoothness = 0.48f;
                    break;
                default:
                    color = new Color(0.5f, 0.52f, 0.52f, 1f);
                    metallic = 0f;
                    smoothness = 0.38f;
                    break;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null && material.shader != urpLit)
            {
                material.shader = urpLit;
            }

            ConfigureSurface(material, false);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            EditorUtility.SetDirty(material);
        }

        private static bool IsClose(Color color, float r, float g, float b, float tolerance)
        {
            return Mathf.Abs(color.r - r) <= tolerance &&
                   Mathf.Abs(color.g - g) <= tolerance &&
                   Mathf.Abs(color.b - b) <= tolerance;
        }

        private static void ConfigureSurface(Material material, bool transparent)
        {
            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                SetFloatIfPresent(material, "_Surface", 1f);
                SetFloatIfPresent(material, "_Blend", 0f);
                SetFloatIfPresent(material, "_AlphaClip", 0f);
                SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
                SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                SetFloatIfPresent(material, "_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
                return;
            }

            material.SetOverrideTag("RenderType", "Opaque");
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.Zero);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = -1;
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static string NormalizeMaterialName(string materialName)
        {
            if (string.IsNullOrWhiteSpace(materialName))
            {
                return string.Empty;
            }

            return materialName
                .Replace(" (Instance)", string.Empty)
                .Replace("(Instance)", string.Empty)
                .Trim();
        }

        private static bool TryParseFloat(string value, out float parsed)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
        }

        private static string ToProjectAbsolutePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool IsImportedCocoonModel(string path)
        {
            return ContainsPath(ImportedObjPaths, path);
        }

        private static bool TryGetMtlPathForModel(string modelPath, out string mtlPath)
        {
            mtlPath = Path.ChangeExtension(modelPath, ".mtl").Replace('\\', '/');
            return ContainsPath(ImportedMtlPaths, mtlPath) && File.Exists(ToProjectAbsolutePath(mtlPath));
        }

        private static bool ContainsAnyPath(string[] paths, string[] expectedPaths)
        {
            if (expectedPaths == null)
            {
                return false;
            }

            for (int i = 0; i < expectedPaths.Length; i++)
            {
                if (ContainsPath(paths, expectedPaths[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPath(string[] paths, string expectedPath)
        {
            if (paths == null)
            {
                return false;
            }

            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].Equals(expectedPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return left.Replace('\\', '/').Equals(right.Replace('\\', '/'), System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
