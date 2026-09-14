using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CocoonPrototype.Editor
{
    public static class CocoonImportedTaxiModelBuilder
    {
        public const string ObjModelPath = "Assets/ImportedAssets/CocoonTaxi/ZOOX.obj";
        public const string ModelPath = ObjModelPath;
        public const string InteriorModelPath = "Assets/ImportedAssets/CocoonTaxi/solid.obj";
        public const string FusionArchivePath = "Assets/ImportedAssets/CocoonTaxi/ZOOX.f3z";
        public const string PreviewTexturePath = "Assets/ImportedAssets/CocoonTaxi/ZOOX-preview.png";
        public const string ModelInstanceName = "Final Taxi Body";
        public const string InteriorModelInstanceName = "Final Taxi Interior Structure";
        public const float CanonicalFinalTaxiBodyScale = 0.003595809f;

        private const float WorldCanvasPixelsPerUnit = 768f;
        private const float ExteriorScreenReferenceWidth = 1180f;
        private const float ExteriorScreenSourceWidth = 23040f;
        private const float ExteriorScreenSourceHeight = 2080f;
        private const string ExteriorScreenResourcePath = "ExteriorScreens/ext-screen";
        private static readonly Vector3 DoorPanelLocalPosition = new Vector3(0.24f, 0.34f, 0.55f);
        private static readonly Vector3 CanonicalFinalTaxiBodyLocalEulerAngles = new Vector3(-90f, 0f, 0f);
        private static readonly Vector3 TargetModelSize = new Vector3(2.08f, 1.75f, 3.45f);

        public static bool TryBuild(
            Transform taxiRoot,
            Material bodyMat,
            Material glassMat,
            Material tireMat,
            Material lightMat,
            out Transform doorHinge,
            out Transform paymentReader,
            out Renderer paymentReaderRenderer,
            out Renderer bodyRenderer,
            out Renderer[] lights,
            out Text windshieldText,
            out Text rearWindshieldText)
        {
            doorHinge = null;
            paymentReader = null;
            paymentReaderRenderer = null;
            bodyRenderer = null;
            lights = new Renderer[0];
            windshieldText = null;
            rearWindshieldText = null;

            if (taxiRoot == null)
            {
                return false;
            }

            GameObject modelAsset = LoadRenderableModelAsset();
            if (modelAsset == null && !HasFusionArchiveSource())
            {
                return false;
            }

            var visualRoot = new GameObject("Taxi Pod Visuals");
            visualRoot.transform.SetParent(taxiRoot, false);

            GameObject modelInstance;
            if (modelAsset != null)
            {
                modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (modelInstance == null)
                {
                    modelInstance = Object.Instantiate(modelAsset);
                }

                modelInstance.name = ModelInstanceName;
                modelInstance.transform.SetParent(visualRoot.transform, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
                RemoveColliders(modelInstance.transform);
                if (IsCurrentZooXObjAsset(modelAsset))
                {
                    ApplyCanonicalFinalTaxiBodyTransform(modelInstance.transform);
                }
                else
                {
                    NormalizeImportedModel(modelInstance.transform);
                }
            }
            else
            {
                modelInstance = CreateFusionSourceProxy(visualRoot.transform, bodyMat, glassMat, tireMat);
            }

            bodyRenderer = FindLargestRenderer(modelInstance.transform);
            EnsureTaxiInteriorStructure(taxiRoot);

            windshieldText = CreateWindshieldDisplay(
                "Taxi Front Windshield Display",
                visualRoot.transform,
                new Vector3(0f, 1.22f, 1.54f),
                Quaternion.Euler(-13f, 0f, 0f),
                new Vector3(-0.00132f, 0.00132f, 0.00132f));
            rearWindshieldText = CreateWindshieldDisplay(
                "Taxi Rear Windshield Display",
                visualRoot.transform,
                new Vector3(0f, 1.12f, -1.91f),
                Quaternion.identity,
                new Vector3(0.00108f, 0.00108f, 0.00108f));

            return true;
        }

        public static bool HasRenderableModelAsset()
        {
            return EnsureRenderableModelAssetImported();
        }

        public static bool EnsureRenderableModelAssetImported()
        {
            return EnsureModelAssetImported(ObjModelPath) || EnsureModelAssetImported(ModelPath);
        }

        public static bool HasFusionArchiveSource()
        {
            return AssetDatabase.LoadAssetAtPath<Object>(FusionArchivePath) != null || File.Exists(FusionArchivePath);
        }

        public static bool HasAnyTaxiSourceAsset()
        {
            return HasRenderableModelAsset() || HasFusionArchiveSource();
        }

        public static string ActiveRenderableModelPath()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ObjModelPath) != null)
            {
                return ObjModelPath;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) != null)
            {
                return ModelPath;
            }

            return string.Empty;
        }

        public static bool EnsureTaxiInteriorStructure(Transform taxiRoot)
        {
            Transform visualRoot = taxiRoot != null ? taxiRoot.Find("Taxi Pod Visuals") : null;
            if (visualRoot == null)
            {
                return false;
            }

            GameObject interiorAsset = LoadInteriorModelAsset();
            if (interiorAsset == null)
            {
                return false;
            }

            Transform existing = visualRoot.Find(InteriorModelInstanceName);
            if (existing != null)
            {
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing.gameObject);
                if (!string.IsNullOrEmpty(sourcePath) &&
                    !sourcePath.Replace('\\', '/').Equals(InteriorModelPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    Object.DestroyImmediate(existing.gameObject);
                    existing = null;
                }
                else
                {
                    bool changed = !IsCanonicalFinalTaxiBodyTransform(existing);
                    ApplyCanonicalFinalTaxiBodyTransform(existing);
                    RemoveColliders(existing);
                    return changed;
                }
            }

            GameObject interiorInstance = PrefabUtility.InstantiatePrefab(interiorAsset) as GameObject;
            if (interiorInstance == null)
            {
                interiorInstance = Object.Instantiate(interiorAsset);
            }

            interiorInstance.name = InteriorModelInstanceName;
            interiorInstance.transform.SetParent(visualRoot, false);
            ApplyCanonicalFinalTaxiBodyTransform(interiorInstance.transform);
            RemoveColliders(interiorInstance.transform);
            return true;
        }

        private static GameObject LoadRenderableModelAsset()
        {
            GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(ObjModelPath);
            if (obj != null)
            {
                return obj;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        }

        private static GameObject LoadInteriorModelAsset()
        {
            EnsureModelAssetImported(InteriorModelPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(InteriorModelPath);
        }

        public static void ApplyCanonicalFinalTaxiBodyTransform(Transform modelRoot)
        {
            if (modelRoot == null)
            {
                return;
            }

            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = Quaternion.Euler(CanonicalFinalTaxiBodyLocalEulerAngles);
            modelRoot.localScale = Vector3.one * CanonicalFinalTaxiBodyScale;
        }

        public static void ApplyCanonicalImportedAccessoryTransform(Transform modelRoot)
        {
            ApplyCanonicalFinalTaxiBodyTransform(modelRoot);
        }

        public static bool IsCanonicalFinalTaxiBodyTransform(Transform modelRoot)
        {
            if (modelRoot == null)
            {
                return false;
            }

            return Vector3.Distance(modelRoot.localPosition, Vector3.zero) <= 0.000001f &&
                   Quaternion.Angle(modelRoot.localRotation, Quaternion.Euler(CanonicalFinalTaxiBodyLocalEulerAngles)) <= 0.01f &&
                   Vector3.Distance(modelRoot.localScale, Vector3.one * CanonicalFinalTaxiBodyScale) <= 0.0000001f;
        }

        public static bool IsCanonicalImportedAccessoryTransform(Transform modelRoot)
        {
            return IsCanonicalFinalTaxiBodyTransform(modelRoot);
        }

        private static bool IsCurrentZooXObjAsset(GameObject modelAsset)
        {
            string assetPath = AssetDatabase.GetAssetPath(modelAsset);
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.Replace('\\', '/').Equals(ObjModelPath, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool EnsureModelAssetImported(string modelPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) != null)
            {
                return true;
            }

            if (!File.Exists(modelPath))
            {
                return false;
            }

            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) != null;
        }

        public static void AlignDoorPanelToImportedDoor(Transform doorPanel)
        {
            if (doorPanel == null)
            {
                return;
            }

            doorPanel.localPosition = DoorPanelLocalPosition;
            doorPanel.localRotation = Quaternion.Euler(0f, -90f, 0f);
            doorPanel.localScale = Vector3.one * 0.00095f;
        }

        private static void NormalizeImportedModel(Transform modelRoot)
        {
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (!TryCalculateLocalBounds(modelRoot.parent, renderers, out Bounds bounds))
            {
                return;
            }

            if (bounds.size.x > bounds.size.z)
            {
                modelRoot.localRotation = Quaternion.Euler(0f, -90f, 0f);
                TryCalculateLocalBounds(modelRoot.parent, renderers, out bounds);
            }

            float widthScale = TargetModelSize.x / Mathf.Max(0.01f, bounds.size.x);
            float heightScale = TargetModelSize.y / Mathf.Max(0.01f, bounds.size.y);
            float lengthScale = TargetModelSize.z / Mathf.Max(0.01f, bounds.size.z);
            float scale = Mathf.Clamp(Mathf.Min(widthScale, heightScale, lengthScale), 0.0001f, 1000f);
            modelRoot.localScale = Vector3.one * scale;

            if (TryCalculateLocalBounds(modelRoot.parent, renderers, out bounds))
            {
                modelRoot.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            }
        }

        private static bool TryCalculateLocalBounds(Transform reference, Renderer[] renderers, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            if (reference == null || renderers == null)
            {
                return false;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                EncapsulateRenderer(reference, renderer, ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateRenderer(Transform reference, Renderer renderer, ref Bounds bounds, ref bool hasBounds)
        {
            Bounds rendererBounds = renderer.bounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 localCorner = reference.InverseTransformPoint(corners[i]);
                if (!hasBounds)
                {
                    bounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(localCorner);
                }
            }
        }

        private static Renderer FindLargestRenderer(Transform root)
        {
            Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
            Renderer largest = null;
            float largestVolume = 0f;
            if (renderers == null)
            {
                return null;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Vector3 size = renderer.bounds.size;
                float volume = size.x * size.y * size.z;
                if (largest == null || volume > largestVolume)
                {
                    largest = renderer;
                    largestVolume = volume;
                }
            }

            return largest;
        }

        private static GameObject CreateFusionSourceProxy(Transform parent, Material bodyMat, Material glassMat, Material tireMat)
        {
            var root = new GameObject(ModelInstanceName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var marker = new GameObject("Source ZOOX.f3z (Fusion archive)");
            marker.transform.SetParent(root.transform, false);

            CreateProxyCube("F3Z Proxy Lower Body", root.transform, new Vector3(0f, 0.48f, -0.05f), new Vector3(2.05f, 0.66f, 3.28f), Quaternion.identity, bodyMat);
            CreateProxyCube("F3Z Proxy Cabin Volume", root.transform, new Vector3(0f, 1.03f, -0.15f), new Vector3(1.75f, 1.0f, 2.55f), Quaternion.identity, bodyMat);
            CreateProxyCube("F3Z Proxy Roof", root.transform, new Vector3(0f, 1.62f, -0.2f), new Vector3(1.58f, 0.22f, 2.05f), Quaternion.identity, bodyMat);
            CreateProxyCube("F3Z Proxy Front Glass", root.transform, new Vector3(0f, 1.1f, 1.42f), new Vector3(1.55f, 0.82f, 0.06f), Quaternion.Euler(-11f, 0f, 0f), glassMat);
            CreateProxyCube("F3Z Proxy Rear Glass", root.transform, new Vector3(0f, 1.05f, -1.72f), new Vector3(1.38f, 0.62f, 0.06f), Quaternion.identity, glassMat);
            CreateProxyCube("F3Z Proxy Left Window", root.transform, new Vector3(-1.03f, 1.08f, -0.05f), new Vector3(0.05f, 0.68f, 1.45f), Quaternion.identity, glassMat);
            CreateProxyCube("F3Z Proxy Right Window", root.transform, new Vector3(1.03f, 1.08f, -0.05f), new Vector3(0.05f, 0.68f, 1.45f), Quaternion.identity, glassMat);
            CreateProxyCube("F3Z Proxy Door Cut Line Left", root.transform, new Vector3(-1.075f, 0.82f, -0.22f), new Vector3(0.035f, 0.95f, 0.08f), Quaternion.identity, glassMat);
            CreateProxyCube("F3Z Proxy Door Cut Line Right", root.transform, new Vector3(1.075f, 0.82f, -0.22f), new Vector3(0.035f, 0.95f, 0.08f), Quaternion.identity, glassMat);

            CreateProxyCylinder("F3Z Proxy Wheel FL", root.transform, new Vector3(-1.04f, 0.26f, 1.08f), new Vector3(0.48f, 0.09f, 0.48f), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateProxyCylinder("F3Z Proxy Wheel FR", root.transform, new Vector3(1.04f, 0.26f, 1.08f), new Vector3(0.48f, 0.09f, 0.48f), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateProxyCylinder("F3Z Proxy Wheel RL", root.transform, new Vector3(-1.04f, 0.26f, -1.08f), new Vector3(0.48f, 0.09f, 0.48f), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateProxyCylinder("F3Z Proxy Wheel RR", root.transform, new Vector3(1.04f, 0.26f, -1.08f), new Vector3(0.48f, 0.09f, 0.48f), Quaternion.Euler(0f, 0f, 90f), tireMat);

            Texture2D previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PreviewTexturePath);
            if (previewTexture != null)
            {
                AddPreviewReference(root.transform, previewTexture);
            }

            return root;
        }

        private static GameObject CreateProxyCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject cube = CreateFixtureCube(name, parent, localPosition, localScale, localRotation, material);
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return cube;
        }

        private static GameObject CreateProxyCylinder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localRotation = localRotation;
            cylinder.transform.localScale = localScale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;

            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return cylinder;
        }

        private static void AddPreviewReference(Transform parent, Texture2D previewTexture)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "F3Z Preview Reference";
            plane.transform.SetParent(parent, false);
            plane.transform.localPosition = new Vector3(0f, 1.62f, 1.76f);
            plane.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            plane.transform.localScale = new Vector3(0.36f, 0.36f, 0.36f);

            Collider collider = plane.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Shader previewShader = Shader.Find("Unlit/Texture") != null ? Shader.Find("Unlit/Texture") : Shader.Find("Universal Render Pipeline/Unlit");
            if (previewShader == null)
            {
                return;
            }

            Material previewMaterial = new Material(previewShader);
            previewMaterial.name = "Final F3Z Preview Reference";
            previewMaterial.mainTexture = previewTexture;
            plane.GetComponent<Renderer>().sharedMaterial = previewMaterial;
        }

        private static void RemoveColliders(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
            }
        }

        private static GameObject CreateFixtureCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = localRotation;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Text CreateWindshieldDisplay(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            GameObject display = CreateWorldCanvas(name, parent, localPosition, localRotation, GetExteriorScreenCanvasSize(), localScale);
            AddExteriorScreenImage(display.transform);
            return null;
        }

        private static Vector2 GetExteriorScreenCanvasSize()
        {
            return new Vector2(ExteriorScreenReferenceWidth, ExteriorScreenReferenceWidth * ExteriorScreenSourceHeight / ExteriorScreenSourceWidth);
        }

        private static RawImage AddExteriorScreenImage(Transform parent)
        {
            var imageObject = new GameObject("Exterior Screen Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage image = imageObject.GetComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>(ExteriorScreenResourcePath);
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static GameObject CreateWorldCanvas(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector2 size, Vector3 localScale)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            canvasObject.transform.localPosition = localPosition;
            canvasObject.transform.localRotation = localRotation;
            canvasObject.transform.localScale = localScale;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;
            return canvasObject;
        }

        private static void AddPanelBackground(Transform parent, Color color, Vector2 size)
        {
            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = background.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            background.GetComponent<Image>().color = color;
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Text text = textObject.GetComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2.3f, -2.3f);
            return text;
        }
    }
}
