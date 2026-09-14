using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

namespace CocoonPrototype
{
    [DefaultExecutionOrder(-925)]
    public sealed class CocoonXRHandVisualizer : MonoBehaviour
    {
        [SerializeField] private Transform leftControllerProxy;
        [SerializeField] private Transform rightControllerProxy;
        [SerializeField] private GameObject leftOfficialHandPrefab;
        [SerializeField] private GameObject rightOfficialHandPrefab;
        [SerializeField] private bool useOfficialHandPrefabs;
        [SerializeField] private bool hideControllerProxyWhenHandTracked;
        [SerializeField] private bool showControllerTrackedHandFallback;
        [SerializeField] private float jointRadius = 0.0075f;
        [SerializeField] private float boneRadius = 0.0045f;
        [SerializeField] private float palmRadius = 0.012f;
        [SerializeField, Range(0.05f, 0.8f)] private float handAlpha = 0.34f;
        [SerializeField] private Color handColor = new Color(0.72f, 0.96f, 1f, 0.34f);
        [SerializeField] private Vector3 controllerHandOffset = new Vector3(0f, -0.012f, 0.035f);
        [SerializeField] private Vector3 controllerHandEuler = new Vector3(8f, 0f, 0f);

        private XRHandSubsystem handSubsystem;
        private Material handMaterial;
        private HandView leftHand;
        private HandView rightHand;
        private ControllerHandView leftControllerHand;
        private ControllerHandView rightControllerHand;
        private GameObject leftOfficialHandInstance;
        private GameObject rightOfficialHandInstance;
        private bool hasLoggedSubsystem;
        private bool hasLoggedNoSubsystem;
        private bool hasLoggedFallback;
        private bool hasLoggedOfficialMesh;

        private static readonly XRHandJointID[] JointIds =
        {
            XRHandJointID.Wrist,
            XRHandJointID.Palm,
            XRHandJointID.ThumbMetacarpal,
            XRHandJointID.ThumbProximal,
            XRHandJointID.ThumbDistal,
            XRHandJointID.ThumbTip,
            XRHandJointID.IndexMetacarpal,
            XRHandJointID.IndexProximal,
            XRHandJointID.IndexIntermediate,
            XRHandJointID.IndexDistal,
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleMetacarpal,
            XRHandJointID.MiddleProximal,
            XRHandJointID.MiddleIntermediate,
            XRHandJointID.MiddleDistal,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingMetacarpal,
            XRHandJointID.RingProximal,
            XRHandJointID.RingIntermediate,
            XRHandJointID.RingDistal,
            XRHandJointID.RingTip,
            XRHandJointID.LittleMetacarpal,
            XRHandJointID.LittleProximal,
            XRHandJointID.LittleIntermediate,
            XRHandJointID.LittleDistal,
            XRHandJointID.LittleTip
        };

        private static readonly Bone[] BonePairs =
        {
            new Bone(XRHandJointID.Wrist, XRHandJointID.Palm),
            new Bone(XRHandJointID.Wrist, XRHandJointID.ThumbMetacarpal),
            new Bone(XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal),
            new Bone(XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal),
            new Bone(XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip),
            new Bone(XRHandJointID.Wrist, XRHandJointID.IndexMetacarpal),
            new Bone(XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal),
            new Bone(XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate),
            new Bone(XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal),
            new Bone(XRHandJointID.IndexDistal, XRHandJointID.IndexTip),
            new Bone(XRHandJointID.Wrist, XRHandJointID.MiddleMetacarpal),
            new Bone(XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal),
            new Bone(XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate),
            new Bone(XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal),
            new Bone(XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip),
            new Bone(XRHandJointID.Wrist, XRHandJointID.RingMetacarpal),
            new Bone(XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal),
            new Bone(XRHandJointID.RingProximal, XRHandJointID.RingIntermediate),
            new Bone(XRHandJointID.RingIntermediate, XRHandJointID.RingDistal),
            new Bone(XRHandJointID.RingDistal, XRHandJointID.RingTip),
            new Bone(XRHandJointID.Wrist, XRHandJointID.LittleMetacarpal),
            new Bone(XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal),
            new Bone(XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate),
            new Bone(XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal),
            new Bone(XRHandJointID.LittleDistal, XRHandJointID.LittleTip),
            new Bone(XRHandJointID.IndexMetacarpal, XRHandJointID.MiddleMetacarpal),
            new Bone(XRHandJointID.MiddleMetacarpal, XRHandJointID.RingMetacarpal),
            new Bone(XRHandJointID.RingMetacarpal, XRHandJointID.LittleMetacarpal)
        };

