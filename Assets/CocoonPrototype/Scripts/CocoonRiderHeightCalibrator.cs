using UnityEngine;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonRiderHeightCalibrator : MonoBehaviour
    {
        [Header("Rider Height")]
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform head;
        [SerializeField] private float targetHeadHeightMeters = 1.55f;
        [SerializeField] private bool scaleWithExperience = true;
        [SerializeField] private bool lockHeightWhilePlaying = true;
        [SerializeField] private bool applyOnStart = true;

        [Header("Ground Reference")]
        [SerializeField] private bool captureInitialRigYAsGround = true;
        [SerializeField] private float groundY = 0f;

        [Header("Body Collider")]
        [SerializeField] private bool updateCharacterController = true;
        [SerializeField] private float controllerRadiusMeters = 0.08f;

        private bool hasGroundReference;
        private float capturedGroundY;
        private bool hasLoggedCalibration;

        public float TargetHeadHeightMeters => Mathf.Max(0.4f, targetHeadHeightMeters);

        public void SetControllerRadiusMeters(float radiusMeters)
        {
            controllerRadiusMeters = Mathf.Clamp(radiusMeters, 0.03f, 0.65f);
            if (updateCharacterController)
            {
                UpdateCharacterController();
            }
        }

        public void SetHeightLockEnabled(bool enabled)
        {
            if (lockHeightWhilePlaying == enabled)
            {
                return;
            }

            lockHeightWhilePlaying = enabled;
            CocoonDebugLog.Info("XR", "Rider fixed head height " + (enabled ? "enabled" : "disabled") + ".", this);
        }

        private void Awake()
        {
            ResolveReferences();
            CaptureGroundReference();
            if (applyOnStart)
            {
                ApplyHeight();
            }
        }

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyHeight();
            }
        }

        private void LateUpdate()
        {
            if (lockHeightWhilePlaying)
            {
                ApplyHeight();
            }
        }

        [ContextMenu("Apply Rider Height Now")]
        public void ApplyHeight()
        {
            ResolveReferences();
            CaptureGroundReference();

            if (rigRoot == null || head == null)
            {
                return;
            }

            float experienceScale = ResolveExperienceScale();
            float desiredWorldHeadY = ResolveGroundY() + TargetHeadHeightMeters * experienceScale;
            float deltaY = desiredWorldHeadY - head.position.y;
            if (Mathf.Abs(deltaY) > 0.0005f)
            {
                Vector3 position = rigRoot.position;
                position.y += deltaY;
                rigRoot.position = position;
            }

            if (updateCharacterController)
            {
                UpdateCharacterController();
            }

            if (!hasLoggedCalibration)
            {
                CocoonDebugLog.Info(
                    "XR",
                    "Rider head height locked to " + TargetHeadHeightMeters.ToString("0.00") +
                    "m, experienceScale=" + experienceScale.ToString("0.###") +
                    ", worldHeadY=" + desiredWorldHeadY.ToString("0.###") + ".",
                    this);
                hasLoggedCalibration = true;
            }
        }

        private void ResolveReferences()
        {
            if (rigRoot == null)
            {
                rigRoot = transform;
            }

            if (head == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null && mainCamera.gameObject.scene == gameObject.scene)
                {
                    head = mainCamera.transform;
                }
            }
        }

        private void CaptureGroundReference()
        {
            if (hasGroundReference)
            {
                return;
            }

            capturedGroundY = captureInitialRigYAsGround && rigRoot != null ? rigRoot.position.y : groundY;
            hasGroundReference = true;
        }

        private float ResolveGroundY()
        {
            return captureInitialRigYAsGround ? capturedGroundY : groundY;
        }

        private float ResolveExperienceScale()
        {
            if (!scaleWithExperience)
            {
                return 1f;
            }

            return Mathf.Max(0.02f, CocoonExperienceScale.ResolveRigScale(rigRoot));
        }

        private void UpdateCharacterController()
        {
            CharacterController controller = rigRoot != null ? rigRoot.GetComponent<CharacterController>() : null;
            if (controller == null)
            {
                return;
            }

            float height = TargetHeadHeightMeters;
            float radius = Mathf.Clamp(controllerRadiusMeters, 0.03f, height * 0.35f);
            controller.height = height;
            controller.center = new Vector3(controller.center.x, height * 0.5f, controller.center.z);
            controller.radius = radius;
            CocoonExperienceScale.CalibrateCharacterController(rigRoot);
        }

        private void OnValidate()
        {
            targetHeadHeightMeters = Mathf.Clamp(targetHeadHeightMeters, 0.4f, 2.4f);
            controllerRadiusMeters = Mathf.Clamp(controllerRadiusMeters, 0.03f, 0.65f);
        }
    }
}
