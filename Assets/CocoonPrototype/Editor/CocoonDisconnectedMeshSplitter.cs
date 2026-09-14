using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CocoonPrototype.EditorTools
{
    public static class CocoonDisconnectedMeshSplitter
    {
        private const string DefaultScenePath = "Assets/Scenes/Cocoon_OnboardingVR.unity";
        private const string GeneratedMeshFolder = "Assets/CocoonPrototype/Generated/SplitMeshes/Object022";
        private const string SplitRootName = "Object022 Split Parts";
        private const string TargetObjectName = "Object022";
        private const string TaxiRootName = "Cocoon Autonomous Taxi";
        private const string TaxiVisualsName = "Taxi Pod Visuals";

        [MenuItem("Cocoon/Taxi/Split Selected Mesh By Disconnected Parts")]
        private static void SplitSelectedMesh()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Split Mesh", "Select a MeshRenderer or SkinnedMeshRenderer first.", "OK");
                return;
            }

            SplitRendererObject(selected, true);
        }

        [MenuItem("Cocoon/Taxi/Split Cocoon Taxi Object022")]
        private static void SplitCocoonTaxiObject022()
        {
            GameObject target = FindTaxiObject022();
            if (target == null)
            {
                EditorUtility.DisplayDialog("Split Object022", "Could not find Object022 under the Cocoon taxi in the open scene.", "OK");
                return;
            }

            SplitRendererObject(target, true);
        }

        public static void SplitCocoonTaxiObject022Batch()
        {
            Scene scene = EditorSceneManager.OpenScene(DefaultScenePath, OpenSceneMode.Single);
            GameObject target = FindTaxiObject022();
            if (target == null)
            {
                throw new InvalidOperationException("Could not find Object022 under the Cocoon taxi in " + DefaultScenePath + ".");
            }

            int parts = SplitRendererObject(target, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Cocoon][TaxiMesh] Split Object022 into " + parts + " disconnected part(s).");
        }

        private static int SplitRendererObject(GameObject source, bool interactive)
        {
            Renderer sourceRenderer = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceRenderer == null)
            {
                sourceRenderer = source.GetComponent<MeshRenderer>();
            }

            Mesh sourceMesh = GetSourceMesh(source);
            if (sourceRenderer == null || sourceMesh == null)
            {
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Split Mesh", source.name + " has no supported mesh renderer.", "OK");
                }

                return 0;
            }

            bool restoredReadWrite = false;
            bool originalReadWrite = false;
            string meshAssetPath = AssetDatabase.GetAssetPath(sourceMesh);
            ModelImporter importer = !string.IsNullOrEmpty(meshAssetPath) ? AssetImporter.GetAtPath(meshAssetPath) as ModelImporter : null;
            if (!sourceMesh.isReadable && importer != null)
            {
                originalReadWrite = importer.isReadable;
                if (!originalReadWrite)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    restoredReadWrite = true;
                    sourceMesh = GetSourceMesh(source);
                }
            }

            if (sourceMesh == null || !sourceMesh.isReadable)
            {
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Split Mesh", source.name + " mesh is not readable. Enable Read/Write on the model importer and retry.", "OK");
                }

                return 0;
            }

            Mesh bakedMesh = GetBakedMeshIfNeeded(source);
            Mesh splitSourceMesh = bakedMesh != null ? bakedMesh : sourceMesh;
            List<MeshComponent> components = FindDisconnectedComponents(splitSourceMesh);
            if (components.Count <= 1)
            {
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Split Mesh", source.name + " did not contain multiple disconnected components.", "OK");
                }

                return components.Count;
            }

            EnsureFolder(GeneratedMeshFolder);
            Transform splitParent = FindSplitParent(source.transform);
            Transform existingSplit = splitParent != null ? splitParent.Find(SplitRootName) : null;
            if (existingSplit != null)
            {
                Undo.DestroyObjectImmediate(existingSplit.gameObject);
            }

            Undo.RegisterFullObjectHierarchyUndo(splitParent != null ? splitParent.gameObject : source, "Split disconnected mesh");
            GameObject splitRoot = new GameObject(SplitRootName);
            splitRoot.transform.SetParent(splitParent, false);
            CopyLocalTransform(source.transform, splitRoot.transform);

            Material[] sourceMaterials = sourceRenderer.sharedMaterials;
            for (int i = 0; i < components.Count; i++)
            {
                MeshComponent component = components[i];
                Mesh partMesh = BuildMeshPart(splitSourceMesh, component, out int[] materialSlots);
                string meshPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedMeshFolder + "/Object022_part_" + (i + 1).ToString("00") + ".asset");
                AssetDatabase.CreateAsset(partMesh, meshPath);

                GameObject partObject = new GameObject("Object022 Part " + (i + 1).ToString("00"));
                partObject.transform.SetParent(splitRoot.transform, false);
                partObject.transform.localPosition = Vector3.zero;
                partObject.transform.localRotation = Quaternion.identity;
                partObject.transform.localScale = Vector3.one;

                MeshFilter meshFilter = partObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = partMesh;
                MeshRenderer meshRenderer = partObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = ResolveMaterials(sourceMaterials, materialSlots);
                meshRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                meshRenderer.receiveShadows = sourceRenderer.receiveShadows;
                meshRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                meshRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                meshRenderer.probeAnchor = sourceRenderer.probeAnchor;
            }

            sourceRenderer.enabled = false;
            Selection.activeGameObject = splitRoot;
            AssetDatabase.SaveAssets();

            if (bakedMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(bakedMesh);
            }

            if (restoredReadWrite && importer != null)
            {
                importer.isReadable = originalReadWrite;
                importer.SaveAndReimport();
            }

            Debug.Log("[Cocoon][TaxiMesh] " + source.name + " split into " + components.Count + " disconnected part(s). Original renderer disabled, source GameObject preserved.");
            return components.Count;
        }

        private static Mesh GetSourceMesh(GameObject source)
        {
            SkinnedMeshRenderer skinned = source.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
            {
                return skinned.sharedMesh;
            }

            MeshFilter meshFilter = source.GetComponent<MeshFilter>();
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        private static Mesh GetBakedMeshIfNeeded(GameObject source)
        {
            SkinnedMeshRenderer skinned = source.GetComponent<SkinnedMeshRenderer>();
            if (skinned == null)
            {
                return null;
            }

            if (skinned.bones != null && skinned.bones.Length > 0)
            {
                Mesh baked = new Mesh { name = skinned.sharedMesh.name + "_baked" };
                skinned.BakeMesh(baked, true);
                return baked;
            }

            return null;
        }

        private static List<MeshComponent> FindDisconnectedComponents(Mesh mesh)
        {
            List<TriangleRecord> triangles = new List<TriangleRecord>();
            List<int>[] trianglesByVertex = new List<int>[mesh.vertexCount];
            for (int i = 0; i < trianglesByVertex.Length; i++)
            {
                trianglesByVertex[i] = new List<int>();
            }

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] indices = mesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int triangleIndex = triangles.Count;
                    TriangleRecord triangle = new TriangleRecord(subMesh, indices[i], indices[i + 1], indices[i + 2]);
                    triangles.Add(triangle);
                    trianglesByVertex[triangle.A].Add(triangleIndex);
                    trianglesByVertex[triangle.B].Add(triangleIndex);
                    trianglesByVertex[triangle.C].Add(triangleIndex);
                }
            }

            bool[] visited = new bool[triangles.Count];
            List<MeshComponent> components = new List<MeshComponent>();
            Queue<int> queue = new Queue<int>();
            for (int start = 0; start < triangles.Count; start++)
            {
                if (visited[start])
                {
                    continue;
                }

                MeshComponent component = new MeshComponent(mesh.subMeshCount);
                visited[start] = true;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int triangleIndex = queue.Dequeue();
                    TriangleRecord triangle = triangles[triangleIndex];
                    component.AddTriangle(triangle);
                    EnqueueNeighbors(triangle.A, trianglesByVertex, visited, queue);
                    EnqueueNeighbors(triangle.B, trianglesByVertex, visited, queue);
                    EnqueueNeighbors(triangle.C, trianglesByVertex, visited, queue);
                }

                components.Add(component);
            }

            components.Sort((left, right) => right.TriangleCount.CompareTo(left.TriangleCount));
            return components;
        }

        private static void EnqueueNeighbors(int vertexIndex, List<int>[] trianglesByVertex, bool[] visited, Queue<int> queue)
        {
            List<int> neighbors = trianglesByVertex[vertexIndex];
            for (int i = 0; i < neighbors.Count; i++)
            {
                int neighbor = neighbors[i];
                if (visited[neighbor])
                {
                    continue;
                }

                visited[neighbor] = true;
                queue.Enqueue(neighbor);
            }
        }

        private static Mesh BuildMeshPart(Mesh source, MeshComponent component, out int[] materialSlots)
        {
            Dictionary<int, int> vertexMap = new Dictionary<int, int>();
            List<int> sourceVertices = component.GetOrderedVertices();
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector4[] tangents = source.tangents;
            Color[] colors = source.colors;
            Color32[] colors32 = source.colors32;
            BoneWeight[] boneWeights = source.boneWeights;

            Mesh mesh = new Mesh { name = "Object022_split" };
            if (sourceVertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            List<Vector3> newVertices = new List<Vector3>(sourceVertices.Count);
            List<Vector3> newNormals = normals != null && normals.Length == source.vertexCount ? new List<Vector3>(sourceVertices.Count) : null;
            List<Vector4> newTangents = tangents != null && tangents.Length == source.vertexCount ? new List<Vector4>(sourceVertices.Count) : null;
            List<Color> newColors = colors != null && colors.Length == source.vertexCount ? new List<Color>(sourceVertices.Count) : null;
            List<Color32> newColors32 = colors32 != null && colors32.Length == source.vertexCount ? new List<Color32>(sourceVertices.Count) : null;
            List<BoneWeight> newBoneWeights = boneWeights != null && boneWeights.Length == source.vertexCount ? new List<BoneWeight>(sourceVertices.Count) : null;

            CopyMappedVertexData(sourceVertices, vertexMap, vertices, newVertices);
            CopyMappedOptionalData(sourceVertices, normals, newNormals);
            CopyMappedOptionalData(sourceVertices, tangents, newTangents);
            CopyMappedOptionalData(sourceVertices, colors, newColors);
            CopyMappedOptionalData(sourceVertices, colors32, newColors32);
            CopyMappedOptionalData(sourceVertices, boneWeights, newBoneWeights);

            mesh.SetVertices(newVertices);
            if (newNormals != null)
            {
                mesh.SetNormals(newNormals);
            }

            if (newTangents != null)
            {
                mesh.SetTangents(newTangents);
            }

            if (newColors != null)
            {
                mesh.SetColors(newColors);
            }
            else if (newColors32 != null)
            {
                mesh.SetColors(newColors32);
            }

            if (newBoneWeights != null)
            {
                mesh.boneWeights = newBoneWeights.ToArray();
                mesh.bindposes = source.bindposes;
            }

            CopyUvChannel(source, mesh, sourceVertices, 0);
            CopyUvChannel(source, mesh, sourceVertices, 1);
            CopyUvChannel(source, mesh, sourceVertices, 2);
            CopyUvChannel(source, mesh, sourceVertices, 3);

            List<int> usedMaterialSlots = new List<int>();
            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                if (component.TrianglesBySubMesh[subMesh].Count > 0)
                {
                    usedMaterialSlots.Add(subMesh);
                }
            }

            mesh.subMeshCount = usedMaterialSlots.Count;
            for (int i = 0; i < usedMaterialSlots.Count; i++)
            {
                int sourceSubMesh = usedMaterialSlots[i];
                List<int> remappedTriangles = new List<int>(component.TrianglesBySubMesh[sourceSubMesh].Count * 3);
                List<TriangleRecord> sourceTriangles = component.TrianglesBySubMesh[sourceSubMesh];
                for (int triangleIndex = 0; triangleIndex < sourceTriangles.Count; triangleIndex++)
                {
                    TriangleRecord triangle = sourceTriangles[triangleIndex];
                    remappedTriangles.Add(vertexMap[triangle.A]);
                    remappedTriangles.Add(vertexMap[triangle.B]);
                    remappedTriangles.Add(vertexMap[triangle.C]);
                }

                mesh.SetTriangles(remappedTriangles, i, true);
            }

            if (newNormals == null)
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            materialSlots = usedMaterialSlots.ToArray();
            return mesh;
        }

        private static void CopyMappedVertexData(IReadOnlyList<int> sourceIndices, Dictionary<int, int> vertexMap, Vector3[] source, List<Vector3> target)
        {
            for (int i = 0; i < sourceIndices.Count; i++)
            {
                int sourceIndex = sourceIndices[i];
                vertexMap[sourceIndex] = i;
                target.Add(source[sourceIndex]);
            }
        }

        private static void CopyMappedOptionalData<T>(IReadOnlyList<int> sourceIndices, T[] source, List<T> target)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < sourceIndices.Count; i++)
            {
                target.Add(source[sourceIndices[i]]);
            }
        }

        private static void CopyUvChannel(Mesh source, Mesh target, IReadOnlyList<int> sourceIndices, int channel)
        {
            VertexAttribute attribute = VertexAttribute.TexCoord0 + channel;
            if (!source.HasVertexAttribute(attribute))
            {
                return;
            }

            List<Vector4> sourceUvs = new List<Vector4>();
            source.GetUVs(channel, sourceUvs);
            if (sourceUvs.Count != source.vertexCount)
            {
                return;
            }

            List<Vector4> targetUvs = new List<Vector4>(sourceIndices.Count);
            for (int i = 0; i < sourceIndices.Count; i++)
            {
                targetUvs.Add(sourceUvs[sourceIndices[i]]);
            }

            target.SetUVs(channel, targetUvs);
        }

        private static Material[] ResolveMaterials(Material[] sourceMaterials, int[] materialSlots)
        {
            Material[] result = new Material[materialSlots.Length];
            for (int i = 0; i < materialSlots.Length; i++)
            {
                int materialIndex = materialSlots[i];
                result[i] = sourceMaterials != null && materialIndex >= 0 && materialIndex < sourceMaterials.Length
                    ? sourceMaterials[materialIndex]
                    : null;
            }

            return result;
        }

        private static Transform FindSplitParent(Transform source)
        {
            Transform visuals = FindAncestor(source, TaxiVisualsName);
            return visuals != null ? visuals : source.parent;
        }

        private static GameObject FindTaxiObject022()
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    !candidate.gameObject.scene.isLoaded ||
                    !string.Equals(candidate.name, TargetObjectName, StringComparison.OrdinalIgnoreCase) ||
                    FindAncestor(candidate, TaxiRootName) == null)
                {
                    continue;
                }

                if (candidate.GetComponent<SkinnedMeshRenderer>() != null || candidate.GetComponent<MeshFilter>() != null)
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private static Transform FindAncestor(Transform transform, string name)
        {
            Transform current = transform;
            while (current != null)
            {
                if (string.Equals(current.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private static void CopyLocalTransform(Transform source, Transform target)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private readonly struct TriangleRecord
        {
            public readonly int SubMesh;
            public readonly int A;
            public readonly int B;
            public readonly int C;

            public TriangleRecord(int subMesh, int a, int b, int c)
            {
                SubMesh = subMesh;
                A = a;
                B = b;
                C = c;
            }
        }

        private sealed class MeshComponent
        {
            private readonly HashSet<int> vertices = new HashSet<int>();

            public MeshComponent(int subMeshCount)
            {
                TrianglesBySubMesh = new List<TriangleRecord>[subMeshCount];
                for (int i = 0; i < TrianglesBySubMesh.Length; i++)
                {
                    TrianglesBySubMesh[i] = new List<TriangleRecord>();
                }
            }

            public List<TriangleRecord>[] TrianglesBySubMesh { get; }

            public int TriangleCount { get; private set; }

            public void AddTriangle(TriangleRecord triangle)
            {
                TrianglesBySubMesh[triangle.SubMesh].Add(triangle);
                vertices.Add(triangle.A);
                vertices.Add(triangle.B);
                vertices.Add(triangle.C);
                TriangleCount++;
            }

            public List<int> GetOrderedVertices()
            {
                List<int> ordered = new List<int>(vertices);
                ordered.Sort();
                return ordered;
            }
        }
    }
}
