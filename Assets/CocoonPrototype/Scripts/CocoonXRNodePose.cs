using UnityEngine;
using UnityEngine.InputSystem;
using InputTrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;
using UnityEngine.XR;

namespace CocoonPrototype
{
    [DefaultExecutionOrder(-10000)]
    public sealed class CocoonXRNodePose : MonoBehaviour
    {
        [SerializeField] private XRNode node = XRNode.RightHand;
        [SerializeField] private Vector3 editorFallbackLocalPosition = new Vector3(0.35f, 1.25f, 0.55f);
        [SerializeField] private Vector3 editorFallbackLocalEuler = new Vector3(18f, 0f, 0f);

        public bool IsTracked { get; private set; }
        public XRNode Node => node;
        private bool lastTracked;
        private bool hasLoggedTracking;

        private void Awake()
        {
            if (node == XRNode.CenterEye)
            {
                EnsureCenterEyeTrackedPoseDriver();
            }
        }

        private void Reset()
        {
            if (gameObject.name.Contains("Camera"))
            {
                node = XRNode.CenterEye;
                editorFallbackLocalPosition = new Vector3(0f, 1.65f, 0f);
                editorFallbackLocalEuler = Vector3.zero;
            }
            else if (gameObject.name.Contains("Left"))
            {
                node = XRNode.LeftHand;
                editorFallbackLocalPosition = new Vector3(-0.35f, 1.2f, 0.55f);
                editorFallbackLocalEuler = new Vector3(18f, -12f, 0f);
            }
        }

        public void Configure(XRNode trackedNode, Vector3 fallbackPosition, Vector3 fallbackEuler)
        {
            node = trackedNode;
            editorFallbackLocalPosition = fallbackPosition;
            editorFallbackLocalEuler = fallbackEuler;
        }

        private void Update()
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            IsTracked = device.isValid;

            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position))
            {
                transform.localPosition = position;
                IsTracked = true;
            }
            else if (!Application.isPlaying || node != XRNode.CenterEye)
            {
                transform.localPosition = editorFallbackLocalPosition;
            }

            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.localRotation = rotation;
                IsTracked = true;
            }
            else if (!Application.isPlaying || node != XRNode.CenterEye)
            {
                transform.localRotation = Quaternion.Euler(editorFallbackLocalEuler);
            }

            if (!hasLoggedTracking || IsTracked != lastTracked)
            {
                CocoonDebugLog.Verbose("XRTracking", node + " tracked=" + IsTracked + ".", this);
                lastTracked = IsTracked;
                hasLoggedTracking = true;
            }
        }

        private void EnsureCenterEyeTrackedPoseDriver()
        {
            var driver = GetComponent<InputTrackedPoseDriver>();
            if (driver == null)
            {
                driver = gameObject.AddComponent<InputTrackedPoseDriver>();
            }

            driver.trackingType = InputTrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = InputTrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.ignoreTrackingState = false;
            driver.positionInput = new InputActionProperty(new InputAction(
                "Center Eye Position",
                InputActionType.Value,
                "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction(
                "Center Eye Rotation",
                InputActionType.Value,
                "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion"));
            driver.trackingStateInput = new InputActionProperty(new InputAction(
                "Center Eye Tracking State",
                InputActionType.Value,
                "<XRHMD>/trackingState",
                expectedControlType: "Integer"));
        }
    }
}
