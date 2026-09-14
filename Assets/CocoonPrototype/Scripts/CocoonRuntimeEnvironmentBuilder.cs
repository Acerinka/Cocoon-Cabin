using UnityEngine;
using UnityEngine.SceneManagement;

namespace CocoonPrototype
{
    public static class CocoonRuntimeEnvironmentBuilder
    {
        private const string RuntimeRootName = "Runtime Rich City Environment";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BuildIfNeeded()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.name.Contains("Cocoon"))
            {
                return;
            }

            Transform roadmap = FindTopLevelRoadmap(scene);
            if (roadmap != null)
            {
                DisableIfPresent(scene, RuntimeRootName);
                DisableIfPresent(scene, "Expanded Roads");
                DisableIfPresent(scene, "Expanded Sidewalks");
                DisableIfPresent(scene, "Expanded Curbs");
                DisableIfPresent(scene, "Expanded Crosswalks");
                DisableIfPresent(scene, "Expanded Buildings");
                CalibrateRoadmapVrScale(scene, roadmap);
                CocoonDebugLog.Info("Environment", "ROADMAP detected; runtime procedural city generation skipped.");
                return;
            }

            if (GameObject.Find(RuntimeRootName) != null || GameObject.Find("Expanded Buildings") != null)
            {
                return;
            }

            Transform streetRoot = ResolveStreetRoot();
            var root = new GameObject(RuntimeRootName);
            root.transform.SetParent(streetRoot, false);

            Transform buildings = CreateGroup("Runtime Buildings", root.transform);
            Transform props = CreateGroup("Runtime Street Props", root.transform);

            Material[] facades =
            {
                CreateMaterial("Runtime Facade Cool", new Color(0.18f, 0.22f, 0.24f), false),
                CreateMaterial("Runtime Facade Warm", new Color(0.36f, 0.32f, 0.28f), false),
                CreateMaterial("Runtime Facade Light", new Color(0.46f, 0.50f, 0.48f), false),
                CreateMaterial("Runtime Facade Dark", new Color(0.11f, 0.14f, 0.16f), false)
            };
            Material windowMat = CreateMaterial("Runtime Window Glow", new Color(0.1f, 0.55f, 0.72f), true);
            Material shopfrontMat = CreateMaterial("Runtime Shopfront", new Color(0.04f, 0.24f, 0.28f), true);
            Material awningMat = CreateMaterial("Runtime Awning", new Color(0.64f, 0.18f, 0.24f), false);
            Material planterMat = CreateMaterial("Runtime Plant", new Color(0.1f, 0.36f, 0.22f), false);
            Material concreteMat = CreateMaterial("Runtime Concrete", new Color(0.75f, 0.77f, 0.73f), false);
            Material furnitureMat = CreateMaterial("Runtime Street Furniture", new Color(0.09f, 0.095f, 0.1f), false);
            Material signMat = CreateMaterial("Runtime Sign", new Color(0.93f, 0.94f, 0.9f), false);