        private void Awake()
        {
            handMaterial = CreateHandMaterial();
            leftHand = new HandView("Transparent Left XR Hand", transform, handMaterial, jointRadius, boneRadius, palmRadius);
            rightHand = new HandView("Transparent Right XR Hand", transform, handMaterial, jointRadius, boneRadius, palmRadius);
            EnsureOfficialHandInstances();
            EnsureControllerHandFallbacks();
            SetHandVisible(leftHand, false);
            SetHandVisible(rightHand, false);
            SetOfficialHandVisible(leftOfficialHandInstance, false);
            SetOfficialHandVisible(rightOfficialHandInstance, false);
            SetControllerHandVisible(leftControllerHand, false);
            SetControllerHandVisible(rightControllerHand, false);
        }

        private void OnEnable()
        {
            if (useOfficialHandPrefabs || showControllerTrackedHandFallback || hideControllerProxyWhenHandTracked)
            {
                ResolveHandSubsystem();
            }
        }

        private void LateUpdate()
        {
            if (!useOfficialHandPrefabs && !showControllerTrackedHandFallback && !hideControllerProxyWhenHandTracked)
            {
                SetHandVisible(leftHand, false);
                SetHandVisible(rightHand, false);
                SetOfficialHandVisible(leftOfficialHandInstance, false);
                SetOfficialHandVisible(rightOfficialHandInstance, false);
                SetControllerHandVisible(leftControllerHand, false);
                SetControllerHandVisible(rightControllerHand, false);
                SetProxyRenderersVisible(leftControllerProxy, true);
                SetProxyRenderersVisible(rightControllerProxy, true);
                return;
            }

            if (handSubsystem == null || !handSubsystem.running)
            {
                ResolveHandSubsystem();
            }

            bool leftTracked = handSubsystem != null && handSubsystem.running && handSubsystem.leftHand.isTracked;
            bool rightTracked = handSubsystem != null && handSubsystem.running && handSubsystem.rightHand.isTracked;

            bool leftOfficialVisible = leftTracked && IsOfficialHandAvailable(leftOfficialHandInstance);
            bool rightOfficialVisible = rightTracked && IsOfficialHandAvailable(rightOfficialHandInstance);

            if (leftTracked && !leftOfficialVisible)
            {
                UpdateHand(leftHand, handSubsystem.leftHand);
            }

            if (rightTracked && !rightOfficialVisible)
            {
                UpdateHand(rightHand, handSubsystem.rightHand);
            }

            SetHandVisible(leftHand, leftTracked && !leftOfficialVisible);
            SetHandVisible(rightHand, rightTracked && !rightOfficialVisible);
            SetOfficialHandVisible(leftOfficialHandInstance, leftOfficialVisible);
            SetOfficialHandVisible(rightOfficialHandInstance, rightOfficialVisible);
            if ((leftOfficialVisible || rightOfficialVisible) && !hasLoggedOfficialMesh)
            {
                hasLoggedOfficialMesh = true;
                CocoonDebugLog.Info("XRHands", "Official Unity XR Hands mesh prefabs are visible and driven by hand tracking data.", this);
            }

            EnsureControllerHandFallbacks();
            bool leftFallbackVisible = showControllerTrackedHandFallback && !leftTracked && leftControllerProxy != null;
            bool rightFallbackVisible = showControllerTrackedHandFallback && !rightTracked && rightControllerProxy != null;
            if (leftFallbackVisible)
            {
                leftControllerHand.UpdateFromProxy(leftControllerProxy, true, controllerHandOffset, controllerHandEuler);
            }

            if (rightFallbackVisible)
            {
                rightControllerHand.UpdateFromProxy(rightControllerProxy, false, controllerHandOffset, controllerHandEuler);
            }

            SetControllerHandVisible(leftControllerHand, leftFallbackVisible);
            SetControllerHandVisible(rightControllerHand, rightFallbackVisible);
            SetProxyRenderersVisible(leftControllerProxy, !(leftTracked || leftFallbackVisible) || !hideControllerProxyWhenHandTracked);
            SetProxyRenderersVisible(rightControllerProxy, !(rightTracked || rightFallbackVisible) || !hideControllerProxyWhenHandTracked);

            if ((leftFallbackVisible || rightFallbackVisible) && !hasLoggedFallback)
            {
                hasLoggedFallback = true;
                CocoonDebugLog.Info("XRHands", "XR hand tracking is unavailable or not tracked; controller proxy spheres remain visible.", this);
            }
        }

