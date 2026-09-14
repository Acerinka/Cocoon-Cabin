using System;
using System.Collections.Generic;
using UnityEngine;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonLightSequenceController : MonoBehaviour
    {
        [Header("Light Roots")]
        [SerializeField] private Transform floorLightRoot;
        [SerializeField] private Transform seatLightRoot;
        [SerializeField] private Transform luggagePreventerRoot;

        [Header("Shared Visual Settings")]
        [SerializeField] private Color sequenceColor = new Color(0f, 1f, 0.24f, 1f);
        [SerializeField] private bool logBindings = true;

        [Header("FLOORLIGHT - Green Orbs")]
        [SerializeField, HideInInspector] private float floorActiveIntensity = 0.18f;
        [SerializeField, HideInInspector] private float floorLightRange = 0.055f;
        [Tooltip("Total time, in seconds, for one full FLOORLIGHT pass from P00 through the final point.")]
        [SerializeField] private float floorCycleSeconds = 2f;
        [Tooltip("How many complete FLOORLIGHT passes to play when luggage enters the taxi.")]
        [SerializeField] private int floorRepeats = 3;
        [Tooltip("Fraction of each point's time slot that stays visible. Lower values create a clearer off gap between points.")]
        [SerializeField, Range(0.05f, 1f)] private float floorPointDuty = 0.65f;
        [SerializeField, HideInInspector] private LightShadows floorShadows = LightShadows.None;
        [Tooltip("Default world radius only for newly created FLOORLIGHT runtime orbs. Existing/manual orb sizes are preserved.")]
        [SerializeField] private float floorOrbWorldRadius = 0.012f;

        [Header("SEATLIGHT - Green Orbs")]
        [Tooltip("Total time, in seconds, for one SEATLIGHT all-on/all-off cycle.")]
        [SerializeField] private float seatCycleSeconds = 2f;
        [Tooltip("How many SEATLIGHT all-on/all-off cycles to play after the seat reaches out.")]
        [SerializeField] private int seatRepeats = 2;
        [Tooltip("Fraction of each SEATLIGHT cycle where all seat orbs are visible.")]
        [SerializeField, Range(0.05f, 1f)] private float seatPointDuty = 0.7f;
        [Tooltip("Default world radius only for newly created SEATLIGHT runtime orbs. Existing/manual orb sizes are preserved.")]
        [SerializeField] private float seatOrbWorldRadius = 0.018f;

        [Header("Luggage Preventer - Green Proxy")]
        [Tooltip("Seconds per luggage preventer flash pulse.")]
        [SerializeField] private float preventerPulseSeconds = 0.5f;
        [Tooltip("How many green proxy flashes to play while the luggage preventer rises.")]
        [SerializeField] private int preventerPulseRepeats = 2;

        private readonly List<GameObject> floorOrbs = new List<GameObject>(16);
        private readonly List<Transform> floorLightSources = new List<Transform>(16);
        private readonly List<GameObject> seatOrbs = new List<GameObject>(16);
        private readonly List<Transform> seatOrbSources = new List<Transform>(16);
        private readonly Dictionary<GameObject, AuthoredOrbPose> authoredOrbPoses = new Dictionary<GameObject, AuthoredOrbPose>();
        private readonly List<Renderer> preventerOriginalRenderers = new List<Renderer>(8);
        private readonly List<bool> preventerOriginalRendererEnabled = new List<bool>(8);
        private Transform runtimeFloorOrbRoot;
        private Transform runtimeSeatOrbRoot;
        private Transform preventerProxyRoot;
        private Material greenProxyMaterial;
        private RunningSequence floorSequence;
        private RunningSequence seatSequence;
        private PulseSequence preventerPulse;
        private bool hasLogged;

        private struct RunningSequence
        {
            public bool active;
            public float elapsed;
            public float cycleSeconds;
            public int repeats;
            public int count;
        }

        private struct PulseSequence
        {
            public bool active;
            public float elapsed;
            public float pulseSeconds;
            public int repeats;
        }

        private struct AuthoredOrbPose
        {
            public readonly Vector3 LocalPositionToSource;
            public readonly Quaternion LocalRotationToSource;
            public readonly Vector3 WorldScale;

            public AuthoredOrbPose(Transform orb, Transform source)
            {
                if (orb != null && source != null)
                {
                    LocalPositionToSource = source.InverseTransformPoint(orb.position);
                    LocalRotationToSource = Quaternion.Inverse(source.rotation) * orb.rotation;
                    WorldScale = orb.lossyScale;
                }
                else
                {
                    LocalPositionToSource = Vector3.zero;
                    LocalRotationToSource = Quaternion.identity;
                    WorldScale = Vector3.one;
                }
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            UpdateFloorSequence();
            UpdateSeatOrbSequence();
            UpdatePreventerPulse();
            SyncRuntimeOrbPoses();
        }

        public void PlayFloorEntrySequence()
        {
            ResolveReferences();
            floorSequence = new RunningSequence
            {
                active = floorOrbs.Count > 0,
                elapsed = 0f,
                cycleSeconds = Mathf.Max(0.01f, floorCycleSeconds),
                repeats = Mathf.Max(1, floorRepeats),
                count = floorOrbs.Count
            };
            SetFloorOrbs(floorSequence.active ? 0 : -1);
            if (floorSequence.active)
            {
                CocoonDebugLog.Info(
                    "Light",
                    "FLOORLIGHT orb sequence started. points=" + floorSequence.count +
                    ", cycle=" + floorSequence.cycleSeconds.ToString("0.###") +
                    "s, repeats=" + floorSequence.repeats +
                    ", authored orb sizes preserved.",
                    this);
            }
            else
            {
                CocoonDebugLog.Warn("Light", "FLOORLIGHT sequence requested but no runtime orb points were bound.", this);
            }
        }

        public void PlaySeatOutSequence()
        {
            ResolveReferences();
            seatSequence = new RunningSequence
            {
                active = seatOrbs.Count > 0,
                elapsed = 0f,
                cycleSeconds = Mathf.Max(0.01f, seatCycleSeconds),
                repeats = Mathf.Max(1, seatRepeats),
                count = seatOrbs.Count
            };
            SetSeatOrbsVisible(seatSequence.active);
            if (seatSequence.active)
            {
                CocoonDebugLog.Info(
                    "Light",
                    "SEATLIGHT orb pulse started. points=" + seatSequence.count +
                    ", cycle=" + seatSequence.cycleSeconds.ToString("0.###") +
                    "s, repeats=" + seatSequence.repeats +
                    ", authored orb sizes preserved.",
                    this);
            }
            else
            {
                CocoonDebugLog.Warn("Light", "SEATLIGHT sequence requested but no runtime orb points were bound.", this);
            }
        }

        public void PlayLuggagePreventerPulse()
        {
            ResolveReferences();
            preventerPulse = new PulseSequence
            {
                active = preventerProxyRoot != null && preventerOriginalRenderers.Count > 0,
                elapsed = 0f,
                pulseSeconds = Mathf.Max(0.01f, preventerPulseSeconds),
                repeats = Mathf.Max(1, preventerPulseRepeats)
            };
            SetPreventerProxyVisible(preventerPulse.active);
            if (preventerPulse.active)
            {
                CocoonDebugLog.Info(
                    "Light",
                    "Luggage preventer green proxy pulse started. pulse=" + preventerPulse.pulseSeconds.ToString("0.###") +
                    "s, repeats=" + preventerPulse.repeats + ".",
                    this);
            }
            else
            {
                CocoonDebugLog.Warn("Light", "Luggage preventer pulse requested but proxy renderers were not bound.", this);
            }
        }

        public void StopAll()
        {
            floorSequence.active = false;
            seatSequence.active = false;
            preventerPulse.active = false;
            SetFloorOrbs(-1);
            SetSeatOrbsVisible(false);
            SetPreventerProxyVisible(false);
        }

        public void ResolveReferences()
        {
            if (floorLightRoot == null)
            {
                floorLightRoot = FindSceneTransform("FLOORLIGHT");
            }

            if (seatLightRoot == null)
            {
                seatLightRoot = FindSceneTransform("SEATLIGHT");
            }

            if (luggagePreventerRoot == null)
            {
                luggagePreventerRoot = FindSceneTransform("luggage_preventrs_A1");
            }

            RebuildFloorLights();
            RebuildSeatOrbs();
            RebuildPreventerProxy();

            if (logBindings && !hasLogged)
            {
                hasLogged = true;
                CocoonDebugLog.Info(
                    "Light",
                    "Light sequence controller bound. floor=" + GetPath(floorLightRoot) +
                    " orbs=" + floorOrbs.Count +
                    ", seat=" + GetPath(seatLightRoot) +
                    " orbs=" + seatOrbs.Count +
                    ", preventer=" + GetPath(luggagePreventerRoot) +
                    " proxy=" + (preventerProxyRoot != null) + ".",
                    this);
            }
        }

        private void UpdateFloorSequence()
        {
            if (!floorSequence.active || floorSequence.count <= 0)
            {
                return;
            }

            floorSequence.elapsed += Time.deltaTime;
            if (floorSequence.elapsed >= floorSequence.cycleSeconds * floorSequence.repeats)
            {
                SetFloorOrbs(-1);
                floorSequence.active = false;
                return;
            }

            int activeIndex = EvaluateActiveIndex(floorSequence.elapsed, floorSequence.cycleSeconds, floorSequence.count, floorPointDuty);
            SetFloorOrbs(activeIndex);
        }

        private void UpdateSeatOrbSequence()
        {
            if (!seatSequence.active || seatSequence.count <= 0)
            {
                return;
            }

            seatSequence.elapsed += Time.deltaTime;
            if (seatSequence.elapsed >= seatSequence.cycleSeconds * seatSequence.repeats)
            {
                SetSeatOrbsVisible(false);
                seatSequence.active = false;
                return;
            }

            SetSeatOrbsVisible(EvaluatePulseVisible(seatSequence.elapsed, seatSequence.cycleSeconds, seatPointDuty));
        }

        private void UpdatePreventerPulse()
        {
            if (!preventerPulse.active)
            {
                return;
            }

            preventerPulse.elapsed += Time.deltaTime;
            if (preventerPulse.elapsed >= preventerPulse.pulseSeconds * preventerPulse.repeats)
            {
                SetPreventerProxyVisible(false);
                preventerPulse.active = false;
                return;
            }

            float cycle = preventerPulse.elapsed % preventerPulse.pulseSeconds;
            SetPreventerProxyVisible(cycle < preventerPulse.pulseSeconds * 0.5f);
        }

        private static int EvaluateActiveIndex(float elapsed, float cycleSeconds, int count, float duty)
        {
            float cycleTime = elapsed % cycleSeconds;
            float slotSeconds = cycleSeconds / Mathf.Max(1, count);
            int index = Mathf.Clamp(Mathf.FloorToInt(cycleTime / slotSeconds), 0, count - 1);
            float slotProgress = (cycleTime - index * slotSeconds) / Mathf.Max(0.0001f, slotSeconds);
            return slotProgress <= duty ? index : -1;
        }

        private static bool EvaluatePulseVisible(float elapsed, float cycleSeconds, float duty)
        {
            float cycleTime = elapsed % Mathf.Max(0.0001f, cycleSeconds);
            float cycleProgress = cycleTime / Mathf.Max(0.0001f, cycleSeconds);
            return cycleProgress <= duty;
        }

        private void RebuildFloorLights()
        {
            floorOrbs.Clear();
            floorLightSources.Clear();
            DisableLegacyRuntimeLights();
            if (floorLightRoot == null)
            {
                return;
            }

            List<Transform> points = CollectPointTransforms(floorLightRoot, "P");
            for (int i = 0; i < points.Count; i++)
            {
                DisablePointLights(points[i]);
                GameObject orb = EnsureFloorOrb(points[i]);
                floorOrbs.Add(orb);
                floorLightSources.Add(points[i]);
            }

            SetFloorOrbs(-1);
        }

        private void RebuildSeatOrbs()
        {
            seatOrbs.Clear();
            seatOrbSources.Clear();
            if (seatLightRoot == null)
            {
                return;
            }

            List<Transform> points = CollectPointTransforms(seatLightRoot, "A");
            for (int i = 0; i < points.Count; i++)
            {
                DisablePointLights(points[i]);
                Transform legacyOrb = FindDirectChild(points[i], "Cocoon Seat Light Orb");
                if (legacyOrb != null)
                {
                    legacyOrb.gameObject.SetActive(false);
                }

                GameObject orb = EnsureSeatOrb(points[i]);
                seatOrbs.Add(orb);
                seatOrbSources.Add(points[i]);
            }

            SetSeatOrbsVisible(false);
        }

        private void RebuildPreventerProxy()
        {
            preventerOriginalRenderers.Clear();
            preventerOriginalRendererEnabled.Clear();
            if (luggagePreventerRoot == null)
            {
                preventerProxyRoot = null;
                return;
            }

            DisablePointLights(luggagePreventerRoot);
            preventerProxyRoot = FindDirectChild(luggagePreventerRoot, "Cocoon Preventer Green Proxy");
            if (preventerProxyRoot == null)
            {
                var proxyObject = new GameObject("Cocoon Preventer Green Proxy");
                preventerProxyRoot = proxyObject.transform;
                preventerProxyRoot.SetParent(luggagePreventerRoot, false);
            }

            greenProxyMaterial = EnsureGreenProxyMaterial();
            Renderer[] renderers = luggagePreventerRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || IsDescendantOrSelf(renderer.transform, preventerProxyRoot))
                {
                    continue;
                }

                preventerOriginalRenderers.Add(renderer);
                preventerOriginalRendererEnabled.Add(renderer.enabled);
                EnsurePreventerProxyRenderer(renderer);
            }

            SetPreventerProxyVisible(false);
        }

        private GameObject EnsureFloorOrb(Transform point)
        {
            if (runtimeFloorOrbRoot == null)
            {
                runtimeFloorOrbRoot = EnsureRuntimeRoot("FLOORLIGHT Runtime Orbs");
            }

            Transform existing = FindDirectChild(runtimeFloorOrbRoot, point.name + " Orb");
            GameObject orb;
            bool wasExisting = existing != null;
            if (wasExisting)
            {
                orb = existing.gameObject;
            }
            else
            {
                orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = point.name + " Orb";
                orb.transform.SetParent(runtimeFloorOrbRoot, false);
                Collider collider = orb.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediateSafe(collider);
                }
            }

            if (!wasExisting)
            {
                orb.transform.SetPositionAndRotation(point.position, point.rotation);
                SetWorldScale(orb.transform, Vector3.one * Mathf.Max(0.0001f, floorOrbWorldRadius * 2f));
            }

            Renderer renderer = orb.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = EnsureGreenProxyMaterial();
                renderer.enabled = true;
            }

            CaptureAuthoredOrbPose(orb, point);
            orb.SetActive(false);
            return orb;
        }

        private GameObject EnsureSeatOrb(Transform point)
        {
            if (runtimeSeatOrbRoot == null)
            {
                runtimeSeatOrbRoot = EnsureRuntimeRoot("SEATLIGHT Runtime Orbs");
            }

            Transform existing = FindDirectChild(runtimeSeatOrbRoot, point.name + " Orb");
            GameObject orb;
            bool wasExisting = existing != null;
            if (wasExisting)
            {
                orb = existing.gameObject;
            }
            else
            {
                orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = point.name + " Orb";
                orb.transform.SetParent(runtimeSeatOrbRoot, false);
                Collider collider = orb.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediateSafe(collider);
                }
            }

            if (!wasExisting)
            {
                orb.transform.SetPositionAndRotation(point.position, point.rotation);
                SetWorldScale(orb.transform, Vector3.one * Mathf.Max(0.0001f, seatOrbWorldRadius * 2f));
            }

            Renderer renderer = orb.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = EnsureGreenProxyMaterial();
                renderer.enabled = true;
            }

            CaptureAuthoredOrbPose(orb, point);
            orb.SetActive(false);
            return orb;
        }

        private void EnsurePreventerProxyRenderer(Renderer source)
        {
            if (source == null || preventerProxyRoot == null)
            {
                return;
            }

            Transform existing = FindDirectChild(preventerProxyRoot, "Proxy_" + source.name);
            GameObject proxy = existing != null ? existing.gameObject : null;
            if (proxy == null)
            {
                proxy = new GameObject("Proxy_" + source.name);
                proxy.transform.SetParent(preventerProxyRoot, false);
            }

            proxy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            SetWorldScale(proxy.transform, source.transform.lossyScale);

            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            MeshFilter targetFilter = proxy.GetComponent<MeshFilter>();
            if (sourceFilter != null)
            {
                if (targetFilter == null)
                {
                    targetFilter = proxy.AddComponent<MeshFilter>();
                }

                targetFilter.sharedMesh = sourceFilter.sharedMesh;
            }

            MeshRenderer targetRenderer = proxy.GetComponent<MeshRenderer>();
            if (targetRenderer == null)
            {
                targetRenderer = proxy.AddComponent<MeshRenderer>();
            }

            targetRenderer.sharedMaterial = EnsureGreenProxyMaterial();
            targetRenderer.enabled = true;
        }

        private void SetFloorOrbs(int activeIndex)
        {
            for (int i = 0; i < floorOrbs.Count; i++)
            {
                GameObject orb = floorOrbs[i];
                if (orb == null)
                {
                    continue;
                }

                Transform source = i < floorLightSources.Count ? floorLightSources[i] : null;
                ApplyAuthoredOrbPose(orb, source, floorOrbWorldRadius);
                orb.SetActive(i == activeIndex);
            }
        }

        private void SetSeatOrbsVisible(bool visible)
        {
            for (int i = 0; i < seatOrbs.Count; i++)
            {
                GameObject orb = seatOrbs[i];
                if (orb != null)
                {
                    Transform source = i < seatOrbSources.Count ? seatOrbSources[i] : null;
                    ApplyAuthoredOrbPose(orb, source, seatOrbWorldRadius);
                    orb.SetActive(visible);
                }
            }
        }

        private void SyncRuntimeOrbPoses()
        {
            for (int i = 0; i < floorOrbs.Count; i++)
            {
                GameObject orb = floorOrbs[i];
                Transform source = i < floorLightSources.Count ? floorLightSources[i] : null;
                ApplyAuthoredOrbPose(orb, source, floorOrbWorldRadius);
            }

            for (int i = 0; i < seatOrbs.Count; i++)
            {
                GameObject orb = seatOrbs[i];
                Transform source = i < seatOrbSources.Count ? seatOrbSources[i] : null;
                ApplyAuthoredOrbPose(orb, source, seatOrbWorldRadius);
            }
        }

        private void CaptureAuthoredOrbPose(GameObject orb, Transform source)
        {
            if (orb == null)
            {
                return;
            }

            authoredOrbPoses[orb] = new AuthoredOrbPose(orb.transform, source);
        }

        private void ApplyAuthoredOrbPose(GameObject orb, Transform source, float fallbackRadius)
        {
            if (orb == null)
            {
                return;
            }

            if (source != null && authoredOrbPoses.TryGetValue(orb, out AuthoredOrbPose pose))
            {
                orb.transform.SetPositionAndRotation(
                    source.TransformPoint(pose.LocalPositionToSource),
                    source.rotation * pose.LocalRotationToSource);
                SetWorldScale(orb.transform, pose.WorldScale);
                return;
            }

            if (source != null)
            {
                orb.transform.SetPositionAndRotation(source.position, source.rotation);
            }

            if (!authoredOrbPoses.ContainsKey(orb))
            {
                SetWorldScale(orb.transform, Vector3.one * Mathf.Max(0.0001f, fallbackRadius * 2f));
            }
        }

        private void DisableLegacyRuntimeLights()
        {
            Transform legacyRoot = FindDirectChild(transform, "FLOORLIGHT Runtime Lights");
            if (legacyRoot == null)
            {
                return;
            }

            Light[] lights = legacyRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null)
                {
                    continue;
                }

                light.intensity = 0f;
                light.range = Mathf.Max(0f, floorLightRange);
                light.shadows = floorShadows;
                light.color = sequenceColor * Mathf.Max(0f, floorActiveIntensity);
            }

            DisablePointLights(legacyRoot);
            legacyRoot.gameObject.SetActive(false);
        }

        private Transform EnsureRuntimeRoot(string rootName)
        {
            Transform existing = FindDirectChild(transform, rootName);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root.transform;
        }

        private void SetPreventerProxyVisible(bool visible)
        {
            if (preventerProxyRoot != null)
            {
                preventerProxyRoot.gameObject.SetActive(visible);
            }

            for (int i = 0; i < preventerOriginalRenderers.Count; i++)
            {
                Renderer renderer = preventerOriginalRenderers[i];
                if (renderer != null)
                {
                    bool authoredEnabled = i < preventerOriginalRendererEnabled.Count
                        ? preventerOriginalRendererEnabled[i]
                        : true;
                    renderer.enabled = visible ? false : authoredEnabled;
                }
            }
        }

        private List<Transform> CollectPointTransforms(Transform root, string prefix)
        {
            var points = new List<Transform>();
            if (root == null)
            {
                return points;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            Array.Sort(children, (a, b) => ComparePointNames(a != null ? a.name : "", b != null ? b.name : "", prefix));
            for (int i = 0; i < children.Length; i++)
            {
                Transform point = children[i];
                if (point != null && point != root && IsPointName(point.name, prefix))
                {
                    points.Add(point);
                }
            }

            return points;
        }

        private void DisablePointLights(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Light[] lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].intensity = 0f;
                    lights[i].enabled = false;
                }
            }
        }

        private Material EnsureGreenProxyMaterial()
        {
            if (greenProxyMaterial != null)
            {
                return greenProxyMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            greenProxyMaterial = new Material(shader)
            {
                name = "Cocoon Runtime Green Light Proxy",
                color = sequenceColor
            };
            if (greenProxyMaterial.HasProperty("_BaseColor"))
            {
                greenProxyMaterial.SetColor("_BaseColor", sequenceColor);
            }

            if (greenProxyMaterial.HasProperty("_Color"))
            {
                greenProxyMaterial.SetColor("_Color", sequenceColor);
            }

            return greenProxyMaterial;
        }

        private static void SetWorldScale(Transform target, Vector3 worldScale)
        {
            if (target == null)
            {
                return;
            }

            Transform parent = target.parent;
            if (parent == null)
            {
                target.localScale = worldScale;
                return;
            }

            Vector3 parentScale = parent.lossyScale;
            target.localScale = new Vector3(
                Mathf.Abs(parentScale.x) > 0.0001f ? worldScale.x / parentScale.x : worldScale.x,
                Mathf.Abs(parentScale.y) > 0.0001f ? worldScale.y / parentScale.y : worldScale.y,
                Mathf.Abs(parentScale.z) > 0.0001f ? worldScale.z / parentScale.z : worldScale.z);
        }

        private static void DestroyImmediateSafe(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsDescendantOrSelf(Transform candidate, Transform root)
        {
            if (candidate == null || root == null)
            {
                return false;
            }

            Transform cursor = candidate;
            while (cursor != null)
            {
                if (cursor == root)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private static bool IsPointName(string name, string prefix)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(prefix) || !name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (int i = prefix.Length; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i]))
                {
                    return false;
                }
            }

            return name.Length > prefix.Length;
        }

        private static int ComparePointNames(string a, string b, string prefix)
        {
            int ai = ExtractPointIndex(a, prefix);
            int bi = ExtractPointIndex(b, prefix);
            int compare = ai.CompareTo(bi);
            return compare != 0 ? compare : string.CompareOrdinal(a, b);
        }

        private static int ExtractPointIndex(string name, string prefix)
        {
            if (!IsPointName(name, prefix))
            {
                return int.MaxValue;
            }

            int.TryParse(name.Substring(prefix.Length), out int value);
            return value;
        }

        private static Transform FindSceneTransform(string name)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    candidate.hideFlags != HideFlags.None ||
                    !candidate.gameObject.scene.IsValid() ||
                    !string.Equals(candidate.name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                return candidate;
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
            Transform cursor = transform.parent;
            while (cursor != null)
            {
                path = cursor.name + "/" + path;
                cursor = cursor.parent;
            }

            return path;
        }
    }
}
