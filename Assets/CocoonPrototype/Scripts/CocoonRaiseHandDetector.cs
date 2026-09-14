using UnityEngine;
using UnityEngine.InputSystem;

namespace CocoonPrototype
{
    public sealed class CocoonRaiseHandDetector : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform targetTaxi;
        [SerializeField] private CocoonXRNodePose rightHandPose;
        [SerializeField] private CocoonXRNodePose leftHandPose;
        [SerializeField] private float requiredHoldSeconds = 2f;
        [SerializeField] private float minimumHeightBelowHead = 0.05f;
        [SerializeField] private float minimumDistance = 0f;
        [SerializeField] private float maximumDistance = 50f;
        [SerializeField] private float facingDotThreshold = 0.55f;
        [SerializeField] private float taxiApproachDotThreshold = 0.35f;
        [SerializeField] private float waveVelocityThreshold = 0.45f;
        [SerializeField] private float waveMemorySeconds = 0.45f;

        public bool IsRaised { get; private set; }
        public bool IsHailingPoseActive { get; private set; }
        public bool IsHailDetected { get; private set; }
        public float RaiseProgress01 => Mathf.Clamp01(holdTimer / Mathf.Max(0.01f, requiredHoldSeconds));
        public bool IsFacingTarget { get; private set; }
        public bool IsTargetInRange { get; private set; }
        public bool IsTaxiApproachingRider { get; private set; }
        public float TargetDistance { get; private set; }
        public float TaxiApproachDot { get; private set; }
        public string LastBlockReason { get; private set; } = "Ready";
        public bool HasRecentWave => waveMemoryTimer > 0f;
        public bool HasRiderPosition => head != null;
        public Vector3 RiderPosition => head != null ? head.position : Vector3.zero;
        public float DisplayDistance => GetDisplayDistance();

        private float holdTimer;
        private float waveMemoryTimer;
        private float lastLateralPosition;
        private bool hasLastLateralPosition;
        private bool lastPoseActive;
        private bool lastHailDetected;
        private bool lastFacingTarget;
        private bool lastTargetInRange;
        private bool lastTaxiApproachingRider;
        private bool lastRaised;
        private float nextDiagnosticTime;
        private float nextWaveLogTime;

        private const float StrictRequiredHoldSeconds = 2f;
        private const float StrictMinimumHeightBelowHead = 0.05f;
        private const float StrictMaximumDistance = 50f;
        private const float StrictFacingDotThreshold = 0.55f;
        private const float StrictTaxiApproachDotThreshold = 0.35f;

        public void Configure(Transform headTransform, Transform rightHandTransform, CocoonXRNodePose handPose, Transform taxiTarget = null, Transform leftHandTransform = null, CocoonXRNodePose leftHandNodePose = null)
        {
            head = headTransform;
            rightHand = rightHandTransform;
            rightHandPose = handPose;
            leftHand = leftHandTransform;
            leftHandPose = leftHandNodePose;
            targetTaxi = taxiTarget;
            ApplyStrictHailSettings();
        }

        private void Awake()
        {
            ApplyStrictHailSettings();
        }

        private void OnValidate()
        {
            ApplyStrictHailSettings();
        }

        private void Update()
        {
            bool targetReady = EvaluateTargetWindow();
            bool rightTracked = rightHand != null && (rightHandPose == null || rightHandPose.IsTracked);
            bool leftTracked = leftHand != null && (leftHandPose == null || leftHandPose.IsTracked);
            bool rightHigh = IsHandHigh(rightHand);
            bool leftHigh = IsHandHigh(leftHand);
            bool rightPoseActive = rightTracked && rightHigh && targetReady;
            bool leftPoseActive = leftTracked && leftHigh && targetReady;
            bool tracked = rightTracked || leftTracked;
            bool highEnough = rightHigh || leftHigh;
            bool editorFallback = Keyboard.current != null && Keyboard.current.spaceKey.isPressed && targetReady;

            IsHailingPoseActive = rightPoseActive || leftPoseActive || editorFallback;
            Transform activeHand = rightPoseActive ? rightHand : (leftPoseActive ? leftHand : null);
            UpdateWaveMemory(IsHailingPoseActive, activeHand);

            holdTimer = IsHailingPoseActive ? holdTimer + Time.deltaTime : 0f;
            IsRaised = holdTimer >= requiredHoldSeconds;
            IsHailDetected = IsHailingPoseActive && IsRaised;

            if (IsHailingPoseActive != lastPoseActive)
            {
                CocoonDebugLog.Verbose("HailDetector", "Pose " + (IsHailingPoseActive ? "active" : "inactive") + ". tracked=" + tracked + " (R=" + rightTracked + ", L=" + leftTracked + "), high=" + highEnough + " (R=" + rightHigh + ", L=" + leftHigh + "), facing=" + IsFacingTarget + ", range=" + IsTargetInRange + ", approach=" + IsTaxiApproachingRider + ", distance=" + GetDisplayDistance().ToString("0.0") + "m, block=" + LastBlockReason + ", hold=" + holdTimer.ToString("0.00") + "s.", this);
                lastPoseActive = IsHailingPoseActive;
            }

            if (IsRaised != lastRaised)
            {
                CocoonDebugLog.Verbose("HailDetector", "Raised hold " + (IsRaised ? "complete" : "lost") + " at " + holdTimer.ToString("0.00") + "s.", this);
                lastRaised = IsRaised;
            }

            if (IsHailDetected != lastHailDetected)
            {
                CocoonDebugLog.Verbose("HailDetector", "HailDetected=" + IsHailDetected + " raised=" + IsRaised + " recentWave=" + HasRecentWave + ".", this);
                lastHailDetected = IsHailDetected;
            }

            if (IsFacingTarget != lastFacingTarget || IsTargetInRange != lastTargetInRange || IsTaxiApproachingRider != lastTaxiApproachingRider)
            {
                CocoonDebugLog.Verbose("HailDetector", "Target window changed. facing=" + IsFacingTarget + ", range=" + IsTargetInRange + ", approach=" + IsTaxiApproachingRider + ", distance=" + GetDisplayDistance().ToString("0.0") + "m, taxiDot=" + TaxiApproachDot.ToString("0.00") + ", block=" + LastBlockReason + ".", this);
                lastFacingTarget = IsFacingTarget;
                lastTargetInRange = IsTargetInRange;
                lastTaxiApproachingRider = IsTaxiApproachingRider;
            }

            if (!IsHailingPoseActive && Time.time >= nextDiagnosticTime)
            {
                CocoonDebugLog.Verbose("HailDetector", "Waiting for hail. tracked=" + tracked + " (R=" + rightTracked + ", L=" + leftTracked + "), high=" + highEnough + " (R=" + rightHigh + ", L=" + leftHigh + "), facing=" + IsFacingTarget + ", range=" + IsTargetInRange + ", approach=" + IsTaxiApproachingRider + ", distance=" + GetDisplayDistance().ToString("0.0") + "m, block=" + LastBlockReason + ", progress=" + RaiseProgress01.ToString("0.00") + ".", this);
                nextDiagnosticTime = Time.time + 2f;
            }
        }

