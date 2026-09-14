using UnityEngine;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonSeatV2Controller : MonoBehaviour
    {
        private enum SeatV2Phase
        {
            Idle,
            MoveOut,
            MoveBack,
            MoveIdle,
            LegacyRotate
        }

        [Header("Seat V2 References")]
        [SerializeField] private Transform seatAssembly;
        [SerializeField] private Transform seatBody;
        [SerializeField] private Transform seatOutTarget;
        [SerializeField] private Transform seatBackTarget;
        [SerializeField] private Transform seatedRiderHeadAnchor;
        [SerializeField] private Transform miniScreenAnchor;

        [Header("Legacy Fallback Markers")]
        [SerializeField] private Transform idlePose;
        [SerializeField] private Transform storageTarget;
        [SerializeField] private Transform seatedPose;
        [SerializeField] private Transform seatedRotateTarget;

        [Header("Authorable Part Pivots")]
        [SerializeField] private Transform backPivot;
        [SerializeField] private Transform leftArmPivot;
        [SerializeField] private Transform rightArmPivot;
        [SerializeField] private Transform footrestPivot;

        [Header("Animation")]
        [SerializeField] private float storageToOutDuration = 4f;
        [SerializeField] private float sitToBackDuration = 3f;
        [SerializeField] private float restoreToIdleDuration = 3f;
        [SerializeField] private float storageDuration = 4f;
        [SerializeField] private float sitRotateDuration = 7f;
        [SerializeField] private bool enableSeatSitRotationToMarker;
        [SerializeField] private bool enableSeatedStickSeatRotation;
        [Tooltip("Use the seated head anchor local -X axis as the seated camera forward. Enable this when the seat visual has been mirrored and local +X points into the seat back.")]
        [SerializeField] private bool useNegativeXForSeatedHeadForward = true;
        [SerializeField] private bool hideMarkersOnStart = true;

        private static readonly string[] SeatOutTargetNames = { "Seat V2 Bod_out", "Seat V2 Body_out", "SeatV2 Storage Target" };
        private static readonly string[] SeatBackTargetNames = { "Seat V2 Bod_back", "Seat V2 Body_back", "SeatV2 Seated Rotate Target", "SeatV2 Seated Pose" };
        private static readonly string[] SeatedHeadAnchorNames = { "Seated Rider Head Anchor", "SeatV2 Seated Rider Head Anchor" };
        private static readonly string[] MiniScreenAnchorNames = { "MIINIscreen1", "SeatV2 Mini Screen Anchor" };

        private SeatV2Phase phase;
        private float phaseTimer;
        private Vector3 segmentStartLocalPosition;
        private Quaternion segmentStartLocalRotation = Quaternion.identity;
        private Vector3 segmentStartLocalScale = Vector3.one;
        private Transform segmentTarget;
        private bool hasLoggedBinding;
        private bool hasLoggedMissing;
        private bool hasAuthoredIdlePose;
        private Vector3 authoredIdleLocalPosition;
        private Quaternion authoredIdleLocalRotation = Quaternion.identity;
        private Vector3 authoredIdleLocalScale = Vector3.one;

        public Transform SeatTransform
        {
            get { return seatBody != null ? seatBody : seatAssembly; }
        }

        public Transform StorageTarget
        {
            get { return seatOutTarget != null ? seatOutTarget : storageTarget; }
        }

        public Transform OutTarget
        {
            get { return StorageTarget; }
        }

        public Transform BackTarget
        {
            get
            {
                if (seatBackTarget != null)
                {
                    return seatBackTarget;
                }

                return seatedRotateTarget != null ? seatedRotateTarget : seatedPose;
            }
        }

        public Transform SeatedPose
        {
            get { return seatedPose; }
        }

        public Transform SeatedRotateTarget
        {
            get { return seatedRotateTarget; }
        }

        public Transform EffectiveSitTarget
        {
            get
            {
                if (enableSeatSitRotationToMarker && seatedRotateTarget != null)
                {
                    return seatedRotateTarget;
                }

                return BackTarget;
            }
        }

        public Transform SeatedRiderHeadAnchor
        {
            get { return seatedRiderHeadAnchor; }
        }

        public Transform MiniScreenAnchor
        {
            get { return miniScreenAnchor; }
        }

        public bool EnableSeatSitRotationToMarker
        {
            get { return enableSeatSitRotationToMarker; }
        }

        public bool EnableSeatedStickSeatRotation
        {
            get { return enableSeatedStickSeatRotation; }
        }

        public Vector3 SeatedHeadCameraForwardLocalAxis
        {
            get
            {
                Vector3 axis = useNegativeXForSeatedHeadForward ? Vector3.left : Vector3.right;
                Transform seat = SeatTransform;
                if (seat != null && seat.localScale.x < 0f)
                {
                    axis.x = -axis.x;
                }

                return axis;
            }
        }

        public string SeatedHeadCameraForwardAxisLabel
        {
            get
            {
                Vector3 axis = SeatedHeadCameraForwardLocalAxis;
                return axis.x < 0f ? "local -X" : "local +X";
            }
        }

        public bool HasStorageReferences
        {
            get { return SeatTransform != null && StorageTarget != null; }
        }

        public bool HasSitReferences
        {
            get { return SeatTransform != null && EffectiveSitTarget != null && seatedRiderHeadAnchor != null; }
        }

        public bool IsStorageComplete
        {
            get { return phase != SeatV2Phase.MoveOut; }
        }

        public bool IsUnfoldComplete
        {
            get { return IsStorageComplete; }
        }

        public bool IsSitDownComplete
        {
            get { return phase != SeatV2Phase.MoveBack && phase != SeatV2Phase.LegacyRotate; }
        }

        public bool IsRestoreComplete
        {
            get { return phase != SeatV2Phase.MoveIdle; }
        }

        public bool IsSitReady
        {
            get { return HasSitReferences; }
        }

        private void Awake()
        {
            ResolveReferences();
            if (hideMarkersOnStart)
            {
                HideMarkers();
            }
        }

        private void Update()
        {
            UpdateAnimation();
        }

        public void ResolveReferences()
        {
            if (seatAssembly == null)
            {
                seatAssembly = transform;
            }

            seatBody = ResolveExactNamedDescendant(seatAssembly, seatBody, "Seat V2 Body");
            seatOutTarget = ResolveAliasedNamedDescendant(seatAssembly, seatOutTarget, SeatOutTargetNames);
            seatBackTarget = ResolveAliasedNamedDescendant(seatAssembly, seatBackTarget, SeatBackTargetNames);

            idlePose = ResolveExactNamedDescendant(seatAssembly, idlePose, "SeatV2 Idle Pose");
            storageTarget = ResolveExactNamedDescendant(seatAssembly, storageTarget, "SeatV2 Storage Target");
            seatedPose = ResolveExactNamedDescendant(seatAssembly, seatedPose, "SeatV2 Seated Pose");
            seatedRotateTarget = ResolveExactNamedDescendant(seatAssembly, seatedRotateTarget, "SeatV2 Seated Rotate Target");
            seatedRiderHeadAnchor =
                ResolveAliasedNamedDescendant(seatBody, null, SeatedHeadAnchorNames) ??
                ResolveAliasedNamedDescendant(seatAssembly, seatedRiderHeadAnchor, SeatedHeadAnchorNames);
            miniScreenAnchor =
                ResolveAliasedNamedDescendant(seatBody, null, MiniScreenAnchorNames) ??
                ResolveAliasedNamedDescendant(seatAssembly, miniScreenAnchor, MiniScreenAnchorNames);
            backPivot = ResolveExactNamedDescendant(seatAssembly, backPivot, "SeatV2 Back Pivot");
            leftArmPivot = ResolveExactNamedDescendant(seatAssembly, leftArmPivot, "SeatV2 Arm L Pivot");
            rightArmPivot = ResolveExactNamedDescendant(seatAssembly, rightArmPivot, "SeatV2 Arm R Pivot");
            footrestPivot = ResolveExactNamedDescendant(seatAssembly, footrestPivot, "SeatV2 Footrest Pivot");
            CaptureAuthoredIdlePoseIfNeeded();

            if (!hasLoggedBinding && SeatTransform != null)
            {
                hasLoggedBinding = true;
                CocoonDebugLog.Info(
                    "SeatV2",
                    "Seat V2 bound. body=" + GetTransformPath(SeatTransform) +
                    ", outTarget=" + GetTransformPath(StorageTarget) +
                    ", backTarget=" + GetTransformPath(BackTarget) +
                    ", headAnchor=" + GetTransformPath(seatedRiderHeadAnchor) +
                    ", miniAnchor=" + GetTransformPath(miniScreenAnchor) +
                    ", pivots=" + (backPivot != null) + "/" + (leftArmPivot != null) + "/" + (rightArmPivot != null) + "/" + (footrestPivot != null) +
                    ", sitRotationEnabled=" + enableSeatSitRotationToMarker +
                    ", seatedStickRotationEnabled=" + enableSeatedStickSeatRotation +
                    ", headForward=" + SeatedHeadCameraForwardAxisLabel + ".",
                    this);
            }
        }

        public void ResetRuntimeState()
        {
            ResolveReferences();
            phase = SeatV2Phase.Idle;
            phaseTimer = 0f;
            segmentTarget = null;
            RestoreAuthoredIdlePose();
            HideMarkers();
        }

        public void StartStorage()
        {
            ResolveReferences();
            if (!HasStorageReferences)
            {
                LogMissingReferences("storage");
                phase = SeatV2Phase.Idle;
                return;
            }

            StartSegment(SeatV2Phase.MoveOut, StorageTarget);
            CocoonDebugLog.Info("SeatV2", "Seat V2 moving out toward " + StorageTarget.name + " using parent-local interpolation.", this);
        }

        public void StartUnfold()
        {
            StartStorage();
        }

        public void StartSitDown()
        {
            ResolveReferences();
            if (!HasSitReferences)
            {
                LogMissingReferences("sit");
                phase = SeatV2Phase.Idle;
                return;
            }

            SeatV2Phase nextPhase = enableSeatSitRotationToMarker && seatedRotateTarget != null
                ? SeatV2Phase.LegacyRotate
                : SeatV2Phase.MoveBack;
            Transform target = EffectiveSitTarget;
            StartSegment(nextPhase, target);
            CocoonDebugLog.Info("SeatV2", "Seat V2 sit movement started toward " + target.name + " using parent-local interpolation.", this);
        }

        public void StartRestoreToIdle()
        {
            ResolveReferences();
            Transform seat = SeatTransform;
            if (seat == null)
            {
                LogMissingReferences("restore");
                phase = SeatV2Phase.Idle;
                return;
            }

            CaptureAuthoredIdlePoseIfNeeded();
            phase = SeatV2Phase.MoveIdle;
            phaseTimer = 0f;
            segmentTarget = null;
            segmentStartLocalPosition = seat.localPosition;
            segmentStartLocalRotation = seat.localRotation;
            segmentStartLocalScale = seat.localScale;
            HideMarkers();
            CocoonDebugLog.Info("SeatV2", "Seat V2 restoring to authored idle pose using parent-local interpolation.", this);
        }

        public void HideMarkers()
        {
            SetRenderersEnabled(idlePose, false);
            SetRenderersEnabled(storageTarget, false);
            SetRenderersEnabled(seatedPose, false);
            SetRenderersEnabled(seatedRotateTarget, false);
            SetRenderersEnabled(seatOutTarget, false);
            SetRenderersEnabled(seatBackTarget, false);
            if (miniScreenAnchor != null && !string.Equals(miniScreenAnchor.name, "MIINIscreen1", System.StringComparison.Ordinal))
            {
                SetRenderersEnabled(miniScreenAnchor, false);
            }
        }

        private void StartSegment(SeatV2Phase nextPhase, Transform target)
        {
            Transform seat = SeatTransform;
            phase = nextPhase;
            phaseTimer = 0f;
            segmentTarget = target;
            segmentStartLocalPosition = seat.localPosition;
            segmentStartLocalRotation = seat.localRotation;
            segmentStartLocalScale = seat.localScale;
            HideMarkers();
        }

        private void UpdateAnimation()
        {
            if (phase == SeatV2Phase.Idle)
            {
                return;
            }

            Transform seat = SeatTransform;
            if (seat == null || (segmentTarget == null && phase != SeatV2Phase.MoveIdle))
            {
                phase = SeatV2Phase.Idle;
                segmentTarget = null;
                return;
            }

            float duration = GetPhaseDuration(phase);
            phaseTimer = Mathf.Min(duration, phaseTimer + Time.deltaTime);
            float t = Mathf.SmoothStep(0f, 1f, phaseTimer / duration);

            Vector3 targetLocalPosition;
            Quaternion targetLocalRotation;
            Vector3 targetLocalScale;
            if (phase == SeatV2Phase.MoveIdle)
            {
                targetLocalPosition = authoredIdleLocalPosition;
                targetLocalRotation = authoredIdleLocalRotation;
                targetLocalScale = authoredIdleLocalScale;
            }
            else
            {
                GetTargetPoseInSeatParent(
                    seat.parent,
                    segmentTarget,
                    out targetLocalPosition,
                    out targetLocalRotation,
                    out targetLocalScale);
            }

            ApplyLocalPose(
                seat,
                Vector3.Lerp(segmentStartLocalPosition, targetLocalPosition, t),
                Quaternion.Slerp(segmentStartLocalRotation, targetLocalRotation, t),
                Vector3.Lerp(segmentStartLocalScale, targetLocalScale, t));

            if (phaseTimer < duration)
            {
                return;
            }

            if (phase == SeatV2Phase.MoveIdle)
            {
                targetLocalPosition = authoredIdleLocalPosition;
                targetLocalRotation = authoredIdleLocalRotation;
                targetLocalScale = authoredIdleLocalScale;
            }
            else
            {
                GetTargetPoseInSeatParent(
                    seat.parent,
                    segmentTarget,
                    out targetLocalPosition,
                    out targetLocalRotation,
                    out targetLocalScale);
            }
            ApplyLocalPose(seat, targetLocalPosition, targetLocalRotation, targetLocalScale);
            CocoonDebugLog.Info("SeatV2", "Seat V2 " + phase + " complete.", this);
            phase = SeatV2Phase.Idle;
            segmentTarget = null;
        }

        private float GetPhaseDuration(SeatV2Phase currentPhase)
        {
            switch (currentPhase)
            {
                case SeatV2Phase.MoveOut:
                    return Mathf.Max(0.01f, storageToOutDuration > 0.0001f ? storageToOutDuration : storageDuration);
                case SeatV2Phase.MoveBack:
                    return Mathf.Max(0.01f, sitToBackDuration);
                case SeatV2Phase.MoveIdle:
                    return Mathf.Max(0.01f, restoreToIdleDuration);
                case SeatV2Phase.LegacyRotate:
                    return Mathf.Max(0.01f, sitRotateDuration);
                default:
                    return 0.01f;
            }
        }

        private void LogMissingReferences(string action)
        {
            if (hasLoggedMissing)
            {
                return;
            }

            hasLoggedMissing = true;
            CocoonDebugLog.Warn(
                "SeatV2",
                "Seat V2 cannot start " + action + ". body=" + (SeatTransform != null) +
                ", outTarget=" + (StorageTarget != null) +
                ", backTarget=" + (BackTarget != null) +
                ", headAnchor=" + (seatedRiderHeadAnchor != null) + ".",
                this);
        }

        private void CaptureAuthoredIdlePoseIfNeeded()
        {
            if (hasAuthoredIdlePose)
            {
                return;
            }

            Transform seat = SeatTransform;
            if (seat == null)
            {
                return;
            }

            authoredIdleLocalPosition = seat.localPosition;
            authoredIdleLocalRotation = seat.localRotation;
            authoredIdleLocalScale = seat.localScale;
            hasAuthoredIdlePose = true;
        }

        private void RestoreAuthoredIdlePose()
        {
            Transform seat = SeatTransform;
            if (seat == null || !hasAuthoredIdlePose)
            {
                return;
            }

            ApplyLocalPose(seat, authoredIdleLocalPosition, authoredIdleLocalRotation, authoredIdleLocalScale);
        }

        private static Transform ResolveAliasedNamedDescendant(Transform root, Transform current, string[] objectNames)
        {
            if (root == null || objectNames == null || objectNames.Length == 0)
            {
                return current;
            }

            for (int i = 0; i < objectNames.Length; i++)
            {
                Transform match = FindExactNamedDescendant(root, objectNames[i]);
                if (match != null)
                {
                    return match;
                }
            }

            if (current != null)
            {
                for (int i = 0; i < objectNames.Length; i++)
                {
                    if (string.Equals(current.name, objectNames[i], System.StringComparison.Ordinal))
                    {
                        return current;
                    }
                }
            }

            return current;
        }

        private static Transform ResolveExactNamedDescendant(Transform root, Transform current, string objectName)
        {
            if (current != null && string.Equals(current.name, objectName, System.StringComparison.Ordinal))
            {
                return current;
            }

            return FindExactNamedDescendant(root, objectName);
        }

        private static Transform FindExactNamedDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (string.Equals(children[i].name, objectName, System.StringComparison.Ordinal))
                {
                    return children[i];
                }
            }

            return null;
        }

        private static void GetTargetPoseInSeatParent(
            Transform seatParent,
            Transform target,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            if (target == null)
            {
                localPosition = Vector3.zero;
                localRotation = Quaternion.identity;
                localScale = Vector3.one;
                return;
            }

            if (seatParent == null)
            {
                localPosition = target.position;
                localRotation = target.rotation;
                localScale = target.lossyScale;
                return;
            }

            if (target.parent == seatParent)
            {
                localPosition = target.localPosition;
                localRotation = target.localRotation;
                localScale = target.localScale;
                return;
            }

            localPosition = seatParent.InverseTransformPoint(target.position);
            localRotation = Quaternion.Inverse(seatParent.rotation) * target.rotation;
            Vector3 parentScale = seatParent.lossyScale;
            Vector3 targetScale = target.lossyScale;
            localScale = new Vector3(
                SafeDivide(targetScale.x, parentScale.x),
                SafeDivide(targetScale.y, parentScale.y),
                SafeDivide(targetScale.z, parentScale.z));
        }

        private static void ApplyLocalPose(Transform target, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            target.localPosition = localPosition;
            target.localRotation = localRotation;
            target.localScale = localScale;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.00001f ? value / divisor : value;
        }

        private static void SetRenderersEnabled(Transform root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = enabled;
            }
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
            {
                return "missing";
            }

            string path = target.name;
            Transform parent = target.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