        public void Configure(Transform leftProxy, Transform rightProxy, GameObject leftHandPrefab = null, GameObject rightHandPrefab = null)
        {
            leftControllerProxy = leftProxy;
            rightControllerProxy = rightProxy;

            if (leftHandPrefab != null && leftOfficialHandPrefab != leftHandPrefab)
            {
                leftOfficialHandPrefab = leftHandPrefab;
                DestroyOfficialHandInstance(ref leftOfficialHandInstance);
            }

            if (rightHandPrefab != null && rightOfficialHandPrefab != rightHandPrefab)
            {
                rightOfficialHandPrefab = rightHandPrefab;
                DestroyOfficialHandInstance(ref rightOfficialHandInstance);
            }

            if (Application.isPlaying)
            {
                EnsureOfficialHandInstances();
            }

            if (showControllerTrackedHandFallback)
            {
                EnsureControllerHandFallbacks();
            }
        }

        private void EnsureOfficialHandInstances()
        {
            if (!useOfficialHandPrefabs)
            {
                return;
            }

            EnsureOfficialHandInstance(leftOfficialHandPrefab, "Official Unity Left XR Hand", ref leftOfficialHandInstance);
            EnsureOfficialHandInstance(rightOfficialHandPrefab, "Official Unity Right XR Hand", ref rightOfficialHandInstance);
        }

        private void EnsureOfficialHandInstance(GameObject prefab, string name, ref GameObject instance)
        {
            if (prefab == null || instance != null)
            {
                return;
            }

            instance = Instantiate(prefab, transform);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            AssignMaterialToOfficialHand(instance);
            instance.SetActive(true);
        }

        private void AssignMaterialToOfficialHand(GameObject instance)
        {
            if (instance == null || handMaterial == null)
            {
                return;
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    materials[j] = handMaterial;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static bool IsOfficialHandAvailable(GameObject instance)
        {
            return instance != null && instance.GetComponentInChildren<Renderer>(true) != null;
        }

        private static void SetOfficialHandVisible(GameObject instance, bool visible)
        {
            if (instance == null)
            {
                return;
            }

            if (!instance.activeSelf)
            {
                instance.SetActive(true);
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled != visible)
                {
                    renderers[i].enabled = visible;
                }
            }
        }

        private static void DestroyOfficialHandInstance(ref GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }

            instance = null;
        }

        private void EnsureControllerHandFallbacks()
        {
            if (handMaterial == null)
            {
                return;
            }

            if (leftControllerHand == null)
            {
                leftControllerHand = new ControllerHandView("Transparent Left Controller Hand", transform, handMaterial, true);
            }

            if (rightControllerHand == null)
            {
                rightControllerHand = new ControllerHandView("Transparent Right Controller Hand", transform, handMaterial, false);
            }
        }

        private void ResolveHandSubsystem()
        {
            handSubsystem = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRHandSubsystem>();
            if (handSubsystem == null)
            {
                var subsystems = new List<XRHandSubsystem>();
                SubsystemManager.GetSubsystems(subsystems);
                for (int i = 0; i < subsystems.Count; i++)
                {
                    if (subsystems[i] != null)
                    {
                        handSubsystem = subsystems[i];
                        break;
                    }
                }
            }

            if (handSubsystem != null)
            {
                if (!handSubsystem.running)
                {
                    handSubsystem.Start();
                }

                if (!hasLoggedSubsystem)
                {
                    hasLoggedSubsystem = true;
                    CocoonDebugLog.Info("XRHands", "XR Hands subsystem active; transparent articulated hands will replace controller proxy spheres when tracked.", this);
                }
            }
            else if (!hasLoggedNoSubsystem)
            {
                hasLoggedNoSubsystem = true;
                CocoonDebugLog.Warn("XRHands", "XR Hands subsystem is not available yet. Enable OpenXR Hand Tracking or run on a hand-tracking-capable device; controller proxies remain as fallback.", this);
            }
        }