        private bool IsHandHigh(Transform hand)
        {
            return head != null && hand != null && hand.position.y >= head.position.y - minimumHeightBelowHead * GetExperienceScale();
        }

        private void ApplyStrictHailSettings()
        {
            requiredHoldSeconds = StrictRequiredHoldSeconds;
            minimumHeightBelowHead = StrictMinimumHeightBelowHead;
            maximumDistance = StrictMaximumDistance;
            facingDotThreshold = StrictFacingDotThreshold;
            taxiApproachDotThreshold = StrictTaxiApproachDotThreshold;
        }

        private void UpdateWaveMemory(bool poseActive, Transform activeHand)
        {
            if (waveMemoryTimer > 0f)
            {
                waveMemoryTimer -= Time.deltaTime;
            }

            if (!poseActive || head == null || activeHand == null || Time.deltaTime <= 0f)
            {
                hasLastLateralPosition = false;
                return;
            }

            Vector3 toHand = activeHand.position - head.position;
            float lateral = Vector3.Dot(toHand, head.right);
            if (hasLastLateralPosition)
            {
                float lateralVelocity = Mathf.Abs((lateral - lastLateralPosition) / Time.deltaTime);
                if (lateralVelocity >= waveVelocityThreshold * GetExperienceScale())
                {
                    waveMemoryTimer = waveMemorySeconds;
                    if (Time.time >= nextWaveLogTime)
                    {
                        CocoonDebugLog.Verbose("HailDetector", "Wave motion detected. lateralVelocity=" + lateralVelocity.ToString("0.00") + ".", this);
                        nextWaveLogTime = Time.time + 0.5f;
                    }
                }
            }

            lastLateralPosition = lateral;
            hasLastLateralPosition = true;
        }

        private bool EvaluateTargetWindow()
        {
            IsFacingTarget = true;
            IsTargetInRange = true;
            IsTaxiApproachingRider = true;
            TargetDistance = 0f;
            TaxiApproachDot = 1f;
            LastBlockReason = "Ready";

            if (head == null || targetTaxi == null)
            {
                return true;
            }

            Vector3 toTaxi = targetTaxi.position - head.position;
            toTaxi.y = 0f;
            TargetDistance = toTaxi.magnitude;
            float experienceScale = GetExperienceScale();
            IsTargetInRange = TargetDistance >= minimumDistance * experienceScale && TargetDistance <= maximumDistance * experienceScale;

            Vector3 headForward = head.forward;
            headForward.y = 0f;
            if (headForward.sqrMagnitude < 0.0001f || toTaxi.sqrMagnitude < 0.0001f)
            {
                IsFacingTarget = true;
            }
            else
            {
                IsFacingTarget = Vector3.Dot(headForward.normalized, toTaxi.normalized) >= facingDotThreshold;
            }

            Vector3 taxiToRider = head.position - targetTaxi.position;
            taxiToRider.y = 0f;
            Vector3 taxiForward = targetTaxi.forward;
            taxiForward.y = 0f;
            if (taxiForward.sqrMagnitude < 0.0001f || taxiToRider.sqrMagnitude < 0.0001f)
            {
                IsTaxiApproachingRider = true;
                TaxiApproachDot = 1f;
            }
            else
            {
                TaxiApproachDot = Vector3.Dot(taxiForward.normalized, taxiToRider.normalized);
                IsTaxiApproachingRider = TaxiApproachDot >= taxiApproachDotThreshold;
            }

            if (!IsTargetInRange)
            {
                LastBlockReason = TargetDistance > maximumDistance * experienceScale ? "Taxi farther than " + maximumDistance.ToString("0") + "m" : "Taxi too close";
            }
            else if (!IsFacingTarget)
            {
                LastBlockReason = "Rider not facing taxi";
            }
            else if (!IsTaxiApproachingRider)
            {
                LastBlockReason = "Taxi already passed / rear facing rider";
            }

            return IsTargetInRange && IsFacingTarget && IsTaxiApproachingRider;
        }

        private float GetExperienceScale()
        {
            return head != null ? CocoonExperienceScale.ResolveRigScale(head) : CocoonExperienceScale.RoadmapScale;
        }

        private float GetDisplayDistance()
        {
            return TargetDistance / Mathf.Max(0.001f, GetExperienceScale());
        }
    }
}
