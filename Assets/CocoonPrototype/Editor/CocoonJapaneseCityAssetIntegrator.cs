using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CocoonPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CocoonPrototype.Editor
{
    public static class CocoonJapaneseCityAssetIntegrator
    {
        private const string ScenePath = "Assets/Scenes/Cocoon_OnboardingVR.unity";
        private const string AssetRoot = "Assets/JapaneseCity";
        private const string DemoScenePath = "Assets/JapaneseCity/Scenes/JapaneseCity_Demo.unity";
        private const string StreetscapeRootName = "Japanese City Demo Streetscape";
        private const string GeneratedStreetscapeRootName = "Japanese City Streetscape";
        private const float MinimumDemoScale = 0.04f;
        private const float MaximumDemoScale = 0.18f;
        private const float TrafficFootprintMarginX = 46f;
        private const float TrafficFootprintMarginZ = 36f;
        private static readonly string[] DemoSignatureChildNames =
        {
            "Geometry",
            "Colliders",
            "jctFarBuildingsA",
            "trafficlight",
            "trafficlightB"
        };

        [InitializeOnLoadMethod]
        private static void ApplyQueuedStreetscapeOnLoad()
        {
            if (!File.Exists(GetQueuedApplyFlagPath()))
            {
                return;
            }

            if (Application.isBatchMode)
            {
                ApplyQueuedStreetscape();
            }
            else
            {
                EditorApplication.delayCall += ApplyQueuedStreetscape;
            }
        }

        [MenuItem("Cocoon/Import Japanese City Package")]
        public static void ImportJapaneseCityPackageMenu()
        {
            if (ImportJapaneseCityPackageFromDownloads())
            {
                Debug.Log("Japanese City package import finished.");
            }
        }

        [MenuItem("Cocoon/Apply Full Japanese City Demo Streetscape")]
        public static void ApplyJapaneseCityStreetscapeMenu()
        {
            if (ApplyToCurrentSceneIfAvailable(true, true))
            {
                Debug.Log("Full Japanese City demo streetscape applied to the open Cocoon scene.");
            }
        }

        [MenuItem("Cocoon/Apply Full Japanese City Demo Streetscape And Validate")]
        public static void ApplyJapaneseCityStreetscapeAndValidateMenu()
        {
            if (ApplyToCurrentSceneIfAvailable(true, true) && ValidateCurrentScene())
            {
                Debug.Log("Full Japanese City demo streetscape applied and validated in the open Cocoon scene.");
            }
        }

        [MenuItem("Cocoon/Import And Apply Full Japanese City Demo Streetscape")]
        public static void ImportAndApplyJapaneseCityStreetscape()
        {
            if (!AssetDatabase.IsValidFolder(AssetRoot) && !ImportJapaneseCityPackageFromDownloads())
            {
                return;
            }

            ApplyToCurrentSceneIfAvailable(true, true);
        }

        public static void ApplySavedSceneJapaneseCityAndValidate()
        {
            ApplySavedSceneJapaneseCityDemoAndValidate();
        }

        public static void ApplySavedSceneJapaneseCityDemoAndValidate()
        {
            if (!AssetDatabase.IsValidFolder(AssetRoot))
            {
                throw new InvalidOperationException("Assets/JapaneseCity is missing. Import the Japanese City package before applying the full demo streetscape.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
            {
                throw new InvalidOperationException(DemoScenePath + " is missing. The complete Japanese City demo scene is required.");
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyToCurrentSceneIfAvailable(true, true);
            if (!ValidateCurrentScene())
            {
                throw new InvalidOperationException("Japanese City demo streetscape validation failed. See the Unity editor log for details.");
            }

            AssetDatabase.SaveAssets();
        }

        public static void LogSavedSceneJapaneseCityScaleDiagnostics()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Scene scene = SceneManager.GetActiveScene();
            Transform streetRoot = FindTransformDeep(scene, "02_Street_Block");
            Transform streetscapeRoot = streetRoot != null ? streetRoot.Find(StreetscapeRootName) : null;
            if (streetscapeRoot == null)
            {
                Debug.LogWarning("Japanese City scale diagnostics skipped: " + StreetscapeRootName + " is missing.");
                return;
            }

            Debug.Log("Japanese City root transform: position " + streetscapeRoot.position.ToString("F3") +
                      ", localScale " + streetscapeRoot.localScale.ToString("F3") + ".");
            if (TryGetPreferredStreetscapeBounds(streetscapeRoot, out Bounds preferredBounds))
            {
                Debug.Log("Japanese City preferred bounds: center " + preferredBounds.center.ToString("F3") +
                          ", size " + preferredBounds.size.ToString("F3") +
                          ", min " + preferredBounds.min.ToString("F3") +
                          ", max " + preferredBounds.max.ToString("F3") + ".");
            }

            if (TryGetRendererBoundsIgnoringFarDepth(streetscapeRoot, out Bounds nearBounds))
            {
                Debug.Log("Japanese City near-field bounds: center " + nearBounds.center.ToString("F3") +
                          ", size " + nearBounds.size.ToString("F3") +
                          ", min " + nearBounds.min.ToString("F3") +
                          ", max " + nearBounds.max.ToString("F3") + ".");
            }

            if (TryGetStreetSurfaceBounds(streetscapeRoot, out Bounds streetSurfaceBounds))
            {
                Debug.Log("Japanese City street-surface bounds: center " + streetSurfaceBounds.center.ToString("F3") +
                          ", size " + streetSurfaceBounds.size.ToString("F3") +
                          ", min " + streetSurfaceBounds.min.ToString("F3") +
                          ", max " + streetSurfaceBounds.max.ToString("F3") + ".");
            }

            Transform geometry = FindTransformDeep(streetscapeRoot, "Geometry");
            if (geometry != null && TryGetRendererBounds(geometry.gameObject, out Bounds geometryBounds))
            {
                Debug.Log("Japanese City Geometry bounds: center " + geometryBounds.center.ToString("F3") +
                          ", size " + geometryBounds.size.ToString("F3") +
                          ", min " + geometryBounds.min.ToString("F3") +
                          ", max " + geometryBounds.max.ToString("F3") + ".");
            }

            Transform colliders = FindTransformDeep(streetscapeRoot, "Colliders");
            if (colliders != null && TryGetColliderBounds(colliders, out Bounds colliderBounds))
            {
                Debug.Log("Japanese City Colliders bounds: center " + colliderBounds.center.ToString("F3") +
                          ", size " + colliderBounds.size.ToString("F3") +
                          ", min " + colliderBounds.min.ToString("F3") +
                          ", max " + colliderBounds.max.ToString("F3") + ".");
            }

            if (TryGetTrafficFootprint(scene, out Bounds trafficBounds))
            {
                Debug.Log("Cocoon traffic footprint bounds: center " + trafficBounds.center.ToString("F3") +
                          ", size " + trafficBounds.size.ToString("F3") +
                          ", min " + trafficBounds.min.ToString("F3") +
                          ", max " + trafficBounds.max.ToString("F3") + ".");
            }
        }

        public static void QueueApplyOnNextEditorLoad()
        {
            string flagPath = GetQueuedApplyFlagPath();
            Directory.CreateDirectory(Path.GetDirectoryName(flagPath));
            File.WriteAllText(flagPath, DateTime.UtcNow.ToString("O"));
        }

        private static void ApplyQueuedStreetscape()
        {
            string flagPath = GetQueuedApplyFlagPath();
            if (!File.Exists(flagPath))
            {
                return;
            }

            File.Delete(flagPath);
            if (!AssetDatabase.IsValidFolder(AssetRoot))
            {
                Debug.LogWarning("Queued Japanese City streetscape apply skipped because Assets/JapaneseCity is not imported yet.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Scene scene = SceneManager.GetActiveScene();
            if (FindTopLevelTransform(scene, "ROADMAP") != null)
            {
                Debug.Log("Queued Japanese City streetscape apply skipped because the scene already has a top-level ROADMAP; preserving the hand-curated road network.");
                return;
            }

            if (ApplyToCurrentSceneIfAvailable(true, true))
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Queued Japanese City streetscape applied to the Cocoon onboarding scene.");
            }
        }

        private static string GetQueuedApplyFlagPath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, "Library", "CodexApplyJapaneseCity.flag");
        }

        public static bool ApplyToCurrentSceneIfAvailable(bool saveScene, bool force)
        {
            if (!AssetDatabase.IsValidFolder(AssetRoot) || AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
            {
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            Transform streetRoot = FindOrCreateRoot(scene, "02_Street_Block");
            bool changed = ConvertJapaneseCityMaterialsForUrp();
            changed |= RemoveDirectChildIfPresent(streetRoot, GeneratedStreetscapeRootName);
            bool legacyVisibilityChanged = HideLegacyStreetVisuals(streetRoot);
            legacyVisibilityChanged |= HideLegacyPickupBayVisuals(scene);
            changed |= legacyVisibilityChanged;

            Transform existingRoot = streetRoot.Find(StreetscapeRootName);
            if (existingRoot != null)
            {
                bool cityVisibilityChanged = RepairUnsupportedRendererMaterialsForUrp(existingRoot);
                bool hasFullSignature = HasFullDemoSceneSignature(existingRoot);
                bool cityVisible = IsStreetscapeVisibleEnough(existingRoot, out string visibilityReason);
                if (!hasFullSignature)
                {
                    visibilityReason = "full JapaneseCity_Demo signature children are missing";
                }

                bool cityUsable = hasFullSignature && cityVisible;
                if (!force)
                {
                    changed |= cityVisibilityChanged;
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (saveScene)
                        {
                            EditorSceneManager.SaveScene(scene);
                        }
                    }

                    string signatureNote = cityUsable
                        ? visibilityReason
                        : "manual edits preserved; full demo signature check was not used for rebuild. " + visibilityReason;
                    Debug.Log("Existing Japanese City demo streetscape preserved without transform changes. " + signatureNote);
                    return changed;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);
                    changed = true;
                }
            }

            Scene previousActiveScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var rootObject = new GameObject(StreetscapeRootName);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            Transform root = rootObject.transform;
            root.SetParent(streetRoot, false);

            ImportCompleteDemoSceneIntoRoot(scene, root);
            ForceStreetscapeVisible(root);
            RepairUnsupportedRendererMaterialsForUrp(root);
            AlignStreetscapeToCocoonFootprint(root, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            return true;
        }

        private static bool ConvertJapaneseCityMaterialsForUrp()
        {
            Shader targetShader = Shader.Find("Universal Render Pipeline/Lit") ??
                                  Shader.Find("Universal Render Pipeline/Simple Lit") ??
                                  Shader.Find("Standard");
            if (targetShader == null)
            {
                Debug.LogWarning("Unable to find a lit shader for Japanese City materials.");
                return false;
            }

            bool changed = false;
            int convertedCount = 0;
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { AssetRoot + "/Materials" });
            for (int i = 0; i < materialGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == targetShader)
                {
                    continue;
                }

                Texture mainTexture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                if (mainTexture == null && material.HasProperty("_MainTex"))
                {
                    mainTexture = material.GetTexture("_MainTex");
                }

                Texture normalTexture = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
                Color baseColor = Color.white;
                if (material.HasProperty("_BaseColor"))
                {
                    baseColor = material.GetColor("_BaseColor");
                }
                else if (material.HasProperty("_Color"))
                {
                    baseColor = material.GetColor("_Color");
                }

                float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                float smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") :
                    material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.35f;
                bool alphaClip = material.IsKeywordEnabled("_ALPHATEST_ON") ||
                                 (material.HasProperty("_Cutoff") && material.GetFloat("_Cutoff") > 0.001f);
                bool emission = material.IsKeywordEnabled("_EMISSION");
                Color emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                int renderQueue = material.renderQueue;

                material.shader = targetShader;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", mainTexture);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", mainTexture);
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", baseColor);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", baseColor);
                }

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", metallic);
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", smoothness);
                }

                if (normalTexture != null && material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normalTexture);
                    material.EnableKeyword("_NORMALMAP");
                }

                if (alphaClip)
                {
                    if (material.HasProperty("_AlphaClip"))
                    {
                        material.SetFloat("_AlphaClip", 1f);
                    }

                    if (material.HasProperty("_Cutoff"))
                    {
                        material.SetFloat("_Cutoff", 0.5f);
                    }

                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = renderQueue >= 2450 ? renderQueue : 2450;
                }

                if (emission)
                {
                    material.EnableKeyword("_EMISSION");
                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.SetColor("_EmissionColor", emissionColor);
                    }
                }

                EditorUtility.SetDirty(material);
                convertedCount++;
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Converted " + convertedCount + " Japanese City materials to " + targetShader.name + ".");
            }

            return changed;
        }

        private static bool RemoveDirectChildIfPresent(Transform parent, string childName)
        {
            bool changed = false;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == childName)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    changed = true;
                }
            }

            return changed;
        }

        private static void ImportCompleteDemoSceneIntoRoot(Scene targetScene, Transform root)
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(targetScene);
            Scene demoScene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Additive);
            int importedRootCount = 0;
            try
            {
                GameObject[] demoRoots = demoScene.GetRootGameObjects();
                for (int i = 0; i < demoRoots.Length; i++)
                {
                    GameObject demoRoot = demoRoots[i];
                    if (demoRoot == null || !ShouldImportDemoRoot(demoRoot))
                    {
                        continue;
                    }

                    SceneManager.MoveGameObjectToScene(demoRoot, targetScene);
                    demoRoot.transform.SetParent(root, true);
                    importedRootCount++;
                }
            }
            finally
            {
                if (demoScene.IsValid() && demoScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(demoScene, true);
                }

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }

            if (importedRootCount == 0)
            {
                throw new InvalidOperationException("No root objects were imported from " + DemoScenePath + ".");
            }

            Debug.Log("Imported " + importedRootCount + " root objects from the complete JapaneseCity_Demo scene.");
        }

        private static bool ShouldImportDemoRoot(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            string lowerName = root.name.ToLowerInvariant();
            if (lowerName.Contains("camera") || root.GetComponentInChildren<Camera>(true) != null)
            {
                return false;
            }

            return true;
        }

        private static bool AlignStreetscapeToCocoonFootprint(Transform streetscapeRoot, Scene scene)
        {
            if (streetscapeRoot == null)
            {
                return false;
            }

            Vector3 targetCenter = new Vector3(17f, 0f, 0f);
            Vector3 targetFootprintSize = new Vector3(80f, 0f, 100f);
            if (TryGetTrafficFootprint(scene, out Bounds trafficBounds))
            {
                targetCenter = trafficBounds.center;
                targetFootprintSize = new Vector3(
                    Mathf.Max(trafficBounds.size.x + TrafficFootprintMarginX, trafficBounds.size.x * 2.2f),
                    0f,
                    Mathf.Max(trafficBounds.size.z + TrafficFootprintMarginZ, trafficBounds.size.z * 1.45f));
            }

            bool changed = false;
            if (!TryGetStreetscapeAlignmentBounds(streetscapeRoot, out Bounds alignmentBounds))
            {
                return false;
            }

            float scaleX = targetFootprintSize.x / Mathf.Max(0.01f, alignmentBounds.size.x);
            float scaleZ = targetFootprintSize.z / Mathf.Max(0.01f, alignmentBounds.size.z);
            float scaleMultiplier = Mathf.Min(scaleX, scaleZ);
            float currentScale = Mathf.Max(0.0001f, streetscapeRoot.localScale.x);
            float desiredScale = Mathf.Clamp(currentScale * scaleMultiplier, MinimumDemoScale, MaximumDemoScale);
            if (Mathf.Abs(desiredScale - currentScale) > 0.0005f ||
                Mathf.Abs(streetscapeRoot.localScale.y - currentScale) > 0.0005f ||
                Mathf.Abs(streetscapeRoot.localScale.z - currentScale) > 0.0005f)
            {
                streetscapeRoot.localScale = Vector3.one * desiredScale;
                changed = true;
            }

            if (!TryGetStreetscapeAlignmentBounds(streetscapeRoot, out alignmentBounds))
            {
                return changed;
            }

            Vector3 currentGroundCenter = new Vector3(alignmentBounds.center.x, alignmentBounds.min.y, alignmentBounds.center.z);
            Vector3 desiredGroundCenter = new Vector3(targetCenter.x, 0f, targetCenter.z);
            Vector3 offset = desiredGroundCenter - currentGroundCenter;
            if (offset.sqrMagnitude > 0.0001f)
            {
                streetscapeRoot.position += offset;
                changed = true;
            }

            if (changed)
            {
                Debug.Log("Scaled/aligned full Japanese City demo streetscape: scale " + desiredScale.ToString("F3") +
                          ", alignment bounds size " + alignmentBounds.size.ToString("F2") +
                          ", offset " + offset.ToString("F2") +
                          ", target footprint " + targetFootprintSize.ToString("F2") + ".");
            }

            return changed;
        }

        private static bool TryGetStreetscapeAlignmentBounds(Transform root, out Bounds bounds)
        {
            if (TryGetStreetSurfaceBounds(root, out bounds))
            {
                return true;
            }

            return TryGetRendererBoundsIgnoringFarDepth(root, out bounds);
        }

        private static bool TryGetTrafficFootprint(Scene scene, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            CocoonTrafficLanePath lanePath = FindComponentDeep<CocoonTrafficLanePath>(scene);
            if (lanePath != null)
            {
                for (int i = 0; i < lanePath.Count; i++)
                {
                    Transform waypoint = lanePath.GetWaypoint(i);
                    if (waypoint == null)
                    {
                        continue;
                    }

                    EncapsulatePoint(ref bounds, ref hasBounds, waypoint.position);
                }
            }

            if (!hasBounds)
            {
                Transform[] stops = FindTransformsNamed(scene, "Pickup Bay Stop");
                for (int i = 0; i < stops.Length; i++)
                {
                    EncapsulatePoint(ref bounds, ref hasBounds, stops[i].position);
                }
            }

            return hasBounds;
        }

        private static void EncapsulatePoint(ref Bounds bounds, ref bool hasBounds, Vector3 point)
        {
            if (!IsFinite(point))
            {
                return;
            }

            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(point);
            }
        }

        private static bool TryGetPreferredStreetscapeBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            Transform geometry = FindTransformDeep(root, "Geometry");
            if (geometry != null && TryGetRendererBounds(geometry.gameObject, out bounds))
            {
                return true;
            }

            return TryGetRendererBoundsIgnoringFarDepth(root, out bounds);
        }

        private static bool TryGetRendererBoundsIgnoringFarDepth(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !RendererHasMesh(renderer) || IsPartOfFarDepth(renderer.transform))
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                if (!IsFinite(rendererBounds.center) || !IsFinite(rendererBounds.size) || rendererBounds.size.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetStreetSurfaceBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !RendererHasMesh(renderer) || IsPartOfFarDepth(renderer.transform) || !IsStreetSurfaceRenderer(renderer))
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                if (!IsFinite(rendererBounds.center) || !IsFinite(rendererBounds.size) || rendererBounds.size.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds;
        }

        private static bool IsStreetSurfaceRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            Transform current = renderer.transform;
            while (current != null)
            {
                string transformName = current.name.ToLowerInvariant();
                if (transformName.Contains("road") ||
                    transformName.Contains("ground") ||
                    transformName.Contains("asphalt") ||
                    transformName.Contains("curb") ||
                    transformName.Contains("line") ||
                    transformName.Contains("painted") ||
                    transformName.Contains("crosswalk"))
                {
                    return true;
                }

                current = current.parent;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                string materialName = material.name.ToLowerInvariant();
                if (materialName.Contains("grd") ||
                    materialName.Contains("asphalt") ||
                    materialName.Contains("curb") ||
                    materialName.Contains("line") ||
                    materialName.Contains("painted") ||
                    materialName.Contains("guideblock"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPartOfFarDepth(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name.IndexOf("far", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool HasFullDemoSceneSignature(Transform root)
        {
            int signatureCount = 0;
            for (int i = 0; i < DemoSignatureChildNames.Length; i++)
            {
                if (FindTransformDeep(root, DemoSignatureChildNames[i]) != null)
                {
                    signatureCount++;
                }
            }

            return signatureCount >= 2;
        }

        private static bool RepairUnsupportedRendererMaterialsForUrp(Transform root)
        {
            Shader targetShader = Shader.Find("Universal Render Pipeline/Lit") ??
                                  Shader.Find("Universal Render Pipeline/Simple Lit") ??
                                  Shader.Find("Standard");
            if (root == null || targetShader == null)
            {
                return false;
            }

            bool changed = false;
            int repairedAssetMaterials = 0;
            int replacedSlots = 0;
            Material fallback = null;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                bool rendererChanged = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        fallback = fallback != null ? fallback : GetOrCreateJapaneseCityFallbackMaterial(targetShader);
                        materials[materialIndex] = fallback;
                        rendererChanged = true;
                        replacedSlots++;
                        continue;
                    }

                    if (!IsUnsupportedMaterial(material))
                    {
                        continue;
                    }

                    string assetPath = AssetDatabase.GetAssetPath(material);
                    if (!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && assetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    {
                        ConvertMaterialToShader(material, targetShader);
                        repairedAssetMaterials++;
                        changed = true;
                    }
                    else
                    {
                        fallback = fallback != null ? fallback : GetOrCreateJapaneseCityFallbackMaterial(targetShader);
                        materials[materialIndex] = fallback;
                        rendererChanged = true;
                        replacedSlots++;
                    }
                }

                if (rendererChanged)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                    changed = true;
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Repaired unsupported Japanese City renderer materials: " + repairedAssetMaterials +
                          " material assets converted, " + replacedSlots + " renderer slots replaced with fallback.");
            }

            return changed;
        }

        private static Material GetOrCreateJapaneseCityFallbackMaterial(Shader targetShader)
        {
            string fallbackPath = AssetRoot + "/Materials/CocoonURPFallback.mat";
            Material fallback = AssetDatabase.LoadAssetAtPath<Material>(fallbackPath);
            if (fallback != null)
            {
                return fallback;
            }

            fallback = new Material(targetShader)
            {
                name = "CocoonURPFallback"
            };
            if (fallback.HasProperty("_BaseColor"))
            {
                fallback.SetColor("_BaseColor", new Color(0.54f, 0.56f, 0.55f));
            }
            if (fallback.HasProperty("_Color"))
            {
                fallback.SetColor("_Color", new Color(0.54f, 0.56f, 0.55f));
            }

            AssetDatabase.CreateAsset(fallback, fallbackPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Material>(fallbackPath);
        }

        private static void ConvertMaterialToShader(Material material, Shader targetShader)
        {
            if (material == null || targetShader == null || material.shader == targetShader)
            {
                return;
            }

            Texture mainTexture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            if (mainTexture == null && material.HasProperty("_MainTex"))
            {
                mainTexture = material.GetTexture("_MainTex");
            }

            Texture normalTexture = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
            Color baseColor = Color.white;
            if (material.HasProperty("_BaseColor"))
            {
                baseColor = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                baseColor = material.GetColor("_Color");
            }

            float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            float smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") :
                material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.35f;
            bool alphaClip = material.IsKeywordEnabled("_ALPHATEST_ON") ||
                             (material.HasProperty("_Cutoff") && material.GetFloat("_Cutoff") > 0.001f);
            bool emission = material.IsKeywordEnabled("_EMISSION");
            Color emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
            int renderQueue = material.renderQueue;

            material.shader = targetShader;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", mainTexture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", mainTexture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (normalTexture != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normalTexture);
                material.EnableKeyword("_NORMALMAP");
            }

            if (alphaClip)
            {
                if (material.HasProperty("_AlphaClip"))
                {
                    material.SetFloat("_AlphaClip", 1f);
                }

                if (material.HasProperty("_Cutoff"))
                {
                    material.SetFloat("_Cutoff", 0.5f);
                }

                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = renderQueue >= 2450 ? renderQueue : 2450;
            }

            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emissionColor);
                }
            }

            EditorUtility.SetDirty(material);
        }

        private static bool IsUnsupportedMaterial(Material material)
        {
            if (material == null)
            {
                return true;
            }

            Shader shader = material.shader;
            string shaderName = shader != null ? shader.name : string.Empty;
            return string.IsNullOrEmpty(shaderName) ||
                   shaderName == "Standard" ||
                   shaderName.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountUnsupportedJapaneseCityMaterials(Transform root)
        {
            var materials = new HashSet<Material>();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] sharedMaterials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material material = sharedMaterials[materialIndex];
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }

            int unsupportedCount = 0;
            foreach (Material material in materials)
            {
                if (IsUnsupportedMaterial(material))
                {
                    unsupportedCount++;
                }
            }

            return unsupportedCount;
        }

        public static bool ValidateCurrentScene()
        {
            var errors = new List<string>();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Japanese City validation failed: no valid scene is loaded.");
                return false;
            }

            Transform streetRoot = FindTransformDeep(scene, "02_Street_Block");
            Transform streetscapeRoot = streetRoot != null ? streetRoot.Find(StreetscapeRootName) : null;
            Transform generatedRoot = streetRoot != null ? streetRoot.Find(GeneratedStreetscapeRootName) : null;
            if (streetRoot == null)
            {
                errors.Add("02_Street_Block is missing.");
            }

            if (streetscapeRoot == null)
            {
                errors.Add(StreetscapeRootName + " is missing.");
            }

            if (generatedRoot != null)
            {
                errors.Add("Old generated Japanese City prefab-scatter layer is still present: " + GeneratedStreetscapeRootName + ".");
            }

            int cityRendererCount = 0;
            int enabledCityColliderCount = 0;
            int unsupportedCityMaterialCount = 0;
            if (streetscapeRoot != null)
            {
                Renderer[] renderers = streetscapeRoot.GetComponentsInChildren<Renderer>(true);
                cityRendererCount = renderers.Count(renderer => IsActuallyVisibleRenderer(renderer));
                Collider[] colliders = streetscapeRoot.GetComponentsInChildren<Collider>(true);
                enabledCityColliderCount = colliders.Count(collider => collider != null && collider.enabled);
                unsupportedCityMaterialCount = CountUnsupportedJapaneseCityMaterials(streetscapeRoot);
                if (!HasFullDemoSceneSignature(streetscapeRoot))
                {
                    errors.Add("Japanese City layer is not the complete JapaneseCity_Demo scene.");
                }

                if (cityRendererCount < 250)
                {
                    errors.Add("Japanese City demo layer has too few active visible renderers: " + cityRendererCount + ".");
                }

                if (enabledCityColliderCount > 0)
                {
                    errors.Add("Japanese City demo layer has enabled colliders: " + enabledCityColliderCount + ".");
                }

                if (unsupportedCityMaterialCount > 0)
                {
                    errors.Add("Japanese City demo layer still has unsupported/pink-risk materials: " + unsupportedCityMaterialCount + ".");
                }

                if (!IsStreetscapeVisibleEnough(streetscapeRoot, out string visibilityReason))
                {
                    errors.Add("Japanese City demo layer has invalid visibility/bounds: " + visibilityReason);
                }
            }

            if (streetRoot != null)
            {
                int legacyRendererCount = CountEnabledLegacyStreetRenderers(streetRoot);
                if (legacyRendererCount > 0)
                {
                    errors.Add("Legacy procedural street renderers are still enabled: " + legacyRendererCount + ".");
                }
            }

            int legacyPickupRendererCount = CountEnabledLegacyPickupBayRenderers(scene);
            if (legacyPickupRendererCount > 0)
            {
                errors.Add("Legacy pickup bay visual renderers are still enabled: " + legacyPickupRendererCount + ".");
            }

            if (FindComponentDeep<CocoonTaxiStateMachine>(scene) == null)
            {
                errors.Add("CocoonTaxiStateMachine is missing.");
            }

            if (FindComponentDeep<CocoonTrafficLanePath>(scene) == null)
            {
                errors.Add("CocoonTrafficLanePath is missing.");
            }

            if (FindComponentDeep<CocoonSafePickupZone>(scene) == null)
            {
                errors.Add("CocoonSafePickupZone is missing.");
            }

            if (FindComponentDeep<CocoonRaiseHandDetector>(scene) == null)
            {
                errors.Add("CocoonRaiseHandDetector is missing.");
            }

            if (FindTransformDeep(scene, "Cocoon Autonomous Taxi") == null)
            {
                errors.Add("Cocoon Autonomous Taxi is missing.");
            }

            if (FindTransformDeep(scene, "Door-side Onboarding UI") == null)
            {
                errors.Add("Door-side Onboarding UI is missing.");
            }

            int pickupStopCount = CountTransformsNamed(scene, "Pickup Bay Stop");
            if (pickupStopCount < 6)
            {
                errors.Add("Expected at least 6 Pickup Bay Stop anchors, found " + pickupStopCount + ".");
            }

            if (errors.Count > 0)
            {
                for (int i = 0; i < errors.Count; i++)
                {
                    Debug.LogError("Japanese City validation failed: " + errors[i]);
                }

                return false;
            }

            Debug.Log("Japanese City validation passed: " + cityRendererCount + " enabled full-demo city renderers, " + pickupStopCount + " pickup stop anchors, 0 enabled city colliders, " + unsupportedCityMaterialCount + " unsupported city materials.");
            return true;
        }

        private static bool ImportJapaneseCityPackageFromDownloads()
        {
            string packagePath = FindJapaneseCityPackage();
            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogWarning("Japanese City package was not found under the current user's Downloads folder.");
                return false;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();
            return true;
        }

        private static string FindJapaneseCityPackage()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = Path.Combine(userProfile, "Downloads");
            if (!Directory.Exists(downloads))
            {
                return null;
            }

            try
            {
                return Directory.EnumerateFiles(downloads, "*.unitypackage", SearchOption.AllDirectories)
                    .Where(path => path.IndexOf("Japanese City", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("JapaneseCity", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning("Unable to scan all Downloads subfolders for Japanese City package: " + exception.Message);
                return null;
            }
        }

        private static void BuildRoadSurfaceOverlays(Transform root)
        {
            Transform parent = CreateGroup("Road Surface Overlays", root);
            string straightRoad = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcRoad22mA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcRoad22mB.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcRoad22mC.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcRoad12mA.prefab");
            string crossRoad = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcRoadCross22mA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcRoadT22mA.prefab");

            for (int i = 0; i < 4; i++)
            {
                float z = -33f + i * 22f;
                PlacePrefab("Japanese West Avenue Road " + i, straightRoad, parent, new Vector3(0f, 0.035f, z), Quaternion.identity, new Vector2(6.35f, 21.6f), true);
                PlacePrefab("Japanese East Avenue Road " + i, straightRoad, parent, new Vector3(34f, 0.035f, z), Quaternion.identity, new Vector2(6.35f, 21.6f), true);
            }

            for (int i = 0; i < 2; i++)
            {
                float x = 6f + i * 22f;
                PlacePrefab("Japanese North Street Road " + i, straightRoad, parent, new Vector3(x, 0.037f, 28f), Quaternion.Euler(0f, 90f, 0f), new Vector2(21.6f, 6.35f), true);
                PlacePrefab("Japanese South Street Road " + i, straightRoad, parent, new Vector3(x, 0.037f, -28f), Quaternion.Euler(0f, 90f, 0f), new Vector2(21.6f, 6.35f), true);
            }

            PlacePrefab("Japanese Crossroad NW", crossRoad, parent, new Vector3(0f, 0.04f, 28f), Quaternion.identity, new Vector2(8.6f, 8.6f), true);
            PlacePrefab("Japanese Crossroad SW", crossRoad, parent, new Vector3(0f, 0.04f, -28f), Quaternion.identity, new Vector2(8.6f, 8.6f), true);
            PlacePrefab("Japanese Crossroad NE", crossRoad, parent, new Vector3(34f, 0.04f, 28f), Quaternion.identity, new Vector2(8.6f, 8.6f), true);
            PlacePrefab("Japanese Crossroad SE", crossRoad, parent, new Vector3(34f, 0.04f, -28f), Quaternion.identity, new Vector2(8.6f, 8.6f), true);
        }

        private static void BuildRoadMarkings(Transform root)
        {
            Transform parent = CreateGroup("Road Markings", root);
            string dashedLine = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdDashedLineA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdDashedLineA_L.prefab");
            string solidLine = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdSolidLineA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdSolidLineA_L.prefab");
            string crosswalk = FirstExistingAsset("Assets/JapaneseCity/Prefabs/Grounds/jcGrdCrosswalkA.prefab");
            string stopLine = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdStopLineA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdStopLineB.prefab");
            string stopText = FirstExistingAsset("Assets/JapaneseCity/Prefabs/Grounds/jcGrdStopTxt.prefab");
            string speedLimit = FirstExistingAsset("Assets/JapaneseCity/Prefabs/Grounds/jcGrdSpeedLimit.prefab");
            string arrowForward = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdArrowA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdArrowB.prefab");

            for (int i = 0; i < 8; i++)
            {
                float z = -29.5f + i * 8.4f;
                PlacePrefab("Japanese West Avenue Dash " + i, dashedLine, parent, new Vector3(0f, 0.07f, z), Quaternion.identity, new Vector2(0.32f, 3.1f), true);
                PlacePrefab("Japanese East Avenue Dash " + i, dashedLine, parent, new Vector3(34f, 0.07f, z), Quaternion.identity, new Vector2(0.32f, 3.1f), true);
            }

            for (int i = 0; i < 3; i++)
            {
                float x = 6.5f + i * 10.5f;
                PlacePrefab("Japanese North Street Dash " + i, dashedLine, parent, new Vector3(x, 0.071f, 28f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.32f, 3.1f), true);
                PlacePrefab("Japanese South Street Dash " + i, dashedLine, parent, new Vector3(x, 0.071f, -28f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.32f, 3.1f), true);
            }

            PlacePrefab("Japanese West Avenue Left Edge", solidLine, parent, new Vector3(-2.9f, 0.072f, 0f), Quaternion.identity, new Vector2(0.22f, 59f), true);
            PlacePrefab("Japanese West Avenue Right Edge", solidLine, parent, new Vector3(2.9f, 0.072f, 0f), Quaternion.identity, new Vector2(0.22f, 59f), true);
            PlacePrefab("Japanese East Avenue Left Edge", solidLine, parent, new Vector3(31.1f, 0.072f, 0f), Quaternion.identity, new Vector2(0.22f, 59f), true);
            PlacePrefab("Japanese East Avenue Right Edge", solidLine, parent, new Vector3(36.9f, 0.072f, 0f), Quaternion.identity, new Vector2(0.22f, 59f), true);
            PlacePrefab("Japanese North Street Near Edge", solidLine, parent, new Vector3(17f, 0.073f, 25.1f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.22f, 35f), true);
            PlacePrefab("Japanese North Street Far Edge", solidLine, parent, new Vector3(17f, 0.073f, 30.9f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.22f, 35f), true);
            PlacePrefab("Japanese South Street Near Edge", solidLine, parent, new Vector3(17f, 0.073f, -25.1f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.22f, 35f), true);
            PlacePrefab("Japanese South Street Far Edge", solidLine, parent, new Vector3(17f, 0.073f, -30.9f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.22f, 35f), true);

            Vector3[] intersections =
            {
                new Vector3(0f, 0.076f, 28f),
                new Vector3(0f, 0.076f, -28f),
                new Vector3(34f, 0.076f, 28f),
                new Vector3(34f, 0.076f, -28f)
            };

            for (int i = 0; i < intersections.Length; i++)
            {
                Vector3 point = intersections[i];
                PlacePrefab("Japanese Crosswalk North-South " + i, crosswalk, parent, point + new Vector3(0f, 0f, 4.4f), Quaternion.identity, new Vector2(5.8f, 1.45f), true);
                PlacePrefab("Japanese Crosswalk East-West " + i, crosswalk, parent, point + new Vector3(4.4f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(5.8f, 1.45f), true);
            }

            PlacePrefab("Japanese Stop Line West North", stopLine, parent, new Vector3(0f, 0.079f, 22.5f), Quaternion.Euler(0f, 90f, 0f), new Vector2(4.8f, 0.35f), true);
            PlacePrefab("Japanese Stop Line West South", stopLine, parent, new Vector3(0f, 0.079f, -22.5f), Quaternion.Euler(0f, 90f, 0f), new Vector2(4.8f, 0.35f), true);
            PlacePrefab("Japanese Stop Line East North", stopLine, parent, new Vector3(34f, 0.079f, 22.5f), Quaternion.Euler(0f, 90f, 0f), new Vector2(4.8f, 0.35f), true);
            PlacePrefab("Japanese Stop Line East South", stopLine, parent, new Vector3(34f, 0.079f, -22.5f), Quaternion.Euler(0f, 90f, 0f), new Vector2(4.8f, 0.35f), true);
            PlacePrefab("Japanese Stop Text West", stopText, parent, new Vector3(0f, 0.081f, 18.7f), Quaternion.identity, new Vector2(1.6f, 2.6f), true);
            PlacePrefab("Japanese Stop Text East", stopText, parent, new Vector3(34f, 0.081f, -18.7f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.6f, 2.6f), true);
            PlacePrefab("Japanese Speed Limit West", speedLimit, parent, new Vector3(0f, 0.082f, -5f), Quaternion.identity, new Vector2(1.8f, 2.2f), true);
            PlacePrefab("Japanese Speed Limit East", speedLimit, parent, new Vector3(34f, 0.082f, 5f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.8f, 2.2f), true);
            PlacePrefab("Japanese Forward Arrow West", arrowForward, parent, new Vector3(0f, 0.083f, -17f), Quaternion.identity, new Vector2(1.6f, 3.4f), true);
            PlacePrefab("Japanese Forward Arrow East", arrowForward, parent, new Vector3(34f, 0.083f, 17f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.6f, 3.4f), true);
        }

        private static void BuildPickupBayStreetscape(Transform root)
        {
            Transform parent = CreateGroup("Pickup Bay Japanese Markings", root);
            string asphaltPatch = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdAsphaltPatchA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdAsphaltPatchB.prefab");
            string guideBlock = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdGuideBlockA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdGuideBlockB.prefab");
            string solidLine = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdSolidLineA.prefab",
                "Assets/JapaneseCity/Prefabs/Grounds/jcGrdSolidLineA_L.prefab");

            PlacePickupBayVisual("West Pickup Bay A", asphaltPatch, guideBlock, solidLine, parent, new Vector3(-4.05f, 0.088f, 10f), Quaternion.LookRotation(Vector3.back, Vector3.up));
            PlacePickupBayVisual("West Pickup Bay B", asphaltPatch, guideBlock, solidLine, parent, new Vector3(-4.05f, 0.088f, -10f), Quaternion.LookRotation(Vector3.back, Vector3.up));
            PlacePickupBayVisual("South Pickup Bay", asphaltPatch, guideBlock, solidLine, parent, new Vector3(14f, 0.088f, -32.05f), Quaternion.LookRotation(Vector3.right, Vector3.up));
            PlacePickupBayVisual("East Pickup Bay A", asphaltPatch, guideBlock, solidLine, parent, new Vector3(38.05f, 0.088f, -10f), Quaternion.LookRotation(Vector3.forward, Vector3.up));
            PlacePickupBayVisual("East Pickup Bay B", asphaltPatch, guideBlock, solidLine, parent, new Vector3(38.05f, 0.088f, 18f), Quaternion.LookRotation(Vector3.forward, Vector3.up));
            PlacePickupBayVisual("North Pickup Bay", asphaltPatch, guideBlock, solidLine, parent, new Vector3(20f, 0.088f, 32.05f), Quaternion.LookRotation(Vector3.left, Vector3.up));
        }

        private static void PlacePickupBayVisual(string name, string asphaltPatch, string guideBlock, string solidLine, Transform parent, Vector3 center, Quaternion rotation)
        {
            PlacePrefab(name + " Asphalt Patch", asphaltPatch, parent, center, rotation, new Vector2(2.4f, 5.6f), true);
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            PlacePrefab(name + " Curb Guide", guideBlock, parent, center - right * 1.55f, rotation, new Vector2(0.45f, 5.3f), true);
            PlacePrefab(name + " Outer White Line", solidLine, parent, center + right * 1.24f, rotation, new Vector2(0.18f, 5.2f), true);
            PlacePrefab(name + " Front White Line", solidLine, parent, center + forward * 2.55f, rotation * Quaternion.Euler(0f, 90f, 0f), new Vector2(0.18f, 2.25f), true);
            PlacePrefab(name + " Rear White Line", solidLine, parent, center - forward * 2.55f, rotation * Quaternion.Euler(0f, 90f, 0f), new Vector2(0.18f, 2.25f), true);
        }

        private static void BuildBuildingRows(Transform root)
        {
            Transform parent = CreateGroup("Building Rows", root);
            string[] prefabs =
            {
                "Assets/JapaneseCity/Prefabs/Buildings/jctBldSetA.prefab",
                "Assets/JapaneseCity/Prefabs/Buildings/jctBldSetC.prefab",
                "Assets/JapaneseCity/Prefabs/Buildings/jctBldSetF.prefab",
                "Assets/JapaneseCity/Prefabs/Buildings/jctBld24_A_mdl.prefab",
                "Assets/JapaneseCity/Prefabs/Buildings/jctBld24_C_tall.prefab",
                "Assets/JapaneseCity/Prefabs/Buildings/jctBld50_A_low_tall.prefab",
                "Assets/JapaneseCity/Prefabs/Buildings/jctBldLowSetA.prefab",
                "Assets/JapaneseCity/Prefabs/Buildings/jctBldLowSetD.prefab"
            };

            for (int i = 0; i < 10; i++)
            {
                float z = -34f + i * 7.6f;
                PlacePrefab("Japanese West Building " + i, Pick(prefabs, i), parent, new Vector3(-9.7f, 0f, z), Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector2(5.2f, 6.9f), true);
                PlacePrefab("Japanese East Building " + i, Pick(prefabs, i + 3), parent, new Vector3(43.1f, 0f, z + 1.2f), Quaternion.LookRotation(Vector3.left, Vector3.up), new Vector2(5.2f, 6.9f), true);
            }

            for (int i = 0; i < 7; i++)
            {
                float x = -1.5f + i * 6.4f;
                PlacePrefab("Japanese North Building " + i, Pick(prefabs, i + 5), parent, new Vector3(x, 0f, 37.9f), Quaternion.LookRotation(Vector3.back, Vector3.up), new Vector2(5.9f, 4.8f), true);
                PlacePrefab("Japanese South Building " + i, Pick(prefabs, i + 1), parent, new Vector3(x + 1.7f, 0f, -37.9f), Quaternion.LookRotation(Vector3.forward, Vector3.up), new Vector2(5.9f, 4.8f), true);
            }
        }

        private static void BuildStreetProps(Transform root)
        {
            Transform parent = CreateGroup("Street Props", root);
            string streetLight = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Props/jctStreetLightA.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctStreetLightB.prefab");
            string trafficLight = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Props/jctTrafficLightA.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctTrafficLightC.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctTrafficLightE.prefab");
            string bench = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Props/jctBenchA.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctStreetStandA.prefab");
            string planter = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Props/jctRoadConeA.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctGuardRailA.prefab");
            string utilityHole = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Props/jctUtilityholeA.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctUtilityholeB.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctUtilityholeC.prefab");
            string streetSign = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Props/jctStreetSignA.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctStreetSignB.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctStreetSignC.prefab");
            string guardRail = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Props/jctGuardRailA.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctGuardRailB.prefab",
                "Assets/JapaneseCity/Prefabs/Props/jctGuardRailC.prefab");

            for (int i = 0; i < 9; i++)
            {
                float z = -34f + i * 8.5f;
                PlacePrefab("Japanese Street Light West " + i, streetLight, parent, new Vector3(-3.55f, 0f, z), Quaternion.identity, new Vector2(0.8f, 0.8f), true);
                PlacePrefab("Japanese Street Light East " + i, streetLight, parent, new Vector3(37.55f, 0f, z + 2.2f), Quaternion.identity, new Vector2(0.8f, 0.8f), true);
            }

            PlacePrefab("Japanese Traffic Light NW", trafficLight, parent, new Vector3(-2.9f, 0f, 25.2f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.9f, 0.9f), true);
            PlacePrefab("Japanese Traffic Light SW", trafficLight, parent, new Vector3(-2.9f, 0f, -25.2f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.9f, 0.9f), true);
            PlacePrefab("Japanese Traffic Light NE", trafficLight, parent, new Vector3(36.9f, 0f, 25.2f), Quaternion.identity, new Vector2(0.9f, 0.9f), true);
            PlacePrefab("Japanese Traffic Light SE", trafficLight, parent, new Vector3(36.9f, 0f, -25.2f), Quaternion.identity, new Vector2(0.9f, 0.9f), true);

            for (int i = 0; i < 5; i++)
            {
                float z = -24f + i * 12f;
                PlacePrefab("Japanese Bench West " + i, bench, parent, new Vector3(-6.1f, 0f, z), Quaternion.Euler(0f, 90f, 0f), new Vector2(1.2f, 0.7f), true);
                PlacePrefab("Japanese Bench East " + i, bench, parent, new Vector3(39.1f, 0f, z + 5f), Quaternion.Euler(0f, -90f, 0f), new Vector2(1.2f, 0.7f), true);
                PlacePrefab("Japanese Pickup Marker " + i, planter, parent, new Vector3(-6.55f, 0f, -28f + i * 9.5f), Quaternion.identity, new Vector2(0.7f, 0.7f), true);
            }

            for (int i = 0; i < 7; i++)
            {
                float z = -27f + i * 9f;
                PlacePrefab("Japanese Utilityhole West " + i, utilityHole, parent, new Vector3(1.35f, 0.09f, z), Quaternion.Euler(0f, 25f * i, 0f), new Vector2(0.85f, 0.85f), true);
                PlacePrefab("Japanese Utilityhole East " + i, utilityHole, parent, new Vector3(32.65f, 0.09f, z + 3.8f), Quaternion.Euler(0f, -18f * i, 0f), new Vector2(0.85f, 0.85f), true);
            }

            PlacePrefab("Japanese Street Sign West Pickup", streetSign, parent, new Vector3(-6.15f, 0f, 10f), Quaternion.Euler(0f, 88f, 0f), new Vector2(0.9f, 0.55f), true);
            PlacePrefab("Japanese Street Sign East Pickup", streetSign, parent, new Vector3(40.25f, 0f, 18f), Quaternion.Euler(0f, -92f, 0f), new Vector2(0.9f, 0.55f), true);
            PlacePrefab("Japanese Guard Rail West North", guardRail, parent, new Vector3(-4.25f, 0f, 26.6f), Quaternion.identity, new Vector2(0.45f, 5.4f), true);
            PlacePrefab("Japanese Guard Rail West South", guardRail, parent, new Vector3(-4.25f, 0f, -26.6f), Quaternion.identity, new Vector2(0.45f, 5.4f), true);
            PlacePrefab("Japanese Guard Rail East North", guardRail, parent, new Vector3(38.25f, 0f, 26.6f), Quaternion.identity, new Vector2(0.45f, 5.4f), true);
            PlacePrefab("Japanese Guard Rail East South", guardRail, parent, new Vector3(38.25f, 0f, -26.6f), Quaternion.identity, new Vector2(0.45f, 5.4f), true);
        }

        private static void BuildFoliageAndDepth(Transform root)
        {
            Transform parent = CreateGroup("Foliage And Distant City", root);
            string tree = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Foliage/jctTreeA.prefab",
                "Assets/JapaneseCity/Prefabs/Foliage/jctHedgeA.prefab");
            string hedge = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Foliage/jctHedgeA.prefab",
                "Assets/JapaneseCity/Prefabs/Foliage/jctHedgeB.prefab",
                "Assets/JapaneseCity/Prefabs/Foliage/jctHedgeC.prefab");
            string farBuildings = FirstExistingAsset(
                "Assets/JapaneseCity/Prefabs/Buildings/jctFarBuildings.prefab",
                "Assets/JapaneseCity/Prefabs/Buildings/jctBldFarA.prefab");

            for (int i = 0; i < 7; i++)
            {
                float z = -31f + i * 10.4f;
                PlacePrefab("Japanese Tree West " + i, tree, parent, new Vector3(-7.15f, 0f, z), Quaternion.Euler(0f, 35f * i, 0f), new Vector2(2.2f, 2.2f), true);
                PlacePrefab("Japanese Tree East " + i, tree, parent, new Vector3(41.25f, 0f, z + 2.6f), Quaternion.Euler(0f, -28f * i, 0f), new Vector2(2.2f, 2.2f), true);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = 4f + i * 8.6f;
                PlacePrefab("Japanese Hedge North " + i, hedge, parent, new Vector3(x, 0f, 34.4f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.85f, 4.4f), true);
                PlacePrefab("Japanese Hedge South " + i, hedge, parent, new Vector3(x + 1.2f, 0f, -34.4f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.85f, 4.4f), true);
            }

            PlacePrefab("Japanese Far Skyline North", farBuildings, parent, new Vector3(17f, 0f, 55f), Quaternion.LookRotation(Vector3.back, Vector3.up), new Vector2(35f, 8f), true);
            PlacePrefab("Japanese Far Skyline South", farBuildings, parent, new Vector3(17f, 0f, -55f), Quaternion.LookRotation(Vector3.forward, Vector3.up), new Vector2(35f, 8f), true);
        }

        private static GameObject PlacePrefab(string name, string assetPath, Transform parent, Vector3 groundCenter, Quaternion rotation, Vector2 footprint, bool visualOnly)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }

            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = rotation;
            instance.transform.localScale = Vector3.one;
            FitAndPlace(instance, groundCenter, footprint);
            SetStaticRecursive(instance);
            if (visualOnly)
            {
                DisableColliders(instance);
            }

            return instance;
        }

        private static void FitAndPlace(GameObject instance, Vector3 groundCenter, Vector2 footprint)
        {
            if (!TryGetRendererBounds(instance, out Bounds bounds))
            {
                instance.transform.position = groundCenter;
                return;
            }

            float scaleX = footprint.x / Mathf.Max(0.01f, bounds.size.x);
            float scaleZ = footprint.y / Mathf.Max(0.01f, bounds.size.z);
            float scale = Mathf.Clamp(Mathf.Min(scaleX, scaleZ), 0.01f, 10f);
            instance.transform.localScale *= scale;

            if (!TryGetRendererBounds(instance, out bounds))
            {
                instance.transform.position = groundCenter;
                return;
            }

            Vector3 currentGroundCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            instance.transform.position += groundCenter - currentGroundCenter;
        }

        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetColliderBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            bool hasBounds = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                Bounds colliderBounds = collider.bounds;
                if (!IsFinite(colliderBounds.center) || !IsFinite(colliderBounds.size) || colliderBounds.size.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = colliderBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(colliderBounds);
                }
            }

            return hasBounds;
        }

        private static bool ForceStreetscapeVisible(Transform streetscapeRoot)
        {
            bool changed = false;
            Transform[] transforms = streetscapeRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && !transforms[i].gameObject.activeSelf)
                {
                    transforms[i].gameObject.SetActive(true);
                    changed = true;
                }
            }

            Renderer[] renderers = streetscapeRoot.GetComponentsInChildren<Renderer>(true);
            int enabledCount = 0;
            int missingMeshCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!renderer.enabled)
                {
                    renderer.enabled = true;
                    changed = true;
                }

                if (RendererHasMesh(renderer))
                {
                    enabledCount++;
                }
                else
                {
                    missingMeshCount++;
                }
            }

            Collider[] colliders = streetscapeRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    colliders[i].enabled = false;
                    changed = true;
                }
            }

            Debug.Log("Japanese City renderer repair: " + enabledCount + " mesh-backed renderers visible, " + missingMeshCount + " renderers without mesh, colliders disabled.");
            return changed;
        }

        private static bool IsStreetscapeVisibleEnough(Transform streetscapeRoot, out string reason)
        {
            reason = "no streetscape root";
            if (streetscapeRoot == null || !streetscapeRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            Renderer[] renderers = streetscapeRoot.GetComponentsInChildren<Renderer>(true);
            int visibleRendererCount = 0;
            int meshBackedCount = 0;
            bool hasBounds = false;
            Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!IsActuallyVisibleRenderer(renderer))
                {
                    continue;
                }

                visibleRendererCount++;
                if (RendererHasMesh(renderer))
                {
                    meshBackedCount++;
                }

                Bounds bounds = renderer.bounds;
                if (!IsFinite(bounds.center) || !IsFinite(bounds.size) || bounds.size.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(bounds);
                }
            }

            if (visibleRendererCount < 80)
            {
                reason = "only " + visibleRendererCount + " active visible renderers";
                return false;
            }

            if (meshBackedCount < 60)
            {
                reason = "only " + meshBackedCount + " mesh-backed renderers";
                return false;
            }

            if (!hasBounds)
            {
                reason = "no finite renderer bounds";
                return false;
            }

            Vector3 size = combinedBounds.size;
            Vector3 center = combinedBounds.center;
            if (size.x < 25f || size.z < 35f || size.y < 1f)
            {
                reason = "bounds too small: center " + center.ToString("F2") + ", size " + size.ToString("F2");
                return false;
            }

            if (Mathf.Abs(center.x - 17f) > 1000f || Mathf.Abs(center.z) > 1000f)
            {
                reason = "bounds far from Cocoon block: center " + center.ToString("F2") + ", size " + size.ToString("F2");
                return false;
            }

            reason = visibleRendererCount + " active visible renderers, " + meshBackedCount + " mesh-backed renderers, bounds center " + center.ToString("F2") + ", size " + size.ToString("F2");
            return true;
        }

        private static bool IsActuallyVisibleRenderer(Renderer renderer)
        {
            return renderer != null &&
                   renderer.enabled &&
                   renderer.gameObject.activeInHierarchy &&
                   RendererHasMesh(renderer);
        }

        private static bool RendererHasMesh(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                return meshFilter.sharedMesh != null;
            }

            SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
            return skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool HideLegacyStreetVisuals(Transform streetRoot)
        {
            bool changed = false;
            Transform[] transforms = streetRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null || transform.name == StreetscapeRootName || IsChildOfStreetscape(transform))
                {
                    continue;
                }

                string name = transform.name;
                if (!IsLegacyStreetVisualName(name))
                {
                    continue;
                }

                changed |= SetRenderersAndCollidersEnabled(transform, false);
            }

            return changed;
        }

        private static bool HideLegacyPickupBayVisuals(Scene scene)
        {
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform transform = transforms[i];
                    if (transform == null || IsChildOfStreetscape(transform) || !IsLegacyPickupBayVisualName(transform.name))
                    {
                        continue;
                    }

                    changed |= SetRenderersAndCollidersEnabled(transform, false);
                }
            }

            return changed;
        }

        private static bool IsLegacyStreetVisualName(string name)
        {
            return name == "Roads" ||
                   name == "Sidewalks" ||
                   name == "Curbs" ||
                   name == "Lane Markings" ||
                   name == "Crosswalks" ||
                   name == "Buildings" ||
                   name == "Street Lights" ||
                   name == "Street Furniture" ||
                   name == "Expanded Roads" ||
                   name == "Expanded Sidewalks" ||
                   name == "Expanded Curbs" ||
                   name == "Expanded Crosswalks" ||
                   name == "Expanded Buildings" ||
                   name == "Expanded Street Furniture" ||
                   name.Contains("Building") ||
                   name.Contains("Planter") ||
                   name.Contains("Bench") ||
                   name.Contains("Shelter") ||
                   name.Contains("Street Light") ||
                   name.Contains("Lane Dash") ||
                   name.Contains("Crosswalk") ||
                   name.Contains("Curb") ||
                   name.Contains("Sidewalk") ||
                   name.Contains("Road Surface") ||
                   name.Contains("Road Spine");
        }

        private static bool IsLegacyPickupBayVisualName(string name)
        {
            return name == "Recessed Bay Asphalt" ||
                   name == "Outer Curb Return" ||
                   name == "Front Curb Return" ||
                   name == "Rear Curb Return" ||
                   name == "White Bay Edge" ||
                   name == "Stop Bar" ||
                   name.StartsWith("Pickup Sign", StringComparison.Ordinal) ||
                   name.Contains("Expanded Road") ||
                   name.Contains("Expanded Sidewalk") ||
                   name.Contains("Expanded Curb") ||
                   name.Contains("Expanded Crosswalk") ||
                   name.Contains("Expanded Building") ||
                   name.Contains("Expanded Street Furniture");
        }

        private static int CountEnabledLegacyStreetRenderers(Transform streetRoot)
        {
            int count = 0;
            Transform[] transforms = streetRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null || IsChildOfStreetscape(transform) || !IsLegacyStreetVisualName(transform.name))
                {
                    continue;
                }

                Renderer[] renderers = transform.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    if (renderers[rendererIndex] != null && renderers[rendererIndex].enabled)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountEnabledLegacyPickupBayRenderers(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform transform = transforms[i];
                    if (transform == null || IsChildOfStreetscape(transform) || !IsLegacyPickupBayVisualName(transform.name))
                    {
                        continue;
                    }

                    Renderer[] renderers = transform.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        if (renderers[rendererIndex] != null && renderers[rendererIndex].enabled)
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private static bool IsChildOfStreetscape(Transform transform)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.name == StreetscapeRootName)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool SetRenderersAndCollidersEnabled(Transform root, bool enabled)
        {
            bool changed = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled != enabled)
                {
                    renderers[i].enabled = enabled;
                    changed = true;
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].enabled != enabled)
                {
                    colliders[i].enabled = enabled;
                    changed = true;
                }
            }

            return changed;
        }

        private static void DisableColliders(GameObject instance)
        {
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static void SetStaticRecursive(GameObject instance)
        {
            Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.isStatic = true;
            }
        }

        private static Transform FindOrCreateRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }
            }

            return new GameObject(name).transform;
        }

        private static Transform FindTopLevelTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != null && root.name == name)
                {
                    return root.transform;
                }
            }

            return null;
        }

        private static Transform FindTransformDeep(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == name)
                    {
                        return transforms[i];
                    }
                }
            }

            return null;
        }

        private static Transform FindTransformDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static T FindComponentDeep<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static int CountTransformsNamed(Scene scene, string name)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == name)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static Transform[] FindTransformsNamed(Scene scene, string name)
        {
            var matches = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i] != null && transforms[i].name == name)
                    {
                        matches.Add(transforms[i]);
                    }
                }
            }

            return matches.ToArray();
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static string FirstExistingAsset(params string[] assetPaths)
        {
            for (int i = 0; i < assetPaths.Length; i++)
            {
                if (!string.IsNullOrEmpty(assetPaths[i]) && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPaths[i]) != null)
                {
                    return assetPaths[i];
                }
            }

            return null;
        }

        private static string Pick(string[] assetPaths, int index)
        {
            if (assetPaths == null || assetPaths.Length == 0)
            {
                return null;
            }

            for (int offset = 0; offset < assetPaths.Length; offset++)
            {
                string candidate = assetPaths[Mathf.Abs(index + offset) % assetPaths.Length];
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidate) != null)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
