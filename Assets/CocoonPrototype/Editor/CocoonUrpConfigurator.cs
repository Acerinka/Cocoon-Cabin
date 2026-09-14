using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CocoonPrototype.Editor
{
    [InitializeOnLoad]
    public static class CocoonUrpConfigurator
    {
        private static readonly bool VerboseUrpLogs = false;
        private const string RenderingFolder = "Assets/CocoonPrototype/Rendering";
        private const string RendererPath = RenderingFolder + "/Cocoon_UniversalRenderer.asset";
        private const string PipelinePath = RenderingFolder + "/Cocoon_URP_Pipeline.asset";
        private const string ScenePath = "Assets/Scenes/Cocoon_OnboardingVR.unity";
        private const int CocoonAntiAliasingSamples = 8;
        private const float CocoonRenderScale = 1.35f;
        private const float CocoonShadowDistance = 70f;
        private const int CocoonShadowResolution = 4096;
        private const int CocoonScreenTextureMaxSize = 8192;
        private static readonly string[] ScreenTextureFolders =
        {
            "Assets/CocoonPrototype/Resources/ExteriorScreens",
            "Assets/CocoonPrototype/Resources/CabinScreens"
        };

        static CocoonUrpConfigurator()
        {
            EditorApplication.delayCall += ConfigureUrp;
        }

        [MenuItem("Cocoon/Configure URP Rendering")]
        public static void ConfigureUrp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EnsureFolder(RenderingFolder);

            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "Cocoon Universal Renderer";
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            UniversalRenderPipelineAsset pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipelineAsset == null)
            {
                pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                pipelineAsset.name = "Cocoon URP Pipeline";
                AssetDatabase.CreateAsset(pipelineAsset, PipelinePath);
            }

            ConfigurePipelineAsset(pipelineAsset, rendererData);
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            int originalQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
                ConfigureQualitySettings();
            }

            QualitySettings.SetQualityLevel(originalQuality, false);
            QualitySettings.renderPipeline = pipelineAsset;
            ConfigureQualitySettings();

            ConvertPrototypeMaterials();
            ConfigureScreenTextureImports();
            ConfigureOpenSceneCameras();
            ConfigureOpenSceneLighting();
            int repairedSceneMaterialSlots = RepairOpenSceneMaterials();

            AssetDatabase.SaveAssets();
            if (VerboseUrpLogs)
            {
                Debug.Log("Cocoon URP rendering configured: pipeline asset, renderer asset, quality settings, prototype materials, and " + repairedSceneMaterialSlots + " open-scene material slots.");
            }
        }

        private static void ConfigurePipelineAsset(UniversalRenderPipelineAsset pipelineAsset, UniversalRendererData rendererData)
        {
            SerializedObject serialized = new SerializedObject(pipelineAsset);
            SerializedProperty rendererList = serialized.FindProperty("m_RendererDataList");
            if (rendererList != null)
            {
                rendererList.arraySize = 1;
                rendererList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            }

            SetSerializedInt(serialized, "m_DefaultRendererIndex", 0);
            SetSerializedInt(serialized, "m_MSAA", CocoonAntiAliasingSamples);
            SetSerializedFloat(serialized, "m_RenderScale", CocoonRenderScale);
            SetSerializedBool(serialized, "m_SupportsHDR", true);
            SetSerializedBool(serialized, "m_RequireDepthTexture", false);
            SetSerializedBool(serialized, "m_RequireOpaqueTexture", false);
            SetSerializedBool(serialized, "m_SupportsCameraDepthTexture", false);
            SetSerializedBool(serialized, "m_SupportsCameraOpaqueTexture", false);
            SetSerializedBool(serialized, "m_UseSRPBatcher", true);
            SetSerializedFloat(serialized, "m_ShadowDistance", CocoonShadowDistance);
            SetSerializedInt(serialized, "m_MainLightShadowmapResolution", CocoonShadowResolution);
            SetSerializedInt(serialized, "m_AdditionalLightsShadowmapResolution", 2048);
            SetSerializedInt(serialized, "m_ShadowAtlasResolution", CocoonShadowResolution);
            SetSerializedInt(serialized, "m_ShadowCascadeCount", 4);
            SetSerializedBool(serialized, "m_SoftShadowsSupported", true);
            SetSerializedInt(serialized, "m_SoftShadowQuality", 2);
            SetSerializedFloat(serialized, "m_ShadowDepthBias", 0.85f);
            SetSerializedFloat(serialized, "m_ShadowNormalBias", 0.48f);
            SetSerializedFloat(serialized, "m_CascadeBorder", 0.08f);
            SetSerializedBool(serialized, "m_ConservativeEnclosingSphere", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            pipelineAsset.msaaSampleCount = CocoonAntiAliasingSamples;
            pipelineAsset.renderScale = CocoonRenderScale;
            EditorUtility.SetDirty(pipelineAsset);
            EditorUtility.SetDirty(rendererData);
        }

        private static void ConfigureQualitySettings()
        {
            QualitySettings.antiAliasing = CocoonAntiAliasingSamples;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.vSyncCount = 0;
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 1.8f);
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, CocoonShadowDistance);
            QualitySettings.realtimeReflectionProbes = true;
        }

        private static void ConvertPrototypeMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("URP Lit shader was not found. URP package may still be importing.");
                return;
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/CocoonPrototype/Materials" });
            for (int i = 0; i < materialGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                if (path.EndsWith("/TaxiGlass.mat", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("\\TaxiGlass.mat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                Color baseColor = GetMaterialColor(material);
                Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                bool emissive = material.IsKeywordEnabled("_EMISSION") || emission.maxColorComponent > 0.01f;

                material.shader = urpLit;
                SetColorIfPresent(material, "_BaseColor", baseColor);
                SetColorIfPresent(material, "_Color", baseColor);
                if (emissive)
                {
                    material.EnableKeyword("_EMISSION");
                    SetColorIfPresent(material, "_EmissionColor", emission.maxColorComponent > 0.01f ? emission : baseColor * 1.8f);
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    SetColorIfPresent(material, "_EmissionColor", Color.black);
                }

                EditorUtility.SetDirty(material);
            }
        }

        private static void ConfigureScreenTextureImports()
        {
            for (int folderIndex = 0; folderIndex < ScreenTextureFolders.Length; folderIndex++)
            {
                string folder = ScreenTextureFolders[folderIndex];
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                for (int i = 0; i < textureGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        continue;
                    }

                    bool changed = false;
                    changed |= SetTextureImporterValue(importer.textureType != TextureImporterType.Default, () => importer.textureType = TextureImporterType.Default);
                    changed |= SetTextureImporterValue(!importer.sRGBTexture, () => importer.sRGBTexture = true);
                    changed |= SetTextureImporterValue(!importer.mipmapEnabled, () => importer.mipmapEnabled = true);
                    changed |= SetTextureImporterValue(importer.npotScale != TextureImporterNPOTScale.None, () => importer.npotScale = TextureImporterNPOTScale.None);
                    changed |= SetTextureImporterValue(importer.wrapMode != TextureWrapMode.Clamp, () => importer.wrapMode = TextureWrapMode.Clamp);
                    changed |= SetTextureImporterValue(importer.filterMode != FilterMode.Trilinear, () => importer.filterMode = FilterMode.Trilinear);
                    changed |= SetTextureImporterValue(importer.anisoLevel < 16, () => importer.anisoLevel = 16);
                    changed |= SetTextureImporterValue(importer.maxTextureSize < CocoonScreenTextureMaxSize, () => importer.maxTextureSize = CocoonScreenTextureMaxSize);
                    changed |= SetTextureImporterValue(importer.textureCompression != TextureImporterCompression.Uncompressed, () => importer.textureCompression = TextureImporterCompression.Uncompressed);

                    if (changed)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static bool SetTextureImporterValue(bool shouldSet, Action setter)
        {
            if (!shouldSet)
            {
                return false;
            }

            setter();
            return true;
        }

        private static void ConfigureOpenSceneCameras()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !scene.path.Equals(ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    camera.allowMSAA = true;
                    camera.allowHDR = false;
                    if (camera.GetComponent<UniversalAdditionalCameraData>() == null)
                    {
                        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    }

                    EditorUtility.SetDirty(camera);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void ConfigureOpenSceneLighting()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !scene.path.Equals(ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.88f, 0.93f);
            RenderSettings.ambientEquatorColor = new Color(0.64f, 0.68f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.44f, 0.46f, 0.48f);
            RenderSettings.reflectionIntensity = 0.78f;
            RenderSettings.fog = false;

            bool changed = true;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Light[] lights = root.GetComponentsInChildren<Light>(true);
                for (int i = 0; i < lights.Length; i++)
                {
                    Light light = lights[i];
                    if (light == null || light.type != LightType.Directional)
                    {
                        continue;
                    }

                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.42f;
                    light.shadowBias = 0.075f;
                    light.shadowNormalBias = 0.42f;
                    light.shadowNearPlane = 0.1f;
                    light.shadowResolution = LightShadowResolution.VeryHigh;
                    if (light.name.IndexOf("Soft Directional", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        light.intensity = 1.05f;
                        light.color = new Color(1f, 0.965f, 0.91f);
                    }
                    else
                    {
                        light.intensity = Mathf.Max(light.intensity, 1.05f);
                    }

                    EditorUtility.SetDirty(light);
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static int RepairOpenSceneMaterials()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !scene.path.Equals(ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                return 0;
            }

            int repairedSlots = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    Material[] materials = renderer.sharedMaterials;
                    bool rendererChanged = false;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        if (material == null)
                        {
                            continue;
                        }

                        Material prototypeMaterial = ResolvePrototypeMaterial(material);
                        if (prototypeMaterial != null && NeedsShaderUpgrade(material.shader))
                        {
                            materials[materialIndex] = prototypeMaterial;
                            rendererChanged = true;
                            repairedSlots++;
                            continue;
                        }

                        if (NeedsShaderUpgrade(material.shader))
                        {
                            Color baseColor = GetMaterialColor(material);
                            Color emission = GetEmissionColor(material);
                            bool emissive = material.IsKeywordEnabled("_EMISSION") || emission.maxColorComponent > 0.01f;
                            material.shader = urpLit;
                            SetColorIfPresent(material, "_BaseColor", baseColor);
                            SetColorIfPresent(material, "_Color", baseColor);
                            if (emissive)
                            {
                                material.EnableKeyword("_EMISSION");
                                SetColorIfPresent(material, "_EmissionColor", emission.maxColorComponent > 0.01f ? emission : baseColor * 1.8f);
                            }
                            else
                            {
                                material.DisableKeyword("_EMISSION");
                                SetColorIfPresent(material, "_EmissionColor", Color.black);
                            }

                            EditorUtility.SetDirty(material);
                            rendererChanged = true;
                            repairedSlots++;
                        }
                    }

                    if (rendererChanged)
                    {
                        renderer.sharedMaterials = materials;
                        EditorUtility.SetDirty(renderer);
                    }
                }
            }

            if (repairedSlots > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return repairedSlots;
        }

        private static Material ResolvePrototypeMaterial(Material material)
        {
            string materialName = material.name.Replace(" (Instance)", "").Trim();
            if (string.IsNullOrEmpty(materialName))
            {
                return null;
            }

            string materialPath = "Assets/CocoonPrototype/Materials/" + materialName + ".mat";
            return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        private static Color GetEmissionColor(Material material)
        {
            if (material.HasProperty("_EmissionColor"))
            {
                return material.GetColor("_EmissionColor");
            }

            return Color.black;
        }

        private static bool NeedsShaderUpgrade(Shader shader)
        {
            if (shader == null)
            {
                return true;
            }

            string shaderName = shader.name;
            return shaderName == "Standard" ||
                   shaderName == "Hidden/InternalErrorShader" ||
                   shaderName.Contains("Error") ||
                   shaderName.StartsWith("Legacy Shaders");
        }

        private static void SetColorIfPresent(Material material, string property, Color color)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        private static void SetSerializedInt(SerializedObject serialized, string property, int value)
        {
            SerializedProperty serializedProperty = serialized.FindProperty(property);
            if (serializedProperty != null)
            {
                serializedProperty.intValue = value;
            }
        }

        private static void SetSerializedFloat(SerializedObject serialized, string property, float value)
        {
            SerializedProperty serializedProperty = serialized.FindProperty(property);
            if (serializedProperty != null)
            {
                serializedProperty.floatValue = value;
            }
        }

        private static void SetSerializedBool(SerializedObject serialized, string property, bool value)
        {
            SerializedProperty serializedProperty = serialized.FindProperty(property);
            if (serializedProperty != null)
            {
                serializedProperty.boolValue = value;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string child = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