        private void UpdateHand(HandView view, XRHand hand)
        {
            var positions = view.JointPositions;
            for (int i = 0; i < JointIds.Length; i++)
            {
                XRHandJointID jointId = JointIds[i];
                XRHandJoint joint = hand.GetJoint(jointId);
                int index = jointId.ToIndex();
                if (!joint.TryGetPose(out Pose pose))
                {
                    view.SetJointVisible(index, false);
                    continue;
                }

                positions[index] = pose.position;
                Transform jointTransform = view.Joints[index];
                jointTransform.localPosition = pose.position;
                jointTransform.localRotation = pose.rotation;
                jointTransform.localScale = Vector3.one * GetJointVisualRadius(jointId);
                view.SetJointVisible(index, true);
            }

            for (int i = 0; i < BonePairs.Length; i++)
            {
                Bone bone = BonePairs[i];
                bool fromValid = view.IsJointVisible(bone.From.ToIndex());
                bool toValid = view.IsJointVisible(bone.To.ToIndex());
                Transform boneTransform = view.BoneTransforms[i];
                if (!fromValid || !toValid)
                {
                    boneTransform.gameObject.SetActive(false);
                    continue;
                }

                Vector3 from = positions[bone.From.ToIndex()];
                Vector3 to = positions[bone.To.ToIndex()];
                Vector3 delta = to - from;
                float length = delta.magnitude;
                if (length <= 0.0001f)
                {
                    boneTransform.gameObject.SetActive(false);
                    continue;
                }

                boneTransform.gameObject.SetActive(true);
                boneTransform.localPosition = (from + to) * 0.5f;
                boneTransform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
                boneTransform.localScale = new Vector3(boneRadius, length * 0.5f, boneRadius);
            }
        }

        private float GetJointVisualRadius(XRHandJointID jointId)
        {
            return jointId == XRHandJointID.Palm || jointId == XRHandJointID.Wrist ? palmRadius : jointRadius;
        }

        private Material CreateHandMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = "Cocoon Runtime Transparent XR Hand"
            };
            Color color = handColor;
            color.a = handAlpha;
            SetMaterialColor(material, color);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetHandVisible(HandView view, bool visible)
        {
            if (view != null)
            {
                view.Root.SetActive(visible);
            }
        }

        private static void SetProxyRenderersVisible(Transform proxy, bool visible)
        {
            if (proxy == null)
            {
                return;
            }

            Renderer[] renderers = proxy.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
        }

        private static void SetControllerHandVisible(ControllerHandView view, bool visible)
        {
            if (view != null)
            {
                view.Root.SetActive(visible);
            }
        }

        private readonly struct Bone
        {
            public readonly XRHandJointID From;
            public readonly XRHandJointID To;

            public Bone(XRHandJointID from, XRHandJointID to)
            {
                From = from;
                To = to;
            }
        }

        private sealed class ControllerHandView
        {
            public readonly GameObject Root;
            private readonly bool isLeft;

            public ControllerHandView(string name, Transform parent, Material material, bool left)
            {
                isLeft = left;
                Root = new GameObject(name);
                Root.transform.SetParent(parent, false);
                CreatePalm(material);
                CreateFingers(material);
                CreateThumb(material);
            }

            public void UpdateFromProxy(Transform proxy, bool left, Vector3 localOffset, Vector3 localEuler)
            {
                if (proxy == null)
                {
                    return;
                }

                Vector3 handedOffset = localOffset;
                handedOffset.x = Mathf.Abs(localOffset.x) * (left ? -1f : 1f);
                Quaternion handedRotation = Quaternion.Euler(localEuler.x, localEuler.y * (left ? -1f : 1f), localEuler.z * (left ? -1f : 1f));
                Root.transform.position = proxy.TransformPoint(handedOffset);
                Root.transform.rotation = proxy.rotation * handedRotation;
            }

            private void CreatePalm(Material material)
            {
                GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                palm.name = "Palm";
                palm.transform.SetParent(Root.transform, false);
                palm.transform.localPosition = Vector3.zero;
                palm.transform.localScale = new Vector3(0.045f, 0.018f, 0.062f);
                AssignTransparentHandMaterial(palm, material);
            }

