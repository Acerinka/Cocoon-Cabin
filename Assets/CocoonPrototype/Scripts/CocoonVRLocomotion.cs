using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CocoonPrototype
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CocoonVRLocomotion : MonoBehaviour
    {
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private float moveSpeed = 1.45f;
        [SerializeField] private float snapTurnDegrees = 35f;
        [SerializeField] private float snapTurnCooldown = 0.35f;
        [SerializeField] private float teleportMaxDistance = 8f;
        [SerializeField] private bool teleportUsesLeftTrigger = true;
        [SerializeField] private bool teleportUsesLeftGrip = false;
        [SerializeField] private LayerMask teleportLayers = ~0;
        [SerializeField] private GameObject teleportMarker;
        [SerializeField] private bool movementInputEnabled = true;

        private readonly List<InputDevice> leftDevices = new List<InputDevice>();
        private readonly List<InputDevice> rightDevices = new List<InputDevice>();
        private LineRenderer lineRenderer;
        private float nextSnapTurnTime;
        private bool wasTeleportHeld;
        private bool hasTeleportTarget;
        private Vector3 teleportTarget;
        private bool wasMoving;
        private int lastLeftDeviceCount = -1;
        private int lastRightDeviceCount = -1;
        private float nextMoveLogTime;
        private float lastControllerScale = -1f;

        public void Configure(Transform root, Transform headTransform, Transform leftHandTransform, GameObject marker)
        {
            rigRoot = root;
            head = headTransform;
            leftHand = leftHandTransform;
            teleportMarker = marker;
        }

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.018f;
            lineRenderer.endWidth = 0.006f;
            lineRenderer.enabled = false;
        }

        private void Update()
        {
            if (rigRoot == null || head == null)
            {
                return;
            }

            CalibrateRigControllerIfNeeded();
            ReadDevices();
            if (!movementInputEnabled)
            {
                CancelTeleportAim();
                wasMoving = false;
                return;
            }

            ApplyContinuousMove();
            ApplySnapTurn();
            ApplyTeleport();
        }

        public void SetMovementInputEnabled(bool enabled)
        {
            if (movementInputEnabled == enabled)
            {
                return;
            }

            movementInputEnabled = enabled;
            if (!movementInputEnabled)
            {
                CancelTeleportAim();
                wasMoving = false;
                CocoonDebugLog.Info("Locomotion", "Controller locomotion input disabled.", this);
            }
            else
            {
                CocoonDebugLog.Info("Locomotion", "Controller locomotion input enabled.", this);
            }
        }

        private void CalibrateRigControllerIfNeeded()
        {
            float rigScale = CocoonExperienceScale.ResolveRigScale(rigRoot);
            if (Mathf.Abs(rigScale - lastControllerScale) < 0.0001f)
            {
                return;
            }

            if (CocoonExperienceScale.CalibrateCharacterController(rigRoot))
            {
                CocoonDebugLog.Info("Locomotion", "CharacterController scaled for ROADMAP locomotion.", this);
            }

            lastControllerScale = rigScale;
        }

        private void ReadDevices()
        {
            leftDevices.Clear();
            rightDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, leftDevices);
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, rightDevices);
            if (leftDevices.Count != lastLeftDeviceCount || rightDevices.Count != lastRightDeviceCount)
            {
                CocoonDebugLog.Info("Locomotion", "Controller devices changed. left=" + leftDevices.Count + ", right=" + rightDevices.Count + ".", this);
                lastLeftDeviceCount = leftDevices.Count;
                lastRightDeviceCount = rightDevices.Count;
            }
        }

        private void ApplyContinuousMove()
        {
            Vector2 axis = ReadAxis(leftDevices);
            if (axis.sqrMagnitude < 0.04f)
            {
                if (wasMoving)
                {
                    CocoonDebugLog.Verbose("Locomotion", "Continuous move stopped.", this);
                    wasMoving = false;
                }

                return;
            }

            Vector3 forward = head.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = head.right;
            right.y = 0f;
            right.Normalize();

            Vector3 motion = forward * axis.y + right * axis.x;
            if (motion.sqrMagnitude > 1f)
            {
                motion.Normalize();
            }

            rigRoot.position += motion * (moveSpeed * CocoonExperienceScale.ResolveRigScale(rigRoot) * Time.deltaTime);
            if (!wasMoving || Time.time >= nextMoveLogTime)
            {
                CocoonDebugLog.Verbose("Locomotion", "Continuous move axis=" + axis.ToString("F2") + " rig=" + rigRoot.position.ToString("F2") + ".", this);
                wasMoving = true;
                nextMoveLogTime = Time.time + 1f;
            }
        }

        private void ApplySnapTurn()
        {
            Vector2 axis = ReadAxis(rightDevices);
            if (Time.time < nextSnapTurnTime || Mathf.Abs(axis.x) < 0.75f)
            {
                return;
            }

            float direction = Mathf.Sign(axis.x);
            rigRoot.RotateAround(head.position, Vector3.up, direction * snapTurnDegrees);
            nextSnapTurnTime = Time.time + snapTurnCooldown;
            CocoonDebugLog.Verbose("Locomotion", "Snap turn " + (direction > 0f ? "right" : "left") + " by " + snapTurnDegrees + " degrees.", this);
        }

        private void ApplyTeleport()
        {
            bool held = (teleportUsesLeftTrigger && ReadButton(leftDevices, CommonUsages.triggerButton)) ||
                        (teleportUsesLeftGrip && ReadButton(leftDevices, CommonUsages.gripButton));
            if (held)
            {
                Ray ray = BuildTeleportRay();
                float scaledTeleportMaxDistance = teleportMaxDistance * CocoonExperienceScale.ResolveRigScale(rigRoot);
                hasTeleportTarget = Physics.Raycast(ray, out RaycastHit hit, scaledTeleportMaxDistance, teleportLayers, QueryTriggerInteraction.Ignore);
                teleportTarget = hasTeleportTarget ? hit.point : ray.origin + ray.direction * scaledTeleportMaxDistance;
                if (!wasTeleportHeld)
                {
                    CocoonDebugLog.Info("Locomotion", "Teleport aim started. validTarget=" + hasTeleportTarget + ".", this);
                }

                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, ray.origin);
                lineRenderer.SetPosition(1, teleportTarget);
                if (teleportMarker != null)
                {
                    teleportMarker.SetActive(hasTeleportTarget);
                    teleportMarker.transform.position = teleportTarget + Vector3.up * 0.015f;
                }
            }
            else
            {
                lineRenderer.enabled = false;
                if (teleportMarker != null)
                {
                    teleportMarker.SetActive(false);
                }

                if (wasTeleportHeld && hasTeleportTarget)
                {
                    Vector3 headOffset = head.position - rigRoot.position;
                    headOffset.y = 0f;
                    rigRoot.position = teleportTarget - headOffset;
                    CocoonDebugLog.Info("Locomotion", "Teleported to " + rigRoot.position.ToString("F2") + ".", this);
                }
                else if (wasTeleportHeld)
                {
                    CocoonDebugLog.Warn("Locomotion", "Teleport released without valid target.", this);
                }

                hasTeleportTarget = false;
            }

            wasTeleportHeld = held;
        }

        private void CancelTeleportAim()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            if (teleportMarker != null)
            {
                teleportMarker.SetActive(false);
            }

            wasTeleportHeld = false;
            hasTeleportTarget = false;
        }

        private Ray BuildTeleportRay()
        {
            Transform source = leftHand != null ? leftHand : head;
            Vector3 direction = source.forward;
            direction.y = Mathf.Min(direction.y, -0.18f);
            return new Ray(source.position, direction.normalized);
        }

        private static Vector2 ReadAxis(List<InputDevice> devices)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 value))
                {
                    return value;
                }
            }

            return Vector2.zero;
        }

        private static bool ReadButton(List<InputDevice> devices, InputFeatureUsage<bool> usage)
        {
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].TryGetFeatureValue(usage, out bool value) && value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
