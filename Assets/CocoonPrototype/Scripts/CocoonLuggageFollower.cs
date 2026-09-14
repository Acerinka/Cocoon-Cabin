using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonLuggageFollower : MonoBehaviour
    {
        [Header("Follow Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform fallbackTarget;
        [SerializeField] private bool preferLeftHandTarget = true;
        [SerializeField] private Vector3 localOffset = new Vector3(-0.04f, 0f, 0.02f);
        [SerializeField] private Vector3 restLocalOffset = new Vector3(-0.12f, 0f, 0.03f);
        [SerializeField] private bool followYawOnly = true;
        [SerializeField] private bool scaleWithExperience;
        [SerializeField] private bool scaleModelWithExperience = false;
        [SerializeField] private float modelScale = 1f;
        [SerializeField] private bool followOnlyWhileLeftGripHeld = true;
        [SerializeField] private bool updateWhilePlaying = true;

        [Header("Grounded Bounds Placement")]
        [SerializeField] private bool alignRendererBoundsToTarget = true;
        [SerializeField] private float groundYOffset;
        [SerializeField] private float groundRaycastHeight = 0.6f;
        [SerializeField] private float groundRaycastDistance = 1.4f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Transform elevatedSupportRoot;
        [SerializeField] private string elevatedSupportTag = "solid";
        [SerializeField] private float elevatedSupportPadding = 0.04f;

        [Header("Pose")]
        [SerializeField] private Vector3 localEuler = new Vector3(0f, 0f, 0f);

        private readonly List<XRInputDevice> leftDevices = new List<XRInputDevice>();
        private bool hasLoggedGripFollowState;
        private bool previousGripFollowAllowed;

        public void ConfigureFollowTarget(Transform preferredTarget, Transform fallback, bool useExperienceScaledOffset)
        {
            target = preferredTarget;
            fallbackTarget = fallback;
            scaleWithExperience = useExperienceScaledOffset;
            hasLoggedGripFollowState = false;
            previousGripFollowAllowed = false;
        }

        public void ConfigureElevatedSupport(Transform supportRoot, string supportTag, float supportPadding)
        {
            elevatedSupportRoot = supportRoot;
            if (!string.IsNullOrWhiteSpace(supportTag))
            {
                elevatedSupportTag = supportTag;
            }

            elevatedSupportPadding = Mathf.Max(0f, supportPadding);
        }

        private void OnEnable()
        {
            PlaceAtRestBesideTarget();
        }

        private void LateUpdate()
        {
            if (updateWhilePlaying)
            {
                ApplyFollowIfAllowed();
            }
        }

        public void ApplyFollowIfAllowed()
        {
            bool followAllowed = !followOnlyWhileLeftGripHeld || IsLeftGripHeld();
            if (!hasLoggedGripFollowState || previousGripFollowAllowed != followAllowed)
            {
                if (hasLoggedGripFollowState && previousGripFollowAllowed && !followAllowed)
                {
                    DropToGroundAtCurrentPosition();
                }

                hasLoggedGripFollowState = true;
                previousGripFollowAllowed = followAllowed;
                CocoonDebugLog.Info("Luggage", "Left grip luggage follow " + (followAllowed ? "active" : "paused") + ".", this);
            }

            if (followAllowed)
            {
                ApplyFollow();
            }
        }

        [ContextMenu("Apply Luggage Follow Now")]
        public void ApplyFollow()
        {
            Transform resolvedTarget = ResolveTarget();
            if (resolvedTarget == null)
            {
                return;
            }

            ApplyBoundsPlacement(resolvedTarget, localOffset);
        }

        [ContextMenu("Place Luggage At Rest")]
        public void PlaceAtRestBesideTarget()
        {
            Transform resolvedTarget = ResolveRestTarget();
            if (resolvedTarget == null)
            {
                return;
            }

            ApplyBoundsPlacement(resolvedTarget, restLocalOffset);
        }

        [ContextMenu("Drop Luggage To Ground")]
        public void DropToGroundAtCurrentPosition()
        {
            if (!TryGetRendererBounds(transform, out Bounds bounds))
            {
                Vector3 fallbackPosition = transform.position;
                fallbackPosition.y = ResolvePlacementY(fallbackPosition, null) + groundYOffset;
                transform.position = fallbackPosition;
                return;
            }

            Vector3 reference = bounds.center;
            transform.position += Vector3.up * (ResolvePlacementY(reference, bounds) - bounds.min.y + groundYOffset);
        }

        private void ApplyBoundsPlacement(Transform resolvedTarget, Vector3 offset)
        {
            Quaternion yawRotation = followYawOnly
                ? Quaternion.Euler(0f, resolvedTarget.eulerAngles.y, 0f)
                : resolvedTarget.rotation;
            float experienceScale = scaleWithExperience ? Mathf.Max(0.02f, CocoonExperienceScale.ResolveRigScale(resolvedTarget)) : 1f;
            Vector3 horizontalOffset = offset * experienceScale;
            horizontalOffset.y = 0f;
            Vector3 desiredBoundsCenter = resolvedTarget.position + yawRotation * horizontalOffset;

            transform.rotation = yawRotation * Quaternion.Euler(localEuler);
            transform.localScale = Vector3.one * Mathf.Max(0.001f, modelScale * (scaleModelWithExperience ? experienceScale : 1f));
            if (!alignRendererBoundsToTarget || !TryGetRendererBounds(transform, out Bounds bounds))
            {
                Vector3 fallbackPosition = desiredBoundsCenter;
                fallbackPosition.y = ResolvePlacementY(desiredBoundsCenter, null) + groundYOffset;
                transform.position = fallbackPosition;
                return;
            }

            Vector3 correction = Vector3.zero;
            correction.x = desiredBoundsCenter.x - bounds.center.x;
            correction.z = desiredBoundsCenter.z - bounds.center.z;
            Bounds desiredBounds = bounds;
            desiredBounds.center = new Vector3(desiredBoundsCenter.x, bounds.center.y, desiredBoundsCenter.z);
            correction.y = ResolvePlacementY(desiredBoundsCenter, desiredBounds) - bounds.min.y + groundYOffset;
            transform.position += correction;
        }

        private Transform ResolveTarget()
        {
            if (target != null)
            {
                return target;
            }

            if (preferLeftHandTarget)
            {
                Transform leftHand = FindLeftHandTransform();
                if (leftHand != null)
                {
                    target = leftHand;
                    return target;
                }
            }

            if (fallbackTarget != null)
            {
                return fallbackTarget;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                target = mainCamera.transform;
                return target;
            }

            return null;
        }

        private Transform ResolveRestTarget()
        {
            if (preferLeftHandTarget)
            {
                if (target != null)
                {
                    return target;
                }

                Transform leftHand = FindLeftHandTransform();
                if (leftHand != null)
                {
                    target = leftHand;
                    return target;
                }
            }

            if (fallbackTarget != null)
            {
                return fallbackTarget;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform;
            }

            return target != null ? target : ResolveTarget();
        }

        private static Transform FindLeftHandTransform()
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                string name = candidate.name;
                if (name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (name.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool IsLeftGripHeld()
        {
            leftDevices.Clear();
            AddDevice(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand));
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, leftDevices);
            for (int i = 0; i < leftDevices.Count; i++)
            {
                if (leftDevices[i].TryGetFeatureValue(XRCommonUsages.gripButton, out bool gripPressed) && gripPressed)
                {
                    return true;
                }

                if (leftDevices[i].TryGetFeatureValue(XRCommonUsages.grip, out float gripValue) && gripValue > 0.55f)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddDevice(XRInputDevice device)
        {
            if (device.isValid && !leftDevices.Contains(device))
            {
                leftDevices.Add(device);
            }
        }

        private float ResolvePlacementY(Vector3 referencePosition, Bounds? footprintBounds)
        {
            if (TryResolveElevatedSupportY(referencePosition, footprintBounds, out float supportY))
            {
                return supportY;
            }

            return ResolveGroundY(referencePosition);
        }

        private bool TryResolveElevatedSupportY(Vector3 referencePosition, Bounds? footprintBounds, out float supportY)
        {
            supportY = 0f;
            if (elevatedSupportRoot == null || string.IsNullOrEmpty(elevatedSupportTag))
            {
                return false;
            }

            Bounds footprint = footprintBounds ?? new Bounds(referencePosition, Vector3.one * Mathf.Max(0.02f, elevatedSupportPadding * 2f));
            float padding = Mathf.Max(0f, elevatedSupportPadding);
            bool hasSupport = false;
            Renderer[] renderers = elevatedSupportRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !IsTaggedSupport(renderer.transform))
                {
                    continue;
                }

                Bounds supportBounds = renderer.bounds;
                if (!OverlapsXZ(footprint, supportBounds, padding))
                {
                    continue;
                }

                supportY = hasSupport ? Mathf.Max(supportY, supportBounds.max.y) : supportBounds.max.y;
                hasSupport = true;
            }

            return hasSupport;
        }

        private bool IsTaggedSupport(Transform candidate)
        {
            Transform cursor = candidate;
            while (cursor != null)
            {
                if (cursor.gameObject.tag == elevatedSupportTag)
                {
                    return true;
                }

                if (cursor == elevatedSupportRoot)
                {
                    return false;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private static bool OverlapsXZ(Bounds a, Bounds b, float padding)
        {
            return a.min.x <= b.max.x + padding &&
                   a.max.x >= b.min.x - padding &&
                   a.min.z <= b.max.z + padding &&
                   a.max.z >= b.min.z - padding;
        }

        private float ResolveGroundY(Vector3 referencePosition)
        {
            Vector3 origin = referencePosition + Vector3.up * Mathf.Max(0.01f, groundRaycastHeight);
            float distance = Mathf.Max(0.05f, groundRaycastHeight + groundRaycastDistance);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            if (fallbackTarget != null)
            {
                Transform fallbackRoot = fallbackTarget.root != null ? fallbackTarget.root : fallbackTarget;
                return fallbackRoot.position.y;
            }

            return 0f;
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
                if (renderer == null || !renderer.enabled)
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
    }
}
