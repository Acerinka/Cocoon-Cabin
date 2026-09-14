using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CocoonPrototype.Editor
{
    public static class CocoonFlowLightStripControlPointCreator
    {
        private const string TaxiPodVisualsName = "Taxi Pod Visuals";
        private const string RootName = "Cocoon Flow Light Strips";
        private const string TopName = "Flow Light Top";
        private const string BottomName = "Flow Light Bottom";
        private const string LeftName = "Flow Light Left";
        private const string RightName = "Flow Light Right";
        private const float PointSpacing = 0.08f;
        private const float PreviewOffset = 0.08f;

        [MenuItem("Cocoon/Create Flow Light Strip Control Points")]
        public static void CreateFlowLightStripControlPoints()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("Cocoon flow light setup failed: no active scene is loaded.");
                return;
            }

            Transform taxiPodVisuals = FindTransformDeep(scene, TaxiPodVisualsName);
            if (taxiPodVisuals == null)
            {
                Debug.LogError("Cocoon flow light setup failed: could not find '" + TaxiPodVisualsName + "' in the active scene.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Cocoon Flow Light Strip Control Points");

            int createdCount = 0;
            Transform root = EnsureChild(taxiPodVisuals, RootName, Vector3.zero, ref createdCount);
            EnsureStrip(root, TopName, 6, FlowAxis.X, PreviewOffset, ref createdCount);
            EnsureStrip(root, BottomName, 6, FlowAxis.X, -PreviewOffset, ref createdCount);
            EnsureStrip(root, LeftName, 5, FlowAxis.Z, -PreviewOffset, ref createdCount);
            EnsureStrip(root, RightName, 5, FlowAxis.Z, PreviewOffset, ref createdCount);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeTransform = root;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Cocoon flow light strip control hierarchy ready under " + GetPath(root) + ". Created " + createdCount + " missing object(s); existing transforms were preserved.");
        }

        private static void EnsureStrip(Transform root, string stripName, int pointCount, FlowAxis axis, float previewOffset, ref int createdCount)
        {
            Transform strip = EnsureChild(root, stripName, Vector3.zero, ref createdCount);
            for (int i = 0; i < pointCount; i++)
            {
                string pointName = "P" + i.ToString("00");
                Vector3 localPosition = GetPreviewPointPosition(i, pointCount, axis, previewOffset);
                EnsureChild(strip, pointName, localPosition, ref createdCount);
            }
        }

        private static Vector3 GetPreviewPointPosition(int index, int pointCount, FlowAxis axis, float previewOffset)
        {
            float centered = (index - (pointCount - 1) * 0.5f) * PointSpacing;
            switch (axis)
            {
                case FlowAxis.X:
                    return new Vector3(centered, 0f, previewOffset);
                case FlowAxis.Z:
                    return new Vector3(previewOffset, 0f, centered);
                default:
                    return Vector3.zero;
            }
        }

        private static Transform EnsureChild(Transform parent, string childName, Vector3 createdLocalPosition, ref int createdCount)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var gameObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + childName);
            Transform transform = gameObject.transform;
            transform.SetParent(parent, false);
            transform.localPosition = createdLocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            createdCount++;
            return transform;
        }

        private static Transform FindTransformDeep(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform match = FindTransformDeep(roots[i].transform, name);
                if (match != null)
                {
                    return match;
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

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindTransformDeep(root.GetChild(i), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private enum FlowAxis
        {
            X,
            Z
        }
    }
}