            BuildBuildings(buildings, facades, windowMat, shopfrontMat, awningMat);
            BuildStreetProps(props, planterMat, concreteMat, furnitureMat, shopfrontMat, signMat);
            CocoonDebugLog.Info("Environment", "Runtime rich city environment generated.", root);
        }

        private static Transform ResolveStreetRoot()
        {
            GameObject street = GameObject.Find("02_Street_Block");
            if (street != null)
            {
                return street.transform;
            }

            return new GameObject("02_Street_Block").transform;
        }

        private static Transform FindTopLevelRoadmap(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == "ROADMAP")
                {
                    return roots[i].transform;
                }
            }

            return null;
        }

        private static void CalibrateRoadmapVrScale(Scene scene, Transform roadmap)
        {
            Transform xrOrigin = FindTransformDeep(scene, "XR Origin - Sidewalk Rider");
            if (xrOrigin == null)
            {
                return;
            }

            float experienceScale = CocoonExperienceScale.ResolveRoadmapScale(roadmap);
            if (Mathf.Abs(xrOrigin.localScale.x - experienceScale) < 0.0001f &&
                Mathf.Abs(xrOrigin.localScale.y - experienceScale) < 0.0001f &&
                Mathf.Abs(xrOrigin.localScale.z - experienceScale) < 0.0001f)
            {
                if (CocoonExperienceScale.CalibrateCharacterController(xrOrigin))
                {
                    CocoonDebugLog.Info("Environment", "XR CharacterController step offset matched to ROADMAP scale.", xrOrigin);
                }

                return;
            }

            xrOrigin.localScale = Vector3.one * experienceScale;
            CocoonDebugLog.Info("Environment", "XR rider scale matched to ROADMAP scale " + experienceScale.ToString("0.###") + ".", xrOrigin);
            if (CocoonExperienceScale.CalibrateCharacterController(xrOrigin))
            {
                CocoonDebugLog.Info("Environment", "XR CharacterController step offset matched to ROADMAP scale.", xrOrigin);
            }
        }

        private static Transform FindTransformDeep(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform transform = transforms[transformIndex];
                    if (transform != null && transform.name == name)
                    {
                        return transform;
                    }
                }
            }

            return null;
        }

        private static void DisableIfPresent(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform transform = transforms[transformIndex];
                    if (transform != null && transform.name == objectName)
                    {
                        transform.gameObject.SetActive(false);
                    }
                }
            }
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void BuildBuildings(Transform parent, Material[] facades, Material windowMat, Material shopfrontMat, Material awningMat)
        {
            for (int i = 0; i < 12; i++)
            {
                float z = -38f + i * 6.8f;
                float height = 3.0f + (i % 5) * 0.55f;
                CreateBuilding("Runtime West Building " + i, parent, new Vector3(-9.75f, 0f, z), Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector3(2.5f + (i % 3) * 0.42f, height, 3.1f + (i % 2) * 0.55f), facades[i % facades.Length], windowMat, shopfrontMat, awningMat, i);
            }

            for (int i = 0; i < 12; i++)
            {
                float z = -36f + i * 6.8f;
                float height = 3.4f + (i % 4) * 0.72f;
                CreateBuilding("Runtime East Building " + i, parent, new Vector3(43.35f, 0f, z + 1.25f), Quaternion.LookRotation(Vector3.left, Vector3.up), new Vector3(2.8f + (i % 2) * 0.55f, height, 3.0f + (i % 4) * 0.28f), facades[(i + 1) % facades.Length], windowMat, shopfrontMat, awningMat, i + 10);
            }

            for (int i = 0; i < 7; i++)
            {
                float x = -1f + i * 6.0f;
                float height = 3.0f + (i % 3) * 0.8f;
                CreateBuilding("Runtime North Building " + i, parent, new Vector3(x, 0f, 37.9f), Quaternion.LookRotation(Vector3.back, Vector3.up), new Vector3(4.2f, height, 3.0f), facades[(i + 2) % facades.Length], windowMat, shopfrontMat, awningMat, i + 20);
                CreateBuilding("Runtime South Building " + i, parent, new Vector3(x + 1.8f, 0f, -37.9f), Quaternion.LookRotation(Vector3.forward, Vector3.up), new Vector3(4.0f, height + 0.45f, 3.0f), facades[(i + 3) % facades.Length], windowMat, shopfrontMat, awningMat, i + 30);
            }

            for (int i = 0; i < 9; i++)
            {
                float x = 7.2f + (i % 3) * 4.8f;
                float z = -14f + (i / 3) * 9.0f;
                CreateBuilding("Runtime Central Building " + i, parent, new Vector3(x, 0f, z), Quaternion.LookRotation(i % 2 == 0 ? Vector3.back : Vector3.forward, Vector3.up), new Vector3(3.2f, 2.4f + (i % 4) * 0.45f, 2.8f), facades[i % facades.Length], windowMat, shopfrontMat, awningMat, i + 40);
            }
        }

        private static void CreateBuilding(string name, Transform parent, Vector3 groundCenter, Quaternion rotation, Vector3 size, Material facadeMat, Material windowMat, Material shopfrontMat, Material awningMat, int seed)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = groundCenter;
            root.transform.rotation = rotation;

            float width = Mathf.Max(1f, size.x);
            float height = Mathf.Max(1.8f, size.y);
            float depth = Mathf.Max(1f, size.z);
            float frontZ = depth * 0.5f + 0.026f;

            CreateCube("Facade Mass", root.transform, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), facadeMat);
            CreateCube("Roof Cap", root.transform, new Vector3(0f, height + 0.08f, 0f), new Vector3(width + 0.18f, 0.16f, depth + 0.18f), facadeMat);
            CreateCube("Ground Floor Shopfront", root.transform, new Vector3(0f, 0.66f, frontZ), new Vector3(width * 0.78f, 0.72f, 0.04f), shopfrontMat);
            CreateCube("Awning Band", root.transform, new Vector3(0f, 1.13f, frontZ + 0.035f), new Vector3(width * 0.86f, 0.12f, 0.1f), awningMat);

            int columns = Mathf.Clamp(Mathf.FloorToInt(width / 0.62f), 2, 6);
            int floors = Mathf.Clamp(Mathf.FloorToInt((height - 1.55f) / 0.62f), 1, 8);
            float windowWidth = Mathf.Min(0.42f, width / (columns + 1) * 0.55f);
            float startX = -(columns - 1) * width / (columns + 1) * 0.5f;
            float stepX = width / (columns + 1);
            for (int floor = 0; floor < floors; floor++)
            {
                float y = 1.65f + floor * 0.62f;
                for (int column = 0; column < columns; column++)
                {
                    if (((column + floor + seed) % 7) == 0)
                    {
                        continue;
                    }

                    CreateCube("Window " + floor + "-" + column, root.transform, new Vector3(startX + column * stepX, y, frontZ + 0.012f), new Vector3(windowWidth, 0.34f, 0.035f), windowMat);
                }
            }
        }

        private static void BuildStreetProps(Transform parent, Material plantMat, Material concreteMat, Material furnitureMat, Material glassMat, Material signMat)
        {
            CreateShelter("Runtime West Pickup Shelter", parent, new Vector3(-6.35f, 0f, 10f), Quaternion.LookRotation(Vector3.right, Vector3.up), furnitureMat, glassMat, signMat);
            CreateShelter("Runtime East Pickup Shelter", parent, new Vector3(40.55f, 0f, 18f), Quaternion.LookRotation(Vector3.left, Vector3.up), furnitureMat, glassMat, signMat);

            for (int i = 0; i < 7; i++)
            {
                float z = -28f + i * 9.5f;
                CreatePlanter("Runtime West Planter " + i, parent, new Vector3(-6.55f, 0f, z), plantMat, concreteMat);
                CreatePlanter("Runtime East Planter " + i, parent, new Vector3(39.95f, 0f, z + 2.5f), plantMat, concreteMat);
            }

            for (int i = 0; i < 5; i++)
            {
                float z = -24f + i * 12f;
                CreateBench("Runtime West Bench " + i, parent, new Vector3(-5.9f, 0f, z), Quaternion.Euler(0f, 90f, 0f), furnitureMat);
                CreateBench("Runtime East Bench " + i, parent, new Vector3(39.0f, 0f, z + 5f), Quaternion.Euler(0f, -90f, 0f), furnitureMat);
            }
        }

        private static void CreateShelter(string name, Transform parent, Vector3 position, Quaternion rotation, Material frameMat, Material glassMat, Material signMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = rotation;
            CreateCube("Shelter Roof", root.transform, new Vector3(0f, 1.72f, 0f), new Vector3(1.65f, 0.1f, 0.64f), frameMat);
            CreateCube("Shelter Back Glass", root.transform, new Vector3(0f, 0.95f, -0.28f), new Vector3(1.55f, 1.18f, 0.045f), glassMat);
            CreateCube("Shelter Side Glass", root.transform, new Vector3(-0.78f, 0.95f, 0f), new Vector3(0.045f, 1.18f, 0.5f), glassMat);
            CreateCube("Shelter Seat", root.transform, new Vector3(0f, 0.42f, 0.05f), new Vector3(1.18f, 0.1f, 0.26f), frameMat);
            CreateCube("Shelter Pickup Sign", root.transform, new Vector3(0f, 1.48f, 0.34f), new Vector3(1.0f, 0.28f, 0.05f), signMat);
        }

        private static void CreatePlanter(string name, Transform parent, Vector3 position, Material plantMat, Material baseMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            CreateCube("Planter Base", root.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.78f, 0.34f, 0.42f), baseMat);
            CreateCube("Plant Mass", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.66f, 0.22f, 0.34f), plantMat);
        }

        private static void CreateBench(string name, Transform parent, Vector3 position, Quaternion rotation, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = rotation;
            CreateCube("Seat", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(1.05f, 0.08f, 0.28f), material);
            CreateCube("Back", root.transform, new Vector3(0f, 0.66f, -0.15f), new Vector3(1.05f, 0.34f, 0.06f), material);
            CreateCube("Left Leg", root.transform, new Vector3(-0.42f, 0.23f, 0f), new Vector3(0.08f, 0.32f, 0.08f), material);
            CreateCube("Right Leg", root.transform, new Vector3(0.42f, 0.23f, 0f), new Vector3(0.08f, 0.32f, 0.08f), material);
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;

            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            return cube;
        }

        private static Material CreateMaterial(string name, Color color, bool emissive)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = name
            };

            int baseColorId = Shader.PropertyToID("_BaseColor");
            int colorId = Shader.PropertyToID("_Color");
            if (material.HasProperty(baseColorId))
            {
                material.SetColor(baseColorId, color);
            }
            else if (material.HasProperty(colorId))
            {
                material.SetColor(colorId, color);
            }

            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                int emissionId = Shader.PropertyToID("_EmissionColor");
                if (material.HasProperty(emissionId))
                {
                    material.SetColor(emissionId, color * 1.35f);
                }
            }

            return material;
        }
    }
}
