using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace CocoonPrototype
{
    public enum CocoonTaxiState
    {
        Availability,
        HailDetected,
        AwaitPoseConfirm,
        SafePullOver,
        ReachPickupPoint,
        ConfirmRide,
        ChooseDestination,
        ChooseRideMode,
        ContactlessPayment,
        PaymentConfirmed,
        DoorOpening,
        DoorOpen,
        DoorClosing,
        Departing
    }

    public enum CocoonTaxiExteriorScreenState
    {
        Available,
        DetectedHold,
        StoppingFollowMe,
        Goodbye,
        OutOfService,
        TwoSeatsAvailable,
        Full
    }

    internal enum CocoonPullOverParkingPhase
    {
        None,
        ApproachLaneEntry,
        DiagonalIntoBay,
        AlignWithBay,
        SettleAtBay
    }

    internal enum CocoonTaxiDoorSide
    {
        A,
        B
    }

    public enum CocoonBoardingDoorSideMode
    {
        AutoCurbSide,
        ForceSideA,
        ForceSideB
    }

    internal enum CocoonLuggageStoragePhase
    {
        Inactive,
        BecomeA1,
        WaitingForSeat,
        WaitingForMarkers,
        MoveWaypoint,
        RaisePreventer,
        Complete
    }

    internal enum CocoonSeatStoragePhase
    {
        Inactive,
        RotateToMarker,
        MoveToMarker,
        Complete
    }

    internal enum CocoonSeatSitPhase
    {
        Inactive,
        MoveRiderToSeat,
        RotateSeat,
        RotateRo,
        Complete,
        StandingUp
    }

    internal enum CocoonDestinationExitPhase
    {
        Inactive,
        CruisingToDropoff,
        PullOverToCurb,
        SeatToOut,
        LuggageToA1,
        DoorOpening,
        WaitingForExit,
        DoorClosing,
        SeatRestore,
        DestinationBayExit
    }

    public sealed class CocoonTaxiStateMachine : MonoBehaviour
    {
        [Header("Taxi Motion")]
        [SerializeField] private Transform taxiRoot;
        [SerializeField] private Transform cruiseStart;
        [SerializeField] private Transform cruiseEnd;
        [SerializeField] private CocoonTrafficLanePath cruisePath;
        [SerializeField] private CocoonTrafficLanePath primaryCruisePath;
        [SerializeField] private CocoonTrafficLanePath oppositeCruisePath;
        [SerializeField] private CocoonRoadGraph roadGraph;
        [SerializeField] private bool useRoadGraph = true;
        [SerializeField] private CocoonRouteCursor taxiGraphCursor = CocoonRouteCursor.Invalid();
        [SerializeField] private float graphTurnRadiusMeters = 2.4f;
        [SerializeField] private float graphTurnRateDegreesPerSecond = 300f;
        [SerializeField] private int cruiseTargetIndex = 1;
        [SerializeField] private Transform pullOverPoint;
        [SerializeField] private Transform[] pullOverPoints;
        [SerializeField] private int[] pullOverPathIndices;
        [SerializeField] private bool preserveAuthoredTaxiSpawnTransform = true;
        [SerializeField] private float cruiseSpeed = 4.8f;
        [SerializeField] private float detectedSpeed = 2.6f;
        [SerializeField] private float pullOverSpeed = 2.7f;

        [Header("Interaction")]
        [SerializeField] private CocoonRaiseHandDetector raiseHandDetector;
        [SerializeField] private CocoonConfirmGestureDetector confirmGestureDetector;
        [SerializeField] private CocoonSafePickupZone safePickupZone;
        [SerializeField] private Transform riderRoot;
        [SerializeField] private Transform riderHead;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform paymentReader;
        [SerializeField] private Renderer paymentReaderRenderer;
        [SerializeField] private GameObject guidanceRoot;
        [SerializeField] private float confirmationTimeoutSeconds = 8f;
        [SerializeField] private float poseConfirmSeconds = 3f;
        [SerializeField] private float boardingTimeoutSeconds = 30f;
        [SerializeField] private float boardingPromptVisibleSeconds = 5f;
        [SerializeField] private float onboardInteriorRadius = 1.05f;
        [SerializeField] private float paymentReaderRadius = 0.18f;
        [SerializeField] private float paymentHoldSeconds = 0.6f;

        [Header("06 - UI")]
        [SerializeField] private bool enableBodyLightVisuals;
        [SerializeField] private Renderer[] lightRenderers;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Transform doorHinge;
        [SerializeField] private Transform doorSideALeftPanel;
        [SerializeField] private Transform doorSideARightPanel;
        [SerializeField] private Transform doorSideBLeftPanel;
        [SerializeField] private Transform doorSideBRightPanel;
        [SerializeField] private GameObject doorPanel;
        [SerializeField] private GameObject doorSideAPanel;
        [SerializeField] private GameObject doorSideBPanel;
        [SerializeField] private GameObject instructionPanel;
        [SerializeField] private GameObject baggageQuestionPanel;
        [SerializeField] private GameObject boardingCardPromptPanel;
        [SerializeField] private GameObject sitPromptPanel;
        [SerializeField] private GameObject passengerLuggage;
        [SerializeField] private CocoonHeadLockedUITuning headLockedUiTuning;
        [Header("Head-Locked UI Tuning")]
        [Tooltip("Hide all face-locked flow prompts while keeping their timing gates and input triggers active.")]
        [SerializeField] private bool suppressHeadLockedFlowUi = true;
        [Tooltip("When enabled, runtime applies one shared readable layout to every face-locked prompt panel. Disable this only if you want to hand-edit each child Text/RectTransform in 06_UI.")]
        [SerializeField] private bool applyHeadLockedUiRuntimeLayout = true;
        [Tooltip("Canvas layout size in UI pixels. Smaller values simplify the panel; physical size is controlled mostly by Local Scale.")]
        [SerializeField] private Vector2 headLockedPanelSize = new Vector2(720f, 320f);
        [Tooltip("Physical scale of the face-locked panel. Lower this when the panel feels too large in the headset.")]
        [SerializeField] private float headLockedPanelLocalScale = 0.0014f;
        [Tooltip("World-space canvas density. Increase this if text edges look soft; 128 is a good VR starting point.")]
        [SerializeField] private float headLockedCanvasPixelsPerUnit = 128f;
        [Tooltip("Shared multiplier for all face-locked UI text. Increase this when the panel size is good but text is too small.")]
        [SerializeField] private float headLockedTextScale = 1.55f;
        [SerializeField] private CocoonBoardingDoorUISettings boardingDoorUiSettings;
        [SerializeField] private CocoonBoardingDoorSideMode boardingDoorSideMode = CocoonBoardingDoorSideMode.AutoCurbSide;
        [SerializeField] private bool boardingDoorUseManualPanelTransform = true;
        [SerializeField] private Vector2 boardingDoorPanelSize = new Vector2(760f, 560f);
        [SerializeField] private Vector3 boardingDoorPanelLocalOffset = Vector3.zero;
        [SerializeField] private float boardingDoorPanelScale = 0.00042f;
        [SerializeField] private float boardingDoorPanelOutwardOffset = 0.055f;
        [SerializeField] private float boardingDoorPushDistance = 0.08f;
        [SerializeField] private float boardingDoorSlideDistance = 0.22f;
        [SerializeField] private float boardingDoorPushFraction = 0.35f;
        [SerializeField] private float boardingDoorOpenSpeed = 0.2f;
        [SerializeField] private float boardingDoorCloseSpeed = 0.25f;
        [SerializeField] private float riderInsideRequiredSeconds = 5f;
        [SerializeField] private float boardingCabinEntryDepth = 0.22f;
        [SerializeField] private Transform doorL1;
        [SerializeField] private Transform doorL2;
        [SerializeField] private bool hasAuthoredLeftDoorOpenPose;
        [SerializeField] private Vector3 doorL1OpenLocalPosition;
        [SerializeField] private Vector3 doorL2OpenLocalPosition;
        [SerializeField] private Transform boardingTouchZone;
        [SerializeField] private bool doorUiFollowsDoorL2 = true;
        [Header("DOORUI Surface Display")]
        [SerializeField] private RectTransform boardingDoorSurfacePanel;
        [SerializeField] private Texture2D boardingDoorIdleTexture;
        [SerializeField] private Texture2D boardingDoorDetectedTexture;
        [SerializeField] private float boardingDoorSurfaceReferenceWidth = 1024f;
        [Header("Boarding Credit Card")]
        [SerializeField] private CocoonBoardingCreditCardSettings boardingCreditCardSettings;
        [SerializeField] private Transform boardingCreditCard;
        [SerializeField] private Transform boardingCreditCardVisual;
        [SerializeField] private Vector3 cardLocalPosition = new Vector3(0.025f, -0.015f, 0.055f);
        [SerializeField] private Vector3 cardLocalRotation = new Vector3(70f, 0f, 8f);
        [SerializeField] private Vector3 cardLocalScale = new Vector3(0.085f, 0.052f, 0.004f);
        [SerializeField] private float cardBoundsPadding = 0.012f;
        [SerializeField] private Transform luggageBoardingRamp1;
        [SerializeField] private Transform luggageBoardingRamp2;
        [SerializeField] private Transform luggageRampClosedMarker1;
        [SerializeField] private Transform luggageRampClosedMarker2;
        [SerializeField] private bool hasAuthoredLuggageRampStandbyPose;
        [SerializeField] private Vector3 luggageRamp1StandbyLocalPosition;
        [SerializeField] private Quaternion luggageRamp1StandbyLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 luggageRamp1StandbyLocalScale = Vector3.one;
        [SerializeField] private Vector3 luggageRamp2StandbyLocalPosition;
        [SerializeField] private Quaternion luggageRamp2StandbyLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 luggageRamp2StandbyLocalScale = Vector3.one;
        [SerializeField] private float luggageInstructionSeconds = 10f;
        [SerializeField] private float luggageCabinLiftDuration = 0.2f;
        [SerializeField] private float luggageStorageA1ToA11Duration = 1.5f;
        [SerializeField] private float luggageStorageA11ToA12Duration = 1.5f;
        [SerializeField] private float luggageStorageA12ToA13Duration = 1.5f;
        [SerializeField] private float luggageStorageA13ToA2Duration = 3f;
        [SerializeField] private float luggageStorageA2ToA21Duration = 2f;
        [SerializeField] private float luggageStorageA21ToA3Duration = 2f;
        [SerializeField] private float luggagePreventerRaiseDuration = 3f;
        [SerializeField] private Transform luggageStorageBagA1;
        [SerializeField] private Transform luggageStorageA11;
        [SerializeField] private Transform luggageStorageA12;
        [SerializeField] private Transform luggageStorageA13;
        [SerializeField] private Transform luggageStorageBagA2;
        [SerializeField] private Transform luggageStorageA21;
        [SerializeField] private Transform luggageStorageBagA3;
        [SerializeField] private Transform luggagePreventerA1;
        [SerializeField] private bool hasAuthoredLuggagePreventerStowedPose;
        [SerializeField] private Vector3 luggagePreventerStowedLocalPosition;
        [SerializeField] private Quaternion luggagePreventerStowedLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 luggagePreventerStowedLocalScale = Vector3.one;
        [SerializeField] private CocoonSeatV2Controller seatV2Controller;
        [SerializeField] private Transform seat1;
        [SerializeField] private Transform seat1RotationTarget;
        [SerializeField] private Transform seat1MoveTarget;
        [SerializeField] private Transform seat1SitTarget;
        [SerializeField] private Transform seatedRiderHeadAnchor;
        [SerializeField] private Transform ro1;
        [SerializeField] private Transform ro2;
        [SerializeField] private Transform ro3;
        [SerializeField] private float seat1RotateDuration = 3.5f;
        [SerializeField] private float seat1MoveDuration = 4f;
        [SerializeField] private float sitPromptSeconds = 7f;
        [SerializeField] private float destinationExitDoorCloseDelay = 10f;
        [SerializeField] private float seatMoveRiderToAnchorDuration = 3f;
        [SerializeField] private float seatSitRotateDuration = 7f;
        [SerializeField] private float seatedTurnSpeedDegreesPerSecond = 45f;
        [SerializeField] private float seatedTurnStickDeadzone = 0.18f;
        [SerializeField] private bool logSeatSitDiagnostics;
        [SerializeField] private float roFoldDuration = 2f;
        [SerializeField] private float roFoldLocalYDegrees = -90f;
        [SerializeField] private bool useDoorPanelRayPointer = false;
        [SerializeField] private bool preserveAuthoredTaxiComposition = true;
        [SerializeField] private Text headlineText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text doorHeadlineText;
        [SerializeField] private Text doorStatusText;
        [SerializeField] private Text doorTimerText;
        [SerializeField] private Text destinationText;
        [SerializeField] private Text instructionText;
        [SerializeField] private RectTransform windshieldPanel;
        [SerializeField] private RectTransform rearWindshieldPanel;
        [SerializeField] private Text windshieldText;
        [SerializeField] private Text rearWindshieldText;
        [SerializeField] private Transform frontLed1;
        [SerializeField] private Transform frontLed2;
        [SerializeField] private Transform frontLed3;
        [SerializeField] private Transform rearOled1;
        [SerializeField] private Transform rearOled2;
        [SerializeField] private Transform rearOled3;
        [SerializeField] private Transform frontOled1;
        [SerializeField] private Transform frontOled2;
        [SerializeField] private Transform frontOled3;
        [SerializeField] private MeshRenderer frontUnifiedDisplayRenderer;
        [SerializeField] private MeshRenderer rearUnifiedDisplayRenderer;
        [SerializeField] private Texture2D availableExteriorScreen;
        [SerializeField] private Texture2D detectedExteriorScreen;
        [SerializeField] private Texture2D stoppingFollowMeExteriorScreen;
        [SerializeField] private Texture2D goodbyeExteriorScreen;
        [SerializeField] private Texture2D outOfServiceExteriorScreen;
        [SerializeField] private Texture2D twoSeatsAvailableExteriorScreen;
        [SerializeField] private Texture2D fullExteriorScreen;
        [SerializeField] private float exteriorScreenPixelsPerUnit = 768f;
        [SerializeField] private float exteriorScreenMipMapBias = -1.25f;
        [SerializeField] private bool useHighReadabilityExteriorScreenMaterial = true;

        private CocoonTaxiState state;
        private float stateTimer;
        private float poseConfirmTimer;
        private float paymentHoldTimer;
        private float doorOpenAmount;
        private string selectedDestination = "Duomo";
        private string selectedRideMode = "Solo";
        private Material[] lightMaterials;
        private Material bodyMaterial;
        private Material paymentReaderMaterial;
        private RawImage windshieldImage;
        private RawImage rearWindshieldImage;
        private Material exteriorScreenRuntimeMaterial;
        private Material frontUnifiedDisplayRuntimeMaterial;
        private Material rearUnifiedDisplayRuntimeMaterial;
        private CocoonCabinScreenController cabinScreenController;
        private CocoonLightSequenceController lightSequenceController;
        private bool hasAppliedCabinOnboardUi;
        private bool hasAppliedCabinSeatedUi;
        private bool hasAppliedCabinDestinationUi;
        private bool hasPlayedFloorEntryLights;
        private bool hasPlayedSeatOutLights;
        private bool hasPlayedLuggagePreventerLights;
        private bool destinationDropoffRequested;
        private CocoonDestinationExitPhase destinationExitPhase;
        private float destinationExitTimer;
        [SerializeField] private float destinationDropoffCruiseSeconds = 40f;
        [SerializeField] private Transform destinationStopPoint;
        [SerializeField] private float destinationStopCruiseSpeed = 5.8f;
        [SerializeField] private float destinationStopArrivalDistance = 0.12f;
        [SerializeField] private bool autoBeginDestinationAfterSeated = true;
        [SerializeField] private bool enableDestinationExitAReset = true;
        private CocoonRouteCursor destinationStopCursor = CocoonRouteCursor.Invalid();
        private bool hasDestinationStopCursor;
        private bool hasLoggedDestinationStopPoint;
        private bool hasLoggedDestinationFallbackTimer;
        private bool hasLoggedDestinationGraphProjectionFailure;
        private bool hasLoggedDestinationGraphDistanceFailure;
        private bool hasLoggedDestinationDepartureProjectionFailure;
        private bool destinationDepartureGraphOnly;
        private bool hasLoggedDestinationDepartureGraphHold;
        private bool destinationRiderDetached;
        private bool destinationExitReadyForAReset;
        private bool destinationExitResetButtonWasDown;
        private bool destinationSeatRestoreStarted;
        private Transform selectedPullOverPoint;
        private CocoonTrafficLanePath selectedPullOverCruisePath;
        private int selectedPullOverPathIndex = -1;
        private int selectedPullOverEntryPathIndex = -1;
        private bool selectedPullOverDirectPullIn;
        private int selectedGraphBayIndex = -1;
        private CocoonRouteCursor selectedGraphBayCursor = CocoonRouteCursor.Invalid();
        private CocoonRouteCursor selectedGraphParkingEntryCursor = CocoonRouteCursor.Invalid();
        private float selectedGraphBayForwardDistance;
        private float selectedGraphParkingEntryForwardDistance;
        private bool hasSelectedGraphParkingEntryCursor;
        private bool hasLoggedMissingGraphParkingEntryProjection;
        private bool pendingTurnaroundToOppositeLane;
        private int turnaroundPathIndex = -1;
        private bool reachedSelectedPullOverEntry;
        private CocoonPullOverParkingPhase parkingPhase;
        private Vector3 parkingLaneEntryPosition;
        private Vector3 parkingDiagonalPosition;
        private Vector3 parkingAlignPosition;
        private Vector3 parkingFinalPosition;
        private Vector3 parkingApproachForward = Vector3.forward;
        private Quaternion parkingFinalRotation;
        private CocoonTaxiDoorSide activeSlidingDoorSide;
        private bool hasSelectedSlidingDoorSide;
        private readonly SlidingDoorPanel[] slidingDoorPanels = new SlidingDoorPanel[4];
        private bool hasCachedSlidingDoorClosedPose;
        private bool hasCachedLeftDoorClosedPose;
        private Vector3 doorL1ClosedLocalPosition;
        private Vector3 doorL2ClosedLocalPosition;
        private float rampOpenAmount;
        private AuthoredRampPose luggageRampPose1;
        private AuthoredRampPose luggageRampPose2;
        private Collider boardingTouchCollider;
        private Collider boardingCreditCardCollider;
        private Material boardingCreditCardMaterial;
        private Material boardingCreditCardAccentMaterial;
        private RawImage boardingDoorSurfaceImage;
        private bool hasLoggedBoardingDoorSurfaceMissing;
        private Transform boardingDoorSurfaceFollowTarget;
        private Vector3 boardingDoorSurfaceLocalPositionToDoorUi;
        private Quaternion boardingDoorSurfaceLocalRotationToDoorUi = Quaternion.identity;
        private bool hasBoardingDoorSurfaceFollowPose;
        private Vector3 doorUiClosedLocalPositionToDoorL2;
        private Quaternion doorUiClosedLocalRotationToDoorL2 = Quaternion.identity;
        private bool hasDoorUiDoorL2FollowPose;
        private bool hasLoggedDoorUiDoorL2FollowPose;
        private bool hasLoggedDoorUiFollowMissing;
        private float nextBoardingCreditCardMissLogTime;
        private bool boardingTouchConfirmed;
        private bool hasLoggedBoardingTouch;
        private bool hasLoggedBoardingConfirmationWaitingForRamp;
        private bool hasLoggedBoardingCreditCardShown;
        private bool hasLoggedBoardingCreditCardTouch;
        private bool hasLoggedRampDeployHalfway;
        private bool hasLoggedRampDeployReady;
        private bool hasLoggedMissingBoardingTouchZone;
        private bool hasLoggedLeftDoorBinding;
        private bool hasLoggedMissingLeftDoorPanels;
        private bool hasLoggedLeftDoorMotionStart;
        private bool hasLoggedLeftDoorMotionComplete;
        private bool hasLoggedAuthoredRampBinding;
        private bool hasLoggedMissingAuthoredRamps;
        private bool hasLoggedRampInitialStandby;
        private bool hasLoggedTaxiGroundSnap;
        private bool hasLoggedParkingGroundConform;
        private float nextParkingSpeedDiagnosticTime;
        private string lastParkingSpeedDiagnosticKey;
        [SerializeField] private bool enableParkingSpeedDiagnostics;
        private bool hasLoggedSolidCabinSupports;
        private bool hasLoggedMissingSlidingDoorPanels;
        private bool hasLoggedSlidingDoorBinding;
        private Transform slidingDoorUiAnchor;
        private bool riderOnboard;
        private bool lastLoggedRiderCabinInside;
        private bool hasLoggedRiderCabinInside;
        private bool hasLoggedRiderAttachment;
        private bool baggageQuestionAnswered;
        private bool riderHasLuggage;
        private bool baggageInstructionActive;
        private float baggageInstructionTimer;
        private bool doorDecisionConfirmWasPressed;
        private bool doorDecisionDeclineWasPressed;
        private Transform passengerLuggageOriginalParent;
        private bool hasPassengerLuggageOriginalParent;
        private bool passengerLuggageStowedInTaxi;
        private bool passengerLuggagePlacedAtRest;
        private CocoonLuggageStoragePhase passengerLuggageStoragePhase;
        private float passengerLuggageStorageTimer;
        private bool hasAuthoredLuggageStorageBagA1Pose;
        private Vector3 luggageStorageBagA1LocalPosition;
        private Quaternion luggageStorageBagA1LocalRotation = Quaternion.identity;
        private Vector3 luggageStorageBagA1LocalScale = Vector3.one;
        private Vector3 luggageStorageBagA1ArcStartPosition;
        private Quaternion luggageStorageBagA1ArcStartRotation = Quaternion.identity;
        private Vector3 luggageStorageBagA1ArcStartWorldScale = Vector3.one;
        private readonly List<Transform> luggageStorageWaypointTargets = new List<Transform>(6);
        private readonly List<float> luggageStorageWaypointDurations = new List<float>(6);
        private readonly List<LuggageExitPose> luggageExitPoseTargets = new List<LuggageExitPose>(6);
        private Vector3 luggageStorageSegmentStartPosition;
        private Quaternion luggageStorageSegmentStartRotation = Quaternion.identity;
        private Vector3 luggageStorageSegmentStartWorldScale = Vector3.one;
        private int luggageStorageWaypointIndex;
        private int luggageExitPoseIndex;
        private Vector3 luggageExitSegmentStartPosition;
        private Quaternion luggageExitSegmentStartRotation = Quaternion.identity;
        private Vector3 luggageExitSegmentStartWorldScale = Vector3.one;
        private Vector3 luggagePreventerRaiseStartLocalPosition;
        private bool hasLoggedLuggageStorageBinding;
        private bool hasLoggedLuggageStorageMissingMarkers;
        private bool hasLoggedLuggageStorageA1;
        private bool hasLoggedLuggageStorageA2;
        private bool hasLoggedLuggageStorageA3;
        private bool hasLoggedLuggagePreventerRaised;
        private bool hasLoggedLuggageSupportContactStorageStart;
        private bool hasLoggedLuggageStorageWaitingForSeat;
        private bool hasLoggedLuggageStorageWaitingForMarkers;
        private CocoonSeatStoragePhase seatStoragePhase;
        private float seatStorageTimer;
        private bool seatSitUsingSeatV2Controller;
        private bool seatSitV2BackMovementStarted;
        private bool hasAuthoredSeat1StowedPose;
        private Vector3 seat1StowedLocalPosition;
        private Quaternion seat1StowedLocalRotation = Quaternion.identity;
        private Vector3 seat1StowedLocalScale = Vector3.one;
        private Vector3 seatStorageSegmentStartPosition;
        private Quaternion seatStorageSegmentStartRotation = Quaternion.identity;
        private Vector3 seatStorageSegmentStartWorldScale = Vector3.one;
        private Vector3 seatStoragePivotLocalPoint;
        private Vector3 seatStoragePivotWorldPosition;
        private bool seatStorageHasRendererPivot;
        private bool hasLoggedSeatStorageBinding;
        private bool hasLoggedSeatStorageMissingMarkers;
        private bool hasLoggedSeatStorageMissingPivot;
        private bool hasLoggedSeatStorageRotateStart;
        private bool hasLoggedSeatStorageMoveStart;
        private bool hasLoggedSeatStorageComplete;
        private bool seatStorageUsingSeatV2;
        private CocoonSeatSitPhase seatSitPhase;
        private float seatSitTimer;
        private bool sitPromptWasShown;
        private bool sitPromptActive;
        private bool sitPromptWasPressed;
        private bool seatToggleButtonWasDown;
        private bool riderIsSeated;
        private float sitPromptTimer;
        private Vector3 seatSitMoveStartHeadPosition;
        private Quaternion seatSitMoveStartHeadRotation = Quaternion.identity;
        private Vector3 seatSitMoveStartHeadTaxiLocalPosition;
        private Quaternion seatSitMoveStartHeadTaxiLocalRotation = Quaternion.identity;
        private bool seatSitMoveStartUsesTaxiReference;
        private Quaternion seatSitMoveStartRigWorldYawRotation = Quaternion.identity;
        private Quaternion seatSitMoveStartRigTaxiLocalYawRotation = Quaternion.identity;
        private bool seatSitMoveStartUsesTaxiRigReference;
        private Vector3 seatSitMoveStartRigToHeadWorldOffset;
        private Vector3 seatSitMoveStartRigToHeadTaxiLocalOffset;
        private bool hasSeatSitMoveStartWorldOffset;
        private bool hasLoggedSeatSitSafetyFallback;
        private Vector3 seatSitSegmentStartLocalPosition;
        private Quaternion seatSitSegmentStartLocalRotation = Quaternion.identity;
        private Vector3 seatSitSegmentStartLocalScale = Vector3.one;
        private Quaternion seatSitTargetLocalRotation = Quaternion.identity;
        private Vector3 seatSitPivotLocalPoint;
        private Vector3 seatSitPivotParentLocalPosition;
        private bool seatSitHasRendererPivot;
        private bool seatSitRotatePendingContinuousEntry;
        private bool hasSeatSitRotatePreviousAnchorRotation;
        private Quaternion seatSitRotatePreviousAnchorRotation = Quaternion.identity;
        private bool hasSeatSitMoveFinalPose;
        private Vector3 seatSitMoveFinalHeadPosition;
        private Quaternion seatSitMoveFinalHeadRotation = Quaternion.identity;
        private Vector3 seatSitMoveFinalRigPosition;
        private Quaternion seatSitMoveFinalRigRotation = Quaternion.identity;
        private Vector3 seatedHeadAnchorLocalPosition;
        private Quaternion seatedHeadAnchorLocalRotation = Quaternion.identity;
        private bool hasSeatedHeadAnchorLocalPose;
        private readonly Transform[] roFoldTransforms = new Transform[3];
        private readonly Vector3[] roFoldStartFramePositions = new Vector3[3];
        private readonly Quaternion[] roFoldStartFrameRotations = new Quaternion[3];
        private readonly Vector3[] roFoldStartLocalScales = new Vector3[3];
        private Transform roFoldFrame;
        private Vector3 roFoldAxisPointFrameLocal;
        private Vector3 roFoldAxisDirectionFrameLocal = Vector3.up;
        private bool roFoldHasFrameAxis;
        private readonly Vector3[] roFoldStowedLocalPositions = new Vector3[3];
        private readonly Quaternion[] roFoldStowedLocalRotations = new Quaternion[3];
        private readonly Vector3[] roFoldStowedLocalScales = new Vector3[3];
        private bool hasAuthoredRoFoldStowedPose;
        private bool roFolded;
        private bool hasLoggedSeatSitBinding;
        private bool hasLoggedSeatSitMissingMarkers;
        private bool hasLoggedSeatSitPrompt;
        private bool hasLoggedSeatMoveStart;
        private bool hasLoggedSeatMoveCompleteWithoutSnap;
        private bool hasLoggedSeatRotateContinuousEntry;
        private bool hasLoggedSeatRotateAnchorFollow;
        private bool hasLoggedSeatSitStart;
        private bool hasLoggedRoFoldStart;
        private bool hasLoggedSeatSitComplete;
        private bool hasLoggedSeatStandUp;
        private bool hasLoggedSeatedTurn;
        private bool hasLoggedRiderComfortVisibility;
        private bool hasLoggedPassengerLuggageVisible;
        private Transform riderOriginalParent;
        private bool hasRiderOriginalParent;
        private Transform authoredRiderRootParent;
        private bool hasAuthoredRiderRootTransform;
        private Vector3 authoredRiderRootLocalPosition;
        private Quaternion authoredRiderRootLocalRotation;
        private Vector3 authoredRiderRootLocalScale = Vector3.one;
        private Vector3 authoredRiderRootWorldPosition;
        private Quaternion authoredRiderRootWorldRotation;
        private float riderInsideCabinTimer;
        private float nextRiderInsideHoldLogTime;
        private GameObject activeDoorPanel;
        private CocoonTrafficParticipant trafficParticipant;
        private bool departureCompletesRide;
        private Vector3 departureMergePosition;
        private Quaternion departureMergeRotation;
        private int departureCruiseResumeIndex = -1;
        private bool hasDepartureMergeTarget;
        private bool reachedDepartureMergeTarget;
        private int destinationBayExitStep;
        private Vector3 destinationBayExitLeadPosition;
        private CocoonRouteCursor destinationBayExitMergeCursor = CocoonRouteCursor.Invalid();
        private bool hasDestinationBayExitMergeCursor;
        private string lastLoggedDestinationBayExitStep = "";
        private bool hasAuthoredTaxiSpawnTransform;
        private Vector3 authoredTaxiSpawnPosition;
        private Quaternion authoredTaxiSpawnRotation;
        private Color currentLightColor = ExteriorAvailableGreen;
        private float currentLightIntensity = 1.8f;
        private bool lightBlinking;
        private string currentLightMode = "";
        private CocoonTaxiState previousLoggedState;
        private bool hasLoggedState;
        private bool lastLoggedPaymentNear;
        private bool hasLoggedPaymentNear;
        private bool hasLoggedMissingDoorPanel;
        private bool hasLoggedHeadLockedInstructionPanel;
        private bool hasLoggedNamedLightBinding;
        private bool hasLoggedMissingNamedLightBinding;
        private bool hasLoggedMissingExteriorScreenTexture;
        private bool hasBuiltFrontUnifiedDisplayMesh;
        private bool hasBuiltRearUnifiedDisplayMesh;
        private float frontUnifiedDisplayMeshAspect = -1f;
        private float rearUnifiedDisplayMeshAspect = -1f;
        private bool hasLoggedFrontUnifiedDisplayBinding;
        private bool hasLoggedRearUnifiedDisplayBinding;
        private bool hasLoggedMissingFrontLed;
        private bool hasLoggedMissingRearOled;
        private bool hasLoggedFrontUnifiedDisplayFallback;
        private bool hasLoggedRearUnifiedDisplayFallback;
        private bool hasLoggedFrontUnreadableUnifiedDisplayMesh;
        private bool hasLoggedRearUnreadableUnifiedDisplayMesh;
        private float nextPoseProgressLogTime;
        private float nextPaymentProgressLogTime;
        private float nextPullOverProgressLogTime;
        private float taxiMotionSpeed;
        private bool taxiTrafficHardBlocked;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private const float RequiredPoseConfirmSeconds = 3f;
        private const float RiderCameraNearClipPlane = 0.005f;
        private const float RiderControllerRadiusMeters = 0.08f;
        private const float RightHandProxyScale = 0.035f;
        private const float LeftHandProxyScale = 0.032f;
        private const float HandProxyAlpha = 1f;
        private static readonly Color ExteriorAvailableGreen = new Color(0f, 1f, 0.04f);
        private static readonly Color ExteriorDetectedMintBlue = new Color(0f, 1f, 1f);
        private static readonly Color ExteriorWhite = Color.white;
        private static readonly Color ExteriorTwoSeatsYellow = new Color(1f, 0.816f, 0.004f);
        private static readonly Color ExteriorFullRed = new Color(1f, 0.004f, 0.004f);
        private static readonly string[] TaxiLightRendererNames =
        {
            "Roof Availability Light",
            "Front Brow Intent Light",
            "Front Lower Intent Light",
            "Right Lower Guidance Light",
            "Left Lower Guidance Light",
            "Rear Confirmation Light"
        };

        private static readonly string[] ExteriorScreenResourceNames =
        {
            "ext-screen",
            "ext-screen-1",
            "ext-screen-2",
            "ext-screen-3",
            "ext-screen-4",
            "ext-screen-5",
            "ext-screen-6"
        };

        private const string ExteriorScreenResourceFolder = "ExteriorScreens/";
        private const string ExteriorScreenImageObjectName = "Exterior Screen Image";
        private const string FrontWindshieldPanelName = "Taxi Front Windshield Display";
        private const string RearWindshieldPanelName = "Taxi Rear Windshield Display";
        private const string FrontLed1Name = "led1";
        private const string FrontLed2Name = "led2";
        private const string FrontLed3Name = "led3";
        private const string RearOled1Name = "oled1";
        private const string RearOled2Name = "oled2";
        private const string RearOled3Name = "oled3";
        private const string FrontUnifiedDisplayName = "Cocoon Front LED Unified Display";
        private const string RearUnifiedDisplayName = "Cocoon Rear OLED Unified Display";
        private const string OledMaterialNameFragment = "OLED";
        private const string OledChineseMaterialNameFragment = "\u663E\u793A\u5668";
        private const float UnifiedDisplaySurfaceOffset = 0.002f;
        private const string LuggageRampName = "Luggage Boarding Ramp";
        private const string LuggageRamp1Name = "Luggage Boarding Ramp1";
        private const string LuggageRamp2Name = "Luggage Boarding Ramp2";
        private const string LuggageRampClosedMarker1Name = "\u5B9E\u4F5321";
        private const string LuggageRampClosedMarker2Name = "\u5B9E\u4F5322";
        private const string BoardingTouchZoneName = "DOORUI";
        private const string BoardingDoorSurfacePanelName = "DOORUI Surface Display Panel";
        private const string BoardingDoorSurfaceImageName = "DOORUI Surface Image";
        private const string BoardingDoorScreenResourceFolder = "BoardingDoorScreens/";
        private const string BoardingDoorIdleScreenResourceName = "external-screen-UI-idle";
        private const string BoardingDoorDetectedScreenResourceName = "external-screen-UI-detected";
        private const string BoardingCreditCardName = "Right Hand Boarding Credit Card";
        private const string BoardingCreditCardVisualName = "Credit Card Visual";
        private const string LeftDoorPanel1Name = "doorL1";
        private const string LeftDoorPanel2Name = "doorL2";
        private const string PassengerLuggageModelName = "Passenger Luggage Model";
        private const string PassengerLuggageBaselineName = "bag0";
        private const string TaxiPodVisualsName = "Taxi Pod Visuals";
        private const string FinalTaxiInteriorStructureName = "Final Taxi Interior Structure";
        private const string LuggageCabinSupportAName = "\u5B9E\u4F538";
        private const string LuggageCabinSupportBName = "\u5B9E\u4F5319";
        private const string LuggageCabinSupportFallbackAName = "xx0 (8)";
        private const string LuggageCabinSupportFallbackBName = "xx0 (19)";
        private const string LuggageCabinSupportTag = "solid";
        private const string LuggageStorageBagA1Name = "bagA1";
        private const string LuggageStorageBagA2Name = "bagA2";
        private const string LuggageStorageBagA3Name = "bagA3";
        private const string LuggagePreventerA1Name = "luggage_preventrs_A1";
        private static readonly string[] LuggageStorageA11Names = { "A1.1", "bagA1.1", "1.1" };
        private static readonly string[] LuggageStorageA12Names = { "A1.2", "bagA1.2", "1.2" };
        private static readonly string[] LuggageStorageA13Names = { "A1.3", "bagA1.3", "1.3" };
        private static readonly string[] LuggageStorageA21Names = { "A2.1", "bagA2.1", "2.1" };
        private const string Seat1AnchorName = "seat1 Static Anchor";
        private const string Seat1Name = "seat1";
        private const string Seat1RotationTargetName = "seat1_";
        private static readonly string[] Seat1MoveTargetNames = { "1x", "seat1x" };
        private static readonly string[] Seat1SitTargetNames = { "1x_", "seat1x_" };
        private const string SeatedRiderHeadAnchorName = "Seated Rider Head Anchor";
        private const string Ro1Name = "ro1";
        private const string Ro2Name = "ro2";
        private const string Ro3Name = "ro3";
        private const string BoardingCardPromptPanelName = "Boarding Card Prompt Panel";
        private const string SitPromptPanelName = "Sit Prompt Panel";
        private const string Wheel1Name = "wheel1";
        private const string Wheel2Name = "wheel2";
        private const string Wheel3Name = "wheel3";
        private const string Wheel4Name = "wheel4";
        private const float ParkingGroundRaycastHeight = 0.9f;
        private const float ParkingGroundRaycastDistance = 2.2f;
        private const float ParkingGroundMaxDownCorrection = 0.012f;
        private const float LuggageSolidSupportPadding = 0.04f;
        private const float LuggageSupportContactHeightTolerance = 0.08f;
        private const float ExteriorScreenReferenceWidth = 1180f;
        private const float ExteriorScreenSourceWidth = 23040f;
        private const float ExteriorScreenSourceHeight = 2080f;
        private static readonly Vector3 DoorPanelLocalPosition = new Vector3(0.24f, 0.34f, 0.55f);
        private const float TaxiTrafficLength = 3.6f;
        private const float TaxiTrafficWidth = 1.65f;
        private const float MinimumTaxiCruiseSpeed = 4.8f;
        private const float MinimumTaxiHailSpeed = 2.6f;
        private const float MinimumTaxiPullOverSpeed = 2.7f;
        private const float MinimumParkingLineupSpeed = 2.3f;
        private const float MinimumParkingDiagonalSpeed = 2.15f;
        private const float MinimumParkingAlignSpeed = 1.9f;
        private const float MinimumParkingSettleSpeed = 1.6f;
        private const float TaxiAccelerationMetersPerSecond = 4.2f;
        private const float TaxiBrakingMetersPerSecond = 7.5f;
        private const float TaxiParkingSlowdownDistanceMeters = 6f;
        private const float TaxiTrafficHardClearanceMeters = 0.45f;
        private const float TaxiTrafficHardLookAheadSeconds = 0.35f;
        private const float MinimumTaxiCrawlSpeed = 0.35f;
        private const float ParkingFallbackLaneOffset = 3.2f;
        private const float ParkingMinimumLeadInDistance = 4.6f;
        private const float ParkingMaximumLeadInDistance = 7.4f;
        private const float ParkingFinalArrivalDistance = 0.08f;
        private const float ParkingFinalAngleTolerance = 2.5f;
        private const float TurnaroundImmediateArrivalDistance = 0.18f;
        private const float SerializedBayIndexWarningDistanceMeters = 0.75f;
        private const float DirectBayPullInMaxDistanceMeters = 4f;
        private const float DirectBayPullInBehindToleranceMeters = 0.1f;
        private const float DepartureMergeLeadDistanceMeters = 5.2f;
        private const float DepartureMergeArrivalDistance = 0.12f;
        private CocoonTaxiExteriorScreenState availabilityExteriorScreenState = CocoonTaxiExteriorScreenState.Available;
        private CocoonTaxiExteriorScreenState currentExteriorScreenState = (CocoonTaxiExteriorScreenState)(-1);

        public CocoonTaxiState State => state;

        public bool IsDoorPanelInteractive => useDoorPanelRayPointer && state == CocoonTaxiState.ConfirmRide;
        private bool IsBaggageQuestionInteractive => baggageQuestionPanel != null && baggageQuestionPanel.activeInHierarchy && !baggageQuestionAnswered && !riderOnboard;
        private readonly List<XRInputDevice> baggageChoiceDevices = new List<XRInputDevice>();
        private bool baggageYesWasPressed;
        private bool baggageNoWasPressed;

        public CocoonTrafficLanePath PrimaryCruiseLane => primaryCruisePath != null ? primaryCruisePath : cruisePath;

        public CocoonTrafficLanePath OppositeCruiseLane => oppositeCruisePath;

        public static bool IsPreRideTrafficReleased { get; private set; }

        public void SetAvailabilityExteriorScreen(CocoonTaxiExteriorScreenState screenState)
        {
            if (!IsAvailabilityCapacityScreen(screenState))
            {
                CocoonDebugLog.Info("TaxiDisplay", "Ignored availability screen state " + screenState + "; use Available, TwoSeatsAvailable, or Full.", this);
                return;
            }

            availabilityExteriorScreenState = screenState;
            if (state == CocoonTaxiState.Availability)
            {
                ApplyExteriorScreenLights(availabilityExteriorScreenState, 2.6f, "AVAILABLE");
                TryApplyExteriorScreenState(availabilityExteriorScreenState, out _);
            }
        }

        public void ShowOutOfServiceExteriorScreen()
        {
            ApplyExteriorScreenLights(CocoonTaxiExteriorScreenState.OutOfService, 2.4f, "OUT OF SERVICE");
            TryApplyExteriorScreenState(CocoonTaxiExteriorScreenState.OutOfService, out _);
        }

        public void Configure(
            Transform taxi,
            Transform start,
            Transform end,
            CocoonTrafficLanePath path,
            int pathStartIndex,
            Transform pullOver,
            Transform[] pullOvers,
            int[] pullOverIndices,
            Transform door,
            GameObject panel,
            Transform controllerRightHand,
            Transform reader,
            Renderer readerRenderer,
            CocoonRaiseHandDetector raiseDetector,
            CocoonConfirmGestureDetector confirmDetector,
            CocoonSafePickupZone pickupZone,
            GameObject guidance,
            Renderer[] lights,
            Renderer body,
            GameObject streetPanel,
            Text headline,
            Text status,
            Text timer,
            Text doorHeadline,
            Text doorStatus,
            Text doorTimer,
            Text destination,
            Text instructions,
            Text windshield,
            Text rearWindshield)
        {
            taxiRoot = taxi;
            cruiseStart = start;
            cruiseEnd = end;
            cruisePath = path;
            primaryCruisePath = path;
            oppositeCruisePath = null;
            cruiseTargetIndex = Mathf.Max(1, pathStartIndex + 1);
            pullOverPoint = pullOver;
            pullOverPoints = pullOvers;
            pullOverPathIndices = pullOverIndices;
            doorHinge = door;
            doorPanel = panel;
            rightHand = controllerRightHand;
            paymentReader = reader;
            paymentReaderRenderer = readerRenderer;
            raiseHandDetector = raiseDetector;
            confirmGestureDetector = confirmDetector;
            safePickupZone = pickupZone;
            guidanceRoot = guidance;
            lightRenderers = lights;
            bodyRenderer = body;
            instructionPanel = streetPanel;
            headlineText = headline;
            statusText = status;
            timerText = timer;
            doorHeadlineText = doorHeadline;
            doorStatusText = doorStatus;
            doorTimerText = doorTimer;
            destinationText = destination;
            instructionText = instructions;
            windshieldText = windshield;
            rearWindshieldText = rearWindshield;
            windshieldPanel = windshieldText != null ? windshieldText.transform.parent as RectTransform : windshieldPanel;
            rearWindshieldPanel = rearWindshieldText != null ? rearWindshieldText.transform.parent as RectTransform : rearWindshieldPanel;
            EnsureTrafficParticipant();
            if (trafficParticipant != null)
            {
                trafficParticipant.Configure(cruisePath, taxiRoot != null ? taxiRoot : transform, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), cruiseTargetIndex);
                trafficParticipant.SetBlocksTraffic(true);
            }

            CacheMaterialInstances();
            HidePickupBayVisuals();
            CocoonDebugLog.Info("Setup", "Taxi state machine configured. Cruise path points=" + (cruisePath != null ? cruisePath.Count.ToString() : "none") + ", pullOverBays=" + CountPullOverPoints() + ".", this);
            CocoonDebugLog.Info("TaxiDisplay", "Windshield displays configured front=" + (windshieldText != null) + ", rear=" + (rearWindshieldText != null) + ", pose confirm=" + poseConfirmSeconds.ToString("0.0") + "s.", this);
        }

        private void Awake()
        {
            poseConfirmSeconds = RequiredPoseConfirmSeconds;
            EnsureRouteCandidates();
            ResolveLeftBoardingDoorPanels();
            ResolveBoardingTouchZone();
            ResolveLuggageRampReference();
            ResolveBaggageReferences();
            EnsureTrafficParticipant();
            DisableTaxiPhysicalCollision();
            CaptureAuthoredTaxiSpawnTransform();
            CaptureAuthoredRiderRootTransform();
            CacheMaterialInstances();
            EnsureRiderComfortVisibility();
        }

        private void Start()
        {
            ResetExperience();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ResetExperience();
            }

            if (TryHandleDestinationExitResetInput())
            {
                return;
            }

            bool destinationExitStagingActive = destinationExitPhase != CocoonDestinationExitPhase.Inactive;
            if (!destinationExitStagingActive)
            {
                switch (state)
                {
                    case CocoonTaxiState.Availability:
                        RunAvailability();
                        break;
                    case CocoonTaxiState.HailDetected:
                        RunHailDetected();
                        break;
                    case CocoonTaxiState.AwaitPoseConfirm:
                        RunAwaitPoseConfirm();
                        break;
                    case CocoonTaxiState.SafePullOver:
                        RunSafePullOver();
                        break;
                    case CocoonTaxiState.ReachPickupPoint:
                        RunReachPickupPoint();
                        break;
                    case CocoonTaxiState.ConfirmRide:
                        RunTouchBoardingConfirmation();
                        break;
                    case CocoonTaxiState.ChooseDestination:
                        RunDoorStep("Choose destination", "Pick a destination preset.");
                        break;
                    case CocoonTaxiState.ChooseRideMode:
                        RunDoorStep("Choose ride", "Choose Solo or Shared.");
                        break;
                    case CocoonTaxiState.ContactlessPayment:
                        RunContactlessPayment();
                        break;
                    case CocoonTaxiState.PaymentConfirmed:
                        RunPaymentConfirmed();
                        break;
                    case CocoonTaxiState.DoorOpening:
                        RunDoorOpening();
                        break;
                    case CocoonTaxiState.DoorOpen:
                        RunDoorOpen();
                        break;
                    case CocoonTaxiState.DoorClosing:
                        RunDoorClosing();
                        break;
                    case CocoonTaxiState.Departing:
                        RunDeparting();
                        break;
                }
            }

            RefreshActiveHeadLockedPanels();
            UpdatePassengerLuggageStorageSequence();
            UpdatePassengerSeatStorageSequence();
            UpdateDestinationExitSequence();
            UpdateSeatDownPromptAndSequence();
            HandleExteriorScreenKeyboardBackdoor();
            UpdateBlinkingLights();
        }

        private bool TryHandleDestinationExitResetInput()
        {
            if (!enableDestinationExitAReset)
            {
                destinationExitResetButtonWasDown = false;
                return false;
            }

            bool controllerDown = ReadRightControllerButton(XRCommonUsages.primaryButton);
            bool keyboardPressed = Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame;
            bool pressedThisFrame = keyboardPressed || (controllerDown && !destinationExitResetButtonWasDown);
            destinationExitResetButtonWasDown = controllerDown;

            if (!pressedThisFrame ||
                !destinationExitReadyForAReset ||
                !destinationRiderDetached ||
                riderOnboard)
            {
                return false;
            }

            CocoonDebugLog.Info("Destination", "Destination exit reset requested by A; resetting experience.", this);
            ResetExperience();
            return true;
        }

        public void ResetExperience()
        {
            CocoonDebugLog.Info("Flow", "Reset experience requested.", this);
            EnsureRiderComfortVisibility();
            DetachRiderFromTaxi();
            RestoreAuthoredRiderRootTransform();
            EnsureRouteCandidates();
            if (primaryCruisePath != null)
            {
                cruisePath = primaryCruisePath;
            }

            if (taxiRoot != null)
            {
                if (preserveAuthoredTaxiSpawnTransform && hasAuthoredTaxiSpawnTransform)
                {
                    taxiRoot.position = authoredTaxiSpawnPosition;
                    taxiRoot.rotation = authoredTaxiSpawnRotation;
                    cruiseTargetIndex = ResolveCruiseTargetIndexFromTaxiPosition();
                    CocoonDebugLog.Info("Taxi", "Authored taxi spawn transform restored at " + FormatPosition(taxiRoot.position) + ", target=" + cruiseTargetIndex + ".", this);
                }
                else if (cruisePath != null && cruisePath.Count > 1)
                {
                    Transform start = cruisePath.GetWaypoint(0);
                    Transform next = cruisePath.GetWaypoint(1);
                    if (start != null)
                    {
                        taxiRoot.position = start.position;
                    }

                    if (start != null && next != null)
                    {
                        SetFacing(next.position - start.position);
                    }

                    cruiseTargetIndex = 1;
                }
                else if (cruiseStart != null)
                {
                    taxiRoot.position = cruiseStart.position;
                    taxiRoot.rotation = cruiseStart.rotation;
                }

                SnapTaxiRootToGround("reset");
            }

            selectedDestination = "Duomo";
            selectedRideMode = "Solo";
            poseConfirmTimer = 0f;
            paymentHoldTimer = 0f;
            selectedPullOverPoint = null;
            selectedPullOverCruisePath = null;
            selectedPullOverPathIndex = -1;
            selectedPullOverEntryPathIndex = -1;
            selectedPullOverDirectPullIn = false;
            selectedGraphBayIndex = -1;
            selectedGraphBayCursor = CocoonRouteCursor.Invalid();
            selectedGraphParkingEntryCursor = CocoonRouteCursor.Invalid();
            selectedGraphBayForwardDistance = 0f;
            selectedGraphParkingEntryForwardDistance = 0f;
            hasSelectedGraphParkingEntryCursor = false;
            hasLoggedMissingGraphParkingEntryProjection = false;
            pendingTurnaroundToOppositeLane = false;
            turnaroundPathIndex = -1;
            reachedSelectedPullOverEntry = false;
            parkingPhase = CocoonPullOverParkingPhase.None;
            taxiMotionSpeed = 0f;
            hasLoggedPaymentNear = false;
            nextPoseProgressLogTime = 0f;
            nextPaymentProgressLogTime = 0f;
            nextPullOverProgressLogTime = 0f;
            doorOpenAmount = 0f;
            rampOpenAmount = 0f;
            hasSelectedSlidingDoorSide = false;
            boardingTouchConfirmed = false;
            hasLoggedBoardingTouch = false;
            hasLoggedBoardingConfirmationWaitingForRamp = false;
            hasLoggedRampDeployHalfway = false;
            hasLoggedRampDeployReady = false;
            hasLoggedLeftDoorMotionStart = false;
            hasLoggedLeftDoorMotionComplete = false;
            hasLoggedRampInitialStandby = false;
            hasLoggedParkingGroundConform = false;
            riderOnboard = false;
            hasLoggedRiderCabinInside = false;
            hasLoggedRiderAttachment = false;
            baggageQuestionAnswered = false;
            riderHasLuggage = false;
            baggageInstructionActive = false;
            baggageInstructionTimer = 0f;
            baggageYesWasPressed = false;
            baggageNoWasPressed = false;
            doorDecisionConfirmWasPressed = false;
            doorDecisionDeclineWasPressed = false;
            hasLoggedBoardingCreditCardShown = false;
            hasLoggedBoardingCreditCardTouch = false;
            nextBoardingCreditCardMissLogTime = 0f;
            hasAppliedCabinOnboardUi = false;
            hasAppliedCabinSeatedUi = false;
            hasAppliedCabinDestinationUi = false;
            hasPlayedFloorEntryLights = false;
            hasPlayedSeatOutLights = false;
            hasPlayedLuggagePreventerLights = false;
            destinationDropoffRequested = false;
            destinationExitPhase = CocoonDestinationExitPhase.Inactive;
            destinationExitTimer = 0f;
            destinationStopCursor = CocoonRouteCursor.Invalid();
            hasDestinationStopCursor = false;
            destinationRiderDetached = false;
            destinationExitReadyForAReset = false;
            destinationExitResetButtonWasDown = false;
            destinationSeatRestoreStarted = false;
            hasLoggedDestinationFallbackTimer = false;
            hasLoggedDestinationGraphProjectionFailure = false;
            hasLoggedDestinationGraphDistanceFailure = false;
            hasLoggedDestinationDepartureProjectionFailure = false;
            destinationDepartureGraphOnly = false;
            hasLoggedDestinationDepartureGraphHold = false;
            destinationBayExitStep = 0;
            destinationBayExitLeadPosition = Vector3.zero;
            destinationBayExitMergeCursor = CocoonRouteCursor.Invalid();
            hasDestinationBayExitMergeCursor = false;
            lastLoggedDestinationBayExitStep = "";
            luggageExitPoseTargets.Clear();
            luggageExitPoseIndex = 0;
            CocoonLightSequenceController lightController = GetLightSequenceController();
            if (lightController != null)
            {
                lightController.StopAll();
            }
            passengerLuggageStowedInTaxi = false;
            passengerLuggagePlacedAtRest = false;
            ResetPassengerLuggageStorageSequence();
            ResetSeatDownSequence();
            taxiTrafficHardBlocked = false;
            hasLoggedLuggageSupportContactStorageStart = false;
            hasLoggedPassengerLuggageVisible = false;
            IsPreRideTrafficReleased = false;
            riderInsideCabinTimer = 0f;
            nextRiderInsideHoldLogTime = 0f;
            activeDoorPanel = null;
            departureCompletesRide = false;
            ClearDepartureMergePlan();
            ApplyDoor(0f);
            CaptureDoorUiDoorL2FollowPose(true);
            ApplyDoorUiDoorL2FollowPose();
            ApplyLuggageRamp(0f);
            SetPanelActive(false);
            SetBaggageQuestionPanelActive(false);
            SetBoardingCardPromptPanelActive(false);
            SetSitPromptPanelActive(false);
            SetBoardingCreditCardActive(false);
            SetBoardingDoorSurfaceScreen(false, false);
            SetPassengerLuggageActive(false);
            ApplyCabinDefaultUiState();
            if (ShouldSuppressHeadLockedFlowUi())
            {
                StartDefaultLuggageFlowWithoutHeadLockedUi();
            }
            SetGuidanceActive(false);
            SetPaymentReaderActive(false);
            SetPickupProjectionActive(false);
            SetRiderLocomotionEnabled(true);
            HidePickupBayVisuals();
            taxiGraphCursor = CocoonRouteCursor.Invalid();
            bool resetGraphBound = EnsureRoadGraphBinding();
            EnsureTrafficParticipant();
            if (trafficParticipant != null)
            {
                if (resetGraphBound && roadGraph != null && taxiGraphCursor.IsValid)
                {
                    trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot != null ? taxiRoot : transform, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
                    trafficParticipant.SetGraphCursor(taxiGraphCursor);
                }
                else
                {
                    trafficParticipant.Configure(cruisePath, taxiRoot != null ? taxiRoot : transform, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), cruiseTargetIndex);
                }

                trafficParticipant.SetBlocksTraffic(true);
            }

            EnterState(CocoonTaxiState.Availability);
        }

        private void CaptureAuthoredTaxiSpawnTransform()
        {
            if (taxiRoot == null || hasAuthoredTaxiSpawnTransform)
            {
                return;
            }

            authoredTaxiSpawnPosition = taxiRoot.position;
            authoredTaxiSpawnRotation = taxiRoot.rotation;
            hasAuthoredTaxiSpawnTransform = true;
        }

        private void CaptureAuthoredRiderRootTransform()
        {
            if (hasAuthoredRiderRootTransform)
            {
                return;
            }

            Transform rig = ResolveRiderRootTransform();
            if (rig == null)
            {
                return;
            }

            authoredRiderRootParent = rig.parent;
            authoredRiderRootLocalPosition = rig.localPosition;
            authoredRiderRootLocalRotation = rig.localRotation;
            authoredRiderRootLocalScale = rig.localScale;
            authoredRiderRootWorldPosition = rig.position;
            authoredRiderRootWorldRotation = rig.rotation;
            hasAuthoredRiderRootTransform = true;
        }

        private void RestoreAuthoredRiderRootTransform()
        {
            if (!hasAuthoredRiderRootTransform)
            {
                return;
            }

            Transform rig = ResolveRiderRootTransform();
            if (rig == null)
            {
                return;
            }

            rig.SetParent(authoredRiderRootParent, false);
            rig.localScale = authoredRiderRootLocalScale;
            if (authoredRiderRootParent != null)
            {
                rig.localPosition = authoredRiderRootLocalPosition;
                rig.localRotation = authoredRiderRootLocalRotation;
            }
            else
            {
                rig.SetPositionAndRotation(authoredRiderRootWorldPosition, authoredRiderRootWorldRotation);
            }

            riderOriginalParent = null;
            hasRiderOriginalParent = false;
        }

        private void SnapTaxiRootToGround(string reason)
        {
            if (taxiRoot == null || !TryGetTaxiGroundContactBounds(out Bounds contactBounds))
            {
                return;
            }

            float groundY = ResolveVehicleGroundY(contactBounds.center);
            float correctionY = groundY - contactBounds.min.y;
            if (Mathf.Abs(correctionY) <= 0.0005f)
            {
                return;
            }

            taxiRoot.position += Vector3.up * correctionY;
            if (!hasLoggedTaxiGroundSnap)
            {
                hasLoggedTaxiGroundSnap = true;
                CocoonDebugLog.Info(
                    "Taxi",
                    "Taxi ground snap " + reason + ": correctionY=" + correctionY.ToString("0.###") +
                    ", groundY=" + groundY.ToString("0.###") +
                    ", contactBottom=" + contactBounds.min.y.ToString("0.###") + ".",
                    this);
            }
        }

        private bool TryGetTaxiGroundContactBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            EncapsulateRendererBounds(FindExactNamedDescendant(searchRoot, Wheel1Name), ref bounds, ref hasBounds);
            EncapsulateRendererBounds(FindExactNamedDescendant(searchRoot, Wheel2Name), ref bounds, ref hasBounds);
            EncapsulateRendererBounds(FindExactNamedDescendant(searchRoot, Wheel3Name), ref bounds, ref hasBounds);
            EncapsulateRendererBounds(FindExactNamedDescendant(searchRoot, Wheel4Name), ref bounds, ref hasBounds);
            return hasBounds || TryGetRendererBounds(searchRoot, out bounds);
        }

        private static float ResolveVehicleGroundY(Vector3 referencePosition)
        {
            Vector3 origin = referencePosition + Vector3.up * 0.6f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return 0f;
        }

        private void ConformTaxiToHighestParkingSurface(string reason, bool forceFinalPose = false)
        {
            if (taxiRoot == null || !TryGetParkingWheelSurfaceCorrection(out float correctionY, out float surfaceY, out string wheelName))
            {
                return;
            }

            if (correctionY < 0f)
            {
                correctionY = Mathf.Max(correctionY, -ParkingGroundMaxDownCorrection);
            }

            if (Mathf.Abs(correctionY) <= 0.0005f)
            {
                return;
            }

            taxiRoot.position += Vector3.up * correctionY;
            if (forceFinalPose)
            {
                parkingFinalPosition.y = taxiRoot.position.y;
            }

            if (!hasLoggedParkingGroundConform)
            {
                hasLoggedParkingGroundConform = true;
                CocoonDebugLog.Info(
                    "Taxi",
                    "Parking ground conform: reason=" + reason +
                    ", maxSurfaceY=" + surfaceY.ToString("0.###") +
                    ", correctionY=" + correctionY.ToString("0.###") +
                    ", wheel=" + wheelName + ".",
                    this);
            }
        }

        private bool TryGetParkingWheelSurfaceCorrection(out float correctionY, out float surfaceY, out string wheelName)
        {
            correctionY = float.NegativeInfinity;
            surfaceY = 0f;
            wheelName = "";
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            bool hasSample = false;
            SampleParkingWheelSurface(searchRoot, Wheel1Name, ref correctionY, ref surfaceY, ref wheelName, ref hasSample);
            SampleParkingWheelSurface(searchRoot, Wheel2Name, ref correctionY, ref surfaceY, ref wheelName, ref hasSample);
            SampleParkingWheelSurface(searchRoot, Wheel3Name, ref correctionY, ref surfaceY, ref wheelName, ref hasSample);
            SampleParkingWheelSurface(searchRoot, Wheel4Name, ref correctionY, ref surfaceY, ref wheelName, ref hasSample);
            return hasSample;
        }

        private void SampleParkingWheelSurface(
            Transform searchRoot,
            string targetWheelName,
            ref float bestCorrectionY,
            ref float bestSurfaceY,
            ref string bestWheelName,
            ref bool hasSample)
        {
            Transform wheel = FindExactNamedDescendant(searchRoot, targetWheelName);
            if (!TryGetRendererBounds(wheel, out Bounds wheelBounds) ||
                !TryRaycastParkingSurface(wheelBounds, out float sampledSurfaceY))
            {
                return;
            }

            float correctionY = sampledSurfaceY - wheelBounds.min.y;
            if (!hasSample || correctionY > bestCorrectionY)
            {
                hasSample = true;
                bestCorrectionY = correctionY;
                bestSurfaceY = sampledSurfaceY;
                bestWheelName = targetWheelName;
            }
        }

        private bool TryRaycastParkingSurface(Bounds wheelBounds, out float surfaceY)
        {
            surfaceY = 0f;
            Vector3 origin = wheelBounds.center + Vector3.up * Mathf.Max(0.05f, ParkingGroundRaycastHeight);
            float distance = Mathf.Max(0.1f, ParkingGroundRaycastHeight + ParkingGroundRaycastDistance);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || IsTaxiOwnedTransform(hitCollider.transform))
                {
                    continue;
                }

                surfaceY = hits[i].point.y;
                return true;
            }

            return false;
        }

        private bool IsTaxiOwnedTransform(Transform candidate)
        {
            return candidate != null && taxiRoot != null && (candidate == taxiRoot || candidate.IsChildOf(taxiRoot));
        }

        private int ResolveCruiseTargetIndexFromTaxiPosition()
        {
            if (cruisePath == null || cruisePath.Count < 2 || taxiRoot == null)
            {
                return Mathf.Max(1, cruiseTargetIndex);
            }

            return cruisePath.FindNearestSegmentTargetIndex(taxiRoot.position);
        }

        public void ConfirmRide()
        {
            if (state != CocoonTaxiState.ConfirmRide)
            {
                CocoonDebugLog.Warn("DoorUI", "Confirm Ride rejected in state " + state + ".", this);
                SetStatus("Enter the pickup flow in order: confirm ride first.");
                return;
            }

            if (!boardingTouchConfirmed)
            {
                CocoonDebugLog.Warn("DoorUI", "Boarding confirmation ignored: tap the right-hand card on DOORUI first.", this);
                SetStatus("Please tap your card on the DOORUI prepay zone.");
                return;
            }

            CocoonDebugLog.Info("DoorUI", "Boarding prepay confirmed; waiting for ramp if needed.", this);
            departureCompletesRide = true;
            SetBoardingCardPromptPanelActive(false);
            SetBoardingCreditCardActive(false);
            SetBoardingDoorSurfaceScreen(false, false);
            SetPanelActive(false);
            SetPickupProjectionActive(false);
            if (rampOpenAmount >= 0.995f)
            {
                CocoonDebugLog.Info("DoorUI", "Ramp already deployed; opening left door after card prepay.", this);
                EnterState(CocoonTaxiState.DoorOpening);
            }
            else if (!hasLoggedBoardingConfirmationWaitingForRamp)
            {
                hasLoggedBoardingConfirmationWaitingForRamp = true;
                CocoonDebugLog.Info("DoorUI", "Card prepay confirmed; cached while ramp deploys.", this);
            }
        }

        public void DeclineRide()
        {
            if (state != CocoonTaxiState.ConfirmRide)
            {
                CocoonDebugLog.Warn("DoorUI", "Leave rejected in state " + state + ".", this);
                return;
            }

            CocoonDebugLog.Info("DoorUI", "Rider declined boarding; departing pickup point.", this);
            departureCompletesRide = false;
            doorOpenAmount = 0f;
            rampOpenAmount = 0f;
            ApplyDoor(0f);
            ApplyLuggageRamp(0f);
            SetBoardingCardPromptPanelActive(false);
            SetBoardingCreditCardActive(false);
            SetBoardingDoorSurfaceScreen(false, false);
            SetPanelActive(false);
            EnterState(CocoonTaxiState.Departing);
        }

        public void SelectDestination(string destination)
        {
            if (state != CocoonTaxiState.ChooseDestination || string.IsNullOrEmpty(destination))
            {
                CocoonDebugLog.Warn("DoorUI", "Destination '" + destination + "' rejected in state " + state + ".", this);
                SetStatus("Destination unlocks after confirming this Cocoon.");
                return;
            }

            selectedDestination = destination;
            SetDestinationLine();

            CocoonDebugLog.Info("DoorUI", "Destination selected: " + selectedDestination + ".", this);
            EnterState(CocoonTaxiState.ChooseRideMode);
        }

        public void SelectRideMode(string rideMode)
        {
            if (state != CocoonTaxiState.ChooseRideMode || string.IsNullOrEmpty(rideMode))
            {
                CocoonDebugLog.Warn("DoorUI", "Ride mode '" + rideMode + "' rejected in state " + state + ".", this);
                SetStatus("Choose a destination before selecting ride mode.");
                return;
            }

            selectedRideMode = rideMode;
            SetDestinationLine();

            CocoonDebugLog.Info("DoorUI", "Ride mode selected: " + selectedRideMode + ".", this);
            EnterState(CocoonTaxiState.ContactlessPayment);
        }

        public void ConfirmPayment()
        {
            if (state == CocoonTaxiState.ContactlessPayment)
            {
                CocoonDebugLog.Info("Payment", "Payment confirmed by explicit UI action.", this);
                EnterState(CocoonTaxiState.PaymentConfirmed);
            }
            else
            {
                CocoonDebugLog.Warn("Payment", "ConfirmPayment ignored in state " + state + ".", this);
            }
        }

        public void SelectBaggagePreference(bool hasLuggage)
        {
            if (riderOnboard)
            {
                return;
            }

            baggageQuestionAnswered = true;
            riderHasLuggage = hasLuggage;
            SetPassengerLuggageActive(hasLuggage);
            SetBaggageQuestionPanelActive(false);
            CocoonDebugLog.Info("Luggage", "Baggage preference selected: " + (hasLuggage ? "Yes" : "No") + ".", this);

            if (hasLuggage)
            {
                baggageInstructionActive = true;
                baggageInstructionTimer = Mathf.Max(0.1f, luggageInstructionSeconds);
                IsPreRideTrafficReleased = false;
                SetBaggageInstructionPanelActive(!ShouldSuppressHeadLockedFlowUi());
                return;
            }

            ReleasePreRideTraffic();
            if (!ShouldSuppressRiderInstructionPanel())
            {
                SetInstructionPanelActive(true);
            }
        }

        private void StartDefaultLuggageFlowWithoutHeadLockedUi()
        {
            if (riderOnboard)
            {
                return;
            }

            baggageQuestionAnswered = true;
            riderHasLuggage = true;
            baggageInstructionActive = true;
            baggageInstructionTimer = Mathf.Max(0.1f, luggageInstructionSeconds);
            IsPreRideTrafficReleased = false;
            SetBaggageQuestionPanelActive(false);
            SetBaggageInstructionPanelActive(false);
            SetInstructionPanelActive(false);
            SetPassengerLuggageActive(true);
            CocoonDebugLog.Info(
                "Luggage",
                "Head-locked flow UI suppressed; defaulting to luggage=yes and preserving the " +
                baggageInstructionTimer.ToString("0.0") + "s pre-ride gate.",
                this);
        }

        private void ReleasePreRideTraffic()
        {
            if (IsPreRideTrafficReleased)
            {
                return;
            }

            IsPreRideTrafficReleased = true;
            CocoonDebugLog.Info("Flow", "Pre-ride start gate released; taxi and traffic may move.", this);
        }

        private void UpdateBaggageQuestionInput()
        {
            if (!IsBaggageQuestionInteractive)
            {
                return;
            }

            bool yesPressed = ReadRightControllerButton(XRCommonUsages.primaryButton) ||
                              (Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame);
            bool noPressed = ReadRightControllerButton(XRCommonUsages.secondaryButton) ||
                             (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame);

            if (yesPressed && !baggageYesWasPressed)
            {
                SelectBaggagePreference(true);
            }
            else if (noPressed && !baggageNoWasPressed)
            {
                SelectBaggagePreference(false);
            }

            baggageYesWasPressed = yesPressed;
            baggageNoWasPressed = noPressed;
        }

        private void UpdateDoorDecisionInput()
        {
            if (state != CocoonTaxiState.ConfirmRide)
            {
                return;
            }

            bool confirmPressed = ReadRightControllerButton(XRCommonUsages.primaryButton) ||
                                  (Keyboard.current != null &&
                                   (Keyboard.current.aKey.wasPressedThisFrame ||
                                    Keyboard.current.enterKey.wasPressedThisFrame ||
                                    Keyboard.current.cKey.wasPressedThisFrame));
            bool declinePressed = ReadRightControllerButton(XRCommonUsages.secondaryButton) ||
                                  (Keyboard.current != null &&
                                   (Keyboard.current.bKey.wasPressedThisFrame ||
                                    Keyboard.current.escapeKey.wasPressedThisFrame ||
                                    Keyboard.current.lKey.wasPressedThisFrame));

            if (confirmPressed && !doorDecisionConfirmWasPressed)
            {
                CocoonDebugLog.Info("DoorUI", "Right A pressed for boarding confirmation.", this);
                ConfirmRide();
            }
            else if (declinePressed && !doorDecisionDeclineWasPressed)
            {
                CocoonDebugLog.Info("DoorUI", "Right B pressed to decline boarding.", this);
                DeclineRide();
            }

            doorDecisionConfirmWasPressed = confirmPressed;
            doorDecisionDeclineWasPressed = declinePressed;
        }

        private bool ReadRightControllerButton(InputFeatureUsage<bool> usage)
        {
            baggageChoiceDevices.Clear();
            AddBaggageChoiceDevice(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));

            List<XRInputDevice> fallbackDevices = new List<XRInputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, fallbackDevices);
            for (int i = 0; i < fallbackDevices.Count; i++)
            {
                AddBaggageChoiceDevice(fallbackDevices[i]);
            }

            for (int i = 0; i < baggageChoiceDevices.Count; i++)
            {
                if (baggageChoiceDevices[i].TryGetFeatureValue(usage, out bool value) && value)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryReadRightControllerPrimary2DAxis(out Vector2 axis)
        {
            axis = Vector2.zero;
            baggageChoiceDevices.Clear();
            AddBaggageChoiceDevice(InputDevices.GetDeviceAtXRNode(XRNode.RightHand));

            List<XRInputDevice> fallbackDevices = new List<XRInputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, fallbackDevices);
            for (int i = 0; i < fallbackDevices.Count; i++)
            {
                AddBaggageChoiceDevice(fallbackDevices[i]);
            }

            for (int i = 0; i < baggageChoiceDevices.Count; i++)
            {
                if (baggageChoiceDevices[i].TryGetFeatureValue(XRCommonUsages.primary2DAxis, out axis))
                {
                    return true;
                }
            }

            axis = Vector2.zero;
            return false;
        }

        private void AddBaggageChoiceDevice(XRInputDevice device)
        {
            if (device.isValid && !baggageChoiceDevices.Contains(device))
            {
                baggageChoiceDevices.Add(device);
            }
        }

        private void RunAvailability()
        {
            if (!baggageQuestionAnswered)
            {
                if (ShouldSuppressHeadLockedFlowUi())
                {
                    StartDefaultLuggageFlowWithoutHeadLockedUi();
                }

                UpdateBaggageQuestionInput();
                return;
            }

            if (baggageInstructionActive)
            {
                RunBaggageInstruction();
                return;
            }

            ReleasePreRideTraffic();
            MoveAlongCruisePath(GetCruiseMoveSpeed());

            if (raiseHandDetector != null && raiseHandDetector.IsHailDetected)
            {
                CocoonDebugLog.Info("Hail", "Hail detected after 2s pose. facing=" + raiseHandDetector.IsFacingTarget + ", range=" + raiseHandDetector.IsTargetInRange + ", approach=" + raiseHandDetector.IsTaxiApproachingRider + ", distance=" + raiseHandDetector.DisplayDistance.ToString("0.0") + "m, taxiDot=" + raiseHandDetector.TaxiApproachDot.ToString("0.00") + ", raised=" + raiseHandDetector.IsRaised + ", wave=" + raiseHandDetector.HasRecentWave + ".", this);
                EnterState(CocoonTaxiState.HailDetected);
            }
        }

        private void RunBaggageInstruction()
        {
            baggageInstructionTimer -= Time.deltaTime;
            SetBaggageInstructionPanelActive(true);
            SetPassengerLuggageActive(true);

            if (baggageInstructionTimer > 0f)
            {
                return;
            }

            baggageInstructionActive = false;
            SetBaggageInstructionPanelActive(false);
            ReleasePreRideTraffic();
            if (!ShouldSuppressRiderInstructionPanel())
            {
                SetInstructionPanelActive(true);
            }
        }

        private void RunHailDetected()
        {
            stateTimer -= Time.deltaTime;
            MoveAlongCruisePath(GetHailMoveSpeed());
            SetTimer("I see you");

            if (stateTimer <= 0f)
            {
                EnterState(CocoonTaxiState.AwaitPoseConfirm);
            }
        }

        private void RunAwaitPoseConfirm()
        {
            stateTimer -= Time.deltaTime;
            MoveAlongCruisePath(GetHailMoveSpeed());

            bool poseHeld = raiseHandDetector != null && raiseHandDetector.IsHailingPoseActive;
            poseConfirmTimer = poseHeld ? poseConfirmTimer + Time.deltaTime : 0f;
            float remainingHold = Mathf.Max(0f, poseConfirmSeconds - poseConfirmTimer);
            if (Time.time >= nextPoseProgressLogTime)
            {
                CocoonDebugLog.Info("Hail", "Pose confirm progress " + poseConfirmTimer.ToString("0.00") + "/" + poseConfirmSeconds.ToString("0.00") + "s, poseHeld=" + poseHeld + ", timeout=" + stateTimer.ToString("0.0") + "s.", this);
                nextPoseProgressLogTime = Time.time + 0.75f;
            }

            if (poseConfirmTimer >= poseConfirmSeconds)
            {
                CocoonDebugLog.Info("Hail", "Pose confirmation complete.", this);
                EnterState(CocoonTaxiState.SafePullOver);
                return;
            }

            SetTimer(Mathf.CeilToInt(stateTimer) + "s window | hold " + remainingHold.ToString("0.0") + "s");
            UpdateKeepCountdownMessage(remainingHold);

            if (stateTimer <= 0f)
            {
                CocoonDebugLog.Warn("Hail", "Confirmation timed out. Returning to availability.", this);
                SetStatus("Request timed out. Wave again when the next available Cocoon passes.");
                EnterState(CocoonTaxiState.Availability);
            }
        }

        private void RunSafePullOver()
        {
            if (selectedPullOverPoint == null)
            {
                SelectNextPullOverPoint();
            }

            if (selectedPullOverPoint == null)
            {
                CocoonDebugLog.Warn("Taxi", "No pickup bay available; returning to cruise.", this);
                EnterState(CocoonTaxiState.Availability);
                return;
            }

            if (pendingTurnaroundToOppositeLane && selectedPullOverCruisePath != null && selectedPullOverCruisePath != cruisePath && cruisePath != null && cruisePath.Count > 0 && turnaroundPathIndex >= 0)
            {
                CocoonDebugLog.Warn("Taxi", "Legacy opposite-lane pickup state discarded. Taxi is forward-only: no reverse, no U-turn, no lane shortcut.", this);
                selectedPullOverCruisePath = cruisePath;
                pendingTurnaroundToOppositeLane = false;
                turnaroundPathIndex = -1;
                selectedPullOverDirectPullIn = false;
                selectedPullOverEntryPathIndex = GetPullOverEntryPathIndex(cruisePath, selectedPullOverPathIndex, NormalizePathIndex(cruiseTargetIndex));
                reachedSelectedPullOverEntry = selectedPullOverEntryPathIndex < 0 || cruisePath == null || cruisePath.Count == 0;
            }

            if (useRoadGraph && selectedGraphBayIndex >= 0 && EnsureRoadGraphBinding() && !reachedSelectedPullOverEntry)
            {
                if (parkingPhase == CocoonPullOverParkingPhase.None)
                {
                    BuildParkingPlan();
                }

                if (parkingPhase != CocoonPullOverParkingPhase.None &&
                    parkingPhase != CocoonPullOverParkingPhase.ApproachLaneEntry)
                {
                    reachedSelectedPullOverEntry = true;
                    CocoonDebugLog.Info("Taxi", "Frozen parking plan starts at " + parkingPhase +
                        "; graph entry travel skipped so taxi does not loop back or reverse.", this);
                }
            }

            if (useRoadGraph && selectedGraphBayIndex >= 0 && EnsureRoadGraphBinding() && !reachedSelectedPullOverEntry)
            {
                EnsureSelectedGraphParkingEntryCursor();
                float graphDistance = 0f;
                bool hasEntryDistance = hasSelectedGraphParkingEntryCursor &&
                    roadGraph.TryGetForwardDistance(taxiGraphCursor, selectedGraphParkingEntryCursor, out graphDistance, Mathf.Max(16, roadGraph.EdgeCount + 8));
                if (hasEntryDistance)
                {
                    selectedGraphParkingEntryForwardDistance = graphDistance;
                    if (graphDistance > Mathf.Max(0.18f * GetExperienceScale(), 0.015f))
                    {
                        MoveAlongCruisePath(GetPullOverMoveSpeed());
                        SetTimer("Bay ahead");
                        return;
                    }
                }

                reachedSelectedPullOverEntry = true;
                CocoonDebugLog.Info("Taxi", "Reached ROADMAP graph frozen parking entry for bay index " + selectedGraphBayIndex +
                    " at edge " + (hasSelectedGraphParkingEntryCursor ? selectedGraphParkingEntryCursor.EdgeId : selectedGraphBayCursor.EdgeId) +
                    ", remaining=" + GetDisplayMeters(hasEntryDistance ? selectedGraphParkingEntryForwardDistance : 0f).ToString("0.00") +
                    "m. Parking plan will continue forward without reverse/U-turn.", this);
            }

            if (!reachedSelectedPullOverEntry && cruisePath != null && cruisePath.Count > 0 && selectedPullOverEntryPathIndex >= 0)
            {
                int targetIndex = NormalizePathIndex(cruiseTargetIndex);
                LogForwardRouteProgress("Pickup entry", selectedPullOverEntryPathIndex);
                bool reachedWaypoint = MoveAlongCruisePath(GetPullOverMoveSpeed());
                SetTimer("Bay ahead");

                float entryDistanceMeters;
                bool nearEntry = IsTaxiNearPathWaypoint(cruisePath, selectedPullOverEntryPathIndex, TurnaroundImmediateArrivalDistance, out entryDistanceMeters);
                if ((targetIndex == selectedPullOverEntryPathIndex && reachedWaypoint) || (targetIndex == selectedPullOverEntryPathIndex && nearEntry))
                {
                    reachedSelectedPullOverEntry = true;
                    CocoonDebugLog.Info("Taxi", "Reached forward-route parking entry waypoint " + selectedPullOverEntryPathIndex +
                        " for bay index " + selectedPullOverPathIndex +
                        " at distance " + entryDistanceMeters.ToString("0.00") + "m.", this);
                }

                return;
            }

            if (parkingPhase == CocoonPullOverParkingPhase.None)
            {
                BuildParkingPlan();
            }

            KeepTaxiBlockingAtPickupEntry();
            DrawParkingDebugPath();
            switch (parkingPhase)
            {
                case CocoonPullOverParkingPhase.ApproachLaneEntry:
                    SetTimer("Line up");
                    if (MoveTaxiTowardsParkingPoint(parkingLaneEntryPosition, GetParkingMoveSpeed(1f, MinimumParkingLineupSpeed), false))
                    {
                        parkingPhase = CocoonPullOverParkingPhase.DiagonalIntoBay;
                        CocoonDebugLog.Info("Taxi", "Parking line-up complete; entering diagonal bay segment.", this);
                    }

                    break;
                case CocoonPullOverParkingPhase.DiagonalIntoBay:
                    SetTimer("Enter bay");
                    if (MoveTaxiTowardsParkingPoint(parkingDiagonalPosition, GetParkingMoveSpeed(0.9f, MinimumParkingDiagonalSpeed), false))
                    {
                        parkingPhase = CocoonPullOverParkingPhase.AlignWithBay;
                        CocoonDebugLog.Info("Taxi", "Diagonal parking segment complete; aligning inside pickup bay.", this);
                    }

                    break;
                case CocoonPullOverParkingPhase.AlignWithBay:
                    SetTimer("Align");
                    if (MoveTaxiTowardsParkingPoint(parkingAlignPosition, GetParkingMoveSpeed(0.78f, MinimumParkingAlignSpeed), false))
                    {
                        parkingPhase = CocoonPullOverParkingPhase.SettleAtBay;
                        CocoonDebugLog.Info("Taxi", "Bay alignment segment complete; settling exactly on pickup anchor.", this);
                    }

                    break;
                case CocoonPullOverParkingPhase.SettleAtBay:
                    SetTimer("Arrive");
                    if (MoveTaxiTowardsParkingPoint(parkingFinalPosition, GetParkingMoveSpeed(0.62f, MinimumParkingSettleSpeed), true))
                    {
                        if (taxiRoot != null)
                        {
                            taxiRoot.position = parkingFinalPosition;
                            taxiRoot.rotation = parkingFinalRotation;
                            ConformTaxiToHighestParkingSurface("final", true);
                            parkingFinalPosition = taxiRoot.position;
                        }

                        if (trafficParticipant != null)
                        {
                            KeepTaxiBlockingAtPickupEntry();
                        }

                        SetPickupProjectionActive(false);
                        CocoonDebugLog.Info("Taxi", "Taxi completed pickup bay parking in " + selectedPullOverPoint.name + " exactly at " + FormatPosition(parkingFinalPosition) + ".", this);
                        EnterState(CocoonTaxiState.ReachPickupPoint);
                    }

                    break;
            }
        }

        private void RunReachPickupPoint()
        {
            KeepTaxiBlockingAtPickupEntry();
            SelectSlidingDoorSideForBoarding();
            SetGuidanceActive(false);
            SetPickupProjectionActive(false);
            CocoonDebugLog.Info("Pickup", "Taxi parked; deploying ramp and waiting for right-hand card prepay tap.", this);
            EnterState(CocoonTaxiState.ConfirmRide);
        }

        private void RunTouchBoardingConfirmation()
        {
            KeepTaxiBlockingAtPickupEntry();
            stateTimer -= Time.deltaTime;
            float elapsed = Mathf.Max(0f, boardingTimeoutSeconds - stateTimer);
            SetPanelActive(false);
            SetBoardingCardPromptPanelActive(!boardingTouchConfirmed && elapsed <= boardingPromptVisibleSeconds);
            SetBoardingCreditCardActive(!boardingTouchConfirmed);
            SetBoardingDoorSurfaceScreen(true, !boardingTouchConfirmed);
            SetGuidanceActive(false);
            SetPickupProjectionActive(false);
            SetPaymentReaderActive(false);
            ApplyDoor(0f);

            float previousRampOpenAmount = rampOpenAmount;
            rampOpenAmount = Mathf.MoveTowards(rampOpenAmount, 1f, Time.deltaTime * GetBoardingDoorOpenSpeed());
            ApplyLuggageRamp(rampOpenAmount);
            LogRampDeployMilestones(previousRampOpenAmount, rampOpenAmount);

            if (!boardingTouchConfirmed && IsBoardingTouchDetected())
            {
                boardingTouchConfirmed = true;
                SetBoardingCardPromptPanelActive(false);
                SetBoardingCreditCardActive(false);
                SetBoardingDoorSurfaceScreen(false, false);
                if (!hasLoggedBoardingTouch)
                {
                    hasLoggedBoardingTouch = true;
                    CocoonDebugLog.Info("DoorUI", "Credit card touched DOORUI prepay zone.", this);
                }

                if (rampOpenAmount < 0.995f && !hasLoggedBoardingConfirmationWaitingForRamp)
                {
                    hasLoggedBoardingConfirmationWaitingForRamp = true;
                    CocoonDebugLog.Info("DoorUI", "Card prepay confirmed; cached while ramp deploys.", this);
                }
            }

            bool rampReady = rampOpenAmount >= 0.995f;
            SetTimer((rampReady ? "Tap card" : "Ramp deploying") + " | " + Mathf.CeilToInt(Mathf.Max(0f, stateTimer)) + "s");
            SetStatus(boardingTouchConfirmed
                ? (rampReady ? "Boarding confirmed. Door opening." : "Boarding confirmed. Waiting for ramp.")
                : "Tap your right-hand card on the DOORUI prepay zone to open the door.");

            if (boardingTouchConfirmed && rampReady)
            {
                departureCompletesRide = true;
                CocoonDebugLog.Info("DoorUI", "Ramp deployed and card prepay confirmed; opening left door.", this);
                EnterState(CocoonTaxiState.DoorOpening);
                return;
            }

            if (stateTimer <= 0f)
            {
                CocoonDebugLog.Warn("DoorUI", "Boarding card tap timed out; departing pickup point.", this);
                DeclineRide();
            }
        }

        private void RunDoorStep(string timer, string status)
        {
            KeepTaxiBlockingAtPickupEntry();
            stateTimer -= Time.deltaTime;
            SetPanelActive(true);
            SetGuidanceActive(false);
            SetPickupProjectionActive(false);
            SetTimer(timer + " | " + Mathf.CeilToInt(stateTimer) + "s");
            SetStatus(status);

            if (state == CocoonTaxiState.ConfirmRide)
            {
                UpdateDoorDecisionInput();
                if (state != CocoonTaxiState.ConfirmRide)
                {
                    return;
                }
            }

            if (stateTimer <= 0f)
            {
                if (state == CocoonTaxiState.ConfirmRide)
                {
                    CocoonDebugLog.Warn("DoorUI", "Door decision timed out; leaving pickup point.", this);
                    DeclineRide();
                }
                else
                {
                    CocoonDebugLog.Warn("DoorUI", "Door onboarding timed out in state " + state + "; resetting.", this);
                    ResetExperience();
                }
            }
        }

        private void RunContactlessPayment()
        {
            KeepTaxiBlockingAtPickupEntry();
            stateTimer -= Time.deltaTime;
            SetPanelActive(true);
            SetGuidanceActive(false);
            SetPaymentReaderActive(true);

            bool nearReader = rightHand != null && paymentReader != null && Vector3.Distance(rightHand.position, paymentReader.position) <= paymentReaderRadius;
            if (!hasLoggedPaymentNear || lastLoggedPaymentNear != nearReader)
            {
                CocoonDebugLog.Info("Payment", "Reader proximity " + (nearReader ? "entered" : "left") + ". hold=" + paymentHoldTimer.ToString("0.00") + "s.", this);
                hasLoggedPaymentNear = true;
                lastLoggedPaymentNear = nearReader;
            }

            paymentHoldTimer = nearReader ? paymentHoldTimer + Time.deltaTime : 0f;
            float progress = Mathf.Clamp01(paymentHoldTimer / Mathf.Max(0.01f, paymentHoldSeconds));
            SetPaymentReaderProgress(progress);
            SetTimer("Tap reader " + Mathf.RoundToInt(progress * 100f) + "%");
            SetStatus("Hold the right controller near the glowing reader for contactless payment.");
            SetInstruction("No button needed. Keep the controller close until the reader turns white.");
            if (Time.time >= nextPaymentProgressLogTime)
            {
                CocoonDebugLog.Info("Payment", "Payment progress " + Mathf.RoundToInt(progress * 100f) + "%.", this);
                nextPaymentProgressLogTime = Time.time + 0.5f;
            }

            if (paymentHoldTimer >= paymentHoldSeconds)
            {
                CocoonDebugLog.Info("Payment", "Contactless payment hold complete.", this);
                EnterState(CocoonTaxiState.PaymentConfirmed);
                return;
            }

            if (stateTimer <= 0f)
            {
                CocoonDebugLog.Warn("Payment", "Payment timed out; resetting experience.", this);
                ResetExperience();
            }
        }

        private void RunPaymentConfirmed()
        {
            RunDoorOpening();
        }

        private void RunDoorOpening()
        {
            KeepTaxiBlockingAtPickupEntry();
            rampOpenAmount = 1f;
            ApplyLuggageRamp(1f);
            doorOpenAmount = Mathf.MoveTowards(doorOpenAmount, 1f, Time.deltaTime * GetBoardingDoorOpenSpeed());
            ApplyDoor(doorOpenAmount);
            SetBoardingCardPromptPanelActive(false);
            SetBoardingDoorSurfaceScreen(false, false);
            SetPanelActive(false);
            SetTimer("Door opening");
            SetStatus("Door opening.");

            if (doorOpenAmount >= 1f)
            {
                CocoonDebugLog.Info("Door", "Door open animation complete.", this);
                EnterState(CocoonTaxiState.DoorOpen);
            }
        }

        private void RunDoorOpen()
        {
            KeepTaxiBlockingAtPickupEntry();
            stateTimer -= Time.deltaTime;
            SetPanelActive(false);
            SetBoardingDoorSurfaceScreen(false, false);
            ApplyDoor(1f);
            rampOpenAmount = 1f;
            ApplyLuggageRamp(1f);
            TryStartPassengerLuggageStorageFromSupportContact();

            bool riderFullyInside = IsRiderInTaxiCabinWithLog();
            float requiredInsideSeconds = GetRiderInsideRequiredSeconds();
            if (riderFullyInside)
            {
                SetBoardingDoorSurfaceScreen(false, false);
                if (!riderHasLuggage && seatStoragePhase == CocoonSeatStoragePhase.Inactive)
                {
                    StartPassengerSeatStorageSequence();
                }

                riderInsideCabinTimer = Mathf.Min(requiredInsideSeconds, riderInsideCabinTimer + Time.deltaTime);
                if (Time.time >= nextRiderInsideHoldLogTime)
                {
                    CocoonDebugLog.Info("Door", "Cabin hold " + riderInsideCabinTimer.ToString("0.0") + "/" + requiredInsideSeconds.ToString("0.0") + "s.", this);
                    nextRiderInsideHoldLogTime = Time.time + 1f;
                }
            }
            else
            {
                if (riderInsideCabinTimer > 0.001f)
                {
                    CocoonDebugLog.Info("Door", "Cabin hold reset before completion.", this);
                }

                riderInsideCabinTimer = 0f;
                if (Time.time >= nextRiderInsideHoldLogTime)
                {
                    CocoonDebugLog.Info("Door", "Waiting for rider inside cabin. " + BuildRiderCabinDiagnostic(), this);
                    nextRiderInsideHoldLogTime = Time.time + 1f;
                }
            }

            SetTimer("Inside " + riderInsideCabinTimer.ToString("0.0") + "/" + requiredInsideSeconds.ToString("0.0") + "s | timeout " + Mathf.CeilToInt(Mathf.Max(0f, stateTimer)) + "s");
            SetStatus(riderFullyInside ? "Stay fully inside Cocoon until boarding locks in." : "Step fully inside Cocoon.");

            if (riderInsideCabinTimer >= requiredInsideSeconds)
            {
                riderOnboard = true;
                ApplyCabinOnboardUiState();
                StowPassengerLuggageInTaxi();
                AttachRiderToTaxi();
                CocoonDebugLog.Info("Door", "Rider remained fully inside cabin for " + requiredInsideSeconds.ToString("0.0") + "s; closing door.", this);
                EnterState(CocoonTaxiState.DoorClosing);
                return;
            }

            if (stateTimer <= 0f)
            {
                CocoonDebugLog.Warn("Door", "Boarding timed out without rider inside; keeping taxi parked with door open.", this);
                stateTimer = boardingTimeoutSeconds;
                riderInsideCabinTimer = 0f;
                nextRiderInsideHoldLogTime = Time.time + 1f;
            }
        }

        private void RunDoorClosing()
        {
            KeepTaxiBlockingAtPickupEntry();
            doorOpenAmount = Mathf.MoveTowards(doorOpenAmount, 0f, Time.deltaTime * GetBoardingDoorCloseSpeed());
            rampOpenAmount = Mathf.MoveTowards(rampOpenAmount, 0f, Time.deltaTime * GetBoardingDoorCloseSpeed());
            ApplyDoor(doorOpenAmount);
            ApplyLuggageRamp(rampOpenAmount);
            SetPanelActive(false);
            SetBoardingDoorSurfaceScreen(false, false);
            SetTimer("Door closing");
            SetStatus("Door closing.");

            if (doorOpenAmount <= 0f && rampOpenAmount <= 0f)
            {
                if (!IsPassengerSeatStorageComplete())
                {
                    SetTimer("Moving seat");
                    SetStatus("Preparing seat.");
                    return;
                }

                if (!IsPassengerLuggageStorageComplete())
                {
                    SetTimer("Stowing luggage");
                    SetStatus("Securing luggage.");
                    return;
                }

                if (departureCompletesRide && !riderOnboard)
                {
                    CocoonDebugLog.Warn("Door", "Door closed without rider onboard; returning to door decision instead of departing.", this);
                    EnterState(CocoonTaxiState.ConfirmRide);
                    return;
                }

                CocoonDebugLog.Info("Door", "Door closed; departing pickup point.", this);
                EnterState(CocoonTaxiState.Departing);
            }
        }

        private void RunDeparting()
        {
            stateTimer -= Time.deltaTime;
            rampOpenAmount = 0f;
            ApplyDoor(0f);
            ApplyLuggageRamp(0f);
            SetPanelActive(false);
            SetBoardingDoorSurfaceScreen(false, false);
            SetPickupProjectionActive(false);
            SetGuidanceActive(false);
            SetPaymentReaderActive(false);
            if (hasDepartureMergeTarget && !reachedDepartureMergeTarget)
            {
                KeepTaxiBlockingDuringDepartureMerge();
                bool reachedMerge = MoveTaxiTowards(departureMergePosition, GetPullOverMoveSpeed(), true);
                if (taxiRoot != null)
                {
                    taxiRoot.rotation = Quaternion.RotateTowards(taxiRoot.rotation, departureMergeRotation, 420f * Time.deltaTime);
                }

                float remaining = taxiRoot != null ? Vector3.Distance(taxiRoot.position, departureMergePosition) : float.MaxValue;
                if (reachedMerge || remaining <= GetScaledArrivalDistance(DepartureMergeArrivalDistance))
                {
                    CompleteDepartureMerge();
                }
            }
            else
            {
                if (destinationDepartureGraphOnly && useRoadGraph && !EnsureRoadGraphBinding())
                {
                    taxiMotionSpeed = 0f;
                    if (!hasLoggedDestinationDepartureGraphHold)
                    {
                        hasLoggedDestinationDepartureGraphHold = true;
                        CocoonDebugLog.Warn("Destination", "Destination departure lost ROADMAP graph binding; taxi is held to avoid straight-line travel through buildings.", this);
                    }
                }
                else
                {
                    MoveAlongCruisePath(GetCruiseMoveSpeed());
                }
            }

            SetTimer("Departing | " + Mathf.CeilToInt(Mathf.Max(0f, stateTimer)) + "s");
            SetStatus(departureCompletesRide ? (riderOnboard ? "Rider onboard. Cocoon is leaving pickup point." : "Ride accepted. Cocoon is leaving pickup point.") : "Cocoon is leaving pickup point.");

            if (stateTimer <= 0f && (!hasDepartureMergeTarget || reachedDepartureMergeTarget))
            {
                departureCompletesRide = false;
                destinationDepartureGraphOnly = false;
                hasLoggedDestinationDepartureGraphHold = false;
                EnterState(CocoonTaxiState.Availability);
            }
        }

        private void EnterState(CocoonTaxiState nextState)
        {
            CocoonTaxiState fromState = state;
            state = nextState;
            if (!hasLoggedState || previousLoggedState != nextState)
            {
                CocoonDebugLog.Info("State", (hasLoggedState ? fromState.ToString() : "Start") + " -> " + nextState + ".", this);
                previousLoggedState = nextState;
                hasLoggedState = true;
            }

            if (IsStationaryTaxiState(state))
            {
                taxiMotionSpeed = 0f;
            }

            switch (state)
            {
                case CocoonTaxiState.Availability:
                    ApplyCabinDefaultUiState();
                    poseConfirmTimer = 0f;
                    paymentHoldTimer = 0f;
                    SetInstructionPanelActive(true);
                    SetBoardingCardPromptPanelActive(false);
                    SetSitPromptPanelActive(false);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPaymentReaderActive(false);
                    SetPickupProjectionActive(false);
                    SetLightBlinking(false);
                    ApplyExteriorScreenLights(availabilityExteriorScreenState, 2.6f, "AVAILABLE");
                    SetWindshieldMessage("WAVE TO RESERVE", Color.white);
                    SetHeadline("COCOON AVAILABLE");
                    SetStatus("Face an approaching Cocoon within 30m.");
                    SetTimer("Cruising");
                    SetInstruction("Face the taxi and hold a raised hand for 2s.");
                    break;
                case CocoonTaxiState.HailDetected:
                    stateTimer = 0f;
                    poseConfirmTimer = 0f;
                    SetInstructionPanelActive(true);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPaymentReaderActive(false);
                    SetPickupProjectionActive(false);
                    ApplyExteriorScreenLights(CocoonTaxiExteriorScreenState.DetectedHold, 3.5f, "DETECTED");
                    SetLightBlinking(true);
                    SetWindshieldMessage("KEEP FOR 3S", new Color(1f, 0.84f, 0.12f));
                    SetHeadline("I SEE YOU");
                    SetStatus("Cocoon saw you. Keep hand up for 3s.");
                    SetInstruction("No buttons. Hold the hail pose.");
                    break;
                case CocoonTaxiState.AwaitPoseConfirm:
                    stateTimer = confirmationTimeoutSeconds;
                    poseConfirmTimer = 0f;
                    SetInstructionPanelActive(true);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPaymentReaderActive(false);
                    SetPickupProjectionActive(false);
                    ApplyExteriorScreenLights(CocoonTaxiExteriorScreenState.DetectedHold, 3.5f, "DETECTED");
                    SetLightBlinking(true);
                    SetWindshieldMessage("KEEP FOR 3S", new Color(1f, 0.84f, 0.12f));
                    SetHeadline("CONFIRM REQUEST");
                    SetStatus("Hold right controller high for 3s.");
                    SetInstruction("Lowering hand restarts. 8s window.");
                    break;
                case CocoonTaxiState.SafePullOver:
                    SetInstructionPanelActive(true);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPaymentReaderActive(false);
                    SetPickupProjectionActive(false);
                    SelectNextPullOverPoint();
                    SetLightBlinking(false);
                    ApplyExteriorScreenLights(CocoonTaxiExteriorScreenState.StoppingFollowMe, 7f, "STOPPING");
                    SetWindshieldMessage("CONFIRMED! STOPPING NOW", new Color(0.35f, 1f, 0.48f));
                    SetHeadline("SAFE PULL-OVER");
                    SetStatus("Confirmed. Cocoon enters pickup bay.");
                    SetTimer("Approaching");
                    SetInstruction("Follow the glowing path.");
                    break;
                case CocoonTaxiState.ReachPickupPoint:
                    SetInstructionPanelActive(true);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPaymentReaderActive(false);
                    SetPickupProjectionActive(false);
                    SetLightBlinking(false);
                    ApplyExteriorScreenLights(CocoonTaxiExteriorScreenState.StoppingFollowMe, 6f, "STOPPING");
                    SetWindshieldMessage("PICKUP POINT", new Color(0.35f, 1f, 0.48f));
                    SetHeadline("REACH PICKUP POINT");
                    SetStatus("Prepay by tapping the card at the boarding zone.");
                    SetTimer("Prepay");
                    SetInstruction("Tap your card on the boarding zone to open the door.");
                    break;
                case CocoonTaxiState.ConfirmRide:
                    stateTimer = boardingTimeoutSeconds;
                    doorDecisionConfirmWasPressed = false;
                    doorDecisionDeclineWasPressed = false;
                    boardingTouchConfirmed = false;
                    hasLoggedBoardingTouch = false;
                    hasLoggedBoardingCreditCardShown = false;
                    hasLoggedBoardingCreditCardTouch = false;
                    nextBoardingCreditCardMissLogTime = 0f;
                    hasLoggedBoardingConfirmationWaitingForRamp = false;
                    hasLoggedRampDeployHalfway = false;
                    hasLoggedRampDeployReady = false;
                    rampOpenAmount = 0f;
                    doorOpenAmount = 0f;
                    SelectSlidingDoorSideForBoarding();
                    ResolveBoardingTouchZone();
                    ResolveLuggageRampReference();
                    SetInstructionPanelActive(false);
                    SetBoardingCardPromptPanelActive(true);
                    SetBoardingCreditCardActive(true);
                    SetBoardingDoorSurfaceScreen(true, true);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPaymentReaderActive(false);
                    SetPickupProjectionActive(false);
                    SetLightBlinking(false);
                    ApplyDoor(0f);
                    CaptureDoorUiDoorL2FollowPose(true);
                    ApplyDoorUiDoorL2FollowPose();
                    ApplyLuggageRamp(0f);
                    ApplyExteriorScreenLights(CocoonTaxiExteriorScreenState.StoppingFollowMe, 6f, "STOPPING");
                    SetWindshieldMessage("PICKUP POINT", new Color(0.35f, 1f, 0.48f));
                    SetHeadline("PREPAY TO OPEN");
                    SetStatus("Ramp deploying. Tap your right-hand card on the DOORUI zone.");
                    SetDestinationLine();
                    SetTimer("Ramp deploying | " + Mathf.CeilToInt(stateTimer) + "s");
                    SetInstruction("Please tap your card to prepay and open the door.");
                    break;
                case CocoonTaxiState.ChooseDestination:
                    SetInstructionPanelActive(false);
                    SetHeadline("CHOOSE DESTINATION");
                    SetStatus("Pick a destination preset.");
                    SetInstruction("Choose Duomo, Central, or Brera.");
                    SetDestinationLine();
                    break;
                case CocoonTaxiState.ChooseRideMode:
                    SetInstructionPanelActive(false);
                    SetHeadline("SOLO OR SHARED");
                    SetStatus("Choose Solo or Shared ride mode.");
                    SetInstruction("Solo direct. Shared lower cost.");
                    SetDestinationLine();
                    break;
                case CocoonTaxiState.ContactlessPayment:
                    paymentHoldTimer = 0f;
                    SetInstructionPanelActive(false);
                    SetPanelActive(true);
                    SetGuidanceActive(false);
                    SetPaymentReaderActive(true);
                    SetLights(new Color(0.95f, 0.86f, 0.38f), 2.5f, "PAYMENT AMBER");
                    SetHeadline("PAY CONTACTLESS");
                    SetStatus("Hold controller near reader.");
                    SetInstruction("No trigger. Pay by proximity.");
                    SetDestinationLine();
                    break;
                case CocoonTaxiState.PaymentConfirmed:
                case CocoonTaxiState.DoorOpening:
                    SetInstructionPanelActive(false);
                    SetBoardingCardPromptPanelActive(false);
                    SetBoardingCreditCardActive(false);
                    SetBoardingDoorSurfaceScreen(false, false);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPickupProjectionActive(false);
                    SetPaymentReaderActive(false);
                    SelectSlidingDoorSideForBoarding();
                    rampOpenAmount = 1f;
                    ApplyLuggageRamp(1f);
                    SetLights(new Color(1f, 1f, 1f), 2.9f, "DOOR OPENING WHITE");
                    SetWindshieldMessage("WELCOME", new Color(0.35f, 1f, 0.48f));
                    SetHeadline("WELCOME");
                    SetStatus("Door opening.");
                    SetInstruction("Board when the door opens.");
                    break;
                case CocoonTaxiState.DoorOpen:
                    stateTimer = boardingTimeoutSeconds;
                    hasLoggedRiderCabinInside = false;
                    riderInsideCabinTimer = 0f;
                    nextRiderInsideHoldLogTime = 0f;
                    SetInstructionPanelActive(false);
                    SetBoardingCardPromptPanelActive(false);
                    SetBoardingCreditCardActive(false);
                    SetBoardingDoorSurfaceScreen(false, false);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPickupProjectionActive(false);
                    SetPaymentReaderActive(false);
                    rampOpenAmount = 1f;
                    ApplyLuggageRamp(1f);
                    SetLights(new Color(1f, 1f, 1f), 3.2f, "WELCOME WHITE");
                    SetHeadline("WELCOME ABOARD");
                    SetStatus("Door open. Ready to board.");
                    SetInstruction("Door will close automatically.");
                    break;
                case CocoonTaxiState.DoorClosing:
                    SetInstructionPanelActive(false);
                    SetBoardingCardPromptPanelActive(false);
                    SetBoardingCreditCardActive(false);
                    SetBoardingDoorSurfaceScreen(false, false);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPickupProjectionActive(false);
                    SetPaymentReaderActive(false);
                    SetLights(new Color(1f, 1f, 1f), 2.4f, "DOOR CLOSING WHITE");
                    SetHeadline("DOOR CLOSING");
                    SetStatus("Door closing.");
                    SetInstruction("Cocoon will leave the pickup point.");
                    break;
                case CocoonTaxiState.Departing:
                    stateTimer = 4f;
                    poseConfirmTimer = 0f;
                    paymentHoldTimer = 0f;
                    hasSelectedSlidingDoorSide = false;
                    boardingTouchConfirmed = false;
                    rampOpenAmount = 0f;
                    ApplyLuggageRamp(0f);
                    SetInstructionPanelActive(false);
                    SetBoardingCardPromptPanelActive(false);
                    SetBoardingCreditCardActive(false);
                    SetBoardingDoorSurfaceScreen(false, false);
                    SetPanelActive(false);
                    SetGuidanceActive(false);
                    SetPickupProjectionActive(false);
                    SetPaymentReaderActive(false);
                    SetLightBlinking(false);
                    ApplyExteriorScreenLights(CocoonTaxiExteriorScreenState.Goodbye, 2.4f, "GOODBYE");
                    SetWindshieldMessage("GOODBYE", Color.white);
                    SetHeadline("COCOON DEPARTING");
                    SetStatus(departureCompletesRide ? (riderOnboard ? "Rider onboard. Cocoon is leaving pickup point." : "Ride accepted. Cocoon is leaving pickup point.") : "Cocoon is leaving pickup point.");
                    SetInstruction("The next Cocoon can be hailed after departure.");
                    PrepareDepartureMergePlan();
                    break;
            }
        }

        private static bool IsStationaryTaxiState(CocoonTaxiState taxiState)
        {
            switch (taxiState)
            {
                case CocoonTaxiState.ReachPickupPoint:
                case CocoonTaxiState.ConfirmRide:
                case CocoonTaxiState.ChooseDestination:
                case CocoonTaxiState.ChooseRideMode:
                case CocoonTaxiState.ContactlessPayment:
                case CocoonTaxiState.PaymentConfirmed:
                case CocoonTaxiState.DoorOpening:
                case CocoonTaxiState.DoorOpen:
                case CocoonTaxiState.DoorClosing:
                    return true;
                default:
                    return false;
            }
        }

        private bool MoveAlongCruisePath(float speed)
        {
            if (useRoadGraph && TryMoveAlongRoadGraph(speed))
            {
                return false;
            }

            if (cruisePath == null || cruisePath.Count < 2)
            {
                bool arrived = MoveTaxiTowards(cruiseEnd, speed);
                if (arrived && taxiRoot != null && cruiseStart != null)
                {
                    taxiRoot.position = cruiseStart.position;
                    taxiRoot.rotation = cruiseStart.rotation;
                }

                return arrived;
            }

            int targetIndex = NormalizePathIndex(cruiseTargetIndex);
            Transform target = cruisePath.GetWaypoint(targetIndex);
            if (target == null)
            {
                return false;
            }

            EnsureTrafficParticipant();
            if (trafficParticipant != null)
            {
                trafficParticipant.SetBlocksTraffic(true);
                trafficParticipant.SetLaneTargetIndex(targetIndex);
            }

            bool reached = MoveTaxiTowards(target.position, speed, true);
            if (reached)
            {
                if (!cruisePath.TryGetNextIndex(targetIndex, out int nextIndex))
                {
                    taxiMotionSpeed = 0f;
                    CocoonDebugLog.Warn("Taxi", "Reached the end of non-loop lane " + GetPathLabel(cruisePath) + "; taxi stopped instead of wrapping through an END gap.", this);
                    return true;
                }

                cruiseTargetIndex = nextIndex;
                if (trafficParticipant != null)
                {
                    trafficParticipant.SetLaneTargetIndex(cruiseTargetIndex);
                }
            }

            return reached;
        }

        private bool TryMoveAlongRoadGraph(float speed)
        {
            if (!EnsureRoadGraphBinding())
            {
                return false;
            }

            EnsureTrafficParticipant();
            if (trafficParticipant != null)
            {
                trafficParticipant.SetBlocksTraffic(true);
                trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
            }

            float experienceScale = GetExperienceScale();
            float requestedWorldSpeed = Mathf.Max(0f, speed) * experienceScale;
            float allowedWorldSpeed = trafficParticipant != null ? trafficParticipant.GetAllowedSpeed(requestedWorldSpeed) : requestedWorldSpeed;
            if (allowedWorldSpeed <= 0.0001f)
            {
                taxiMotionSpeed = 0f;
                if (trafficParticipant != null)
                {
                    trafficParticipant.ReportSpeed(0f);
                }

                return true;
            }

            float desiredSpeed = allowedWorldSpeed / Mathf.Max(experienceScale, 0.0001f);
            float acceleration = desiredSpeed > taxiMotionSpeed ? TaxiAccelerationMetersPerSecond : TaxiBrakingMetersPerSecond;
            taxiMotionSpeed = Mathf.MoveTowards(taxiMotionSpeed, desiredSpeed, acceleration * Time.deltaTime);
            float stepWorldDistance = taxiMotionSpeed * experienceScale * Time.deltaTime;
            CocoonRouteCursor nextCursor = taxiGraphCursor;
            if (!roadGraph.TryAdvance(ref nextCursor, stepWorldDistance, out bool hitDeadEnd) && hitDeadEnd)
            {
                taxiMotionSpeed = 0f;
                if (trafficParticipant != null)
                {
                    trafficParticipant.ReportSpeed(0f);
                }

                CocoonDebugLog.Warn("Taxi", "ROADMAP graph dead end at edge " + taxiGraphCursor.EdgeId + "; taxi stopped instead of reversing or turning through END.", this);
                return true;
            }

            Vector3 before = taxiRoot.position;
            Vector3 nextPosition;
            Vector3 graphForward;
            float turnRadius = Mathf.Max(0f, graphTurnRadiusMeters) * experienceScale;
            if (!roadGraph.EvaluateSmoothedPose(nextCursor, turnRadius, out nextPosition, out graphForward))
            {
                nextPosition = roadGraph.GetPosition(nextCursor);
                graphForward = roadGraph.GetForward(nextCursor.EdgeId);
            }

            nextPosition.y = taxiRoot.position.y;
            if (CocoonTrafficParticipant.HasHardBlockInPath(
                    trafficParticipant,
                    taxiRoot,
                    nextPosition,
                    GetScaledTaxiTrafficLength(),
                    TaxiTrafficHardClearanceMeters * experienceScale,
                    Mathf.Max(GetScaledTaxiTrafficLength(), stepWorldDistance + GetScaledTaxiTrafficLength() * 0.5f),
                    true,
                    out CocoonTrafficParticipant blocker))
            {
                taxiMotionSpeed = 0f;
                if (trafficParticipant != null)
                {
                    trafficParticipant.ReportSpeed(0f);
                }

                if (!taxiTrafficHardBlocked)
                {
                    CocoonDebugLog.Info("Traffic", "Taxi graph path hard-blocked by " + FormatTrafficBlocker(blocker) + ". No overtaking or pass-through allowed.", this);
                    taxiTrafficHardBlocked = true;
                }

                return true;
            }

            if (taxiTrafficHardBlocked)
            {
                CocoonDebugLog.Info("Traffic", "Taxi graph path clear; resuming movement.", this);
                taxiTrafficHardBlocked = false;
            }

            taxiRoot.position = nextPosition;
            if (graphForward.sqrMagnitude > 0.0001f)
            {
                FaceTowards(graphForward, Mathf.Max(1f, graphTurnRateDegreesPerSecond));
            }

            taxiGraphCursor = nextCursor;
            if (trafficParticipant != null)
            {
                trafficParticipant.SetGraphCursor(taxiGraphCursor);
                Vector3 delta = taxiRoot.position - before;
                delta.y = 0f;
                trafficParticipant.ReportSpeed(Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f);
            }

            return true;
        }

        private bool EnsureRoadGraphBinding()
        {
            if (!useRoadGraph)
            {
                return false;
            }

            if (roadGraph == null)
            {
                roadGraph = FindObjectOfType<CocoonRoadGraph>();
            }

            if (roadGraph == null || roadGraph.EdgeCount == 0 || taxiRoot == null)
            {
                return false;
            }

            if ((!taxiGraphCursor.IsValid || roadGraph.GetEdge(taxiGraphCursor.EdgeId) == null) &&
                !roadGraph.TryProjectToGraph(taxiRoot.position, out taxiGraphCursor))
            {
                return false;
            }

            return taxiGraphCursor.IsValid && roadGraph.GetEdge(taxiGraphCursor.EdgeId) != null;
        }

        private void BuildParkingPlan()
        {
            Transform anchor = GetPhysicalPullOverAnchor(selectedPullOverPoint);
            if (anchor == null)
            {
                parkingPhase = CocoonPullOverParkingPhase.None;
                return;
            }

            parkingFinalPosition = anchor.position;
            if (taxiRoot != null)
            {
                parkingFinalPosition.y = taxiRoot.position.y;
            }

            Vector3 forward = ResolveParkingApproachForward(anchor);
            parkingApproachForward = forward;
            parkingFinalRotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 laneCenter = ResolvePullOverLaneCenter(anchor);
            Vector3 laneToBay = parkingFinalPosition - laneCenter;
            float experienceScale = GetExperienceScale();
            laneToBay.y = 0f;
            laneToBay -= forward * Vector3.Dot(laneToBay, forward);
            if (laneToBay.sqrMagnitude < Mathf.Pow(0.5f * experienceScale, 2f))
            {
                Vector3 routeRight = Vector3.Cross(Vector3.up, forward);
                if (routeRight.sqrMagnitude < 0.0001f)
                {
                    routeRight = FlattenDirection(anchor.right, Vector3.right);
                }
                else
                {
                    routeRight.Normalize();
                }

                Vector3 anchorSide = anchor.position - laneCenter;
                anchorSide.y = 0f;
                float sideSign = Vector3.Dot(anchorSide, routeRight) >= 0f ? 1f : -1f;
                laneToBay = routeRight * sideSign * ParkingFallbackLaneOffset * experienceScale;
            }

            float lateralDistance = laneToBay.magnitude;
            float leadInDistance = Mathf.Clamp(lateralDistance + 1.6f * experienceScale, ParkingMinimumLeadInDistance * experienceScale, ParkingMaximumLeadInDistance * experienceScale);
            float diagonalLeadDistance = Mathf.Clamp(lateralDistance * 0.62f, 2.2f * experienceScale, 3.4f * experienceScale);
            float finalStraightDistance = Mathf.Clamp(lateralDistance * 0.42f, 1.25f * experienceScale, 2.4f * experienceScale);
            Vector3 laneSidePosition = parkingFinalPosition - laneToBay;

            parkingLaneEntryPosition = laneSidePosition - forward * leadInDistance;
            parkingDiagonalPosition = laneSidePosition + laneToBay * 0.42f - forward * diagonalLeadDistance;
            parkingAlignPosition = parkingFinalPosition - forward * finalStraightDistance;
            parkingPhase = ResolveInitialParkingPhase();
            EnsureSelectedGraphParkingEntryCursor();

            CocoonDebugLog.Info("Taxi", "Pickup parking plan built for " + selectedPullOverPoint.name
                + ": entry=" + FormatPosition(parkingLaneEntryPosition)
                + ", diagonal=" + FormatPosition(parkingDiagonalPosition)
                + ", align=" + FormatPosition(parkingAlignPosition)
                + ", final=" + FormatPosition(parkingFinalPosition)
                + ", routeForward=" + FormatPosition(forward)
                + ", initialPhase=" + parkingPhase
                + ", laneOffset=" + lateralDistance.ToString("0.00") + "m.", this);
        }

        private void EnsureSelectedGraphParkingEntryCursor()
        {
            if (selectedGraphBayIndex < 0 || roadGraph == null || parkingPhase == CocoonPullOverParkingPhase.None)
            {
                hasSelectedGraphParkingEntryCursor = false;
                selectedGraphParkingEntryCursor = CocoonRouteCursor.Invalid();
                selectedGraphParkingEntryForwardDistance = 0f;
                return;
            }

            if (hasSelectedGraphParkingEntryCursor && roadGraph.GetEdge(selectedGraphParkingEntryCursor.EdgeId) != null)
            {
                return;
            }

            if (roadGraph.TryProjectToGraph(parkingLaneEntryPosition, out selectedGraphParkingEntryCursor))
            {
                hasSelectedGraphParkingEntryCursor = true;
                if (!roadGraph.TryGetForwardDistance(taxiGraphCursor, selectedGraphParkingEntryCursor, out selectedGraphParkingEntryForwardDistance, Mathf.Max(16, roadGraph.EdgeCount + 8)))
                {
                    selectedGraphParkingEntryForwardDistance = 0f;
                }

                CocoonDebugLog.Info("Taxi", "Frozen graph parking entry bound for bay index " + selectedGraphBayIndex +
                    ": edge=" + selectedGraphParkingEntryCursor.EdgeId +
                    ", progress=" + selectedGraphParkingEntryCursor.Progress.ToString("0.00") +
                    ", forwardDistance=" + GetDisplayMeters(selectedGraphParkingEntryForwardDistance).ToString("0.0") +
                    "m.", this);
                return;
            }

            hasSelectedGraphParkingEntryCursor = false;
            selectedGraphParkingEntryCursor = CocoonRouteCursor.Invalid();
            selectedGraphParkingEntryForwardDistance = 0f;
            if (!hasLoggedMissingGraphParkingEntryProjection)
            {
                hasLoggedMissingGraphParkingEntryProjection = true;
                CocoonDebugLog.Warn("Taxi", "Could not project frozen parking entry onto ROADMAP graph; taxi will fall back to physical entry arrival for bay index " + selectedGraphBayIndex + ".", this);
            }
        }

        private bool MoveTaxiTowardsParkingPoint(Vector3 targetPosition, float speed, bool settleToFinalPose)
        {
            bool arrived = MoveTaxiTowardsParkingTarget(targetPosition, speed, true, settleToFinalPose);
            if (taxiRoot != null)
            {
                if (settleToFinalPose)
                {
                    taxiRoot.rotation = Quaternion.Slerp(taxiRoot.rotation, parkingFinalRotation, Time.deltaTime * 6.5f);
                }

                ConformTaxiToHighestParkingSurface(settleToFinalPose ? "settle" : "approach", false);
                targetPosition.y = taxiRoot.position.y;
                if (settleToFinalPose)
                {
                    parkingFinalPosition.y = taxiRoot.position.y;
                }
            }

            float arrivalDistance = GetScaledArrivalDistance(ParkingFinalArrivalDistance);
            float remainingPosition = taxiRoot != null ? Vector3.Distance(taxiRoot.position, targetPosition) : float.MaxValue;
            if (!settleToFinalPose)
            {
                return arrived && remainingPosition < arrivalDistance;
            }

            float remainingAngle = taxiRoot != null ? Quaternion.Angle(taxiRoot.rotation, parkingFinalRotation) : 180f;
            return arrived && remainingPosition < arrivalDistance && remainingAngle < ParkingFinalAngleTolerance;
        }

        private bool MoveTaxiTowardsParkingTarget(Vector3 targetPosition, float speed, bool faceMovement, bool brakeForArrival = false)
        {
            if (taxiRoot == null)
            {
                return false;
            }

            targetPosition.y = taxiRoot.position.y;
            Vector3 before = taxiRoot.position;
            float experienceScale = GetExperienceScale();
            CocoonTrafficParticipant blocker;
            bool hardBlocked;
            float safeRequestedSpeed = GetParkingTrafficSafeTaxiSpeed(targetPosition, speed, experienceScale, out hardBlocked, out blocker);
            float motionSpeed = hardBlocked ? 0f : GetSmoothedTaxiMotionSpeed(targetPosition, safeRequestedSpeed, brakeForArrival);
            if (hardBlocked)
            {
                taxiMotionSpeed = 0f;
            }

            LogParkingSpeedDiagnostic(speed, safeRequestedSpeed, hardBlocked, brakeForArrival, motionSpeed, blocker);

            taxiRoot.position = Vector3.MoveTowards(taxiRoot.position, targetPosition, motionSpeed * experienceScale * Time.deltaTime);
            Vector3 delta = taxiRoot.position - before;

            if (faceMovement && delta.sqrMagnitude > 0.00001f)
            {
                FaceTowards(delta);
            }

            if (trafficParticipant != null)
            {
                Vector3 flatDelta = delta;
                flatDelta.y = 0f;
                trafficParticipant.ReportSpeed(Time.deltaTime > 0f ? flatDelta.magnitude / Time.deltaTime : 0f);
            }

            bool arrived = Vector3.Distance(taxiRoot.position, targetPosition) < GetScaledArrivalDistance(0.08f);
            if (arrived && brakeForArrival)
            {
                taxiMotionSpeed = 0f;
            }

            return arrived;
        }

        private bool MoveTaxiTowardsDestinationBayExitTarget(Vector3 targetPosition, float speed, bool faceMovement, bool brakeForArrival = false)
        {
            if (taxiRoot == null)
            {
                return false;
            }

            targetPosition.y = taxiRoot.position.y;
            Vector3 before = taxiRoot.position;
            CocoonTrafficParticipant blocker;
            if (IsDestinationBayExitCorridorBlocked(targetPosition, out blocker))
            {
                taxiMotionSpeed = 0f;
                if (!taxiTrafficHardBlocked)
                {
                    CocoonDebugLog.Info("Traffic", "Destination bay exit is waiting for real corridor blocker " +
                        FormatTrafficBlocker(blocker) + ". Taxi will not reverse, U-turn, or pass through.", this);
                    taxiTrafficHardBlocked = true;
                }

                return false;
            }

            if (taxiTrafficHardBlocked)
            {
                CocoonDebugLog.Info("Traffic", "Destination bay exit corridor clear; taxi will continue forward-only merge.", this);
                taxiTrafficHardBlocked = false;
            }

            float experienceScale = GetExperienceScale();
            float motionSpeed = GetSmoothedTaxiMotionSpeed(targetPosition, speed, brakeForArrival);
            taxiRoot.position = Vector3.MoveTowards(taxiRoot.position, targetPosition, motionSpeed * experienceScale * Time.deltaTime);
            Vector3 delta = taxiRoot.position - before;

            if (faceMovement && delta.sqrMagnitude > 0.00001f)
            {
                FaceTowards(delta);
            }

            if (trafficParticipant != null)
            {
                Vector3 flatDelta = delta;
                flatDelta.y = 0f;
                trafficParticipant.ReportSpeed(Time.deltaTime > 0f ? flatDelta.magnitude / Time.deltaTime : 0f);
            }

            bool arrived = Vector3.Distance(taxiRoot.position, targetPosition) < GetScaledArrivalDistance(0.08f);
            if (arrived && brakeForArrival)
            {
                taxiMotionSpeed = 0f;
            }

            return arrived;
        }

        private void KeepTaxiBlockingAtPickupEntry()
        {
            EnsureTrafficParticipant();
            if (trafficParticipant == null)
            {
                return;
            }

            trafficParticipant.SetBlocksTraffic(true);
            if (roadGraph != null && taxiGraphCursor.IsValid)
            {
                trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot != null ? taxiRoot : transform, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
                return;
            }

            if (cruisePath != null && selectedPullOverPathIndex >= 0)
            {
                trafficParticipant.SetManualProgress(cruisePath.GetProgressAtSegmentTarget(selectedPullOverPathIndex, taxiRoot != null ? taxiRoot.position : parkingFinalPosition));
            }
        }

        private void PrepareDepartureMergePlan()
        {
            if (destinationDepartureGraphOnly)
            {
                ClearDepartureMergePlan();
                if (useRoadGraph && !TryRebindTaxiGraphCursorAtCurrentPosition("destination graph departure"))
                {
                    taxiMotionSpeed = 0f;
                    if (!hasLoggedDestinationDepartureGraphHold)
                    {
                        hasLoggedDestinationDepartureGraphHold = true;
                        CocoonDebugLog.Warn("Destination", "Destination departure cannot bind to ROADMAP graph; taxi will remain stopped instead of using a straight-line fallback.", this);
                    }
                }
                else
                {
                    CocoonDebugLog.Info("Destination", "Destination departure is graph-only; pickup merge plan skipped.", this);
                }

                return;
            }

            ClearDepartureMergePlan();
            if (taxiRoot == null)
            {
                return;
            }

            if (useRoadGraph && EnsureRoadGraphBinding() && selectedGraphBayIndex >= 0)
            {
                CocoonRoadGraph.BayBinding bay = roadGraph.GetBay(selectedGraphBayIndex);
                if (bay != null && bay.EdgeId >= 0)
                {
                    CocoonRouteCursor mergeCursor = new CocoonRouteCursor { EdgeId = bay.EdgeId, Progress = bay.Progress };
                    roadGraph.TryAdvance(ref mergeCursor, DepartureMergeLeadDistanceMeters * GetExperienceScale(), out _);
                    Vector3 graphForward = roadGraph.GetForward(mergeCursor.EdgeId);
                    departureMergePosition = roadGraph.GetPosition(mergeCursor);
                    departureMergePosition.y = taxiRoot.position.y;
                    departureMergeRotation = Quaternion.LookRotation(graphForward.sqrMagnitude > 0.0001f ? graphForward : parkingApproachForward, Vector3.up);
                    taxiGraphCursor = mergeCursor;
                    hasDepartureMergeTarget = true;
                    reachedDepartureMergeTarget = false;

                    EnsureTrafficParticipant();
                    if (trafficParticipant != null)
                    {
                        trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
                        trafficParticipant.SetBlocksTraffic(true);
                    }

                    CocoonDebugLog.Info("Taxi", "Graph departure merge plan from pickup bay: bayIndex=" + selectedGraphBayIndex +
                        ", edge=" + mergeCursor.EdgeId +
                        ", progress=" + mergeCursor.Progress.ToString("0.00") +
                        ", merge=" + FormatPosition(departureMergePosition) +
                        ", forward=" + FormatPosition(graphForward) +
                        ". No reverse/U-turn shortcut.", this);
                    return;
                }
            }

            if (cruisePath == null || cruisePath.Count < 2)
            {
                return;
            }

            if (selectedPullOverCruisePath != null)
            {
                cruisePath = selectedPullOverCruisePath;
            }

            int departurePathIndex = selectedPullOverPathIndex >= 0
                ? NormalizePathIndex(cruisePath, selectedPullOverPathIndex)
                : FindNearestPathIndex(cruisePath, taxiRoot.position);
            if (departurePathIndex < 0)
            {
                CocoonDebugLog.Warn("Taxi", "Departure has no valid pickup lane index; falling back to normal cruise path.", this);
                return;
            }

            Vector3 forward;
            if (!TryGetPathSegmentForward(cruisePath, departurePathIndex, out forward))
            {
                forward = FlattenDirection(parkingApproachForward, taxiRoot.forward);
            }

            Transform laneWaypoint = cruisePath.GetWaypoint(departurePathIndex);
            Vector3 laneCenter = laneWaypoint != null ? laneWaypoint.position : ResolvePullOverLaneCenter(GetPhysicalPullOverAnchor(selectedPullOverPoint));
            laneCenter.y = taxiRoot.position.y;
            Vector3 lateralFromLane = taxiRoot.position - laneCenter;
            lateralFromLane.y = 0f;
            lateralFromLane -= forward * Vector3.Dot(lateralFromLane, forward);
            Vector3 laneSidePosition = taxiRoot.position - lateralFromLane;
            laneSidePosition.y = taxiRoot.position.y;

            float experienceScale = GetExperienceScale();
            departureMergePosition = laneSidePosition + forward * DepartureMergeLeadDistanceMeters * experienceScale;
            departureMergePosition.y = taxiRoot.position.y;
            departureMergeRotation = Quaternion.LookRotation(forward, Vector3.up);
            departureCruiseResumeIndex = FindDepartureResumeIndex(cruisePath, departurePathIndex, departureMergePosition, forward);
            cruiseTargetIndex = departureCruiseResumeIndex;
            hasDepartureMergeTarget = true;
            reachedDepartureMergeTarget = false;

            EnsureTrafficParticipant();
            if (trafficParticipant != null)
            {
                trafficParticipant.Configure(cruisePath, taxiRoot, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), cruiseTargetIndex);
                trafficParticipant.SetBlocksTraffic(true);
                trafficParticipant.SetManualProgress(cruisePath.GetProgressAtSegmentTarget(departureCruiseResumeIndex, taxiRoot.position));
            }

            CocoonDebugLog.Info("Taxi", "Departure merge plan from pickup bay: lane=" + GetPathLabel(cruisePath) +
                ", bayIndex=" + departurePathIndex +
                ", resumeTarget=" + departureCruiseResumeIndex +
                ", merge=" + FormatPosition(departureMergePosition) +
                ", forward=" + FormatPosition(forward) + ".", this);
        }

        private int FindDepartureResumeIndex(CocoonTrafficLanePath path, int departurePathIndex, Vector3 mergePosition, Vector3 forward)
        {
            if (path == null || path.Count == 0)
            {
                return departurePathIndex;
            }

            int fallback = path.TryGetNextIndex(departurePathIndex, out int nextIndex)
                ? nextIndex
                : path.NormalizeIndex(departurePathIndex);
            Vector3 flatMergePosition = mergePosition;
            flatMergePosition.y = 0f;
            forward = FlattenDirection(forward, Vector3.forward);
            float minimumForwardDistance = GetScaledArrivalDistance(0.6f);
            for (int offset = 1; offset <= path.Count; offset++)
            {
                int rawIndex = departurePathIndex + offset;
                if (!path.IsClosedLoop && rawIndex >= path.Count)
                {
                    break;
                }

                int index = NormalizePathIndex(path, rawIndex);
                Transform waypoint = path.GetWaypoint(index);
                if (waypoint == null)
                {
                    continue;
                }

                Vector3 toWaypoint = waypoint.position;
                toWaypoint.y = 0f;
                toWaypoint -= flatMergePosition;
                if (Vector3.Dot(toWaypoint, forward) > minimumForwardDistance)
                {
                    return index;
                }
            }

            return fallback;
        }

        private void KeepTaxiBlockingDuringDepartureMerge()
        {
            EnsureTrafficParticipant();
            if (trafficParticipant == null)
            {
                return;
            }

            trafficParticipant.SetBlocksTraffic(true);
            if (roadGraph != null && taxiGraphCursor.IsValid)
            {
                trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot != null ? taxiRoot : transform, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
                return;
            }

            if (cruisePath != null && cruisePath.Count > 0 && departureCruiseResumeIndex >= 0)
            {
                trafficParticipant.SetManualProgress(cruisePath.GetProgressAtSegmentTarget(departureCruiseResumeIndex, taxiRoot != null ? taxiRoot.position : departureMergePosition));
            }
        }

        private void CompleteDepartureMerge()
        {
            reachedDepartureMergeTarget = true;
            if (taxiRoot != null)
            {
                taxiRoot.rotation = departureMergeRotation;
            }

            if (trafficParticipant != null)
            {
                trafficParticipant.SetBlocksTraffic(true);
                if (roadGraph != null && taxiGraphCursor.IsValid)
                {
                    trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot != null ? taxiRoot : transform, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
                }
                else
                {
                    trafficParticipant.SetLaneTargetIndex(cruiseTargetIndex);
                }
            }

            string routeLabel = roadGraph != null && taxiGraphCursor.IsValid ? "ROADMAP graph edge " + taxiGraphCursor.EdgeId : GetPathLabel(cruisePath) + " target " + cruiseTargetIndex;
            CocoonDebugLog.Info("Taxi", "Departure merge complete; rejoined " + routeLabel +
                " at " + FormatPosition(taxiRoot != null ? taxiRoot.position : departureMergePosition) + ".", this);
        }

        private void ClearDepartureMergePlan()
        {
            departureMergePosition = Vector3.zero;
            departureMergeRotation = Quaternion.identity;
            departureCruiseResumeIndex = -1;
            hasDepartureMergeTarget = false;
            reachedDepartureMergeTarget = false;
        }

        private Vector3 ResolveDestinationBayExitForward()
        {
            Vector3 fallback = taxiRoot != null ? taxiRoot.forward : parkingApproachForward;
            Vector3 forward = FlattenDirection(fallback, parkingApproachForward);

            if (parkingApproachForward.sqrMagnitude > 0.0001f)
            {
                Vector3 parkingForward = FlattenDirection(parkingApproachForward, forward);
                if (Vector3.Dot(forward, parkingForward) > 0.2f)
                {
                    forward = parkingForward;
                }
            }

            if (roadGraph != null && selectedGraphBayIndex >= 0)
            {
                CocoonRoadGraph.BayBinding bay = roadGraph.GetBay(selectedGraphBayIndex);
                if (bay != null && bay.EdgeId >= 0)
                {
                    Vector3 graphForward = FlattenDirection(bay.Forward, roadGraph.GetForward(bay.EdgeId));
                    if (Vector3.Dot(forward, graphForward) > 0.2f)
                    {
                        forward = graphForward;
                    }
                }
            }

            if (taxiRoot != null)
            {
                Vector3 currentForward = FlattenDirection(taxiRoot.forward, forward);
                if (Vector3.Dot(currentForward, forward) < 0.2f)
                {
                    // Prefer the physical car nose over any stale graph/bay direction; this keeps
                    // destination exit forward-only and prevents an in-place turnaround.
                    forward = currentForward;
                }
            }

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private Vector3 ResolveParkingApproachForward(Transform anchor)
        {
            CocoonRoadGraph.BayBinding graphBay = selectedGraphBayIndex >= 0 && roadGraph != null ? roadGraph.GetBay(selectedGraphBayIndex) : null;
            if (graphBay != null && graphBay.EdgeId >= 0)
            {
                Vector3 graphForward = graphBay.Forward;
                graphForward.y = 0f;
                if (graphForward.sqrMagnitude > 0.0001f)
                {
                    return graphForward.normalized;
                }
            }

            CocoonTrafficLanePath parkingPath = selectedPullOverCruisePath != null ? selectedPullOverCruisePath : cruisePath;
            int directionTargetIndex = selectedPullOverEntryPathIndex >= 0 ? selectedPullOverEntryPathIndex : selectedPullOverPathIndex;
            Vector3 routeForward;
            if (TryGetPathSegmentForward(parkingPath, directionTargetIndex, out routeForward))
            {
                return routeForward;
            }

            if (taxiRoot != null)
            {
                Vector3 taxiToAnchor = anchor.position - taxiRoot.position;
                taxiToAnchor.y = 0f;
                if (taxiToAnchor.sqrMagnitude > 0.0001f)
                {
                    return taxiToAnchor.normalized;
                }
            }

            return FlattenDirection(anchor.forward, Vector3.forward);
        }

        private bool TryGetPathSegmentForward(CocoonTrafficLanePath path, int targetIndex, out Vector3 forward)
        {
            forward = Vector3.zero;
            if (path == null || path.Count < 2 || targetIndex < 0)
            {
                return false;
            }

            int normalizedTarget = NormalizePathIndex(path, targetIndex);
            Transform previous = path.GetWaypoint(normalizedTarget - 1);
            Transform target = path.GetWaypoint(normalizedTarget);
            if (previous != null && target != null)
            {
                forward = target.position - previous.position;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    forward.Normalize();
                    return true;
                }
            }

            Transform current = path.GetWaypoint(normalizedTarget);
            Transform next = path.GetWaypoint(normalizedTarget + 1);
            if (current != null && next != null)
            {
                forward = next.position - current.position;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    forward.Normalize();
                    return true;
                }
            }

            return false;
        }

        private CocoonPullOverParkingPhase ResolveInitialParkingPhase()
        {
            CocoonPullOverParkingPhase phase = CocoonPullOverParkingPhase.ApproachLaneEntry;
            if (HasPassedParkingPoint(parkingLaneEntryPosition))
            {
                phase = CocoonPullOverParkingPhase.DiagonalIntoBay;
            }

            if (phase == CocoonPullOverParkingPhase.DiagonalIntoBay && HasPassedParkingPoint(parkingDiagonalPosition))
            {
                phase = CocoonPullOverParkingPhase.AlignWithBay;
            }

            if (phase == CocoonPullOverParkingPhase.AlignWithBay && HasPassedParkingPoint(parkingAlignPosition))
            {
                phase = CocoonPullOverParkingPhase.SettleAtBay;
            }

            return phase;
        }

        private bool HasPassedParkingPoint(Vector3 point)
        {
            if (taxiRoot == null)
            {
                return false;
            }

            Vector3 taxiToPoint = point - taxiRoot.position;
            taxiToPoint.y = 0f;
            return Vector3.Dot(taxiToPoint, parkingApproachForward) < -GetScaledArrivalDistance(0.18f);
        }

        private Vector3 ResolvePullOverLaneCenter(Transform anchor)
        {
            if (selectedGraphBayIndex >= 0 && roadGraph != null)
            {
                CocoonRoadGraph.BayBinding graphBay = roadGraph.GetBay(selectedGraphBayIndex);
                if (graphBay != null && graphBay.EdgeId >= 0)
                {
                    Vector3 laneCenter = roadGraph.GetPosition(new CocoonRouteCursor { EdgeId = graphBay.EdgeId, Progress = graphBay.Progress });
                    laneCenter.y = taxiRoot != null ? taxiRoot.position.y : laneCenter.y;
                    return laneCenter;
                }
            }

            CocoonTrafficLanePath parkingPath = selectedPullOverCruisePath != null ? selectedPullOverCruisePath : cruisePath;
            if (anchor != null && parkingPath != null && selectedPullOverPathIndex >= 0)
            {
                Transform waypoint = parkingPath.GetWaypoint(selectedPullOverPathIndex);
                if (waypoint != null)
                {
                    Vector3 laneCenter = waypoint.position;
                    laneCenter.y = taxiRoot != null ? taxiRoot.position.y : anchor.position.y;
                    return laneCenter;
                }
            }

            Vector3 fallback = anchor != null ? anchor.position - FlattenDirection(anchor.right, Vector3.right) * ParkingFallbackLaneOffset * GetExperienceScale() : parkingFinalPosition;
            fallback.y = taxiRoot != null ? taxiRoot.position.y : fallback.y;
            return fallback;
        }

        private void DrawParkingDebugPath()
        {
            if (parkingPhase == CocoonPullOverParkingPhase.None)
            {
                return;
            }

            Vector3 lift = Vector3.up * 0.16f;
            Debug.DrawLine(parkingLaneEntryPosition + lift, parkingDiagonalPosition + lift, Color.cyan);
            Debug.DrawLine(parkingDiagonalPosition + lift, parkingAlignPosition + lift, Color.yellow);
            Debug.DrawLine(parkingAlignPosition + lift, parkingFinalPosition + lift, Color.green);
        }

        private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = fallback;
                direction.y = 0f;
            }

            return direction.normalized;
        }

        private bool MoveTaxiTowards(Transform target, float speed)
        {
            return MoveTaxiTowards(target, speed, true);
        }

        private bool MoveTaxiTowards(Transform target, float speed, bool faceMovement)
        {
            if (taxiRoot == null || target == null)
            {
                return false;
            }

            bool arrived = MoveTaxiTowards(target.position, speed, faceMovement, false);
            if (arrived && target == pullOverPoint)
            {
                taxiRoot.rotation = Quaternion.Slerp(taxiRoot.rotation, target.rotation, Time.deltaTime * 7f);
            }

            return arrived;
        }

        private bool MoveTaxiTowards(Vector3 targetPosition, float speed, bool faceMovement, bool brakeForArrival = false)
        {
            if (taxiRoot == null)
            {
                return false;
            }

            targetPosition.y = taxiRoot.position.y;
            Vector3 before = taxiRoot.position;
            float experienceScale = GetExperienceScale();
            float safeRequestedSpeed = GetTrafficSafeTaxiSpeed(targetPosition, speed, experienceScale);
            float motionSpeed = GetSmoothedTaxiMotionSpeed(targetPosition, safeRequestedSpeed, brakeForArrival);
            taxiRoot.position = Vector3.MoveTowards(taxiRoot.position, targetPosition, motionSpeed * experienceScale * Time.deltaTime);
            Vector3 delta = taxiRoot.position - before;

            if (faceMovement && delta.sqrMagnitude > 0.00001f)
            {
                FaceTowards(delta);
            }

            if (trafficParticipant != null)
            {
                Vector3 flatDelta = delta;
                flatDelta.y = 0f;
                trafficParticipant.ReportSpeed(Time.deltaTime > 0f ? flatDelta.magnitude / Time.deltaTime : 0f);
            }

            bool arrived = Vector3.Distance(taxiRoot.position, targetPosition) < GetScaledArrivalDistance(0.08f);
            if (arrived && brakeForArrival)
            {
                taxiMotionSpeed = 0f;
            }

            return arrived;
        }

        private float GetTrafficSafeTaxiSpeed(Vector3 targetPosition, float requestedSpeed, float experienceScale)
        {
            float participantLimitedSpeed = requestedSpeed;
            if (trafficParticipant != null && experienceScale > 0.0001f)
            {
                float requestedWorldSpeed = Mathf.Max(0f, requestedSpeed) * experienceScale;
                float allowedWorldSpeed = trafficParticipant.GetAllowedSpeed(requestedWorldSpeed);
                participantLimitedSpeed = Mathf.Min(participantLimitedSpeed, allowedWorldSpeed / experienceScale);
                if (participantLimitedSpeed <= 0.0001f)
                {
                    if (!taxiTrafficHardBlocked)
                    {
                        CocoonDebugLog.Info("Traffic", "Taxi holding behind route leader: " + trafficParticipant.LastStopReason + ". No overtaking or pass-through allowed.", this);
                        taxiTrafficHardBlocked = true;
                    }

                    return 0f;
                }
            }

            CocoonTrafficParticipant blocker;
            float maxLookAhead = GetTaxiTrafficHardLookAhead(participantLimitedSpeed, experienceScale);
            bool includeCrossLaneBlocks = ShouldTaxiCheckCrossLaneTraffic();
            bool blocked = CocoonTrafficParticipant.HasHardBlockInPath(
                trafficParticipant,
                taxiRoot,
                targetPosition,
                GetScaledTaxiTrafficLength(),
                TaxiTrafficHardClearanceMeters * experienceScale,
                maxLookAhead,
                includeCrossLaneBlocks,
                out blocker);

            if (blocked)
            {
                if (!taxiTrafficHardBlocked)
                {
                    CocoonDebugLog.Info("Traffic", "Taxi holding behind blocking vehicle " + FormatTrafficBlocker(blocker) +
                        ", lookAhead=" + GetDisplayMeters(maxLookAhead).ToString("0.00") +
                        "m. No overtaking or pass-through allowed.", this);
                    taxiTrafficHardBlocked = true;
                }

                return 0f;
            }

            if (taxiTrafficHardBlocked)
            {
                CocoonDebugLog.Info("Traffic", "Taxi path clear; resuming movement.", this);
                taxiTrafficHardBlocked = false;
            }

            return participantLimitedSpeed;
        }

        private float GetParkingTrafficSafeTaxiSpeed(Vector3 targetPosition, float requestedSpeed, float experienceScale, out bool hardBlocked, out CocoonTrafficParticipant blocker)
        {
            hardBlocked = false;
            blocker = null;
            if (taxiRoot == null)
            {
                return 0f;
            }

            float maxLookAhead = GetTaxiTrafficHardLookAhead(requestedSpeed, experienceScale);
            bool blocked = CocoonTrafficParticipant.HasHardBlockInPath(
                trafficParticipant,
                taxiRoot,
                targetPosition,
                GetScaledTaxiTrafficLength(),
                TaxiTrafficHardClearanceMeters * experienceScale,
                maxLookAhead,
                true,
                out blocker);

            if (blocked)
            {
                hardBlocked = true;
                if (!taxiTrafficHardBlocked)
                {
                    CocoonDebugLog.Info("Traffic", "Taxi parking path hard-blocked by " + FormatTrafficBlocker(blocker) +
                        ", lookAhead=" + GetDisplayMeters(maxLookAhead).ToString("0.00") +
                        "m. Parking will wait; no pass-through allowed.", this);
                    taxiTrafficHardBlocked = true;
                }

                return 0f;
            }

            if (taxiTrafficHardBlocked)
            {
                CocoonDebugLog.Info("Traffic", "Taxi parking path clear; resuming bay movement.", this);
                taxiTrafficHardBlocked = false;
            }

            return requestedSpeed;
        }

        private void LogParkingSpeedDiagnostic(float requestedSpeed, float routeSafeSpeed, bool hardBlocked, bool brakeForArrival, float finalSpeed, CocoonTrafficParticipant blocker)
        {
            if (!enableParkingSpeedDiagnostics)
            {
                return;
            }

            if (Time.time < nextParkingSpeedDiagnosticTime)
            {
                return;
            }

            string blockerName = blocker != null ? blocker.name : "none";
            string key = parkingPhase + "|" + hardBlocked + "|" + blockerName + "|" + Mathf.RoundToInt(finalSpeed * 100f);
            if (key == lastParkingSpeedDiagnosticKey && Time.time < nextParkingSpeedDiagnosticTime + 0.75f)
            {
                return;
            }

            lastParkingSpeedDiagnosticKey = key;
            nextParkingSpeedDiagnosticTime = Time.time + 1f;
            CocoonDebugLog.Info("Taxi", "Parking speed diagnostic: phase=" + parkingPhase +
                ", requested=" + requestedSpeed.ToString("0.00") +
                "m/s, routeLeaderLimit=skipped-in-bay, routeSafe=" + routeSafeSpeed.ToString("0.00") +
                "m/s, hardBlock=" + hardBlocked +
                ", arrivalBrake=" + brakeForArrival +
                ", final=" + finalSpeed.ToString("0.00") +
                "m/s, blocker=" + blockerName + ".", this);
        }

        private bool ShouldTaxiCheckCrossLaneTraffic()
        {
            return true;
        }

        private float GetTaxiTrafficHardLookAhead(float requestedSpeed, float experienceScale)
        {
            float scaledClearance = TaxiTrafficHardClearanceMeters * experienceScale;
            float speedHorizon = Mathf.Max(0f, requestedSpeed) * experienceScale * TaxiTrafficHardLookAheadSeconds;
            return GetScaledTaxiTrafficLength() * 0.55f + scaledClearance + Mathf.Max(0.25f * experienceScale, speedHorizon);
        }

        private string FormatTrafficBlocker(CocoonTrafficParticipant blocker)
        {
            if (blocker == null)
            {
                return "unknown traffic blocker";
            }

            string blockerName = blocker.gameObject != null ? blocker.gameObject.name : "traffic";
            return blockerName +
                " lane=" + blocker.LaneName +
                " target=" + blocker.TargetIndex +
                " pos=" + FormatPosition(blocker.TrackedPosition);
        }

        private void DisableTaxiPhysicalCollision()
        {
            Transform root = taxiRoot != null ? taxiRoot : transform;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null ||
                    collider.GetComponentInParent<CocoonWorldButton>() != null ||
                    collider.GetComponentInParent<Canvas>() != null ||
                    collider.GetComponentInParent<CocoonSafePickupZone>() != null)
                {
                    continue;
                }

                collider.enabled = false;
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rigidbody = rigidbodies[i];
                if (rigidbody == null)
                {
                    continue;
                }

                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }
        }

        private float GetSmoothedTaxiMotionSpeed(Vector3 targetPosition, float requestedSpeed, bool brakeForArrival)
        {
            float desiredSpeed = Mathf.Max(0f, requestedSpeed);
            if (desiredSpeed <= 0.0001f)
            {
                taxiMotionSpeed = 0f;
                return 0f;
            }

            if (brakeForArrival && taxiRoot != null)
            {
                Vector3 toTarget = targetPosition - taxiRoot.position;
                toTarget.y = 0f;
                float remainingMeters = GetDisplayMeters(toTarget.magnitude);
                float brakeT = Mathf.Clamp01(remainingMeters / TaxiParkingSlowdownDistanceMeters);
                desiredSpeed *= Mathf.SmoothStep(0f, 1f, brakeT);
                if (remainingMeters > ParkingFinalArrivalDistance)
                {
                    desiredSpeed = Mathf.Max(desiredSpeed, MinimumTaxiCrawlSpeed);
                }
            }

            float acceleration = desiredSpeed > taxiMotionSpeed ? TaxiAccelerationMetersPerSecond : TaxiBrakingMetersPerSecond;
            taxiMotionSpeed = Mathf.MoveTowards(taxiMotionSpeed, desiredSpeed, acceleration * Time.deltaTime);
            return taxiMotionSpeed;
        }

        private void SelectNextPullOverPoint()
        {
            EnsureRouteCandidates();
            if (TrySelectGraphPullOverPoint())
            {
                return;
            }

            if (pullOverPoints == null || pullOverPoints.Length == 0)
            {
                selectedPullOverPoint = pullOverPoint;
                selectedPullOverCruisePath = cruisePath;
                selectedPullOverPathIndex = -1;
                selectedPullOverEntryPathIndex = -1;
                selectedPullOverDirectPullIn = false;
                pendingTurnaroundToOppositeLane = false;
                turnaroundPathIndex = -1;
                reachedSelectedPullOverEntry = true;
                parkingPhase = CocoonPullOverParkingPhase.None;
                return;
            }

            int bestIndex = -1;
            int bestPathIndex = -1;
            int bestEntryIndex = -1;
            float bestDistance = float.MaxValue;
            float bestForwardRouteDistance = float.MaxValue;
            int bestForwardEntryDelta = int.MaxValue;
            CocoonTrafficLanePath bestPath = null;
            int bestTurnaroundIndex = -1;
            string bestLaneReason = "";
            string bestLaneDiagnostics = "";
            int currentTarget = NormalizePathIndex(cruisePath, cruiseTargetIndex);
            bool hasRiderPosition = raiseHandDetector != null && raiseHandDetector.HasRiderPosition;
            Vector3 referencePosition = hasRiderPosition
                ? raiseHandDetector.RiderPosition
                : (taxiRoot != null ? taxiRoot.position : transform.position);
            referencePosition.y = 0f;
            int validBayCount = 0;
            int nullBayCount = 0;
            CocoonDebugLog.Verbose("Pickup", "Evaluating " + pullOverPoints.Length +
                " pickup bay reference(s). currentPath=" + GetPathLabel(cruisePath) +
                ", currentTarget=" + currentTarget +
                ", reference=" + (hasRiderPosition ? "rider" : "taxi") +
                " " + FormatPosition(referencePosition) + ".", this);

            for (int i = 0; i < pullOverPoints.Length; i++)
            {
                if (pullOverPoints[i] == null)
                {
                    nullBayCount++;
                    CocoonDebugLog.Warn("Pickup", "Bay[" + i + "] is a null reference and cannot be selected.", this);
                    continue;
                }

                int pathIndex;
                Transform anchor = GetPhysicalPullOverAnchor(pullOverPoints[i]);
                if (anchor == null)
                {
                    nullBayCount++;
                    CocoonDebugLog.Warn("Pickup", "Bay[" + i + "] " + pullOverPoints[i].name + " has no usable transform anchor.", this);
                    continue;
                }

                bool hasExplicitStopAnchor = anchor.name == "Pickup Bay Stop";
                if (!hasExplicitStopAnchor)
                {
                    CocoonDebugLog.Warn("Pickup", "Bay[" + i + "] " + pullOverPoints[i].name +
                        " has no explicit Pickup Bay Stop child/reference; using the bay transform as a fallback anchor.", this);
                }

                Vector3 bayPosition = anchor != null ? anchor.position : pullOverPoints[i].position;
                bayPosition.y = 0f;
                float riderDistance = Vector3.Distance(bayPosition, referencePosition);
                int entryIndex;
                int turnaroundIndex;
                int forwardEntryDelta;
                string laneReason;
                string laneDiagnostics;
                CocoonTrafficLanePath path = ResolveBestLaneForBay(i, bayPosition, currentTarget, out pathIndex, out entryIndex, out forwardEntryDelta, out turnaroundIndex, out laneReason, out laneDiagnostics);
                float forwardRouteDistance = EstimateForwardOnlyRouteDistance(path, entryIndex, bayPosition, currentTarget, forwardEntryDelta);
                validBayCount++;
                CocoonDebugLog.Verbose("Pickup", "Bay[" + i + "] recognized: ref=" + pullOverPoints[i].name +
                    ", anchor=" + anchor.name +
                    " " + FormatPosition(bayPosition) +
                    ", " + (hasRiderPosition ? "riderDistance" : "taxiDistance") + "=" + GetDisplayMeters(riderDistance).ToString("0.0") + "m" +
                    ", forwardRouteDistance=" + FormatDisplayMeters(GetDisplayMeters(forwardRouteDistance)) +
                    ", candidatePath=" + GetPathLabel(path) +
                    ", pathIndex=" + pathIndex +
                    ", entryIndex=" + entryIndex +
                    ", forwardEntryDelta=" + forwardEntryDelta +
                    ", turnaroundIndex=" + turnaroundIndex +
                    ", reason=" + laneReason +
                    ", routeCheck=" + laneDiagnostics + ".", this);

                float routeTieTolerance = Mathf.Max(0.08f * GetExperienceScale(), 0.001f);
                if (forwardRouteDistance < bestForwardRouteDistance - routeTieTolerance ||
                    (Mathf.Abs(forwardRouteDistance - bestForwardRouteDistance) <= routeTieTolerance &&
                     (riderDistance < bestDistance - 0.001f ||
                      (Mathf.Abs(riderDistance - bestDistance) <= 0.001f && forwardEntryDelta < bestForwardEntryDelta))))
                {
                    bestDistance = riderDistance;
                    bestForwardRouteDistance = forwardRouteDistance;
                    bestForwardEntryDelta = forwardEntryDelta;
                    bestPath = path;
                    bestPathIndex = pathIndex;
                    bestEntryIndex = entryIndex;
                    bestTurnaroundIndex = turnaroundIndex;
                    bestLaneReason = laneReason;
                    bestLaneDiagnostics = laneDiagnostics;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                selectedPullOverPoint = pullOverPoint;
                selectedPullOverCruisePath = cruisePath;
                selectedPullOverPathIndex = -1;
                selectedPullOverEntryPathIndex = -1;
                selectedPullOverDirectPullIn = false;
                pendingTurnaroundToOppositeLane = false;
                turnaroundPathIndex = -1;
                reachedSelectedPullOverEntry = true;
                parkingPhase = CocoonPullOverParkingPhase.None;
                return;
            }

            selectedPullOverPoint = pullOverPoints[bestIndex];
            selectedPullOverCruisePath = bestPath != null ? bestPath : cruisePath;
            selectedPullOverPathIndex = bestPathIndex >= 0 ? bestPathIndex : GetPullOverPathIndexForPath(bestIndex, selectedPullOverCruisePath, pullOverPathIndices);
            selectedPullOverDirectPullIn = CanDirectPullIntoBay(selectedPullOverCruisePath, selectedPullOverPathIndex, selectedPullOverPoint);
            selectedPullOverEntryPathIndex = selectedPullOverDirectPullIn
                ? selectedPullOverPathIndex
                : (bestEntryIndex >= 0 ? bestEntryIndex : GetRouteAwarePullOverEntryPathIndex(selectedPullOverCruisePath, selectedPullOverPathIndex, currentTarget, -1));
            pendingTurnaroundToOppositeLane = false;
            turnaroundPathIndex = -1;
            reachedSelectedPullOverEntry = selectedPullOverDirectPullIn || (selectedPullOverEntryPathIndex < 0 || cruisePath == null || cruisePath.Count == 0);
            parkingPhase = CocoonPullOverParkingPhase.None;
            Transform selectedAnchor = GetPhysicalPullOverAnchor(selectedPullOverPoint);
            string anchorDetails = selectedAnchor != null && selectedAnchor != selectedPullOverPoint
                ? ", physical anchor=" + selectedAnchor.name + " " + FormatPosition(selectedAnchor.position)
                : "";
            string referenceLabel = hasRiderPosition ? "rider" : "taxi";
            string distanceLabel = hasRiderPosition ? "riderDistance" : "taxiDistance";
            string laneDetails = ", lane=current-forward-only";
            CocoonDebugLog.Info("Taxi", "Selected forward-route " + referenceLabel + " pickup bay " + selectedPullOverPoint.name +
                " at route index " + selectedPullOverPathIndex +
                ", entry index " + selectedPullOverEntryPathIndex +
                ", route=" + (selectedPullOverDirectPullIn ? "direct-pull-in" : "forward-only") +
                laneDetails +
                ", forwardEntryDelta=" + bestForwardEntryDelta +
                ", forwardRouteDistance=" + FormatDisplayMeters(GetDisplayMeters(bestForwardRouteDistance)) +
                ", reason=" + bestLaneReason +
                ", " + distanceLabel + "=" + GetDisplayMeters(bestDistance).ToString("0.0") + "m" +
                anchorDetails +
                ", evaluatedBays=" + validBayCount + "/" + pullOverPoints.Length +
                ", nullRefs=" + nullBayCount +
                ", routeCheck=" + bestLaneDiagnostics + ".", this);
        }

        private bool TrySelectGraphPullOverPoint()
        {
            if (!EnsureRoadGraphBinding() || roadGraph.BayCount == 0)
            {
                return false;
            }

            bool hasRiderPosition = raiseHandDetector != null && raiseHandDetector.HasRiderPosition;
            Vector3 referencePosition = hasRiderPosition
                ? raiseHandDetector.RiderPosition
                : (taxiRoot != null ? taxiRoot.position : transform.position);
            referencePosition.y = 0f;

            int bestImmediateIndex = -1;
            float bestImmediateDistance = float.MaxValue;
            float bestImmediateReferenceDistance = float.MaxValue;
            int bestReachableIndex = -1;
            float bestReachableDistance = float.MaxValue;
            float bestReachableReferenceDistance = float.MaxValue;

            for (int i = 0; i < roadGraph.BayCount; i++)
            {
                CocoonRoadGraph.BayBinding bay = roadGraph.GetBay(i);
                if (bay == null || bay.Stop == null || bay.EdgeId < 0)
                {
                    continue;
                }

                if (!roadGraph.TryGetForwardDistanceToBay(taxiGraphCursor, i, out float forwardDistance))
                {
                    continue;
                }

                Vector3 bayPosition = bay.Stop.position;
                bayPosition.y = 0f;
                float referenceDistance = Vector3.Distance(referencePosition, bayPosition);
                bool immediateBay = roadGraph.IsBayOnCurrentOrNextEdge(taxiGraphCursor, i, out float immediateDistance);
                if (!immediateBay && TryGetPhysicalCurrentOrNextBayDistance(bay.Stop.position, out float physicalImmediateDistance))
                {
                    immediateBay = true;
                    immediateDistance = physicalImmediateDistance;
                }

                if (immediateBay)
                {
                    if (immediateDistance < bestImmediateDistance - 0.001f ||
                        (Mathf.Abs(immediateDistance - bestImmediateDistance) <= 0.001f && referenceDistance < bestImmediateReferenceDistance))
                    {
                        bestImmediateIndex = i;
                        bestImmediateDistance = immediateDistance;
                        bestImmediateReferenceDistance = referenceDistance;
                    }
                }

                if (forwardDistance < bestReachableDistance - 0.001f ||
                    (Mathf.Abs(forwardDistance - bestReachableDistance) <= 0.001f && referenceDistance < bestReachableReferenceDistance))
                {
                    bestReachableIndex = i;
                    bestReachableDistance = forwardDistance;
                    bestReachableReferenceDistance = referenceDistance;
                }
            }

            int selectedIndex = bestImmediateIndex >= 0 ? bestImmediateIndex : bestReachableIndex;
            if (selectedIndex < 0)
            {
                return false;
            }

            CocoonRoadGraph.BayBinding selectedBay = roadGraph.GetBay(selectedIndex);
            if (selectedBay == null || selectedBay.Stop == null)
            {
                return false;
            }

            selectedPullOverPoint = selectedBay.Stop;
            selectedPullOverCruisePath = null;
            selectedPullOverPathIndex = -1;
            selectedPullOverEntryPathIndex = -1;
            selectedPullOverDirectPullIn = false;
            selectedGraphBayIndex = selectedIndex;
            selectedGraphBayCursor = new CocoonRouteCursor { EdgeId = selectedBay.EdgeId, Progress = selectedBay.Progress };
            selectedGraphBayForwardDistance = bestImmediateIndex >= 0 ? bestImmediateDistance : bestReachableDistance;
            selectedGraphParkingEntryCursor = CocoonRouteCursor.Invalid();
            selectedGraphParkingEntryForwardDistance = 0f;
            hasSelectedGraphParkingEntryCursor = false;
            hasLoggedMissingGraphParkingEntryProjection = false;
            reachedSelectedPullOverEntry = false;
            pendingTurnaroundToOppositeLane = false;
            turnaroundPathIndex = -1;
            parkingPhase = CocoonPullOverParkingPhase.None;
            BuildParkingPlan();
            if (parkingPhase == CocoonPullOverParkingPhase.None)
            {
                selectedPullOverPoint = null;
                selectedGraphBayIndex = -1;
                selectedGraphBayCursor = CocoonRouteCursor.Invalid();
                selectedGraphParkingEntryCursor = CocoonRouteCursor.Invalid();
                hasSelectedGraphParkingEntryCursor = false;
                return false;
            }

            if (parkingPhase != CocoonPullOverParkingPhase.ApproachLaneEntry)
            {
                selectedGraphParkingEntryForwardDistance = 0f;
                reachedSelectedPullOverEntry = true;
            }
            else
            {
                float entryForwardDistance = 0f;
                if (hasSelectedGraphParkingEntryCursor &&
                    roadGraph.TryGetForwardDistance(taxiGraphCursor, selectedGraphParkingEntryCursor, out entryForwardDistance, Mathf.Max(16, roadGraph.EdgeCount + 8)))
                {
                    selectedGraphParkingEntryForwardDistance = entryForwardDistance;
                    reachedSelectedPullOverEntry = entryForwardDistance <= Mathf.Max(0.18f * GetExperienceScale(), 0.015f);
                }
                else
                {
                    selectedGraphParkingEntryForwardDistance = 0f;
                    reachedSelectedPullOverEntry = HasPassedParkingPoint(parkingLaneEntryPosition);
                }
            }

            string priority = bestImmediateIndex >= 0 ? "current-or-next-edge-priority" : "forward-graph-distance";
            CocoonDebugLog.Info("Taxi", "Selected ROADMAP graph pickup bay " + selectedPullOverPoint.name +
                ": bayIndex=" + selectedIndex +
                ", edge=" + selectedBay.EdgeId +
                ", progress=" + selectedBay.Progress.ToString("0.00") +
                ", forwardDistance=" + GetDisplayMeters(selectedGraphBayForwardDistance).ToString("0.0") + "m" +
                ", parkingEntryDistance=" + GetDisplayMeters(selectedGraphParkingEntryForwardDistance).ToString("0.0") + "m" +
                ", priority=" + priority +
                ", policy=no-reverse-no-u-turn-no-overtake.", this);
            return true;
        }

        private bool TryGetPhysicalCurrentOrNextBayDistance(Vector3 bayPosition, out float distance)
        {
            distance = float.MaxValue;
            if (roadGraph == null || !taxiGraphCursor.IsValid || taxiRoot == null)
            {
                return false;
            }

            float experienceScale = GetExperienceScale();
            float lateralWindow = Mathf.Max(0.22f, 2.8f * experienceScale);
            if (TryProjectBayOntoGraphEdge(taxiGraphCursor.EdgeId, taxiGraphCursor.Progress, bayPosition, lateralWindow, out distance))
            {
                return true;
            }

            CocoonRoadGraph.Edge edge = roadGraph.GetEdge(taxiGraphCursor.EdgeId);
            if (edge == null || edge.NextEdgeIds == null)
            {
                return false;
            }

            float currentRemaining = Mathf.Max(0f, edge.Length - taxiGraphCursor.Progress);
            for (int i = 0; i < edge.NextEdgeIds.Length; i++)
            {
                if (TryProjectBayOntoGraphEdge(edge.NextEdgeIds[i], 0f, bayPosition, lateralWindow, out float nextDistance))
                {
                    distance = currentRemaining + nextDistance;
                    return true;
                }
            }

            return false;
        }

        private bool TryProjectBayOntoGraphEdge(int edgeId, float minimumProgress, Vector3 bayPosition, float lateralWindow, out float distance)
        {
            distance = float.MaxValue;
            CocoonRoadGraph.Edge edge = roadGraph != null ? roadGraph.GetEdge(edgeId) : null;
            if (edge == null || edge.Length <= 0.001f)
            {
                return false;
            }

            Vector3 start = edge.Start;
            Vector3 end = edge.End;
            start.y = 0f;
            end.y = 0f;
            Vector3 flatBay = bayPosition;
            flatBay.y = 0f;
            Vector3 segment = end - start;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= 0.000001f)
            {
                return false;
            }

            float t = Mathf.Clamp01(Vector3.Dot(flatBay - start, segment) / segmentLengthSqr);
            float progress = edge.Length * t;
            if (progress < minimumProgress - Mathf.Max(0.02f, 0.4f * GetExperienceScale()))
            {
                return false;
            }

            Vector3 closest = start + segment * t;
            float lateralDistance = Vector3.Distance(flatBay, closest);
            if (lateralDistance > lateralWindow)
            {
                return false;
            }

            Vector3 taxiForward = taxiRoot.forward;
            taxiForward.y = 0f;
            Vector3 taxiPosition = taxiRoot.position;
            taxiPosition.y = 0f;
            Vector3 toBay = flatBay - taxiPosition;
            if (taxiForward.sqrMagnitude > 0.0001f && toBay.sqrMagnitude > 0.0001f && Vector3.Dot(taxiForward.normalized, toBay.normalized) < 0.18f)
            {
                return false;
            }

            distance = Mathf.Max(0f, progress - minimumProgress);
            return true;
        }

        private CocoonTrafficLanePath ResolveBestLaneForBay(int bayIndex, Vector3 bayPosition, int currentTarget, out int pathIndex, out int entryIndex, out int forwardEntryDelta, out int turnaroundIndex, out string laneReason, out string laneDiagnostics)
        {
            CocoonTrafficLanePath primaryPath = primaryCruisePath != null ? primaryCruisePath : cruisePath;
            int primaryIndex = GetPullOverPathIndexForPath(bayIndex, primaryPath, pullOverPathIndices);
            int primaryEntryIndex = GetPullOverEntryPathIndex(primaryPath, primaryIndex, currentTarget);
            int primaryTurnaroundIndex;
            int primaryForwardDelta = EstimateForwardOnlyRouteDelta(primaryPath, primaryEntryIndex, currentTarget, bayIndex, out primaryTurnaroundIndex);
            float primaryDistance = GetWaypointDistanceSqr(primaryPath, primaryIndex, bayPosition);

            int activePathCount = cruisePath != null ? cruisePath.Count : (primaryPath != null ? primaryPath.Count : 0);
            int longLoopThreshold = Mathf.Max(4, activePathCount / 2);
            laneDiagnostics = "primary=" + FormatPathRouteCheck(primaryPath, primaryIndex, primaryEntryIndex, primaryForwardDelta, primaryDistance) +
                "; opposite=ignored-by-forward-only-policy" +
                "; lanePolicy=forward-only-no-u-turn-no-overtake" +
                "; longLoopThreshold=" + longLoopThreshold;

            pathIndex = primaryIndex;
            entryIndex = primaryEntryIndex;
            forwardEntryDelta = primaryForwardDelta;
            turnaroundIndex = primaryTurnaroundIndex;
            laneReason = primaryPath != null && primaryIndex >= 0 && primaryEntryIndex >= 0
                ? "primary-lane-forward-only"
                : "primary-lane-unavailable";
            return primaryPath;
        }

        private int EstimateForwardOnlyRouteDelta(CocoonTrafficLanePath targetPath, int entryIndex, int currentTarget, int fallbackOrder, out int turnaroundIndex)
        {
            turnaroundIndex = -1;
            if (targetPath == null)
            {
                return Mathf.Max(1, fallbackOrder + 1);
            }

            if (targetPath == cruisePath)
            {
                if (targetPath.IsClosedLoop)
                {
                    return GetForwardRouteDelta(entryIndex, currentTarget, targetPath.Count, fallbackOrder);
                }

                int normalizedEntry = NormalizePathIndex(targetPath, entryIndex);
                int normalizedCurrent = NormalizePathIndex(targetPath, currentTarget);
                return normalizedEntry >= normalizedCurrent ? normalizedEntry - normalizedCurrent : int.MaxValue / 4;
            }

            return int.MaxValue / 4;
        }

        private float EstimateForwardOnlyRouteDistance(CocoonTrafficLanePath targetPath, int entryIndex, Vector3 bayPosition, int currentTarget, int fallbackDelta)
        {
            if (targetPath == null || entryIndex < 0)
            {
                return float.MaxValue;
            }

            float fallbackDistance = Mathf.Max(1, fallbackDelta + 1) * Mathf.Max(GetExperienceScale(), 0.01f);
            if (cruisePath == null || taxiRoot == null)
            {
                return fallbackDistance;
            }

            if (targetPath == cruisePath)
            {
                return GetForwardDistanceOnPath(targetPath, currentTarget, taxiRoot.position, entryIndex, bayPosition, fallbackDistance);
            }

            return float.MaxValue;
        }

        private float GetForwardDistanceOnPath(CocoonTrafficLanePath path, int startTargetIndex, Vector3 startPosition, int targetIndex, Vector3 targetPosition, float fallbackDistance)
        {
            if (path == null || path.Count < 2 || targetIndex < 0)
            {
                return fallbackDistance;
            }

            float totalLength = path.TotalLength;
            if (totalLength <= 0.001f)
            {
                return fallbackDistance;
            }

            int normalizedStart = NormalizePathIndex(path, startTargetIndex);
            int normalizedTarget = NormalizePathIndex(path, targetIndex);
            float startProgress = path.GetProgressAtSegmentTarget(normalizedStart, startPosition);
            float targetProgress = path.GetProgressAtSegmentTarget(normalizedTarget, targetPosition);
            float distance = path.GetForwardDistance(startProgress, targetProgress);
            if (distance == float.MaxValue)
            {
                return float.MaxValue;
            }

            float minimumForwardDistance = Mathf.Max(0.12f * GetExperienceScale(), 0.001f);
            if (path.IsClosedLoop && distance <= minimumForwardDistance)
            {
                distance += totalLength;
            }

            return distance;
        }

        private bool CanDirectPullIntoBay(CocoonTrafficLanePath path, int bayPathIndex, Transform stopPoint)
        {
            if (path == null || path != cruisePath || taxiRoot == null || bayPathIndex < 0 || stopPoint == null)
            {
                return false;
            }

            Transform anchor = GetPhysicalPullOverAnchor(stopPoint);
            if (anchor == null)
            {
                return false;
            }

            Vector3 routeForward;
            if (!TryGetPathSegmentForward(path, bayPathIndex, out routeForward))
            {
                return false;
            }

            Vector3 toBay = anchor.position - taxiRoot.position;
            toBay.y = 0f;
            float distance = toBay.magnitude;
            if (distance <= 0.0001f)
            {
                return true;
            }

            float experienceScale = GetExperienceScale();
            float forwardDistance = Vector3.Dot(toBay, routeForward);
            bool bayIsAheadOrBeside = forwardDistance >= -DirectBayPullInBehindToleranceMeters * experienceScale;
            bool bayIsCloseEnough = distance <= DirectBayPullInMaxDistanceMeters * experienceScale;
            return bayIsAheadOrBeside && bayIsCloseEnough;
        }

        private void LogForwardRouteProgress(string phase, int targetIndex)
        {
            if (Time.time < nextPullOverProgressLogTime || cruisePath == null || cruisePath.Count == 0)
            {
                return;
            }

            int currentTarget = NormalizePathIndex(cruisePath, cruiseTargetIndex);
            int remainingDelta = GetForwardRouteDelta(targetIndex, currentTarget, cruisePath.Count, 0);
            float targetDistanceMeters;
            IsTaxiNearPathWaypoint(cruisePath, targetIndex, 0f, out targetDistanceMeters);
            CocoonDebugLog.Info("Taxi", phase + " forward route on " + GetPathLabel(cruisePath) +
                ": currentTarget=" + currentTarget +
                ", target=" + targetIndex +
                ", remainingWaypointDelta=" + remainingDelta +
                ", targetDistance=" + FormatDisplayMeters(targetDistanceMeters) + ".", this);
            nextPullOverProgressLogTime = Time.time + 1.25f;
        }

        private int FindNextTurnaroundIndex()
        {
            if (cruisePath == null || cruisePath.Count == 0)
            {
                return -1;
            }

            int startIndex = NormalizePathIndex(cruisePath, cruiseTargetIndex);
            for (int offset = 0; offset < cruisePath.Count; offset++)
            {
                int index = NormalizePathIndex(cruisePath, startIndex + offset);
                Transform waypoint = cruisePath.GetWaypoint(index);
                CocoonTrafficControlPoint control = waypoint != null ? waypoint.GetComponent<CocoonTrafficControlPoint>() : null;
                if (control != null && control.ControlType == CocoonTrafficControlType.IntersectionYield)
                {
                    return index;
                }
            }

            return startIndex;
        }

        private int FindSwitchTargetIndexOnPath(CocoonTrafficLanePath targetPath, int activeTurnaroundIndex)
        {
            if (targetPath == null)
            {
                return -1;
            }

            Transform activeTurnaround = cruisePath != null && activeTurnaroundIndex >= 0 ? cruisePath.GetWaypoint(activeTurnaroundIndex) : null;
            int nearestIndex = FindNearestPathIndex(targetPath, activeTurnaround != null ? activeTurnaround.position : (taxiRoot != null ? taxiRoot.position : transform.position));
            return nearestIndex >= 0 ? NormalizePathIndex(targetPath, nearestIndex) : 0;
        }

        private string GetPathLabel(CocoonTrafficLanePath path)
        {
            return path != null ? path.name : "none";
        }

        private string FormatPathRouteCheck(CocoonTrafficLanePath path, int pathIndex, int entryIndex, int forwardDelta, float laneDistanceSqr)
        {
            if (path == null)
            {
                return "none";
            }

            return GetPathLabel(path) +
                " idx=" + pathIndex +
                " entry=" + entryIndex +
                " delta=" + forwardDelta +
                " laneDist=" + FormatDisplayMetersFromSqr(laneDistanceSqr);
        }

        private string FormatDisplayMetersFromSqr(float distanceSqr)
        {
            if (float.IsInfinity(distanceSqr) || distanceSqr >= float.MaxValue * 0.5f)
            {
                return "n/a";
            }

            return GetDisplayMeters(Mathf.Sqrt(Mathf.Max(0f, distanceSqr))).ToString("0.0") + "m";
        }

        private string FormatDisplayMeters(float distanceMeters)
        {
            if (float.IsInfinity(distanceMeters) || distanceMeters >= float.MaxValue * 0.5f)
            {
                return "n/a";
            }

            return Mathf.Max(0f, distanceMeters).ToString("0.0") + "m";
        }

        private int GetForwardRouteDelta(int pathIndex, int currentTarget, int pathCount, int fallbackOrder)
        {
            if (pathCount <= 0 || pathIndex < 0)
            {
                return Mathf.Max(1, fallbackOrder + 1);
            }

            int normalizedPath = ((pathIndex % pathCount) + pathCount) % pathCount;
            int normalizedTarget = ((currentTarget % pathCount) + pathCount) % pathCount;
            return (normalizedPath - normalizedTarget + pathCount) % pathCount;
        }

        private int GetCircularIndexDistance(int firstIndex, int secondIndex, int pathCount)
        {
            if (pathCount <= 0 || firstIndex < 0 || secondIndex < 0)
            {
                return int.MaxValue;
            }

            int first = ((firstIndex % pathCount) + pathCount) % pathCount;
            int second = ((secondIndex % pathCount) + pathCount) % pathCount;
            int direct = Mathf.Abs(first - second);
            return Mathf.Min(direct, pathCount - direct);
        }

        private int GetPullOverEntryPathIndex(int bayPathIndex)
        {
            return GetPullOverEntryPathIndex(cruisePath, bayPathIndex, NormalizePathIndex(cruisePath, cruiseTargetIndex));
        }

        private int GetPullOverEntryPathIndex(CocoonTrafficLanePath path, int bayPathIndex)
        {
            int routeStartIndex = path == cruisePath ? NormalizePathIndex(path, cruiseTargetIndex) : bayPathIndex;
            return GetPullOverEntryPathIndex(path, bayPathIndex, routeStartIndex);
        }

        private int GetPullOverEntryPathIndex(CocoonTrafficLanePath path, int bayPathIndex, int routeStartIndex)
        {
            if (path == null || path.Count == 0 || bayPathIndex < 0)
            {
                return -1;
            }

            int normalizedStart = routeStartIndex >= 0 ? NormalizePathIndex(path, routeStartIndex) : NormalizePathIndex(path, bayPathIndex);
            List<int> entryCandidates = new List<int>();
            AddPullOverEntryCandidate(path, entryCandidates, bayPathIndex - 1);
            AddPullOverEntryCandidate(path, entryCandidates, bayPathIndex);
            AddPullOverEntryCandidate(path, entryCandidates, bayPathIndex + 1);
            if (entryCandidates.Count == 0)
            {
                return -1;
            }

            int bestEntryIndex = entryCandidates[0];
            int bestDelta = int.MaxValue;
            int bestBayDistance = int.MaxValue;
            for (int i = 0; i < entryCandidates.Count; i++)
            {
                int candidate = entryCandidates[i];
                int delta = path.IsClosedLoop
                    ? GetForwardRouteDelta(candidate, normalizedStart, path.Count, i)
                    : (candidate >= normalizedStart ? candidate - normalizedStart : int.MaxValue / 4);
                int bayDistance = GetCircularIndexDistance(candidate, bayPathIndex, path.Count);
                if (delta < bestDelta || (delta == bestDelta && bayDistance < bestBayDistance))
                {
                    bestEntryIndex = candidate;
                    bestDelta = delta;
                    bestBayDistance = bayDistance;
                }
            }

            return bestEntryIndex;
        }

        private static void AddPullOverEntryCandidate(CocoonTrafficLanePath path, List<int> candidates, int index)
        {
            if (path == null || candidates == null || path.Count == 0)
            {
                return;
            }

            int candidate = path.NormalizeIndex(index);
            if (!path.IsClosedLoop && (index <= 0 || index >= path.Count))
            {
                return;
            }

            if (!candidates.Contains(candidate))
            {
                candidates.Add(candidate);
            }
        }

        private int GetRouteAwarePullOverEntryPathIndex(CocoonTrafficLanePath path, int bayPathIndex, int activeCurrentTarget, int activeTurnaroundIndex)
        {
            if (path == null || path.Count == 0 || bayPathIndex < 0)
            {
                return -1;
            }

            if (path == cruisePath)
            {
                return GetPullOverEntryPathIndex(path, bayPathIndex, activeCurrentTarget);
            }

            int switchTargetIndex = FindSwitchTargetIndexOnPath(path, activeTurnaroundIndex);
            return GetPullOverEntryPathIndex(path, bayPathIndex, switchTargetIndex);
        }

        private int GetPullOverPathIndex(int bayIndex)
        {
            return GetPullOverPathIndexForPath(bayIndex, primaryCruisePath != null ? primaryCruisePath : cruisePath, pullOverPathIndices);
        }

        private int GetPullOverPathIndexForPath(int bayIndex, CocoonTrafficLanePath path, int[] pathIndices)
        {
            Transform bay = pullOverPoints != null && bayIndex >= 0 && bayIndex < pullOverPoints.Length ? pullOverPoints[bayIndex] : null;
            int dynamicValue = FindNearestPathIndex(path, bay);
            int indexedValue = -1;
            if (pathIndices != null && bayIndex >= 0 && bayIndex < pathIndices.Length)
            {
                indexedValue = pathIndices[bayIndex];
            }

            if (dynamicValue >= 0)
            {
                if (path != null && path.Count > 0 && indexedValue >= 0 && indexedValue < path.Count && indexedValue != dynamicValue)
                {
                    Transform anchor = bay != null ? GetPhysicalPullOverAnchor(bay) : null;
                    Vector3 bayPosition = anchor != null ? anchor.position : (bay != null ? bay.position : Vector3.zero);
                    float dynamicDistance = GetWaypointDistanceSqr(path, dynamicValue, bayPosition);
                    float serializedDistance = GetWaypointDistanceSqr(path, indexedValue, bayPosition);
                    float warningDistance = SerializedBayIndexWarningDistanceMeters * GetExperienceScale();
                    int indexGap = GetCircularIndexDistance(indexedValue, dynamicValue, path.Count);
                    if (indexGap > 1 || serializedDistance > dynamicDistance + warningDistance * warningDistance)
                    {
                        CocoonDebugLog.Warn("Pickup", "Bay[" + bayIndex + "] " + (bay != null ? bay.name : "null") +
                            " serialized index " + indexedValue +
                            " is stale for " + GetPathLabel(path) +
                            "; live Pickup Bay Stop resolves to " + dynamicValue +
                            " (gap=" + indexGap +
                            ", serializedDist=" + FormatDisplayMetersFromSqr(serializedDistance) +
                            ", liveDist=" + FormatDisplayMetersFromSqr(dynamicDistance) +
                            "). Using live transform index.", this);
                    }
                }

                return dynamicValue;
            }

            if (path != null && path.Count > 0 && indexedValue >= 0 && indexedValue < path.Count)
            {
                CocoonDebugLog.Warn("Pickup", "Bay[" + bayIndex + "] has no live Pickup Bay Stop transform for " + GetPathLabel(path) +
                    "; falling back to serialized index " + indexedValue + ".", this);
                return indexedValue;
            }

            return -1;
        }

        private int FindNearestCruisePathIndex(Transform stopPoint)
        {
            return FindNearestPathIndex(cruisePath, stopPoint);
        }

        private int FindNearestPathIndex(CocoonTrafficLanePath path, Transform stopPoint)
        {
            if (stopPoint == null)
            {
                return -1;
            }

            Transform anchor = GetPhysicalPullOverAnchor(stopPoint);
            return FindNearestPathIndex(path, anchor != null ? anchor.position : stopPoint.position);
        }

        private int FindNearestPathIndex(CocoonTrafficLanePath path, Vector3 position)
        {
            if (path == null || path.Count == 0)
            {
                return -1;
            }

            int nearestIndex = -1;
            float nearestDistance = float.MaxValue;
            position.y = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                Transform waypoint = path.GetWaypoint(i);
                if (waypoint == null)
                {
                    continue;
                }

                Vector3 waypointPosition = waypoint.position;
                waypointPosition.y = 0f;
                float distance = Vector3.SqrMagnitude(waypointPosition - position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private float GetWaypointDistanceSqr(CocoonTrafficLanePath path, int index, Vector3 position)
        {
            if (path == null || path.Count == 0 || index < 0)
            {
                return float.MaxValue;
            }

            Transform waypoint = path.GetWaypoint(index);
            if (waypoint == null)
            {
                return float.MaxValue;
            }

            Vector3 waypointPosition = waypoint.position;
            waypointPosition.y = 0f;
            position.y = 0f;
            return Vector3.SqrMagnitude(waypointPosition - position);
        }

        private bool IsTaxiNearPathWaypoint(CocoonTrafficLanePath path, int index, float meters, out float distanceMeters)
        {
            distanceMeters = float.MaxValue;
            if (path == null || path.Count == 0 || index < 0)
            {
                return false;
            }

            Vector3 position = taxiRoot != null ? taxiRoot.position : transform.position;
            float distanceSqr = GetWaypointDistanceSqr(path, index, position);
            if (float.IsInfinity(distanceSqr) || distanceSqr >= float.MaxValue * 0.5f)
            {
                return false;
            }

            float distance = Mathf.Sqrt(Mathf.Max(0f, distanceSqr));
            distanceMeters = GetDisplayMeters(distance);
            return distance <= GetScaledArrivalDistance(meters);
        }

        private Transform GetPhysicalPullOverAnchor(Transform stopPoint)
        {
            if (stopPoint == null)
            {
                return null;
            }

            if (stopPoint.name == "Pickup Bay Stop")
            {
                return stopPoint;
            }

            Transform explicitStop = stopPoint.Find("Pickup Bay Stop");
            if (explicitStop != null)
            {
                return explicitStop;
            }

            return stopPoint;
        }

        private int NormalizePathIndex(int index)
        {
            return NormalizePathIndex(cruisePath, index);
        }

        private int NormalizePathIndex(CocoonTrafficLanePath path, int index)
        {
            if (path == null || path.Count == 0)
            {
                return index;
            }

            return path.NormalizeIndex(index);
        }

        private void EnsureRouteCandidates()
        {
            if (primaryCruisePath == null)
            {
                primaryCruisePath = cruisePath;
            }

            if (cruisePath == null)
            {
                cruisePath = primaryCruisePath;
            }

            if (oppositeCruisePath == null)
            {
                oppositeCruisePath = FindOppositeCruisePath();
            }
        }

        private CocoonTrafficLanePath FindOppositeCruisePath()
        {
            CocoonTrafficLanePath referencePath = primaryCruisePath != null ? primaryCruisePath : cruisePath;
            CocoonTrafficLanePath fallback = null;
            CocoonTrafficLanePath[] lanes = FindObjectsOfType<CocoonTrafficLanePath>(true);
            for (int i = 0; i < lanes.Length; i++)
            {
                CocoonTrafficLanePath lane = lanes[i];
                if (lane == null || lane == referencePath)
                {
                    continue;
                }

                string laneName = lane.name.ToLowerInvariant();
                if (laneName.Contains("reverse") || laneName.Contains("opposite"))
                {
                    return lane;
                }

                if (fallback == null)
                {
                    fallback = lane;
                }
            }

            return fallback;
        }

        private int CountPullOverPoints()
        {
            return pullOverPoints == null ? (pullOverPoint != null ? 1 : 0) : pullOverPoints.Length;
        }

        private void EnsureTrafficParticipant()
        {
            if (trafficParticipant != null)
            {
                return;
            }

            Transform root = taxiRoot != null ? taxiRoot : transform;
            trafficParticipant = root.GetComponent<CocoonTrafficParticipant>();
            if (trafficParticipant == null)
            {
                trafficParticipant = root.gameObject.AddComponent<CocoonTrafficParticipant>();
            }

            trafficParticipant.Configure(cruisePath, root, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), cruiseTargetIndex);
            if (roadGraph != null && taxiGraphCursor.IsValid)
            {
                trafficParticipant.ConfigureGraphIfNeeded(roadGraph, root, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
            }
        }

        private float GetExperienceScale()
        {
            return CocoonExperienceScale.RoadmapScale;
        }

        private float GetDisplayMeters(float worldDistance)
        {
            float scale = Mathf.Max(GetExperienceScale(), 0.0001f);
            return worldDistance / scale;
        }

        private float GetCruiseMoveSpeed()
        {
            return Mathf.Max(cruiseSpeed, MinimumTaxiCruiseSpeed);
        }

        private float GetHailMoveSpeed()
        {
            return Mathf.Max(detectedSpeed, MinimumTaxiHailSpeed);
        }

        private float GetPullOverMoveSpeed()
        {
            return Mathf.Max(pullOverSpeed, MinimumTaxiPullOverSpeed);
        }

        private float GetParkingMoveSpeed(float multiplier, float minimumSpeed)
        {
            return Mathf.Max(GetPullOverMoveSpeed() * Mathf.Max(0f, multiplier), minimumSpeed);
        }

        private float GetScaledTaxiTrafficLength()
        {
            return Mathf.Max(0.2f, TaxiTrafficLength * GetExperienceScale());
        }

        private float GetScaledTaxiTrafficWidth()
        {
            return Mathf.Max(0.08f, TaxiTrafficWidth * GetExperienceScale());
        }

        private float GetScaledArrivalDistance(float meters)
        {
            return Mathf.Max(0.006f, meters * GetExperienceScale());
        }

        private void FaceTowards(Vector3 direction)
        {
            FaceTowards(direction, 420f);
        }

        private void FaceTowards(Vector3 direction, float degreesPerSecond)
        {
            direction.y = 0f;
            if (taxiRoot == null || direction.sqrMagnitude < 0.00001f)
            {
                return;
            }

            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            taxiRoot.rotation = Quaternion.RotateTowards(taxiRoot.rotation, lookRotation, Mathf.Max(1f, degreesPerSecond) * Time.deltaTime);
        }

        private void SetFacing(Vector3 direction)
        {
            direction.y = 0f;
            if (taxiRoot == null || direction.sqrMagnitude < 0.00001f)
            {
                return;
            }

            taxiRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void CacheMaterialInstances()
        {
            if (enableBodyLightVisuals)
            {
                ResolveNamedLightRenderers();

                if (lightRenderers != null)
                {
                    lightMaterials = new Material[lightRenderers.Length];
                    for (int i = 0; i < lightRenderers.Length; i++)
                    {
                        if (lightRenderers[i] != null)
                        {
                            lightMaterials[i] = CreateRuntimeMaterial(lightRenderers[i], true, ExteriorAvailableGreen, 1.8f);
                        }
                    }
                }

                if (bodyRenderer != null)
                {
                    bodyMaterial = CreateRuntimeMaterial(bodyRenderer, false, new Color(0.82f, 0.88f, 0.86f), 0f);
                }
            }
            else
            {
                lightMaterials = null;
                bodyMaterial = null;
            }

            if (paymentReaderRenderer != null)
            {
                paymentReaderMaterial = CreateRuntimeMaterial(paymentReaderRenderer, true, new Color(0.1f, 1f, 0.55f), 1.7f);
            }
        }

        private void ResolveNamedLightRenderers()
        {
            if (!enableBodyLightVisuals)
            {
                return;
            }

            Renderer[] namedRenderers = new Renderer[TaxiLightRendererNames.Length];
            int foundCount = 0;
            for (int i = 0; i < TaxiLightRendererNames.Length; i++)
            {
                namedRenderers[i] = FindNamedLightRenderer(TaxiLightRendererNames[i]);
                if (namedRenderers[i] != null)
                {
                    foundCount++;
                }
            }

            if (foundCount != TaxiLightRendererNames.Length)
            {
                if (!hasLoggedMissingNamedLightBinding && foundCount > 0)
                {
                    hasLoggedMissingNamedLightBinding = true;
                    CocoonDebugLog.Info("TaxiDisplay", "Named light binding found " + foundCount + "/" + TaxiLightRendererNames.Length + " requested taxi light(s); keeping serialized renderer list.", this);
                }

                return;
            }

            if (!ShouldReplaceLightRenderers(namedRenderers))
            {
                return;
            }

            lightRenderers = namedRenderers;
            lightMaterials = null;
            if (!hasLoggedNamedLightBinding)
            {
                hasLoggedNamedLightBinding = true;
                CocoonDebugLog.Info("TaxiDisplay", "Named taxi lights bound: " + string.Join(", ", TaxiLightRendererNames) + ".", this);
            }
        }

        private bool ShouldReplaceLightRenderers(Renderer[] namedRenderers)
        {
            if (lightRenderers == null || lightRenderers.Length != namedRenderers.Length)
            {
                return true;
            }

            for (int i = 0; i < namedRenderers.Length; i++)
            {
                if (lightRenderers[i] != namedRenderers[i])
                {
                    return true;
                }
            }

            return false;
        }

        private Renderer FindNamedLightRenderer(string targetName)
        {
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            Renderer renderer = FindNamedLightRenderer(searchRoot, targetName);
            if (renderer != null)
            {
                return renderer;
            }

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || !candidate.gameObject.scene.isLoaded || !NamesMatch(candidate.name, targetName))
                {
                    continue;
                }

                renderer = candidate.GetComponent<Renderer>() ?? candidate.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                {
                    return renderer;
                }
            }

            return null;
        }

        private static Renderer FindNamedLightRenderer(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || !NamesMatch(candidate.name, targetName))
                {
                    continue;
                }

                Renderer renderer = candidate.GetComponent<Renderer>() ?? candidate.GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                {
                    return renderer;
                }
            }

            return null;
        }

        private static bool NamesMatch(string candidate, string target)
        {
            return string.Equals(candidate != null ? candidate.Trim() : string.Empty, target, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyExteriorScreenLights(CocoonTaxiExteriorScreenState screenState, float intensity, string context)
        {
            SetLights(GetExteriorScreenColor(screenState), intensity, GetExteriorScreenLightMode(screenState, context));
        }

        private void HandleExteriorScreenKeyboardBackdoor()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                ApplyExteriorScreenBackdoor(CocoonTaxiExteriorScreenState.OutOfService, "1");
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                ApplyExteriorScreenBackdoor(CocoonTaxiExteriorScreenState.TwoSeatsAvailable, "2");
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                ApplyExteriorScreenBackdoor(CocoonTaxiExteriorScreenState.Full, "3");
            }
        }

        private void ApplyExteriorScreenBackdoor(CocoonTaxiExteriorScreenState screenState, string key)
        {
            if (IsAvailabilityCapacityScreen(screenState))
            {
                availabilityExteriorScreenState = screenState;
            }

            SetLightBlinking(false);
            ApplyExteriorScreenLights(screenState, GetExteriorScreenDebugIntensity(screenState), "KEY " + key);
            TryApplyExteriorScreenState(screenState, out _);
            CocoonDebugLog.Info("TaxiDisplay", "Keyboard backdoor " + key + " -> " + screenState + ".", this);
        }

        private static float GetExteriorScreenDebugIntensity(CocoonTaxiExteriorScreenState screenState)
        {
            switch (screenState)
            {
                case CocoonTaxiExteriorScreenState.OutOfService:
                    return 2.4f;
                case CocoonTaxiExteriorScreenState.TwoSeatsAvailable:
                    return 3.0f;
                case CocoonTaxiExteriorScreenState.Full:
                    return 3.2f;
                default:
                    return 2.6f;
            }
        }

        private static Color GetExteriorScreenColor(CocoonTaxiExteriorScreenState screenState)
        {
            switch (screenState)
            {
                case CocoonTaxiExteriorScreenState.DetectedHold:
                    return ExteriorDetectedMintBlue;
                case CocoonTaxiExteriorScreenState.Goodbye:
                case CocoonTaxiExteriorScreenState.OutOfService:
                    return ExteriorWhite;
                case CocoonTaxiExteriorScreenState.TwoSeatsAvailable:
                    return ExteriorTwoSeatsYellow;
                case CocoonTaxiExteriorScreenState.Full:
                    return ExteriorFullRed;
                case CocoonTaxiExteriorScreenState.Available:
                case CocoonTaxiExteriorScreenState.StoppingFollowMe:
                default:
                    return ExteriorAvailableGreen;
            }
        }

        private static string GetExteriorScreenLightMode(CocoonTaxiExteriorScreenState screenState, string context)
        {
            switch (screenState)
            {
                case CocoonTaxiExteriorScreenState.DetectedHold:
                    return "MINT BLUE / DETECTED";
                case CocoonTaxiExteriorScreenState.StoppingFollowMe:
                    return "GREEN / STOPPING";
                case CocoonTaxiExteriorScreenState.Goodbye:
                    return "WHITE / GOODBYE";
                case CocoonTaxiExteriorScreenState.OutOfService:
                    return "WHITE / OUT OF SERVICE";
                case CocoonTaxiExteriorScreenState.TwoSeatsAvailable:
                    return "YELLOW / 2 SEATS AVAILABLE";
                case CocoonTaxiExteriorScreenState.Full:
                    return "RED / FULL";
                case CocoonTaxiExteriorScreenState.Available:
                default:
                    return "GREEN / " + (string.IsNullOrEmpty(context) ? "AVAILABLE" : context);
            }
        }

        private void SetLights(Color color, float intensity, string mode)
        {
            currentLightColor = color;
            currentLightIntensity = intensity;
            if (currentLightMode != mode)
            {
                currentLightMode = mode;
                if (enableBodyLightVisuals)
                {
                    CocoonDebugLog.Info("TaxiDisplay", "Body lights -> " + mode + " color=(" + color.r.ToString("0.00") + "," + color.g.ToString("0.00") + "," + color.b.ToString("0.00") + ") intensity=" + intensity.ToString("0.0") + " renderers=" + CountActiveLightRenderers() + ".", this);
                }
            }

            if (!enableBodyLightVisuals)
            {
                return;
            }

            ApplyLightAppearance(color, intensity);
        }

        private void SetLightBlinking(bool blinking)
        {
            if (lightBlinking == blinking)
            {
                return;
            }

            lightBlinking = blinking;
            if (!enableBodyLightVisuals)
            {
                return;
            }

            CocoonDebugLog.Info("TaxiDisplay", "Body light blinking " + (lightBlinking ? "enabled" : "disabled") + ".", this);
            if (!lightBlinking)
            {
                ApplyLightAppearance(currentLightColor, currentLightIntensity);
            }
        }

        private void UpdateBlinkingLights()
        {
            if (!enableBodyLightVisuals || !lightBlinking)
            {
                return;
            }

            float blinkOn = Mathf.PingPong(Time.time * 4.6f, 1f) > 0.38f ? 1f : 0.18f;
            Color dimColor = Color.Lerp(Color.black, currentLightColor, 0.18f);
            Color blinkColor = Color.Lerp(dimColor, currentLightColor, blinkOn);
            float blinkIntensity = Mathf.Lerp(0.35f, currentLightIntensity, blinkOn);
            ApplyLightAppearance(blinkColor, blinkIntensity);
        }

        private void SetWindshieldMessage(string message, Color color)
        {
            ResolveWindshieldPanelReferences();
            if (!HasAnyWindshieldDisplay())
            {
                return;
            }

            string displayMessage = message.ToUpperInvariant();
            CocoonTaxiExteriorScreenState screenState = ResolveExteriorScreenState(displayMessage);
            Color displayColor = GetExteriorScreenColor(screenState);
            if (TryApplyExteriorScreenState(screenState, out bool imageChanged))
            {
                if (imageChanged)
                {
                    CocoonDebugLog.Info("TaxiDisplay", "Windshield image -> " + screenState + ".", this);
                }

                return;
            }

            string renderMessage = displayMessage == "CONFIRMED! STOPPING NOW"
                ? "CONFIRMED!\nSTOPPING NOW"
                : displayMessage;
            SetExteriorScreenImageActive(windshieldImage, false);
            SetExteriorScreenImageActive(rearWindshieldImage, false);
            bool changed = ApplyWindshieldMessage(windshieldText, renderMessage, displayMessage, displayColor);
            changed |= ApplyWindshieldMessage(rearWindshieldText, renderMessage, displayMessage, displayColor);
            if (changed)
            {
                CocoonDebugLog.Info("TaxiDisplay", "Windshield message -> " + displayMessage + ".", this);
            }
        }

        private bool TryApplyExteriorScreenState(CocoonTaxiExteriorScreenState screenState, out bool changed)
        {
            changed = false;
            Texture2D texture = GetExteriorScreenTexture(screenState);
            if (texture == null)
            {
                if (!hasLoggedMissingExteriorScreenTexture)
                {
                    hasLoggedMissingExteriorScreenTexture = true;
                    CocoonDebugLog.Info("TaxiDisplay", "Exterior screen texture missing for " + screenState + "; using text fallback.", this);
                }

                return false;
            }

            ResolveWindshieldPanelReferences();
            DisableUnifiedDisplayRenderer(frontUnifiedDisplayRenderer);
            DisableUnifiedDisplayRenderer(rearUnifiedDisplayRenderer);

            bool applied = false;
            applied |= ApplyExteriorScreenTexture(windshieldPanel, windshieldText, ref windshieldImage, texture);
            applied |= ApplyExteriorScreenTexture(rearWindshieldPanel, rearWindshieldText, ref rearWindshieldImage, texture);

            if (!applied)
            {
                return false;
            }

            changed = currentExteriorScreenState != screenState;
            currentExteriorScreenState = screenState;
            return true;
        }

        private Texture2D GetExteriorScreenTexture(CocoonTaxiExteriorScreenState screenState)
        {
            Texture2D texture = GetSerializedExteriorScreenTexture(screenState);
            if (texture != null)
            {
                return texture;
            }

            int index = Mathf.Clamp((int)screenState, 0, ExteriorScreenResourceNames.Length - 1);
            return Resources.Load<Texture2D>(ExteriorScreenResourceFolder + ExteriorScreenResourceNames[index]);
        }

        private Texture2D GetSerializedExteriorScreenTexture(CocoonTaxiExteriorScreenState screenState)
        {
            switch (screenState)
            {
                case CocoonTaxiExteriorScreenState.Available:
                    return availableExteriorScreen;
                case CocoonTaxiExteriorScreenState.DetectedHold:
                    return detectedExteriorScreen;
                case CocoonTaxiExteriorScreenState.StoppingFollowMe:
                    return stoppingFollowMeExteriorScreen;
                case CocoonTaxiExteriorScreenState.Goodbye:
                    return goodbyeExteriorScreen;
                case CocoonTaxiExteriorScreenState.OutOfService:
                    return outOfServiceExteriorScreen;
                case CocoonTaxiExteriorScreenState.TwoSeatsAvailable:
                    return twoSeatsAvailableExteriorScreen;
                case CocoonTaxiExteriorScreenState.Full:
                    return fullExteriorScreen;
                default:
                    return availableExteriorScreen;
            }
        }

        private bool ApplyExteriorScreenTexture(RectTransform panel, Text legacyText, ref RawImage image, Texture2D texture)
        {
            if (panel == null && legacyText != null)
            {
                panel = legacyText.transform.parent as RectTransform;
            }

            if (panel == null)
            {
                return false;
            }

            SetWindshieldPanelActive(panel, true);
            ConfigureExteriorPanelQuality(panel);
            ConfigureExteriorScreenTexture(texture);
            image = EnsureExteriorScreenImage(panel, image);
            if (image == null)
            {
                return false;
            }

            image.texture = texture;
            image.color = Color.white;
            image.material = GetExteriorScreenRuntimeMaterial();
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            image.raycastTarget = false;
            image.enabled = true;
            image.gameObject.SetActive(true);
            FitExteriorScreenPanel(panel, image.rectTransform, texture);
            DisableLegacyWindshieldMessageBlock(panel, legacyText);
            return true;
        }

        private void ConfigureExteriorPanelQuality(RectTransform panel)
        {
            if (panel == null)
            {
                return;
            }

            Canvas canvas = panel.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.pixelPerfect = false;
                if (canvas.worldCamera == null)
                {
                    canvas.worldCamera = Camera.main;
                }
            }

            CanvasScaler scaler = panel.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = Mathf.Max(768f, exteriorScreenPixelsPerUnit);
                scaler.referencePixelsPerUnit = 100f;
            }
        }

        private void ConfigureExteriorScreenTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.wrapModeU = TextureWrapMode.Clamp;
            texture.wrapModeV = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = Mathf.Max(texture.anisoLevel, 16);
            if (texture.mipmapCount > 1)
            {
                texture.mipMapBias = Mathf.Min(texture.mipMapBias, exteriorScreenMipMapBias);
            }
        }

        private Material GetExteriorScreenRuntimeMaterial()
        {
            if (!useHighReadabilityExteriorScreenMaterial)
            {
                return null;
            }

            if (exteriorScreenRuntimeMaterial != null)
            {
                return exteriorScreenRuntimeMaterial;
            }

            Shader shader = Shader.Find("UI/Default");
            if (shader == null)
            {
                return null;
            }

            exteriorScreenRuntimeMaterial = new Material(shader)
            {
                name = "Cocoon Exterior Screen Runtime UI",
                hideFlags = HideFlags.DontSave
            };

            if (exteriorScreenRuntimeMaterial.HasProperty(ColorId))
            {
                exteriorScreenRuntimeMaterial.SetColor(ColorId, Color.white);
            }

            return exteriorScreenRuntimeMaterial;
        }

        private bool ApplyUnifiedExteriorScreenTexture(
            bool front,
            Texture2D texture,
            ref MeshRenderer displayRenderer,
            ref Material runtimeMaterial)
        {
            if (texture == null ||
                !EnsureUnifiedDisplayRenderer(front, texture, ref displayRenderer))
            {
                return false;
            }

            Material material = EnsureUnifiedDisplayRuntimeMaterial(displayRenderer, ref runtimeMaterial, front);
            if (material == null)
            {
                return false;
            }

            ApplyTextureToDisplayMaterial(material, texture);
            displayRenderer.enabled = true;
            displayRenderer.sharedMaterial = material;
            return true;
        }

        private bool EnsureUnifiedDisplayRenderer(bool front, Texture2D texture, ref MeshRenderer displayRenderer)
        {
            Transform interior = ResolveFinalTaxiInteriorStructure();
            if (interior == null)
            {
                return false;
            }

            Transform[] sources = ResolveUnifiedDisplaySources(front, interior);
            if (!HasAnyTransform(sources))
            {
                LogMissingUnifiedDisplaySources(front);
                return false;
            }

            string displayName = front ? FrontUnifiedDisplayName : RearUnifiedDisplayName;
            displayRenderer = ResolveUnifiedDisplayRenderer(interior, displayRenderer, displayName);
            if (displayRenderer == null)
            {
                return false;
            }

            bool built = front ? hasBuiltFrontUnifiedDisplayMesh : hasBuiltRearUnifiedDisplayMesh;
            float textureAspect = GetExteriorScreenAspect(texture);
            float builtAspect = front ? frontUnifiedDisplayMeshAspect : rearUnifiedDisplayMeshAspect;
            bool aspectChanged = built && Mathf.Abs(builtAspect - textureAspect) > 0.01f;
            if (!built || aspectChanged)
            {
                Vector3 expectedLocalNormal = front ? Vector3.down : Vector3.up;
                bool usedExtractedMesh = TryBuildExtractedUnifiedDisplayMesh(
                    sources,
                    interior,
                    expectedLocalNormal,
                    textureAspect,
                    displayName,
                    out Mesh mesh,
                    out int triangleCount,
                    out float fixedLength,
                    out float adjustedWidth,
                    out float topAlignedV,
                    out Vector3 averageNormal,
                    out string extractionFailureReason);

                if (!usedExtractedMesh)
                {
                    LogUnreadableUnifiedDisplayMesh(front, extractionFailureReason);
                    if (!TryBuildFallbackUnifiedDisplayMesh(
                            sources,
                            interior,
                            expectedLocalNormal,
                            textureAspect,
                            displayName,
                            out mesh,
                            out triangleCount,
                            out fixedLength,
                            out adjustedWidth,
                            out topAlignedV,
                            out averageNormal))
                    {
                        LogMissingUnifiedDisplaySources(front);
                        return false;
                    }

                    LogUnifiedDisplayFallback(front, extractionFailureReason);
                }

                MeshFilter filter = displayRenderer.GetComponent<MeshFilter>();
                if (filter == null)
                {
                    filter = displayRenderer.gameObject.AddComponent<MeshFilter>();
                }

                filter.sharedMesh = mesh;
                if (front)
                {
                    hasBuiltFrontUnifiedDisplayMesh = true;
                    frontUnifiedDisplayMeshAspect = textureAspect;
                }
                else
                {
                    hasBuiltRearUnifiedDisplayMesh = true;
                    rearUnifiedDisplayMeshAspect = textureAspect;
                }

                CocoonDebugLog.Info(
                    "TaxiDisplay",
                    (front ? "Front LED" : "Rear OLED") +
                    " unified display mesh ready: source=" +
                    (usedExtractedMesh ? "screen faces" : "bounds fallback") +
                    ", vertices=" + mesh.vertexCount +
                    ", triangles=" + triangleCount +
                    ", visibleSide=" + (front ? "-Y" : "+Y") +
                    ", normal=" + averageNormal.ToString("F2") +
                    ", aspect=" + textureAspect.ToString("0.###") +
                    ", topAlignedV=" + topAlignedV.ToString("0.###") +
                    ", fixedLength=" + fixedLength.ToString("0.###") +
                    "m, adjustedWidth=" + adjustedWidth.ToString("0.###") + "m.",
                    this);
            }

            LogUnifiedDisplayBinding(front, sources, displayRenderer);
            return true;
        }

        private Transform[] ResolveUnifiedDisplaySources(bool front, Transform interior)
        {
            if (front)
            {
                frontLed1 = ResolveExactNamedReference(interior, frontLed1, FrontLed1Name);
                frontLed2 = ResolveExactNamedReference(interior, frontLed2, FrontLed2Name);
                frontLed3 = ResolveExactNamedReference(interior, frontLed3, FrontLed3Name);
                return new[] { frontLed1, frontLed2, frontLed3 };
            }

            rearOled1 = ResolveExactNamedReference(interior, rearOled1, RearOled1Name);
            rearOled2 = ResolveExactNamedReference(interior, rearOled2, RearOled2Name);
            rearOled3 = ResolveExactNamedReference(interior, rearOled3, RearOled3Name);

            // Migration from the earlier frontOled fields, which pointed at the rear oled targets.
            if (rearOled1 == null)
            {
                rearOled1 = ResolveExactNamedReference(interior, frontOled1, RearOled1Name);
            }

            if (rearOled2 == null)
            {
                rearOled2 = ResolveExactNamedReference(interior, frontOled2, RearOled2Name);
            }

            if (rearOled3 == null)
            {
                rearOled3 = ResolveExactNamedReference(interior, frontOled3, RearOled3Name);
            }

            return new[] { rearOled1, rearOled2, rearOled3 };
        }

        private static MeshRenderer ResolveUnifiedDisplayRenderer(Transform parent, MeshRenderer current, string displayName)
        {
            Transform existing = current != null && current.name == displayName
                ? current.transform
                : FindExactNamedDescendant(parent, displayName);
            MeshRenderer renderer = existing != null ? existing.GetComponent<MeshRenderer>() : null;
            if (renderer != null)
            {
                if (renderer.transform.parent != parent)
                {
                    renderer.transform.SetParent(parent, false);
                }

                renderer.transform.localPosition = Vector3.zero;
                renderer.transform.localRotation = Quaternion.identity;
                renderer.transform.localScale = Vector3.one;
                if (renderer.GetComponent<MeshFilter>() == null)
                {
                    renderer.gameObject.AddComponent<MeshFilter>();
                }

                return renderer;
            }

            GameObject displayObject = existing != null ? existing.gameObject : new GameObject(displayName);
            if (existing == null)
            {
                displayObject.transform.SetParent(parent, false);
            }
            else if (displayObject.transform.parent != parent)
            {
                displayObject.transform.SetParent(parent, false);
            }

            displayObject.transform.localPosition = Vector3.zero;
            displayObject.transform.localRotation = Quaternion.identity;
            displayObject.transform.localScale = Vector3.one;

            if (displayObject.GetComponent<MeshFilter>() == null)
            {
                displayObject.AddComponent<MeshFilter>();
            }

            return displayObject.GetComponent<MeshRenderer>() ?? displayObject.AddComponent<MeshRenderer>();
        }

        private Material EnsureUnifiedDisplayRuntimeMaterial(MeshRenderer renderer, ref Material runtimeMaterial, bool front)
        {
            if (runtimeMaterial != null)
            {
                return runtimeMaterial;
            }

            Material source = renderer != null ? renderer.sharedMaterial : null;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = source != null ? source.shader : Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            runtimeMaterial = source != null ? new Material(source) : new Material(shader);
            runtimeMaterial.name = front ? "Cocoon Front LED Unified Runtime" : "Cocoon Rear OLED Unified Runtime";
            ConfigureDisplayRuntimeMaterial(runtimeMaterial, front);
            return runtimeMaterial;
        }

        private static void ConfigureDisplayRuntimeMaterial(Material material, bool front)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, Color.white);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, Color.white);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.72f);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 2f);
            }

            if (material.HasProperty("_CullMode"))
            {
                material.SetFloat("_CullMode", 2f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 1f);
            }

            if (material.HasProperty(EmissionColorId))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, Color.white * 1.5f);
            }
        }

        private static void ApplyTextureToDisplayMaterial(Material material, Texture2D texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_EmissionMap"))
            {
                material.SetTexture("_EmissionMap", texture);
            }
        }

        private static bool TryBuildExtractedUnifiedDisplayMesh(
            Transform[] sources,
            Transform meshSpace,
            Vector3 expectedLocalNormal,
            float textureAspect,
            string meshName,
            out Mesh mesh,
            out int triangleCount,
            out float fixedLength,
            out float adjustedWidth,
            out float topAlignedV,
            out Vector3 averageNormal,
            out string failureReason)
        {
            mesh = null;
            triangleCount = 0;
            fixedLength = 0f;
            adjustedWidth = 0f;
            topAlignedV = 0f;
            averageNormal = Vector3.zero;
            failureReason = string.Empty;
            if (sources == null || meshSpace == null)
            {
                failureReason = "missing source transforms or mesh space";
                return false;
            }

            Vector3 expectedWorldNormal = meshSpace.TransformDirection(expectedLocalNormal.normalized);
            var vertices = new List<Vector3>(96);
            var triangles = new List<int>(96);

            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                Transform source = sources[sourceIndex];
                if (source == null)
                {
                    continue;
                }

                MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
                for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
                {
                    MeshFilter filter = filters[filterIndex];
                    Mesh sourceMesh = filter != null ? filter.sharedMesh : null;
                    Renderer renderer = filter != null ? filter.GetComponent<Renderer>() : null;
                    if (sourceMesh == null || renderer == null)
                    {
                        continue;
                    }

                    Material[] materials = renderer.sharedMaterials;
                    int subMeshCount = Mathf.Min(sourceMesh.subMeshCount, materials.Length);
                    for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                    {
                        if (!IsScreenDisplayMaterial(materials[subMesh]))
                        {
                            continue;
                        }

                        if (!sourceMesh.isReadable)
                        {
                            failureReason = "screen mesh '" + sourceMesh.name + "' under " + GetTransformPath(filter.transform) + " is not readable";
                            return false;
                        }

                        Vector3[] sourceVertices = sourceMesh.vertices;
                        int[] sourceTriangles;
                        try
                        {
                            sourceTriangles = sourceMesh.GetTriangles(subMesh);
                        }
                        catch (Exception)
                        {
                            failureReason = "screen mesh '" + sourceMesh.name + "' triangles could not be read";
                            return false;
                        }

                        for (int tri = 0; tri + 2 < sourceTriangles.Length; tri += 3)
                        {
                            Vector3 worldA = filter.transform.TransformPoint(sourceVertices[sourceTriangles[tri]]);
                            Vector3 worldB = filter.transform.TransformPoint(sourceVertices[sourceTriangles[tri + 1]]);
                            Vector3 worldC = filter.transform.TransformPoint(sourceVertices[sourceTriangles[tri + 2]]);
                            Vector3 normal = Vector3.Cross(worldB - worldA, worldC - worldA).normalized;
                            if (Vector3.Dot(normal, expectedWorldNormal) < 0.35f)
                            {
                                continue;
                            }

                            int baseIndex = vertices.Count;
                            Vector3 offset = expectedWorldNormal * UnifiedDisplaySurfaceOffset;
                            vertices.Add(meshSpace.InverseTransformPoint(worldA + offset));
                            vertices.Add(meshSpace.InverseTransformPoint(worldB + offset));
                            vertices.Add(meshSpace.InverseTransformPoint(worldC + offset));
                            triangles.Add(baseIndex);
                            triangles.Add(baseIndex + 1);
                            triangles.Add(baseIndex + 2);
                        }
                    }
                }
            }

            if (vertices.Count < 3 || triangles.Count < 3)
            {
                failureReason = "no readable screen-material faces matched visible side " + (expectedLocalNormal.y < 0f ? "-Y" : "+Y");
                return false;
            }

            mesh = BuildUnifiedDisplayMesh(
                meshName,
                vertices,
                triangles,
                expectedLocalNormal,
                textureAspect,
                out fixedLength,
                out adjustedWidth,
                out topAlignedV,
                out averageNormal);
            triangleCount = triangles.Count / 3;
            return true;
        }

        private static bool TryBuildFallbackUnifiedDisplayMesh(
            Transform[] sources,
            Transform meshSpace,
            Vector3 expectedLocalNormal,
            float textureAspect,
            string meshName,
            out Mesh mesh,
            out int triangleCount,
            out float fixedLength,
            out float adjustedWidth,
            out float topAlignedV,
            out Vector3 averageNormal)
        {
            mesh = null;
            triangleCount = 0;
            fixedLength = 0f;
            adjustedWidth = 0f;
            topAlignedV = 0f;
            averageNormal = Vector3.zero;
            if (sources == null || meshSpace == null)
            {
                return false;
            }

            bool hasBounds = false;
            Bounds localBounds = new Bounds();
            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                Transform source = sources[sourceIndex];
                if (source == null)
                {
                    continue;
                }

                Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        continue;
                    }

                    EncapsulateRendererWorldBoundsAsLocal(renderers[i].bounds, meshSpace, ref localBounds, ref hasBounds);
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            Vector3 normal = expectedLocalNormal.normalized;
            float y = normal.y >= 0f ? localBounds.max.y : localBounds.min.y;
            y += normal.y * UnifiedDisplaySurfaceOffset;

            var vertices = new List<Vector3>(4)
            {
                new Vector3(localBounds.min.x, y, localBounds.min.z),
                new Vector3(localBounds.max.x, y, localBounds.min.z),
                new Vector3(localBounds.max.x, y, localBounds.max.z),
                new Vector3(localBounds.min.x, y, localBounds.max.z)
            };

            var triangles = new List<int>(6) { 0, 1, 2, 0, 2, 3 };
            if (Vector3.Dot(Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]).normalized, normal) < 0f)
            {
                triangles[1] = 2;
                triangles[2] = 1;
                triangles[4] = 3;
                triangles[5] = 2;
            }

            mesh = BuildUnifiedDisplayMesh(
                meshName,
                vertices,
                triangles,
                expectedLocalNormal,
                textureAspect,
                out fixedLength,
                out adjustedWidth,
                out topAlignedV,
                out averageNormal);
            triangleCount = triangles.Count / 3;
            return true;
        }

        private static Mesh BuildUnifiedDisplayMesh(
            string meshName,
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 expectedLocalNormal,
            float textureAspect,
            out float fixedLength,
            out float adjustedWidth,
            out float topAlignedV,
            out Vector3 averageNormal)
        {
            FitUnifiedDisplayVerticesToTextureAspect(vertices, expectedLocalNormal, textureAspect, out fixedLength, out adjustedWidth, out topAlignedV);
            EnsureUnifiedDisplayTriangleWinding(vertices, triangles, expectedLocalNormal);
            averageNormal = CalculateAverageNormal(vertices, triangles);

            var mesh = new Mesh
            {
                name = meshName + " Mesh"
            };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, BuildUnifiedDisplayUvs(vertices, expectedLocalNormal));
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void FitUnifiedDisplayVerticesToTextureAspect(
            List<Vector3> vertices,
            Vector3 expectedLocalNormal,
            float textureAspect,
            out float fixedLength,
            out float adjustedWidth,
            out float topAlignedV)
        {
            fixedLength = 0f;
            adjustedWidth = 0f;
            topAlignedV = 0f;
            if (vertices == null || vertices.Count == 0)
            {
                return;
            }

            textureAspect = textureAspect > 0.001f ? textureAspect : ExteriorScreenSourceWidth / ExteriorScreenSourceHeight;
            GetUnifiedDisplayUvAxes(expectedLocalNormal, out Vector3 axisU, out Vector3 axisV);
            float minU = float.PositiveInfinity;
            float maxU = float.NegativeInfinity;
            float minV = float.PositiveInfinity;
            float maxV = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                float u = Vector3.Dot(vertices[i], axisU);
                float v = Vector3.Dot(vertices[i], axisV);
                minU = Mathf.Min(minU, u);
                maxU = Mathf.Max(maxU, u);
                minV = Mathf.Min(minV, v);
                maxV = Mathf.Max(maxV, v);
            }

            fixedLength = Mathf.Max(0.0001f, maxU - minU);
            float currentWidth = Mathf.Max(0.0001f, maxV - minV);
            adjustedWidth = fixedLength / textureAspect;
            topAlignedV = maxV;
            float scale = adjustedWidth / currentWidth;
            for (int i = 0; i < vertices.Count; i++)
            {
                float currentV = Vector3.Dot(vertices[i], axisV);
                float targetV = topAlignedV - (topAlignedV - currentV) * scale;
                vertices[i] += axisV * (targetV - currentV);
            }
        }

        private static void EnsureUnifiedDisplayTriangleWinding(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 expectedLocalNormal)
        {
            Vector3 targetNormal = expectedLocalNormal.normalized;
            Vector3 averageNormal = CalculateAverageNormal(vertices, triangles);
            if (Vector3.Dot(averageNormal, targetNormal) >= 0f)
            {
                return;
            }

            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                int temp = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temp;
            }
        }

        private static Vector3 CalculateAverageNormal(List<Vector3> vertices, List<int> triangles)
        {
            if (vertices == null || triangles == null)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertices.Count || b >= vertices.Count || c >= vertices.Count)
                {
                    continue;
                }

                Vector3 ab = vertices[b] - vertices[a];
                Vector3 ac = vertices[c] - vertices[a];
                Vector3 normal = Vector3.Cross(ab, ac);
                if (normal.sqrMagnitude > 0.0000001f)
                {
                    sum += normal.normalized;
                }
            }

            return sum.sqrMagnitude > 0.0000001f ? sum.normalized : Vector3.zero;
        }

        private static List<Vector2> BuildUnifiedDisplayUvs(List<Vector3> vertices, Vector3 expectedLocalNormal)
        {
            GetUnifiedDisplayUvAxes(expectedLocalNormal, out Vector3 axisU, out Vector3 axisV);

            float minU = float.PositiveInfinity;
            float maxU = float.NegativeInfinity;
            float minV = float.PositiveInfinity;
            float maxV = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                float u = Vector3.Dot(vertices[i], axisU);
                float v = Vector3.Dot(vertices[i], axisV);
                minU = Mathf.Min(minU, u);
                maxU = Mathf.Max(maxU, u);
                minV = Mathf.Min(minV, v);
                maxV = Mathf.Max(maxV, v);
            }

            float width = Mathf.Max(0.0001f, maxU - minU);
            float height = Mathf.Max(0.0001f, maxV - minV);
            var uvs = new List<Vector2>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                float u = (Vector3.Dot(vertices[i], axisU) - minU) / width;
                float v = (Vector3.Dot(vertices[i], axisV) - minV) / height;
                uvs.Add(new Vector2(u, v));
            }

            return uvs;
        }

        private static void GetUnifiedDisplayUvAxes(Vector3 expectedLocalNormal, out Vector3 axisU, out Vector3 axisV)
        {
            Vector3 normal = expectedLocalNormal.normalized;
            if (Mathf.Abs(normal.y) >= Mathf.Abs(normal.x) && Mathf.Abs(normal.y) >= Mathf.Abs(normal.z))
            {
                axisU = Vector3.right;
                axisV = Vector3.forward;
            }
            else if (Mathf.Abs(normal.z) >= Mathf.Abs(normal.x))
            {
                axisU = Vector3.right;
                axisV = Vector3.up;
            }
            else
            {
                axisU = Vector3.forward;
                axisV = Vector3.up;
            }
        }

        private static void EncapsulateRendererWorldBoundsAsLocal(
            Bounds worldBounds,
            Transform localSpace,
            ref Bounds localBounds,
            ref bool hasBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            var corners = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 local = localSpace.InverseTransformPoint(corners[i]);
                if (!hasBounds)
                {
                    localBounds = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(local);
                }
            }
        }

        private static bool IsScreenDisplayMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            string materialName = material.name ?? string.Empty;
            return materialName.IndexOf(OledMaterialNameFragment, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf(OledChineseMaterialNameFragment, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("\u93C4\u5267", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   materialName.IndexOf("\u935D", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasAnyTransform(Transform[] transforms)
        {
            if (transforms == null)
            {
                return false;
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void LogUnifiedDisplayBinding(bool front, Transform[] sources, MeshRenderer renderer)
        {
            if (front)
            {
                if (hasLoggedFrontUnifiedDisplayBinding)
                {
                    return;
                }

                hasLoggedFrontUnifiedDisplayBinding = true;
            }
            else
            {
                if (hasLoggedRearUnifiedDisplayBinding)
                {
                    return;
                }

                hasLoggedRearUnifiedDisplayBinding = true;
            }

            CocoonDebugLog.Info(
                "TaxiDisplay",
                (front ? "Front LED" : "Rear OLED") +
                " unified display bound: sources=" + GetTransformPathSafe(sources, 0) +
                ", " + GetTransformPathSafe(sources, 1) +
                ", " + GetTransformPathSafe(sources, 2) +
                ", renderer=" + GetTransformPath(renderer != null ? renderer.transform : null) + ".",
                this);
        }

        private void LogMissingUnifiedDisplaySources(bool front)
        {
            if (front)
            {
                if (hasLoggedMissingFrontLed)
                {
                    return;
                }

                hasLoggedMissingFrontLed = true;
            }
            else
            {
                if (hasLoggedMissingRearOled)
                {
                    return;
                }

                hasLoggedMissingRearOled = true;
            }

            CocoonDebugLog.Warn(
                "TaxiDisplay",
                (front ? "Front LED targets led1/led2/led3" : "Rear OLED targets oled1/oled2/oled3") +
                " were not found under Final Taxi Interior Structure; using old windshield panel fallback.",
                this);
        }

        private void LogUnreadableUnifiedDisplayMesh(bool front, string reason)
        {
            if (string.IsNullOrEmpty(reason) ||
                reason.IndexOf("not readable", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            if (front)
            {
                if (hasLoggedFrontUnreadableUnifiedDisplayMesh)
                {
                    return;
                }

                hasLoggedFrontUnreadableUnifiedDisplayMesh = true;
            }
            else
            {
                if (hasLoggedRearUnreadableUnifiedDisplayMesh)
                {
                    return;
                }

                hasLoggedRearUnreadableUnifiedDisplayMesh = true;
            }

            CocoonDebugLog.Warn(
                "TaxiDisplay",
                (front ? "Front LED" : "Rear OLED") +
                " screen face extraction skipped because " + reason +
                "; reimport Assets/ImportedAssets/CocoonTaxi/solid.obj with Read/Write enabled.",
                this);
        }

        private void LogUnifiedDisplayFallback(bool front, string reason)
        {
            if (front)
            {
                if (hasLoggedFrontUnifiedDisplayFallback)
                {
                    return;
                }

                hasLoggedFrontUnifiedDisplayFallback = true;
            }
            else
            {
                if (hasLoggedRearUnifiedDisplayFallback)
                {
                    return;
                }

                hasLoggedRearUnifiedDisplayFallback = true;
            }

            CocoonDebugLog.Warn(
                "TaxiDisplay",
                (front ? "Front LED" : "Rear OLED") +
                " screen material faces could not be extracted" +
                (string.IsNullOrEmpty(reason) ? string.Empty : " (" + reason + ")") +
                "; generated a single fitted fallback display plane.",
                this);
        }

        private static string GetTransformPathSafe(Transform[] transforms, int index)
        {
            return transforms != null && index >= 0 && index < transforms.Length
                ? GetTransformPath(transforms[index])
                : "<missing>";
        }

        private static void SetWindshieldPanelActive(RectTransform panel, bool active)
        {
            if (panel != null && panel.gameObject.activeSelf != active)
            {
                panel.gameObject.SetActive(active);
            }
        }

        private static void DisableUnifiedDisplayRenderer(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.enabled = false;
            if (renderer.gameObject.activeSelf)
            {
                renderer.gameObject.SetActive(false);
            }
        }

        private RawImage EnsureExteriorScreenImage(RectTransform panel, RawImage existingImage)
        {
            if (existingImage != null)
            {
                return existingImage;
            }

            Transform existingTransform = panel.Find(ExteriorScreenImageObjectName);
            RawImage image = existingTransform != null ? existingTransform.GetComponent<RawImage>() : null;
            if (existingTransform != null)
            {
                return image != null ? image : existingTransform.gameObject.AddComponent<RawImage>();
            }

            var imageObject = new GameObject(ExteriorScreenImageObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchoredPosition = Vector2.zero;
            imageObject.transform.SetAsLastSibling();

            image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static void SetExteriorScreenImageActive(RawImage image, bool active)
        {
            if (image != null)
            {
                image.gameObject.SetActive(active);
            }
        }

        private static void FitExteriorScreenPanel(RectTransform panelRect, RectTransform imageRect, Texture texture)
        {
            if (panelRect == null || imageRect == null)
            {
                return;
            }

            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            Transform background = panelRect.Find("Background");
            if (background != null)
            {
                Image backgroundImage = background.GetComponent<Image>();
                if (backgroundImage != null)
                {
                    backgroundImage.enabled = false;
                }
            }
        }

        private static Vector2 GetExteriorScreenPanelSize(float width)
        {
            return new Vector2(width, width * ExteriorScreenSourceHeight / ExteriorScreenSourceWidth);
        }

        private static float GetExteriorScreenAspect(Texture texture)
        {
            if (texture != null && texture.height > 0)
            {
                return Mathf.Max(0.0001f, (float)texture.width / texture.height);
            }

            return ExteriorScreenSourceWidth / ExteriorScreenSourceHeight;
        }

        private static void DisableLegacyWindshieldMessageBlock(RectTransform panel, Text legacyText)
        {
            if (legacyText != null)
            {
                legacyText.gameObject.SetActive(false);
            }

            Text[] texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name.IndexOf("Windshield Message", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    texts[i].gameObject.SetActive(false);
                }
            }
        }

        private void ResolveWindshieldPanelReferences()
        {
            if (windshieldPanel == null && windshieldText != null)
            {
                windshieldPanel = windshieldText.transform.parent as RectTransform;
            }

            if (rearWindshieldPanel == null && rearWindshieldText != null)
            {
                rearWindshieldPanel = rearWindshieldText.transform.parent as RectTransform;
            }

            if (windshieldPanel == null)
            {
                windshieldPanel = FindSceneRectTransform(FrontWindshieldPanelName);
            }

            if (rearWindshieldPanel == null)
            {
                rearWindshieldPanel = FindSceneRectTransform(RearWindshieldPanelName);
            }

            if (windshieldImage == null && windshieldPanel != null)
            {
                Transform imageTransform = windshieldPanel.Find(ExteriorScreenImageObjectName);
                windshieldImage = imageTransform != null ? imageTransform.GetComponent<RawImage>() : null;
            }

            if (rearWindshieldImage == null && rearWindshieldPanel != null)
            {
                Transform imageTransform = rearWindshieldPanel.Find(ExteriorScreenImageObjectName);
                rearWindshieldImage = imageTransform != null ? imageTransform.GetComponent<RawImage>() : null;
            }
        }

        private bool HasAnyWindshieldDisplay()
        {
            return windshieldPanel != null ||
                   rearWindshieldPanel != null ||
                   windshieldText != null ||
                   rearWindshieldText != null ||
                   windshieldImage != null ||
                   rearWindshieldImage != null;
        }

        private RectTransform FindSceneRectTransform(string objectName)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    candidate.gameObject.scene != gameObject.scene ||
                    !string.Equals(candidate.name, objectName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return candidate as RectTransform;
            }

            return null;
        }

        private CocoonTaxiExteriorScreenState ResolveExteriorScreenState(string displayMessage)
        {
            if (displayMessage.Contains("OUT OF SERVICE"))
            {
                return CocoonTaxiExteriorScreenState.OutOfService;
            }

            if (displayMessage.Contains("2 SEATS"))
            {
                return CocoonTaxiExteriorScreenState.TwoSeatsAvailable;
            }

            if (displayMessage.Contains("FULL"))
            {
                return CocoonTaxiExteriorScreenState.Full;
            }

            if (displayMessage.Contains("GOODBYE"))
            {
                return CocoonTaxiExteriorScreenState.Goodbye;
            }

            if (displayMessage.Contains("KEEP") || displayMessage.Contains("DETECTED") || displayMessage.Contains("HOLD"))
            {
                return CocoonTaxiExteriorScreenState.DetectedHold;
            }

            if (displayMessage.Contains("CONFIRMED") ||
                displayMessage.Contains("STOPPING") ||
                displayMessage.Contains("FOLLOW") ||
                displayMessage.Contains("PICKUP") ||
                displayMessage.Contains("WELCOME"))
            {
                return CocoonTaxiExteriorScreenState.StoppingFollowMe;
            }

            return availabilityExteriorScreenState;
        }

        private static bool IsAvailabilityCapacityScreen(CocoonTaxiExteriorScreenState screenState)
        {
            return screenState == CocoonTaxiExteriorScreenState.Available ||
                   screenState == CocoonTaxiExteriorScreenState.TwoSeatsAvailable ||
                   screenState == CocoonTaxiExteriorScreenState.Full;
        }

        private bool ApplyWindshieldMessage(Text text, string renderMessage, string displayMessage, Color color)
        {
            if (text == null)
            {
                return false;
            }

            text.gameObject.SetActive(true);
            bool changed = text.text != renderMessage;
            if (changed)
            {
                text.text = renderMessage;
            }

            text.color = color;
            ConfigureWindshieldTypography(text, displayMessage);
            return changed;
        }

        private void UpdateKeepCountdownMessage(float remainingSeconds)
        {
            int seconds = Mathf.Clamp(Mathf.CeilToInt(remainingSeconds), 1, Mathf.CeilToInt(poseConfirmSeconds));
            SetWindshieldMessage("KEEP FOR " + seconds + "S", new Color(1f, 0.84f, 0.12f));
        }

        private void ConfigureWindshieldTypography(Text text, string displayMessage)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            bool isConfirmedMessage = displayMessage == "CONFIRMED! STOPPING NOW";
            if (isConfirmedMessage)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(1110f, 650f);
                text.fontSize = 520;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 180;
                text.resizeTextMaxSize = 620;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.lineSpacing = 0.76f;
            }
            else
            {
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(1110f, 650f);
                text.fontSize = 520;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 220;
                text.resizeTextMaxSize = 560;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.lineSpacing = 1f;
            }

            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.SetAllDirty();
        }

        private void ApplyLightAppearance(Color color, float intensity)
        {
            if (!enableBodyLightVisuals)
            {
                return;
            }

            if (lightMaterials == null)
            {
                CacheMaterialInstances();
            }

            if (lightMaterials != null)
            {
                for (int i = 0; i < lightMaterials.Length; i++)
                {
                    if (lightMaterials[i] == null)
                    {
                        continue;
                    }

                    ApplyMaterialColor(lightMaterials[i], color, color * intensity);
                }
            }

            if (bodyMaterial != null)
            {
                float bodyTint = currentLightMode.StartsWith("GREEN") ? 0.18f : 0.08f;
                ApplyMaterialColor(bodyMaterial, Color.Lerp(new Color(0.82f, 0.88f, 0.86f), color, bodyTint), Color.black);
            }
        }

        private int CountActiveLightRenderers()
        {
            if (!enableBodyLightVisuals || lightRenderers == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < lightRenderers.Length; i++)
            {
                if (lightRenderers[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void SetPaymentReaderProgress(float progress)
        {
            if (paymentReaderMaterial == null)
            {
                CacheMaterialInstances();
            }

            if (paymentReaderMaterial == null)
            {
                return;
            }

            Color color = Color.Lerp(new Color(0.1f, 1f, 0.55f), Color.white, Mathf.Clamp01(progress));
            ApplyMaterialColor(paymentReaderMaterial, color, color * Mathf.Lerp(1.7f, 3.2f, Mathf.Clamp01(progress)));
        }

        private static Material CreateRuntimeMaterial(Renderer renderer, bool emissive, Color fallbackColor, float emissionIntensity)
        {
            if (renderer == null)
            {
                return null;
            }

            Material source = renderer.sharedMaterial;
            Color baseColor = GetMaterialColor(source, fallbackColor);
            Shader preferredShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = source != null && !NeedsShaderReplacement(source.shader)
                ? new Material(source)
                : new Material(preferredShader);

            if (NeedsShaderReplacement(material.shader) && preferredShader != null)
            {
                material.shader = preferredShader;
            }

            ApplyMaterialColor(material, baseColor, emissive ? baseColor * Mathf.Max(0.01f, emissionIntensity) : Color.black);
            renderer.material = material;
            return material;
        }

        private static bool NeedsShaderReplacement(Shader shader)
        {
            if (shader == null)
            {
                return true;
            }

            string shaderName = shader.name;
            return shaderName == "Standard" ||
                   shaderName == "Hidden/InternalErrorShader" ||
                   shaderName.Contains("Error") ||
                   shaderName.StartsWith("Legacy Shaders");
        }

        private static Color GetMaterialColor(Material material, Color fallbackColor)
        {
            if (material == null)
            {
                return fallbackColor;
            }

            if (material.HasProperty(BaseColorId))
            {
                return material.GetColor(BaseColorId);
            }

            if (material.HasProperty(ColorId))
            {
                return material.GetColor(ColorId);
            }

            return fallbackColor;
        }

        private static void ApplyMaterialColor(Material material, Color baseColor, Color emissionColor)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, baseColor);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, baseColor);
            }

            if (!material.HasProperty(EmissionColorId))
            {
                return;
            }

            bool emissive = emissionColor.maxColorComponent > 0.01f;
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, emissionColor);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        private struct LuggageExitPose
        {
            public readonly string Label;
            public readonly Vector3 LocalPosition;
            public readonly Quaternion LocalRotation;
            public readonly Vector3 WorldScale;
            public readonly float Duration;

            public LuggageExitPose(string label, Vector3 localPosition, Quaternion localRotation, Vector3 worldScale, float duration)
            {
                Label = label;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                WorldScale = worldScale;
                Duration = duration;
            }
        }

        private struct SlidingDoorPanel
        {
            public readonly CocoonTaxiDoorSide Side;
            public readonly Transform Panel;
            public readonly float SlideSign;
            public Vector3 ClosedLocalPosition;

            public SlidingDoorPanel(CocoonTaxiDoorSide side, Transform panel, float slideSign)
            {
                Side = side;
                Panel = panel;
                SlideSign = slideSign;
                ClosedLocalPosition = panel != null ? panel.localPosition : Vector3.zero;
            }

            public void CaptureClosedPose()
            {
                ClosedLocalPosition = Panel != null ? Panel.localPosition : Vector3.zero;
            }
        }

        private struct AuthoredRampPose
        {
            public bool HasPose;
            public Vector3 StandbyLocalPosition;
            public Quaternion StandbyLocalRotation;
            public Vector3 StandbyLocalScale;

            public void Capture(
                Transform ramp,
                bool hasSerializedStandbyPose,
                Vector3 serializedStandbyLocalPosition,
                Quaternion serializedStandbyLocalRotation,
                Vector3 serializedStandbyLocalScale)
            {
                if (ramp == null)
                {
                    HasPose = false;
                    return;
                }

                bool useSerializedStandby = hasSerializedStandbyPose &&
                                            (serializedStandbyLocalPosition.sqrMagnitude > 0.000001f ||
                                             Quaternion.Angle(serializedStandbyLocalRotation, Quaternion.identity) > 0.01f ||
                                             Vector3.Distance(serializedStandbyLocalScale, Vector3.one) > 0.000001f ||
                                             ramp.localPosition.sqrMagnitude <= 0.000001f);
                if (useSerializedStandby)
                {
                    StandbyLocalPosition = serializedStandbyLocalPosition;
                    StandbyLocalRotation = serializedStandbyLocalRotation;
                    StandbyLocalScale = serializedStandbyLocalScale;
                }
                else
                {
                    StandbyLocalPosition = ramp.localPosition;
                    StandbyLocalRotation = ramp.localRotation;
                    StandbyLocalScale = ramp.localScale;
                }

                HasPose = true;
            }
        }

        private void ApplyDoor(float open01)
        {
            open01 = Mathf.Clamp01(open01);
            ResolveLeftBoardingDoorPanels();
            if (HasLeftBoardingDoorPanels())
            {
                ApplyLeftBoardingDoor(open01);
                ApplyDoorUiDoorL2FollowPose();
                return;
            }

            ResolveSlidingDoorPanels();

            if (HasSlidingDoorPanels())
            {
                if (open01 > 0.001f && !hasSelectedSlidingDoorSide)
                {
                    SelectSlidingDoorSideForBoarding();
                }

                CocoonTaxiDoorSide openingSide = hasSelectedSlidingDoorSide ? activeSlidingDoorSide : CocoonTaxiDoorSide.A;
                ApplySlidingDoorSide(CocoonTaxiDoorSide.A, openingSide == CocoonTaxiDoorSide.A ? open01 : 0f);
                ApplySlidingDoorSide(CocoonTaxiDoorSide.B, openingSide == CocoonTaxiDoorSide.B ? open01 : 0f);
                ApplyDoorUiDoorL2FollowPose();
                return;
            }

            if (doorHinge != null)
            {
                doorHinge.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, -72f, open01), 0f);
            }

            ApplyDoorUiDoorL2FollowPose();
        }

        private bool HasLeftBoardingDoorPanels()
        {
            return doorL1 != null && doorL2 != null;
        }

        private bool HasBoardingDoorGeometry()
        {
            ResolveLeftBoardingDoorPanels();
            return HasLeftBoardingDoorPanels() || HasSlidingDoorPanels();
        }

        private void ResolveLeftBoardingDoorPanels()
        {
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            Transform previousDoorL1 = doorL1;
            Transform previousDoorL2 = doorL2;

            doorL1 = ResolveExactNamedReference(searchRoot, doorL1, LeftDoorPanel1Name);
            doorL2 = ResolveExactNamedReference(searchRoot, doorL2, LeftDoorPanel2Name);

            if (previousDoorL1 != doorL1 || previousDoorL2 != doorL2)
            {
                hasCachedLeftDoorClosedPose = false;
                hasDoorUiDoorL2FollowPose = false;
                hasLoggedLeftDoorBinding = false;
                hasLoggedMissingLeftDoorPanels = false;
                hasLoggedLeftDoorMotionStart = false;
                hasLoggedLeftDoorMotionComplete = false;
            }

            if (HasLeftBoardingDoorPanels())
            {
                CacheLeftBoardingDoorClosedPoses();
                LogLeftDoorBindingIfNeeded();
                return;
            }

            if (!hasLoggedMissingLeftDoorPanels)
            {
                hasLoggedMissingLeftDoorPanels = true;
                CocoonDebugLog.Warn("Door", "Left boarding door panels missing; falling back to legacy door animation.", this);
            }
        }

        private void CacheLeftBoardingDoorClosedPoses()
        {
            if (hasCachedLeftDoorClosedPose || !HasLeftBoardingDoorPanels())
            {
                return;
            }

            CaptureLeftBoardingDoorOpenPoseIfNeeded();
            doorL1ClosedLocalPosition = Vector3.zero;
            doorL2ClosedLocalPosition = Vector3.zero;
            doorL1.localPosition = doorL1ClosedLocalPosition;
            doorL2.localPosition = doorL2ClosedLocalPosition;
            hasCachedLeftDoorClosedPose = true;
        }

        private void CaptureLeftBoardingDoorOpenPoseIfNeeded()
        {
            if (hasAuthoredLeftDoorOpenPose && doorL2OpenLocalPosition.sqrMagnitude > 0.000001f)
            {
                return;
            }

            Vector3 authoredDoorL2Target = doorL2 != null ? doorL2.localPosition : Vector3.zero;
            if (authoredDoorL2Target.sqrMagnitude < 0.000001f)
            {
                authoredDoorL2Target = new Vector3(-6.8f, 73.8f, 0f);
                CocoonDebugLog.Warn("Door", "doorL2 open target was not serialized or authored; using fallback local target " + FormatPosition(authoredDoorL2Target) + ".", this);
            }

            doorL2OpenLocalPosition = authoredDoorL2Target;
            doorL1OpenLocalPosition = new Vector3(authoredDoorL2Target.x, -authoredDoorL2Target.y, authoredDoorL2Target.z);
            hasAuthoredLeftDoorOpenPose = true;
        }

        private void LogLeftDoorBindingIfNeeded()
        {
            if (hasLoggedLeftDoorBinding)
            {
                return;
            }

            hasLoggedLeftDoorBinding = true;
            bool hasDoorBounds = TryGetLeftBoardingDoorBounds(out Bounds bounds);
            string boundsText = hasDoorBounds
                ? " boundsCenter=" + FormatPosition(bounds.center) + ", boundsSize=" + FormatPosition(bounds.size)
                : " bounds=missing-renderers";
            CocoonDebugLog.Info(
                "Door",
                "Left boarding door panels exact-bound: doorL1=" + GetTransformPath(doorL1) +
                ", doorL2=" + GetTransformPath(doorL2) +
                ", parentScale1=" + FormatPosition(doorL1.parent != null ? doorL1.parent.lossyScale : Vector3.one) +
                ", parentScale2=" + FormatPosition(doorL2.parent != null ? doorL2.parent.lossyScale : Vector3.one) +
                ", doorL1OpenLocal=" + FormatPosition(doorL1OpenLocalPosition) +
                ", doorL2OpenLocal=" + FormatPosition(doorL2OpenLocalPosition) +
                boundsText + ".",
                this);

            if (!hasDoorBounds)
            {
                CocoonDebugLog.Warn("Door", "doorL1/doorL2 were found by name, but no renderer bounds were found under them; visual door motion may be invisible.", this);
            }
        }

        private void ApplyLeftBoardingDoor(float open01)
        {
            CacheLeftBoardingDoorClosedPoses();
            float pushFraction = Mathf.Clamp(GetBoardingDoorPushFraction(), 0.05f, 0.95f);
            float pushProgress = Smooth01(Mathf.Clamp01(open01 / pushFraction));
            float slideProgress = Smooth01(Mathf.Clamp01((open01 - pushFraction) / Mathf.Max(0.001f, 1f - pushFraction)));

            if (doorL1 != null)
            {
                doorL1.gameObject.SetActive(true);
                doorL1.localPosition = BuildLeftBoardingDoorLocalPosition(doorL1ClosedLocalPosition, doorL1OpenLocalPosition, pushProgress, slideProgress);
            }

            if (doorL2 != null)
            {
                doorL2.gameObject.SetActive(true);
                doorL2.localPosition = BuildLeftBoardingDoorLocalPosition(doorL2ClosedLocalPosition, doorL2OpenLocalPosition, pushProgress, slideProgress);
            }

            if (open01 > 0.001f && !hasLoggedLeftDoorMotionStart)
            {
                hasLoggedLeftDoorMotionStart = true;
                CocoonDebugLog.Info(
                    "Door",
                    "Left door opening visual motion started. phase1 local X target=" + doorL2OpenLocalPosition.x.ToString("0.###") +
                    ", phase2 local Y targets=(" + doorL1OpenLocalPosition.y.ToString("0.###") + "," + doorL2OpenLocalPosition.y.ToString("0.###") + ").",
                    this);
            }

            if (open01 >= 0.999f && !hasLoggedLeftDoorMotionComplete)
            {
                hasLoggedLeftDoorMotionComplete = true;
                CocoonDebugLog.Info(
                    "Door",
                    "Left door visual open pose reached. doorL1=" + FormatPosition(doorL1) + ", doorL2=" + FormatPosition(doorL2) + ".",
                    this);
            }
        }

        private static Vector3 BuildLeftBoardingDoorLocalPosition(Vector3 closedLocalPosition, Vector3 openLocalPosition, float pushProgress, float slideProgress)
        {
            Vector3 delta = new Vector3(
                Mathf.Lerp(0f, openLocalPosition.x, pushProgress),
                Mathf.Lerp(0f, openLocalPosition.y, slideProgress),
                Mathf.Lerp(0f, openLocalPosition.z, pushProgress));
            return closedLocalPosition + delta;
        }

        private bool HasSlidingDoorPanels()
        {
            return doorSideALeftPanel != null &&
                   doorSideARightPanel != null &&
                   doorSideBLeftPanel != null &&
                   doorSideBRightPanel != null;
        }

        private void ResolveSlidingDoorPanels()
        {
            if (HasSlidingDoorPanels())
            {
                CacheSlidingDoorClosedPoses();
                return;
            }

            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            doorSideARightPanel = doorSideARightPanel != null ? doorSideARightPanel : FindNamedDescendant(searchRoot, "rxx");
            doorSideALeftPanel = doorSideALeftPanel != null ? doorSideALeftPanel : FindNamedDescendant(searchRoot, "lxx");
            doorSideBRightPanel = doorSideBRightPanel != null ? doorSideBRightPanel : FindNamedDescendant(searchRoot, "rxx (1)");
            doorSideBLeftPanel = doorSideBLeftPanel != null ? doorSideBLeftPanel : FindNamedDescendant(searchRoot, "lxx (1)");

            CacheSlidingDoorClosedPoses();

            if (HasSlidingDoorPanels())
            {
                if (!hasLoggedSlidingDoorBinding)
                {
                    hasLoggedSlidingDoorBinding = true;
                    CocoonDebugLog.Info("Door", "Sliding door panels bound: rxx/lxx and rxx (1)/lxx (1).", this);
                }
            }
            else if (!hasLoggedMissingSlidingDoorPanels)
            {
                hasLoggedMissingSlidingDoorPanels = true;
                CocoonDebugLog.Warn("Door", "Sliding door panels incomplete; falling back to legacy hinge animation.", this);
            }
        }

        private void CacheSlidingDoorClosedPoses()
        {
            if (hasCachedSlidingDoorClosedPose)
            {
                return;
            }

            slidingDoorPanels[0] = new SlidingDoorPanel(CocoonTaxiDoorSide.A, doorSideALeftPanel, 1f);
            slidingDoorPanels[1] = new SlidingDoorPanel(CocoonTaxiDoorSide.A, doorSideARightPanel, -1f);
            slidingDoorPanels[2] = new SlidingDoorPanel(CocoonTaxiDoorSide.B, doorSideBLeftPanel, 1f);
            slidingDoorPanels[3] = new SlidingDoorPanel(CocoonTaxiDoorSide.B, doorSideBRightPanel, -1f);

            if (!HasSlidingDoorPanels())
            {
                return;
            }

            for (int i = 0; i < slidingDoorPanels.Length; i++)
            {
                slidingDoorPanels[i].CaptureClosedPose();
            }

            hasCachedSlidingDoorClosedPose = true;
        }

        private void SelectSlidingDoorSideForBoarding()
        {
            ResolveLeftBoardingDoorPanels();
            if (HasLeftBoardingDoorPanels())
            {
                activeSlidingDoorSide = CocoonTaxiDoorSide.A;
                hasSelectedSlidingDoorSide = true;
                if (!hasLoggedLeftDoorBinding)
                {
                    hasLoggedLeftDoorBinding = true;
                    CocoonDebugLog.Info("Door", "Selected left boarding door for pickup interaction.", this);
                }

                return;
            }

            ResolveSlidingDoorPanels();
            if (!HasSlidingDoorPanels())
            {
                return;
            }

            CocoonBoardingDoorSideMode sideMode = GetBoardingDoorSideMode();
            if (sideMode == CocoonBoardingDoorSideMode.ForceSideA)
            {
                activeSlidingDoorSide = CocoonTaxiDoorSide.A;
                hasSelectedSlidingDoorSide = true;
                CocoonDebugLog.Info("Door", "Selected forced sliding door side A for pickup interaction.", this);
                return;
            }

            if (sideMode == CocoonBoardingDoorSideMode.ForceSideB)
            {
                activeSlidingDoorSide = CocoonTaxiDoorSide.B;
                hasSelectedSlidingDoorSide = true;
                CocoonDebugLog.Info("Door", "Selected forced sliding door side B for pickup interaction.", this);
                return;
            }

            Vector3 reference = ResolveBoardingDoorSideReferencePoint();
            float distanceA = Vector3.SqrMagnitude(GetSlidingDoorSideCentroid(CocoonTaxiDoorSide.A) - reference);
            float distanceB = Vector3.SqrMagnitude(GetSlidingDoorSideCentroid(CocoonTaxiDoorSide.B) - reference);
            activeSlidingDoorSide = distanceB < distanceA ? CocoonTaxiDoorSide.B : CocoonTaxiDoorSide.A;
            hasSelectedSlidingDoorSide = true;
            CocoonDebugLog.Info("Door", "Selected curb-side sliding door side " + activeSlidingDoorSide + " for pickup interaction.", this);
        }

        private Vector3 ResolveBoardingDoorSideReferencePoint()
        {
            Transform anchor = GetPhysicalPullOverAnchor(selectedPullOverPoint);
            if (anchor != null)
            {
                float experienceScale = GetExperienceScale();
                return anchor.position - anchor.right * (2.55f * experienceScale) + anchor.forward * (0.08f * experienceScale);
            }

            return ResolveDoorSideReferencePoint();
        }

        private Vector3 ResolveDoorSideReferencePoint()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform.position;
            }

            if (rightHand != null)
            {
                return rightHand.position;
            }

            Transform anchor = GetPhysicalPullOverAnchor(selectedPullOverPoint);
            if (anchor != null)
            {
                return anchor.position;
            }

            return taxiRoot != null ? taxiRoot.position : transform.position;
        }

        private Vector3 GetSlidingDoorSideCentroid(CocoonTaxiDoorSide side)
        {
            if (side == CocoonTaxiDoorSide.A && HasLeftBoardingDoorPanels())
            {
                return (doorL1.position + doorL2.position) * 0.5f;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < slidingDoorPanels.Length; i++)
            {
                SlidingDoorPanel panel = slidingDoorPanels[i];
                if (panel.Panel != null && panel.Side == side)
                {
                    sum += panel.Panel.position;
                    count++;
                }
            }

            return count > 0 ? sum / count : (taxiRoot != null ? taxiRoot.position : transform.position);
        }

        private void ApplySlidingDoorSide(CocoonTaxiDoorSide side, float open01)
        {
            float pushFraction = Mathf.Clamp(GetBoardingDoorPushFraction(), 0.05f, 0.95f);
            float pushProgress = Smooth01(Mathf.Clamp01(open01 / pushFraction));
            float slideProgress = Smooth01(Mathf.Clamp01((open01 - pushFraction) / Mathf.Max(0.001f, 1f - pushFraction)));
            float pushDistance = GetBoardingDoorPushDistance();
            float slideDistance = GetBoardingDoorSlideDistance();

            for (int i = 0; i < slidingDoorPanels.Length; i++)
            {
                SlidingDoorPanel panel = slidingDoorPanels[i];
                if (panel.Panel == null || panel.Side != side)
                {
                    continue;
                }

                Vector3 localDelta = Vector3.down * (pushDistance * pushProgress) +
                                     Vector3.right * (slideDistance * slideProgress * panel.SlideSign);
                panel.Panel.localPosition = panel.ClosedLocalPosition + localDelta;
            }
        }

        private Vector3 GetSlidingDoorOutwardDirection(CocoonTaxiDoorSide side)
        {
            Transform root = taxiRoot != null ? taxiRoot : transform;
            Vector3 sideCenter = GetSlidingDoorSideCentroid(side);
            Vector3 toBoardingSide = ResolveBoardingDoorSideReferencePoint() - sideCenter;
            toBoardingSide.y = 0f;
            if (toBoardingSide.sqrMagnitude > 0.0001f)
            {
                return toBoardingSide.normalized;
            }

            Vector3 centroidLocal = root.InverseTransformPoint(sideCenter);
            Vector3 localOutward;
            if (Mathf.Abs(centroidLocal.x) >= Mathf.Abs(centroidLocal.z))
            {
                localOutward = Vector3.right * (centroidLocal.x >= 0f ? 1f : -1f);
            }
            else
            {
                localOutward = Vector3.forward * (centroidLocal.z >= 0f ? 1f : -1f);
            }

            return root.TransformDirection(localOutward).normalized;
        }

        private Vector3 GetStableSlidingDoorOutwardDirection(CocoonTaxiDoorSide side)
        {
            Transform root = taxiRoot != null ? taxiRoot : transform;
            Vector3 outward = GetSlidingDoorSideVisualCenter(side) - root.position;
            outward.y = 0f;
            if (outward.sqrMagnitude > 0.0001f)
            {
                return outward.normalized;
            }

            return GetSlidingDoorOutwardDirection(side);
        }

        private Vector3 GetSlidingDoorSlideDirection(CocoonTaxiDoorSide side)
        {
            Transform root = taxiRoot != null ? taxiRoot : transform;
            Vector3 outwardLocal = root.InverseTransformDirection(GetSlidingDoorOutwardDirection(side));
            Vector3 localSlide = Mathf.Abs(outwardLocal.x) >= Mathf.Abs(outwardLocal.z) ? Vector3.forward : Vector3.right;
            return root.TransformDirection(localSlide).normalized;
        }

        private static Vector3 GetParentLocalVector(Transform target, Vector3 worldVector)
        {
            return target.parent != null ? target.parent.InverseTransformVector(worldVector) : worldVector;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static Transform FindNamedDescendant(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && string.Equals(transforms[i].name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private Transform ResolveBoardingTouchZone()
        {
            if (boardingTouchZone == null || !string.Equals(boardingTouchZone.name, BoardingTouchZoneName, StringComparison.Ordinal))
            {
                Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
                Transform interior = FindExactNamedDescendant(searchRoot, FinalTaxiInteriorStructureName);
                Transform previousTouchZone = boardingTouchZone;
                boardingTouchZone = FindExactNamedDescendant(interior, BoardingTouchZoneName) ??
                                    FindExactNamedDescendant(searchRoot, BoardingTouchZoneName);
                boardingTouchCollider = null;
                if (previousTouchZone != boardingTouchZone)
                {
                    hasDoorUiDoorL2FollowPose = false;
                    hasLoggedDoorUiDoorL2FollowPose = false;
                    hasLoggedDoorUiFollowMissing = false;
                }
            }

            if (boardingTouchZone != null && !boardingTouchZone.gameObject.activeSelf)
            {
                boardingTouchZone.gameObject.SetActive(true);
            }

            if (boardingTouchZone == null && !hasLoggedMissingBoardingTouchZone)
            {
                hasLoggedMissingBoardingTouchZone = true;
                CocoonDebugLog.Warn("DoorUI", "DOORUI touch zone missing; boarding touch cannot be detected.", this);
            }

            return boardingTouchZone;
        }

        private void CaptureDoorUiDoorL2FollowPose(bool force = false)
        {
            if (!doorUiFollowsDoorL2)
            {
                return;
            }

            Transform zone = ResolveBoardingTouchZone();
            ResolveLeftBoardingDoorPanels();
            if (zone == null || doorL2 == null)
            {
                if (!hasLoggedDoorUiFollowMissing)
                {
                    hasLoggedDoorUiFollowMissing = true;
                    CocoonDebugLog.Warn(
                        "DoorUI",
                        "Cannot capture DOORUI follow pose; zone=" + GetTransformPath(zone) + ", doorL2=" + GetTransformPath(doorL2) + ".",
                        this);
                }

                return;
            }

            if (hasDoorUiDoorL2FollowPose && !force)
            {
                return;
            }

            doorUiClosedLocalPositionToDoorL2 = doorL2.InverseTransformPoint(zone.position);
            doorUiClosedLocalRotationToDoorL2 = Quaternion.Inverse(doorL2.rotation) * zone.rotation;
            hasDoorUiDoorL2FollowPose = true;

            if (!hasLoggedDoorUiDoorL2FollowPose)
            {
                hasLoggedDoorUiDoorL2FollowPose = true;
                CocoonDebugLog.Info(
                    "DoorUI",
                    "DOORUI follow pose captured relative to doorL2. zone=" + GetTransformPath(zone) +
                    ", doorL2=" + GetTransformPath(doorL2) +
                    ", localOffset=" + FormatPosition(doorUiClosedLocalPositionToDoorL2) + ".",
                    this);
            }
        }

        private void ApplyDoorUiDoorL2FollowPose()
        {
            if (!doorUiFollowsDoorL2)
            {
                return;
            }

            if (!hasDoorUiDoorL2FollowPose)
            {
                CaptureDoorUiDoorL2FollowPose(false);
            }

            if (!hasDoorUiDoorL2FollowPose || boardingTouchZone == null || doorL2 == null)
            {
                return;
            }

            Vector3 worldPosition = doorL2.TransformPoint(doorUiClosedLocalPositionToDoorL2);
            Quaternion worldRotation = doorL2.rotation * doorUiClosedLocalRotationToDoorL2;
            boardingTouchZone.SetPositionAndRotation(worldPosition, worldRotation);
        }

        private Collider ResolveBoardingTouchCollider()
        {
            Transform zone = ResolveBoardingTouchZone();
            if (zone == null)
            {
                return null;
            }

            if (boardingTouchCollider != null && boardingTouchCollider.transform == zone)
            {
                boardingTouchCollider.enabled = true;
                boardingTouchCollider.isTrigger = true;
                return boardingTouchCollider;
            }

            boardingTouchCollider = zone.GetComponent<Collider>();
            if (boardingTouchCollider == null)
            {
                BoxCollider box = zone.gameObject.AddComponent<BoxCollider>();
                MeshFilter meshFilter = zone.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    box.center = meshFilter.sharedMesh.bounds.center;
                    box.size = meshFilter.sharedMesh.bounds.size;
                }
                else
                {
                    box.center = Vector3.zero;
                    box.size = Vector3.one * Mathf.Max(0.12f, GetExperienceScale() * 1.4f);
                }

                boardingTouchCollider = box;
            }

            boardingTouchCollider.enabled = true;
            boardingTouchCollider.isTrigger = true;
            return boardingTouchCollider;
        }

        private void SetBoardingDoorSurfaceScreen(bool active, bool detected)
        {
            Texture2D texture = ResolveBoardingDoorSurfaceTexture(active && detected);
            if (texture == null || !EnsureBoardingDoorSurfacePanel(texture))
            {
                return;
            }

            ConfigureExteriorScreenTexture(texture);
            boardingDoorSurfaceImage.texture = texture;
            boardingDoorSurfaceImage.color = Color.white;
            boardingDoorSurfaceImage.material = null;
            boardingDoorSurfaceImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            boardingDoorSurfaceImage.raycastTarget = false;
            boardingDoorSurfaceImage.enabled = true;
            ApplyBoardingDoorSurfaceFollowPose();
            boardingDoorSurfacePanel.gameObject.SetActive(true);
        }

        private Texture2D ResolveBoardingDoorSurfaceTexture(bool detected)
        {
            if (detected)
            {
                if (boardingDoorDetectedTexture == null)
                {
                    boardingDoorDetectedTexture = Resources.Load<Texture2D>(BoardingDoorScreenResourceFolder + BoardingDoorDetectedScreenResourceName);
                }

                return boardingDoorDetectedTexture;
            }

            if (boardingDoorIdleTexture == null)
            {
                boardingDoorIdleTexture = Resources.Load<Texture2D>(BoardingDoorScreenResourceFolder + BoardingDoorIdleScreenResourceName);
            }

            return boardingDoorIdleTexture;
        }

        private bool EnsureBoardingDoorSurfacePanel(Texture2D texture)
        {
            Transform zone = ResolveBoardingTouchZone();
            Transform preferredParent = ResolveBoardingDoorSurfaceParent(zone);
            if (zone == null)
            {
                if (!hasLoggedBoardingDoorSurfaceMissing)
                {
                    hasLoggedBoardingDoorSurfaceMissing = true;
                    CocoonDebugLog.Warn("DoorUI", "Cannot create DOORUI surface display because DOORUI is missing.", this);
                }

                return false;
            }

            if (boardingDoorSurfacePanel == null || !string.Equals(boardingDoorSurfacePanel.name, BoardingDoorSurfacePanelName, StringComparison.Ordinal))
            {
                Transform existing = FindExactNamedDescendant(preferredParent, BoardingDoorSurfacePanelName) ??
                                     FindExactNamedDescendant(zone, BoardingDoorSurfacePanelName) ??
                                     FindExactNamedDescendant(taxiRoot != null ? taxiRoot : transform, BoardingDoorSurfacePanelName);
                boardingDoorSurfacePanel = existing as RectTransform;
            }

            if (boardingDoorSurfacePanel != null && preferredParent != null && boardingDoorSurfacePanel.parent != preferredParent)
            {
                boardingDoorSurfacePanel.SetParent(preferredParent, true);
            }

            bool createdPanel = false;
            if (boardingDoorSurfacePanel == null)
            {
                var panelObject = new GameObject(BoardingDoorSurfacePanelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                boardingDoorSurfacePanel = panelObject.GetComponent<RectTransform>();
                boardingDoorSurfacePanel.SetParent(preferredParent != null ? preferredParent : zone, false);
                float aspect = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 1f;
                boardingDoorSurfacePanel.sizeDelta = new Vector2(Mathf.Max(16f, boardingDoorSurfaceReferenceWidth), Mathf.Max(16f, boardingDoorSurfaceReferenceWidth) / Mathf.Max(0.001f, aspect));
                boardingDoorSurfacePanel.position = zone.position;
                boardingDoorSurfacePanel.rotation = zone.rotation;
                SetWorldScale(boardingDoorSurfacePanel, Vector3.one * (0.14f / Mathf.Max(16f, boardingDoorSurfaceReferenceWidth)));
                createdPanel = true;
                CocoonDebugLog.Info("DoorUI", "Created DOORUI surface display panel under Final Taxi Interior Structure. You can fine tune this RectTransform in the Hierarchy.", this);
            }

            Canvas canvas = boardingDoorSurfacePanel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = boardingDoorSurfacePanel.gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            if (canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }

            CanvasScaler scaler = boardingDoorSurfacePanel.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = boardingDoorSurfacePanel.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.dynamicPixelsPerUnit = Mathf.Max(768f, exteriorScreenPixelsPerUnit);
            scaler.referencePixelsPerUnit = 100f;

            GraphicRaycaster raycaster = boardingDoorSurfacePanel.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = boardingDoorSurfacePanel.gameObject.AddComponent<GraphicRaycaster>();
            }

            raycaster.ignoreReversedGraphics = true;

            boardingDoorSurfaceImage = EnsureBoardingDoorSurfaceImage(boardingDoorSurfacePanel, createdPanel);
            CaptureBoardingDoorSurfaceFollowPose(zone, createdPanel);
            return boardingDoorSurfaceImage != null;
        }

        private Transform ResolveBoardingDoorSurfaceParent(Transform zone)
        {
            Transform interior = ResolveFinalTaxiInteriorStructure();
            if (interior != null)
            {
                return interior;
            }

            if (zone != null && zone.parent != null)
            {
                return zone.parent;
            }

            return taxiRoot != null ? taxiRoot : transform;
        }

        private void CaptureBoardingDoorSurfaceFollowPose(Transform zone, bool force)
        {
            if (boardingDoorSurfacePanel == null || zone == null)
            {
                return;
            }

            if (!force && hasBoardingDoorSurfaceFollowPose && boardingDoorSurfaceFollowTarget == zone)
            {
                return;
            }

            boardingDoorSurfaceFollowTarget = zone;
            boardingDoorSurfaceLocalPositionToDoorUi = zone.InverseTransformPoint(boardingDoorSurfacePanel.position);
            boardingDoorSurfaceLocalRotationToDoorUi = Quaternion.Inverse(zone.rotation) * boardingDoorSurfacePanel.rotation;
            hasBoardingDoorSurfaceFollowPose = true;
        }

        private void ApplyBoardingDoorSurfaceFollowPose()
        {
            if (boardingDoorSurfacePanel == null)
            {
                return;
            }

            Transform zone = ResolveBoardingTouchZone();
            if (zone == null)
            {
                return;
            }

            CaptureBoardingDoorSurfaceFollowPose(zone, false);
            if (!hasBoardingDoorSurfaceFollowPose)
            {
                return;
            }

            boardingDoorSurfacePanel.position = zone.TransformPoint(boardingDoorSurfaceLocalPositionToDoorUi);
            boardingDoorSurfacePanel.rotation = zone.rotation * boardingDoorSurfaceLocalRotationToDoorUi;
        }

        private RawImage EnsureBoardingDoorSurfaceImage(RectTransform panel, bool panelWasCreated)
        {
            Transform imageTransform = panel.Find(BoardingDoorSurfaceImageName);
            RawImage image = imageTransform != null ? imageTransform.GetComponent<RawImage>() : null;
            bool createdImage = false;
            if (image == null)
            {
                var imageObject = new GameObject(BoardingDoorSurfaceImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                imageTransform = imageObject.transform;
                imageTransform.SetParent(panel, false);
                image = imageObject.GetComponent<RawImage>();
                createdImage = true;
            }

            RectTransform rect = image.GetComponent<RectTransform>();
            if (rect != null && (createdImage || panelWasCreated))
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }

            image.raycastTarget = false;
            return image;
        }

        private bool IsBoardingTouchDetected()
        {
            Collider touchCollider = ResolveBoardingTouchCollider();
            if (touchCollider == null || !EnsureBoardingCreditCard())
            {
                return false;
            }

            ApplyBoardingCreditCardPose();
            return IsBoardingCreditCardTouchingZone(touchCollider);
        }

        private bool EnsureBoardingCreditCard()
        {
            Transform parent = ResolveRightHandTransform();
            if (parent == null)
            {
                parent = ResolveRiderRootTransform();
            }

            if (parent == null)
            {
                return false;
            }

            if (boardingCreditCard == null || !string.Equals(boardingCreditCard.name, BoardingCreditCardName, StringComparison.Ordinal))
            {
                boardingCreditCard = FindExactNamedDescendant(parent, BoardingCreditCardName);
            }

            if (boardingCreditCard == null)
            {
                GameObject anchor = new GameObject(BoardingCreditCardName);
                boardingCreditCard = anchor.transform;
                boardingCreditCard.SetParent(parent, false);
            }

            if (boardingCreditCard.parent != parent)
            {
                boardingCreditCard.SetParent(parent, false);
            }

            Renderer anchorRenderer = boardingCreditCard.GetComponent<Renderer>();
            if (anchorRenderer != null)
            {
                anchorRenderer.enabled = false;
            }

            Collider anchorCollider = boardingCreditCard.GetComponent<Collider>();
            if (anchorCollider != null)
            {
                Destroy(anchorCollider);
            }

            boardingCreditCardMaterial = boardingCreditCardMaterial != null
                ? boardingCreditCardMaterial
                : CreateBoardingCardMaterial(new Color(0.015f, 0.05f, 0.12f, 1f));
            boardingCreditCardAccentMaterial = boardingCreditCardAccentMaterial != null
                ? boardingCreditCardAccentMaterial
                : CreateBoardingCardMaterial(new Color(0.95f, 0.74f, 0.28f, 1f));

            boardingCreditCardVisual = ResolveCreditCardVisual();
            if (boardingCreditCardVisual == null)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = BoardingCreditCardVisualName;
                boardingCreditCardVisual = visual.transform;
                boardingCreditCardVisual.SetParent(boardingCreditCard, false);
            }

            Renderer cardRenderer = boardingCreditCardVisual.GetComponent<Renderer>();
            if (cardRenderer != null && boardingCreditCardMaterial != null)
            {
                cardRenderer.enabled = true;
                cardRenderer.sharedMaterial = boardingCreditCardMaterial;
            }

            Material stripeMaterial = CreateBoardingCardMaterial(new Color(0.62f, 0.87f, 1f, 1f));
            CreateBoardingCardDetailPair("Card Chip", new Vector3(-0.24f, 0.12f, 0.53f), new Vector3(0.2f, 0.18f, 0.06f), boardingCreditCardAccentMaterial);
            CreateBoardingCardDetailPair("Contactless Bar 1", new Vector3(0.2f, 0.13f, 0.54f), new Vector3(0.24f, 0.025f, 0.045f), boardingCreditCardAccentMaterial);
            CreateBoardingCardDetailPair("Contactless Bar 2", new Vector3(0.25f, 0.05f, 0.54f), new Vector3(0.3f, 0.025f, 0.045f), boardingCreditCardAccentMaterial);
            CreateBoardingCardDetailPair("Name Stripe", new Vector3(-0.03f, -0.28f, 0.54f), new Vector3(0.64f, 0.035f, 0.045f), stripeMaterial);

            boardingCreditCardCollider = boardingCreditCardVisual.GetComponent<Collider>();
            if (boardingCreditCardCollider == null)
            {
                boardingCreditCardCollider = boardingCreditCardVisual.gameObject.AddComponent<BoxCollider>();
            }

            boardingCreditCardCollider.enabled = true;
            boardingCreditCardCollider.isTrigger = true;
            ApplyBoardingCreditCardPose();
            return true;
        }

        private Transform ResolveCreditCardVisual()
        {
            if (boardingCreditCardVisual != null && string.Equals(boardingCreditCardVisual.name, BoardingCreditCardVisualName, StringComparison.Ordinal))
            {
                return boardingCreditCardVisual;
            }

            boardingCreditCardVisual = FindExactNamedDescendant(boardingCreditCard, BoardingCreditCardVisualName);
            return boardingCreditCardVisual;
        }

        private Material CreateBoardingCardMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Unlit/Color");
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.58f);
            }

            return material;
        }

        private void CreateBoardingCardDetailPair(string detailName, Vector3 frontLocalPosition, Vector3 localScale, Material material)
        {
            CreateBoardingCardDetail(detailName, frontLocalPosition, localScale, material);
            CreateBoardingCardDetail(detailName + " Back", new Vector3(frontLocalPosition.x, frontLocalPosition.y, -frontLocalPosition.z), localScale, material);
        }

        private void CreateBoardingCardDetail(string detailName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            if (boardingCreditCard == null)
            {
                return;
            }

            Transform detailParent = ResolveCreditCardVisual() != null ? boardingCreditCardVisual : boardingCreditCard;
            if (FindExactNamedDescendant(detailParent, detailName) != null)
            {
                return;
            }

            GameObject detail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            detail.name = detailName;
            Transform detailTransform = detail.transform;
            detailTransform.SetParent(detailParent, false);
            detailTransform.localPosition = localPosition;
            detailTransform.localRotation = Quaternion.identity;
            detailTransform.localScale = localScale;

            Collider detailCollider = detail.GetComponent<Collider>();
            if (detailCollider != null)
            {
                Destroy(detailCollider);
            }

            Renderer renderer = detail.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void ApplyBoardingCreditCardPose()
        {
            if (boardingCreditCard == null)
            {
                return;
            }

            Transform parent = ResolveRightHandTransform();
            if (parent != null && boardingCreditCard.parent != parent)
            {
                boardingCreditCard.SetParent(parent, false);
            }

            boardingCreditCard.localPosition = GetBoardingCreditCardLocalPosition();
            boardingCreditCard.localRotation = Quaternion.Euler(GetBoardingCreditCardLocalRotation());
            boardingCreditCard.localScale = Vector3.one;
            ApplyBoardingCreditCardWorldScale();
        }

        private void ApplyBoardingCreditCardWorldScale()
        {
            Transform visual = ResolveCreditCardVisual();
            if (boardingCreditCard == null || visual == null)
            {
                return;
            }

            Vector3 targetWorldSize = GetBoardingCreditCardWorldSize();
            Vector3 parentScale = boardingCreditCard.lossyScale;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(
                DivideByNonZero(targetWorldSize.x, parentScale.x),
                DivideByNonZero(targetWorldSize.y, parentScale.y),
                DivideByNonZero(targetWorldSize.z, parentScale.z));
        }

        private static float DivideByNonZero(float value, float divisor)
        {
            float safeDivisor = Mathf.Abs(divisor) > 0.00001f ? Mathf.Abs(divisor) : 1f;
            return value / safeDivisor;
        }

        private void SetBoardingCreditCardActive(bool active)
        {
            if (active && !EnsureBoardingCreditCard())
            {
                return;
            }

            if (boardingCreditCard == null)
            {
                return;
            }

            ApplyBoardingCreditCardPose();
            if (boardingCreditCard.gameObject.activeSelf != active)
            {
                boardingCreditCard.gameObject.SetActive(active);
            }

            if (active && !hasLoggedBoardingCreditCardShown)
            {
                hasLoggedBoardingCreditCardShown = true;
                Vector3 targetWorldSize = GetBoardingCreditCardWorldSize();
                string boundsText = TryGetRendererBounds(boardingCreditCard, out Bounds bounds)
                    ? ", visualBoundsSize=" + FormatPosition(bounds.size) + ", visualBoundsCenter=" + FormatPosition(bounds.center)
                    : ", visualBounds=missing";
                CocoonDebugLog.Info(
                    "DoorUI",
                    "Right-hand boarding credit card shown for prepay tap. parent=" +
                    GetTransformPath(boardingCreditCard.parent) +
                    ", anchor=" + GetTransformPath(boardingCreditCard) +
                    ", targetWorldSize=" + FormatPosition(targetWorldSize) +
                    ", renderers=" + CountEnabledRenderers(boardingCreditCard) +
                    boundsText + ".",
                    this);
            }
        }

        private bool IsBoardingCreditCardTouchingZone(Collider touchCollider)
        {
            if (touchCollider == null || boardingCreditCard == null || !boardingCreditCard.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (boardingCreditCardCollider == null)
            {
                Transform visual = ResolveCreditCardVisual();
                boardingCreditCardCollider = visual != null ? visual.GetComponent<Collider>() : boardingCreditCard.GetComponent<Collider>();
            }

            if (boardingCreditCardCollider == null)
            {
                return false;
            }

            Bounds zoneBounds = touchCollider.bounds;
            float padding = Mathf.Max(0.002f, GetBoardingCreditCardBoundsPadding());
            zoneBounds.Expand(padding);
            Bounds cardBounds = boardingCreditCardCollider.bounds;
            cardBounds.Expand(padding);
            bool overlaps = zoneBounds.Intersects(cardBounds);
            if (overlaps && !hasLoggedBoardingCreditCardTouch)
            {
                hasLoggedBoardingCreditCardTouch = true;
                CocoonDebugLog.Info("DoorUI", "Credit card bounds overlapped DOORUI zone.", this);
            }

            if (!overlaps && Time.time >= nextBoardingCreditCardMissLogTime)
            {
                nextBoardingCreditCardMissLogTime = Time.time + 1.5f;
                float distance = Vector3.Distance(cardBounds.center, zoneBounds.center);
                CocoonDebugLog.Info(
                    "DoorUI",
                    "Credit card visible but not touching DOORUI. distance=" + distance.ToString("0.000") +
                    "m, cardCenter=" + FormatPosition(cardBounds.center) +
                    ", cardSize=" + FormatPosition(cardBounds.size) +
                    ", zoneCenter=" + FormatPosition(zoneBounds.center) +
                    ", zoneSize=" + FormatPosition(zoneBounds.size) + ".",
                    this);
            }

            return overlaps;
        }

        private static int CountEnabledRenderers(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private Vector3 GetBoardingCreditCardLocalPosition()
        {
            CocoonBoardingCreditCardSettings settings = ResolveBoardingCreditCardSettings();
            return settings != null ? settings.LocalPosition : cardLocalPosition;
        }

        private Vector3 GetBoardingCreditCardLocalRotation()
        {
            CocoonBoardingCreditCardSettings settings = ResolveBoardingCreditCardSettings();
            return settings != null ? settings.LocalRotation : cardLocalRotation;
        }

        private Vector3 GetBoardingCreditCardWorldSize()
        {
            CocoonBoardingCreditCardSettings settings = ResolveBoardingCreditCardSettings();
            if (settings != null)
            {
                return settings.WorldSize;
            }

            return new Vector3(
                Mathf.Max(0.0005f, Mathf.Abs(cardLocalScale.x)),
                Mathf.Max(0.0005f, Mathf.Abs(cardLocalScale.y)),
                Mathf.Max(0.0002f, Mathf.Abs(cardLocalScale.z)));
        }

        private float GetBoardingCreditCardBoundsPadding()
        {
            CocoonBoardingCreditCardSettings settings = ResolveBoardingCreditCardSettings();
            return settings != null ? settings.BoundsPadding : Mathf.Clamp(cardBoundsPadding, 0.001f, 0.08f);
        }

        private CocoonBoardingCreditCardSettings ResolveBoardingCreditCardSettings()
        {
            if (boardingCreditCardSettings != null)
            {
                return boardingCreditCardSettings;
            }

            CocoonBoardingCreditCardSettings[] settings = Resources.FindObjectsOfTypeAll<CocoonBoardingCreditCardSettings>();
            for (int i = 0; i < settings.Length; i++)
            {
                CocoonBoardingCreditCardSettings candidate = settings[i];
                if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                {
                    boardingCreditCardSettings = candidate;
                    return boardingCreditCardSettings;
                }
            }

            return null;
        }

        private Transform ResolveLeftHandTransform()
        {
            if (leftHand != null)
            {
                return leftHand;
            }

            Transform root = ResolveRiderRootTransform();
            if (root != null)
            {
                leftHand = FindNamedDescendant(root, "LeftHand") ??
                           FindNamedDescendant(root, "Left Hand") ??
                           FindNamedDescendant(root, "Left Controller") ??
                           FindNamedDescendant(root, "LeftHand Controller");
                if (leftHand != null)
                {
                    return leftHand;
                }
            }

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                string name = candidate.name;
                if (name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (name.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    leftHand = candidate;
                    return leftHand;
                }
            }

            return null;
        }

        private void SetPanelActive(bool active)
        {
            if (!active)
            {
                SetDoorPanelObjectActive(doorSideAPanel, false);
                SetDoorPanelObjectActive(doorSideBPanel, false);
                SetDoorPanelObjectActive(doorPanel, false);
                activeDoorPanel = null;
                return;
            }

            ResolveDoorPanelReference();

            if (!hasSelectedSlidingDoorSide)
            {
                SelectSlidingDoorSideForBoarding();
            }

            GameObject selectedPanel = GetDoorPanelForSide(activeSlidingDoorSide);
            if (selectedPanel == null)
            {
                CocoonDebugLog.Warn("DoorUI", "Door panel reference missing; cannot show onboarding controls.", this);
                hasLoggedMissingDoorPanel = true;
                return;
            }

            SetDoorPanelObjectActive(doorSideAPanel, selectedPanel == doorSideAPanel);
            SetDoorPanelObjectActive(doorSideBPanel, selectedPanel == doorSideBPanel);
            SetDoorPanelObjectActive(doorPanel, selectedPanel == doorPanel);

            if (activeDoorPanel != selectedPanel)
            {
                activeDoorPanel = selectedPanel;
                CocoonDebugLog.Info("DoorUI", "Door panel shown for sliding door side " + activeSlidingDoorSide + ": " + selectedPanel.name + ".", this);
            }

            PrepareDoorDecisionPanel(selectedPanel.transform);
        }

        private void SetDoorPanelObjectActive(GameObject panel, bool active)
        {
            if (panel != null && panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }

        private GameObject GetDoorPanelForSide(CocoonTaxiDoorSide side)
        {
            if (side == CocoonTaxiDoorSide.A && doorSideAPanel != null)
            {
                return doorSideAPanel;
            }

            if (side == CocoonTaxiDoorSide.B && doorSideBPanel != null)
            {
                return doorSideBPanel;
            }

            return doorPanel;
        }

        private void ResolveDoorPanelReference()
        {
            if (doorPanel == null || doorSideAPanel == null || doorSideBPanel == null)
            {
                ResolveSlidingDoorPanels();
                Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    if (!IsDoorPanelCanvasCandidate(canvas))
                    {
                        continue;
                    }

                    GameObject panelObject = canvas.gameObject;
                    string panelName = panelObject.name;
                    if (doorSideAPanel == null && IsDoorPanelNameForSide(panelName, CocoonTaxiDoorSide.A))
                    {
                        doorSideAPanel = panelObject;
                    }
                    else if (doorSideBPanel == null && IsDoorPanelNameForSide(panelName, CocoonTaxiDoorSide.B))
                    {
                        doorSideBPanel = panelObject;
                    }
                    else if (doorPanel == null && string.Equals(panelName, "Door-side Onboarding UI", StringComparison.OrdinalIgnoreCase))
                    {
                        doorPanel = panelObject;
                    }
                }

                if (HasSlidingDoorPanels())
                {
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        Canvas canvas = canvases[i];
                        if (!IsDoorPanelCanvasCandidate(canvas))
                        {
                            continue;
                        }

                        GameObject panelObject = canvas.gameObject;
                        if (panelObject == doorSideAPanel || panelObject == doorSideBPanel || panelObject == doorPanel)
                        {
                            continue;
                        }

                        CocoonTaxiDoorSide nearestSide = GetNearestSlidingDoorSide(panelObject.transform.position);
                        if (nearestSide == CocoonTaxiDoorSide.A && doorSideAPanel == null)
                        {
                            doorSideAPanel = panelObject;
                        }
                        else if (nearestSide == CocoonTaxiDoorSide.B && doorSideBPanel == null)
                        {
                            doorSideBPanel = panelObject;
                        }
                        else if (doorPanel == null)
                        {
                            doorPanel = panelObject;
                        }
                    }
                }

                if (doorSideAPanel != null || doorSideBPanel != null || doorPanel != null)
                {
                    hasLoggedMissingDoorPanel = false;
                }
            }

            if (doorPanel == null && doorSideAPanel == null && doorSideBPanel == null)
            {
                ResolveDoorHingeReference();
                if (doorHinge != null && !preserveAuthoredTaxiComposition)
                {
                    CreateRuntimeDoorPanel();
                }
                else if (doorHinge != null && !hasLoggedMissingDoorPanel)
                {
                    CocoonDebugLog.Info("DoorUI", "Door panel missing; authored taxi composition preserved, no runtime door UI created.", this);
                    hasLoggedMissingDoorPanel = true;
                }
            }
        }

        private bool IsDoorPanelCanvasCandidate(Canvas canvas)
        {
            if (canvas == null || canvas.gameObject.scene != gameObject.scene)
            {
                return false;
            }

            string panelName = canvas.name;
            if (panelName.IndexOf("Door-side Onboarding UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                panelName.IndexOf("Boarding Door UI", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return canvas.transform.Find("Confirm Ride Button") != null &&
                   canvas.transform.Find("Leave Button") != null;
        }

        private static bool IsDoorPanelNameForSide(string panelName, CocoonTaxiDoorSide side)
        {
            if (string.IsNullOrEmpty(panelName))
            {
                return false;
            }

            string lower = panelName.ToLowerInvariant();
            if (side == CocoonTaxiDoorSide.A)
            {
                return lower.Contains("side a") ||
                       lower.Contains("door a") ||
                       lower.EndsWith(" a") ||
                       lower.EndsWith("-a") ||
                       lower.EndsWith("_a");
            }

            return lower.Contains("side b") ||
                   lower.Contains("door b") ||
                   lower.EndsWith(" b") ||
                   lower.EndsWith("-b") ||
                   lower.EndsWith("_b");
        }

        private CocoonTaxiDoorSide GetNearestSlidingDoorSide(Vector3 position)
        {
            float distanceA = Vector3.SqrMagnitude(GetSlidingDoorSideCentroid(CocoonTaxiDoorSide.A) - position);
            float distanceB = Vector3.SqrMagnitude(GetSlidingDoorSideCentroid(CocoonTaxiDoorSide.B) - position);
            return distanceB < distanceA ? CocoonTaxiDoorSide.B : CocoonTaxiDoorSide.A;
        }

        private void ResolveDoorHingeReference()
        {
            if (doorHinge != null)
            {
                return;
            }

            if (taxiRoot != null)
            {
                Transform localHinge = taxiRoot.Find("Taxi Pod Visuals/Passenger Door Hinge");
                if (localHinge == null)
                {
                    localHinge = taxiRoot.Find("Taxi Pod Visuals/Cocoon Interaction Fixtures/Passenger Door Hinge");
                }

                if (localHinge != null)
                {
                    doorHinge = localHinge;
                    CocoonDebugLog.Info("DoorUI", "Passenger door hinge auto-bound from taxi visuals.", this);
                    return;
                }
            }

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null &&
                    transforms[i].name == "Passenger Door Hinge" &&
                    transforms[i].gameObject.scene == gameObject.scene)
                {
                    doorHinge = transforms[i];
                    CocoonDebugLog.Info("DoorUI", "Passenger door hinge auto-bound from scene.", this);
                    return;
                }
            }
        }

        private void CreateRuntimeDoorPanel()
        {
            GameObject canvasObject = new GameObject("Door-side Onboarding UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(doorHinge, false);
            rect.sizeDelta = new Vector2(760f, 560f);
            canvasObject.transform.localPosition = DoorPanelLocalPosition;
            canvasObject.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.00095f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 768f;
            scaler.referencePixelsPerUnit = 100f;

            AddRuntimePanelBackground(canvasObject.transform, new Vector2(760f, 560f));
            doorPanel = canvasObject;
            PrepareDoorDecisionPanel(canvasObject.transform);
            hasLoggedMissingDoorPanel = false;
            doorPanel.SetActive(false);
            CocoonDebugLog.Info("DoorUI", "Door panel auto-created on passenger door.", this);
        }

        private static void AddRuntimePanelBackground(Transform parent, Vector2 size)
        {
            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = background.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.032f, 0.985f);
        }

        private Text CreateRuntimePanelText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = GetRuntimeFont();
            ConfigureRuntimePanelText(text, anchoredPosition, size, fontSize, color, alignment, value);

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2.3f, -2.3f);
            return text;
        }

        private GameObject CreateRuntimePanelButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, string label, CocoonButtonAction action, string payload)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BoxCollider), typeof(CocoonWorldButton));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image image = button.GetComponent<Image>();
            ConfigureRuntimePanelButton(button.transform, anchoredPosition, size, label, action, payload, image);
            return button;
        }

        private static Font GetRuntimeFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void PrepareDoorDecisionPanel(Transform panelTransform)
        {
            PrepareDoorPanelSurface(panelTransform);

            doorHeadlineText = EnsureRuntimePanelText("Door Panel Title", panelTransform, new Vector2(0f, 194f), new Vector2(650f, 72f), 78, Color.white, TextAnchor.MiddleCenter, "BOARD COCOON?");
            doorStatusText = EnsureRuntimePanelText("Door Panel Status", panelTransform, new Vector2(0f, 104f), new Vector2(650f, 64f), 40, new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, "Right controller: A confirms, B leaves.");
            destinationText = EnsureRuntimePanelText("Door Panel Destination", panelTransform, new Vector2(0f, -4f), new Vector2(650f, 152f), 38, new Color(0.1f, 1f, 0.55f), TextAnchor.MiddleLeft, BuildRideSummaryText());
            doorTimerText = EnsureRuntimePanelText("Door Panel Timer", panelTransform, new Vector2(0f, -226f), new Vector2(540f, 46f), 40, new Color(0.95f, 0.86f, 0.38f), TextAnchor.MiddleCenter, "Decision");

            EnsureRuntimePanelButton("Confirm Ride Button", panelTransform, new Vector2(-142f, -142f), new Vector2(248f, 78f), "A\nCONFIRM", CocoonButtonAction.ConfirmRide, "");
            EnsureRuntimePanelButton("Leave Button", panelTransform, new Vector2(142f, -142f), new Vector2(248f, 78f), "B\nLEAVE", CocoonButtonAction.DeclineRide, "");

            SetPanelChildActive(panelTransform, "Contactless Hint", false);
            SetPanelChildActive(panelTransform, "Duomo Button", false);
            SetPanelChildActive(panelTransform, "Central Button", false);
            SetPanelChildActive(panelTransform, "Brera Button", false);
            SetPanelChildActive(panelTransform, "Solo Button", false);
            SetPanelChildActive(panelTransform, "Shared Button", false);
            SetPanelChildActive(panelTransform, "Reset Button", false);
        }

        private void AlignDoorPanelToDoor(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return;
            }

            if (ShouldUseSlidingDoorPanelAnchor() && TryAlignDoorPanelToSlidingDoor(panelTransform))
            {
                return;
            }

            ResolveDoorHingeReference();
            if (doorHinge != null && panelTransform.parent != doorHinge)
            {
                panelTransform.SetParent(doorHinge, false);
            }

            panelTransform.localPosition = DoorPanelLocalPosition;
            panelTransform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            panelTransform.localScale = Vector3.one * 0.00095f;
        }

        private bool ShouldUseSlidingDoorPanelAnchor()
        {
            return state == CocoonTaxiState.ConfirmRide ||
                   state == CocoonTaxiState.PaymentConfirmed ||
                   state == CocoonTaxiState.DoorOpening ||
                   state == CocoonTaxiState.DoorOpen ||
                   state == CocoonTaxiState.DoorClosing;
        }

        private bool TryAlignDoorPanelToSlidingDoor(Transform panelTransform)
        {
            ResolveSlidingDoorPanels();
            if (!HasSlidingDoorPanels())
            {
                return false;
            }

            if (!hasSelectedSlidingDoorSide)
            {
                SelectSlidingDoorSideForBoarding();
            }

            Transform anchor = GetOrCreateSlidingDoorUiAnchor();
            if (anchor == null)
            {
                return false;
            }

            float experienceScale = GetExperienceScale();
            Vector3 outward = GetSlidingDoorPanelNormalDirection(activeSlidingDoorSide);
            Vector3 center = GetSlidingDoorSideVisualCenter(activeSlidingDoorSide);
            anchor.position = center + outward * (GetBoardingDoorPanelOutwardOffset() * experienceScale);
            anchor.rotation = Quaternion.LookRotation(outward, Vector3.up);

            if (panelTransform.parent != anchor)
            {
                panelTransform.SetParent(anchor, false);
            }

            RectTransform rect = panelTransform as RectTransform;
            Vector2 panelSize = GetBoardingDoorPanelSize();
            if (rect != null && panelSize.x > 1f && panelSize.y > 1f)
            {
                rect.sizeDelta = panelSize;
            }

            panelTransform.localPosition = GetBoardingDoorPanelLocalOffset();
            panelTransform.localRotation = Quaternion.identity;
            panelTransform.localScale = Vector3.one * Mathf.Max(0.00005f, GetBoardingDoorPanelScale());
            return true;
        }

        private Vector3 GetSlidingDoorPanelNormalDirection(CocoonTaxiDoorSide side)
        {
            Vector3 travelForward = ResolveBoardingDoorTravelForward();
            Vector3 panelNormal = Vector3.Cross(travelForward, Vector3.up);
            if (panelNormal.sqrMagnitude < 0.0001f)
            {
                return GetSlidingDoorOutwardDirection(side);
            }

            panelNormal.Normalize();
            Vector3 sideCenter = GetSlidingDoorSideVisualCenter(side);
            Vector3 toBoardingSide = ResolveBoardingDoorSideReferencePoint() - sideCenter;
            toBoardingSide.y = 0f;
            if (toBoardingSide.sqrMagnitude > 0.0001f && Vector3.Dot(panelNormal, toBoardingSide) < 0f)
            {
                panelNormal = -panelNormal;
            }

            return panelNormal;
        }

        private void ApplyLuggageRamp(float open01)
        {
            ResolveLuggageRampReference();
            open01 = Smooth01(Mathf.Clamp01(open01));
            ApplyAuthoredRamp(
                luggageBoardingRamp1,
                ref luggageRampPose1,
                open01,
                hasAuthoredLuggageRampStandbyPose,
                luggageRamp1StandbyLocalPosition,
                luggageRamp1StandbyLocalRotation,
                luggageRamp1StandbyLocalScale);
            ApplyAuthoredRamp(
                luggageBoardingRamp2,
                ref luggageRampPose2,
                open01,
                hasAuthoredLuggageRampStandbyPose,
                luggageRamp2StandbyLocalPosition,
                luggageRamp2StandbyLocalRotation,
                luggageRamp2StandbyLocalScale);
            if (open01 <= 0.001f && !hasLoggedRampInitialStandby)
            {
                hasLoggedRampInitialStandby = true;
                CocoonDebugLog.Info(
                    "Door",
                    "Ramp initial standby applied from authored taxi pose. ramp1=" + (luggageBoardingRamp1 != null ? FormatPosition(luggageBoardingRamp1.localPosition) : "null") +
                    ", ramp2=" + (luggageBoardingRamp2 != null ? FormatPosition(luggageBoardingRamp2.localPosition) : "null") +
                    ".",
                    this);
            }
        }

        private Vector3 GetSlidingDoorRampHingeCenter(CocoonTaxiDoorSide side)
        {
            if (TryGetSlidingDoorSideBounds(side, out Bounds bounds))
            {
                Vector3 center = bounds.center;
                center.y = bounds.min.y;
                return center;
            }

            Vector3 fallback = GetSlidingDoorSideVisualCenter(side);
            if (taxiRoot != null)
            {
                fallback.y = taxiRoot.position.y;
            }

            return fallback;
        }

        private void ResolveLuggageRampReference()
        {
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            Transform previousRamp1 = luggageBoardingRamp1;
            Transform previousRamp2 = luggageBoardingRamp2;
            Transform previousMarker1 = luggageRampClosedMarker1;
            Transform previousMarker2 = luggageRampClosedMarker2;

            luggageBoardingRamp1 = ResolveExactNamedReference(searchRoot, luggageBoardingRamp1, LuggageRamp1Name);
            luggageBoardingRamp2 = ResolveExactNamedReference(searchRoot, luggageBoardingRamp2, LuggageRamp2Name);
            luggageRampClosedMarker1 = ResolveExactNamedReference(searchRoot, luggageRampClosedMarker1, LuggageRampClosedMarker1Name);
            luggageRampClosedMarker2 = ResolveExactNamedReference(searchRoot, luggageRampClosedMarker2, LuggageRampClosedMarker2Name);

            if (previousRamp1 != luggageBoardingRamp1)
            {
                luggageRampPose1 = new AuthoredRampPose();
            }

            if (previousRamp2 != luggageBoardingRamp2)
            {
                luggageRampPose2 = new AuthoredRampPose();
            }

            if (previousRamp1 != luggageBoardingRamp1 || previousRamp2 != luggageBoardingRamp2 ||
                previousMarker1 != luggageRampClosedMarker1 || previousMarker2 != luggageRampClosedMarker2)
            {
                hasLoggedAuthoredRampBinding = false;
                hasLoggedMissingAuthoredRamps = false;
            }

            SetMarkerRenderersVisible(luggageRampClosedMarker1, false);
            SetMarkerRenderersVisible(luggageRampClosedMarker2, false);

            if (luggageBoardingRamp1 != null && luggageBoardingRamp2 != null)
            {
                if (!hasLoggedAuthoredRampBinding)
                {
                    hasLoggedAuthoredRampBinding = true;
                    CocoonDebugLog.Info(
                        "Door",
                        "Authored luggage ramps bound exactly: " +
                        "ramp1=" + luggageBoardingRamp1.name +
                        ", ramp2=" + luggageBoardingRamp2.name +
                        ", deployedLocal=zero.",
                        this);
                }

                return;
            }

            if (!hasLoggedMissingAuthoredRamps)
            {
                hasLoggedMissingAuthoredRamps = true;
                CocoonDebugLog.Warn("Door", "Authored luggage ramp binding incomplete; expected Luggage Boarding Ramp1/2.", this);
            }
        }

        private void ApplyAuthoredRamp(
            Transform ramp,
            ref AuthoredRampPose pose,
            float open01,
            bool hasSerializedStandbyPose,
            Vector3 serializedStandbyLocalPosition,
            Quaternion serializedStandbyLocalRotation,
            Vector3 serializedStandbyLocalScale)
        {
            if (ramp == null)
            {
                return;
            }

            if (!pose.HasPose)
            {
                pose.Capture(
                    ramp,
                    hasSerializedStandbyPose,
                    serializedStandbyLocalPosition,
                    serializedStandbyLocalRotation,
                    serializedStandbyLocalScale);
                if (pose.HasPose)
                {
                    CocoonDebugLog.Info(
                        "Door",
                        "Ramp pose resolved for " + ramp.name +
                        ": standbySource=" + (hasSerializedStandbyPose ? "serialized-authored-standby" : "current-transform") +
                        ", standby=" + FormatPosition(pose.StandbyLocalPosition) +
                        ", deployed=local zero.",
                        this);
                }
            }

            if (!ramp.gameObject.activeSelf)
            {
                ramp.gameObject.SetActive(true);
            }

            ramp.localPosition = Vector3.Lerp(pose.StandbyLocalPosition, Vector3.zero, open01);
            ramp.localRotation = Quaternion.Slerp(pose.StandbyLocalRotation, Quaternion.identity, open01);
            ramp.localScale = Vector3.Lerp(pose.StandbyLocalScale, Vector3.one, open01);
        }

        private static Transform ResolveExactNamedReference(Transform searchRoot, Transform current, string expectedName)
        {
            if (current != null && string.Equals(current.name, expectedName, StringComparison.Ordinal))
            {
                return current;
            }

            return FindExactNamedDescendant(searchRoot, expectedName);
        }

        private static Transform ResolveAliasedNamedReference(Transform searchRoot, Transform current, string[] expectedNames)
        {
            if (expectedNames == null || expectedNames.Length == 0)
            {
                return current;
            }

            if (current != null)
            {
                for (int i = 0; i < expectedNames.Length; i++)
                {
                    if (string.Equals(current.name, expectedNames[i], StringComparison.Ordinal))
                    {
                        return current;
                    }
                }
            }

            for (int i = 0; i < expectedNames.Length; i++)
            {
                Transform match = FindExactNamedDescendant(searchRoot, expectedNames[i]);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform ResolvePreferredSeatMarkerReference(
            Transform searchRoot,
            Transform current,
            string[] expectedNames)
        {
            Transform seatAnchor = FindExactNamedDescendant(searchRoot, Seat1AnchorName);
            Transform match = FindExactNamedDescendantAny(seatAnchor, expectedNames);
            if (match != null)
            {
                return match;
            }

            match = FindExactNamedDescendantAny(searchRoot, expectedNames);
            if (match != null)
            {
                return match;
            }

            return IsNamedOneOf(current, expectedNames) ? current : null;
        }

        private static Transform FindExactNamedDescendantAny(Transform root, string[] targetNames)
        {
            if (targetNames == null)
            {
                return null;
            }

            for (int i = 0; i < targetNames.Length; i++)
            {
                Transform match = FindExactNamedDescendant(root, targetNames[i]);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static bool IsNamedOneOf(Transform target, string[] expectedNames)
        {
            if (target == null || expectedNames == null)
            {
                return false;
            }

            for (int i = 0; i < expectedNames.Length; i++)
            {
                if (string.Equals(target.name, expectedNames[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
                if (transforms[i] != null && string.Equals(transforms[i].name, targetName, StringComparison.Ordinal))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private Transform FindSceneTransformExact(string targetName)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (string.Equals(candidate.name, targetName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private Transform ResolveDestinationStopPoint()
        {
            if (destinationStopPoint != null &&
                string.Equals(destinationStopPoint.name, "DESTINATION", StringComparison.Ordinal) &&
                destinationStopPoint.gameObject.scene == gameObject.scene)
            {
                return destinationStopPoint;
            }

            Transform marker = FindSceneTransformExact("DESTINATION");
            if (marker != null && !hasLoggedDestinationStopPoint)
            {
                hasLoggedDestinationStopPoint = true;
                CocoonDebugLog.Info("Destination", "Manual DESTINATION marker bound at " + FormatPosition(marker.position) + " path=" + GetTransformPath(marker) + ".", this);
            }

            return marker;
        }

        private bool TryResolveDestinationStopCursor(bool logSuccess)
        {
            hasDestinationStopCursor = false;
            destinationStopCursor = CocoonRouteCursor.Invalid();

            if (destinationStopPoint == null)
            {
                destinationStopPoint = ResolveDestinationStopPoint();
            }

            if (destinationStopPoint == null)
            {
                return false;
            }

            if (!EnsureRoadGraphBinding())
            {
                if (!hasLoggedDestinationGraphProjectionFailure)
                {
                    hasLoggedDestinationGraphProjectionFailure = true;
                    CocoonDebugLog.Warn("Destination", "Cannot project DESTINATION to ROADMAP graph because the taxi graph binding is unavailable. Taxi will keep legal cruising; no straight-line fallback will be used.", this);
                }

                return false;
            }

            if (!roadGraph.TryProjectToGraph(destinationStopPoint.position, out destinationStopCursor))
            {
                if (!hasLoggedDestinationGraphProjectionFailure)
                {
                    hasLoggedDestinationGraphProjectionFailure = true;
                    CocoonDebugLog.Warn("Destination", "DESTINATION marker could not be projected to a legal ROADMAP edge at " + FormatPosition(destinationStopPoint.position) + ". Taxi will keep legal cruising; no straight-line fallback will be used.", this);
                }

                return false;
            }

            hasDestinationStopCursor = true;
            hasLoggedDestinationGraphProjectionFailure = false;
            hasLoggedDestinationGraphDistanceFailure = false;
            if (logSuccess)
            {
                string remainingText = "?";
                if (roadGraph.TryGetForwardDistance(taxiGraphCursor, destinationStopCursor, out float forwardDistance, Mathf.Max(16, roadGraph.EdgeCount + 8)))
                {
                    remainingText = forwardDistance.ToString("0.###") + "m";
                }

                CocoonDebugLog.Info("Destination", "DESTINATION projected to ROADMAP graph edge " + destinationStopCursor.EdgeId + " progress=" + destinationStopCursor.Progress.ToString("0.###") + ", forwardDistance=" + remainingText + ".", this);
            }

            return true;
        }

        private bool TryRebindTaxiGraphCursorAtCurrentPosition(string reason)
        {
            if (!useRoadGraph)
            {
                return true;
            }

            if (roadGraph == null)
            {
                roadGraph = FindObjectOfType<CocoonRoadGraph>();
            }

            if (roadGraph == null || roadGraph.EdgeCount == 0 || taxiRoot == null)
            {
                if (!hasLoggedDestinationDepartureProjectionFailure)
                {
                    hasLoggedDestinationDepartureProjectionFailure = true;
                    CocoonDebugLog.Warn("Destination", "Cannot rebind taxi to ROADMAP graph for " + reason + "; graph or taxi root is unavailable. Taxi will not use a straight-line departure fallback.", this);
                }

                return false;
            }

            if (!roadGraph.TryProjectToGraph(taxiRoot.position, out CocoonRouteCursor reboundCursor))
            {
                if (!hasLoggedDestinationDepartureProjectionFailure)
                {
                    hasLoggedDestinationDepartureProjectionFailure = true;
                    CocoonDebugLog.Warn("Destination", "Cannot rebind taxi to ROADMAP graph for " + reason + " from " + FormatPosition(taxiRoot.position) + ". Taxi will stay stopped rather than departing through buildings.", this);
                }

                return false;
            }

            taxiGraphCursor = reboundCursor;
            hasLoggedDestinationDepartureProjectionFailure = false;
            EnsureTrafficParticipant();
            if (trafficParticipant != null)
            {
                trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
                trafficParticipant.SetGraphCursor(taxiGraphCursor);
            }

            CocoonDebugLog.Info("Destination", "Taxi rebound to ROADMAP graph for " + reason + ": edge=" + taxiGraphCursor.EdgeId + ", progress=" + taxiGraphCursor.Progress.ToString("0.###") + ".", this);
            return true;
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
                if (renderer == null || !renderer.enabled || IsUnderNamedAncestor(renderer.transform, BoardingTouchZoneName))
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

        private void LogRampDeployMilestones(float previousOpen01, float currentOpen01)
        {
            if (!hasLoggedRampDeployHalfway && previousOpen01 < 0.5f && currentOpen01 >= 0.5f)
            {
                hasLoggedRampDeployHalfway = true;
                CocoonDebugLog.Info("Door", "Luggage ramp deployment halfway.", this);
            }

            if (!hasLoggedRampDeployReady && previousOpen01 < 0.995f && currentOpen01 >= 0.995f)
            {
                hasLoggedRampDeployReady = true;
                CocoonDebugLog.Info("Door", "Luggage ramp deployed to local zero.", this);
            }
        }

        private static void SetMarkerRenderersVisible(Transform marker, bool visible)
        {
            if (marker == null)
            {
                return;
            }

            Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
        }

        private Vector3 ResolveBoardingDoorTravelForward()
        {
            if (parkingApproachForward.sqrMagnitude > 0.0001f)
            {
                return FlattenDirection(parkingApproachForward, Vector3.forward);
            }

            Transform anchor = GetPhysicalPullOverAnchor(selectedPullOverPoint);
            if (anchor != null)
            {
                Vector3 anchorForward = ResolveParkingApproachForward(anchor);
                if (anchorForward.sqrMagnitude > 0.0001f)
                {
                    return FlattenDirection(anchorForward, Vector3.forward);
                }
            }

            CocoonTrafficLanePath panelPath = selectedPullOverCruisePath != null ? selectedPullOverCruisePath : cruisePath;
            int panelPathIndex = selectedPullOverPathIndex >= 0 ? selectedPullOverPathIndex : NormalizePathIndex(panelPath, cruiseTargetIndex);
            Vector3 routeForward;
            if (TryGetPathSegmentForward(panelPath, panelPathIndex, out routeForward))
            {
                return routeForward;
            }

            return taxiRoot != null
                ? FlattenDirection(taxiRoot.forward, Vector3.forward)
                : FlattenDirection(transform.forward, Vector3.forward);
        }

        private CocoonBoardingDoorSideMode GetBoardingDoorSideMode()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.DoorSideMode : boardingDoorSideMode;
        }

        private bool GetBoardingDoorUseManualPanelTransform()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.UseManualPanelTransform : boardingDoorUseManualPanelTransform;
        }

        private Vector2 GetBoardingDoorPanelSize()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.PanelSize : boardingDoorPanelSize;
        }

        private Vector3 GetBoardingDoorPanelLocalOffset()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.PanelLocalOffset : boardingDoorPanelLocalOffset;
        }

        private float GetBoardingDoorPanelScale()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.PanelScale : boardingDoorPanelScale;
        }

        private float GetBoardingDoorPanelOutwardOffset()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.PanelOutwardOffset : boardingDoorPanelOutwardOffset;
        }

        private float GetBoardingDoorPushDistance()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.DoorPushDistance : boardingDoorPushDistance;
        }

        private float GetBoardingDoorSlideDistance()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.DoorSlideDistance : boardingDoorSlideDistance;
        }

        private float GetBoardingDoorPushFraction()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            return settings != null ? settings.DoorPushFraction : boardingDoorPushFraction;
        }

        private float GetBoardingDoorOpenSpeed()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            float value = settings != null ? settings.DoorOpenSpeed : boardingDoorOpenSpeed;
            return Mathf.Max(0.05f, value);
        }

        private float GetBoardingDoorCloseSpeed()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            float value = settings != null ? settings.DoorCloseSpeed : boardingDoorCloseSpeed;
            return Mathf.Max(0.05f, value);
        }

        private float GetRiderInsideRequiredSeconds()
        {
            CocoonBoardingDoorUISettings settings = ResolveBoardingDoorUiSettings();
            float value = settings != null ? settings.RiderInsideRequiredSeconds : riderInsideRequiredSeconds;
            return Mathf.Max(0.1f, value);
        }

        private CocoonBoardingDoorUISettings ResolveBoardingDoorUiSettings()
        {
            if (boardingDoorUiSettings != null)
            {
                return boardingDoorUiSettings;
            }

            CocoonBoardingDoorUISettings[] settings = Resources.FindObjectsOfTypeAll<CocoonBoardingDoorUISettings>();
            for (int i = 0; i < settings.Length; i++)
            {
                if (settings[i] != null && settings[i].gameObject.scene == gameObject.scene)
                {
                    boardingDoorUiSettings = settings[i];
                    return boardingDoorUiSettings;
                }
            }

            return null;
        }

        private Transform GetOrCreateSlidingDoorUiAnchor()
        {
            if (slidingDoorUiAnchor != null)
            {
                return slidingDoorUiAnchor;
            }

            Transform root = taxiRoot != null ? taxiRoot : transform;
            Transform existing = FindNamedDescendant(root, "Near-side Door UI Anchor");
            if (existing != null)
            {
                slidingDoorUiAnchor = existing;
                return slidingDoorUiAnchor;
            }

            var anchorObject = new GameObject("Near-side Door UI Anchor");
            slidingDoorUiAnchor = anchorObject.transform;
            slidingDoorUiAnchor.SetParent(root, true);
            return slidingDoorUiAnchor;
        }

        private Vector3 GetSlidingDoorSideVisualCenter(CocoonTaxiDoorSide side)
        {
            return TryGetSlidingDoorSideBounds(side, out Bounds bounds)
                ? bounds.center
                : GetSlidingDoorSideCentroid(side);
        }

        private bool TryGetSlidingDoorSideBounds(CocoonTaxiDoorSide side, out Bounds bounds)
        {
            if (side == CocoonTaxiDoorSide.A && HasLeftBoardingDoorPanels())
            {
                return TryGetLeftBoardingDoorBounds(out bounds);
            }

            bounds = new Bounds(GetSlidingDoorSideCentroid(side), Vector3.zero);
            bool hasBounds = false;
            for (int i = 0; i < slidingDoorPanels.Length; i++)
            {
                SlidingDoorPanel panel = slidingDoorPanels[i];
                if (panel.Panel == null || panel.Side != side)
                {
                    continue;
                }

                Renderer[] renderers = panel.Panel.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null || !renderer.enabled || IsUnderNamedAncestor(renderer.transform, BoardingTouchZoneName))
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
            }

            return hasBounds;
        }

        private bool TryGetLeftBoardingDoorBounds(out Bounds bounds)
        {
            bounds = new Bounds(GetSlidingDoorSideCentroid(CocoonTaxiDoorSide.A), Vector3.zero);
            bool hasBounds = false;
            EncapsulateRendererBounds(doorL1, ref bounds, ref hasBounds);
            EncapsulateRendererBounds(doorL2, ref bounds, ref hasBounds);
            return hasBounds;
        }

        private static Vector3 GetTransformVisualCenter(Transform root)
        {
            return TryGetRendererBounds(root, out Bounds bounds) ? bounds.center : (root != null ? root.position : Vector3.zero);
        }

        private static void EncapsulateRendererBounds(Transform root, ref Bounds bounds, ref bool hasBounds)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || IsUnderNamedAncestor(renderer.transform, BoardingTouchZoneName))
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
        }

        private static bool IsUnderNamedAncestor(Transform transform, string ancestorName)
        {
            Transform cursor = transform;
            while (cursor != null)
            {
                if (cursor.name == ancestorName)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private bool AlignInstructionPanelToRiderView()
        {
            if (instructionPanel == null)
            {
                return false;
            }

            CocoonHeadLockedUI headLockedUi = instructionPanel.GetComponent<CocoonHeadLockedUI>();
            if (headLockedUi == null)
            {
                headLockedUi = instructionPanel.AddComponent<CocoonHeadLockedUI>();
            }

            headLockedUi.SetLocalScale(GetHeadLockedPanelLocalScale());
            bool applied = headLockedUi.TryApply();
            if (ShouldApplyHeadLockedUiRuntimeLayout())
            {
                PrepareHeadLockedInstructionPanelRuntime(instructionPanel.transform);
            }
            if (applied && !hasLoggedHeadLockedInstructionPanel)
            {
                CocoonDebugLog.Info("InstructionUI", "Instruction panel locked to rider view.", this);
                hasLoggedHeadLockedInstructionPanel = true;
            }

            return applied;
        }

        private void PrepareHeadLockedInstructionPanelRuntime(Transform panelTransform)
        {
            PrepareHeadLockedRuntimePanelSurface(panelTransform);
            ConfigureRuntimeHeadLockedText(headlineText, HeadLockedPos(0f, 0.33f), HeadLockedSize(0.9f, 0.24f), HeadLockedFont(96f), Color.white, TextAnchor.MiddleLeft, headlineText != null ? headlineText.text : string.Empty, HeadLockedFont(58f));
            ConfigureRuntimeHeadLockedText(statusText, HeadLockedPos(0f, 0.12f), HeadLockedSize(0.9f, 0.28f), HeadLockedFont(64f), new Color(0.9f, 0.97f, 1f), TextAnchor.MiddleLeft, statusText != null ? statusText.text : string.Empty, HeadLockedFont(38f));
            ConfigureRuntimeHeadLockedText(instructionText, HeadLockedPos(-0.09f, -0.12f), HeadLockedSize(0.72f, 0.28f), HeadLockedFont(56f), new Color(0.82f, 0.88f, 0.9f), TextAnchor.MiddleLeft, instructionText != null ? instructionText.text : string.Empty, HeadLockedFont(34f));
            ConfigureRuntimeHeadLockedText(timerText, HeadLockedPos(0.32f, -0.36f), HeadLockedSize(0.28f, 0.18f), HeadLockedFont(62f), new Color(0.1f, 1f, 0.55f), TextAnchor.MiddleRight, timerText != null ? timerText.text : string.Empty, HeadLockedFont(34f));
        }

        private Text EnsureRuntimePanelText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value)
        {
            Text text = FindPanelText(parent, name);
            if (text == null)
            {
                return CreateRuntimePanelText(name, parent, anchoredPosition, size, fontSize, color, alignment, value);
            }

            text.gameObject.SetActive(true);
            ConfigureRuntimePanelText(text, anchoredPosition, size, fontSize, color, alignment, value);
            return text;
        }

        private static void ConfigureRuntimePanelText(Text text, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value)
        {
            if (text == null)
            {
                return;
            }

            RectTransform rect = text.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = size;
                rect.anchoredPosition = anchoredPosition;
            }

            text.text = value;
            text.font = text.font != null ? text.font : GetRuntimeFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = fontSize;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.SetAllDirty();
        }

        private void PrepareHeadLockedRuntimePanelSurface(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return;
            }

            Vector2 panelSize = GetHeadLockedPanelSize();
            RectTransform panelRect = panelTransform as RectTransform;
            if (panelRect != null)
            {
                panelRect.sizeDelta = panelSize;
            }

            Canvas canvas = panelTransform.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.pixelPerfect = false;
                canvas.worldCamera = Camera.main;
            }

            CanvasScaler scaler = panelTransform.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = GetHeadLockedPixelsPerUnit();
                scaler.referencePixelsPerUnit = 100f;
            }

            CocoonHeadLockedUI headLockedUi = panelTransform.GetComponent<CocoonHeadLockedUI>();
            if (headLockedUi != null)
            {
                headLockedUi.SetLocalScale(GetHeadLockedPanelLocalScale());
                headLockedUi.TryApply(ResolveRiderHeadTransform());
            }

            Transform background = panelTransform.Find("Background");
            RectTransform backgroundRect = background as RectTransform;
            if (backgroundRect != null)
            {
                backgroundRect.anchoredPosition = Vector2.zero;
                backgroundRect.sizeDelta = panelSize;
            }
        }

        private CocoonHeadLockedUITuning ResolveHeadLockedUiTuning()
        {
            if (headLockedUiTuning != null)
            {
                return headLockedUiTuning;
            }

            if (instructionPanel != null)
            {
                headLockedUiTuning = instructionPanel.GetComponentInParent<CocoonHeadLockedUITuning>(true);
                if (headLockedUiTuning != null)
                {
                    return headLockedUiTuning;
                }
            }

            CocoonHeadLockedUITuning[] tunings = Resources.FindObjectsOfTypeAll<CocoonHeadLockedUITuning>();
            for (int i = 0; i < tunings.Length; i++)
            {
                CocoonHeadLockedUITuning candidate = tunings[i];
                if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                {
                    headLockedUiTuning = candidate;
                    return headLockedUiTuning;
                }
            }

            return null;
        }

        private bool ShouldApplyHeadLockedUiRuntimeLayout()
        {
            CocoonHeadLockedUITuning tuning = ResolveHeadLockedUiTuning();
            return tuning != null ? tuning.ApplyRuntimeLayout : applyHeadLockedUiRuntimeLayout;
        }

        private bool ShouldSuppressHeadLockedFlowUi()
        {
            return suppressHeadLockedFlowUi;
        }

        private Vector2 GetHeadLockedPanelSize()
        {
            CocoonHeadLockedUITuning tuning = ResolveHeadLockedUiTuning();
            if (tuning != null)
            {
                return tuning.PanelSize;
            }

            return new Vector2(
                Mathf.Max(240f, headLockedPanelSize.x),
                Mathf.Max(160f, headLockedPanelSize.y));
        }

        private float GetHeadLockedPanelLocalScale()
        {
            CocoonHeadLockedUITuning tuning = ResolveHeadLockedUiTuning();
            if (tuning != null)
            {
                return tuning.PanelLocalScale;
            }

            return Mathf.Clamp(headLockedPanelLocalScale, 0.0004f, 0.004f);
        }

        private float GetHeadLockedPixelsPerUnit()
        {
            CocoonHeadLockedUITuning tuning = ResolveHeadLockedUiTuning();
            if (tuning != null)
            {
                return tuning.CanvasPixelsPerUnit;
            }

            return Mathf.Clamp(headLockedCanvasPixelsPerUnit, 24f, 256f);
        }

        private int HeadLockedFont(float baseSize)
        {
            CocoonHeadLockedUITuning tuning = ResolveHeadLockedUiTuning();
            float scale = tuning != null ? tuning.TextScale : Mathf.Max(0.2f, headLockedTextScale);
            return Mathf.RoundToInt(Mathf.Clamp(baseSize * scale, 12f, 1200f));
        }

        private Vector2 HeadLockedSize(float width01, float height01)
        {
            Vector2 size = GetHeadLockedPanelSize();
            return new Vector2(size.x * width01, size.y * height01);
        }

        private Vector2 HeadLockedPos(float x01, float y01)
        {
            Vector2 size = GetHeadLockedPanelSize();
            return new Vector2(size.x * x01, size.y * y01);
        }

        private static void ConfigureRuntimeHeadLockedText(Text text, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value, int minSize)
        {
            ConfigureRuntimePanelText(text, anchoredPosition, size, fontSize, color, alignment, value);
            if (text == null)
            {
                return;
            }

            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = fontSize;
            text.resizeTextMaxSize = fontSize;
            text.lineSpacing = 0.96f;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.SetAllDirty();
        }

        private GameObject EnsureRuntimePanelButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, string label, CocoonButtonAction action, string payload)
        {
            Transform buttonTransform = parent != null ? parent.Find(name) : null;
            if (buttonTransform == null)
            {
                return CreateRuntimePanelButton(name, parent, anchoredPosition, size, label, action, payload);
            }

            buttonTransform.gameObject.SetActive(true);
            Image image = buttonTransform.GetComponent<Image>();
            ConfigureRuntimePanelButton(buttonTransform, anchoredPosition, size, label, action, payload, image);
            return buttonTransform.gameObject;
        }

        private void ConfigureRuntimePanelButton(Transform buttonTransform, Vector2 anchoredPosition, Vector2 size, string label, CocoonButtonAction action, string payload, Graphic tintGraphic)
        {
            RectTransform rect = buttonTransform.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = size;
                rect.anchoredPosition = anchoredPosition;
            }

            Image image = buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.08f, 0.11f, 0.13f, 0.95f);
            }

            BoxCollider collider = buttonTransform.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = buttonTransform.gameObject.AddComponent<BoxCollider>();
            }

            collider.isTrigger = true;
            collider.enabled = true;
            collider.center = Vector3.zero;
            collider.size = new Vector3(size.x, size.y, Mathf.Max(48f, size.y * 0.65f));

            CocoonWorldButton button = buttonTransform.GetComponent<CocoonWorldButton>();
            if (button == null)
            {
                button = buttonTransform.gameObject.AddComponent<CocoonWorldButton>();
            }

            button.Configure(this, action, payload, tintGraphic);

            Text labelText = buttonTransform.GetComponentInChildren<Text>(true);
            if (labelText == null)
            {
                labelText = CreateRuntimePanelText(buttonTransform.name + " Text", buttonTransform, Vector2.zero, size, 36, Color.white, TextAnchor.MiddleCenter, label);
            }
            else
            {
                labelText.gameObject.SetActive(true);
                ConfigureRuntimePanelText(labelText, Vector2.zero, size, 36, Color.white, TextAnchor.MiddleCenter, label);
            }

            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 18;
            labelText.resizeTextMaxSize = 36;
            labelText.lineSpacing = 0.82f;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.fontStyle = FontStyle.Bold;
        }

        private void PrepareDoorPanelSurface(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return;
            }

            Canvas canvas = panelTransform.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
            }

            if (!GetBoardingDoorUseManualPanelTransform())
            {
                AlignDoorPanelToDoor(panelTransform);
            }
        }

        private static void SetPanelChildActive(Transform parent, string childName, bool active)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null && child.gameObject.activeSelf != active)
            {
                child.gameObject.SetActive(active);
            }
        }

        private void WireDoorPanelButtons(Transform panelTransform)
        {
            WireDoorPanelButton(panelTransform, "Confirm Ride Button", CocoonButtonAction.ConfirmRide, "");
            WireDoorPanelButton(panelTransform, "Leave Button", CocoonButtonAction.DeclineRide, "");
        }

        private void WireDoorPanelButton(Transform panelTransform, string buttonName, CocoonButtonAction action, string payload)
        {
            Transform buttonTransform = panelTransform != null ? panelTransform.Find(buttonName) : null;
            if (buttonTransform == null)
            {
                return;
            }

            CocoonWorldButton button = buttonTransform.GetComponent<CocoonWorldButton>();
            Graphic tintGraphic = buttonTransform.GetComponent<Graphic>();
            if (button != null)
            {
                button.Configure(this, action, payload, tintGraphic);
            }
        }

        private static Text FindPanelText(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform child = root.Find(name);
            if (child == null)
            {
                child = FindNamedDescendant(root, name);
            }

            return child != null ? child.GetComponent<Text>() : null;
        }

        private void SetBoardingCardPromptPanelActive(bool active)
        {
            ResolveBaggageReferences();
            if (boardingCardPromptPanel == null)
            {
                return;
            }

            if (ShouldSuppressHeadLockedFlowUi())
            {
                active = false;
            }

            if (!active)
            {
                if (boardingCardPromptPanel.activeSelf)
                {
                    boardingCardPromptPanel.SetActive(false);
                    CocoonDebugLog.Info("InstructionUI", "Boarding card prompt panel hidden.", this);
                }

                return;
            }

            PrepareHeadLockedPromptPanel(
                boardingCardPromptPanel.transform,
                "PREPAY TO OPEN",
                "Please tap your card to prepay and open the door.");
            SetPromptButtonVisualsActive(boardingCardPromptPanel.transform, false);
            if (boardingCardPromptPanel.activeSelf != active)
            {
                boardingCardPromptPanel.SetActive(active);
                CocoonDebugLog.Info("InstructionUI", "Boarding card prompt panel " + (active ? "shown" : "hidden") + ".", this);
            }
        }

        private void SetSitPromptPanelActive(bool active)
        {
            ResolveBaggageReferences();
            if (sitPromptPanel == null)
            {
                return;
            }

            if (ShouldSuppressHeadLockedFlowUi())
            {
                active = false;
            }

            if (!active)
            {
                if (sitPromptPanel.activeSelf)
                {
                    sitPromptPanel.SetActive(false);
                    CocoonDebugLog.Info("InstructionUI", "Sit prompt panel hidden.", this);
                }

                return;
            }

            PrepareHeadLockedPromptPanel(
                sitPromptPanel.transform,
                "SEAT READY",
                "Press A to sit down.");
            if (sitPromptPanel.activeSelf != active)
            {
                sitPromptPanel.SetActive(active);
                CocoonDebugLog.Info("InstructionUI", "Sit prompt panel " + (active ? "shown" : "hidden") + ".", this);
            }
        }

        private void SetPromptButtonVisualsActive(Transform panelTransform, bool active)
        {
            if (panelTransform == null)
            {
                return;
            }

            SetChildGameObjectActive(panelTransform, "Prompt Primary Button", active);
            SetChildGameObjectActive(panelTransform, "Prompt Secondary Button", active);
        }

        private void SetChildGameObjectActive(Transform root, string childName, bool active)
        {
            Transform child = root != null ? root.Find(childName) : null;
            if (child == null)
            {
                child = FindNamedDescendant(root, childName);
            }

            if (child != null && child.gameObject.activeSelf != active)
            {
                child.gameObject.SetActive(active);
            }
        }

        private void PrepareHeadLockedPromptPanel(Transform panelTransform, string titleText, string statusTextValue)
        {
            if (panelTransform == null)
            {
                return;
            }

            Canvas canvas = panelTransform.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
            }

            CocoonHeadLockedUI headLockedUi = panelTransform.GetComponent<CocoonHeadLockedUI>();
            if (headLockedUi != null)
            {
                headLockedUi.SetLocalScale(GetHeadLockedPanelLocalScale());
                headLockedUi.TryApply(ResolveRiderHeadTransform());
            }

            Text title = FindPanelText(panelTransform, "Prompt Title");
            Text status = FindPanelText(panelTransform, "Prompt Status");
            SetUiText(title, titleText);
            SetUiText(status, statusTextValue);
            if (!ShouldApplyHeadLockedUiRuntimeLayout())
            {
                return;
            }

            PrepareHeadLockedRuntimePanelSurface(panelTransform);
            ConfigureRuntimeHeadLockedText(title, HeadLockedPos(0f, 0.29f), HeadLockedSize(0.9f, 0.3f), HeadLockedFont(96f), Color.white, TextAnchor.MiddleCenter, titleText, HeadLockedFont(54f));
            ConfigureRuntimeHeadLockedText(status, HeadLockedPos(0f, 0.07f), HeadLockedSize(0.9f, 0.24f), HeadLockedFont(62f), new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, statusTextValue, HeadLockedFont(36f));
            ConfigureRuntimePromptButtonText(panelTransform, "Prompt Primary Button Text", HeadLockedFont(58f));
            ConfigureRuntimePromptButtonText(panelTransform, "Prompt Secondary Button Text", HeadLockedFont(58f));
        }

        private void SetInstructionPanelActive(bool active)
        {
            if (ShouldSuppressHeadLockedFlowUi())
            {
                active = false;
                SetBaggageQuestionPanelActive(false);
            }

            if (active && ShouldShowBaggageQuestion())
            {
                active = false;
                SetBaggageQuestionPanelActive(true);
            }
            else if (!ShouldShowBaggageQuestion())
            {
                SetBaggageQuestionPanelActive(false);
            }

            if (active && ShouldSuppressRiderInstructionPanel())
            {
                active = false;
            }

            if (active)
            {
                AlignInstructionPanelToRiderView();
            }

            if (instructionPanel != null && instructionPanel.activeSelf != active)
            {
                instructionPanel.SetActive(active);
                CocoonDebugLog.Info("InstructionUI", "Rider instruction panel " + (active ? "shown" : "hidden") + ".", this);
            }
        }

        private bool ShouldShowBaggageQuestion()
        {
            return !ShouldSuppressHeadLockedFlowUi() &&
                   state == CocoonTaxiState.Availability &&
                   !baggageQuestionAnswered &&
                   !riderOnboard;
        }

        private bool ShouldSuppressRiderInstructionPanel()
        {
            return ShouldSuppressHeadLockedFlowUi() ||
                   riderOnboard ||
                   state == CocoonTaxiState.PaymentConfirmed ||
                   state == CocoonTaxiState.DoorOpening ||
                   state == CocoonTaxiState.DoorOpen ||
                   state == CocoonTaxiState.DoorClosing ||
                   state == CocoonTaxiState.Departing;
        }

        private void SetBaggageQuestionPanelActive(bool active)
        {
            ResolveBaggageReferences();
            if (baggageQuestionPanel == null)
            {
                return;
            }

            if (ShouldSuppressHeadLockedFlowUi())
            {
                active = false;
            }

            if (!active)
            {
                if (baggageQuestionPanel.activeSelf)
                {
                    baggageQuestionPanel.SetActive(false);
                    CocoonDebugLog.Info("InstructionUI", "Baggage question panel hidden.", this);
                }

                return;
            }

            PrepareBaggageQuestionPanel(baggageQuestionPanel.transform);
            if (baggageQuestionPanel.activeSelf != active)
            {
                baggageQuestionPanel.SetActive(active);
                CocoonDebugLog.Info("InstructionUI", "Baggage question panel " + (active ? "shown" : "hidden") + ".", this);
            }
        }

        private void PrepareBaggageQuestionPanel(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return;
            }

            Canvas canvas = panelTransform.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
            }

            CocoonHeadLockedUI headLockedUi = panelTransform.GetComponent<CocoonHeadLockedUI>();
            if (headLockedUi != null)
            {
                headLockedUi.SetLocalScale(GetHeadLockedPanelLocalScale());
                headLockedUi.TryApply(ResolveRiderHeadTransform());
            }

            Text title = FindPanelText(panelTransform, "Baggage Question Title");
            Text status = FindPanelText(panelTransform, "Baggage Question Status");
            SetUiText(title, "ARE YOU CARRYING LUGGAGE?");
            SetUiText(status, "Right controller: press A for YES, B for NO.");
            if (ShouldApplyHeadLockedUiRuntimeLayout())
            {
                PrepareHeadLockedRuntimePanelSurface(panelTransform);
                ConfigureRuntimeHeadLockedText(title, HeadLockedPos(0f, 0.29f), HeadLockedSize(0.9f, 0.3f), HeadLockedFont(90f), Color.white, TextAnchor.MiddleCenter, "ARE YOU CARRYING LUGGAGE?", HeadLockedFont(52f));
                ConfigureRuntimeHeadLockedText(status, HeadLockedPos(0f, 0.07f), HeadLockedSize(0.9f, 0.24f), HeadLockedFont(58f), new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, "Right controller: press A for YES, B for NO.", HeadLockedFont(34f));
                ConfigureRuntimePromptButtonText(panelTransform, "Baggage Yes Button Text", HeadLockedFont(58f));
                ConfigureRuntimePromptButtonText(panelTransform, "Baggage No Button Text", HeadLockedFont(58f));
            }
            SetPanelChildActive(panelTransform, "Baggage Yes Button", true);
            SetPanelChildActive(panelTransform, "Baggage No Button", true);
            WireBaggageQuestionButton(panelTransform, "Baggage Yes Button", CocoonButtonAction.BaggageYes);
            WireBaggageQuestionButton(panelTransform, "Baggage No Button", CocoonButtonAction.BaggageNo);
        }

        private void SetBaggageInstructionPanelActive(bool active)
        {
            ResolveBaggageReferences();
            if (baggageQuestionPanel == null)
            {
                return;
            }

            if (ShouldSuppressHeadLockedFlowUi())
            {
                active = false;
            }

            if (!active)
            {
                if (baggageQuestionPanel.activeSelf)
                {
                    baggageQuestionPanel.SetActive(false);
                    CocoonDebugLog.Info("InstructionUI", "Luggage instruction panel hidden.", this);
                }

                return;
            }

            PrepareBaggageInstructionPanel(baggageQuestionPanel.transform);
            if (baggageQuestionPanel.activeSelf != active)
            {
                baggageQuestionPanel.SetActive(active);
                CocoonDebugLog.Info("InstructionUI", "Luggage instruction panel " + (active ? "shown" : "hidden") + ".", this);
            }
        }

        private void PrepareBaggageInstructionPanel(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return;
            }

            Canvas canvas = panelTransform.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
            }

            CocoonHeadLockedUI headLockedUi = panelTransform.GetComponent<CocoonHeadLockedUI>();
            if (headLockedUi != null)
            {
                headLockedUi.SetLocalScale(GetHeadLockedPanelLocalScale());
                headLockedUi.TryApply(ResolveRiderHeadTransform());
            }

            Text title = FindPanelText(panelTransform, "Baggage Question Title");
            Text status = FindPanelText(panelTransform, "Baggage Question Status");
            string titleValue = "LUGGAGE READY";
            string statusValue = "Your luggage is on your left. Hold the left controller grip to drag it with you. Cocoon starts in " + Mathf.CeilToInt(Mathf.Max(0f, baggageInstructionTimer)) + "s.";
            SetUiText(title, titleValue);
            SetUiText(status, statusValue);
            if (ShouldApplyHeadLockedUiRuntimeLayout())
            {
                PrepareHeadLockedRuntimePanelSurface(panelTransform);
                ConfigureRuntimeHeadLockedText(title, HeadLockedPos(0f, 0.28f), HeadLockedSize(0.9f, 0.26f), HeadLockedFont(84f), Color.white, TextAnchor.MiddleCenter, titleValue, HeadLockedFont(48f));
                ConfigureRuntimeHeadLockedText(status, HeadLockedPos(0f, -0.02f), HeadLockedSize(0.9f, 0.48f), HeadLockedFont(52f), new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, statusValue, HeadLockedFont(30f));
            }
            SetPanelChildActive(panelTransform, "Baggage Yes Button", false);
            SetPanelChildActive(panelTransform, "Baggage No Button", false);
        }

        private void ConfigureRuntimePromptButtonText(Transform panelTransform, string textName, int fontSize)
        {
            Text text = FindPanelText(panelTransform, textName);
            if (text == null && textName.EndsWith(" Text", StringComparison.Ordinal))
            {
                string buttonName = textName.Substring(0, textName.Length - " Text".Length);
                Transform buttonTransform = FindNamedDescendant(panelTransform, buttonName);
                if (buttonTransform != null)
                {
                    text = buttonTransform.GetComponentInChildren<Text>(true);
                }
            }

            if (text == null)
            {
                return;
            }

            Vector2 panelSize = GetHeadLockedPanelSize();
            RectTransform buttonRect = text.transform.parent as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.sizeDelta = new Vector2(panelSize.x * 0.32f, panelSize.y * 0.24f);
                if (textName.Contains("Primary") || textName.Contains("Yes"))
                {
                    buttonRect.anchoredPosition = new Vector2(-panelSize.x * 0.21f, -panelSize.y * 0.31f);
                }
                else if (textName.Contains("Secondary") || textName.Contains("No"))
                {
                    buttonRect.anchoredPosition = new Vector2(panelSize.x * 0.21f, -panelSize.y * 0.31f);
                }
            }

            Vector2 textSize = buttonRect != null ? buttonRect.sizeDelta : text.rectTransform.sizeDelta;
            ConfigureRuntimePanelText(text, Vector2.zero, textSize, fontSize, Color.white, TextAnchor.MiddleCenter, text.text);
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = fontSize;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.SetAllDirty();
        }

        private void RefreshActiveHeadLockedPanels()
        {
            if (ShouldSuppressHeadLockedFlowUi())
            {
                SetInstructionPanelActive(false);
                SetBaggageQuestionPanelActive(false);
                SetBoardingCardPromptPanelActive(false);
                SetSitPromptPanelActive(false);
                return;
            }

            if (!ShouldApplyHeadLockedUiRuntimeLayout())
            {
                return;
            }

            if (instructionPanel != null && instructionPanel.activeInHierarchy)
            {
                PrepareHeadLockedInstructionPanelRuntime(instructionPanel.transform);
            }

            if (baggageQuestionPanel != null && baggageQuestionPanel.activeInHierarchy)
            {
                if (baggageInstructionActive)
                {
                    PrepareBaggageInstructionPanel(baggageQuestionPanel.transform);
                }
                else
                {
                    PrepareBaggageQuestionPanel(baggageQuestionPanel.transform);
                }
            }

            if (boardingCardPromptPanel != null && boardingCardPromptPanel.activeInHierarchy)
            {
                PrepareHeadLockedPromptPanel(
                    boardingCardPromptPanel.transform,
                    "PREPAY TO OPEN",
                    "Please tap your card to prepay and open the door.");
                SetPromptButtonVisualsActive(boardingCardPromptPanel.transform, false);
            }

            if (sitPromptPanel != null && sitPromptPanel.activeInHierarchy)
            {
                PrepareHeadLockedPromptPanel(
                    sitPromptPanel.transform,
                    "SEAT READY",
                    "Press A to sit down.");
            }
        }

        private void WireBaggageQuestionButton(Transform panelTransform, string buttonName, CocoonButtonAction action)
        {
            Transform buttonTransform = panelTransform != null ? panelTransform.Find(buttonName) : null;
            if (buttonTransform == null)
            {
                return;
            }

            CocoonWorldButton button = buttonTransform.GetComponent<CocoonWorldButton>();
            Graphic tintGraphic = buttonTransform.GetComponent<Graphic>();
            if (button != null)
            {
                button.Configure(this, action, string.Empty, tintGraphic);
            }
        }

        private void SetPassengerLuggageActive(bool active)
        {
            ResolveBaggageReferences();
            if (passengerLuggage == null)
            {
                return;
            }

            CapturePassengerLuggageOriginalParent();
            if (!active)
            {
                RestorePassengerLuggageFollowMode();
                passengerLuggagePlacedAtRest = false;
                ResetPassengerLuggageStorageSequence();
            }

            if (passengerLuggage.activeSelf != active)
            {
                passengerLuggage.SetActive(active);
                CocoonDebugLog.Info("Luggage", "Passenger luggage " + (active ? "shown" : "hidden") + ".", this);
            }

            if (active)
            {
                int rendererCount = SetPassengerLuggageRenderersEnabled(true);
                CocoonLuggageFollower follower = passengerLuggage.GetComponent<CocoonLuggageFollower>();
                if (follower != null)
                {
                    if (!passengerLuggagePlacedAtRest)
                    {
                        follower.ConfigureFollowTarget(ResolveLeftHandTransform(), ResolveRiderHeadTransform(), false);
                        follower.ConfigureElevatedSupport(ResolveFinalTaxiInteriorStructure(), LuggageCabinSupportTag, LuggageSolidSupportPadding);
                        follower.enabled = true;
                        CalibratePassengerLuggageToBaseline();
                        follower.PlaceAtRestBesideTarget();
                        passengerLuggagePlacedAtRest = true;
                    }
                    else if (!passengerLuggageStowedInTaxi && !follower.enabled)
                    {
                        follower.ConfigureElevatedSupport(ResolveFinalTaxiInteriorStructure(), LuggageCabinSupportTag, LuggageSolidSupportPadding);
                        follower.enabled = true;
                    }
                    else if (!passengerLuggageStowedInTaxi)
                    {
                        follower.ConfigureElevatedSupport(ResolveFinalTaxiInteriorStructure(), LuggageCabinSupportTag, LuggageSolidSupportPadding);
                    }
                }
                else
                {
                    if (!passengerLuggagePlacedAtRest)
                    {
                        CalibratePassengerLuggageToBaseline();
                        passengerLuggagePlacedAtRest = true;
                    }
                }

                if (!hasLoggedPassengerLuggageVisible)
                {
                    hasLoggedPassengerLuggageVisible = true;
                    string boundsText = TryGetRendererBounds(passengerLuggage.transform, out Bounds bounds)
                        ? ", boundsSize=" + FormatPosition(bounds.size)
                        : ", bounds=missing";
                    CocoonDebugLog.Info(
                        "Luggage",
                        "Passenger luggage visible near left hand: activeSelf=" + passengerLuggage.activeSelf +
                        ", activeInHierarchy=" + passengerLuggage.activeInHierarchy +
                        ", renderers=" + rendererCount +
                        ", pos=" + FormatPosition(passengerLuggage.transform.position) +
                        ", rootScale=" + FormatPosition(passengerLuggage.transform.lossyScale) +
                        boundsText + ".",
                        this);
                }
            }
        }

        private void CalibratePassengerLuggageToBaseline()
        {
            if (passengerLuggage == null)
            {
                return;
            }

            Transform model = FindExactNamedDescendant(passengerLuggage.transform, PassengerLuggageModelName);
            Transform baseline = FindSceneTransformExact(PassengerLuggageBaselineName);
            if (model == null || baseline == null)
            {
                return;
            }

            if (!TryGetRendererBounds(baseline, out Bounds baselineBounds) ||
                !TryGetRendererBounds(model, out Bounds modelBounds))
            {
                return;
            }

            float baselineSize = Mathf.Max(baselineBounds.size.x, baselineBounds.size.y, baselineBounds.size.z);
            float modelSize = Mathf.Max(modelBounds.size.x, modelBounds.size.y, modelBounds.size.z);
            if (baselineSize <= 0.0001f || modelSize <= 0.0001f)
            {
                return;
            }

            float scaleFactor = Mathf.Clamp(baselineSize / modelSize, 0.05f, 20f);
            if (Mathf.Abs(scaleFactor - 1f) > 0.01f)
            {
                model.localScale *= scaleFactor;
                if (TryGetRendererBounds(model, out Bounds calibratedBounds))
                {
                    CocoonDebugLog.Info(
                        "Luggage",
                        "Passenger luggage calibrated to bag0: targetBounds=" + FormatPosition(baselineBounds.size) +
                        ", resultBounds=" + FormatPosition(calibratedBounds.size) +
                        ", scaleFactor=" + scaleFactor.ToString("0.###") + ".",
                        this);
                }
            }
        }

        private int SetPassengerLuggageRenderersEnabled(bool enabled)
        {
            if (passengerLuggage == null)
            {
                return 0;
            }

            Transform[] transforms = passengerLuggage.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i] != passengerLuggage.transform && transforms[i].gameObject.activeSelf != enabled)
                {
                    transforms[i].gameObject.SetActive(enabled);
                }
            }

            Renderer[] renderers = passengerLuggage.GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = enabled;
                    count++;
                }
            }

            return count;
        }

        private void CapturePassengerLuggageOriginalParent()
        {
            if (hasPassengerLuggageOriginalParent || passengerLuggage == null)
            {
                return;
            }

            passengerLuggageOriginalParent = passengerLuggage.transform.parent;
            hasPassengerLuggageOriginalParent = true;
        }

        private void RestorePassengerLuggageFollowMode()
        {
            if (passengerLuggage == null)
            {
                return;
            }

            if (hasPassengerLuggageOriginalParent && passengerLuggage.transform.parent != passengerLuggageOriginalParent)
            {
                passengerLuggage.transform.SetParent(passengerLuggageOriginalParent, true);
            }

            CocoonLuggageFollower follower = passengerLuggage.GetComponent<CocoonLuggageFollower>();
            if (follower != null)
            {
                follower.enabled = true;
            }

            passengerLuggageStowedInTaxi = false;
            hasLoggedLuggageSupportContactStorageStart = false;
            ResetPassengerLuggageStorageSequence();
        }

        private void StowPassengerLuggageInTaxi()
        {
            if (!riderHasLuggage || passengerLuggage == null || taxiRoot == null || passengerLuggageStowedInTaxi)
            {
                return;
            }

            CapturePassengerLuggageOriginalParent();
            CocoonLuggageFollower follower = passengerLuggage.GetComponent<CocoonLuggageFollower>();
            if (follower != null)
            {
                follower.enabled = false;
            }

            if (!ResolveLuggageStorageBaseReferences())
            {
                passengerLuggage.transform.SetParent(taxiRoot, true);
                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
                passengerLuggageStowedInTaxi = true;
                CocoonDebugLog.Warn("Luggage", "Passenger luggage storage A1/preventer missing; visible street luggage will ride with taxi instead.", this);
                return;
            }

            SetPassengerLuggageRenderersEnabled(false);
            StartPassengerLuggageStorageSequence();
            passengerLuggageStowedInTaxi = true;
            PlayFloorEntryLights();
            CocoonDebugLog.Info("Luggage", "Passenger luggage stowed in taxi; street luggage hidden and bagA1 storage proxy started.", this);
        }

        private void TryStartPassengerLuggageStorageFromSupportContact()
        {
            if (!riderHasLuggage ||
                passengerLuggage == null ||
                passengerLuggageStowedInTaxi ||
                passengerLuggageStoragePhase != CocoonLuggageStoragePhase.Inactive ||
                !passengerLuggage.activeInHierarchy)
            {
                return;
            }

            if (!TryGetRendererBounds(passengerLuggage.transform, out Bounds luggageBounds) ||
                !TryGetLuggageCabinSupportBounds(out Bounds supportBounds))
            {
                return;
            }

            float padding = LuggageSolidSupportPadding;
            bool overlapsX = luggageBounds.max.x >= supportBounds.min.x - padding &&
                             luggageBounds.min.x <= supportBounds.max.x + padding;
            bool overlapsZ = luggageBounds.max.z >= supportBounds.min.z - padding &&
                             luggageBounds.min.z <= supportBounds.max.z + padding;
            bool touchesSupportHeight = luggageBounds.min.y <= supportBounds.max.y + LuggageSupportContactHeightTolerance &&
                                        luggageBounds.max.y >= supportBounds.max.y - LuggageSupportContactHeightTolerance;
            if (!overlapsX || !overlapsZ || !touchesSupportHeight)
            {
                return;
            }

            if (!hasLoggedLuggageSupportContactStorageStart)
            {
                hasLoggedLuggageSupportContactStorageStart = true;
                CocoonDebugLog.Info(
                    "Luggage",
                    "Luggage support contact detected; starting storage. luggageBottomY=" +
                    luggageBounds.min.y.ToString("0.###") +
                    ", supportTopY=" + supportBounds.max.y.ToString("0.###") + ".",
                    this);
            }

            StowPassengerLuggageInTaxi();
        }

        private void StartPassengerLuggageStorageSequence()
        {
            if (passengerLuggage == null || taxiRoot == null)
            {
                return;
            }

            if (!ResolveLuggageStorageBaseReferences())
            {
                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
                return;
            }

            RestoreLuggageStorageBagA1AuthoredPose();
            SetTransformRenderersEnabled(luggageStorageBagA1, true);
            HideLuggageStorageWaypointMarkers();
            CaptureLuggageStorageBagA1ArcStartPose();
            bool hasPathMarkers = ResolveLuggageStoragePathReferences();
            if (hasPathMarkers)
            {
                BuildLuggageStorageWaypointTargets();
                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.BecomeA1;
            }
            else
            {
                luggageStorageWaypointTargets.Clear();
                luggageStorageWaypointDurations.Clear();
                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
                CocoonDebugLog.Warn("Luggage", "Storage core path markers missing; bagA1 remains visible at A1 and storage is marked complete so taxi cannot deadlock.", this);
            }

            passengerLuggageStorageTimer = 0f;
            luggageStorageWaypointIndex = 0;
            hasLoggedLuggageStorageA1 = false;
            hasLoggedLuggageStorageA2 = false;
            hasLoggedLuggageStorageA3 = false;
            hasLoggedLuggagePreventerRaised = false;
            CocoonDebugLog.Info("Luggage", "Storage A1 proxy shown.", this);
            CocoonDebugLog.Info("Luggage", hasPathMarkers ? "Passenger luggage storage sequence: waypoint destination route armed." : "Passenger luggage storage sequence skipped because core path markers are missing.", this);
        }

        private void UpdatePassengerLuggageStorageSequence()
        {
            if (passengerLuggageStoragePhase == CocoonLuggageStoragePhase.Inactive ||
                passengerLuggageStoragePhase == CocoonLuggageStoragePhase.Complete ||
                passengerLuggage == null)
            {
                return;
            }

            if (!ResolveLuggageStorageBaseReferences())
            {
                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
                return;
            }

            switch (passengerLuggageStoragePhase)
            {
                case CocoonLuggageStoragePhase.BecomeA1:
                    RunPassengerLuggageBecomeA1();
                    break;
                case CocoonLuggageStoragePhase.WaitingForSeat:
                    RunPassengerLuggageWaitingForSeat();
                    break;
                case CocoonLuggageStoragePhase.WaitingForMarkers:
                    RunPassengerLuggageWaitingForMarkers();
                    break;
                case CocoonLuggageStoragePhase.MoveWaypoint:
                    RunPassengerLuggageWaypointSegment();
                    break;
                case CocoonLuggageStoragePhase.RaisePreventer:
                    RunPassengerLuggagePreventerRaise();
                    break;
            }
        }

        private void RunPassengerLuggageBecomeA1()
        {
            bool completed;
            AdvanceLuggageStorageProgress(luggageCabinLiftDuration, out completed);
            ApplyLuggageStorageBagA1WorldPose(
                luggageStorageBagA1ArcStartPosition,
                luggageStorageBagA1ArcStartRotation,
                luggageStorageBagA1ArcStartWorldScale);

            if (!completed)
            {
                return;
            }

            if (!hasLoggedLuggageStorageA1)
            {
                hasLoggedLuggageStorageA1 = true;
                CocoonDebugLog.Info("Luggage", "Passenger luggage became visible bagA1 proxy; waiting for seat storage before destination waypoints.", this);
            }
            ContinuePassengerLuggageAfterSeatStorage();
        }

        private void RunPassengerLuggageWaitingForSeat()
        {
            ApplyLuggageStorageBagA1WorldPose(
                luggageStorageBagA1ArcStartPosition,
                luggageStorageBagA1ArcStartRotation,
                luggageStorageBagA1ArcStartWorldScale);

            ContinuePassengerLuggageAfterSeatStorage();
        }

        private void ContinuePassengerLuggageAfterSeatStorage()
        {
            if (seatStoragePhase == CocoonSeatStoragePhase.Inactive)
            {
                StartPassengerSeatStorageSequence();
            }

            if (seatStoragePhase != CocoonSeatStoragePhase.Complete)
            {
                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.WaitingForSeat;
                passengerLuggageStorageTimer = 0f;
                if (!hasLoggedLuggageStorageWaitingForSeat)
                {
                    hasLoggedLuggageStorageWaitingForSeat = true;
                    CocoonDebugLog.Info("Luggage", "bagA1 proxy is waiting for Seat V2 to reach its out target before luggage route starts.", this);
                }

                return;
            }

            hasLoggedLuggageStorageWaitingForSeat = false;
            if (luggageStorageWaypointTargets.Count == 0)
            {
                if (!ResolveLuggageStoragePathReferences(false))
                {
                    passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
                    CocoonDebugLog.Warn("Luggage", "Storage path markers missing after seat completed; bagA1 remains visible and luggage storage is marked complete to avoid deadlock.", this);
                    return;
                }

                BuildLuggageStorageWaypointTargets();
            }

            BeginNextPassengerLuggageWaypointSegment();
        }

        private void RunPassengerLuggageWaitingForMarkers()
        {
            ApplyLuggageStorageBagA1WorldPose(
                luggageStorageBagA1ArcStartPosition,
                luggageStorageBagA1ArcStartRotation,
                luggageStorageBagA1ArcStartWorldScale);

            if (!ResolveLuggageStoragePathReferences())
            {
                if (!hasLoggedLuggageStorageWaitingForMarkers)
                {
                    hasLoggedLuggageStorageWaitingForMarkers = true;
                    CocoonDebugLog.Warn("Luggage", "Storage waiting for missing waypoint markers.", this);
                }

                return;
            }

            BuildLuggageStorageWaypointTargets();
            passengerLuggageStoragePhase = CocoonLuggageStoragePhase.BecomeA1;
            passengerLuggageStorageTimer = 0f;
            hasLoggedLuggageStorageA1 = false;
            hasLoggedLuggageStorageWaitingForMarkers = false;
            CocoonDebugLog.Info("Luggage", "Storage waypoint markers are now available; continuing from A1.", this);
        }

        private void BuildLuggageStorageWaypointTargets()
        {
            luggageStorageWaypointTargets.Clear();
            luggageStorageWaypointDurations.Clear();

            AddLuggageStorageWaypoint(luggageStorageA11, luggageStorageA1ToA11Duration);
            AddLuggageStorageWaypoint(luggageStorageA12, luggageStorageA11ToA12Duration);
            AddLuggageStorageWaypoint(luggageStorageA13, luggageStorageA12ToA13Duration);
            AddLuggageStorageWaypoint(luggageStorageBagA2, luggageStorageA13ToA2Duration);
            AddLuggageStorageWaypoint(luggageStorageA21, luggageStorageA2ToA21Duration);
            AddLuggageStorageWaypoint(luggageStorageBagA3, luggageStorageA21ToA3Duration);
            luggageStorageWaypointIndex = 0;

            LogLuggageStorageRoute();
        }

        private float GetLuggageStorageWaypointDuration(int waypointIndex)
        {
            if (waypointIndex >= 0 && waypointIndex < luggageStorageWaypointDurations.Count)
            {
                return luggageStorageWaypointDurations[waypointIndex];
            }

            return 1f;
        }

        private void AddLuggageStorageWaypoint(Transform target, float duration)
        {
            if (target == null)
            {
                return;
            }

            luggageStorageWaypointTargets.Add(target);
            luggageStorageWaypointDurations.Add(Mathf.Max(0.01f, duration));
        }

        private void LogLuggageStorageRoute()
        {
            if (hasLoggedLuggageStorageBinding)
            {
                return;
            }

            hasLoggedLuggageStorageBinding = true;

            string route = "bagA1";
            for (int i = 0; i < luggageStorageWaypointTargets.Count; i++)
            {
                route += " -> " + luggageStorageWaypointTargets[i].name;
            }

            string skipped = "";
            AppendMissingLuggageWaypoint(ref skipped, luggageStorageA11, "A1.1");
            AppendMissingLuggageWaypoint(ref skipped, luggageStorageA12, "A1.2");
            AppendMissingLuggageWaypoint(ref skipped, luggageStorageA13, "A1.3");
            AppendMissingLuggageWaypoint(ref skipped, luggageStorageA21, "A2.1");

            CocoonDebugLog.Info(
                "Luggage",
                "Storage route built: " + route + (string.IsNullOrEmpty(skipped) ? "." : "; skipped optional=" + skipped + "."),
                this);
        }

        private static void AppendMissingLuggageWaypoint(ref string skipped, Transform marker, string label)
        {
            if (marker != null)
            {
                return;
            }

            skipped += string.IsNullOrEmpty(skipped) ? label : "," + label;
        }

        private void BeginNextPassengerLuggageWaypointSegment()
        {
            if (luggageStorageWaypointIndex >= luggageStorageWaypointTargets.Count)
            {
                luggagePreventerRaiseStartLocalPosition = luggagePreventerA1 != null ? luggagePreventerA1.localPosition : Vector3.zero;
                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.RaisePreventer;
                passengerLuggageStorageTimer = 0f;
                PlayLuggagePreventerLights();
                if (!hasLoggedLuggageStorageA3)
                {
                    hasLoggedLuggageStorageA3 = true;
                    CocoonDebugLog.Info("Luggage", "Passenger luggage reached final bagA3 waypoint; raising luggage preventer.", this);
                }
                return;
            }

            luggageStorageSegmentStartPosition = luggageStorageBagA1.position;
            luggageStorageSegmentStartRotation = luggageStorageBagA1.rotation;
            luggageStorageSegmentStartWorldScale = luggageStorageBagA1.lossyScale;
            passengerLuggageStoragePhase = CocoonLuggageStoragePhase.MoveWaypoint;
            passengerLuggageStorageTimer = 0f;
            Transform target = luggageStorageWaypointTargets[luggageStorageWaypointIndex];
            CocoonDebugLog.Info(
                "Luggage",
                "Passenger luggage waypoint segment " + (luggageStorageWaypointIndex + 1) + "/" + luggageStorageWaypointTargets.Count +
                " -> " + target.name + " duration=" + GetLuggageStorageWaypointDuration(luggageStorageWaypointIndex).ToString("0.##") + "s.",
                this);
        }

        private void RunPassengerLuggageWaypointSegment()
        {
            if (luggageStorageWaypointIndex >= luggageStorageWaypointTargets.Count)
            {
                BeginNextPassengerLuggageWaypointSegment();
                return;
            }

            Transform target = luggageStorageWaypointTargets[luggageStorageWaypointIndex];
            if (target == null)
            {
                if (ResolveLuggageStoragePathReferences(false))
                {
                    BuildLuggageStorageWaypointTargets();
                    passengerLuggageStoragePhase = CocoonLuggageStoragePhase.BecomeA1;
                    passengerLuggageStorageTimer = 0f;
                    hasLoggedLuggageStorageA1 = false;
                    CocoonDebugLog.Warn("Luggage", "Passenger luggage waypoint target was lost; storage route was rebuilt from current markers.", this);
                    return;
                }

                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
                passengerLuggageStorageTimer = 0f;
                luggageStorageWaypointTargets.Clear();
                luggageStorageWaypointDurations.Clear();
                luggageStorageWaypointIndex = 0;
                CocoonDebugLog.Warn("Luggage", "Passenger luggage waypoint target was lost and core path markers are unavailable; storage completed at current proxy pose to avoid blocking taxi.", this);
                return;
            }

            bool completed;
            float t = AdvanceLuggageStorageProgress(GetLuggageStorageWaypointDuration(luggageStorageWaypointIndex), out completed);
            ApplyLuggageStorageBagA1WorldPose(
                Vector3.Lerp(luggageStorageSegmentStartPosition, target.position, t),
                Quaternion.Slerp(luggageStorageSegmentStartRotation, target.rotation, t),
                Vector3.Lerp(luggageStorageSegmentStartWorldScale, GetWorldScale(target), t));

            if (!completed)
            {
                return;
            }

            ApplyLuggageStorageBagA1WorldPose(target.position, target.rotation, GetWorldScale(target));
            if (target == luggageStorageBagA2 && !hasLoggedLuggageStorageA2)
            {
                hasLoggedLuggageStorageA2 = true;
                CocoonDebugLog.Info("Luggage", "Passenger luggage reached bagA2 correction waypoint.", this);
            }

            luggageStorageWaypointIndex++;
            BeginNextPassengerLuggageWaypointSegment();
        }

        private void RunPassengerLuggagePreventerRaise()
        {
            if (luggagePreventerA1 == null)
            {
                passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
                return;
            }

            bool completed;
            float t = AdvanceLuggageStorageProgress(luggagePreventerRaiseDuration, out completed);
            luggagePreventerA1.localPosition = Vector3.Lerp(luggagePreventerRaiseStartLocalPosition, Vector3.zero, t);

            if (!completed)
            {
                return;
            }

            luggagePreventerA1.localPosition = Vector3.zero;
            passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
            if (!hasLoggedLuggagePreventerRaised)
            {
                hasLoggedLuggagePreventerRaised = true;
                CocoonDebugLog.Info("Luggage", "Luggage preventer raised; storage sequence complete.", this);
            }
        }

        private void StartPassengerLuggageExitSequence()
        {
            luggageExitPoseTargets.Clear();
            luggageExitPoseIndex = 0;
            passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Complete;
            passengerLuggageStorageTimer = 0f;

            if (!ResolveLuggageStorageBaseReferences(false))
            {
                return;
            }

            ResolveLuggageStoragePathReferences(false);
            RestoreLuggagePreventerStowedPose();
            SetTransformRenderersEnabled(luggageStorageBagA1, true);

            AddLuggageExitPose(luggageStorageA21, luggageStorageA21ToA3Duration, "A2.1");
            AddLuggageExitPose(luggageStorageBagA2, luggageStorageA2ToA21Duration, "bagA2");
            AddLuggageExitPose(luggageStorageA13, luggageStorageA13ToA2Duration, "A1.3");
            AddLuggageExitPose(luggageStorageA12, luggageStorageA12ToA13Duration, "A1.2");
            AddLuggageExitPose(luggageStorageA11, luggageStorageA11ToA12Duration, "A1.1");
            AddLuggageExitAuthoredA1Pose();

            if (luggageExitPoseTargets.Count > 0)
            {
                CaptureNextLuggageExitSegmentStart();
                CocoonDebugLog.Info("Luggage", "Destination luggage exit sequence armed with " + luggageExitPoseTargets.Count + " reverse waypoint(s).", this);
            }
        }

        private bool RunPassengerLuggageExitSequence()
        {
            if (luggageStorageBagA1 == null || luggageExitPoseTargets.Count == 0)
            {
                return true;
            }

            if (luggageExitPoseIndex >= luggageExitPoseTargets.Count)
            {
                return true;
            }

            LuggageExitPose target = luggageExitPoseTargets[luggageExitPoseIndex];
            bool completed;
            float t = AdvanceLuggageStorageProgress(target.Duration, out completed);
            GetLuggageExitWorldPose(
                Vector3.Lerp(luggageExitSegmentStartPosition, target.LocalPosition, t),
                Quaternion.Slerp(luggageExitSegmentStartRotation, target.LocalRotation, t),
                out Vector3 position,
                out Quaternion rotation);
            ApplyLuggageStorageBagA1WorldPose(
                position,
                rotation,
                Vector3.Lerp(luggageExitSegmentStartWorldScale, target.WorldScale, t));

            if (!completed)
            {
                return false;
            }

            GetLuggageExitWorldPose(target.LocalPosition, target.LocalRotation, out Vector3 targetPosition, out Quaternion targetRotation);
            ApplyLuggageStorageBagA1WorldPose(targetPosition, targetRotation, target.WorldScale);
            CocoonDebugLog.Info("Luggage", "Destination luggage exit reached " + target.Label + ".", this);
            luggageExitPoseIndex++;
            if (luggageExitPoseIndex >= luggageExitPoseTargets.Count)
            {
                CompletePassengerLuggageExitForDoorPickup();
                CocoonDebugLog.Info("Luggage", "Destination luggage exit sequence complete at bagA1 pickup pose.", this);
                return true;
            }

            CaptureNextLuggageExitSegmentStart();
            return false;
        }

        private void CompletePassengerLuggageExitForDoorPickup()
        {
            if (!riderHasLuggage || passengerLuggage == null || luggageStorageBagA1 == null)
            {
                return;
            }

            CapturePassengerLuggageOriginalParent();
            if (hasPassengerLuggageOriginalParent && passengerLuggage.transform.parent != passengerLuggageOriginalParent)
            {
                passengerLuggage.transform.SetParent(passengerLuggageOriginalParent, true);
            }

            CocoonLuggageFollower follower = passengerLuggage.GetComponent<CocoonLuggageFollower>();
            if (follower != null)
            {
                follower.enabled = false;
            }

            passengerLuggage.SetActive(true);
            SetPassengerLuggageRenderersEnabled(true);

            passengerLuggage.transform.SetPositionAndRotation(luggageStorageBagA1.position, luggageStorageBagA1.rotation);
            AlignPassengerLuggageBoundsToTransform(luggageStorageBagA1);

            if (follower != null)
            {
                follower.ConfigureFollowTarget(ResolveLeftHandTransform(), ResolveRiderHeadTransform(), false);
                follower.ConfigureElevatedSupport(ResolveFinalTaxiInteriorStructure(), LuggageCabinSupportTag, LuggageSolidSupportPadding);
                follower.enabled = true;
                AlignPassengerLuggageBoundsToTransform(luggageStorageBagA1);
            }

            passengerLuggageStowedInTaxi = false;
            passengerLuggagePlacedAtRest = true;
            hasLoggedLuggageSupportContactStorageStart = false;
            SetTransformRenderersEnabled(luggageStorageBagA1, false);
            CocoonDebugLog.Info("Luggage", "Passenger luggage handed off exactly at bagA1 pickup pose; visual proxy hidden only after bounds alignment.", this);
        }

        private void AlignPassengerLuggageBoundsToTransform(Transform targetVisual)
        {
            if (passengerLuggage == null || targetVisual == null)
            {
                return;
            }

            if (!TryGetRendererBounds(passengerLuggage.transform, out Bounds luggageBounds) ||
                !TryGetRendererBounds(targetVisual, out Bounds targetBounds))
            {
                passengerLuggage.transform.position = targetVisual.position;
                return;
            }

            Vector3 delta = targetBounds.center - luggageBounds.center;
            delta.y = targetBounds.min.y - luggageBounds.min.y;
            passengerLuggage.transform.position += delta;
        }

        private void CaptureNextLuggageExitSegmentStart()
        {
            if (luggageStorageBagA1 == null)
            {
                return;
            }

            luggageExitSegmentStartPosition = luggageStorageBagA1.position;
            luggageExitSegmentStartRotation = luggageStorageBagA1.rotation;
            luggageExitSegmentStartWorldScale = luggageStorageBagA1.lossyScale;
            Transform reference = ResolveLuggageExitReferenceRoot();
            if (reference != null)
            {
                luggageExitSegmentStartPosition = reference.InverseTransformPoint(luggageStorageBagA1.position);
                luggageExitSegmentStartRotation = Quaternion.Inverse(reference.rotation) * luggageStorageBagA1.rotation;
            }
            passengerLuggageStorageTimer = 0f;
        }

        private void AddLuggageExitPose(Transform target, float duration, string label)
        {
            if (target == null)
            {
                return;
            }

            AddLuggageExitWorldPose(label, target.position, target.rotation, GetWorldScale(target), duration);
        }

        private void AddLuggageExitAuthoredA1Pose()
        {
            if (luggageStorageBagA1 == null)
            {
                return;
            }

            GetLuggageStorageBagA1AuthoredWorldPose(out Vector3 position, out Quaternion rotation, out Vector3 worldScale);
            AddLuggageExitWorldPose("bagA1", position, rotation, worldScale, luggageStorageA1ToA11Duration);
        }

        private void AddLuggageExitWorldPose(string label, Vector3 position, Quaternion rotation, Vector3 worldScale, float duration)
        {
            Transform reference = ResolveLuggageExitReferenceRoot();
            Vector3 localPosition = position;
            Quaternion localRotation = rotation;
            if (reference != null)
            {
                localPosition = reference.InverseTransformPoint(position);
                localRotation = Quaternion.Inverse(reference.rotation) * rotation;
            }

            luggageExitPoseTargets.Add(new LuggageExitPose(
                label,
                localPosition,
                localRotation,
                worldScale,
                duration));
        }

        private Transform ResolveLuggageExitReferenceRoot()
        {
            if (taxiRoot != null)
            {
                return taxiRoot;
            }

            return transform;
        }

        private void GetLuggageExitWorldPose(Vector3 localPosition, Quaternion localRotation, out Vector3 position, out Quaternion rotation)
        {
            Transform reference = ResolveLuggageExitReferenceRoot();
            if (reference == null)
            {
                position = localPosition;
                rotation = localRotation;
                return;
            }

            position = reference.TransformPoint(localPosition);
            rotation = reference.rotation * localRotation;
        }

        private void GetLuggageStorageBagA1AuthoredWorldPose(out Vector3 position, out Quaternion rotation, out Vector3 worldScale)
        {
            if (luggageStorageBagA1 == null || !hasAuthoredLuggageStorageBagA1Pose)
            {
                position = luggageStorageBagA1 != null ? luggageStorageBagA1.position : Vector3.zero;
                rotation = luggageStorageBagA1 != null ? luggageStorageBagA1.rotation : Quaternion.identity;
                worldScale = luggageStorageBagA1 != null ? luggageStorageBagA1.lossyScale : Vector3.one;
                return;
            }

            Transform parent = luggageStorageBagA1.parent;
            if (parent == null)
            {
                position = luggageStorageBagA1LocalPosition;
                rotation = luggageStorageBagA1LocalRotation;
                worldScale = luggageStorageBagA1LocalScale;
                return;
            }

            position = parent.TransformPoint(luggageStorageBagA1LocalPosition);
            rotation = parent.rotation * luggageStorageBagA1LocalRotation;
            worldScale = Vector3.Scale(parent.lossyScale, luggageStorageBagA1LocalScale);
        }

        private float AdvanceLuggageStorageProgress(float duration, out bool completed)
        {
            duration = Mathf.Max(0.01f, duration);
            passengerLuggageStorageTimer = Mathf.Min(duration, passengerLuggageStorageTimer + Time.deltaTime);
            completed = passengerLuggageStorageTimer >= duration;
            return Mathf.SmoothStep(0f, 1f, passengerLuggageStorageTimer / duration);
        }

        private void CaptureLuggageStorageBagA1ArcStartPose()
        {
            luggageStorageBagA1ArcStartPosition = luggageStorageBagA1.position;
            luggageStorageBagA1ArcStartRotation = luggageStorageBagA1.rotation;
            luggageStorageBagA1ArcStartWorldScale = luggageStorageBagA1.lossyScale;
        }

        private void ApplyLuggageStorageBagA1WorldPose(Vector3 worldPosition, Quaternion worldRotation, Vector3 worldScale)
        {
            luggageStorageBagA1.position = worldPosition;
            luggageStorageBagA1.rotation = worldRotation;
            SetWorldScale(luggageStorageBagA1, worldScale);
        }

        private bool ResolveLuggageStorageBaseReferences(bool warnIfMissing = true)
        {
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            luggageStorageBagA1 = ResolveExactNamedReference(searchRoot, luggageStorageBagA1, LuggageStorageBagA1Name);
            luggagePreventerA1 = ResolveExactNamedReference(searchRoot, luggagePreventerA1, LuggagePreventerA1Name);
            CaptureLuggageStorageBagA1AuthoredPoseIfNeeded();
            CaptureLuggagePreventerStowedPoseIfNeeded();

            bool hasBaseMarkers = luggageStorageBagA1 != null && luggagePreventerA1 != null;
            if (!hasBaseMarkers && warnIfMissing && !hasLoggedLuggageStorageMissingMarkers)
            {
                hasLoggedLuggageStorageMissingMarkers = true;
                CocoonDebugLog.Warn(
                    "Luggage",
                    "Luggage storage base markers missing. bagA1=" + (luggageStorageBagA1 != null) +
                    ", preventer=" + (luggagePreventerA1 != null) +
                    ". Luggage will ride with taxi without storage animation.",
                    this);
            }

            return hasBaseMarkers;
        }

        private bool ResolveLuggageStoragePathReferences(bool warnIfMissing = true)
        {
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            luggageStorageA11 = ResolveAliasedNamedReference(searchRoot, luggageStorageA11, LuggageStorageA11Names);
            luggageStorageA12 = ResolveAliasedNamedReference(searchRoot, luggageStorageA12, LuggageStorageA12Names);
            luggageStorageA13 = ResolveAliasedNamedReference(searchRoot, luggageStorageA13, LuggageStorageA13Names);
            luggageStorageBagA2 = ResolveExactNamedReference(searchRoot, luggageStorageBagA2, LuggageStorageBagA2Name);
            luggageStorageA21 = ResolveAliasedNamedReference(searchRoot, luggageStorageA21, LuggageStorageA21Names);
            luggageStorageBagA3 = ResolveExactNamedReference(searchRoot, luggageStorageBagA3, LuggageStorageBagA3Name);

            bool hasCorePathMarkers = luggageStorageBagA2 != null && luggageStorageBagA3 != null;

            if (hasCorePathMarkers)
            {
                return true;
            }

            if (warnIfMissing && !hasLoggedLuggageStorageMissingMarkers)
            {
                hasLoggedLuggageStorageMissingMarkers = true;
                CocoonDebugLog.Warn(
                    "Luggage",
                    "Storage core path markers missing: bagA2=" + (luggageStorageBagA2 != null) +
                    ", bagA3=" + (luggageStorageBagA3 != null) +
                    ". Optional markers: A1.1=" + (luggageStorageA11 != null) +
                    ", A1.2=" + (luggageStorageA12 != null) +
                    ", A1.3=" + (luggageStorageA13 != null) +
                    ", A2.1=" + (luggageStorageA21 != null) +
                    ". bagA1 remains visible at A1 and storage will complete to avoid blocking taxi.",
                    this);
            }

            return false;
        }

        private void HideLuggageStorageMarkers()
        {
            SetTransformRenderersEnabled(luggageStorageBagA1, false);
            HideLuggageStorageWaypointMarkers();
        }

        private void HideLuggageStorageWaypointMarkers()
        {
            SetTransformRenderersEnabled(luggageStorageA11, false);
            SetTransformRenderersEnabled(luggageStorageA12, false);
            SetTransformRenderersEnabled(luggageStorageA13, false);
            SetTransformRenderersEnabled(luggageStorageBagA2, false);
            SetTransformRenderersEnabled(luggageStorageA21, false);
            SetTransformRenderersEnabled(luggageStorageBagA3, false);
        }

        private void CaptureLuggageStorageBagA1AuthoredPoseIfNeeded()
        {
            if (hasAuthoredLuggageStorageBagA1Pose || luggageStorageBagA1 == null)
            {
                return;
            }

            luggageStorageBagA1LocalPosition = luggageStorageBagA1.localPosition;
            luggageStorageBagA1LocalRotation = luggageStorageBagA1.localRotation;
            luggageStorageBagA1LocalScale = luggageStorageBagA1.localScale;
            hasAuthoredLuggageStorageBagA1Pose = true;
        }

        private void RestoreLuggageStorageBagA1AuthoredPose()
        {
            if (luggageStorageBagA1 == null || !hasAuthoredLuggageStorageBagA1Pose)
            {
                return;
            }

            luggageStorageBagA1.localPosition = luggageStorageBagA1LocalPosition;
            luggageStorageBagA1.localRotation = luggageStorageBagA1LocalRotation;
            luggageStorageBagA1.localScale = luggageStorageBagA1LocalScale;
        }

        private void CaptureLuggagePreventerStowedPoseIfNeeded()
        {
            if (hasAuthoredLuggagePreventerStowedPose || luggagePreventerA1 == null)
            {
                return;
            }

            luggagePreventerStowedLocalPosition = luggagePreventerA1.localPosition;
            luggagePreventerStowedLocalRotation = luggagePreventerA1.localRotation;
            luggagePreventerStowedLocalScale = luggagePreventerA1.localScale;
            hasAuthoredLuggagePreventerStowedPose = true;
        }

        private void RestoreLuggagePreventerStowedPose()
        {
            if (luggagePreventerA1 == null || !hasAuthoredLuggagePreventerStowedPose)
            {
                return;
            }

            luggagePreventerA1.localPosition = luggagePreventerStowedLocalPosition;
            luggagePreventerA1.localRotation = luggagePreventerStowedLocalRotation;
            luggagePreventerA1.localScale = luggagePreventerStowedLocalScale;
        }

        private void StartPassengerSeatStorageSequence()
        {
            if (!ResolveSeatStorageReferences())
            {
                seatStoragePhase = CocoonSeatStoragePhase.Complete;
                return;
            }

            HideSeatStorageMarkers();
            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) && seatV2.HasStorageReferences)
            {
                seatStorageUsingSeatV2 = true;
                seatV2.StartStorage();
                BindMiniScreenSeatFollow();
                SyncMiniScreenSeatFollow();
                seatStoragePhase = CocoonSeatStoragePhase.MoveToMarker;
                seatStorageTimer = 0f;
                hasLoggedSeatStorageRotateStart = false;
                hasLoggedSeatStorageMoveStart = true;
                hasLoggedSeatStorageComplete = false;
                CocoonDebugLog.Info("Seat", "Seat V2 storage animation started through CocoonSeatV2Controller.", this);
                return;
            }

            seatStorageUsingSeatV2 = false;
            seatStorageSegmentStartPosition = seat1.position;
            seatStorageSegmentStartRotation = seat1.rotation;
            seatStorageSegmentStartWorldScale = seat1.lossyScale;
            BindMiniScreenSeatFollow();
            SyncMiniScreenSeatFollow();
            seatStoragePhase = CocoonSeatStoragePhase.MoveToMarker;
            seatStorageTimer = 0f;
            hasLoggedSeatStorageRotateStart = false;
            hasLoggedSeatStorageMoveStart = false;
            hasLoggedSeatStorageComplete = false;
            CocoonDebugLog.Info("Seat", "Seat storage animation started from authored pose; initial rotation phase skipped.", this);
        }

        private void UpdatePassengerSeatStorageSequence()
        {
            if (seatStoragePhase == CocoonSeatStoragePhase.Inactive ||
                seatStoragePhase == CocoonSeatStoragePhase.Complete)
            {
                return;
            }

            if (!ResolveSeatStorageReferences())
            {
                seatStoragePhase = CocoonSeatStoragePhase.Complete;
                return;
            }

            if (seatStorageUsingSeatV2 && TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) && seatV2.HasStorageReferences)
            {
                if (!seatV2.IsStorageComplete)
                {
                    return;
                }

                seatStoragePhase = CocoonSeatStoragePhase.Complete;
                if (!hasLoggedSeatStorageComplete)
                {
                    hasLoggedSeatStorageComplete = true;
                    CocoonDebugLog.Info("Seat", "Seat V2 storage animation complete.", this);
                }

                PlaySeatOutLights();
                return;
            }

            switch (seatStoragePhase)
            {
                case CocoonSeatStoragePhase.RotateToMarker:
                    RunPassengerSeatRotateToMarker();
                    break;
                case CocoonSeatStoragePhase.MoveToMarker:
                    RunPassengerSeatMoveToMarker();
                    break;
            }
        }

        private void RunPassengerSeatRotateToMarker()
        {
            if (!hasLoggedSeatStorageRotateStart)
            {
                hasLoggedSeatStorageRotateStart = true;
                CocoonDebugLog.Info("Seat", "Seat1 rotating to " + seat1RotationTarget.name + " over " + seat1RotateDuration.ToString("0.##") + "s.", this);
            }

            bool completed;
            float t = AdvanceSeatStorageProgress(seat1RotateDuration, out completed);
            ApplySeat1RotationAroundStoredPivot(Quaternion.Slerp(seatStorageSegmentStartRotation, seat1RotationTarget.rotation, t));

            if (!completed)
            {
                return;
            }

            ApplySeat1RotationAroundStoredPivot(seat1RotationTarget.rotation);
            seatStorageSegmentStartPosition = seat1.position;
            seatStorageSegmentStartRotation = seat1.rotation;
            seatStorageSegmentStartWorldScale = seat1.lossyScale;
            seatStoragePhase = CocoonSeatStoragePhase.MoveToMarker;
            seatStorageTimer = 0f;
        }

        private void RunPassengerSeatMoveToMarker()
        {
            if (!hasLoggedSeatStorageMoveStart)
            {
                hasLoggedSeatStorageMoveStart = true;
                CocoonDebugLog.Info("Seat", "Seat1 moving to " + seat1MoveTarget.name + " over " + seat1MoveDuration.ToString("0.##") + "s.", this);
            }

            bool completed;
            float t = AdvanceSeatStorageProgress(seat1MoveDuration, out completed);
            ApplySeat1WorldPose(
                Vector3.Lerp(seatStorageSegmentStartPosition, seat1MoveTarget.position, t),
                Quaternion.Slerp(seatStorageSegmentStartRotation, seat1MoveTarget.rotation, t),
                Vector3.Lerp(seatStorageSegmentStartWorldScale, GetWorldScale(seat1MoveTarget), t));

            if (!completed)
            {
                return;
            }

            ApplySeat1WorldPose(seat1MoveTarget.position, seat1MoveTarget.rotation, GetWorldScale(seat1MoveTarget));
            seatStoragePhase = CocoonSeatStoragePhase.Complete;
            if (!hasLoggedSeatStorageComplete)
            {
                hasLoggedSeatStorageComplete = true;
                CocoonDebugLog.Info("Seat", "Seat1 storage animation complete.", this);
            }

            PlaySeatOutLights();
        }

        private float AdvanceSeatStorageProgress(float duration, out bool completed)
        {
            duration = Mathf.Max(0.01f, duration);
            seatStorageTimer = Mathf.Min(duration, seatStorageTimer + Time.deltaTime);
            completed = seatStorageTimer >= duration;
            return Mathf.SmoothStep(0f, 1f, seatStorageTimer / duration);
        }

        private void ApplySeat1WorldPose(Vector3 worldPosition, Quaternion worldRotation, Vector3 worldScale)
        {
            seat1.position = worldPosition;
            seat1.rotation = worldRotation;
            SetWorldScale(seat1, worldScale);
            SyncMiniScreenSeatFollow();
        }

        private void CaptureSeatStorageRotationPivot()
        {
            seatStorageHasRendererPivot = TryGetRendererBounds(seat1, out Bounds seatBounds);
            seatStoragePivotWorldPosition = seatStorageHasRendererPivot ? seatBounds.center : seat1.position;
            seatStoragePivotLocalPoint = seat1.InverseTransformPoint(seatStoragePivotWorldPosition);

            if (!seatStorageHasRendererPivot && !hasLoggedSeatStorageMissingPivot)
            {
                hasLoggedSeatStorageMissingPivot = true;
                CocoonDebugLog.Warn(
                    "Seat",
                    "Seat1 has no enabled renderer bounds; rotation will fall back to transform origin.",
                    this);
            }
        }

        private void ApplySeat1RotationAroundStoredPivot(Quaternion worldRotation)
        {
            if (!seatStorageHasRendererPivot)
            {
                ApplySeat1WorldPose(seatStorageSegmentStartPosition, worldRotation, seatStorageSegmentStartWorldScale);
                return;
            }

            seat1.position = seatStorageSegmentStartPosition;
            seat1.rotation = worldRotation;
            SetWorldScale(seat1, seatStorageSegmentStartWorldScale);
            Vector3 currentPivotWorldPosition = seat1.TransformPoint(seatStoragePivotLocalPoint);
            seat1.position += seatStoragePivotWorldPosition - currentPivotWorldPosition;
            SyncMiniScreenSeatFollow();
        }

        private bool ResolveSeatStorageReferences(bool warnIfMissing = true)
        {
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) && seatV2.HasStorageReferences)
            {
                seat1 = seatV2.SeatTransform;
                seat1RotationTarget = null;
                seat1MoveTarget = seatV2.StorageTarget;
                seatedRiderHeadAnchor = seatV2.SeatedRiderHeadAnchor != null ? seatV2.SeatedRiderHeadAnchor : seatedRiderHeadAnchor;
                CaptureSeat1StowedPoseIfNeeded();

                if (!hasLoggedSeatStorageBinding)
                {
                    hasLoggedSeatStorageBinding = true;
                    CocoonDebugLog.Info(
                        "Seat",
                        "Seat storage markers bound to Seat V2: body=" + GetTransformPath(seat1) +
                        ", outTarget=" + GetTransformPath(seat1MoveTarget) + ".",
                        this);
                }

                return true;
            }

            seat1 = ResolveExactNamedReference(searchRoot, seat1, Seat1Name);
            seat1RotationTarget = ResolveExactNamedReference(searchRoot, seat1RotationTarget, Seat1RotationTargetName);
            seat1MoveTarget = ResolvePreferredSeatMarkerReference(searchRoot, seat1MoveTarget, Seat1MoveTargetNames);
            CaptureSeat1StowedPoseIfNeeded();

            bool hasSeatMarkers = seat1 != null && seat1MoveTarget != null;
            if (hasSeatMarkers)
            {
                if (!hasLoggedSeatStorageBinding)
                {
                    hasLoggedSeatStorageBinding = true;
                    CocoonDebugLog.Info(
                        "Seat",
                        "Seat storage markers bound: seat1=" + GetTransformPath(seat1) +
                        ", seat1_=" + GetTransformPath(seat1RotationTarget) +
                        ", 1x=" + GetTransformPath(seat1MoveTarget) + ".",
                        this);
                }

                return true;
            }

            if (warnIfMissing && !hasLoggedSeatStorageMissingMarkers)
            {
                hasLoggedSeatStorageMissingMarkers = true;
                CocoonDebugLog.Warn(
                    "Seat",
                    "Seat storage markers missing. seat1=" + (seat1 != null) +
                    ", seat1_=" + (seat1RotationTarget != null) + " (optional) " +
                    ", 1x=" + (seat1MoveTarget != null) +
                    ". Seat animation skipped without blocking luggage or taxi.",
                    this);
            }

            return false;
        }

        private void CaptureSeat1StowedPoseIfNeeded()
        {
            if (hasAuthoredSeat1StowedPose || seat1 == null)
            {
                return;
            }

            seat1StowedLocalPosition = seat1.localPosition;
            seat1StowedLocalRotation = seat1.localRotation;
            seat1StowedLocalScale = seat1.localScale;
            hasAuthoredSeat1StowedPose = true;
        }

        private void RestoreSeat1StowedPose()
        {
            if (seat1 == null || !hasAuthoredSeat1StowedPose)
            {
                return;
            }

            seat1.localPosition = seat1StowedLocalPosition;
            seat1.localRotation = seat1StowedLocalRotation;
            seat1.localScale = seat1StowedLocalScale;
        }

        private void HideSeatStorageMarkers()
        {
            SetTransformRenderersEnabled(seat1RotationTarget, false);
            SetTransformRenderersEnabled(seat1MoveTarget, false);
        }

        private bool TryGetSeatV2Controller(out CocoonSeatV2Controller controller)
        {
            if (seatV2Controller == null)
            {
                seatV2Controller = GetComponentInChildren<CocoonSeatV2Controller>(true);
            }

            if (seatV2Controller == null && taxiRoot != null)
            {
                seatV2Controller = taxiRoot.GetComponentInChildren<CocoonSeatV2Controller>(true);
            }

            if (seatV2Controller != null)
            {
                seatV2Controller.ResolveReferences();
            }

            controller = seatV2Controller;
            return controller != null && controller.SeatTransform != null;
        }

        private CocoonCabinScreenController GetCabinScreenController()
        {
            if (cabinScreenController != null)
            {
                return cabinScreenController;
            }

            cabinScreenController = GetComponent<CocoonCabinScreenController>();
            if (cabinScreenController == null && taxiRoot != null)
            {
                cabinScreenController = taxiRoot.GetComponent<CocoonCabinScreenController>();
            }

            if (cabinScreenController == null && taxiRoot != null)
            {
                cabinScreenController = taxiRoot.GetComponentInChildren<CocoonCabinScreenController>(true);
            }

            return cabinScreenController;
        }

        private CocoonLightSequenceController GetLightSequenceController()
        {
            if (lightSequenceController != null)
            {
                return lightSequenceController;
            }

            lightSequenceController = GetComponent<CocoonLightSequenceController>();
            if (lightSequenceController == null && taxiRoot != null)
            {
                lightSequenceController = taxiRoot.GetComponent<CocoonLightSequenceController>();
            }

            if (lightSequenceController == null && taxiRoot != null)
            {
                lightSequenceController = taxiRoot.GetComponentInChildren<CocoonLightSequenceController>(true);
            }

            if (lightSequenceController == null)
            {
                lightSequenceController = UnityEngine.Object.FindObjectOfType<CocoonLightSequenceController>(true);
            }

            return lightSequenceController;
        }

        private void ApplyCabinDefaultUiState()
        {
            CocoonCabinScreenController controller = GetCabinScreenController();
            if (controller != null)
            {
                controller.ApplyDefaultScreens();
            }

            hasAppliedCabinOnboardUi = false;
            hasAppliedCabinSeatedUi = false;
            hasAppliedCabinDestinationUi = false;
        }

        private void ApplyCabinOnboardUiState()
        {
            if (hasAppliedCabinOnboardUi)
            {
                return;
            }

            CocoonCabinScreenController controller = GetCabinScreenController();
            if (controller != null)
            {
                controller.ShowMainOnboard();
                controller.ShowMiniUi(0);
            }

            hasAppliedCabinOnboardUi = true;
        }

        private void ApplyCabinSeatedUiState()
        {
            if (hasAppliedCabinSeatedUi)
            {
                return;
            }

            CocoonCabinScreenController controller = GetCabinScreenController();
            if (controller != null)
            {
                controller.StartSeatedMainSequence();
                controller.StartSeatedMiniSequence();
            }

            hasAppliedCabinSeatedUi = true;
        }

        private void ApplyCabinDestinationUiState()
        {
            if (hasAppliedCabinDestinationUi)
            {
                return;
            }

            CocoonCabinScreenController controller = GetCabinScreenController();
            if (controller != null)
            {
                controller.ShowMainDestination();
                controller.StopSeatedMiniSequence();
            }

            hasAppliedCabinDestinationUi = true;
            hasAppliedCabinSeatedUi = false;
        }

        private void PlayFloorEntryLights()
        {
            if (hasPlayedFloorEntryLights)
            {
                return;
            }

            CocoonLightSequenceController controller = GetLightSequenceController();
            if (controller != null)
            {
                controller.PlayFloorEntrySequence();
                hasPlayedFloorEntryLights = true;
                return;
            }

            CocoonDebugLog.Warn("Light", "FLOORLIGHT sequence requested but no CocoonLightSequenceController was found.", this);
        }

        private void PlaySeatOutLights()
        {
            if (hasPlayedSeatOutLights)
            {
                return;
            }

            CocoonLightSequenceController controller = GetLightSequenceController();
            if (controller != null)
            {
                controller.PlaySeatOutSequence();
                hasPlayedSeatOutLights = true;
                return;
            }

            CocoonDebugLog.Warn("Light", "SEATLIGHT sequence requested but no CocoonLightSequenceController was found.", this);
        }

        private void PlayLuggagePreventerLights()
        {
            if (hasPlayedLuggagePreventerLights)
            {
                return;
            }

            CocoonLightSequenceController controller = GetLightSequenceController();
            if (controller != null)
            {
                controller.PlayLuggagePreventerPulse();
                hasPlayedLuggagePreventerLights = true;
                return;
            }

            CocoonDebugLog.Warn("Light", "Luggage preventer light pulse requested but no CocoonLightSequenceController was found.", this);
        }

        private void BindMiniScreenSeatFollow()
        {
            CocoonCabinScreenController controller = GetCabinScreenController();
            if (controller == null)
            {
                return;
            }

            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) && seatV2.MiniScreenAnchor != null)
            {
                controller.SetMiniScreenAnchorOverride(seatV2.MiniScreenAnchor);
                return;
            }

            if (seat1 == null)
            {
                return;
            }

            controller.BindMiniScreenToSeatMotion(seat1);
        }

        private void SyncMiniScreenSeatFollow()
        {
            CocoonCabinScreenController controller = GetCabinScreenController();
            if (controller == null)
            {
                return;
            }

            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) && seatV2.MiniScreenAnchor != null)
            {
                controller.SetMiniScreenAnchorOverride(seatV2.MiniScreenAnchor);
                return;
            }

            if (seat1 == null)
            {
                return;
            }

            controller.SyncMiniScreenToSeatMotion(seat1);
        }

        private bool IsDestinationExitStagingLockedForSeatToggle()
        {
            switch (destinationExitPhase)
            {
                case CocoonDestinationExitPhase.Inactive:
                case CocoonDestinationExitPhase.CruisingToDropoff:
                case CocoonDestinationExitPhase.PullOverToCurb:
                    return false;
                default:
                    return true;
            }
        }

        private void UpdateSeatDownPromptAndSequence()
        {
            if (IsDestinationExitStagingLockedForSeatToggle())
            {
                return;
            }

            bool pressed = ReadRightControllerButton(XRCommonUsages.primaryButton) ||
                           (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame);
            bool destinationPressed = ReadRightControllerButton(XRCommonUsages.secondaryButton) ||
                                      (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame);

            if (pressed && !seatToggleButtonWasDown && riderOnboard && riderIsSeated)
            {
                StandUpFromSeat();
                seatToggleButtonWasDown = pressed;
                return;
            }

            if (seatSitPhase == CocoonSeatSitPhase.Complete)
            {
                UpdateSeatedBindingAndTurn();
                if (destinationPressed && !destinationDropoffRequested)
                {
                    BeginDestinationDropoffRequest();
                    seatToggleButtonWasDown = pressed;
                    return;
                }

                if (pressed && !seatToggleButtonWasDown)
                {
                    StandUpFromSeat();
                }

                seatToggleButtonWasDown = pressed;
                return;
            }

            if (seatSitPhase != CocoonSeatSitPhase.Inactive &&
                seatSitPhase != CocoonSeatSitPhase.StandingUp)
            {
                UpdateSeatDownSequence();
                seatToggleButtonWasDown = pressed;
                return;
            }

            if (pressed &&
                !seatToggleButtonWasDown &&
                riderOnboard &&
                seatSitPhase == CocoonSeatSitPhase.Inactive &&
                seatStoragePhase == CocoonSeatStoragePhase.Complete &&
                ResolveSeatStorageReferences(false))
            {
                sitPromptWasShown = true;
                sitPromptActive = false;
                SetSitPromptPanelActive(false);
                StartSeatDownSequence();
                seatToggleButtonWasDown = pressed;
                return;
            }

            if (!sitPromptWasShown &&
                !sitPromptActive &&
                riderOnboard &&
                seatStoragePhase == CocoonSeatStoragePhase.Complete &&
                ResolveSeatStorageReferences(false))
            {
                sitPromptWasShown = true;
                sitPromptWasPressed = false;
                bool suppressPrompt = ShouldSuppressHeadLockedFlowUi();
                sitPromptActive = !suppressPrompt;
                sitPromptTimer = suppressPrompt ? 0f : Mathf.Max(0.1f, sitPromptSeconds);
                SetSitPromptPanelActive(!suppressPrompt);
                if (!hasLoggedSeatSitPrompt)
                {
                    hasLoggedSeatSitPrompt = true;
                    CocoonDebugLog.Info(
                        "Seat",
                        suppressPrompt
                            ? "Sit input armed after seat storage completed; head-locked sit prompt suppressed."
                            : "Sit prompt shown after seat storage animation completed.",
                        this);
                }
            }

            bool canAcceptSitInput =
                sitPromptWasShown &&
                seatSitPhase == CocoonSeatSitPhase.Inactive &&
                riderOnboard &&
                seatStoragePhase == CocoonSeatStoragePhase.Complete &&
                ResolveSeatStorageReferences(false);
            if (canAcceptSitInput && pressed && !seatToggleButtonWasDown)
            {
                sitPromptActive = false;
                SetSitPromptPanelActive(false);
                StartSeatDownSequence();
                seatToggleButtonWasDown = pressed;
                return;
            }

            sitPromptWasPressed = pressed;
            seatToggleButtonWasDown = pressed;
            if (!sitPromptActive)
            {
                return;
            }

            sitPromptTimer -= Time.deltaTime;
            if (sitPromptTimer <= 0f)
            {
                sitPromptActive = false;
                SetSitPromptPanelActive(false);
                CocoonDebugLog.Info("Seat", "Sit prompt timed out; right A remains available for sit-down.", this);
            }
        }

        private void StartSeatDownSequence()
        {
            if (!ResolveSeatSitReferences())
            {
                seatSitPhase = CocoonSeatSitPhase.Inactive;
                return;
            }

            if (seatStoragePhase != CocoonSeatStoragePhase.Complete ||
                !ResolveSeatStorageReferences(false))
            {
                CocoonDebugLog.Warn("Seat", "Sit-down flow ignored because seat storage has not reached the out target yet.", this);
                seatSitPhase = CocoonSeatSitPhase.Inactive;
                return;
            }

            CaptureSeatedHeadAnchorLocalPose();
            SetRiderHeightLockEnabled(false);
            Transform head = ResolveRiderHeadTransform();
            Transform rig = ResolveRiderRootTransform();
            if (head == null || rig == null)
            {
                SetRiderHeightLockEnabled(true);
                CocoonDebugLog.Warn("Seat", "Sit-down flow ignored because rider head or rig is missing. head=" + (head != null) + ", rig=" + (rig != null) + ".", this);
                seatSitPhase = CocoonSeatSitPhase.Inactive;
                return;
            }

            SetRiderLocomotionEnabled(false);
            HideSeatSitMarkers();
            ApplyCabinSeatedUiState();
            riderIsSeated = false;
            seatSitMoveStartHeadPosition = head.position;
            seatSitMoveStartHeadRotation = head.rotation;
            CaptureSeatMoveStartPoseInTaxiReference(head);
            CaptureSeatMoveStartWorldOffset(head, rig);
            seatSitSegmentStartLocalPosition = seat1.localPosition;
            seatSitSegmentStartLocalRotation = seat1.localRotation;
            seatSitSegmentStartLocalScale = seat1.localScale;
            seatSitUsingSeatV2Controller = TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2ForSit) &&
                                           seatV2ForSit.HasSitReferences;
            seatSitV2BackMovementStarted = false;
            if (!seatSitUsingSeatV2Controller || seatV2ForSit.EnableSeatSitRotationToMarker)
            {
                seatSitTargetLocalRotation = GetLocalRotationRelativeToSeatParent(seat1SitTarget.rotation);
                CaptureSeatSitRotationPivot();
            }
            else
            {
                seatSitTargetLocalRotation = seatSitSegmentStartLocalRotation;
                seatSitHasRendererPivot = false;
            }
            seatSitRotatePendingContinuousEntry = false;
            hasSeatSitRotatePreviousAnchorRotation = false;
            seatSitRotatePreviousAnchorRotation = Quaternion.identity;
            hasSeatSitMoveFinalPose = false;
            seatSitMoveFinalHeadPosition = Vector3.zero;
            seatSitMoveFinalHeadRotation = Quaternion.identity;
            seatSitMoveFinalRigPosition = Vector3.zero;
            seatSitMoveFinalRigRotation = Quaternion.identity;
            seatSitTimer = 0f;
            seatSitPhase = CocoonSeatSitPhase.MoveRiderToSeat;
            hasLoggedSeatMoveStart = false;
            hasLoggedSeatMoveCompleteWithoutSnap = false;
            hasLoggedSeatRotateContinuousEntry = false;
            hasLoggedSeatRotateAnchorFollow = false;
            hasLoggedSeatSitStart = false;
            hasLoggedRoFoldStart = false;
            hasLoggedSeatSitComplete = false;
            hasLoggedSeatStandUp = false;
            hasLoggedSeatedTurn = false;
            Vector3 anchorTarget = ResolveSeatedHeadAnchorWorldPosition();
            Quaternion anchorRotation = ResolveSeatedHeadCameraWorldRotation();
            CocoonDebugLog.Info("Seat", "Sit-down flow accepted; Seated head anchor camera forward is " + GetSeatedHeadCameraForwardAxisLabel() + ". cameraStart=" +
                FormatPosition(seatSitMoveStartHeadPosition) +
                ", cameraStartTaxiLocal=" + FormatPosition(seatSitMoveStartHeadTaxiLocalPosition) +
                ", movingReference=" + (seatSitMoveStartUsesTaxiReference ? "taxi-local" : "world") +
                ", cachedWorldOffset=" + hasSeatSitMoveStartWorldOffset +
                ", anchorTarget=" + FormatPosition(anchorTarget) +
                ", anchorForward=" + FormatPosition(anchorRotation * Vector3.forward) +
                ", anchorUp=" + FormatPosition(anchorRotation * Vector3.up) + ".", this);
        }

        private void UpdateSeatDownSequence()
        {
            if (!ResolveSeatSitReferences())
            {
                seatSitPhase = CocoonSeatSitPhase.Complete;
                return;
            }

            switch (seatSitPhase)
            {
                case CocoonSeatSitPhase.MoveRiderToSeat:
                    RunRiderMoveToSeat();
                    break;
                case CocoonSeatSitPhase.RotateSeat:
                    RunSeatSitRotation();
                    break;
                case CocoonSeatSitPhase.RotateRo:
                    RunRoFoldRotation();
                    break;
            }
        }

        private void RunRiderMoveToSeat()
        {
            if (!hasLoggedSeatMoveStart)
            {
                hasLoggedSeatMoveStart = true;
                CocoonDebugLog.Info("Seat", "Moving rider head to seated anchor (" + GetSeatedHeadCameraForwardAxisLabel() + " forward) over " + seatMoveRiderToAnchorDuration.ToString("0.##") + "s.", this);
            }

            bool completed;
            float t = AdvanceSeatSitProgress(seatMoveRiderToAnchorDuration, out completed);
            Vector3 targetPosition = ResolveSeatedHeadAnchorWorldPosition();
            Quaternion targetRotation = ResolveSeatedHeadCameraWorldRotation();
            Vector3 startPosition = ResolveSeatMoveStartHeadWorldPosition();
            Quaternion startRotation = ResolveSeatMoveStartHeadWorldRotation();
            Vector3 desiredPosition = Vector3.Lerp(startPosition, targetPosition, t);
            Quaternion desiredRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            if (!AlignSeatMoveHeadYawOnlyToWorldPose(desiredPosition, desiredRotation, false, null))
            {
                SetRiderHeightLockEnabled(true);
                SetRiderLocomotionEnabled(true);
                seatSitPhase = CocoonSeatSitPhase.Inactive;
                return;
            }

            if (!completed)
            {
                return;
            }

            CaptureSeatMoveFinalPose();
            if (!hasLoggedSeatMoveCompleteWithoutSnap)
            {
                hasLoggedSeatMoveCompleteWithoutSnap = true;
                CocoonDebugLog.Info("Seat", "Seat move complete without final snap; entering rotate phase from current rider pose.", this);
            }

            seatSitRotatePendingContinuousEntry = true;
            seatSitTimer = 0f;
            seatSitPhase = CocoonSeatSitPhase.RotateSeat;
        }

        private void RunSeatSitRotation()
        {
            if (seatSitUsingSeatV2Controller &&
                TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) &&
                seatV2.HasSitReferences &&
                !seatV2.EnableSeatSitRotationToMarker)
            {
                RunSeatV2BackMovement(seatV2);
                return;
            }

            if (!hasLoggedSeatSitStart)
            {
                hasLoggedSeatSitStart = true;
                CocoonDebugLog.Info("Seat", "Seat1 rotating to " + seat1SitTarget.name + " over " + seatSitRotateDuration.ToString("0.##") + "s.", this);
            }

            if (seatSitRotatePendingContinuousEntry)
            {
                seatSitRotatePendingContinuousEntry = false;
                ApplySeatSitLocalRotationAroundStoredPivot(seatSitSegmentStartLocalRotation);
                CaptureSeatRotateAnchorFollowReference();
                LogSeatRotateContinuousEntry();
                return;
            }

            bool completed;
            float t = AdvanceSeatSitProgress(seatSitRotateDuration, out completed);
            ApplySeatSitLocalRotationAroundStoredPivot(Quaternion.Slerp(seatSitSegmentStartLocalRotation, seatSitTargetLocalRotation, t));
            FollowRiderYawWithSeatedAnchorDelta();
            AlignRiderHeadPositionToSeatedAnchor(false);
            if (!completed)
            {
                return;
            }

            ApplySeatSitLocalRotationAroundStoredPivot(seatSitTargetLocalRotation);
            AlignRiderHeadPositionToSeatedAnchor(false);
            CaptureRoFoldStartPose();
            seatSitTimer = 0f;
            seatSitPhase = HasAnyRoFoldTransform() && !roFolded ? CocoonSeatSitPhase.RotateRo : CocoonSeatSitPhase.Complete;
            if (seatSitPhase == CocoonSeatSitPhase.Complete)
            {
                LogSeatSitComplete();
            }
        }

        private void RunSeatV2BackMovement(CocoonSeatV2Controller seatV2)
        {
            if (!hasLoggedSeatSitStart)
            {
                hasLoggedSeatSitStart = true;
                CocoonDebugLog.Info("Seat", "Seat V2 moving to " + seatV2.EffectiveSitTarget.name + " through CocoonSeatV2Controller.", this);
            }

            if (!seatSitV2BackMovementStarted)
            {
                seatSitV2BackMovementStarted = true;
                seatV2.StartSitDown();
                CaptureSeatRotateAnchorFollowReference();
                LogSeatRotateContinuousEntry();
                return;
            }

            FollowRiderYawWithSeatedAnchorDelta();
            AlignRiderHeadPositionToSeatedAnchor(false);
            if (!seatV2.IsSitDownComplete)
            {
                return;
            }

            AlignRiderHeadPositionToSeatedAnchor(false);
            seatSitPhase = CocoonSeatSitPhase.Complete;
            LogSeatSitComplete();
        }

        private void CaptureSeatRotateAnchorFollowReference()
        {
            seatSitRotatePreviousAnchorRotation = ResolveSeatedHeadCameraWorldRotation();
            hasSeatSitRotatePreviousAnchorRotation = true;
            if (logSeatSitDiagnostics)
            {
                CocoonDebugLog.Info("Seat", "Seat rotate anchor-follow initialized without snap.", this);
            }
        }

        private void FollowRiderYawWithSeatedAnchorDelta()
        {
            Quaternion currentAnchorRotation = ResolveSeatedHeadCameraWorldRotation();
            if (!hasSeatSitRotatePreviousAnchorRotation)
            {
                seatSitRotatePreviousAnchorRotation = currentAnchorRotation;
                hasSeatSitRotatePreviousAnchorRotation = true;
                return;
            }

            Vector3 upAxis = ResolveSeatedStableUp();
            Vector3 previousForward = Vector3.ProjectOnPlane(seatSitRotatePreviousAnchorRotation * Vector3.forward, upAxis);
            Vector3 currentForward = Vector3.ProjectOnPlane(currentAnchorRotation * Vector3.forward, upAxis);
            seatSitRotatePreviousAnchorRotation = currentAnchorRotation;
            if (previousForward.sqrMagnitude < 0.0001f || currentForward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float yawDelta = Vector3.SignedAngle(previousForward.normalized, currentForward.normalized, upAxis);
            if (Mathf.Abs(yawDelta) <= 0.0001f)
            {
                return;
            }

            RotateRiderRigAroundCurrentHead(upAxis, yawDelta);
            if (Mathf.Abs(yawDelta) > 15f)
            {
                CocoonDebugLog.Warn("Seat", "Seat rotate anchor yaw delta is unusually large: " + yawDelta.ToString("0.0") + " degrees.", this);
            }
            else if (!hasLoggedSeatRotateAnchorFollow && logSeatSitDiagnostics)
            {
                hasLoggedSeatRotateAnchorFollow = true;
                CocoonDebugLog.Info("Seat", "Seat rotate follows anchor yaw delta.", this);
            }
        }

        private void RunRoFoldRotation()
        {
            if (!hasLoggedRoFoldStart)
            {
                hasLoggedRoFoldStart = true;
                CocoonDebugLog.Info("Seat", "RO rigid group folding around ro1 visible mesh axis; relative poses preserved. angle=" + roFoldLocalYDegrees.ToString("0.#") + " degrees, duration=" + roFoldDuration.ToString("0.##") + "s.", this);
            }

            bool completed;
            float t = AdvanceSeatSitProgress(roFoldDuration, out completed);
            ApplyRoFoldRotation(t);
            if (!completed)
            {
                return;
            }

            ApplyRoFoldRotation(1f);
            roFolded = true;
            seatSitPhase = CocoonSeatSitPhase.Complete;
            LogSeatSitComplete();
        }

        private float AdvanceSeatSitProgress(float duration, out bool completed)
        {
            duration = Mathf.Max(0.01f, duration);
            seatSitTimer = Mathf.Min(duration, seatSitTimer + Time.deltaTime);
            completed = seatSitTimer >= duration;
            return Mathf.SmoothStep(0f, 1f, seatSitTimer / duration);
        }

        private void CaptureSeatMoveFinalPose()
        {
            Transform head = ResolveRiderHeadTransform();
            Transform rig = ResolveRiderRootTransform();
            if (head == null || rig == null)
            {
                hasSeatSitMoveFinalPose = false;
                return;
            }

            seatSitMoveFinalHeadPosition = head.position;
            seatSitMoveFinalHeadRotation = head.rotation;
            seatSitMoveFinalRigPosition = rig.position;
            seatSitMoveFinalRigRotation = rig.rotation;
            hasSeatSitMoveFinalPose = true;
        }

        private void LogSeatRotateContinuousEntry()
        {
            if (hasLoggedSeatRotateContinuousEntry)
            {
                return;
            }

            hasLoggedSeatRotateContinuousEntry = true;
            if (!hasSeatSitMoveFinalPose)
            {
                if (logSeatSitDiagnostics)
                {
                    CocoonDebugLog.Info("Seat", "Seat rotate phase entered from continuous rider pose; final move pose was not available for delta diagnostics.", this);
                }

                return;
            }

            Transform head = ResolveRiderHeadTransform();
            Transform rig = ResolveRiderRootTransform();
            if (head == null || rig == null)
            {
                return;
            }

            float headDelta = Vector3.Distance(head.position, seatSitMoveFinalHeadPosition);
            float headAngleDelta = Quaternion.Angle(head.rotation, seatSitMoveFinalHeadRotation);
            float rigDelta = Vector3.Distance(rig.position, seatSitMoveFinalRigPosition);
            float rigAngleDelta = Quaternion.Angle(rig.rotation, seatSitMoveFinalRigRotation);
            string message = "Seat rotate phase entered from continuous rider pose. headDelta=" +
                             headDelta.ToString("0.000") +
                             "m, headAngleDelta=" + headAngleDelta.ToString("0.0") +
                             "deg, rigDelta=" + rigDelta.ToString("0.000") +
                             "m, rigAngleDelta=" + rigAngleDelta.ToString("0.0") + "deg.";

            if (headDelta > 0.02f || headAngleDelta > 5f || rigDelta > 0.02f || rigAngleDelta > 5f)
            {
                CocoonDebugLog.Warn("Seat", message, this);
            }
            else if (logSeatSitDiagnostics)
            {
                CocoonDebugLog.Info("Seat", message, this);
            }
        }

        private Quaternion GetLocalRotationRelativeToSeatParent(Quaternion worldRotation)
        {
            Transform parent = seat1 != null ? seat1.parent : null;
            return parent != null ? Quaternion.Inverse(parent.rotation) * worldRotation : worldRotation;
        }

        private void ApplySeatSitLocalRotationAroundStoredPivot(Quaternion localRotation)
        {
            if (seat1 == null)
            {
                return;
            }

            if (!seatSitHasRendererPivot)
            {
                seat1.localPosition = seatSitSegmentStartLocalPosition;
                seat1.localRotation = localRotation;
                seat1.localScale = seatSitSegmentStartLocalScale;
                SyncMiniScreenSeatFollow();
                return;
            }

            seat1.localPosition = seatSitSegmentStartLocalPosition;
            seat1.localRotation = localRotation;
            seat1.localScale = seatSitSegmentStartLocalScale;

            Vector3 currentPivotWorldPosition = seat1.TransformPoint(seatSitPivotLocalPoint);
            Transform parent = seat1.parent;
            Vector3 targetPivotWorldPosition = parent != null
                ? parent.TransformPoint(seatSitPivotParentLocalPosition)
                : seatSitPivotParentLocalPosition;
            Vector3 correctionWorld = targetPivotWorldPosition - currentPivotWorldPosition;
            if (parent != null)
            {
                seat1.localPosition += parent.InverseTransformVector(correctionWorld);
            }
            else
            {
                seat1.position += correctionWorld;
            }

            SyncMiniScreenSeatFollow();
        }

        private void CaptureSeatSitRotationPivot()
        {
            seatSitHasRendererPivot = TryGetRendererBounds(seat1, out Bounds seatBounds);
            Vector3 pivotWorldPosition = seatSitHasRendererPivot ? seatBounds.center : seat1.position;
            seatSitPivotLocalPoint = seat1.InverseTransformPoint(pivotWorldPosition);
            Transform parent = seat1.parent;
            seatSitPivotParentLocalPosition = parent != null ? parent.InverseTransformPoint(pivotWorldPosition) : pivotWorldPosition;
        }

        private void CaptureSeatedHeadAnchorLocalPose()
        {
            if (seat1 == null || seatedRiderHeadAnchor == null)
            {
                hasSeatedHeadAnchorLocalPose = false;
                return;
            }

            seatedHeadAnchorLocalPosition = seat1.InverseTransformPoint(seatedRiderHeadAnchor.position);
            seatedHeadAnchorLocalRotation = Quaternion.Inverse(seat1.rotation) * seatedRiderHeadAnchor.rotation;
            hasSeatedHeadAnchorLocalPose = true;
        }

        private void CaptureSeatMoveStartPoseInTaxiReference(Transform head)
        {
            seatSitMoveStartUsesTaxiReference = taxiRoot != null && head != null;
            if (!seatSitMoveStartUsesTaxiReference)
            {
                seatSitMoveStartHeadTaxiLocalPosition = Vector3.zero;
                seatSitMoveStartHeadTaxiLocalRotation = Quaternion.identity;
                return;
            }

            seatSitMoveStartHeadTaxiLocalPosition = taxiRoot.InverseTransformPoint(head.position);
            seatSitMoveStartHeadTaxiLocalRotation = Quaternion.Inverse(taxiRoot.rotation) * head.rotation;
        }

        private void CaptureSeatMoveStartWorldOffset(Transform head, Transform rig)
        {
            hasSeatSitMoveStartWorldOffset = head != null && rig != null;
            if (!hasSeatSitMoveStartWorldOffset)
            {
                seatSitMoveStartRigToHeadWorldOffset = Vector3.zero;
                seatSitMoveStartRigToHeadTaxiLocalOffset = Vector3.zero;
                seatSitMoveStartRigWorldYawRotation = Quaternion.identity;
                seatSitMoveStartRigTaxiLocalYawRotation = Quaternion.identity;
                seatSitMoveStartUsesTaxiRigReference = false;
                return;
            }

            seatSitMoveStartRigWorldYawRotation = ResolveYawOnlyRotation(rig.rotation);
            seatSitMoveStartRigToHeadWorldOffset = head.position - rig.position;
            seatSitMoveStartUsesTaxiRigReference = taxiRoot != null;
            if (seatSitMoveStartUsesTaxiRigReference)
            {
                seatSitMoveStartRigTaxiLocalYawRotation = Quaternion.Inverse(taxiRoot.rotation) * seatSitMoveStartRigWorldYawRotation;
                seatSitMoveStartRigToHeadTaxiLocalOffset = taxiRoot.InverseTransformVector(seatSitMoveStartRigToHeadWorldOffset);
            }
            else
            {
                seatSitMoveStartRigTaxiLocalYawRotation = Quaternion.identity;
                seatSitMoveStartRigToHeadTaxiLocalOffset = Vector3.zero;
            }

            hasLoggedSeatSitSafetyFallback = false;
            if (logSeatSitDiagnostics)
            {
                CocoonDebugLog.Info(
                    "Seat",
                    "Cached seated transition world offset. rig=" + FormatPosition(rig.position) +
                    ", head=" + FormatPosition(head.position) +
                    ", rigToHead=" + FormatPosition(seatSitMoveStartRigToHeadWorldOffset) +
                    ", taxiLocalOffset=" + FormatPosition(seatSitMoveStartRigToHeadTaxiLocalOffset) + ".",
                    this);
            }
        }

        private Vector3 ResolveSeatMoveStartHeadWorldPosition()
        {
            return seatSitMoveStartUsesTaxiReference && taxiRoot != null
                ? taxiRoot.TransformPoint(seatSitMoveStartHeadTaxiLocalPosition)
                : seatSitMoveStartHeadPosition;
        }

        private Quaternion ResolveSeatMoveStartHeadWorldRotation()
        {
            return seatSitMoveStartUsesTaxiReference && taxiRoot != null
                ? taxiRoot.rotation * seatSitMoveStartHeadTaxiLocalRotation
                : seatSitMoveStartHeadRotation;
        }

        private bool AlignSeatMoveHeadYawOnlyToSeatedAnchor(bool logResult)
        {
            if (seatedRiderHeadAnchor == null)
            {
                CocoonDebugLog.Warn("Seat", "Cannot position rider for seated pose. anchor=False.", this);
                return false;
            }

            return AlignSeatMoveHeadYawOnlyToWorldPose(
                ResolveSeatedHeadAnchorWorldPosition(),
                ResolveSeatedHeadCameraWorldRotation(),
                logResult,
                GetTransformPath(seatedRiderHeadAnchor));
        }

        private bool AlignSeatMoveHeadYawOnlyToWorldPose(Vector3 targetPosition, Quaternion targetRotation, bool logResult, string logTargetName)
        {
            Transform rig = ResolveRiderRootTransform();
            Transform head = ResolveRiderHeadTransform();
            if (rig == null || head == null)
            {
                CocoonDebugLog.Warn("Seat", "Cannot position rider for seated pose. head=" + (head != null) + ", rig=" + (rig != null) + ".", this);
                return false;
            }

            if (!hasSeatSitMoveStartWorldOffset)
            {
                return AlignRiderHeadToWorldPose(targetPosition, targetRotation, logResult, logTargetName);
            }

            CharacterController controller = rig.GetComponent<CharacterController>();
            bool restoreController = controller != null && controller.enabled;
            if (restoreController)
            {
                controller.enabled = false;
            }

            Vector3 upAxis = ResolveSeatedStableUp();
            Quaternion startHeadYaw = ResolveYawOnlyRotation(ResolveSeatMoveStartHeadWorldRotation());
            Quaternion targetHeadYaw = ResolveYawOnlyRotation(targetRotation);
            Vector3 startHeadForward = Vector3.ProjectOnPlane(startHeadYaw * Vector3.forward, upAxis);
            Vector3 targetHeadForward = Vector3.ProjectOnPlane(targetHeadYaw * Vector3.forward, upAxis);
            float yawDelta = 0f;
            if (startHeadForward.sqrMagnitude > 0.0001f && targetHeadForward.sqrMagnitude > 0.0001f)
            {
                yawDelta = Vector3.SignedAngle(startHeadForward.normalized, targetHeadForward.normalized, upAxis);
            }

            Quaternion yawDeltaRotation = Quaternion.AngleAxis(yawDelta, upAxis);
            Quaternion startRigYaw = ResolveSeatMoveStartRigWorldYawRotation();
            Quaternion rigRotation = ResolveYawOnlyRotation(yawDeltaRotation * startRigYaw);
            Vector3 baseOffset = ResolveSeatMoveStartRigToHeadWorldOffset();
            Vector3 rotatedOffset = yawDeltaRotation * baseOffset;
            Vector3 rigPosition = targetPosition - rotatedOffset;
            float safetyFloorY = ResolveSeatedRigSafetyFloorY();
            bool usedSafetyFallback = false;
            if (rigPosition.y < safetyFloorY)
            {
                Vector3 currentWorldOffset = head.position - rig.position;
                rigPosition = targetPosition - currentWorldOffset;
                if (rigPosition.y < safetyFloorY)
                {
                    rigPosition.y = safetyFloorY;
                }

                usedSafetyFallback = true;
                if (!hasLoggedSeatSitSafetyFallback)
                {
                    hasLoggedSeatSitSafetyFallback = true;
                    CocoonDebugLog.Warn(
                        "Seat",
                        "Rejected seated rig pose below safety floor. computedY=" + (targetPosition - rotatedOffset).y.ToString("0.###") +
                        ", fallbackY=" + rigPosition.y.ToString("0.###") +
                        ", safetyFloorY=" + safetyFloorY.ToString("0.###") +
                        ", targetHeadY=" + targetPosition.y.ToString("0.###") + ".",
                        this);
                }
            }

            rig.rotation = rigRotation;
            rig.position = rigPosition;

            if (restoreController)
            {
                controller.enabled = true;
            }

            if (logResult || logSeatSitDiagnostics)
            {
                CocoonDebugLog.Info(
                    "Seat",
                    "Rider rig yaw-only positioned for " +
                    (string.IsNullOrEmpty(logTargetName) ? "seated target" : logTargetName) +
                    ". targetHeadY=" + targetPosition.y.ToString("0.###") +
                    ", rigY=" + rig.position.y.ToString("0.###") +
                    ", yawDelta=" + yawDelta.ToString("0.#") +
                    ", safetyFallback=" + usedSafetyFallback + ".",
                    this);
            }

            return true;
        }

        private Quaternion ResolveSeatMoveStartRigWorldYawRotation()
        {
            return seatSitMoveStartUsesTaxiRigReference && taxiRoot != null
                ? ResolveYawOnlyRotation(taxiRoot.rotation * seatSitMoveStartRigTaxiLocalYawRotation)
                : ResolveYawOnlyRotation(seatSitMoveStartRigWorldYawRotation);
        }

        private Vector3 ResolveSeatMoveStartRigToHeadWorldOffset()
        {
            return seatSitMoveStartUsesTaxiRigReference && taxiRoot != null
                ? taxiRoot.TransformVector(seatSitMoveStartRigToHeadTaxiLocalOffset)
                : seatSitMoveStartRigToHeadWorldOffset;
        }

        private Quaternion ResolveYawOnlyRotation(Quaternion rotation)
        {
            Vector3 up = ResolveSeatedStableUp();
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, up);
            if (forward.sqrMagnitude < 0.0001f && taxiRoot != null)
            {
                forward = Vector3.ProjectOnPlane(taxiRoot.forward, up);
            }

            if (forward.sqrMagnitude < 0.0001f && seat1 != null)
            {
                forward = Vector3.ProjectOnPlane(seat1.forward, up);
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        private float ResolveSeatedRigSafetyFloorY()
        {
            float referenceY = taxiRoot != null ? taxiRoot.position.y : transform.position.y;
            return referenceY - Mathf.Max(0.18f, GetExperienceScale() * 2f);
        }

        private void CaptureRoFoldStartPose()
        {
            roFoldTransforms[0] = ro1;
            roFoldTransforms[1] = ro2;
            roFoldTransforms[2] = ro3;
            roFoldFrame = ro1 != null ? ro1.parent : null;
            roFoldHasFrameAxis = false;
            if (ro1 == null)
            {
                CocoonDebugLog.Warn("Seat", "RO rigid group cannot fold because ro1 is missing.", this);
                return;
            }

            roFoldFrame = ro1.parent;
            Vector3 axisWorldPoint;
            Vector3 axisWorldDirection;
            if (!TryResolveRoFoldVisibleAxis(out axisWorldPoint, out axisWorldDirection))
            {
                axisWorldPoint = ro1.position;
                axisWorldDirection = ro1.up.sqrMagnitude > 0.0001f ? ro1.up.normalized : Vector3.up;
                CocoonDebugLog.Warn("Seat", "ro1 visible mesh axis could not be resolved; falling back to ro1 Transform up.", this);
            }

            if (roFoldFrame != null)
            {
                roFoldAxisPointFrameLocal = roFoldFrame.InverseTransformPoint(axisWorldPoint);
                roFoldAxisDirectionFrameLocal = roFoldFrame.InverseTransformDirection(axisWorldDirection).normalized;
                roFoldHasFrameAxis = roFoldAxisDirectionFrameLocal.sqrMagnitude > 0.0001f;
            }
            else
            {
                roFoldAxisPointFrameLocal = axisWorldPoint;
                roFoldAxisDirectionFrameLocal = axisWorldDirection.normalized;
                roFoldHasFrameAxis = roFoldAxisDirectionFrameLocal.sqrMagnitude > 0.0001f;
            }

            for (int i = 0; i < roFoldTransforms.Length; i++)
            {
                Transform target = roFoldTransforms[i];
                if (target == null)
                {
                    continue;
                }

                roFoldStartFramePositions[i] = roFoldFrame != null ? roFoldFrame.InverseTransformPoint(target.position) : target.position;
                roFoldStartFrameRotations[i] = roFoldFrame != null ? Quaternion.Inverse(roFoldFrame.rotation) * target.rotation : target.rotation;
                roFoldStartLocalScales[i] = target.localScale;
            }
        }

        private void ApplyRoFoldRotation(float progress01)
        {
            if (ro1 == null || !roFoldHasFrameAxis)
            {
                return;
            }

            Quaternion delta = Quaternion.AngleAxis(roFoldLocalYDegrees * Mathf.Clamp01(progress01), roFoldAxisDirectionFrameLocal.normalized);

            for (int i = 0; i < roFoldTransforms.Length; i++)
            {
                Transform target = roFoldTransforms[i];
                if (target == null)
                {
                    continue;
                }

                Vector3 rotatedFramePosition = roFoldAxisPointFrameLocal + delta * (roFoldStartFramePositions[i] - roFoldAxisPointFrameLocal);
                Quaternion rotatedFrameRotation = delta * roFoldStartFrameRotations[i];
                if (roFoldFrame != null)
                {
                    target.position = roFoldFrame.TransformPoint(rotatedFramePosition);
                    target.rotation = roFoldFrame.rotation * rotatedFrameRotation;
                }
                else
                {
                    target.position = rotatedFramePosition;
                    target.rotation = rotatedFrameRotation;
                }

                target.localScale = roFoldStartLocalScales[i];
            }
        }

        private bool TryResolveRoFoldVisibleAxis(out Vector3 axisWorldPoint, out Vector3 axisWorldDirection)
        {
            axisWorldPoint = ro1 != null ? ro1.position : Vector3.zero;
            axisWorldDirection = ro1 != null && ro1.up.sqrMagnitude > 0.0001f ? ro1.up.normalized : Vector3.up;
            if (ro1 == null)
            {
                return false;
            }

            MeshFilter[] filters = ro1.GetComponentsInChildren<MeshFilter>(true);
            float bestWorldLength = -1f;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                if (IsSameOrChildOf(filter.transform, ro2) || IsSameOrChildOf(filter.transform, ro3))
                {
                    continue;
                }

                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds meshBounds = filter.sharedMesh.bounds;
                Vector3 size = meshBounds.size;
                Vector3 localAxis = Vector3.right;
                float localLength = size.x;
                if (size.y > localLength)
                {
                    localAxis = Vector3.up;
                    localLength = size.y;
                }

                if (size.z > localLength)
                {
                    localAxis = Vector3.forward;
                    localLength = size.z;
                }

                Vector3 worldAxis = filter.transform.TransformDirection(localAxis);
                float worldAxisMagnitude = worldAxis.magnitude;
                if (worldAxisMagnitude <= 0.0001f)
                {
                    continue;
                }

                float worldLength = localLength * worldAxisMagnitude;
                if (worldLength <= bestWorldLength)
                {
                    continue;
                }

                bestWorldLength = worldLength;
                axisWorldPoint = filter.transform.TransformPoint(meshBounds.center);
                axisWorldDirection = worldAxis.normalized;
            }

            if (bestWorldLength <= 0f)
            {
                if (TryGetRendererBounds(ro1, out Bounds fallbackBounds))
                {
                    axisWorldPoint = fallbackBounds.center;
                }

                return false;
            }

            return true;
        }

        private static bool IsSameOrChildOf(Transform candidate, Transform ancestor)
        {
            if (candidate == null || ancestor == null)
            {
                return false;
            }

            Transform cursor = candidate;
            while (cursor != null)
            {
                if (cursor == ancestor)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private bool HasAnyRoFoldTransform()
        {
            return ro1 != null;
        }

        private void LogSeatSitComplete()
        {
            if (hasLoggedSeatSitComplete)
            {
                return;
            }

            hasLoggedSeatSitComplete = true;
            riderIsSeated = true;
            ApplyCabinSeatedUiState();
            CocoonDebugLog.Info("Seat", "Seat sit-down animation complete.", this);
            BeginDestinationDropoffAfterSeatedIfReady();
        }

        private void BeginDestinationDropoffAfterSeatedIfReady()
        {
            if (!autoBeginDestinationAfterSeated ||
                destinationDropoffRequested ||
                destinationExitPhase != CocoonDestinationExitPhase.Inactive ||
                !riderOnboard ||
                !riderIsSeated)
            {
                return;
            }

            CocoonDebugLog.Info("Destination", "Seat is occupied; automatically beginning destination drive.", this);
            BeginDestinationDropoffRequest();
        }

        private void UpdateSeatedBindingAndTurn()
        {
            if (!riderIsSeated)
            {
                riderIsSeated = true;
            }

            if (!ResolveSeatSitReferences(false))
            {
                return;
            }

            bool allowSeatedStickRotation = true;
            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2))
            {
                allowSeatedStickRotation = seatV2.EnableSeatedStickSeatRotation;
            }

            if (allowSeatedStickRotation &&
                TryReadRightControllerPrimary2DAxis(out Vector2 axis) &&
                Mathf.Abs(axis.x) > Mathf.Max(0.01f, seatedTurnStickDeadzone))
            {
                RotateSeatedSeatAndView(axis.x * seatedTurnSpeedDegreesPerSecond * Time.deltaTime);
                if (!hasLoggedSeatedTurn)
                {
                    hasLoggedSeatedTurn = true;
                    CocoonDebugLog.Info("Seat", "Seated right stick rotates seat1 and rider yaw; HMD head turns remain free.", this);
                }
            }

            AlignRiderHeadPositionToSeatedAnchor(false);
        }

        private void RotateSeatedSeatAndView(float degrees)
        {
            if (seat1 == null || Mathf.Abs(degrees) <= 0.0001f)
            {
                return;
            }

            Vector3 pivotWorld = seat1.position;
            if (TryGetRendererBounds(seat1, out Bounds seatBounds))
            {
                pivotWorld = seatBounds.center;
            }

            Vector3 upAxis = taxiRoot != null && taxiRoot.up.sqrMagnitude > 0.0001f ? taxiRoot.up.normalized : Vector3.up;
            seat1.RotateAround(pivotWorld, upAxis, degrees);
            RotateRiderRigAroundCurrentHead(upAxis, degrees);
            SyncMiniScreenSeatFollow();
        }

        private void BeginDestinationDropoffRequest()
        {
            if (destinationDropoffRequested)
            {
                return;
            }

            destinationDropoffRequested = true;
            destinationRiderDetached = false;
            destinationExitReadyForAReset = false;
            destinationExitResetButtonWasDown = false;
            ClearDepartureMergePlan();
            destinationStopPoint = ResolveDestinationStopPoint();
            destinationStopCursor = CocoonRouteCursor.Invalid();
            hasDestinationStopCursor = false;
            hasLoggedDestinationGraphProjectionFailure = false;
            hasLoggedDestinationGraphDistanceFailure = false;
            hasLoggedDestinationDepartureProjectionFailure = false;
            if (destinationStopPoint != null)
            {
                TryResolveDestinationStopCursor(true);
            }

            destinationExitPhase = CocoonDestinationExitPhase.CruisingToDropoff;
            destinationExitTimer = Mathf.Max(0f, destinationDropoffCruiseSeconds);
            SetBoardingDoorSurfaceScreen(false, false);
            if (destinationStopPoint != null)
            {
                CocoonDebugLog.Info("Destination", "Destination request received. DESTINATION is treated as a ROADMAP graph stop marker at " + FormatPosition(destinationStopPoint.position) + "; taxi will not drive straight toward it.", this);
            }
            else
            {
                CocoonDebugLog.Warn("Destination", "Destination request received, but no DESTINATION marker was found. Falling back to " + destinationExitTimer.ToString("0.0") + "s cruise window.", this);
            }
        }

        private void UpdateDestinationExitSequence()
        {
            if (destinationExitPhase == CocoonDestinationExitPhase.Inactive)
            {
                return;
            }

            SetPanelActive(false);
            SetBoardingCardPromptPanelActive(false);
            SetBoardingCreditCardActive(false);
            SetBoardingDoorSurfaceScreen(false, false);

            if (riderIsSeated || seatSitPhase == CocoonSeatSitPhase.Complete)
            {
                AlignRiderHeadPositionToSeatedAnchor(false);
            }

            switch (destinationExitPhase)
            {
                case CocoonDestinationExitPhase.CruisingToDropoff:
                    RunDestinationCruisingToDropoff();
                    break;
                case CocoonDestinationExitPhase.PullOverToCurb:
                    RunDestinationPullOverToCurb();
                    break;
                case CocoonDestinationExitPhase.SeatToOut:
                    RunDestinationSeatToOut();
                    break;
                case CocoonDestinationExitPhase.LuggageToA1:
                    if (RunPassengerLuggageExitSequence())
                    {
                        BeginDestinationDoorOpening();
                    }
                    break;
                case CocoonDestinationExitPhase.DoorOpening:
                    RunDestinationDoorOpening();
                    break;
                case CocoonDestinationExitPhase.WaitingForExit:
                    RunDestinationExitWait();
                    break;
                case CocoonDestinationExitPhase.DoorClosing:
                    RunDestinationDoorClosing();
                    break;
                case CocoonDestinationExitPhase.SeatRestore:
                    RunDestinationSeatRestore();
                    break;
                case CocoonDestinationExitPhase.DestinationBayExit:
                    RunDestinationBayExit();
                    break;
            }
        }

        private void RunDestinationCruisingToDropoff()
        {
            if (destinationStopPoint == null)
            {
                destinationStopPoint = ResolveDestinationStopPoint();
            }

            if (destinationStopPoint != null)
            {
                if (!hasDestinationStopCursor)
                {
                    TryResolveDestinationStopCursor(false);
                }

                float arrivalDistance = GetScaledArrivalDistance(Mathf.Max(0.02f, destinationStopArrivalDistance));
                if (hasDestinationStopCursor &&
                    EnsureRoadGraphBinding() &&
                    roadGraph.TryGetForwardDistance(taxiGraphCursor, destinationStopCursor, out float beforeMoveRemaining, Mathf.Max(16, roadGraph.EdgeCount + 8)))
                {
                    float anticipatedStep = Mathf.Max(0.1f, destinationStopCruiseSpeed) * GetExperienceScale() * Time.deltaTime;
                    if (beforeMoveRemaining <= arrivalDistance + anticipatedStep)
                    {
                        CocoonDebugLog.Info("Destination", "DESTINATION graph marker reached. remainingForward=" + beforeMoveRemaining.ToString("0.###") + "m; selecting legal curb/bay pull-over.", this);
                        BeginDestinationPullOverToCurb();
                        return;
                    }
                }

                MoveAlongCruisePath(Mathf.Max(0.1f, destinationStopCruiseSpeed));
                if (hasDestinationStopCursor && EnsureRoadGraphBinding())
                {
                    if (roadGraph.TryGetForwardDistance(taxiGraphCursor, destinationStopCursor, out float remaining, Mathf.Max(16, roadGraph.EdgeCount + 8)))
                    {
                        hasLoggedDestinationGraphDistanceFailure = false;
                        if (remaining <= arrivalDistance)
                        {
                            CocoonDebugLog.Info("Destination", "DESTINATION graph marker reached. remainingForward=" + remaining.ToString("0.###") + "m; selecting legal curb/bay pull-over.", this);
                            BeginDestinationPullOverToCurb();
                        }

                        return;
                    }

                    if (!hasLoggedDestinationGraphDistanceFailure)
                    {
                        hasLoggedDestinationGraphDistanceFailure = true;
                        CocoonDebugLog.Warn("Destination", "Cannot currently compute forward graph distance from taxi edge " + taxiGraphCursor.EdgeId + " to DESTINATION edge " + destinationStopCursor.EdgeId + ". Taxi will keep legal ROADMAP cruising; no straight-line fallback will be used.", this);
                    }
                }

                return;
            }

            if (!hasLoggedDestinationFallbackTimer)
            {
                hasLoggedDestinationFallbackTimer = true;
                CocoonDebugLog.Warn("Destination", "No manual DESTINATION marker available; using timed dropoff fallback.", this);
            }

            MoveAlongCruisePath(GetCruiseMoveSpeed());
            destinationExitTimer -= Time.deltaTime;
            if (destinationExitTimer > 0f)
            {
                return;
            }

            BeginDestinationPullOverToCurb();
        }

        private bool TrySelectDestinationDropoffPullOverPoint()
        {
            selectedPullOverPoint = null;
            selectedPullOverCruisePath = null;
            selectedPullOverPathIndex = -1;
            selectedPullOverEntryPathIndex = -1;
            selectedPullOverDirectPullIn = false;
            selectedGraphBayIndex = -1;
            selectedGraphBayCursor = CocoonRouteCursor.Invalid();
            selectedGraphParkingEntryCursor = CocoonRouteCursor.Invalid();
            selectedGraphBayForwardDistance = 0f;
            selectedGraphParkingEntryForwardDistance = 0f;
            hasSelectedGraphParkingEntryCursor = false;
            hasLoggedMissingGraphParkingEntryProjection = false;
            reachedSelectedPullOverEntry = false;
            pendingTurnaroundToOppositeLane = false;
            turnaroundPathIndex = -1;
            parkingPhase = CocoonPullOverParkingPhase.None;

            Vector3 referencePosition = destinationStopPoint != null
                ? destinationStopPoint.position
                : (taxiRoot != null ? taxiRoot.position : transform.position);
            referencePosition.y = 0f;

            if (EnsureRoadGraphBinding() && roadGraph.BayCount > 0)
            {
                int bestIndex = -1;
                float bestReferenceDistance = float.MaxValue;
                float bestForwardDistance = float.MaxValue;
                for (int i = 0; i < roadGraph.BayCount; i++)
                {
                    CocoonRoadGraph.BayBinding bay = roadGraph.GetBay(i);
                    if (bay == null || bay.Stop == null || bay.EdgeId < 0)
                    {
                        continue;
                    }

                    if (!roadGraph.TryGetForwardDistanceToBay(taxiGraphCursor, i, out float forwardDistance))
                    {
                        continue;
                    }

                    Vector3 bayPosition = bay.Stop.position;
                    bayPosition.y = 0f;
                    float referenceDistance = Vector3.Distance(referencePosition, bayPosition);
                    if (referenceDistance < bestReferenceDistance - 0.025f ||
                        (Mathf.Abs(referenceDistance - bestReferenceDistance) <= 0.025f && forwardDistance < bestForwardDistance))
                    {
                        bestIndex = i;
                        bestReferenceDistance = referenceDistance;
                        bestForwardDistance = forwardDistance;
                    }
                }

                if (bestIndex >= 0)
                {
                    CocoonRoadGraph.BayBinding selectedBay = roadGraph.GetBay(bestIndex);
                    selectedPullOverPoint = selectedBay.Stop;
                    selectedGraphBayIndex = bestIndex;
                    selectedGraphBayCursor = new CocoonRouteCursor { EdgeId = selectedBay.EdgeId, Progress = selectedBay.Progress };
                    selectedGraphBayForwardDistance = bestForwardDistance;
                    BuildParkingPlan();
                    if (parkingPhase == CocoonPullOverParkingPhase.None)
                    {
                        selectedPullOverPoint = null;
                        selectedGraphBayIndex = -1;
                        selectedGraphBayCursor = CocoonRouteCursor.Invalid();
                        return false;
                    }

                    if (parkingPhase != CocoonPullOverParkingPhase.ApproachLaneEntry)
                    {
                        selectedGraphParkingEntryForwardDistance = 0f;
                        reachedSelectedPullOverEntry = true;
                    }
                    else
                    {
                        float entryForwardDistance = 0f;
                        if (hasSelectedGraphParkingEntryCursor &&
                            roadGraph.TryGetForwardDistance(taxiGraphCursor, selectedGraphParkingEntryCursor, out entryForwardDistance, Mathf.Max(16, roadGraph.EdgeCount + 8)))
                        {
                            selectedGraphParkingEntryForwardDistance = entryForwardDistance;
                            reachedSelectedPullOverEntry = entryForwardDistance <= Mathf.Max(0.18f * GetExperienceScale(), 0.015f);
                        }
                        else
                        {
                            selectedGraphParkingEntryForwardDistance = 0f;
                            reachedSelectedPullOverEntry = HasPassedParkingPoint(parkingLaneEntryPosition);
                        }
                    }

                    CocoonDebugLog.Info("Destination", "Selected destination pull-over bay " + selectedPullOverPoint.name +
                        ": bayIndex=" + bestIndex +
                        ", markerDistance=" + GetDisplayMeters(bestReferenceDistance).ToString("0.0") + "m" +
                        ", forwardDistance=" + GetDisplayMeters(bestForwardDistance).ToString("0.0") + "m" +
                        ", parkingEntryDistance=" + GetDisplayMeters(selectedGraphParkingEntryForwardDistance).ToString("0.0") + "m.", this);
                    return true;
                }
            }

            if (destinationStopPoint == null)
            {
                return false;
            }

            selectedPullOverPoint = destinationStopPoint;
            selectedPullOverCruisePath = null;
            selectedPullOverPathIndex = -1;
            selectedPullOverEntryPathIndex = -1;
            reachedSelectedPullOverEntry = true;
            selectedGraphBayIndex = -1;
            BuildParkingPlan();
            if (parkingPhase == CocoonPullOverParkingPhase.None)
            {
                selectedPullOverPoint = null;
                return false;
            }

            CocoonDebugLog.Warn("Destination", "No graph bay was available near DESTINATION; using DESTINATION marker as a curb-side fallback parking anchor. Consider placing DESTINATION near a Pickup Bay Stop.", this);
            return true;
        }

        private void BeginDestinationPullOverToCurb()
        {
            taxiMotionSpeed = 0f;
            ClearDepartureMergePlan();
            if (!TrySelectDestinationDropoffPullOverPoint())
            {
                CocoonDebugLog.Warn("Destination", "No legal destination pull-over bay could be selected; starting destination exit staging at current legal graph position.", this);
                ApplyCabinDestinationUiState();
                BeginDestinationExitStaging();
                return;
            }

            destinationExitPhase = CocoonDestinationExitPhase.PullOverToCurb;
            destinationExitTimer = 0f;
            CocoonDebugLog.Info("Destination", "Destination pull-over selected: " + selectedPullOverPoint.name + ". Taxi will use the same frozen entry/diagonal/align/final parking phases as pickup.", this);
        }

        private void RunDestinationPullOverToCurb()
        {
            if (selectedPullOverPoint == null)
            {
                BeginDestinationPullOverToCurb();
                return;
            }

            if (useRoadGraph && selectedGraphBayIndex >= 0 && EnsureRoadGraphBinding() && !reachedSelectedPullOverEntry)
            {
                EnsureSelectedGraphParkingEntryCursor();
                float graphDistance = 0f;
                bool hasEntryDistance = hasSelectedGraphParkingEntryCursor &&
                    roadGraph.TryGetForwardDistance(taxiGraphCursor, selectedGraphParkingEntryCursor, out graphDistance, Mathf.Max(16, roadGraph.EdgeCount + 8));
                if (hasEntryDistance)
                {
                    selectedGraphParkingEntryForwardDistance = graphDistance;
                    if (graphDistance > Mathf.Max(0.18f * GetExperienceScale(), 0.015f))
                    {
                        MoveAlongCruisePath(Mathf.Max(0.1f, destinationStopCruiseSpeed));
                        SetTimer("Dropoff bay ahead");
                        return;
                    }
                }

                reachedSelectedPullOverEntry = true;
                CocoonDebugLog.Info("Destination", "Reached destination parking entry for bay index " + selectedGraphBayIndex +
                    ", remaining=" + GetDisplayMeters(hasEntryDistance ? selectedGraphParkingEntryForwardDistance : 0f).ToString("0.00") +
                    "m. Continuing into curb/bay without reverse or U-turn.", this);
            }

            if (parkingPhase == CocoonPullOverParkingPhase.None)
            {
                BuildParkingPlan();
            }

            KeepTaxiBlockingAtPickupEntry();
            DrawParkingDebugPath();
            switch (parkingPhase)
            {
                case CocoonPullOverParkingPhase.ApproachLaneEntry:
                    SetTimer("Dropoff line up");
                    if (MoveTaxiTowardsParkingPoint(parkingLaneEntryPosition, GetParkingMoveSpeed(1f, MinimumParkingLineupSpeed), false))
                    {
                        parkingPhase = CocoonPullOverParkingPhase.DiagonalIntoBay;
                        CocoonDebugLog.Info("Destination", "Dropoff parking line-up complete; entering diagonal segment.", this);
                    }

                    break;
                case CocoonPullOverParkingPhase.DiagonalIntoBay:
                    SetTimer("Dropoff enter bay");
                    if (MoveTaxiTowardsParkingPoint(parkingDiagonalPosition, GetParkingMoveSpeed(0.9f, MinimumParkingDiagonalSpeed), false))
                    {
                        parkingPhase = CocoonPullOverParkingPhase.AlignWithBay;
                        CocoonDebugLog.Info("Destination", "Dropoff diagonal segment complete; aligning.", this);
                    }

                    break;
                case CocoonPullOverParkingPhase.AlignWithBay:
                    SetTimer("Dropoff align");
                    if (MoveTaxiTowardsParkingPoint(parkingAlignPosition, GetParkingMoveSpeed(0.78f, MinimumParkingAlignSpeed), false))
                    {
                        parkingPhase = CocoonPullOverParkingPhase.SettleAtBay;
                        CocoonDebugLog.Info("Destination", "Dropoff alignment complete; settling.", this);
                    }

                    break;
                case CocoonPullOverParkingPhase.SettleAtBay:
                    SetTimer("Dropoff arrived");
                    if (MoveTaxiTowardsParkingPoint(parkingFinalPosition, GetParkingMoveSpeed(0.62f, MinimumParkingSettleSpeed), true))
                    {
                        if (taxiRoot != null)
                        {
                            taxiRoot.position = parkingFinalPosition;
                            taxiRoot.rotation = parkingFinalRotation;
                            ConformTaxiToHighestParkingSurface("destination final", true);
                            parkingFinalPosition = taxiRoot.position;
                        }

                        ClearDepartureMergePlan();
                        ApplyCabinDestinationUiState();
                        CocoonDebugLog.Info("Destination", "Taxi completed destination pull-over at " + selectedPullOverPoint.name + " exactly at " + FormatPosition(parkingFinalPosition) + "; starting exit staging.", this);
                        BeginDestinationExitStaging();
                    }

                    break;
                default:
                    CocoonDebugLog.Warn("Destination", "Destination parking plan is unavailable; starting exit staging without a bay.", this);
                    ApplyCabinDestinationUiState();
                    BeginDestinationExitStaging();
                    break;
            }
        }

        private void BeginDestinationExitStaging()
        {
            taxiMotionSpeed = 0f;
            seatStoragePhase = CocoonSeatStoragePhase.Inactive;
            hasLoggedSeatStorageComplete = false;
            hasPlayedSeatOutLights = false;
            StartPassengerSeatStorageSequence();

            destinationExitPhase = CocoonDestinationExitPhase.SeatToOut;
            destinationExitTimer = 0f;
            destinationSeatRestoreStarted = false;
            CocoonDebugLog.Info("Destination", "Dropoff position reached; seat will move to out, rider will stand, luggage will exit, then door opens.", this);
        }

        private void RunDestinationSeatToOut()
        {
            if (seatStoragePhase == CocoonSeatStoragePhase.Inactive)
            {
                StartPassengerSeatStorageSequence();
            }

            if (!IsPassengerSeatStorageComplete())
            {
                return;
            }

            if (riderIsSeated || seatSitPhase == CocoonSeatSitPhase.Complete)
            {
                StandUpFromSeat();
            }

            if (riderHasLuggage && luggageStorageBagA1 != null)
            {
                StartPassengerLuggageExitSequence();
                destinationExitPhase = CocoonDestinationExitPhase.LuggageToA1;
                return;
            }

            BeginDestinationDoorOpening();
        }

        private void BeginDestinationDoorOpening()
        {
            destinationExitPhase = CocoonDestinationExitPhase.DoorOpening;
            destinationExitTimer = 0f;
            CocoonDebugLog.Info("Destination", "Destination exit staging complete; opening door and ramp.", this);
        }

        private void RunDestinationDoorOpening()
        {
            doorOpenAmount = Mathf.MoveTowards(doorOpenAmount, 1f, Time.deltaTime * GetBoardingDoorOpenSpeed());
            rampOpenAmount = Mathf.MoveTowards(rampOpenAmount, 1f, Time.deltaTime * GetBoardingDoorOpenSpeed());
            ApplyDoor(doorOpenAmount);
            ApplyLuggageRamp(rampOpenAmount);

            if (doorOpenAmount >= 0.995f && rampOpenAmount >= 0.995f)
            {
                DetachRiderForDestinationExit();
                destinationExitPhase = CocoonDestinationExitPhase.WaitingForExit;
                destinationExitTimer = Mathf.Max(0.1f, destinationExitDoorCloseDelay);
                CocoonDebugLog.Info("Destination", "Destination exit door open; waiting " + destinationExitTimer.ToString("0.0") + "s before closing.", this);
            }
        }

        private void RunDestinationExitWait()
        {
            doorOpenAmount = 1f;
            rampOpenAmount = 1f;
            ApplyDoor(1f);
            ApplyLuggageRamp(1f);
            destinationExitTimer -= Time.deltaTime;
            if (destinationExitTimer > 0f)
            {
                return;
            }

            destinationExitPhase = CocoonDestinationExitPhase.DoorClosing;
            CocoonDebugLog.Info("Destination", "Destination exit wait complete; closing door and ramp.", this);
        }

        private void RunDestinationDoorClosing()
        {
            doorOpenAmount = Mathf.MoveTowards(doorOpenAmount, 0f, Time.deltaTime * GetBoardingDoorCloseSpeed());
            rampOpenAmount = Mathf.MoveTowards(rampOpenAmount, 0f, Time.deltaTime * GetBoardingDoorCloseSpeed());
            ApplyDoor(doorOpenAmount);
            ApplyLuggageRamp(rampOpenAmount);

            if (doorOpenAmount > 0f || rampOpenAmount > 0f)
            {
                return;
            }

            DetachRiderForDestinationExit();
            destinationExitPhase = CocoonDestinationExitPhase.SeatRestore;
            destinationSeatRestoreStarted = false;
            CocoonDebugLog.Info("Destination", "Destination exit door closed; restoring seat before taxi departs.", this);
        }

        private void RunDestinationSeatRestore()
        {
            if (!destinationSeatRestoreStarted)
            {
                destinationSeatRestoreStarted = true;
                if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2))
                {
                    seatV2.StartRestoreToIdle();
                    CocoonDebugLog.Info("Destination", "Seat V2 restore-to-idle started after passenger exit.", this);
                }
                else
                {
                    RestoreSeat1StowedPose();
                    CocoonDebugLog.Info("Destination", "Legacy seat restored after passenger exit.", this);
                }
            }

            if (TryGetSeatV2Controller(out CocoonSeatV2Controller activeSeatV2) && !activeSeatV2.IsRestoreComplete)
            {
                return;
            }

            BeginDestinationBayExit();
        }

        private void BeginDestinationBayExit()
        {
            riderOnboard = false;
            passengerLuggageStowedInTaxi = false;
            ApplyCabinDefaultUiState();
            ClearDepartureMergePlan();

            destinationBayExitStep = 0;
            destinationBayExitLeadPosition = Vector3.zero;
            destinationBayExitMergeCursor = CocoonRouteCursor.Invalid();
            hasDestinationBayExitMergeCursor = false;
            lastLoggedDestinationBayExitStep = "";
            EnsureTrafficParticipant();
            if (trafficParticipant != null)
            {
                // The taxi is still physically in the bay; do not reserve/block the live lane
                // until it has actually moved to the graph merge point.
                trafficParticipant.SetBlocksTraffic(false);
                trafficParticipant.ReportSpeed(0f);
            }

            Vector3 exitForward = ResolveDestinationBayExitForward();
            float exitLeadDistance = Mathf.Max(GetScaledTaxiTrafficLength() * 0.85f, 1.35f * GetExperienceScale());
            destinationBayExitLeadPosition = taxiRoot != null
                ? taxiRoot.position + exitForward * exitLeadDistance
                : parkingFinalPosition + exitForward * exitLeadDistance;
            if (taxiRoot != null)
            {
                destinationBayExitLeadPosition.y = taxiRoot.position.y;
            }

            if (useRoadGraph && roadGraph != null && selectedGraphBayIndex >= 0)
            {
                CocoonRoadGraph.BayBinding bay = roadGraph.GetBay(selectedGraphBayIndex);
                if (bay != null && bay.EdgeId >= 0)
                {
                    CocoonRouteCursor mergeCursor = ResolveDestinationBayExitMergeCursor(bay, exitForward);
                    if (mergeCursor.IsValid)
                    {
                        destinationBayExitMergeCursor = mergeCursor;
                        hasDestinationBayExitMergeCursor = true;
                        departureMergePosition = roadGraph.GetPosition(mergeCursor);
                        departureMergePosition.y = taxiRoot != null ? taxiRoot.position.y : parkingFinalPosition.y;
                        Vector3 graphForward = roadGraph.GetForward(mergeCursor.EdgeId);
                        departureMergeRotation = Quaternion.LookRotation(graphForward.sqrMagnitude > 0.0001f ? graphForward : parkingApproachForward, Vector3.up);
                    }
                }
            }

            if (!hasDestinationBayExitMergeCursor)
            {
                taxiMotionSpeed = 0f;
                destinationExitPhase = CocoonDestinationExitPhase.DestinationBayExit;
                if (!hasLoggedDestinationDepartureProjectionFailure)
                {
                    hasLoggedDestinationDepartureProjectionFailure = true;
                    CocoonDebugLog.Warn("Destination", "Destination bay exit could not find a forward, unoccupied ROADMAP merge point; taxi is held instead of reversing or using a straight-line fallback.", this);
                }
                return;
            }

            destinationExitPhase = CocoonDestinationExitPhase.DestinationBayExit;
            CocoonDebugLog.Info("Destination", "Destination bay exit started: lead=" + FormatPosition(destinationBayExitLeadPosition) +
                ", merge=" + FormatPosition(departureMergePosition) +
                ", forward=" + FormatPosition(exitForward) +
                ", graphEdge=" + destinationBayExitMergeCursor.EdgeId +
                ", progress=" + destinationBayExitMergeCursor.Progress.ToString("0.00") + ".", this);
        }

        private CocoonRouteCursor ResolveDestinationBayExitMergeCursor(CocoonRoadGraph.BayBinding bay, Vector3 exitForward)
        {
            CocoonRouteCursor fallbackCursor = new CocoonRouteCursor { EdgeId = bay.EdgeId, Progress = bay.Progress };
            float experienceScale = GetExperienceScale();
            float baseDistance = DepartureMergeLeadDistanceMeters * experienceScale;
            float stepDistance = Mathf.Max(1.2f * experienceScale, GetScaledTaxiTrafficLength() * 0.45f);
            Vector3 exitStart = taxiRoot != null ? taxiRoot.position : parkingFinalPosition;
            Vector3 flatExitForward = FlattenDirection(exitForward, taxiRoot != null ? taxiRoot.forward : parkingApproachForward);
            float minimumForwardDistance = Mathf.Max(GetScaledTaxiTrafficLength() * 0.25f, 0.35f * experienceScale);
            CocoonRouteCursor bestCursor = fallbackCursor;
            bool hasBest = false;

            for (int i = 0; i < 7; i++)
            {
                CocoonRouteCursor candidate = new CocoonRouteCursor { EdgeId = bay.EdgeId, Progress = bay.Progress };
                roadGraph.TryAdvance(ref candidate, baseDistance + stepDistance * i, out _);
                Vector3 candidatePosition = roadGraph.GetPosition(candidate);
                candidatePosition.y = taxiRoot != null ? taxiRoot.position.y : parkingFinalPosition.y;
                Vector3 toCandidate = candidatePosition - exitStart;
                toCandidate.y = 0f;
                if (flatExitForward.sqrMagnitude > 0.0001f && Vector3.Dot(toCandidate, flatExitForward) < minimumForwardDistance)
                {
                    continue;
                }

                if (!hasBest)
                {
                    bestCursor = candidate;
                    hasBest = true;
                }

                if (!IsDestinationBayExitCorridorBlocked(candidatePosition, out _))
                {
                    return candidate;
                }
            }

            return hasBest ? bestCursor : CocoonRouteCursor.Invalid();
        }

        private bool IsDestinationBayExitCorridorBlocked(Vector3 targetPosition, out CocoonTrafficParticipant blocker)
        {
            blocker = null;
            if (taxiRoot == null)
            {
                return false;
            }

            float experienceScale = GetExperienceScale();
            float targetDistance = Vector3.Distance(taxiRoot.position, targetPosition);
            float maxLookAhead = Mathf.Max(GetScaledTaxiTrafficLength(), targetDistance + GetScaledTaxiTrafficLength() * 0.35f);
            return CocoonTrafficParticipant.HasHardBlockInPath(
                trafficParticipant,
                taxiRoot,
                targetPosition,
                GetScaledTaxiTrafficLength(),
                TaxiTrafficHardClearanceMeters * experienceScale,
                maxLookAhead,
                true,
                out blocker);
        }

        private void RunDestinationBayExit()
        {
            if (taxiRoot == null)
            {
                return;
            }

            if (!hasDestinationBayExitMergeCursor)
            {
                taxiMotionSpeed = 0f;
                if (!hasLoggedDestinationDepartureProjectionFailure)
                {
                    hasLoggedDestinationDepartureProjectionFailure = true;
                    CocoonDebugLog.Warn("Destination", "Destination bay exit has no legal ROADMAP merge cursor; taxi is held instead of using a straight-line fallback.", this);
                }
                return;
            }

            Vector3 target;
            float speed;
            bool finalLeg;
            string label;
            switch (destinationBayExitStep)
            {
                case 0:
                    target = destinationBayExitLeadPosition;
                    speed = GetParkingMoveSpeed(0.95f, MinimumParkingAlignSpeed);
                    finalLeg = false;
                    label = "forward-lead";
                    break;
                default:
                    target = departureMergePosition;
                    speed = GetPullOverMoveSpeed();
                    finalLeg = true;
                    label = "graph-merge";
                    break;
            }

            if (!string.Equals(lastLoggedDestinationBayExitStep, label, System.StringComparison.Ordinal))
            {
                lastLoggedDestinationBayExitStep = label;
                CocoonDebugLog.Info("Destination", "Destination bay exit phase " + label +
                    " target=" + FormatPosition(target) +
                    ", speed=" + GetDisplayMeters(speed).ToString("0.00") + "m/s.", this);
            }

            bool reached = MoveTaxiTowardsDestinationBayExitTarget(target, speed, true, finalLeg);
            ConformTaxiToHighestParkingSurface("destination bay exit", false);
            if (finalLeg)
            {
                taxiRoot.rotation = Quaternion.RotateTowards(taxiRoot.rotation, departureMergeRotation, 420f * Time.deltaTime);
            }

            float remaining = Vector3.Distance(taxiRoot.position, target);
            if (!reached && remaining > GetScaledArrivalDistance(finalLeg ? DepartureMergeArrivalDistance : ParkingFinalArrivalDistance))
            {
                return;
            }

            if (destinationBayExitStep < 1)
            {
                destinationBayExitStep++;
                return;
            }

            taxiRoot.position = departureMergePosition;
            taxiRoot.rotation = departureMergeRotation;
            taxiGraphCursor = destinationBayExitMergeCursor;
            cruiseTargetIndex = ResolveCruiseTargetIndexFromTaxiPosition();
            if (trafficParticipant != null && roadGraph != null && taxiGraphCursor.IsValid)
            {
                trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
                trafficParticipant.SetGraphCursor(taxiGraphCursor);
                trafficParticipant.SetBlocksTraffic(true);
            }

            CocoonDebugLog.Info("Destination", "Destination bay exit rejoined ROADMAP graph edge " +
                taxiGraphCursor.EdgeId + " at " + FormatPosition(taxiRoot.position) + ".", this);
            destinationDepartureGraphOnly = true;
            FinishDestinationExitAndDepart();
        }

        private void FinishDestinationExitAndDepart()
        {
            riderOnboard = false;
            passengerLuggageStowedInTaxi = false;
            ApplyCabinDefaultUiState();
            ClearDepartureMergePlan();
            hasLoggedDestinationDepartureGraphHold = false;
            destinationExitPhase = CocoonDestinationExitPhase.Inactive;
            destinationSeatRestoreStarted = false;
            destinationDropoffRequested = false;
            destinationStopCursor = CocoonRouteCursor.Invalid();
            hasDestinationStopCursor = false;
            destinationBayExitStep = 0;
            destinationBayExitLeadPosition = Vector3.zero;
            destinationBayExitMergeCursor = CocoonRouteCursor.Invalid();
            hasDestinationBayExitMergeCursor = false;
            lastLoggedDestinationBayExitStep = "";
            cruiseTargetIndex = ResolveCruiseTargetIndexFromTaxiPosition();
            if (trafficParticipant != null)
            {
                if (useRoadGraph && roadGraph != null && taxiGraphCursor.IsValid)
                {
                    trafficParticipant.ConfigureGraphIfNeeded(roadGraph, taxiRoot != null ? taxiRoot : transform, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), taxiGraphCursor);
                    trafficParticipant.SetGraphCursor(taxiGraphCursor);
                }
                else
                {
                    trafficParticipant.Configure(cruisePath, taxiRoot != null ? taxiRoot : transform, GetScaledTaxiTrafficLength(), GetScaledTaxiTrafficWidth(), cruiseTargetIndex);
                }
            }

            CocoonDebugLog.Info("Destination", "Seat restored and bay exit complete; taxi will depart on ROADMAP graph without straight-line fallback.", this);
            EnterState(CocoonTaxiState.Departing);
        }

        private void DetachRiderForDestinationExit()
        {
            if (destinationRiderDetached)
            {
                return;
            }

            destinationRiderDetached = true;
            destinationExitReadyForAReset = true;
            riderOnboard = false;
            riderIsSeated = false;
            seatSitPhase = CocoonSeatSitPhase.Inactive;
            SetRiderHeightLockEnabled(true);
            SetRiderLocomotionEnabled(true);
            DetachRiderFromTaxi();
            CocoonDebugLog.Info("Destination", "Rider detached from taxi for destination exit; locomotion and standing height restored.", this);
        }

        private void StandUpFromSeat()
        {
            if (!riderIsSeated && seatSitPhase != CocoonSeatSitPhase.Complete)
            {
                return;
            }

            riderIsSeated = false;
            seatSitPhase = CocoonSeatSitPhase.Inactive;
            seatSitUsingSeatV2Controller = false;
            seatSitV2BackMovementStarted = false;
            sitPromptActive = false;
            sitPromptWasPressed = false;
            SetSitPromptPanelActive(false);
            SetRiderHeightLockEnabled(true);
            SetRiderLocomotionEnabled(true);
            hasSeatedHeadAnchorLocalPose = false;
            hasSeatSitMoveStartWorldOffset = false;
            seatSitMoveStartRigToHeadWorldOffset = Vector3.zero;
            seatSitMoveStartRigToHeadTaxiLocalOffset = Vector3.zero;
            seatSitMoveStartRigWorldYawRotation = Quaternion.identity;
            seatSitMoveStartRigTaxiLocalYawRotation = Quaternion.identity;
            seatSitMoveStartUsesTaxiRigReference = false;
            seatSitRotatePendingContinuousEntry = false;
            hasSeatSitRotatePreviousAnchorRotation = false;
            seatSitRotatePreviousAnchorRotation = Quaternion.identity;
            hasSeatSitMoveFinalPose = false;
            seatSitMoveFinalHeadPosition = Vector3.zero;
            seatSitMoveFinalHeadRotation = Quaternion.identity;
            seatSitMoveFinalRigPosition = Vector3.zero;
            seatSitMoveFinalRigRotation = Quaternion.identity;
            hasLoggedSeatSitSafetyFallback = false;
            hasLoggedSeatMoveCompleteWithoutSnap = false;
            hasLoggedSeatRotateContinuousEntry = false;
            hasLoggedSeatRotateAnchorFollow = false;
            hasLoggedSeatSitStart = false;
            hasLoggedSeatSitComplete = false;
            hasLoggedSeatedTurn = false;
            if (!hasLoggedSeatStandUp)
            {
                hasLoggedSeatStandUp = true;
                CocoonDebugLog.Info("Seat", "Rider stood up from seated mode; height lock and locomotion restored. Press A again to sit at the same head anchor.", this);
            }
        }

        private bool AlignRiderHeadToSeatedAnchor(bool logResult)
        {
            if (seatedRiderHeadAnchor == null)
            {
                CocoonDebugLog.Warn("Seat", "Cannot position rider for seated pose. anchor=False.", this);
                return false;
            }

            return AlignRiderHeadToWorldPose(
                ResolveSeatedHeadAnchorWorldPosition(),
                ResolveSeatedHeadCameraWorldRotation(),
                logResult,
                GetTransformPath(seatedRiderHeadAnchor));
        }

        private bool AlignRiderHeadPositionToSeatedAnchor(bool logResult)
        {
            if (seatedRiderHeadAnchor == null)
            {
                CocoonDebugLog.Warn("Seat", "Cannot position rider for seated pose. anchor=False.", this);
                return false;
            }

            return AlignRiderHeadPositionToWorldPoint(
                ResolveSeatedHeadAnchorWorldPosition(),
                logResult,
                GetTransformPath(seatedRiderHeadAnchor));
        }

        private bool AlignRiderHeadPositionToWorldPoint(Vector3 targetPosition, bool logResult, string logTargetName)
        {
            Transform head = ResolveRiderHeadTransform();
            Transform rig = ResolveRiderRootTransform();
            if (head == null || rig == null)
            {
                CocoonDebugLog.Warn("Seat", "Cannot position rider for seated pose. head=" + (head != null) + ", rig=" + (rig != null) + ".", this);
                return false;
            }

            CharacterController controller = rig.GetComponent<CharacterController>();
            bool restoreController = controller != null && controller.enabled;
            if (restoreController)
            {
                controller.enabled = false;
            }

            Vector3 headOffset = head.position - rig.position;
            rig.position = targetPosition - headOffset;

            if (restoreController)
            {
                controller.enabled = true;
            }

            if (logResult)
            {
                CocoonDebugLog.Info("Seat", "Rider head position matched " + (string.IsNullOrEmpty(logTargetName) ? "seated target" : logTargetName) + " without overriding HMD rotation.", this);
            }

            return true;
        }

        private void RotateRiderRigAroundCurrentHead(Vector3 axis, float degrees)
        {
            Transform head = ResolveRiderHeadTransform();
            Transform rig = ResolveRiderRootTransform();
            if (head == null || rig == null || Mathf.Abs(degrees) <= 0.0001f || axis.sqrMagnitude < 0.0001f)
            {
                return;
            }

            CharacterController controller = rig.GetComponent<CharacterController>();
            bool restoreController = controller != null && controller.enabled;
            if (restoreController)
            {
                controller.enabled = false;
            }

            rig.RotateAround(head.position, axis.normalized, degrees);

            if (restoreController)
            {
                controller.enabled = true;
            }
        }

        private bool AlignRiderHeadToWorldPose(Vector3 targetPosition, Quaternion targetRotation, bool logResult, string logTargetName)
        {
            Transform head = ResolveRiderHeadTransform();
            Transform rig = ResolveRiderRootTransform();
            if (head == null || rig == null)
            {
                CocoonDebugLog.Warn("Seat", "Cannot position rider for seated pose. head=" + (head != null) + ", rig=" + (rig != null) + ".", this);
                return false;
            }

            CharacterController controller = rig.GetComponent<CharacterController>();
            bool restoreController = controller != null && controller.enabled;
            if (restoreController)
            {
                controller.enabled = false;
            }

            Vector3 upAxis = ResolveSeatedAlignmentUp(targetRotation);
            Vector3 currentForward = Vector3.ProjectOnPlane(head.forward, upAxis);
            Vector3 targetForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, upAxis);
            if (currentForward.sqrMagnitude > 0.0001f && targetForward.sqrMagnitude > 0.0001f)
            {
                float yawDelta = Vector3.SignedAngle(currentForward.normalized, targetForward.normalized, upAxis);
                rig.RotateAround(head.position, upAxis, yawDelta);
            }

            Vector3 headOffset = head.position - rig.position;
            rig.position = targetPosition - headOffset;

            if (restoreController)
            {
                controller.enabled = true;
            }

            if (logResult)
            {
                CocoonDebugLog.Info("Seat", "Rider rig positioned so camera matches " + (string.IsNullOrEmpty(logTargetName) ? "seated target" : logTargetName) + ".", this);
            }

            return true;
        }

        private bool FollowRiderHeadPositionToSeatedAnchorFromCurrentPose()
        {
            Transform head = ResolveRiderHeadTransform();
            Transform rig = ResolveRiderRootTransform();
            if (head == null || rig == null)
            {
                return false;
            }

            CharacterController controller = rig.GetComponent<CharacterController>();
            bool restoreController = controller != null && controller.enabled;
            if (restoreController)
            {
                controller.enabled = false;
            }

            Vector3 headOffset = head.position - rig.position;
            rig.position = ResolveSeatedHeadAnchorWorldPosition() - headOffset;

            if (restoreController)
            {
                controller.enabled = true;
            }

            return true;
        }

        private Vector3 ResolveSeatedHeadAnchorWorldPosition()
        {
            if (seatedRiderHeadAnchor != null)
            {
                return seatedRiderHeadAnchor.position;
            }

            return hasSeatedHeadAnchorLocalPose && seat1 != null
                ? seat1.TransformPoint(seatedHeadAnchorLocalPosition)
                : (seat1 != null ? seat1.position : transform.position);
        }

        private Quaternion ResolveSeatedHeadCameraWorldRotation()
        {
            Quaternion anchorWorldRotation = seatedRiderHeadAnchor != null
                ? seatedRiderHeadAnchor.rotation
                : (hasSeatedHeadAnchorLocalPose && seat1 != null
                    ? seat1.rotation * seatedHeadAnchorLocalRotation
                    : (seat1 != null ? seat1.rotation : transform.rotation));
            Vector3 forward = seatedRiderHeadAnchor != null
                ? seatedRiderHeadAnchor.TransformVector(ResolveSeatedHeadCameraForwardLocalAxis())
                : anchorWorldRotation * ResolveSeatedHeadCameraForwardLocalAxis();
            Vector3 up = ResolveSeatedStableUp();
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = seat1 != null ? seat1.forward : Vector3.forward;
            }

            forward = MaybeFlipSeatedHeadForwardTowardCabin(forward, up);
            forward = Vector3.ProjectOnPlane(forward, up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = taxiRoot != null ? taxiRoot.forward : transform.forward;
                forward = Vector3.ProjectOnPlane(forward, up);
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        private Vector3 MaybeFlipSeatedHeadForwardTowardCabin(Vector3 forward, Vector3 up)
        {
            if (seatedRiderHeadAnchor == null ||
                !TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) ||
                seatV2.SeatedRiderHeadAnchor != seatedRiderHeadAnchor)
            {
                return forward;
            }

            Vector3 flatForward = Vector3.ProjectOnPlane(forward, up);
            Vector3 cabinReference = ResolveSeatedCabinFacingReferencePoint();
            Vector3 toCabin = Vector3.ProjectOnPlane(cabinReference - seatedRiderHeadAnchor.position, up);
            if (flatForward.sqrMagnitude < 0.0001f || toCabin.sqrMagnitude < 0.0001f)
            {
                return forward;
            }

            return Vector3.Dot(flatForward.normalized, toCabin.normalized) < -0.05f ? -forward : forward;
        }

        private Vector3 ResolveSeatedCabinFacingReferencePoint()
        {
            Transform interior = ResolveFinalTaxiInteriorStructure();
            if (interior != null && TryGetRendererBounds(interior, out Bounds interiorBounds))
            {
                return interiorBounds.center;
            }

            if (taxiRoot != null && TryGetRendererBounds(taxiRoot, out Bounds taxiBounds))
            {
                return taxiBounds.center;
            }

            return taxiRoot != null ? taxiRoot.position : transform.position;
        }

        private Vector3 ResolveSeatedHeadCameraForwardLocalAxis()
        {
            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) &&
                seatV2.SeatedRiderHeadAnchor != null &&
                seatedRiderHeadAnchor == seatV2.SeatedRiderHeadAnchor)
            {
                return seatV2.SeatedHeadCameraForwardLocalAxis;
            }

            return Vector3.right;
        }

        private string GetSeatedHeadCameraForwardAxisLabel()
        {
            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) &&
                seatV2.SeatedRiderHeadAnchor != null &&
                seatedRiderHeadAnchor == seatV2.SeatedRiderHeadAnchor)
            {
                return seatV2.SeatedHeadCameraForwardAxisLabel;
            }

            return "local +X";
        }

        private Vector3 ResolveSeatedAlignmentUp(Quaternion targetRotation)
        {
            return ResolveSeatedStableUp();
        }

        private Vector3 ResolveSeatedStableUp()
        {
            Vector3 up = taxiRoot != null ? taxiRoot.up : Vector3.zero;
            if (up.sqrMagnitude < 0.0001f && seat1 != null && seat1.parent != null)
            {
                up = seat1.parent.up;
            }

            if (up.sqrMagnitude < 0.0001f)
            {
                up = transform.up;
            }

            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.up;
            }

            return up.normalized;
        }

        private void SetRiderLocomotionEnabled(bool enabled)
        {
            Transform rig = ResolveRiderRootTransform();
            CocoonVRLocomotion locomotion = rig != null ? rig.GetComponent<CocoonVRLocomotion>() : null;
            if (locomotion == null && rig != null)
            {
                locomotion = rig.GetComponentInChildren<CocoonVRLocomotion>(true);
            }

            if (locomotion != null)
            {
                locomotion.SetMovementInputEnabled(enabled);
            }
        }

        private void SetRiderHeightLockEnabled(bool enabled)
        {
            Transform rig = ResolveRiderRootTransform();
            CocoonRiderHeightCalibrator calibrator = rig != null ? rig.GetComponent<CocoonRiderHeightCalibrator>() : null;
            if (calibrator == null && rig != null)
            {
                calibrator = rig.GetComponentInChildren<CocoonRiderHeightCalibrator>(true);
            }

            if (calibrator != null)
            {
                calibrator.SetHeightLockEnabled(enabled);
            }
        }

        private void EnsureRiderComfortVisibility()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.nearClipPlane > RiderCameraNearClipPlane + 0.0001f)
            {
                mainCamera.nearClipPlane = RiderCameraNearClipPlane;
            }

            Transform rig = ResolveRiderRootTransform();
            CharacterController controller = rig != null ? rig.GetComponent<CharacterController>() : null;
            if (controller != null && controller.radius > RiderControllerRadiusMeters + 0.0001f)
            {
                controller.radius = RiderControllerRadiusMeters;
            }

            CocoonRiderHeightCalibrator calibrator = rig != null ? rig.GetComponent<CocoonRiderHeightCalibrator>() : null;
            if (calibrator == null && rig != null)
            {
                calibrator = rig.GetComponentInChildren<CocoonRiderHeightCalibrator>(true);
            }

            if (calibrator != null)
            {
                calibrator.SetControllerRadiusMeters(RiderControllerRadiusMeters);
            }

            ConfigureHandProxy(ResolveLeftHandTransform(), LeftHandProxyScale);
            ConfigureHandProxy(ResolveRightHandTransform(), RightHandProxyScale);

            if (!hasLoggedRiderComfortVisibility)
            {
                hasLoggedRiderComfortVisibility = true;
                CocoonDebugLog.Info(
                    "XR",
                    "Camera near clip set to " + RiderCameraNearClipPlane.ToString("0.###") +
                    "m; controller radius=" + RiderControllerRadiusMeters.ToString("0.###") +
                    "m; using original small blue controller proxy spheres.",
                    this);
            }
        }

        private Transform ResolveRightHandTransform()
        {
            if (rightHand != null)
            {
                return rightHand;
            }

            Transform root = ResolveRiderRootTransform();
            if (root != null)
            {
                rightHand = FindNamedDescendant(root, "RightHand") ??
                            FindNamedDescendant(root, "Right Hand") ??
                            FindNamedDescendant(root, "Right Controller") ??
                            FindNamedDescendant(root, "RightHand Controller");
                if (rightHand != null)
                {
                    return rightHand;
                }
            }

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                string name = candidate.name;
                if (name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (name.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    rightHand = candidate;
                    return rightHand;
                }
            }

            return null;
        }

        private void ConfigureHandProxy(Transform hand, float targetScale)
        {
            if (hand == null)
            {
                return;
            }

            hand.localScale = Vector3.one * targetScale;
            Renderer[] renderers = hand.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ConfigureHandProxyMaterial(renderers[i]);
            }
        }

        private void ConfigureHandProxyMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = renderer.material;
            if (material == null)
            {
                return;
            }

            renderer.enabled = true;
            Color color = new Color(0.08f, 0.28f, 1f, HandProxyAlpha);
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 1f);
            }

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        private bool ResolveSeatSitReferences(bool warnIfMissing = true)
        {
            Transform searchRoot = taxiRoot != null ? taxiRoot : transform;
            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2) && seatV2.HasSitReferences)
            {
                seat1 = seatV2.SeatTransform;
                seat1SitTarget = seatV2.EffectiveSitTarget;
                seatedRiderHeadAnchor = seatV2.SeatedRiderHeadAnchor;
                if (seatV2.EnableSeatSitRotationToMarker)
                {
                    ro1 = ResolveExactNamedReference(searchRoot, ro1, Ro1Name);
                    ro2 = ResolveExactNamedReference(searchRoot, ro2, Ro2Name);
                    ro3 = ResolveExactNamedReference(searchRoot, ro3, Ro3Name);
                    CaptureRoFoldStowedPoseIfNeeded();
                }
                else
                {
                    ro1 = null;
                    ro2 = null;
                    ro3 = null;
                }

                if (!hasLoggedSeatSitBinding)
                {
                    hasLoggedSeatSitBinding = true;
                    CocoonDebugLog.Info(
                        "Seat",
                        "Seat sit markers bound to Seat V2: body=" + GetTransformPath(seat1) +
                        ", sitTarget=" + GetTransformPath(seat1SitTarget) +
                        ", headAnchor=" + GetTransformPath(seatedRiderHeadAnchor) +
                        ", seatRotationEnabled=" + seatV2.EnableSeatSitRotationToMarker +
                        ", seatedStickRotationEnabled=" + seatV2.EnableSeatedStickSeatRotation + ".",
                        this);
                }

                HideSeatSitMarkers();
                return true;
            }

            seat1 = ResolveExactNamedReference(searchRoot, seat1, Seat1Name);
            seat1SitTarget = ResolvePreferredSeatMarkerReference(searchRoot, seat1SitTarget, Seat1SitTargetNames);
            seatedRiderHeadAnchor = ResolveExactNamedReference(searchRoot, seatedRiderHeadAnchor, SeatedRiderHeadAnchorName);
            ro1 = ResolveExactNamedReference(searchRoot, ro1, Ro1Name);
            ro2 = ResolveExactNamedReference(searchRoot, ro2, Ro2Name);
            ro3 = ResolveExactNamedReference(searchRoot, ro3, Ro3Name);
            CaptureRoFoldStowedPoseIfNeeded();

            bool hasCoreMarkers = seat1 != null && seat1SitTarget != null && seatedRiderHeadAnchor != null;
            if (hasCoreMarkers)
            {
                if (!hasLoggedSeatSitBinding)
                {
                    hasLoggedSeatSitBinding = true;
                    CocoonDebugLog.Info(
                        "Seat",
                        "Seat sit markers bound: seat1=" + GetTransformPath(seat1) +
                        ", 1x_=" + GetTransformPath(seat1SitTarget) +
                        ", headAnchor=" + GetTransformPath(seatedRiderHeadAnchor) +
                        ", ro1=" + GetTransformPath(ro1) +
                        ", ro2=" + GetTransformPath(ro2) +
                        ", ro3=" + GetTransformPath(ro3) + ".",
                        this);
                }

                HideSeatSitMarkers();
                return true;
            }

            if (warnIfMissing && !hasLoggedSeatSitMissingMarkers)
            {
                hasLoggedSeatSitMissingMarkers = true;
                CocoonDebugLog.Warn(
                    "Seat",
                    "Seat sit markers missing. seat1=" + (seat1 != null) +
                    ", 1x_=" + (seat1SitTarget != null) +
                    ", headAnchor=" + (seatedRiderHeadAnchor != null) +
                    ". Sit-down flow skipped.",
                    this);
            }

            return false;
        }

        private void HideSeatSitMarkers()
        {
            SetTransformRenderersEnabled(seat1SitTarget, false);
            SetTransformRenderersEnabled(seatedRiderHeadAnchor, false);
        }

        private void CaptureRoFoldStowedPoseIfNeeded()
        {
            if (hasAuthoredRoFoldStowedPose)
            {
                return;
            }

            roFoldTransforms[0] = ro1;
            roFoldTransforms[1] = ro2;
            roFoldTransforms[2] = ro3;
            for (int i = 0; i < roFoldTransforms.Length; i++)
            {
                Transform target = roFoldTransforms[i];
                if (target == null)
                {
                    continue;
                }

                roFoldStowedLocalPositions[i] = target.localPosition;
                roFoldStowedLocalRotations[i] = target.localRotation;
                roFoldStowedLocalScales[i] = target.localScale;
            }

            hasAuthoredRoFoldStowedPose = true;
        }

        private void RestoreRoFoldStowedPose()
        {
            if (!hasAuthoredRoFoldStowedPose)
            {
                return;
            }

            roFoldTransforms[0] = ro1;
            roFoldTransforms[1] = ro2;
            roFoldTransforms[2] = ro3;
            for (int i = 0; i < roFoldTransforms.Length; i++)
            {
                Transform target = roFoldTransforms[i];
                if (target == null)
                {
                    continue;
                }

                target.localPosition = roFoldStowedLocalPositions[i];
                target.localRotation = roFoldStowedLocalRotations[i];
                target.localScale = roFoldStowedLocalScales[i];
            }
        }

        private void ResetSeatDownSequence()
        {
            seatSitPhase = CocoonSeatSitPhase.Inactive;
            seatSitTimer = 0f;
            sitPromptWasShown = false;
            sitPromptActive = false;
            sitPromptWasPressed = false;
            seatToggleButtonWasDown = false;
            riderIsSeated = false;
            seatSitUsingSeatV2Controller = false;
            seatSitV2BackMovementStarted = false;
            roFolded = false;
            sitPromptTimer = 0f;
            seatSitHasRendererPivot = false;
            seatSitPivotLocalPoint = Vector3.zero;
            seatSitPivotParentLocalPosition = Vector3.zero;
            seatSitMoveStartHeadPosition = Vector3.zero;
            seatSitMoveStartHeadRotation = Quaternion.identity;
            seatSitMoveStartHeadTaxiLocalPosition = Vector3.zero;
            seatSitMoveStartHeadTaxiLocalRotation = Quaternion.identity;
            seatSitMoveStartUsesTaxiReference = false;
            seatSitMoveStartRigToHeadWorldOffset = Vector3.zero;
            seatSitMoveStartRigToHeadTaxiLocalOffset = Vector3.zero;
            seatSitMoveStartRigWorldYawRotation = Quaternion.identity;
            seatSitMoveStartRigTaxiLocalYawRotation = Quaternion.identity;
            seatSitMoveStartUsesTaxiRigReference = false;
            hasSeatSitMoveStartWorldOffset = false;
            seatSitRotatePendingContinuousEntry = false;
            hasSeatSitRotatePreviousAnchorRotation = false;
            seatSitRotatePreviousAnchorRotation = Quaternion.identity;
            hasSeatSitMoveFinalPose = false;
            seatSitMoveFinalHeadPosition = Vector3.zero;
            seatSitMoveFinalHeadRotation = Quaternion.identity;
            seatSitMoveFinalRigPosition = Vector3.zero;
            seatSitMoveFinalRigRotation = Quaternion.identity;
            hasLoggedSeatSitSafetyFallback = false;
            hasSeatedHeadAnchorLocalPose = false;
            seatedHeadAnchorLocalPosition = Vector3.zero;
            seatedHeadAnchorLocalRotation = Quaternion.identity;
            hasLoggedSeatSitBinding = false;
            hasLoggedSeatSitMissingMarkers = false;
            hasLoggedSeatSitPrompt = false;
            hasLoggedSeatMoveStart = false;
            hasLoggedSeatMoveCompleteWithoutSnap = false;
            hasLoggedSeatRotateContinuousEntry = false;
            hasLoggedSeatRotateAnchorFollow = false;
            hasLoggedSeatSitStart = false;
            hasLoggedRoFoldStart = false;
            hasLoggedSeatSitComplete = false;
            hasLoggedSeatStandUp = false;
            hasLoggedSeatedTurn = false;
            ResolveSeatSitReferences(false);
            RestoreRoFoldStowedPose();
            HideSeatSitMarkers();
            SetSitPromptPanelActive(false);
            SetRiderHeightLockEnabled(true);
            SetRiderLocomotionEnabled(true);
        }

        private void ResetPassengerSeatStorageSequence()
        {
            seatStoragePhase = CocoonSeatStoragePhase.Inactive;
            seatStorageTimer = 0f;
            seatStorageUsingSeatV2 = false;
            hasLoggedSeatStorageBinding = false;
            hasLoggedSeatStorageMissingMarkers = false;
            hasLoggedSeatStorageMissingPivot = false;
            hasLoggedSeatStorageRotateStart = false;
            hasLoggedSeatStorageMoveStart = false;
            hasLoggedSeatStorageComplete = false;
            seatStorageHasRendererPivot = false;
            seatStoragePivotLocalPoint = Vector3.zero;
            seatStoragePivotWorldPosition = Vector3.zero;
            ResolveSeatStorageReferences(false);
            if (TryGetSeatV2Controller(out CocoonSeatV2Controller seatV2))
            {
                seatV2.ResetRuntimeState();
            }
            else
            {
                RestoreSeat1StowedPose();
            }
            HideSeatStorageMarkers();
            BindMiniScreenSeatFollow();
            SyncMiniScreenSeatFollow();
        }

        private void ResetPassengerLuggageStorageSequence()
        {
            passengerLuggageStoragePhase = CocoonLuggageStoragePhase.Inactive;
            passengerLuggageStorageTimer = 0f;
            hasLoggedLuggageStorageA1 = false;
            hasLoggedLuggageStorageA2 = false;
            hasLoggedLuggageStorageA3 = false;
            hasLoggedLuggagePreventerRaised = false;
            hasLoggedLuggageStorageWaitingForMarkers = false;
            hasLoggedLuggageStorageWaitingForSeat = false;
            hasLoggedLuggageStorageBinding = false;
            hasLoggedLuggageStorageMissingMarkers = false;
            luggageStorageWaypointTargets.Clear();
            luggageStorageWaypointDurations.Clear();
            luggageExitPoseTargets.Clear();
            luggageStorageWaypointIndex = 0;
            luggageExitPoseIndex = 0;
            ResolveLuggageStorageBaseReferences(false);
            ResolveLuggageStoragePathReferences(false);
            RestoreLuggageStorageBagA1AuthoredPose();
            HideLuggageStorageMarkers();
            RestoreLuggagePreventerStowedPose();
            ResetPassengerSeatStorageSequence();
        }

        private bool IsPassengerLuggageStorageComplete()
        {
            return !riderHasLuggage ||
                   passengerLuggageStoragePhase == CocoonLuggageStoragePhase.Inactive ||
                   passengerLuggageStoragePhase == CocoonLuggageStoragePhase.Complete;
        }

        private bool IsPassengerSeatStorageComplete()
        {
            return seatStoragePhase == CocoonSeatStoragePhase.Inactive ||
                   seatStoragePhase == CocoonSeatStoragePhase.Complete;
        }

        private static Vector3 GetWorldScale(Transform target)
        {
            return target != null ? target.lossyScale : Vector3.one;
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
                SafeScaleComponent(worldScale.x, parentScale.x),
                SafeScaleComponent(worldScale.y, parentScale.y),
                SafeScaleComponent(worldScale.z, parentScale.z));
        }

        private static float SafeScaleComponent(float worldScale, float parentScale)
        {
            return Mathf.Abs(parentScale) > 0.00001f ? worldScale / parentScale : worldScale;
        }

        private static void SetTransformRenderersEnabled(Transform root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled != enabled)
                {
                    renderers[i].enabled = enabled;
                }
            }
        }

        private bool TryGetLuggageCabinSupportBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            Transform interior = ResolveFinalTaxiInteriorStructure();
            if (interior == null)
            {
                return false;
            }

            if (TryGetSolidTaggedCabinSupportBounds(interior, out bounds))
            {
                return true;
            }

            Transform supportA = FindExactNamedDescendant(interior, LuggageCabinSupportAName) ??
                                 FindExactNamedDescendant(interior, LuggageCabinSupportFallbackAName);
            Transform supportB = FindExactNamedDescendant(interior, LuggageCabinSupportBName) ??
                                 FindExactNamedDescendant(interior, LuggageCabinSupportFallbackBName);

            if (!TryGetRendererBounds(supportA, out Bounds supportABounds) ||
                !TryGetRendererBounds(supportB, out Bounds supportBBounds))
            {
                return false;
            }

            bounds = supportABounds;
            bounds.Encapsulate(supportBBounds);
            return true;
        }

        private bool TryGetSolidTaggedCabinSupportBounds(Transform interior, out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;
            if (interior == null)
            {
                return false;
            }

            Transform[] transforms = interior.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.gameObject.tag != LuggageCabinSupportTag)
                {
                    continue;
                }

                EncapsulateRendererBounds(candidate, ref bounds, ref hasBounds);
            }

            if (hasBounds && !hasLoggedSolidCabinSupports)
            {
                hasLoggedSolidCabinSupports = true;
                CocoonDebugLog.Info(
                    "Luggage",
                    "Cabin luggage support resolved from solid tag. center=" + FormatPosition(bounds.center) +
                    ", topY=" + bounds.max.y.ToString("0.###") + ".",
                    this);
            }

            return hasBounds;
        }

        private Transform ResolveFinalTaxiInteriorStructure()
        {
            if (taxiRoot == null)
            {
                return null;
            }

            Transform visuals = FindExactNamedDescendant(taxiRoot, TaxiPodVisualsName);
            Transform interior = visuals != null ? FindExactNamedDescendant(visuals, FinalTaxiInteriorStructureName) : null;
            return interior != null ? interior : FindExactNamedDescendant(taxiRoot, FinalTaxiInteriorStructureName);
        }

        private void ResolveBaggageReferences()
        {
            if (baggageQuestionPanel != null &&
                boardingCardPromptPanel != null &&
                sitPromptPanel != null &&
                passengerLuggage != null)
            {
                return;
            }

            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (baggageQuestionPanel == null && candidate.name == "Baggage Question Panel")
                {
                    baggageQuestionPanel = candidate.gameObject;
                }
                else if (boardingCardPromptPanel == null && candidate.name == BoardingCardPromptPanelName)
                {
                    boardingCardPromptPanel = candidate.gameObject;
                }
                else if (sitPromptPanel == null && candidate.name == SitPromptPanelName)
                {
                    sitPromptPanel = candidate.gameObject;
                }
                else if (passengerLuggage == null && candidate.name == "Passenger Luggage Suitcase")
                {
                    passengerLuggage = candidate.gameObject;
                }
            }
        }

        private void SetGuidanceActive(bool active)
        {
            if (guidanceRoot != null && guidanceRoot.activeSelf != active)
            {
                guidanceRoot.SetActive(active);
                CocoonDebugLog.Info("Guidance", "Pickup guidance " + (active ? "shown" : "hidden") + ".", this);
            }
        }

        private void SetPickupProjectionActive(bool active)
        {
            if (safePickupZone != null)
            {
                safePickupZone.SetZoneActive(active);
            }
        }

        private void HidePickupBayVisuals()
        {
            HidePickupBayVisuals(pullOverPoint);
            if (pullOverPoints == null)
            {
                return;
            }

            for (int i = 0; i < pullOverPoints.Length; i++)
            {
                HidePickupBayVisuals(pullOverPoints[i]);
            }
        }

        private static void HidePickupBayVisuals(Transform bayReference)
        {
            Transform bayRoot = ResolvePickupBayVisualRoot(bayReference);
            if (bayRoot == null)
            {
                return;
            }

            Renderer[] renderers = bayRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private static Transform ResolvePickupBayVisualRoot(Transform bayReference)
        {
            if (bayReference == null)
            {
                return null;
            }

            if (bayReference.name == "Pickup Bay Stop" && bayReference.parent != null)
            {
                return bayReference.parent;
            }

            Transform explicitStop = bayReference.Find("Pickup Bay Stop");
            if (explicitStop != null)
            {
                return bayReference;
            }

            return bayReference.name.IndexOf("Pickup Bay", System.StringComparison.OrdinalIgnoreCase) >= 0 ? bayReference : null;
        }

        private void SetPaymentReaderActive(bool active)
        {
            if (paymentReader != null && paymentReader.gameObject.activeSelf != active)
            {
                paymentReader.gameObject.SetActive(active);
                CocoonDebugLog.Info("Payment", "Payment reader " + (active ? "enabled" : "disabled") + ".", this);
            }
        }

        private bool IsRiderInTaxiCabinWithLog()
        {
            bool inside = IsRiderInTaxiCabin();
            if (!hasLoggedRiderCabinInside || inside != lastLoggedRiderCabinInside)
            {
                CocoonDebugLog.Info("Door", "Cabin occupancy inside=" + inside + ".", this);
                hasLoggedRiderCabinInside = true;
                lastLoggedRiderCabinInside = inside;
            }

            return inside;
        }

        private bool IsRiderInTaxiCabin()
        {
            Transform head = ResolveRiderHeadTransform();
            if (head == null || taxiRoot == null)
            {
                return false;
            }

            Transform rig = ResolveRiderRootTransform();
            if (!IsRiderPointInTaxiCabin(head.position))
            {
                return false;
            }

            if (rig != null && rig != head && !IsRiderPointInTaxiCabin(rig.position))
            {
                return false;
            }

            if (rightHand != null && !IsRiderPointInTaxiCabin(rightHand.position))
            {
                return false;
            }

            Transform resolvedLeftHand = ResolveLeftHandTransform();
            return resolvedLeftHand == null || IsRiderPointInTaxiCabin(resolvedLeftHand.position);
        }

        private bool IsRiderPointInTaxiCabin(Vector3 worldPosition)
        {
            if (taxiRoot == null)
            {
                return false;
            }

            float experienceScale = GetExperienceScale();
            Vector3 flatPosition = worldPosition;
            flatPosition.y = taxiRoot.position.y;

            Vector3 cabinCenter = taxiRoot.position;
            bool passedDoorPlane = true;
            if (HasBoardingDoorGeometry())
            {
                CocoonTaxiDoorSide side = hasSelectedSlidingDoorSide ? activeSlidingDoorSide : CocoonTaxiDoorSide.A;
                Vector3 doorCenter = GetSlidingDoorSideVisualCenter(side);
                Vector3 outward = GetStableSlidingDoorOutwardDirection(side);
                Vector3 flatDoorCenter = doorCenter;
                flatDoorCenter.y = flatPosition.y;
                float inwardDepth = Vector3.Dot(flatPosition - flatDoorCenter, -outward);
                passedDoorPlane = inwardDepth >= Mathf.Max(0.01f, boardingCabinEntryDepth * experienceScale);
                cabinCenter = doorCenter - outward * (0.72f * experienceScale);
            }

            if (!passedDoorPlane)
            {
                return false;
            }

            cabinCenter.y = taxiRoot.position.y;
            float nearDoorCabinRadius = Mathf.Max(0.04f, onboardInteriorRadius * experienceScale);
            if (Vector3.Distance(flatPosition, cabinCenter) <= nearDoorCabinRadius)
            {
                return true;
            }

            float centerCabinRadius = Mathf.Max(0.055f, onboardInteriorRadius * 1.25f * experienceScale);
            Vector3 flatTaxi = taxiRoot.position;
            flatTaxi.y = flatPosition.y;
            return Vector3.Distance(flatPosition, flatTaxi) <= centerCabinRadius;
        }

        private string BuildRiderCabinDiagnostic()
        {
            if (taxiRoot == null)
            {
                return "taxiRoot missing.";
            }

            Transform head = ResolveRiderHeadTransform();
            Transform rig = ResolveRiderRootTransform();
            if (head == null)
            {
                return "rider head missing.";
            }

            float experienceScale = GetExperienceScale();
            CocoonTaxiDoorSide side = hasSelectedSlidingDoorSide ? activeSlidingDoorSide : CocoonTaxiDoorSide.A;
            Vector3 doorCenter = HasBoardingDoorGeometry() ? GetSlidingDoorSideVisualCenter(side) : taxiRoot.position;
            Vector3 outward = HasBoardingDoorGeometry() ? GetStableSlidingDoorOutwardDirection(side) : taxiRoot.forward;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
            Vector3 cabinCenter = doorCenter - outward * (0.72f * experienceScale);
            float requiredDepth = Mathf.Max(0.01f, boardingCabinEntryDepth * experienceScale);
            float nearRadius = Mathf.Max(0.04f, onboardInteriorRadius * experienceScale);
            float centerRadius = Mathf.Max(0.055f, onboardInteriorRadius * 1.25f * experienceScale);

            string headInfo = FormatCabinPointDiagnostic("head", head.position, doorCenter, cabinCenter, outward, requiredDepth, nearRadius, centerRadius);
            string rigInfo = rig != null && rig != head
                ? " " + FormatCabinPointDiagnostic("rig", rig.position, doorCenter, cabinCenter, outward, requiredDepth, nearRadius, centerRadius)
                : "";
            return headInfo + rigInfo;
        }

        private string FormatCabinPointDiagnostic(string label, Vector3 position, Vector3 doorCenter, Vector3 cabinCenter, Vector3 outward, float requiredDepth, float nearRadius, float centerRadius)
        {
            Vector3 flatPosition = position;
            flatPosition.y = taxiRoot.position.y;
            Vector3 flatDoorCenter = doorCenter;
            flatDoorCenter.y = flatPosition.y;
            Vector3 flatCabinCenter = cabinCenter;
            flatCabinCenter.y = flatPosition.y;
            Vector3 flatTaxi = taxiRoot.position;
            flatTaxi.y = flatPosition.y;

            float inwardDepth = Vector3.Dot(flatPosition - flatDoorCenter, -outward);
            float nearDistance = Vector3.Distance(flatPosition, flatCabinCenter);
            float centerDistance = Vector3.Distance(flatPosition, flatTaxi);
            return label + "(depth=" + inwardDepth.ToString("0.000") + "/" + requiredDepth.ToString("0.000") +
                   ", near=" + nearDistance.ToString("0.000") + "/" + nearRadius.ToString("0.000") +
                   ", center=" + centerDistance.ToString("0.000") + "/" + centerRadius.ToString("0.000") + ")";
        }

        private Transform ResolveRiderHeadTransform()
        {
            if (riderHead != null)
            {
                return riderHead;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.gameObject.scene == gameObject.scene)
            {
                riderHead = mainCamera.transform;
                return riderHead;
            }

            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null &&
                    cameras[i].gameObject.scene == gameObject.scene &&
                    cameras[i].name == "Main Camera")
                {
                    riderHead = cameras[i].transform;
                    return riderHead;
                }
            }

            return null;
        }

        private Transform ResolveRiderRootTransform()
        {
            if (riderRoot != null)
            {
                return riderRoot;
            }

            Transform head = ResolveRiderHeadTransform();
            Transform cursor = head;
            while (cursor != null)
            {
                if (cursor.GetComponent<CocoonVRLocomotion>() != null ||
                    cursor.GetComponent<CharacterController>() != null ||
                    cursor.name.IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    riderRoot = cursor;
                    return riderRoot;
                }

                cursor = cursor.parent;
            }

            riderRoot = head != null && head.parent != null ? head.parent : head;
            return riderRoot;
        }

        private void AttachRiderToTaxi()
        {
            Transform rig = ResolveRiderRootTransform();
            if (rig == null || taxiRoot == null || rig == taxiRoot || rig.parent == taxiRoot)
            {
                return;
            }

            if (!hasRiderOriginalParent)
            {
                riderOriginalParent = rig.parent;
                hasRiderOriginalParent = true;
            }

            rig.SetParent(taxiRoot, true);
            if (!hasLoggedRiderAttachment)
            {
                CocoonDebugLog.Info("Door", "Rider rig attached to taxi for departure.", this);
                hasLoggedRiderAttachment = true;
            }
        }

        private void DetachRiderFromTaxi()
        {
            SetRiderHeightLockEnabled(true);
            SetRiderLocomotionEnabled(true);

            Transform rig = ResolveRiderRootTransform();
            if (rig == null || taxiRoot == null || rig.parent != taxiRoot)
            {
                return;
            }

            rig.SetParent(hasRiderOriginalParent ? riderOriginalParent : null, true);
            riderOriginalParent = null;
            hasRiderOriginalParent = false;
            CocoonDebugLog.Info("Door", "Rider rig detached from taxi.", this);
        }

        private void SetDestinationLine()
        {
            if (destinationText != null)
            {
                destinationText.gameObject.SetActive(true);
                SetUiText(destinationText, BuildRideSummaryText());
            }
        }

        private string BuildRideSummaryText()
        {
            string rideMode = string.IsNullOrEmpty(selectedRideMode) ? "Solo" : selectedRideMode;
            string pickup = selectedPullOverPoint != null ? selectedPullOverPoint.name : "Curb-side door";
            return "Destination: " + selectedDestination
                + "\nMode: " + rideMode
                + "\nPickup: " + pickup
                + "\nPayment: Contactless ready";
        }

        private void SetHeadline(string value)
        {
            if (headlineText != null)
            {
                SetUiText(headlineText, value);
            }

            if (doorHeadlineText != null)
            {
                SetUiText(doorHeadlineText, value);
            }
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
            {
                SetUiText(statusText, value);
            }

            if (doorStatusText != null)
            {
                SetUiText(doorStatusText, value);
            }
        }

        private void SetTimer(string value)
        {
            if (timerText != null)
            {
                SetUiText(timerText, value);
            }

            if (doorTimerText != null)
            {
                SetUiText(doorTimerText, value);
            }
        }

        private void SetInstruction(string value)
        {
            if (instructionText != null)
            {
                SetUiText(instructionText, value);
            }
        }

        private static void SetUiText(Text text, string value)
        {
            if (text == null)
            {
                return;
            }

            string resolvedValue = value ?? string.Empty;
            if (text.text == resolvedValue)
            {
                return;
            }

            text.canvasRenderer.Clear();
            text.cachedTextGenerator.Invalidate();
            text.cachedTextGeneratorForLayout.Invalidate();
            text.text = resolvedValue;
            text.SetVerticesDirty();
            text.SetLayoutDirty();
            text.SetMaterialDirty();
            Canvas.ForceUpdateCanvases();
        }

        private static string FormatPosition(Transform transform)
        {
            return transform != null ? FormatPosition(transform.position) : "null";
        }

        private static string FormatPosition(Vector3 position)
        {
            return "(" + position.x.ToString("0.00") + ", " + position.y.ToString("0.00") + ", " + position.z.ToString("0.00") + ")";
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
            {
                return "null";
            }

            string path = target.name;
            Transform cursor = target.parent;
            while (cursor != null)
            {
                path = cursor.name + "/" + path;
                cursor = cursor.parent;
            }

            return path;
        }
    }
}