            private void CreateFingers(Material material)
            {
                float[] xOffsets = { -0.019f, -0.006f, 0.007f, 0.019f };
                float handed = isLeft ? -1f : 1f;
                for (int i = 0; i < xOffsets.Length; i++)
                {
                    float length = i == 3 ? 0.047f : 0.055f;
                    float x = xOffsets[i] * handed;
                    GameObject finger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    finger.name = "Finger " + (i + 1);
                    finger.transform.SetParent(Root.transform, false);
                    finger.transform.localPosition = new Vector3(x, 0.002f, 0.046f + length * 0.5f);
                    finger.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    finger.transform.localScale = new Vector3(0.006f, length * 0.5f, 0.006f);
                    AssignTransparentHandMaterial(finger, material);

                    GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    tip.name = "Finger " + (i + 1) + " Tip";
                    tip.transform.SetParent(Root.transform, false);
                    tip.transform.localPosition = new Vector3(x, 0.002f, 0.046f + length);
                    tip.transform.localScale = Vector3.one * 0.012f;
                    AssignTransparentHandMaterial(tip, material);
                }
            }

            private void CreateThumb(Material material)
            {
                float handed = isLeft ? -1f : 1f;
                GameObject thumb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                thumb.name = "Thumb";
                thumb.transform.SetParent(Root.transform, false);
                thumb.transform.localPosition = new Vector3(0.029f * handed, -0.002f, 0.024f);
                thumb.transform.localRotation = Quaternion.Euler(55f, 0f, -38f * handed);
                thumb.transform.localScale = new Vector3(0.007f, 0.026f, 0.007f);
                AssignTransparentHandMaterial(thumb, material);

                GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.name = "Thumb Tip";
                tip.transform.SetParent(Root.transform, false);
                tip.transform.localPosition = new Vector3(0.045f * handed, -0.003f, 0.043f);
                tip.transform.localScale = Vector3.one * 0.013f;
                AssignTransparentHandMaterial(tip, material);
            }

            private static void AssignTransparentHandMaterial(GameObject visual, Material material)
            {
                Collider collider = visual.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                Renderer renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }

        private sealed class HandView
        {
            public readonly GameObject Root;
            public readonly Transform[] Joints = new Transform[XRHandJointID.EndMarker.ToIndex()];
            public readonly Transform[] BoneTransforms = new Transform[CocoonXRHandVisualizer.BonePairs.Length];
            public readonly Vector3[] JointPositions = new Vector3[XRHandJointID.EndMarker.ToIndex()];
            private readonly bool[] jointVisible = new bool[XRHandJointID.EndMarker.ToIndex()];

            public HandView(string name, Transform parent, Material material, float jointRadius, float boneRadius, float palmRadius)
            {
                Root = new GameObject(name);
                Root.transform.SetParent(parent, false);
                for (int i = 0; i < JointIds.Length; i++)
                {
                    XRHandJointID jointId = JointIds[i];
                    int index = jointId.ToIndex();
                    GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    joint.name = jointId.ToString();
                    joint.transform.SetParent(Root.transform, false);
                    joint.transform.localScale = Vector3.one * (jointId == XRHandJointID.Palm || jointId == XRHandJointID.Wrist ? palmRadius : jointRadius);
                    Collider collider = joint.GetComponent<Collider>();
                    if (collider != null)
                    {
                        Object.Destroy(collider);
                    }

                    Renderer renderer = joint.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = material;
                    }

                    Joints[index] = joint.transform;
                }

                for (int i = 0; i < CocoonXRHandVisualizer.BonePairs.Length; i++)
                {
                    GameObject bone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    bone.name = CocoonXRHandVisualizer.BonePairs[i].From + " to " + CocoonXRHandVisualizer.BonePairs[i].To;
                    bone.transform.SetParent(Root.transform, false);
                    bone.transform.localScale = new Vector3(boneRadius, boneRadius, boneRadius);
                    Collider collider = bone.GetComponent<Collider>();
                    if (collider != null)
                    {
                        Object.Destroy(collider);
                    }

                    Renderer renderer = bone.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = material;
                    }

                    BoneTransforms[i] = bone.transform;
                }
            }

            public bool IsJointVisible(int index)
            {
                return index >= 0 && index < jointVisible.Length && jointVisible[index];
            }

            public void SetJointVisible(int index, bool visible)
            {
                if (index < 0 || index >= jointVisible.Length)
                {
                    return;
                }

                jointVisible[index] = visible;
                Transform joint = Joints[index];
                if (joint != null)
                {
                    joint.gameObject.SetActive(visible);
                }
            }
        }
    }
}
