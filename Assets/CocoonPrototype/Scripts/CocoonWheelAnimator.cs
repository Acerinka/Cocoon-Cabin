using UnityEngine;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonWheelAnimator : MonoBehaviour
    {
        [Header("Vehicle")]
        [SerializeField] private Transform vehicleRoot;
        [SerializeField] private bool autoBindByName = true;

        [Header("Wheels")]
        [SerializeField] private Transform wheel1;
        [SerializeField] private Transform wheel2;
        [SerializeField] private Transform wheel3;
        [SerializeField] private Transform wheel4;
        [SerializeField] private Transform hub1;
        [SerializeField] private Transform hub2;
        [SerializeField] private Transform hub3;
        [SerializeField] private Transform hub4;
        [SerializeField] private bool useCenteredRollingPivots = true;
        [SerializeField] private Vector3 localRollAxis = Vector3.right;
        [SerializeField] private float radiusOverride;
        [SerializeField] private float rollDirection = 1f;

        [Header("Ground Snap")]
        [SerializeField] private bool snapBottomsOnStart = true;
        [SerializeField] private float groundYOffset;
        [SerializeField] private float groundRaycastHeight = 0.5f;
        [SerializeField] private float groundRaycastDistance = 1.2f;
        [SerializeField] private LayerMask groundMask = ~0;

        private readonly Transform[] wheels = new Transform[4];
        private readonly Transform[] hubs = new Transform[4];
        private readonly Transform[] wheelPivots = new Transform[4];
        private Vector3 previousRootPosition;
        private bool hasInitialized;
        private bool isActiveAnimator = true;

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            Initialize();
            Transform root = ResolveVehicleRoot();
            if (root == null || !isActiveAnimator)
            {
                return;
            }

            Vector3 delta = root.position - previousRootPosition;
            previousRootPosition = root.position;
            if (delta.sqrMagnitude < 0.00000001f)
            {
                return;
            }

            Vector3 forward = root.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 flatDelta = delta;
            flatDelta.y = 0f;
            float signedDistance = Mathf.Sign(Vector3.Dot(flatDelta, forward)) * flatDelta.magnitude;
            Vector3 rollAxis = localRollAxis.sqrMagnitude > 0.0001f ? localRollAxis.normalized : Vector3.right;
            for (int i = 0; i < wheels.Length; i++)
            {
                Transform pivot = wheelPivots[i] != null ? wheelPivots[i] : wheels[i];
                if (pivot == null)
                {
                    continue;
                }

                float radius = Mathf.Max(0.0001f, ResolveWheelRadius(wheels[i]));
                float angle = signedDistance / radius * Mathf.Rad2Deg * rollDirection;
                pivot.Rotate(rollAxis, angle, Space.Self);

                if (wheelPivots[i] == null && hubs[i] != null && !IsDescendantOrSelf(hubs[i], wheels[i]))
                {
                    hubs[i].Rotate(rollAxis, angle, Space.Self);
                }
            }
        }

        [ContextMenu("Bind And Snap Wheels")]
        public void Initialize()
        {
            Transform root = ResolveVehicleRoot();
            if (root == null)
            {
                return;
            }

            if (autoBindByName)
            {
                wheel1 = ResolveExactNamedReference(root, wheel1, "wheel1");
                wheel2 = ResolveExactNamedReference(root, wheel2, "wheel2");
                wheel3 = ResolveExactNamedReference(root, wheel3, "wheel3");
                wheel4 = ResolveExactNamedReference(root, wheel4, "wheel4");
                hub1 = ResolveExactNamedReference(root, hub1, "w1");
                hub2 = ResolveExactNamedReference(root, hub2, "w2");
                hub3 = ResolveExactNamedReference(root, hub3, "w3");
                hub4 = ResolveExactNamedReference(root, hub4, "w4");
            }

            wheels[0] = wheel1;
            wheels[1] = wheel2;
            wheels[2] = wheel3;
            wheels[3] = wheel4;
            hubs[0] = hub1;
            hubs[1] = hub2;
            hubs[2] = hub3;
            hubs[3] = hub4;

            int boundWheelCount = CountBoundWheels();
            if (boundWheelCount == 0)
            {
                isActiveAnimator = false;
                enabled = false;
                return;
            }

            if (!hasInitialized)
            {
                hasInitialized = true;
                if (useCenteredRollingPivots)
                {
                    EnsureCenteredRollingPivots();
                }

                if (snapBottomsOnStart)
                {
                    SnapWheelBottomsToGround();
                }

                previousRootPosition = root.position;
                CocoonDebugLog.Info("Taxi", "Wheel animator bound " + boundWheelCount + "/4 wheel(s), hubs=" + CountBoundHubs() + "/4, centeredPivots=" + useCenteredRollingPivots + ", radius=" + ResolveAverageRadius().ToString("0.###") + ".", this);
            }
        }

        private Transform ResolveVehicleRoot()
        {
            if (vehicleRoot != null)
            {
                return vehicleRoot;
            }

            vehicleRoot = transform;
            return vehicleRoot;
        }

        private void EnsureCenteredRollingPivots()
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                Transform wheel = wheels[i];
                if (wheel == null || !TryGetRendererBounds(wheel, out Bounds bounds))
                {
                    continue;
                }

                string pivotName = wheel.name + " Rolling Pivot";
                Transform existingParent = wheel.parent;
                if (existingParent != null && existingParent.name == pivotName)
                {
                    wheelPivots[i] = existingParent;
                    AttachHubToPivot(i, existingParent);
                    continue;
                }

                Transform parent = existingParent;
                GameObject pivotObject = new GameObject(pivotName);
                Transform pivot = pivotObject.transform;
                pivot.SetParent(parent, true);
                pivot.position = bounds.center;
                pivot.rotation = wheel.rotation;
                pivot.localScale = Vector3.one;
                wheel.SetParent(pivot, true);
                wheelPivots[i] = pivot;
                AttachHubToPivot(i, pivot);
            }
        }

        private void AttachHubToPivot(int index, Transform pivot)
        {
            if (pivot == null || index < 0 || index >= hubs.Length)
            {
                return;
            }

            Transform hub = hubs[index];
            if (hub == null ||
                hub == pivot ||
                IsDescendantOrSelf(hub, pivot) ||
                IsDescendantOrSelf(hub, wheels[index]))
            {
                return;
            }

            hub.SetParent(pivot, true);
        }

        private void SnapWheelBottomsToGround()
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                Transform wheel = wheels[i];
                if (wheel == null || !TryGetRendererBounds(wheel, out Bounds bounds))
                {
                    continue;
                }

                float groundY = ResolveGroundY(bounds.center) + groundYOffset;
                Transform mover = wheelPivots[i] != null ? wheelPivots[i] : wheel;
                mover.position += Vector3.up * (groundY - bounds.min.y);
            }
        }

        private float ResolveWheelRadius(Transform wheel)
        {
            if (radiusOverride > 0.0001f)
            {
                return radiusOverride;
            }

            if (!TryGetRendererBounds(wheel, out Bounds bounds))
            {
                return 0.03f;
            }

            return Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        private float ResolveAverageRadius()
        {
            float total = 0f;
            int count = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null)
                {
                    continue;
                }

                total += ResolveWheelRadius(wheels[i]);
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        private int CountBoundWheels()
        {
            int count = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountBoundHubs()
        {
            int count = 0;
            for (int i = 0; i < hubs.Length; i++)
            {
                if (hubs[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private float ResolveGroundY(Vector3 referencePosition)
        {
            Vector3 origin = referencePosition + Vector3.up * Mathf.Max(0.01f, groundRaycastHeight);
            float distance = Mathf.Max(0.05f, groundRaycastHeight + groundRaycastDistance);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            Transform root = ResolveVehicleRoot();
            return root != null ? root.position.y : 0f;
        }

        private static Transform ResolveExactNamedReference(Transform searchRoot, Transform current, string expectedName)
        {
            if (current != null && current.name == expectedName)
            {
                return current;
            }

            return FindExactNamedDescendant(searchRoot, expectedName);
        }

        private static Transform FindExactNamedDescendant(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == targetName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
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
    }
}
