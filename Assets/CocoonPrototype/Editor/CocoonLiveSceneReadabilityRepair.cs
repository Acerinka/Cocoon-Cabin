using System;
using System.Collections.Generic;
using CocoonPrototype;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.OpenXR.Features;

namespace CocoonPrototype.Editor
{
    [InitializeOnLoad]
    public static class CocoonLiveSceneReadabilityRepair
    {
        private static readonly bool VerboseRepairLogs = false;
        private const string ScenePath = "Assets/Scenes/Cocoon_OnboardingVR.unity";
        private const string MaterialFolder = "Assets/CocoonPrototype/Materials";
        private const float WorldCanvasPixelsPerUnit = 768f;
        private const float HeadLockedCanvasPixelsPerUnit = 128f;
        private const float HeadLockedUiLocalScale = 0.0014f;
        private const float HeadLockedPanelWidth = 720f;
        private const float HeadLockedPanelHeight = 320f;
        private const float HeadLockedTextScale = 1.55f;
        private const float ExteriorScreenReferenceWidth = 1180f;
        private const float ExteriorScreenSourceWidth = 23040f;
        private const float ExteriorScreenSourceHeight = 2080f;
        private const string ExteriorScreenAssetPath = "Assets/CocoonPrototype/Resources/ExteriorScreens/ext-screen.png";
        private const string ExteriorScreenResourcePath = "ExteriorScreens/ext-screen";
        private const string CabinScreenResourceFolder = "Assets/CocoonPrototype/Resources/CabinScreens";
        private const string CabinSideScreenResourceFolder = CabinScreenResourceFolder + "/sideUI";
        private const string OfficialLeftHandModelPath = "Assets/CocoonPrototype/Generated/XRHands/LeftHand.fbx";
        private const string OfficialRightHandModelPath = "Assets/CocoonPrototype/Generated/XRHands/RightHand.fbx";
        private const string OfficialLeftHandPrefabPath = "Assets/CocoonPrototype/Generated/XRHands/Cocoon Official Left XR Hand.prefab";
        private const string OfficialRightHandPrefabPath = "Assets/CocoonPrototype/Generated/XRHands/Cocoon Official Right XR Hand.prefab";
        private const string OfficialHandMaterialPath = "Assets/CocoonPrototype/Materials/CocoonOfficialTransparentHand.mat";
        private const string DoorSideAPanelName = "Door-side Onboarding UI Side A";
        private const string DoorSideBPanelName = "Door-side Onboarding UI Side B";
        private const string DoorSidePanelAnchorRootName = "Door-side Onboarding UI Anchors";
        private const string BaggageQuestionPanelName = "Baggage Question Panel";
        private const string BoardingCardPromptPanelName = "Boarding Card Prompt Panel";
        private const string BoardingCardPromptTitle = "PREPAY TO OPEN";
        private const string BoardingCardPromptBody = "Please tap your card to prepay and open the door.";
        private const float BoardingPromptVisibleSeconds = 5f;
        private const float BoardingTimeoutSeconds = 30f;
        private const string SitPromptPanelName = "Sit Prompt Panel";
        private const string PassengerLuggageName = "Passenger Luggage Suitcase";
        private const string PassengerLuggageModelPath = "Assets/ImportedAssets/CocoonTaxi/bag.obj";
        private const string PassengerLuggageModelName = "Passenger Luggage Model";
        private const string LuggageRampName = "Luggage Boarding Ramp";
        private const string LuggageRamp1Name = "Luggage Boarding Ramp1";
        private const string LuggageRamp2Name = "Luggage Boarding Ramp2";
        private const string LuggageRampClosedMarker1Name = "\u5B9E\u4F5321";
        private const string LuggageRampClosedMarker2Name = "\u5B9E\u4F5322";
        private const string BoardingTouchZoneName = "DOORUI";
        private const string BoardingDoorSurfacePanelName = "DOORUI Surface Display Panel";
        private const string BoardingDoorSurfaceImageName = "DOORUI Surface Image";
        private const string BoardingDoorIdleScreenResourcePath = "BoardingDoorScreens/external-screen-UI-idle";
        private const string LeftDoorPanel1Name = "doorL1";
        private const string LeftDoorPanel2Name = "doorL2";
        private const string FinalTaxiInteriorStructureName = "Final Taxi Interior Structure";
        private const string FrontLed1Name = "led1";
        private const string FrontLed2Name = "led2";
        private const string FrontLed3Name = "led3";
        private const string RearOled1Name = "oled1";
        private const string RearOled2Name = "oled2";
        private const string RearOled3Name = "oled3";
        private const string FrontUnifiedDisplayName = "Cocoon Front LED Unified Display";
        private const string RearUnifiedDisplayName = "Cocoon Rear OLED Unified Display";
        private const string Seat1AnchorName = "seat1 Static Anchor";
        private const string MiniScreenName = "MIINIscreen1";
        private const string CabinLedEnvPanelName = "Cabin LED Env Display Panel";
        private const string CabinLedTripPanelName = "Cabin LED Trip Display Panel";
        private const string CabinLedMainPanelName = "Cabin LED Main Display Panel";
        private const string CabinOledEnvPanelName = "Cabin OLED Env Display Panel";
        private const string CabinOledTripPanelName = "Cabin OLED Trip Display Panel";
        private const string CabinOledMainPanelName = "Cabin OLED Main Display Panel";
        private const string CabinMiniPanelName = "Cabin Mini UI Display Panel";
        private const string CabinDefaultScreenImageName = "Screen Image";
        private const string CabinMiniScreenImageName = "Screen Image_final";
        private const float CabinLargePanelAspectEnvTrip = 4f;
        private const float CabinLargePanelAspectMain = 2f;
        private const float CabinMiniPanelAspect = 1f;
        private const float CabinPanelReferenceWidth = 1024f;
        private const float CabinLargePanelFallbackWidth = 0.08f;
        private const float CabinMiniPanelFallbackWidth = 0.04f;
        private const string LuggageStorageBagA1Name = "bagA1";
        private const string LuggageStorageBagA2Name = "bagA2";
        private const string LuggageStorageBagA3Name = "bagA3";
        private const string LuggagePreventerA1Name = "luggage_preventrs_A1";
        private const float LuggageCabinLiftDuration = 0.2f;
        private const float LuggageStorageA1ToA11Duration = 1.5f;
        private const float LuggageStorageA11ToA12Duration = 1.5f;
        private const float LuggageStorageA12ToA13Duration = 1.5f;
        private const float LuggageStorageA13ToA2Duration = 3f;
        private const float LuggageStorageA2ToA21Duration = 2f;
        private const float LuggageStorageA21ToA3Duration = 2f;
        private const float LuggagePreventerRaiseDuration = 3f;
        private static readonly string[] LuggageStorageA11Names = { "A1.1", "bagA1.1", "1.1" };
        private static readonly string[] LuggageStorageA12Names = { "A1.2", "bagA1.2", "1.2" };
        private static readonly string[] LuggageStorageA13Names = { "A1.3", "bagA1.3", "1.3" };
        private static readonly string[] LuggageStorageA21Names = { "A2.1", "bagA2.1", "2.1" };
        private const string Seat1Name = "seat1";
        private const string Seat1RotationTargetName = "seat1_";
        private const string Seat1LegacyMoveTargetName = "x";
        private const string Seat1LegacySitTargetName = "x_";
        private static readonly string[] Seat1MoveTargetNames = { "1x", "seat1x" };
        private static readonly string[] Seat1SitTargetNames = { "1x_", "seat1x_" };
        private const string SeatedRiderHeadAnchorName = "Seated Rider Head Anchor";
        private const string SeatV2ModelPath = "Assets/ImportedAssets/CocoonTaxi/SeatV2/seat_obj.obj";
        private const string SeatV2LegacyModelPath = "Assets/ImportedAssets/CocoonTaxi/SeatV2/seat-v2.obj";
        private const string SeatV2AssemblyName = "Seat V2 Assembly";
        private const string SeatV2BodyName = "Seat V2 Body";
        private const string SeatV2VisualName = "Seat V2 Visual";
        private const string SeatV2OutTargetName = "Seat V2 Bod_out";
        private const string SeatV2OutTargetAltName = "Seat V2 Body_out";
        private const string SeatV2BackTargetName = "Seat V2 Bod_back";
        private const string SeatV2BackTargetAltName = "Seat V2 Body_back";
        private const string SeatV2IdlePoseName = "SeatV2 Idle Pose";
        private const string SeatV2StorageTargetName = "SeatV2 Storage Target";
        private const string SeatV2SeatedPoseName = "SeatV2 Seated Pose";
        private const string SeatV2SeatedRotateTargetName = "SeatV2 Seated Rotate Target";
        private const string SeatV2SeatedHeadAnchorName = "SeatV2 Seated Rider Head Anchor";
        private const string SeatV2MiniScreenAnchorName = "SeatV2 Mini Screen Anchor";
        private const string SeatV2BackPivotName = "SeatV2 Back Pivot";
        private const string SeatV2ArmLPivotName = "SeatV2 Arm L Pivot";
        private const string SeatV2ArmRPivotName = "SeatV2 Arm R Pivot";
        private const string SeatV2FootrestPivotName = "SeatV2 Footrest Pivot";
        private static readonly Vector3 SeatV2DefaultScale = Vector3.one * 0.003595809f;
        private static readonly Quaternion SeatV2DefaultRotation = Quaternion.Euler(-90f, 0f, 0f);
        private const string Ro1Name = "ro1";
        private const string Ro2Name = "ro2";
        private const string Ro3Name = "ro3";
        private const float Seat1RotateDuration = 3.5f;
        private const float Seat1MoveDuration = 4f;
        private const float SitPromptSeconds = 7f;
        private const float SeatMoveRiderToAnchorDuration = 3f;
        private const float SeatSitRotateDuration = 7f;
        private const float RoFoldDuration = 2f;
        private const float RoFoldLocalYDegrees = -90f;
        private const float RiderCameraNearClipPlane = 0.005f;
        private const float RiderControllerRadiusMeters = 0.08f;
        private const float RightHandProxyScale = 0.035f;
        private const float LeftHandProxyScale = 0.032f;
        private const float HandProxyAlpha = 0.22f;
        private const string PassengerLuggageBaselineName = "bag0";
        private const string WheelchairBaselineName = "wheelchair0";
        private const string Wheel1Name = "wheel1";
        private const string Wheel2Name = "wheel2";
        private const string Wheel3Name = "wheel3";
        private const string Wheel4Name = "wheel4";
        private static readonly Vector3 DoorPanelLocalPosition = new Vector3(0.24f, 0.34f, 0.55f);
        private static readonly Vector3 DoorSideAPanelDefaultLocalPosition = new Vector3(-0.45f, 1.3f, 0f);
        private static readonly Vector3 DoorSideBPanelDefaultLocalPosition = new Vector3(0.45f, 1.3f, 0f);
        private static readonly Vector3 PassengerLuggageFallbackScale = Vector3.one * 0.004f;
        private static readonly Quaternion PassengerLuggageFallbackRotation = Quaternion.Euler(-90f, 0f, 0f);
        private static readonly Vector3 WheelchairFallbackScale = Vector3.one * 0.0035f;
        private static readonly Quaternion WheelchairFallbackRotation = Quaternion.Euler(-90f, 0f, -90f);
        private static bool hasLoggedMissingFinalTaxiModel;
        private static bool hasLoggedPreservedFinalTaxiModel;

        static CocoonLiveSceneReadabilityRepair()
        {
            EditorApplication.delayCall += ApplyToOpenScene;
        }

        [MenuItem("Cocoon/Repair Readable VR UI In Open Scene")]
        public static void ApplyToOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || !scene.path.Equals(ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool changed = false;
            DisableOpenXRHandTrackingFeatures(BuildTargetGroup.Android);
            DisableOpenXRHandTrackingFeatures(BuildTargetGroup.Standalone);
            Transform roadmap = FindTopLevelTransform(scene, "ROADMAP");
            if (roadmap != null && RepairRoadmapRuntimeSafety(scene, roadmap))
            {
                changed = true;
            }

            Transform head = FindTransform(scene, "Main Camera");
            Canvas streetPanel = FindCanvas(scene, "Street Instruction Panel");
            Transform taxi = FindTransform(scene, "Cocoon Autonomous Taxi");

            if (streetPanel != null)
            {
                RepairStreetPanel(streetPanel, head);
                changed = true;
            }

            changed |= EnsureHeadLockedUiTuning(scene);

            if (taxi != null)
            {
                changed |= PreserveAuthoredTaxiBaseline(scene, taxi);
                changed |= RepairTaxiPodVisuals(scene, taxi);
                changed |= EnsureSeatV2System(scene, taxi);
                changed |= RepairAuthoredWindshieldScreens(scene, taxi);
                changed |= RepairCabinScreens(scene, taxi);
                changed |= EnsureLightSequenceController(scene, taxi);
                changed |= RepairTaxiVisualMaterials(taxi);
            }

            changed |= EnsureBoardingDoorUiSettings(scene);
            changed |= EnsureBoardingCreditCardSettings(scene);
            changed |= EnsureRiderHeightCalibrator(scene);
            changed |= EnsureRiderComfortVisibility(scene);
            changed |= EnsureBaggageQuestionExperience(scene);
            changed |= EnsureBoardingDecisionPromptExperience(scene);
            changed |= CleanupDeprecatedBoardingObjects(scene);
            changed |= EnsureNewBoardingAnimationBindings(scene);
            changed |= EnsureTaxiWheelAnimator(scene);
            changed |= PreserveAuthoredTrafficVehicleSpawns(scene);

            if (roadmap == null && RepairExpandedPickupLoop(scene, head))
            {
                changed = true;
            }

            CocoonRaiseHandDetector raiseDetector = FindComponent<CocoonRaiseHandDetector>(scene);
            if (raiseDetector != null)
            {
                RepairHailDetectorTuning(raiseDetector);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (VerboseRepairLogs)
            {
                Debug.Log("Cocoon readable VR UI repair applied to the open onboarding scene.");
            }
        }

        private static bool RepairRoadmapRuntimeSafety(Scene scene, Transform roadmap)
        {
            bool changed = false;
            changed |= SetNamedRootActive(scene, "Runtime Rich City Environment", false);
            changed |= SetNamedTransformActive(scene, "Expanded Roads", false);
            changed |= SetNamedTransformActive(scene, "Expanded Sidewalks", false);
            changed |= SetNamedTransformActive(scene, "Expanded Curbs", false);
            changed |= SetNamedTransformActive(scene, "Expanded Crosswalks", false);
            changed |= SetNamedTransformActive(scene, "Expanded Buildings", false);
            changed |= SetNamedTransformActive(scene, "Expanded Street Furniture", false);
            changed |= EnableMainCamera(scene);
            changed |= CalibrateRoadmapVrScale(scene, roadmap);
            changed |= NormalizeRootTransformPreservingChildren(FindTopLevelTransform(scene, "03_Traffic"));
            changed |= NormalizeRootTransformPreservingChildren(FindTopLevelTransform(scene, "04_Cocoon_Taxi"));
            changed |= KeepXrOriginInsideRoadmap(scene, roadmap);
            changed |= RepairRoadmapTrafficGraphIfStale(scene);
            return changed;
        }

        private static bool RepairRoadmapTrafficGraphIfStale(Scene scene)
        {
            Transform networkRoot = FindTransform(scene, "ROADMAP Two-Way Traffic Network");
            CocoonTrafficLanePath[] lanes = networkRoot != null
                ? networkRoot.GetComponentsInChildren<CocoonTrafficLanePath>(true)
                : new CocoonTrafficLanePath[0];
            CocoonRoadGraph roadGraph = networkRoot != null ? networkRoot.GetComponentInChildren<CocoonRoadGraph>(true) : null;
            bool graphMissing = roadGraph == null || roadGraph.EdgeCount <= 0;
            bool needsRebuild = graphMissing;
            int blockedSegments = 0;
            for (int i = 0; i < lanes.Length; i++)
            {
                CocoonTrafficLanePath lane = lanes[i];
                if (lane == null)
                {
                    needsRebuild = true;
                    continue;
                }

                blockedSegments += lane.BlockedSegmentCount;
                if (graphMissing && lane.Count < 2)
                {
                    needsRebuild = true;
                }
            }

            if (graphMissing && blockedSegments > 0)
            {
                needsRebuild = true;
            }

            if (!needsRebuild)
            {
                return false;
            }

            Debug.Log("Cocoon ROADMAP traffic graph is stale or discontinuous (lanes=" + lanes.Length +
                      ", graphEdges=" + (roadGraph != null ? roadGraph.EdgeCount.ToString() : "none") +
                      ", blockedSegments=" + blockedSegments +
                      "); rebuilding lanes from ROADMAP while preserving authored vehicle and pickup transforms.");
            return CocoonJapaneseRoadNetworkBuilder.ApplyToCurrentSceneIfAvailable(false);
        }

        private static bool CalibrateRoadmapVrScale(Scene scene, Transform roadmap)
        {
            Transform xrOrigin = FindTransform(scene, "XR Origin - Sidewalk Rider");
            if (xrOrigin == null || roadmap == null)
            {
                return false;
            }

            float experienceScale = ResolveRoadmapExperienceScale(roadmap);
            Vector3 targetScale = Vector3.one * experienceScale;
            if (Vector3.Distance(xrOrigin.localScale, targetScale) < 0.0001f)
            {
                return CocoonExperienceScale.CalibrateCharacterController(xrOrigin);
            }

            xrOrigin.localScale = targetScale;
            CocoonExperienceScale.CalibrateCharacterController(xrOrigin);
            Debug.Log("ROADMAP VR rider scale calibrated to " + experienceScale.ToString("0.###") +
                      " to restore street-level headset height.", xrOrigin);
            return true;
        }

        private static float ResolveRoadmapExperienceScale(Transform roadmap)
        {
            Vector3 scale = roadmap.lossyScale;
            float horizontalScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.z)) * 0.5f;
            if (horizontalScale < 0.02f || horizontalScale > 0.35f)
            {
                return 1f;
            }

            return horizontalScale;
        }

        private static bool EnableMainCamera(Scene scene)
        {
            bool changed = false;
            Transform mainCamera = FindTransform(scene, "Main Camera");
            if (mainCamera == null)
            {
                return false;
            }

            if (!mainCamera.gameObject.activeSelf)
            {
                mainCamera.gameObject.SetActive(true);
                changed = true;
            }

            Camera camera = mainCamera.GetComponent<Camera>();
            if (camera != null && !camera.enabled)
            {
                camera.enabled = true;
                changed = true;
            }

            if (camera != null && camera.nearClipPlane > RiderCameraNearClipPlane + 0.0001f)
            {
                camera.nearClipPlane = RiderCameraNearClipPlane;
                changed = true;
            }

            return changed;
        }

        private static bool KeepXrOriginInsideRoadmap(Scene scene, Transform roadmap)
        {
            Transform xrOrigin = FindTransform(scene, "XR Origin - Sidewalk Rider");
            if (xrOrigin == null || roadmap == null || !TryGetRendererBounds(roadmap, out Bounds bounds))
            {
                return false;
            }

            float experienceScale = ResolveRoadmapExperienceScale(roadmap);
            if (ContainsHorizontal(bounds, xrOrigin.position, 0.75f * experienceScale))
            {
                return false;
            }

            Transform pickupStop = FindTransform(scene, "Pickup Bay Stop");
            Vector3 lookAt = pickupStop != null ? pickupStop.position : bounds.center;
            Vector3 position = pickupStop != null
                ? pickupStop.position - FlattenDirection(pickupStop.forward, Vector3.forward) * (1.85f * experienceScale)
                : new Vector3(bounds.min.x + Mathf.Min(2.2f * experienceScale, bounds.size.x * 0.18f), bounds.min.y, bounds.min.z + Mathf.Min(2.2f * experienceScale, bounds.size.z * 0.18f));

            position.y = Mathf.Max(0f, bounds.min.y);
            xrOrigin.position = position;
            Vector3 direction = lookAt - position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                xrOrigin.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            Debug.Log("ROADMAP VR origin moved back inside the active road network at " + xrOrigin.position.ToString("F2") + ".", xrOrigin);
            return true;
        }

        [MenuItem("Cocoon/Repair Saved Prototype Scene")]
        public static void RepairSavedPrototypeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyToOpenScene();
            AssetDatabase.SaveAssets();
        }

        private static bool RepairTaxiPodVisuals(Scene scene, Transform taxi)
        {
            bool disabledBodyLights = EnsureBodyLightVisualsDisabled(scene);
            CocoonImportedTaxiModelBuilder.EnsureRenderableModelAssetImported();
            if (HasAuthoredFinalTaxiModel(taxi))
            {
                bool snappedFinalTaxiTransform = SnapAuthoredFinalTaxiBodyTransform(taxi);
                bool ensuredInterior = CocoonImportedTaxiModelBuilder.EnsureTaxiInteriorStructure(taxi);
                if (!hasLoggedPreservedFinalTaxiModel)
                {
                    hasLoggedPreservedFinalTaxiModel = true;
                    if (VerboseRepairLogs)
                    {
                        Debug.Log("Cocoon taxi visuals preserved: authored Final Taxi Body sample detected.");
                    }
                }

                return disabledBodyLights || snappedFinalTaxiTransform || ensuredInterior;
            }

            if (!HasFinalTaxiModelAsset())
            {
                if (!hasLoggedMissingFinalTaxiModel)
                {
                    hasLoggedMissingFinalTaxiModel = true;
                    Debug.LogWarning("Cocoon current taxi source missing at " + CocoonImportedTaxiModelBuilder.ModelPath + ", " + CocoonImportedTaxiModelBuilder.ObjModelPath + ", or " + CocoonImportedTaxiModelBuilder.FusionArchivePath + "; preserving existing taxi visuals.");
                }

                return disabledBodyLights;
            }

            Transform existing = taxi.Find("Taxi Pod Visuals");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            DisableLegacyTaxiVisuals(taxi);

            Material bodyMat = LoadMaterial("TaxiBody", new Color(0.82f, 0.88f, 0.86f));
            Material glassMat = LoadMaterial("TaxiGlass", new Color(0.08f, 0.16f, 0.2f));
            Material tireMat = LoadMaterial("Tires", new Color(0.015f, 0.015f, 0.018f));
            Material lightMat = LoadMaterial("TaxiLights", new Color(0.12f, 0.95f, 1f));

            if (CocoonImportedTaxiModelBuilder.TryBuild(taxi, bodyMat, glassMat, tireMat, lightMat, out Transform importedDoorHinge, out Transform importedReader, out Renderer importedReaderRenderer, out Renderer importedBodyRenderer, out Renderer[] importedLights, out Text importedWindshieldText, out Text importedRearWindshieldText))
            {
                CocoonTaxiStateMachine importedStateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
                if (importedStateMachine != null)
                {
                    var serialized = new SerializedObject(importedStateMachine);
                    serialized.FindProperty("doorHinge").objectReferenceValue = importedDoorHinge;
                    serialized.FindProperty("paymentReader").objectReferenceValue = importedReader;
                    serialized.FindProperty("paymentReaderRenderer").objectReferenceValue = importedReaderRenderer;
                    serialized.FindProperty("bodyRenderer").objectReferenceValue = importedBodyRenderer;
                    serialized.FindProperty("windshieldText").objectReferenceValue = importedWindshieldText;
                    serialized.FindProperty("rearWindshieldText").objectReferenceValue = importedRearWindshieldText;
                    serialized.FindProperty("poseConfirmSeconds").floatValue = 3f;
                    SerializedProperty bodyLightVisualsProperty = serialized.FindProperty("enableBodyLightVisuals");
                    if (bodyLightVisualsProperty != null)
                    {
                        bodyLightVisualsProperty.boolValue = false;
                    }

                    SerializedProperty lightProperty = serialized.FindProperty("lightRenderers");
                    lightProperty.arraySize = importedLights != null ? importedLights.Length : 0;
                    for (int i = 0; importedLights != null && i < importedLights.Length; i++)
                    {
                        lightProperty.GetArrayElementAtIndex(i).objectReferenceValue = importedLights[i];
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                string activeModelPath = CocoonImportedTaxiModelBuilder.ActiveRenderableModelPath();
                Debug.Log(!string.IsNullOrEmpty(activeModelPath)
                    ? "Cocoon taxi visuals repaired using imported final model: " + activeModelPath + "."
                    : "Cocoon taxi visuals repaired using ZOOX.f3z source proxy; export ZOOX.fbx later for full-fidelity geometry.");
                return true;
            }

            var visualRoot = new GameObject("Taxi Pod Visuals");
            visualRoot.transform.SetParent(taxi, false);

            GameObject body = CreateTaxiCubeChild("Rounded Lower Body", visualRoot.transform, new Vector3(0f, 0.52f, -0.02f), new Vector3(2.04f, 0.72f, 3.2f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Short Rounded Front Nose", visualRoot.transform, new Vector3(0f, 0.46f, 1.78f), new Vector3(1.68f, 0.5f, 0.45f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Compact Rear Volume", visualRoot.transform, new Vector3(0f, 0.76f, -1.56f), new Vector3(1.78f, 0.94f, 0.54f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("High Cocoon Roof", visualRoot.transform, new Vector3(0f, 1.5f, -0.28f), new Vector3(1.66f, 0.28f, 2.18f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Roof Message Cap", visualRoot.transform, new Vector3(0f, 1.76f, 0.92f), new Vector3(1.55f, 0.18f, 0.52f), Quaternion.identity, bodyMat);

            CreateTaxiCubeChild("Panoramic Front Glass", visualRoot.transform, new Vector3(0f, 1.15f, 1.47f), new Vector3(1.64f, 1.08f, 0.08f), Quaternion.Euler(-13f, 0f, 0f), glassMat);
            CreateTaxiCubeChild("Rear Glass", visualRoot.transform, new Vector3(0f, 1.12f, -1.86f), new Vector3(1.35f, 0.72f, 0.08f), Quaternion.identity, glassMat);
            CreateTaxiCubeChild("Left Side Window", visualRoot.transform, new Vector3(-1.04f, 1.16f, 0.02f), new Vector3(0.06f, 0.74f, 1.65f), Quaternion.identity, glassMat);
            GameObject rightSideWindow = CreateTaxiCubeChild("Right Side Window", visualRoot.transform, new Vector3(1.04f, 1.16f, 0.02f), new Vector3(0.06f, 0.74f, 1.65f), Quaternion.identity, glassMat);
            rightSideWindow.SetActive(false);
            CreateTaxiCubeChild("Left Sliding Door Frame", visualRoot.transform, new Vector3(-1.08f, 0.82f, -0.2f), new Vector3(0.08f, 1.08f, 1.36f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Left Door Center Seam", visualRoot.transform, new Vector3(-1.13f, 0.94f, -0.2f), new Vector3(0.035f, 0.9f, 0.05f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Left Door Handle", visualRoot.transform, new Vector3(-1.14f, 0.9f, -0.17f), new Vector3(0.04f, 0.12f, 0.46f), Quaternion.identity, bodyMat);

            CreateTaxiCylinderChild("Wheel Cover FL", visualRoot.transform, new Vector3(-1.07f, 0.28f, 1.05f), new Vector3(0.5f, 0.09f, 0.5f), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateTaxiCylinderChild("Wheel Cover FR", visualRoot.transform, new Vector3(1.07f, 0.28f, 1.05f), new Vector3(0.5f, 0.09f, 0.5f), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateTaxiCylinderChild("Wheel Cover RL", visualRoot.transform, new Vector3(-1.07f, 0.28f, -1.13f), new Vector3(0.5f, 0.09f, 0.5f), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateTaxiCylinderChild("Wheel Cover RR", visualRoot.transform, new Vector3(1.07f, 0.28f, -1.13f), new Vector3(0.5f, 0.09f, 0.5f), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateTaxiCubeChild("Front Wheel Arch Left", visualRoot.transform, new Vector3(-1.03f, 0.5f, 1.05f), new Vector3(0.1f, 0.42f, 0.78f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Front Wheel Arch Right", visualRoot.transform, new Vector3(1.03f, 0.5f, 1.05f), new Vector3(0.1f, 0.42f, 0.78f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Rear Wheel Arch Left", visualRoot.transform, new Vector3(-1.03f, 0.5f, -1.13f), new Vector3(0.1f, 0.42f, 0.78f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Rear Wheel Arch Right", visualRoot.transform, new Vector3(1.03f, 0.5f, -1.13f), new Vector3(0.1f, 0.42f, 0.78f), Quaternion.identity, bodyMat);

            Renderer[] lights = new Renderer[0];

            Text windshieldText = CreateWindshieldDisplay(
                "Taxi Front Windshield Display",
                visualRoot.transform,
                new Vector3(0f, 1.22f, 1.54f),
                Quaternion.Euler(-13f, 0f, 0f),
                new Vector3(-0.00132f, 0.00132f, 0.00132f));
            Text rearWindshieldText = CreateWindshieldDisplay(
                "Taxi Rear Windshield Display",
                visualRoot.transform,
                new Vector3(0f, 1.12f, -1.91f),
                Quaternion.identity,
                new Vector3(0.00108f, 0.00108f, 0.00108f));

            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (stateMachine != null)
            {
                var serialized = new SerializedObject(stateMachine);
                serialized.FindProperty("doorHinge").objectReferenceValue = null;
                serialized.FindProperty("paymentReader").objectReferenceValue = null;
                serialized.FindProperty("paymentReaderRenderer").objectReferenceValue = null;
                serialized.FindProperty("bodyRenderer").objectReferenceValue = body.GetComponent<Renderer>();
                serialized.FindProperty("windshieldText").objectReferenceValue = windshieldText;
                serialized.FindProperty("rearWindshieldText").objectReferenceValue = rearWindshieldText;
                serialized.FindProperty("poseConfirmSeconds").floatValue = 3f;

                SerializedProperty lightProperty = serialized.FindProperty("lightRenderers");
                lightProperty.arraySize = lights.Length;
                for (int i = 0; i < lights.Length; i++)
                {
                    lightProperty.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log("Cocoon taxi pod visuals repaired: front/rear windshield displays and pod body are live; body light visuals are disabled.");
            return true;
        }

        private static bool PreserveAuthoredTaxiBaseline(Scene scene, Transform taxi)
        {
            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (stateMachine == null)
            {
                return false;
            }

            bool changed = false;
            var serialized = new SerializedObject(stateMachine);
            SerializedProperty taxiRootProperty = serialized.FindProperty("taxiRoot");
            if (taxiRootProperty != null && taxiRootProperty.objectReferenceValue == null)
            {
                taxiRootProperty.objectReferenceValue = taxi;
                changed = true;
            }

            SerializedProperty preserveProperty = serialized.FindProperty("preserveAuthoredTaxiComposition");
            if (preserveProperty != null && !preserveProperty.boolValue)
            {
                preserveProperty.boolValue = true;
                changed = true;
            }

            SerializedProperty preserveSpawnProperty = serialized.FindProperty("preserveAuthoredTaxiSpawnTransform");
            if (preserveSpawnProperty != null && !preserveSpawnProperty.boolValue)
            {
                preserveSpawnProperty.boolValue = true;
                changed = true;
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed;
        }

        private static bool SnapAuthoredFinalTaxiBodyTransform(Transform taxi)
        {
            Transform model = taxi != null ? FindTransformDeep(taxi, CocoonImportedTaxiModelBuilder.ModelInstanceName) : null;
            if (model == null || CocoonImportedTaxiModelBuilder.IsCanonicalFinalTaxiBodyTransform(model))
            {
                return false;
            }

            CocoonImportedTaxiModelBuilder.ApplyCanonicalFinalTaxiBodyTransform(model);
            Debug.Log("Cocoon snapped Final Taxi Body to the authored ZOOX transform baseline.", model);
            return true;
        }

        private static bool EnsureBoardingDoorUiSettings(Scene scene)
        {
            Transform uiRoot = FindTransform(scene, "06_UI");
            if (uiRoot == null)
            {
                return false;
            }

            bool changed = false;
            CocoonBoardingDoorUISettings settings = uiRoot.GetComponentInChildren<CocoonBoardingDoorUISettings>(true);
            if (settings == null)
            {
                var settingsObject = new GameObject("Boarding Door UI Settings");
                settingsObject.transform.SetParent(uiRoot, false);
                settings = settingsObject.AddComponent<CocoonBoardingDoorUISettings>();
                changed = true;
            }

            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (stateMachine != null && settings != null)
            {
                var serialized = new SerializedObject(stateMachine);
                SerializedProperty settingsProperty = serialized.FindProperty("boardingDoorUiSettings");
                if (settingsProperty != null && settingsProperty.objectReferenceValue == null)
                {
                    settingsProperty.objectReferenceValue = settings;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsureBoardingCreditCardSettings(Scene scene)
        {
            Transform uiRoot = FindTransform(scene, "06_UI");
            if (uiRoot == null)
            {
                return false;
            }

            bool changed = false;
            CocoonBoardingCreditCardSettings settings = uiRoot.GetComponentInChildren<CocoonBoardingCreditCardSettings>(true);
            if (settings == null)
            {
                var settingsObject = new GameObject("Boarding Credit Card Settings");
                settingsObject.transform.SetParent(uiRoot, false);
                settings = settingsObject.AddComponent<CocoonBoardingCreditCardSettings>();
                changed = true;
            }

            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (stateMachine != null && settings != null)
            {
                var serialized = new SerializedObject(stateMachine);
                SerializedProperty settingsProperty = serialized.FindProperty("boardingCreditCardSettings");
                if (settingsProperty != null && settingsProperty.objectReferenceValue != settings)
                {
                    settingsProperty.objectReferenceValue = settings;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsureRiderHeightCalibrator(Scene scene)
        {
            Transform rig = FindTransform(scene, "XR Origin - Sidewalk Rider");
            if (rig == null)
            {
                return false;
            }

            Type calibratorType = FindType("CocoonPrototype.CocoonRiderHeightCalibrator");
            if (calibratorType == null)
            {
                return false;
            }

            bool changed = false;
            Component calibrator = rig.GetComponent(calibratorType);
            if (calibrator == null)
            {
                calibrator = rig.gameObject.AddComponent(calibratorType);
                changed = true;
            }

            Transform head = FindTransform(scene, "Main Camera");
            var serialized = new SerializedObject(calibrator);
            changed |= SetObjectReference(serialized, "rigRoot", rig);
            if (head != null)
            {
                changed |= SetObjectReference(serialized, "head", head);
            }

            SerializedProperty targetHeightProperty = serialized.FindProperty("targetHeadHeightMeters");
            if (targetHeightProperty != null && targetHeightProperty.floatValue <= 0.01f)
            {
                targetHeightProperty.floatValue = 1.55f;
                changed = true;
            }

            SerializedProperty controllerRadiusProperty = serialized.FindProperty("controllerRadiusMeters");
            if (controllerRadiusProperty != null && Mathf.Abs(controllerRadiusProperty.floatValue - RiderControllerRadiusMeters) > 0.0001f)
            {
                controllerRadiusProperty.floatValue = RiderControllerRadiusMeters;
                changed = true;
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            return changed;
        }

        private static bool EnsureRiderComfortVisibility(Scene scene)
        {
            bool changed = false;
            Transform mainCamera = FindTransform(scene, "Main Camera");
            Camera camera = mainCamera != null ? mainCamera.GetComponent<Camera>() : null;
            if (camera != null && camera.nearClipPlane > RiderCameraNearClipPlane + 0.0001f)
            {
                camera.nearClipPlane = RiderCameraNearClipPlane;
                changed = true;
            }

            Transform rig = FindTransform(scene, "XR Origin - Sidewalk Rider");
            CharacterController controller = rig != null ? rig.GetComponent<CharacterController>() : null;
            if (controller != null && Mathf.Abs(controller.radius - RiderControllerRadiusMeters) > 0.0001f)
            {
                controller.radius = RiderControllerRadiusMeters;
                changed = true;
            }

            Transform leftHand = FindTransform(scene, "Left Controller - Move and Teleport");
            Transform rightHand = FindTransform(scene, "Right Controller - Hail and UI Ray");
            changed |= ConfigureHandProxyVisual(leftHand, LeftHandProxyScale);
            changed |= ConfigureHandProxyVisual(rightHand, RightHandProxyScale);
            changed |= EnsureXRHandVisualizer(scene, leftHand, rightHand);
            return changed;
        }

        private static bool EnsureXRHandVisualizer(Scene scene, Transform leftHand, Transform rightHand)
        {
            Transform cameraOffset = FindTransform(scene, "Camera Offset");
            if (cameraOffset == null)
            {
                return false;
            }

            CocoonXRHandVisualizer visualizer = cameraOffset.GetComponent<CocoonXRHandVisualizer>();
            bool changed = false;
            changed |= DestroyGeneratedHandChild(cameraOffset, "Transparent Left XR Hand");
            changed |= DestroyGeneratedHandChild(cameraOffset, "Transparent Right XR Hand");
            changed |= DestroyGeneratedHandChild(cameraOffset, "Transparent Left Controller Hand");
            changed |= DestroyGeneratedHandChild(cameraOffset, "Transparent Right Controller Hand");
            changed |= DestroyGeneratedHandChild(cameraOffset, "Official Unity Left XR Hand");
            changed |= DestroyGeneratedHandChild(cameraOffset, "Official Unity Right XR Hand");
            if (visualizer == null)
            {
                return changed;
            }

            var serialized = new SerializedObject(visualizer);
            changed |= SetObjectReference(serialized, "leftControllerProxy", leftHand);
            changed |= SetObjectReference(serialized, "rightControllerProxy", rightHand);
            changed |= SetObjectReference(serialized, "leftOfficialHandPrefab", null);
            changed |= SetObjectReference(serialized, "rightOfficialHandPrefab", null);
            changed |= SetNestedBool(serialized, "useOfficialHandPrefabs", false);
            changed |= SetNestedBool(serialized, "showControllerTrackedHandFallback", false);
            changed |= SetNestedBool(serialized, "hideControllerProxyWhenHandTracked", false);
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            if (visualizer.enabled)
            {
                visualizer.enabled = false;
                changed = true;
            }

            EditorUtility.SetDirty(visualizer);
            return changed;
        }

        private static bool DestroyGeneratedHandChild(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child == null)
            {
                return false;
            }

            UnityEngine.Object.DestroyImmediate(child.gameObject);
            return true;
        }

        private static GameObject EnsureOfficialXRHandPrefab(string modelPath, string prefabPath, Handedness handedness, string prefabName)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null &&
                existing.GetComponent<XRHandTrackingEvents>() != null &&
                existing.GetComponent<XRHandSkeletonDriver>() != null &&
                existing.GetComponent<XRHandMeshController>() != null)
            {
                return existing;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning("Cocoon XR Hands model missing: " + modelPath + ". Official hand mesh prefab will not be generated.");
                return null;
            }

            EnsureFolderForAsset(prefabPath);
            Material handMaterial = EnsureOfficialTransparentHandMaterial();
            GameObject root = new GameObject(prefabName);
            GameObject modelInstance = null;
            try
            {
                modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
                if (modelInstance == null)
                {
                    modelInstance = UnityEngine.Object.Instantiate(model);
                }

                modelInstance.name = model.name;
                modelInstance.transform.SetParent(root.transform, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                XRHandTrackingEvents trackingEvents = root.AddComponent<XRHandTrackingEvents>();
                trackingEvents.handedness = handedness;
                trackingEvents.updateType = XRHandTrackingEvents.UpdateTypes.Dynamic | XRHandTrackingEvents.UpdateTypes.BeforeRender;

                XRHandSkeletonDriver skeletonDriver = root.AddComponent<XRHandSkeletonDriver>();
                skeletonDriver.handTrackingEvents = trackingEvents;
                skeletonDriver.rootTransform = FindJointRoot(modelInstance.transform) ?? modelInstance.transform;
                skeletonDriver.jointTransformReferences = new List<JointToTransformReference>();
                XRHandSkeletonDriverUtility.FindJointsFromRoot(skeletonDriver, new List<string>());

                XRHandMeshController meshController = root.AddComponent<XRHandMeshController>();
                meshController.handTrackingEvents = trackingEvents;
                meshController.handMeshRenderer = modelInstance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (meshController.handMeshRenderer == null)
                {
                    meshController.handMeshRenderer = modelInstance.GetComponentInChildren<Renderer>(true);
                }

                meshController.showMeshWhenTrackingIsAcquired = true;
                meshController.hideMeshWhenTrackingIsLost = true;
                ConfigureOfficialHandRenderers(modelInstance, handMaterial);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab != null)
                {
                    Debug.Log("Cocoon official XR hand prefab generated: " + prefabPath);
                }

                return prefab;
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static Transform FindJointRoot(Transform root)
        {
            Transform wrist = FindJointTransform(root, XRHandJointID.Wrist.ToString());
            return wrist != null ? wrist : root;
        }

        private static Transform FindJointTransform(Transform root, string jointName)
        {
            if (root == null)
            {
                return null;
            }

            if (StartsOrEndsWith(root.name, jointName))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindJointTransform(root.GetChild(i), jointName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool StartsOrEndsWith(string value, string searchTerm)
        {
            return value.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                   value.EndsWith(searchTerm, StringComparison.OrdinalIgnoreCase);
        }

        private static Material EnsureOfficialTransparentHandMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(OfficialHandMaterialPath);
            if (material != null)
            {
                return material;
            }

            EnsureFolderForAsset(OfficialHandMaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            material.name = "CocoonOfficialTransparentHand";
            ConfigureTransparentMaterial(material, new Color(0.72f, 0.96f, 1f, 0.28f));
            AssetDatabase.CreateAsset(material, OfficialHandMaterialPath);
            return material;
        }

        private static void ConfigureOfficialHandRenderers(GameObject root, Material material)
        {
            if (root == null || material == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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
                    materials[j] = material;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void ConfigureTransparentMaterial(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void EnsureFolderForAsset(string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void EnsureOpenXRHandTrackingFeatures(BuildTargetGroup targetGroup)
        {
            FeatureHelpers.RefreshFeatures(targetGroup);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handtracking", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.metahandtrackingaim", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handinteraction", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handinteractionposes", true);
        }

        private static void DisableOpenXRHandTrackingFeatures(BuildTargetGroup targetGroup)
        {
            FeatureHelpers.RefreshFeatures(targetGroup);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handtracking", false);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.metahandtrackingaim", false);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handinteraction", false);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handinteractionposes", false);
        }

        private static void SetOpenXRFeature(BuildTargetGroup targetGroup, string featureId, bool enabled)
        {
            OpenXRFeature feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(targetGroup, featureId);
            if (feature == null || feature.enabled == enabled)
            {
                return;
            }

            feature.enabled = enabled;
            EditorUtility.SetDirty(feature);
        }

        private static bool ConfigureHandProxyVisual(Transform hand, float targetScale)
        {
            if (hand == null)
            {
                return false;
            }

            bool changed = false;
            Vector3 scale = Vector3.one * targetScale;
            if (Vector3.Distance(hand.localScale, scale) > 0.0001f)
            {
                hand.localScale = scale;
                changed = true;
            }

            Renderer[] renderers = hand.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                changed |= ConfigureHandProxyMaterial(renderers[i]);
            }

            return changed;
        }

        private static bool ConfigureHandProxyMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            bool changed = false;
            if (!renderer.enabled)
            {
                renderer.enabled = true;
                changed = true;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                Color color = new Color(0.08f, 0.28f, 1f, 1f);
                if (SetMaterialColorIfNeeded(material, color))
                {
                    changed = true;
                }

                if (material.HasProperty("_Surface") && Mathf.Abs(material.GetFloat("_Surface")) > 0.0001f)
                {
                    material.SetFloat("_Surface", 0f);
                    changed = true;
                }

                if (material.HasProperty("_ZWrite") && Mathf.Abs(material.GetFloat("_ZWrite") - 1f) > 0.0001f)
                {
                    material.SetFloat("_ZWrite", 1f);
                    changed = true;
                }

                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHABLEND_ON");
                if (material.renderQueue != (int)RenderQueue.Geometry)
                {
                    material.renderQueue = (int)RenderQueue.Geometry;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(material);
                }
            }

            return changed;
        }

        private static bool SetMaterialColorIfNeeded(Material material, Color color)
        {
            bool changed = false;
            if (material.HasProperty("_BaseColor") && material.GetColor("_BaseColor") != color)
            {
                material.SetColor("_BaseColor", color);
                changed = true;
            }

            if (material.HasProperty("_Color") && material.GetColor("_Color") != color)
            {
                material.SetColor("_Color", color);
                changed = true;
            }

            return changed;
        }

        private static bool EnsureBaggageQuestionExperience(Scene scene)
        {
            Transform uiRoot = FindTransform(scene, "06_UI");
            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (uiRoot == null || stateMachine == null)
            {
                return false;
            }

            bool changed = false;
            Canvas baggagePanel = FindCanvas(scene, BaggageQuestionPanelName);
            if (baggagePanel != null)
            {
                if (baggagePanel.gameObject.activeSelf)
                {
                    baggagePanel.gameObject.SetActive(false);
                    changed = true;
                }
            }

            Transform luggage = FindTransform(scene, PassengerLuggageName);
            if (luggage == null)
            {
                luggage = CreatePassengerLuggage(uiRoot);
                changed = true;
            }

            if (luggage != null)
            {
                changed |= EnsurePassengerLuggageModel(luggage);
                changed |= EnsureLuggageFollower(
                    luggage,
                    FindTransform(scene, "Left Controller - Move and Teleport"),
                    FindTransform(scene, "Main Camera"));
            }

            var serialized = new SerializedObject(stateMachine);
            changed |= SetBool(serialized, "suppressHeadLockedFlowUi", true);
            changed |= SetObjectReference(serialized, "baggageQuestionPanel", baggagePanel != null ? baggagePanel.gameObject : null);
            changed |= SetObjectReference(serialized, "passengerLuggage", luggage != null ? luggage.gameObject : null);
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            return changed;
        }

        private static bool EnsureBoardingDecisionPromptExperience(Scene scene)
        {
            Transform uiRoot = FindTransform(scene, "06_UI");
            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (uiRoot == null || stateMachine == null)
            {
                return false;
            }

            bool changed = false;
            Canvas boardingPrompt = FindCanvas(scene, BoardingCardPromptPanelName);
            if (boardingPrompt != null)
            {
                if (boardingPrompt.gameObject.activeSelf)
                {
                    boardingPrompt.gameObject.SetActive(false);
                    changed = true;
                }
            }

            Canvas sitPrompt = FindCanvas(scene, SitPromptPanelName);
            if (sitPrompt != null)
            {
                if (sitPrompt.gameObject.activeSelf)
                {
                    sitPrompt.gameObject.SetActive(false);
                    changed = true;
                }
            }

            var serialized = new SerializedObject(stateMachine);
            changed |= SetBool(serialized, "suppressHeadLockedFlowUi", true);
            changed |= SetObjectReference(serialized, "boardingCardPromptPanel", boardingPrompt != null ? boardingPrompt.gameObject : null);
            changed |= SetObjectReference(serialized, "sitPromptPanel", sitPrompt != null ? sitPrompt.gameObject : null);
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            return changed;
        }

        private static bool EnsureDualDoorSidePanels(Scene scene)
        {
            Transform uiRoot = FindTransform(scene, "06_UI");
            Transform taxi = FindTransform(scene, "Cocoon Autonomous Taxi");
            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (uiRoot == null || stateMachine == null)
            {
                return false;
            }

            bool changed = false;
            Transform panelParent = GetOrCreateDoorSidePanelAnchorRoot(taxi, ref changed);
            if (panelParent == null)
            {
                panelParent = uiRoot;
            }

            Canvas sideA = FindCanvas(scene, DoorSideAPanelName);
            Canvas sideB = FindCanvas(scene, DoorSideBPanelName);

            if (sideA == null)
            {
                sideA = CreateManualDoorSidePanel(panelParent, DoorSideAPanelName, DoorSideAPanelDefaultLocalPosition, stateMachine).GetComponent<Canvas>();
                changed = true;
            }

            if (sideB == null)
            {
                sideB = CreateManualDoorSidePanel(panelParent, DoorSideBPanelName, DoorSideBPanelDefaultLocalPosition, stateMachine).GetComponent<Canvas>();
                changed = true;
            }

            if (sideA != null)
            {
                changed |= BindDoorSidePanelToTaxi(sideA.transform, panelParent);
                EnsureManualDoorPanelContents(sideA.transform, stateMachine);
                WireDoorPanelButtons(sideA.transform, stateMachine);
            }

            if (sideB != null)
            {
                changed |= BindDoorSidePanelToTaxi(sideB.transform, panelParent);
                EnsureManualDoorPanelContents(sideB.transform, stateMachine);
                WireDoorPanelButtons(sideB.transform, stateMachine);
            }

            var serialized = new SerializedObject(stateMachine);
            changed |= SetObjectReference(serialized, "doorSideAPanel", sideA != null ? sideA.gameObject : null);
            changed |= SetObjectReference(serialized, "doorSideBPanel", sideB != null ? sideB.gameObject : null);

            if (sideA != null)
            {
                changed |= SetObjectReference(serialized, "doorHeadlineText", FindChildText(sideA.transform, "Door Panel Title"));
                changed |= SetObjectReference(serialized, "doorStatusText", FindChildText(sideA.transform, "Door Panel Status"));
                changed |= SetObjectReference(serialized, "doorTimerText", FindChildText(sideA.transform, "Door Panel Timer"));
                changed |= SetObjectReference(serialized, "destinationText", FindChildText(sideA.transform, "Door Panel Destination"));
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            return changed;
        }

        private static bool EnsureLuggageRamp(Scene scene)
        {
            Transform taxi = FindTransform(scene, "Cocoon Autonomous Taxi");
            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (taxi == null || stateMachine == null)
            {
                return false;
            }

            bool changed = false;
            Transform ramp = FindTransformDeep(taxi, LuggageRampName);
            if (ramp == null)
            {
                Transform fixtureRoot = FindTransformDeep(taxi, "Cocoon Interaction Fixtures");
                Transform parent = fixtureRoot != null ? fixtureRoot : taxi;
                GameObject rampObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rampObject.name = LuggageRampName;
                ramp = rampObject.transform;
                ramp.SetParent(parent, false);
                ramp.localPosition = Vector3.zero;
                ramp.localRotation = Quaternion.identity;
                ramp.localScale = new Vector3(0.85f, 0.035f, 0.48f);
                rampObject.SetActive(true);

                Renderer renderer = rampObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = LoadMaterial("LuggageRamp", new Color(0.055f, 0.07f, 0.075f));
                }

                changed = true;
            }

            if (ramp != null && ramp.GetComponent<CocoonLuggageRamp>() == null)
            {
                ramp.gameObject.AddComponent<CocoonLuggageRamp>();
                changed = true;
            }

            var serialized = new SerializedObject(stateMachine);
            changed |= SetObjectReference(serialized, "luggageRamp", ramp);
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            return changed;
        }

        private static bool CleanupDeprecatedBoardingObjects(Scene scene)
        {
            bool changed = false;
            changed |= DestroyNamedTransform(scene, DoorSideAPanelName);
            changed |= DestroyNamedTransform(scene, DoorSideBPanelName);
            changed |= DestroyNamedTransform(scene, DoorSidePanelAnchorRootName);
            changed |= DestroyNamedTransform(scene, "Door-side Onboarding UI");
            changed |= DestroyNamedTransform(scene, "Cocoon Interaction Fixtures");
            changed |= DestroyNamedTransform(scene, LuggageRampName);
            changed |= DestroyNamedTransform(scene, "Passenger Door Hinge");
            changed |= DestroyNamedTransform(scene, "Contactless Payment Reader");
            return changed;
        }

        private static bool EnsureNewBoardingAnimationBindings(Scene scene)
        {
            Transform taxi = FindTransform(scene, "Cocoon Autonomous Taxi");
            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (taxi == null || stateMachine == null)
            {
                return false;
            }

            bool changed = false;
            Transform doorL1 = FindTransformDeep(taxi, LeftDoorPanel1Name);
            Transform doorL2 = FindTransformDeep(taxi, LeftDoorPanel2Name);
            Transform interior = FindTransformDeep(taxi, FinalTaxiInteriorStructureName);
            Transform touchZone = FindTransformDeep(interior, BoardingTouchZoneName) ?? FindTransformDeep(taxi, BoardingTouchZoneName);
            RectTransform boardingDoorSurfacePanel = EnsureBoardingDoorSurfacePanel(taxi, interior, touchZone, out bool createdBoardingDoorSurfacePanel);
            changed |= createdBoardingDoorSurfacePanel;
            Transform ramp1 = FindTransformDeep(taxi, LuggageRamp1Name);
            Transform ramp2 = FindTransformDeep(taxi, LuggageRamp2Name);
            Transform marker1 = FindTransformDeep(taxi, LuggageRampClosedMarker1Name);
            Transform marker2 = FindTransformDeep(taxi, LuggageRampClosedMarker2Name);
            Transform bagA1 = FindTransformDeep(taxi, LuggageStorageBagA1Name);
            Transform bagA11 = FindTransformDeepAny(taxi, LuggageStorageA11Names);
            Transform bagA12 = FindTransformDeepAny(taxi, LuggageStorageA12Names);
            Transform bagA13 = FindTransformDeepAny(taxi, LuggageStorageA13Names);
            Transform bagA2 = FindTransformDeep(taxi, LuggageStorageBagA2Name);
            Transform bagA21 = FindTransformDeepAny(taxi, LuggageStorageA21Names);
            Transform bagA3 = FindTransformDeep(taxi, LuggageStorageBagA3Name);
            Transform luggagePreventerA1 = FindTransformDeep(taxi, LuggagePreventerA1Name);
            Transform seatAnchor = FindTransformDeep(taxi, Seat1AnchorName);
            Transform seat1 = FindTransformDeep(taxi, Seat1Name);
            Transform seat1RotationTarget = FindTransformDeep(taxi, Seat1RotationTargetName);
            Transform seat1MoveTarget = FindTransformDeepAny(seatAnchor, taxi, Seat1MoveTargetNames);
            Transform seat1SitTarget = FindTransformDeepAny(seatAnchor, taxi, Seat1SitTargetNames);
            Transform legacySeatMoveTarget = FindTransformDeep(seatAnchor, Seat1LegacyMoveTargetName);
            Transform legacySeatSitTarget = FindTransformDeep(seatAnchor, Seat1LegacySitTargetName);
            Transform seatedRiderHeadAnchor = EnsureSeatedRiderHeadAnchor(taxi, seat1, ref changed);
            Transform ro1 = FindTransformDeep(taxi, Ro1Name);
            Transform ro2 = FindTransformDeep(taxi, Ro2Name);
            Transform ro3 = FindTransformDeep(taxi, Ro3Name);

            LogProtectedLuggageWaypointMarkers(bagA11, bagA12, bagA13, bagA21);
            LogProtectedSeatStorageMarkers(seat1, seat1RotationTarget, seat1MoveTarget);
            LogProtectedSeatSitMarkers(seat1SitTarget, seatedRiderHeadAnchor, ro1, ro2, ro3);
            changed |= SetRenderersEnabled(marker1, false);
            changed |= SetRenderersEnabled(marker2, false);
            changed |= SetRenderersEnabled(bagA1, false);
            changed |= SetRenderersEnabled(bagA11, false);
            changed |= SetRenderersEnabled(bagA12, false);
            changed |= SetRenderersEnabled(bagA13, false);
            changed |= SetRenderersEnabled(bagA2, false);
            changed |= SetRenderersEnabled(bagA21, false);
            changed |= SetRenderersEnabled(bagA3, false);
            changed |= SetRenderersEnabled(seat1RotationTarget, false);
            changed |= SetRenderersEnabled(seat1MoveTarget, false);
            changed |= SetRenderersEnabled(seat1SitTarget, false);
            changed |= SetRenderersEnabled(legacySeatMoveTarget, false);
            changed |= SetRenderersEnabled(legacySeatSitTarget, false);

            var serialized = new SerializedObject(stateMachine);
            changed |= SetBool(serialized, "suppressHeadLockedFlowUi", true);
            changed |= EnsureAuthoredLeftDoorTargetAndClosedPose(serialized, doorL1, doorL2);
            changed |= EnsureTriggerCollider(touchZone);
            changed |= EnsureAuthoredRampStandbyPose(serialized, ramp1, 1);
            changed |= EnsureAuthoredRampStandbyPose(serialized, ramp2, 2);
            changed |= EnsureAuthoredLuggagePreventerStowedPose(serialized, luggagePreventerA1);
            changed |= SetObjectReference(serialized, "doorL1", doorL1);
            changed |= SetObjectReference(serialized, "doorL2", doorL2);
            changed |= SetObjectReference(serialized, "boardingTouchZone", touchZone);
            changed |= SetObjectReference(serialized, "boardingDoorSurfacePanel", boardingDoorSurfacePanel);
            changed |= SetNestedBool(serialized, "doorUiFollowsDoorL2", true);
            changed |= SetObjectReference(serialized, "luggageBoardingRamp1", ramp1);
            changed |= SetObjectReference(serialized, "luggageBoardingRamp2", ramp2);
            changed |= SetObjectReference(serialized, "luggageRampClosedMarker1", marker1);
            changed |= SetObjectReference(serialized, "luggageRampClosedMarker2", marker2);
            changed |= SetObjectReference(serialized, "luggageStorageBagA1", bagA1);
            changed |= SetObjectReference(serialized, "luggageStorageA11", bagA11);
            changed |= SetObjectReference(serialized, "luggageStorageA12", bagA12);
            changed |= SetObjectReference(serialized, "luggageStorageA13", bagA13);
            changed |= SetObjectReference(serialized, "luggageStorageBagA2", bagA2);
            changed |= SetObjectReference(serialized, "luggageStorageA21", bagA21);
            changed |= SetObjectReference(serialized, "luggageStorageBagA3", bagA3);
            changed |= SetObjectReference(serialized, "luggagePreventerA1", luggagePreventerA1);
            changed |= SetObjectReference(serialized, "seat1", seat1);
            changed |= SetObjectReference(serialized, "seat1RotationTarget", seat1RotationTarget);
            changed |= SetObjectReference(serialized, "seat1MoveTarget", seat1MoveTarget);
            changed |= SetObjectReference(serialized, "seat1SitTarget", seat1SitTarget);
            changed |= SetObjectReference(serialized, "seatedRiderHeadAnchor", seatedRiderHeadAnchor);
            changed |= SetObjectReference(serialized, "ro1", ro1);
            changed |= SetObjectReference(serialized, "ro2", ro2);
            changed |= SetObjectReference(serialized, "ro3", ro3);
            changed |= SetFloat(serialized, "luggageCabinLiftDuration", LuggageCabinLiftDuration);
            changed |= SetFloat(serialized, "luggageStorageA1ToA11Duration", LuggageStorageA1ToA11Duration);
            changed |= SetFloat(serialized, "luggageStorageA11ToA12Duration", LuggageStorageA11ToA12Duration);
            changed |= SetFloat(serialized, "luggageStorageA12ToA13Duration", LuggageStorageA12ToA13Duration);
            changed |= SetFloat(serialized, "luggageStorageA13ToA2Duration", LuggageStorageA13ToA2Duration);
            changed |= SetFloat(serialized, "luggageStorageA2ToA21Duration", LuggageStorageA2ToA21Duration);
            changed |= SetFloat(serialized, "luggageStorageA21ToA3Duration", LuggageStorageA21ToA3Duration);
            changed |= SetFloat(serialized, "luggagePreventerRaiseDuration", LuggagePreventerRaiseDuration);
            changed |= SetFloat(serialized, "seat1RotateDuration", Seat1RotateDuration);
            changed |= SetFloat(serialized, "seat1MoveDuration", Seat1MoveDuration);
            changed |= SetFloat(serialized, "sitPromptSeconds", SitPromptSeconds);
            changed |= SetFloat(serialized, "seatMoveRiderToAnchorDuration", SeatMoveRiderToAnchorDuration);
            changed |= SetFloat(serialized, "seatSitRotateDuration", SeatSitRotateDuration);
            changed |= SetFloat(serialized, "boardingPromptVisibleSeconds", BoardingPromptVisibleSeconds);
            changed |= SetFloat(serialized, "boardingTimeoutSeconds", BoardingTimeoutSeconds);
            changed |= SetFloat(serialized, "roFoldDuration", RoFoldDuration);
            changed |= SetFloat(serialized, "roFoldLocalYDegrees", RoFoldLocalYDegrees);
            changed |= SetObjectReference(serialized, "doorHinge", null);
            changed |= SetObjectReference(serialized, "doorPanel", null);
            changed |= SetObjectReference(serialized, "doorSideAPanel", null);
            changed |= SetObjectReference(serialized, "doorSideBPanel", null);
            changed |= SetObjectReference(serialized, "paymentReader", null);
            changed |= SetObjectReference(serialized, "paymentReaderRenderer", null);

            SerializedProperty doorRayPointer = serialized.FindProperty("useDoorPanelRayPointer");
            if (doorRayPointer != null && doorRayPointer.boolValue)
            {
                doorRayPointer.boolValue = false;
                changed = true;
            }

            SerializedProperty bodyLightVisualsProperty = serialized.FindProperty("enableBodyLightVisuals");
            if (bodyLightVisualsProperty != null && bodyLightVisualsProperty.boolValue)
            {
                bodyLightVisualsProperty.boolValue = false;
                changed = true;
            }

            SerializedProperty lightProperty = serialized.FindProperty("lightRenderers");
            if (lightProperty != null && lightProperty.arraySize != 0)
            {
                lightProperty.arraySize = 0;
                changed = true;
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            return changed;
        }

        private static RectTransform EnsureBoardingDoorSurfacePanel(Transform taxi, Transform interior, Transform touchZone, out bool created)
        {
            created = false;
            Transform parent = interior != null ? interior : taxi;
            if (parent == null)
            {
                return null;
            }

            RectTransform existing = FindRectTransform(parent, BoardingDoorSurfacePanelName);
            if (existing == null && taxi != null)
            {
                existing = FindRectTransform(taxi, BoardingDoorSurfacePanelName);
            }

            if (existing != null)
            {
                return existing;
            }

            Texture2D idleTexture = Resources.Load<Texture2D>(BoardingDoorIdleScreenResourcePath);
            float aspect = idleTexture != null && idleTexture.height > 0 ? (float)idleTexture.width / idleTexture.height : 1f;
            var panelObject = new GameObject(BoardingDoorSurfacePanelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            panel.sizeDelta = new Vector2(CabinPanelReferenceWidth, CabinPanelReferenceWidth / Mathf.Max(0.001f, aspect));
            if (touchZone != null)
            {
                panel.position = touchZone.position;
                panel.rotation = touchZone.rotation;
            }

            SetWorldScale(panel, Vector3.one * (0.14f / CabinPanelReferenceWidth));

            Canvas canvas = panelObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                canvas.worldCamera = mainCamera;
            }

            CanvasScaler scaler = panelObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            GraphicRaycaster raycaster = panelObject.GetComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = true;

            RawImage image = EnsureDoorSurfaceImage(panel);
            image.texture = idleTexture;
            image.raycastTarget = false;
            panelObject.SetActive(false);
            created = true;
            return panel;
        }

        private static RawImage EnsureDoorSurfaceImage(RectTransform panel)
        {
            Transform imageTransform = panel.Find(BoardingDoorSurfaceImageName);
            RawImage image = imageTransform != null ? imageTransform.GetComponent<RawImage>() : null;
            if (image != null)
            {
                return image;
            }

            var imageObject = new GameObject(BoardingDoorSurfaceImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(panel, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.localPosition = Vector3.zero;
            imageRect.localRotation = Quaternion.identity;
            imageRect.localScale = Vector3.one;

            image = imageObject.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static bool EnsureSeatV2System(Scene scene, Transform taxi)
        {
            if (taxi == null)
            {
                return false;
            }

            bool changed = false;
            Transform taxiPodVisuals = taxi.Find("Taxi Pod Visuals");
            if (taxiPodVisuals == null)
            {
                taxiPodVisuals = FindTransformDeep(taxi, "Taxi Pod Visuals");
            }

            if (taxiPodVisuals == null)
            {
                return false;
            }

            Transform assembly = taxiPodVisuals.Find(SeatV2AssemblyName);
            if (assembly == null)
            {
                GameObject assemblyObject = new GameObject(SeatV2AssemblyName);
                assembly = assemblyObject.transform;
                assembly.SetParent(taxiPodVisuals, false);
                assembly.localPosition = Vector3.zero;
                assembly.localRotation = Quaternion.identity;
                assembly.localScale = Vector3.one;
                changed = true;
            }

            CocoonSeatV2Controller controller = assembly.GetComponent<CocoonSeatV2Controller>();
            if (controller == null)
            {
                controller = assembly.gameObject.AddComponent<CocoonSeatV2Controller>();
                changed = true;
            }

            Transform body = assembly.Find(SeatV2BodyName);
            if (body == null)
            {
                GameObject modelAsset = LoadSeatV2ModelAsset();

                if (modelAsset != null)
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                    if (instance != null)
                    {
                        instance.name = SeatV2BodyName;
                        body = instance.transform;
                        body.SetParent(assembly, false);
                        body.localPosition = Vector3.zero;
                        body.localRotation = SeatV2DefaultRotation;
                        body.localScale = SeatV2DefaultScale;
                        changed = true;
                    }
                }
                else
                {
                    Debug.LogWarning("Cocoon Seat V2 model not found at " + SeatV2ModelPath + ". Copy seat_obj.obj/.mtl into that folder and re-run repair.");
                }
            }

            changed |= EnsureSeatV2LatestVisual(body);

            Transform seatOutTarget =
                FindTransformDeep(assembly, SeatV2OutTargetName) ??
                FindTransformDeep(assembly, SeatV2OutTargetAltName) ??
                EnsureSeatV2Marker(assembly, SeatV2OutTargetName, body, false, ref changed);
            Transform seatBackTarget =
                FindTransformDeep(assembly, SeatV2BackTargetName) ??
                FindTransformDeep(assembly, SeatV2BackTargetAltName) ??
                EnsureSeatV2Marker(assembly, SeatV2BackTargetName, body, false, ref changed);
            Transform idlePose = FindTransformDeep(assembly, SeatV2IdlePoseName);
            Transform storageTarget = FindTransformDeep(assembly, SeatV2StorageTargetName);
            Transform seatedPose = FindTransformDeep(assembly, SeatV2SeatedPoseName);
            Transform seatedRotateTarget = FindTransformDeep(assembly, SeatV2SeatedRotateTargetName);
            Transform backPivot = EnsureSeatV2Marker(body != null ? body : assembly, SeatV2BackPivotName, body, true, ref changed);
            Transform leftArmPivot = EnsureSeatV2Marker(body != null ? body : assembly, SeatV2ArmLPivotName, body, true, ref changed);
            Transform rightArmPivot = EnsureSeatV2Marker(body != null ? body : assembly, SeatV2ArmRPivotName, body, true, ref changed);
            Transform footrestPivot = EnsureSeatV2Marker(body != null ? body : assembly, SeatV2FootrestPivotName, body, true, ref changed);
            Transform seatedHeadAnchor =
                FindTransformDeep(body, SeatedRiderHeadAnchorName) ??
                FindTransformDeep(body, SeatV2SeatedHeadAnchorName) ??
                FindTransformDeep(assembly, SeatedRiderHeadAnchorName) ??
                FindTransformDeep(assembly, SeatV2SeatedHeadAnchorName) ??
                EnsureSeatV2Anchor(body != null ? body : assembly, SeatedRiderHeadAnchorName, new Vector3(0f, 55f, 150f), Quaternion.Euler(0f, 0f, -90f), ref changed);
            Transform miniAnchor =
                FindTransformDeep(body, MiniScreenName) ??
                FindTransformDeep(body, SeatV2MiniScreenAnchorName) ??
                FindTransformDeep(assembly, MiniScreenName) ??
                FindTransformDeep(assembly, SeatV2MiniScreenAnchorName) ??
                EnsureSeatV2Anchor(body != null ? body : assembly, MiniScreenName, new Vector3(0f, 25f, 95f), Quaternion.identity, ref changed);

            changed |= SetRenderersEnabled(idlePose, false);
            changed |= SetRenderersEnabled(storageTarget, false);
            changed |= SetRenderersEnabled(seatedPose, false);
            changed |= SetRenderersEnabled(seatedRotateTarget, false);
            changed |= SetRenderersEnabled(seatOutTarget, false);
            changed |= SetRenderersEnabled(seatBackTarget, false);
            if (miniAnchor != null && miniAnchor.name != MiniScreenName)
            {
                changed |= SetRenderersEnabled(miniAnchor, false);
            }

            var controllerSerialized = new SerializedObject(controller);
            changed |= SetObjectReference(controllerSerialized, "seatAssembly", assembly);
            changed |= SetObjectReference(controllerSerialized, "seatBody", body);
            changed |= SetObjectReference(controllerSerialized, "seatOutTarget", seatOutTarget);
            changed |= SetObjectReference(controllerSerialized, "seatBackTarget", seatBackTarget);
            changed |= SetObjectReference(controllerSerialized, "idlePose", idlePose);
            changed |= SetObjectReference(controllerSerialized, "storageTarget", storageTarget);
            changed |= SetObjectReference(controllerSerialized, "seatedPose", seatedPose);
            changed |= SetObjectReference(controllerSerialized, "seatedRotateTarget", seatedRotateTarget);
            changed |= SetObjectReference(controllerSerialized, "seatedRiderHeadAnchor", seatedHeadAnchor);
            changed |= SetObjectReference(controllerSerialized, "miniScreenAnchor", miniAnchor);
            changed |= SetObjectReference(controllerSerialized, "backPivot", backPivot);
            changed |= SetObjectReference(controllerSerialized, "leftArmPivot", leftArmPivot);
            changed |= SetObjectReference(controllerSerialized, "rightArmPivot", rightArmPivot);
            changed |= SetObjectReference(controllerSerialized, "footrestPivot", footrestPivot);
            if (controllerSerialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (stateMachine != null)
            {
                var stateMachineSerialized = new SerializedObject(stateMachine);
                changed |= SetObjectReference(stateMachineSerialized, "seatV2Controller", controller);
                if (stateMachineSerialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    changed = true;
                }
            }

            CocoonCabinScreenController cabinController = taxi.GetComponent<CocoonCabinScreenController>();
            if (cabinController != null)
            {
                var cabinSerialized = new SerializedObject(cabinController);
                changed |= SetObjectReference(cabinSerialized, "miniScreenAnchorOverride", miniAnchor);
                if (cabinSerialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    changed = true;
                }
            }

            Debug.Log("Cocoon Seat V2 protected: assembly=" + GetMarkerPathOrMissing(assembly) +
                      ", body=" + GetMarkerPathOrMissing(body) +
                      ", outTarget=" + GetMarkerPathOrMissing(seatOutTarget) +
                      ", backTarget=" + GetMarkerPathOrMissing(seatBackTarget) +
                      ", headAnchor=" + GetMarkerPathOrMissing(seatedHeadAnchor) +
                      ", miniAnchor=" + GetMarkerPathOrMissing(miniAnchor) +
                      ". Existing transforms are preserved.");

            return changed;
        }

        private static GameObject LoadSeatV2ModelAsset()
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SeatV2ModelPath);
            if (modelAsset == null)
            {
                AssetDatabase.ImportAsset(SeatV2ModelPath, ImportAssetOptions.ForceUpdate);
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SeatV2ModelPath);
            }

            if (modelAsset != null)
            {
                return modelAsset;
            }

            modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SeatV2LegacyModelPath);
            if (modelAsset == null)
            {
                AssetDatabase.ImportAsset(SeatV2LegacyModelPath, ImportAssetOptions.ForceUpdate);
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SeatV2LegacyModelPath);
            }

            return modelAsset;
        }

        private static bool EnsureSeatV2LatestVisual(Transform body)
        {
            if (body == null)
            {
                return false;
            }

            Transform existingLatestVisual = body.Find(SeatV2VisualName);
            if (existingLatestVisual != null)
            {
                return DisableLegacySeatV2VisualChildren(body, existingLatestVisual);
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SeatV2ModelPath);
            if (modelAsset == null)
            {
                AssetDatabase.ImportAsset(SeatV2ModelPath, ImportAssetOptions.ForceUpdate);
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SeatV2ModelPath);
            }

            if (modelAsset == null)
            {
                Debug.LogWarning("Cocoon Seat V2 latest visual source missing at " + SeatV2ModelPath + ". Existing Seat V2 Body children were preserved.");
                return false;
            }

            Transform oldVisual = FindSeatV2VisualChild(body);
            Vector3 localPosition = oldVisual != null ? oldVisual.localPosition : Vector3.zero;
            Quaternion localRotation = oldVisual != null ? oldVisual.localRotation : Quaternion.identity;
            Vector3 localScale = oldVisual != null ? oldVisual.localScale : Vector3.one;

            GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (instance == null)
            {
                return false;
            }

            Transform visual = instance.transform;
            visual.name = SeatV2VisualName;
            visual.SetParent(body, false);
            visual.localPosition = localPosition;
            visual.localRotation = localRotation;
            visual.localScale = localScale;

            if (oldVisual != null)
            {
                oldVisual.gameObject.SetActive(false);
            }

            Debug.Log("Cocoon Seat V2 latest visual installed under " + GetMarkerPathOrMissing(body) +
                      ". Body transform and author markers were preserved.");
            return true;
        }

        private static bool DisableLegacySeatV2VisualChildren(Transform body, Transform preservedVisual)
        {
            if (body == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < body.childCount; i++)
            {
                Transform child = body.GetChild(i);
                if (child == null || child == preservedVisual || IsSeatV2AuthoringChild(child))
                {
                    continue;
                }

                if (child.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                    changed = true;
                }
            }

            return changed;
        }

        private static Transform FindSeatV2VisualChild(Transform body)
        {
            if (body == null)
            {
                return null;
            }

            for (int i = 0; i < body.childCount; i++)
            {
                Transform child = body.GetChild(i);
                if (child == null || string.Equals(child.name, SeatV2VisualName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsSeatV2AuthoringChild(child))
                {
                    continue;
                }

                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsSeatV2AuthoringChild(Transform child)
        {
            if (child == null)
            {
                return true;
            }

            string name = child.name;
            if (string.Equals(name, MiniScreenName, StringComparison.Ordinal) ||
                string.Equals(name, CabinMiniPanelName, StringComparison.Ordinal) ||
                string.Equals(name, SeatedRiderHeadAnchorName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2OutTargetName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2OutTargetAltName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2BackTargetName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2BackTargetAltName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2IdlePoseName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2StorageTargetName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2SeatedPoseName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2SeatedRotateTargetName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2SeatedHeadAnchorName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2MiniScreenAnchorName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2BackPivotName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2ArmLPivotName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2ArmRPivotName, StringComparison.Ordinal) ||
                string.Equals(name, SeatV2FootrestPivotName, StringComparison.Ordinal))
            {
                return true;
            }

            return name.StartsWith("SeatV2 ", StringComparison.Ordinal) ||
                   name.StartsWith("Seat V2 Bod_", StringComparison.Ordinal) ||
                   name.StartsWith("Cabin Mini UI", StringComparison.Ordinal);
        }

        private static Transform EnsureSeatV2Marker(Transform parent, string markerName, Transform reference, bool childLocalIdentity, ref bool changed)
        {
            if (parent == null)
            {
                return null;
            }

            Transform marker = parent.Find(markerName);
            if (marker != null)
            {
                return marker;
            }

            GameObject markerObject = new GameObject(markerName);
            marker = markerObject.transform;
            marker.SetParent(parent, false);
            if (childLocalIdentity || reference == null)
            {
                marker.localPosition = Vector3.zero;
                marker.localRotation = Quaternion.identity;
                marker.localScale = Vector3.one;
            }
            else
            {
                marker.position = reference.position;
                marker.rotation = reference.rotation;
                SetWorldScale(marker, reference.lossyScale);
            }

            changed = true;
            return marker;
        }

        private static Transform EnsureSeatV2Anchor(Transform parent, string anchorName, Vector3 fallbackLocalPosition, Quaternion fallbackLocalRotation, ref bool changed)
        {
            if (parent == null)
            {
                return null;
            }

            Transform anchor = parent.Find(anchorName);
            if (anchor != null)
            {
                return anchor;
            }

            GameObject anchorObject = new GameObject(anchorName);
            anchor = anchorObject.transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = fallbackLocalPosition;
            anchor.localRotation = fallbackLocalRotation;
            anchor.localScale = Vector3.one;
            changed = true;
            return anchor;
        }

        private static Transform EnsureSeatedRiderHeadAnchor(Transform taxi, Transform seat1, ref bool changed)
        {
            Transform existing = FindTransformDeep(taxi, SeatedRiderHeadAnchorName);
            if (existing != null)
            {
                return existing;
            }

            Transform parent = seat1 != null ? seat1 : taxi;
            if (parent == null)
            {
                return null;
            }

            var anchor = new GameObject(SeatedRiderHeadAnchorName);
            Transform anchorTransform = anchor.transform;
            anchorTransform.SetParent(parent, false);
            anchorTransform.localPosition = new Vector3(0f, 0.16f, 0.02f);
            anchorTransform.localRotation = Quaternion.identity;
            anchorTransform.localScale = Vector3.one;
            changed = true;
            Debug.Log("Cocoon repair created Seated Rider Head Anchor. Adjust it in Inspector for the seated camera point.", anchor);
            return anchorTransform;
        }

        private static bool EnsureAuthoredLeftDoorTargetAndClosedPose(SerializedObject serialized, Transform doorL1, Transform doorL2)
        {
            if (serialized == null || doorL1 == null || doorL2 == null)
            {
                return false;
            }

            bool changed = false;
            Vector3 doorL2Target = doorL2.localPosition;
            SerializedProperty hasDoorTargetProperty = serialized.FindProperty("hasAuthoredLeftDoorOpenPose");
            bool hasDoorTarget = hasDoorTargetProperty != null && hasDoorTargetProperty.boolValue;

            if (doorL2Target.sqrMagnitude > 0.000001f || !hasDoorTarget)
            {
                if (doorL2Target.sqrMagnitude <= 0.000001f)
                {
                    SerializedProperty savedDoorL2 = serialized.FindProperty("doorL2OpenLocalPosition");
                    doorL2Target = savedDoorL2 != null && savedDoorL2.vector3Value.sqrMagnitude > 0.000001f
                        ? savedDoorL2.vector3Value
                        : new Vector3(-6.8f, 73.8f, 0f);
                }

                changed |= SetVector3(serialized, "doorL2OpenLocalPosition", doorL2Target);
                changed |= SetVector3(serialized, "doorL1OpenLocalPosition", new Vector3(doorL2Target.x, -doorL2Target.y, doorL2Target.z));
                if (hasDoorTargetProperty != null && !hasDoorTargetProperty.boolValue)
                {
                    hasDoorTargetProperty.boolValue = true;
                    changed = true;
                }
            }

            if (doorL1.localPosition.sqrMagnitude > 0.000001f)
            {
                doorL1.localPosition = Vector3.zero;
                changed = true;
            }

            if (doorL2.localPosition.sqrMagnitude > 0.000001f)
            {
                doorL2.localPosition = Vector3.zero;
                changed = true;
            }

            return changed;
        }

        private static bool EnsureAuthoredRampStandbyPose(SerializedObject serialized, Transform ramp, int rampIndex)
        {
            if (serialized == null || ramp == null)
            {
                return false;
            }

            bool changed = false;
            SerializedProperty hasStandbyProperty = serialized.FindProperty("hasAuthoredLuggageRampStandbyPose");
            bool hasStandby = hasStandbyProperty != null && hasStandbyProperty.boolValue;
            if (!hasStandby || ramp.localPosition.sqrMagnitude > 0.000001f)
            {
                changed |= SetVector3(serialized, "luggageRamp" + rampIndex + "StandbyLocalPosition", ramp.localPosition);
                changed |= SetQuaternion(serialized, "luggageRamp" + rampIndex + "StandbyLocalRotation", ramp.localRotation);
                changed |= SetVector3(serialized, "luggageRamp" + rampIndex + "StandbyLocalScale", ramp.localScale);
                if (hasStandbyProperty != null && !hasStandbyProperty.boolValue)
                {
                    hasStandbyProperty.boolValue = true;
                    changed = true;
                }
            }

            return changed;
        }

        private static void LogProtectedLuggageWaypointMarkers(Transform bagA11, Transform bagA12, Transform bagA13, Transform bagA21)
        {
            Debug.Log(
                "Cocoon repair protected luggage waypoint marker transforms: " +
                "A1.1=" + GetMarkerPathOrMissing(bagA11) +
                ", A1.2=" + GetMarkerPathOrMissing(bagA12) +
                ", A1.3=" + GetMarkerPathOrMissing(bagA13) +
                ", A2.1=" + GetMarkerPathOrMissing(bagA21) +
                ". Repair only binds references and hides marker renderers.");
        }

        private static void LogProtectedSeatStorageMarkers(Transform seat1, Transform seat1RotationTarget, Transform seat1MoveTarget)
        {
            Debug.Log(
                "Cocoon repair protected seat storage markers: " +
                "seat1=" + GetMarkerPathOrMissing(seat1) +
                ", seat1_=" + GetMarkerPathOrMissing(seat1RotationTarget) +
                ", 1x=" + GetMarkerPathOrMissing(seat1MoveTarget) +
                ". Repair only binds references and hides target marker renderers.");
        }

        private static void LogProtectedSeatSitMarkers(Transform seat1SitTarget, Transform seatedRiderHeadAnchor, Transform ro1, Transform ro2, Transform ro3)
        {
            Debug.Log(
                "Cocoon repair protected seat-down markers: " +
                "1x_=" + GetMarkerPathOrMissing(seat1SitTarget) +
                ", seatedHeadAnchor=" + GetMarkerPathOrMissing(seatedRiderHeadAnchor) +
                ", ro1=" + GetMarkerPathOrMissing(ro1) +
                ", ro2=" + GetMarkerPathOrMissing(ro2) +
                ", ro3=" + GetMarkerPathOrMissing(ro3) +
                ". Repair only binds references and hides marker renderers.");
        }

        private static string GetMarkerPathOrMissing(Transform marker)
        {
            return marker != null ? GetTransformPath(marker) : "missing";
        }

        private static bool EnsureAuthoredLuggagePreventerStowedPose(SerializedObject serialized, Transform preventer)
        {
            if (serialized == null || preventer == null)
            {
                return false;
            }

            bool changed = false;
            SerializedProperty hasPoseProperty = serialized.FindProperty("hasAuthoredLuggagePreventerStowedPose");
            bool hasPose = hasPoseProperty != null && hasPoseProperty.boolValue;
            if (!hasPose || preventer.localPosition.sqrMagnitude > 0.000001f)
            {
                changed |= SetVector3(serialized, "luggagePreventerStowedLocalPosition", preventer.localPosition);
                changed |= SetQuaternion(serialized, "luggagePreventerStowedLocalRotation", preventer.localRotation);
                changed |= SetVector3(serialized, "luggagePreventerStowedLocalScale", preventer.localScale);
                if (hasPoseProperty != null && !hasPoseProperty.boolValue)
                {
                    hasPoseProperty.boolValue = true;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool EnsureTaxiWheelAnimator(Scene scene)
        {
            Transform taxi = FindTransform(scene, "Cocoon Autonomous Taxi");
            if (taxi == null)
            {
                return false;
            }

            bool changed = false;
            changed |= RemoveNonTaxiWheelAnimators(scene, taxi);
            CocoonWheelAnimator animator = taxi.GetComponent<CocoonWheelAnimator>();
            if (animator == null)
            {
                animator = taxi.gameObject.AddComponent<CocoonWheelAnimator>();
                changed = true;
            }

            var serialized = new SerializedObject(animator);
            changed |= SetObjectReference(serialized, "vehicleRoot", taxi);
            changed |= SetObjectReference(serialized, "wheel1", FindTransformDeep(taxi, Wheel1Name));
            changed |= SetObjectReference(serialized, "wheel2", FindTransformDeep(taxi, Wheel2Name));
            changed |= SetObjectReference(serialized, "wheel3", FindTransformDeep(taxi, Wheel3Name));
            changed |= SetObjectReference(serialized, "wheel4", FindTransformDeep(taxi, Wheel4Name));

            SerializedProperty autoBindProperty = serialized.FindProperty("autoBindByName");
            if (autoBindProperty != null && !autoBindProperty.boolValue)
            {
                autoBindProperty.boolValue = true;
                changed = true;
            }

            SerializedProperty snapProperty = serialized.FindProperty("snapBottomsOnStart");
            if (snapProperty != null && !snapProperty.boolValue)
            {
                snapProperty.boolValue = true;
                changed = true;
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            return changed;
        }

        private static bool RepairTaxiVisualMaterials(Transform taxi)
        {
            if (taxi == null)
            {
                return false;
            }

            bool changed = false;
            Material tireMaterial = LoadMaterial("Tires", new Color(0.006f, 0.006f, 0.007f, 1f));
            changed |= ConfigureTireMaterial(tireMaterial);

            Transform wheel1 = FindTransformDeep(taxi, Wheel1Name);
            Transform wheel2 = FindTransformDeep(taxi, Wheel2Name);
            Transform wheel3 = FindTransformDeep(taxi, Wheel3Name);
            Transform wheel4 = FindTransformDeep(taxi, Wheel4Name);

            Renderer[] renderers = taxi.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (IsDescendantOrSelf(renderer.transform, wheel1) ||
                    IsDescendantOrSelf(renderer.transform, wheel2) ||
                    IsDescendantOrSelf(renderer.transform, wheel3) ||
                    IsDescendantOrSelf(renderer.transform, wheel4))
                {
                    changed |= ReplaceRendererMaterials(renderer, tireMaterial, replaceAllSlots: true);
                    continue;
                }
            }

            return changed;
        }

        private static bool ReplaceRendererMaterials(Renderer renderer, Material material, bool replaceAllSlots)
        {
            if (renderer == null || material == null)
            {
                return false;
            }

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (replaceAllSlots && materials[i] != material)
                {
                    materials[i] = material;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }

            return changed;
        }

        private static bool ReplaceGlassMaterialSlots(Renderer renderer, Material glassMaterial)
        {
            if (renderer == null || glassMaterial == null)
            {
                return false;
            }

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (IsGlassMaterial(materials[i]) && materials[i] != glassMaterial)
                {
                    materials[i] = glassMaterial;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }

            return changed;
        }

        private static bool IsGlassMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            string name = material.name ?? string.Empty;
            if (name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("\u73BB\u7483", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("鐜", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("\u9438", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("\u7487", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static bool ConfigureTransparentGlassMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetOverrideTag("RenderType", "Transparent");
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.96f);
            SetColorIfPresent(material, "_BaseColor", new Color(0.035f, 0.07f, 0.085f, 0.28f));
            SetColorIfPresent(material, "_Color", new Color(0.035f, 0.07f, 0.085f, 0.28f));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return true;
        }

        private static bool ConfigureTireMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetOverrideTag("RenderType", "Opaque");
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.24f);
            SetColorIfPresent(material, "_BaseColor", new Color(0.006f, 0.006f, 0.007f, 1f));
            SetColorIfPresent(material, "_Color", new Color(0.006f, 0.006f, 0.007f, 1f));
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return true;
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static bool IsDescendantOrSelf(Transform candidate, Transform root)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current == root)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool RemoveNonTaxiWheelAnimators(Scene scene, Transform taxi)
        {
            bool changed = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                CocoonWheelAnimator[] animators = roots[rootIndex].GetComponentsInChildren<CocoonWheelAnimator>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    CocoonWheelAnimator animator = animators[i];
                    if (animator == null || animator.transform == taxi)
                    {
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(animator);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool DestroyNamedTransform(Scene scene, string name)
        {
            bool changed = false;
            List<Transform> matches = new List<Transform>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i] != null && string.Equals(transforms[i].name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(transforms[i]);
                    }
                }
            }

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                Transform match = matches[i];
                if (match == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(match.gameObject);
                changed = true;
            }

            return changed;
        }

        private static bool EnsureTriggerCollider(Transform zone)
        {
            if (zone == null)
            {
                return false;
            }

            bool changed = false;
            if (!zone.gameObject.activeSelf)
            {
                zone.gameObject.SetActive(true);
                changed = true;
            }

            Collider collider = zone.GetComponent<Collider>();
            if (collider == null)
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
                    box.size = Vector3.one * 0.25f;
                }

                collider = box;
                changed = true;
            }

            if (!collider.isTrigger)
            {
                collider.isTrigger = true;
                changed = true;
            }

            if (!collider.enabled)
            {
                collider.enabled = true;
                changed = true;
            }

            return changed;
        }

        private static bool SetRenderersEnabled(Transform root, bool enabled)
        {
            if (root == null)
            {
                return false;
            }

            bool changed = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled != enabled)
                {
                    renderers[i].enabled = enabled;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool SetGameObjectActive(Component component, bool active)
        {
            return component != null && SetGameObjectActive(component.gameObject, active);
        }

        private static bool SetGameObjectActive(GameObject gameObject, bool active)
        {
            if (gameObject == null || gameObject.activeSelf == active)
            {
                return false;
            }

            gameObject.SetActive(active);
            return true;
        }

        private static MeshRenderer EnsureUnifiedDisplayRenderer(Transform parent, string displayName, ref bool changed)
        {
            if (parent == null)
            {
                return null;
            }

            Transform existing = FindTransformDeep(parent, displayName);
            GameObject displayObject;
            if (existing != null)
            {
                displayObject = existing.gameObject;
            }
            else
            {
                displayObject = new GameObject(displayName);
                displayObject.transform.SetParent(parent, false);
                changed = true;
            }

            if (displayObject.transform.parent != parent)
            {
                displayObject.transform.SetParent(parent, false);
                changed = true;
            }

            if (displayObject.transform.localPosition != Vector3.zero)
            {
                displayObject.transform.localPosition = Vector3.zero;
                changed = true;
            }

            if (displayObject.transform.localRotation != Quaternion.identity)
            {
                displayObject.transform.localRotation = Quaternion.identity;
                changed = true;
            }

            if (displayObject.transform.localScale != Vector3.one)
            {
                displayObject.transform.localScale = Vector3.one;
                changed = true;
            }

            if (displayObject.GetComponent<MeshFilter>() == null)
            {
                displayObject.AddComponent<MeshFilter>();
                changed = true;
            }

            MeshRenderer renderer = displayObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = displayObject.AddComponent<MeshRenderer>();
                changed = true;
            }

            if (!displayObject.activeSelf)
            {
                displayObject.SetActive(true);
                changed = true;
            }

            return renderer;
        }

        private static Transform GetOrCreateDoorSidePanelAnchorRoot(Transform taxi, ref bool changed)
        {
            if (taxi == null)
            {
                return null;
            }

            Transform anchorRoot = taxi.Find(DoorSidePanelAnchorRootName);
            if (anchorRoot != null)
            {
                return anchorRoot;
            }

            var anchorObject = new GameObject(DoorSidePanelAnchorRootName);
            anchorRoot = anchorObject.transform;
            anchorRoot.SetParent(taxi, false);
            anchorRoot.localPosition = Vector3.zero;
            anchorRoot.localRotation = Quaternion.identity;
            anchorRoot.localScale = Vector3.one;
            changed = true;
            Debug.Log("Cocoon repair created taxi-relative onboarding UI anchor root.", taxi);
            return anchorRoot;
        }

        private static bool BindDoorSidePanelToTaxi(Transform panel, Transform panelParent)
        {
            if (panel == null || panelParent == null || panel.parent == panelParent)
            {
                return false;
            }

            panel.SetParent(panelParent, true);
            Debug.Log("Cocoon repair bound " + panel.name + " to taxi-relative onboarding UI anchors while preserving its authored world pose.", panel);
            return true;
        }

        private static bool UsesManualBoardingDoorPanel(Scene scene)
        {
            CocoonBoardingDoorUISettings settings = FindComponent<CocoonBoardingDoorUISettings>(scene);
            if (settings == null)
            {
                return true;
            }

            var serialized = new SerializedObject(settings);
            SerializedProperty manualProperty = serialized.FindProperty("useManualPanelTransform");
            return manualProperty == null || manualProperty.boolValue;
        }

        private static bool HasAuthoredFinalTaxiModel(Transform taxi)
        {
            Transform model = taxi != null ? FindTransformDeep(taxi, CocoonImportedTaxiModelBuilder.ModelInstanceName) : null;
            if (model == null)
            {
                return false;
            }

            string activeModelPath = CocoonImportedTaxiModelBuilder.ActiveRenderableModelPath();
            if (string.IsNullOrEmpty(activeModelPath))
            {
                return true;
            }

            string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject);
            if (PathsEqual(sourcePath, activeModelPath))
            {
                return true;
            }

            Debug.Log("Cocoon taxi visuals will be rebuilt: authored Final Taxi Body source is " + (string.IsNullOrEmpty(sourcePath) ? "not a current prefab asset" : sourcePath) + ", active source is " + activeModelPath + ".", model);
            return false;
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            return left.Replace('\\', '/').Equals(right.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasFinalTaxiModelAsset()
        {
            return CocoonImportedTaxiModelBuilder.HasAnyTaxiSourceAsset();
        }

        private static bool EnsureBodyLightVisualsDisabled(Scene scene)
        {
            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (stateMachine == null)
            {
                return false;
            }

            var serialized = new SerializedObject(stateMachine);
            SerializedProperty property = serialized.FindProperty("enableBodyLightVisuals");
            if (property == null || !property.boolValue)
            {
                return false;
            }

            property.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool PreserveAuthoredTrafficVehicleSpawns(Scene scene)
        {
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CocoonTrafficVehicle[] vehicles = root.GetComponentsInChildren<CocoonTrafficVehicle>(true);
                for (int i = 0; i < vehicles.Length; i++)
                {
                    var serialized = new SerializedObject(vehicles[i]);
                    SerializedProperty preserveProperty = serialized.FindProperty("preserveAuthoredSpawnTransform");
                    if (preserveProperty != null && !preserveProperty.boolValue)
                    {
                        preserveProperty.boolValue = true;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static bool RepairAuthoredWindshieldScreens(Scene scene, Transform taxi)
        {
            Texture2D defaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ExteriorScreenAssetPath);
            bool changed = false;
            RectTransform frontPanel = FindRectTransform(taxi, "Taxi Front Windshield Display");
            RectTransform rearPanel = FindRectTransform(taxi, "Taxi Rear Windshield Display");
            Transform interior = FindTransformDeep(taxi, FinalTaxiInteriorStructureName);
            changed |= RepairWindshieldScreenPanel(frontPanel, defaultTexture);
            changed |= RepairWindshieldScreenPanel(rearPanel, defaultTexture);
            changed |= SetGameObjectActive(frontPanel, true);
            changed |= SetGameObjectActive(rearPanel, true);
            changed |= SetGameObjectActive(FindTransformDeep(interior, FrontUnifiedDisplayName), false);
            changed |= SetGameObjectActive(FindTransformDeep(interior, RearUnifiedDisplayName), false);

            Debug.Log(
                "Cocoon repair preserved authored windshield panels: front=" + GetMarkerPathOrMissing(frontPanel) +
                ", rear=" + GetMarkerPathOrMissing(rearPanel) +
                ". Unified led/oled display generation is disabled; panel transforms are left authored.");

            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (stateMachine != null)
            {
                var serialized = new SerializedObject(stateMachine);
                changed |= SetObjectReference(serialized, "windshieldPanel", frontPanel);
                changed |= SetObjectReference(serialized, "rearWindshieldPanel", rearPanel);
                changed |= SetObjectReference(serialized, "windshieldText", null);
                changed |= SetObjectReference(serialized, "rearWindshieldText", null);
                changed |= SetObjectReference(serialized, "frontLed1", null);
                changed |= SetObjectReference(serialized, "frontLed2", null);
                changed |= SetObjectReference(serialized, "frontLed3", null);
                changed |= SetObjectReference(serialized, "rearOled1", null);
                changed |= SetObjectReference(serialized, "rearOled2", null);
                changed |= SetObjectReference(serialized, "rearOled3", null);
                changed |= SetObjectReference(serialized, "frontOled1", null);
                changed |= SetObjectReference(serialized, "frontOled2", null);
                changed |= SetObjectReference(serialized, "frontOled3", null);
                changed |= SetObjectReference(serialized, "frontUnifiedDisplayRenderer", null);
                changed |= SetObjectReference(serialized, "rearUnifiedDisplayRenderer", null);
                if (serialized.hasModifiedProperties)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return changed;
        }

        private static bool RepairCabinScreens(Scene scene, Transform taxi)
        {
            bool changed = ConfigureCabinScreenTextureImportSettings();
            if (taxi == null)
            {
                return changed;
            }

            CocoonCabinScreenController controller = taxi.GetComponent<CocoonCabinScreenController>();
            if (controller == null)
            {
                controller = taxi.gameObject.AddComponent<CocoonCabinScreenController>();
                changed = true;
            }

            Transform interior = FindTransformDeep(taxi, FinalTaxiInteriorStructureName);
            Transform seatAnchor = FindTransformDeep(taxi, Seat1AnchorName);
            Transform seatV2Assembly = FindTransformDeep(taxi, SeatV2AssemblyName);
            Transform seatV2Body = FindTransformDeep(seatV2Assembly, SeatV2BodyName);
            Transform miniScreenOverride = FindTransformDeep(seatV2Body, MiniScreenName) ??
                                           FindTransformDeep(seatV2Body, SeatV2MiniScreenAnchorName) ??
                                           FindTransformDeep(seatV2Assembly, MiniScreenName) ??
                                           FindTransformDeep(seatV2Assembly, SeatV2MiniScreenAnchorName);
            Transform miniScreen = miniScreenOverride != null ? miniScreenOverride : FindTransformDeep(seatAnchor, MiniScreenName);
            if (miniScreen == null)
            {
                miniScreen = FindTransformDeep(taxi, MiniScreenName);
            }

            Transform led1 = FindTransformDeep(interior, FrontLed1Name);
            Transform led2 = FindTransformDeep(interior, FrontLed2Name);
            Transform led3 = FindTransformDeep(interior, FrontLed3Name);
            Transform oled1 = FindTransformDeep(interior, RearOled1Name);
            Transform oled2 = FindTransformDeep(interior, RearOled2Name);
            Transform oled3 = FindTransformDeep(interior, RearOled3Name);

            RectTransform cabinLedEnvPanel = EnsureCabinDisplayPanel(taxi, led1, CabinLedEnvPanelName, CabinLargePanelAspectEnvTrip, CabinLargePanelFallbackWidth, false, null, out bool createdLedEnv);
            RectTransform cabinLedTripPanel = EnsureCabinDisplayPanel(taxi, led2, CabinLedTripPanelName, CabinLargePanelAspectEnvTrip, CabinLargePanelFallbackWidth, false, null, out bool createdLedTrip);
            RectTransform cabinLedMainPanel = EnsureCabinDisplayPanel(taxi, led3, CabinLedMainPanelName, CabinLargePanelAspectMain, CabinLargePanelFallbackWidth, false, null, out bool createdLedMain);
            RectTransform cabinOledEnvPanel = EnsureCabinDisplayPanel(taxi, oled1, CabinOledEnvPanelName, CabinLargePanelAspectEnvTrip, CabinLargePanelFallbackWidth, true, null, out bool createdOledEnv);
            RectTransform cabinOledTripPanel = EnsureCabinDisplayPanel(taxi, oled2, CabinOledTripPanelName, CabinLargePanelAspectEnvTrip, CabinLargePanelFallbackWidth, true, null, out bool createdOledTrip);
            RectTransform cabinOledMainPanel = EnsureCabinDisplayPanel(taxi, oled3, CabinOledMainPanelName, CabinLargePanelAspectMain, CabinLargePanelFallbackWidth, true, null, out bool createdOledMain);
            RectTransform cabinMiniPanel = EnsureSeatV2MiniDisplayPanel(taxi, miniScreen, out bool createdMini);
            changed |= createdLedEnv || createdLedTrip || createdLedMain || createdOledEnv || createdOledTrip || createdOledMain || createdMini;
            RawImage cabinMiniImage = EnsureCabinScreenImage(cabinMiniPanel, CabinMiniPanelName, CabinMiniPanelAspect);
            Texture2D defaultMiniTexture = LoadCabinMiniTextureAsset("main-UI");
            changed |= ApplyDefaultMiniTexture(cabinMiniImage, defaultMiniTexture);

            var serialized = new SerializedObject(controller);
            changed |= SetObjectReference(serialized, "interiorRoot", interior);
            changed |= SetObjectReference(serialized, "led1", led1);
            changed |= SetObjectReference(serialized, "led2", led2);
            changed |= SetObjectReference(serialized, "led3", led3);
            changed |= SetObjectReference(serialized, "oled1", oled1);
            changed |= SetObjectReference(serialized, "oled2", oled2);
            changed |= SetObjectReference(serialized, "oled3", oled3);
            changed |= SetObjectReference(serialized, "miniScreen", miniScreen);
            changed |= SetObjectReference(serialized, "miniScreenAnchorOverride", miniScreenOverride);
            changed |= SetObjectReference(serialized, "cabinLedEnvPanel", cabinLedEnvPanel);
            changed |= SetObjectReference(serialized, "cabinLedTripPanel", cabinLedTripPanel);
            changed |= SetObjectReference(serialized, "cabinLedMainPanel", cabinLedMainPanel);
            changed |= SetObjectReference(serialized, "cabinOledEnvPanel", cabinOledEnvPanel);
            changed |= SetObjectReference(serialized, "cabinOledTripPanel", cabinOledTripPanel);
            changed |= SetObjectReference(serialized, "cabinOledMainPanel", cabinOledMainPanel);
            changed |= SetObjectReference(serialized, "cabinMiniPanel", cabinMiniPanel);
            changed |= SetNestedBool(serialized, "led1Fit.flipX", false);
            changed |= SetNestedBool(serialized, "led2Fit.flipX", false);
            changed |= SetNestedBool(serialized, "led3Fit.flipX", false);
            changed |= SetNestedBool(serialized, "oled1Fit.flipX", false);
            changed |= SetNestedBool(serialized, "oled2Fit.flipX", false);
            changed |= SetNestedBool(serialized, "oled3Fit.flipX", false);
            changed |= SetObjectReference(serialized, "envScreen", LoadCabinTextureAsset("env-screen"));
            changed |= SetObjectReference(serialized, "tripScreen", LoadCabinTextureAsset("trip-screen"));
            changed |= SetObjectReference(serialized, "sideEnvScreen", LoadCabinSideTextureAsset("env-screen"));
            changed |= SetObjectReference(serialized, "sideTripScreen", LoadCabinSideTextureAsset("trip-screen"));
            changed |= SetTextureArrayReferences(serialized, "mainUiScreens", BuildCabinMainTextures());
            changed |= SetTextureArrayReferences(serialized, "miniUiScreens", BuildCabinMiniTextures());
            changed |= SetObjectReference(serialized, "mainDefaultScreen", LoadCabinMainLifecycleTextureAsset("default"));
            changed |= SetObjectReference(serialized, "mainOnboardScreen", LoadCabinMainLifecycleTextureAsset("onboard"));
            changed |= SetObjectReference(serialized, "mainDestinationScreen", LoadCabinMainLifecycleTextureAsset("destination"));
            changed |= SetTextureArrayReferences(serialized, "seatedMainUiScreens", BuildCabinSeatedMainTextures());
            changed |= SetTextureArrayReferences(serialized, "seekScreens", BuildCabinMiniSeekTextures());
            changed |= SetTextureArrayReferences(serialized, "roScreens", BuildCabinMiniRoTextures());

            if (serialized.hasModifiedProperties)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log(
                "Cocoon cabin screens protected and bound: led1/2/3 under " + GetMarkerPathOrMissing(interior) +
                ", oled1/2/3 under " + GetMarkerPathOrMissing(interior) +
                ", mini=" + GetMarkerPathOrMissing(miniScreen) +
                ", miniPanel=" + GetMarkerPathOrMissing(cabinMiniPanel) +
                ", miniImage=" + GetMarkerPathOrMissing(cabinMiniImage != null ? cabinMiniImage.transform : null) +
                ", miniTexture=" + (defaultMiniTexture != null ? defaultMiniTexture.name : "missing") +
                ", panels=" + GetMarkerPathOrMissing(cabinLedEnvPanel) + ", " + GetMarkerPathOrMissing(cabinOledEnvPanel) + ", " + GetMarkerPathOrMissing(cabinMiniPanel) +
                ". Repair does not move, rename, rebuild, or reparent these screen objects.");

            return changed;
        }

        private static bool EnsureLightSequenceController(Scene scene, Transform taxi)
        {
            bool changed = false;
            Transform uiRoot = FindTransform(scene, "06_UI");
            if (uiRoot == null)
            {
                return false;
            }

            const string ControllerName = "Cocoon Light Sequence Controller";
            Transform controllerTransform = FindTransformDeep(uiRoot, ControllerName);
            if (controllerTransform == null)
            {
                var controllerObject = new GameObject(ControllerName);
                controllerTransform = controllerObject.transform;
                controllerTransform.SetParent(uiRoot, false);
                changed = true;
            }

            CocoonLightSequenceController controller = controllerTransform.GetComponent<CocoonLightSequenceController>();
            if (controller == null)
            {
                controller = controllerTransform.gameObject.AddComponent<CocoonLightSequenceController>();
                changed = true;
            }

            Transform floorLight = FindTransformDeep(taxi, "FLOORLIGHT");
            Transform seatLight = FindTransformDeep(taxi, "SEATLIGHT");
            Transform preventer = FindTransformDeep(taxi, "luggage_preventrs_A1");

            var serialized = new SerializedObject(controller);
            changed |= SetObjectReference(serialized, "floorLightRoot", floorLight);
            changed |= SetObjectReference(serialized, "seatLightRoot", seatLight);
            changed |= SetObjectReference(serialized, "luggagePreventerRoot", preventer);
            if (serialized.hasModifiedProperties)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            controller.ResolveReferences();
            EditorUtility.SetDirty(controller);

            Debug.Log(
                "Cocoon light sequence controller bound under 06_UI. floor=" + GetMarkerPathOrMissing(floorLight) +
                ", seat=" + GetMarkerPathOrMissing(seatLight) +
                ", preventer=" + GetMarkerPathOrMissing(preventer) +
                ". Point transforms and existing runtime orb transforms are protected; runtime only toggles visibility.");

            return changed;
        }

        private static RectTransform EnsureSeatV2MiniDisplayPanel(Transform taxi, Transform miniScreen, out bool created)
        {
            created = false;
            RectTransform existing = miniScreen != null ? FindRectTransform(miniScreen, CabinMiniPanelName) : null;
            if (existing != null)
            {
                return existing;
            }

            if (miniScreen == null)
            {
                Debug.LogWarning("Cocoon cabin mini screen binding failed: current Seat V2 MIINIscreen1 was not found; old duplicate mini panels will not be rebound.");
                return null;
            }

            var canvasObject = new GameObject(CabinMiniPanelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(miniScreen, false);
            rect.sizeDelta = new Vector2(CabinPanelReferenceWidth, CabinPanelReferenceWidth / Mathf.Max(0.001f, CabinMiniPanelAspect));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                canvas.worldCamera = mainCamera;
            }

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = true;

            RawImage image = EnsureCabinScreenImage(rect, CabinMiniPanelName, CabinMiniPanelAspect);
            if (image != null)
            {
                image.raycastTarget = false;
            }

            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * (Mathf.Max(0.001f, CabinMiniPanelFallbackWidth) / CabinPanelReferenceWidth);
            created = true;
            return rect;
        }

        private static bool ApplyDefaultMiniTexture(RawImage image, Texture2D defaultTexture)
        {
            if (image == null)
            {
                return false;
            }

            bool changed = false;
            if (image.texture != defaultTexture)
            {
                image.texture = defaultTexture;
                changed = true;
            }

            if (image.color != Color.white)
            {
                image.color = Color.white;
                changed = true;
            }

            if (image.raycastTarget)
            {
                image.raycastTarget = false;
                changed = true;
            }

            if (!image.enabled)
            {
                image.enabled = true;
                changed = true;
            }

            if (!image.gameObject.activeSelf)
            {
                image.gameObject.SetActive(true);
                changed = true;
            }

            return changed;
        }

        private static RectTransform EnsureCabinDisplayPanel(
            Transform taxi,
            Transform reference,
            string panelName,
            float aspect,
            float fallbackWorldWidth,
            bool oledFacingSide,
            Transform preferredParent,
            out bool created)
        {
            created = false;
            RectTransform existing = FindRectTransform(taxi, panelName);
            if (existing != null)
            {
                return existing;
            }

            Transform parent = preferredParent != null ? preferredParent : taxi;
            if (parent == null)
            {
                return null;
            }

            var canvasObject = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(CabinPanelReferenceWidth, CabinPanelReferenceWidth / Mathf.Max(0.001f, aspect));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                canvas.worldCamera = mainCamera;
            }

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = true;

            RawImage image = EnsureCabinScreenImage(rect, panelName, aspect);
            image.raycastTarget = false;

            Transform interior = taxi != null ? FindTransformDeep(taxi, FinalTaxiInteriorStructureName) : null;
            Vector3 worldPosition = reference != null ? GetCabinReferenceCenter(reference) : rect.position;
            Quaternion worldRotation = GetCabinPanelInitialRotation(reference, interior, oledFacingSide, string.Equals(panelName, CabinMiniPanelName, StringComparison.Ordinal));
            rect.position = worldPosition;
            rect.rotation = worldRotation;

            float widthWorld = reference != null ? EstimateCabinReferenceWorldWidth(reference, interior) : 0f;
            if (widthWorld <= 0.001f)
            {
                widthWorld = Mathf.Max(0.001f, fallbackWorldWidth);
            }

            SetWorldScale(rect, Vector3.one * (widthWorld / CabinPanelReferenceWidth));
            created = true;
            return rect;
        }

        private static RawImage EnsureCabinScreenImage(RectTransform panel, string panelName, float aspect)
        {
            if (panel == null)
            {
                return null;
            }

            bool miniPanel = string.Equals(panelName, CabinMiniPanelName, StringComparison.Ordinal);
            string imageName = miniPanel ? CabinMiniScreenImageName : CabinDefaultScreenImageName;
            Transform imageTransform = panel.Find(imageName);
            RawImage image = imageTransform != null ? imageTransform.GetComponent<RawImage>() : null;
            if (image != null)
            {
                return image;
            }

            var imageObject = new GameObject(imageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(panel, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            if (miniPanel)
            {
                imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                imageRect.pivot = new Vector2(0.5f, 0.5f);
                imageRect.sizeDelta = new Vector2(CabinPanelReferenceWidth, CabinPanelReferenceWidth / Mathf.Max(0.001f, aspect));
                imageRect.localPosition = Vector3.zero;
                imageRect.localRotation = Quaternion.identity;
                imageRect.localScale = Vector3.one;
            }
            else
            {
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.offsetMin = Vector2.zero;
                imageRect.offsetMax = Vector2.zero;
            }

            image = imageObject.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static Quaternion GetCabinPanelInitialRotation(Transform reference, Transform interior, bool oledFacingSide, bool miniPanel)
        {
            if (reference == null)
            {
                return Quaternion.identity;
            }

            if (miniPanel || interior == null)
            {
                return reference.rotation;
            }

            Vector3 normal = oledFacingSide ? -interior.up : interior.up;
            Vector3 up = Vector3.ProjectOnPlane(interior.forward, normal);
            if (normal.sqrMagnitude < 0.0001f || up.sqrMagnitude < 0.0001f)
            {
                return reference.rotation;
            }

            return Quaternion.LookRotation(normal.normalized, up.normalized);
        }

        private static Vector3 GetCabinReferenceCenter(Transform reference)
        {
            Renderer[] renderers = reference.GetComponentsInChildren<Renderer>(true);
            if (TryGetCabinRendererBounds(renderers, out Bounds bounds))
            {
                return bounds.center;
            }

            return reference.position;
        }

        private static float EstimateCabinReferenceWorldWidth(Transform reference, Transform interior)
        {
            Renderer[] renderers = reference.GetComponentsInChildren<Renderer>(true);
            if (!TryGetCabinRendererBounds(renderers, out Bounds bounds))
            {
                return 0f;
            }

            Vector3 axis = interior != null ? interior.right : reference.right;
            if (axis.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            axis.Normalize();
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float projectedMin = float.PositiveInfinity;
            float projectedMax = float.NegativeInfinity;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                        float projected = Vector3.Dot(corner, axis);
                        projectedMin = Mathf.Min(projectedMin, projected);
                        projectedMax = Mathf.Max(projectedMax, projected);
                    }
                }
            }

            return Mathf.Max(0f, projectedMax - projectedMin);
        }

        private static bool TryGetCabinRendererBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (renderers == null)
            {
                return false;
            }

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

        private static bool ConfigureCabinScreenTextureImportSettings()
        {
            if (!AssetDatabase.IsValidFolder(CabinScreenResourceFolder))
            {
                return false;
            }

            bool changed = false;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { CabinScreenResourceFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool importerChanged = false;
                if (importer.maxTextureSize != 8192)
                {
                    importer.maxTextureSize = 8192;
                    importerChanged = true;
                }

                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importerChanged = true;
                }

                if (!importer.sRGBTexture)
                {
                    importer.sRGBTexture = true;
                    importerChanged = true;
                }

                if (importerChanged)
                {
                    importer.SaveAndReimport();
                    changed = true;
                }
            }

            return changed;
        }

        private static Texture2D LoadCabinTextureAsset(string textureName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CabinScreenResourceFolder + "/" + textureName + ".png");
        }

        private static Texture2D LoadCabinSideTextureAsset(string textureName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CabinSideScreenResourceFolder + "/" + textureName + ".png");
            return texture != null ? texture : LoadCabinTextureAsset(textureName);
        }

        private static Texture2D LoadCabinMiniTextureAsset(string textureName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CabinScreenResourceFolder + "/miniUI/" + textureName + ".png");
        }

        private static Texture2D LoadCabinMainLifecycleTextureAsset(string textureName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CabinScreenResourceFolder + "/mainUI/" + textureName + ".png");
        }

        private static Texture2D LoadCabinMiniSeekTextureAsset(string textureName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CabinScreenResourceFolder + "/miniUI/seek/" + textureName + ".png");
        }

        private static Texture2D LoadCabinMiniRoTextureAsset(string textureName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CabinScreenResourceFolder + "/miniUI/ro/" + textureName + ".png");
        }

        private static Texture2D[] BuildCabinMainTextures()
        {
            var textures = new Texture2D[11];
            textures[0] = LoadCabinTextureAsset("main-UI");
            for (int i = 1; i < textures.Length; i++)
            {
                textures[i] = LoadCabinTextureAsset("main-UI-" + i);
            }

            return textures;
        }

        private static Texture2D[] BuildCabinMiniTextures()
        {
            var textures = new Texture2D[14];
            textures[0] = LoadCabinMiniTextureAsset("main-UI");
            for (int i = 1; i < textures.Length; i++)
            {
                textures[i] = LoadCabinMiniTextureAsset("main-UI-" + i);
            }

            return textures;
        }

        private static Texture2D[] BuildCabinSeatedMainTextures()
        {
            var textures = new Texture2D[4];
            textures[0] = LoadCabinMainLifecycleTextureAsset("main-UI");
            for (int i = 1; i < textures.Length; i++)
            {
                textures[i] = LoadCabinMainLifecycleTextureAsset("main-UI-" + i);
            }

            return textures;
        }

        private static Texture2D[] BuildCabinMiniSeekTextures()
        {
            var textures = new Texture2D[3];
            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = LoadCabinMiniSeekTextureAsset("SEEK" + (i + 1));
            }

            return textures;
        }

        private static Texture2D[] BuildCabinMiniRoTextures()
        {
            var textures = new Texture2D[7];
            textures[0] = LoadCabinMiniRoTextureAsset("RO");
            for (int i = 1; i < textures.Length; i++)
            {
                textures[i] = LoadCabinMiniRoTextureAsset("RO-" + i);
            }

            return textures;
        }

        private static bool SetTextureArrayReferences(SerializedObject serialized, string propertyName, Texture2D[] textures)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || textures == null)
            {
                return false;
            }

            bool changed = false;
            if (property.arraySize != textures.Length)
            {
                property.arraySize = textures.Length;
                changed = true;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != textures[i])
                {
                    element.objectReferenceValue = textures[i];
                    changed = true;
                }
            }

            return changed;
        }

        private static bool RepairWindshieldScreenPanel(RectTransform panel, Texture2D defaultTexture)
        {
            if (panel == null)
            {
                return false;
            }

            bool changed = false;
            Text[] messageTexts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < messageTexts.Length; i++)
            {
                if (messageTexts[i] != null && messageTexts[i].name.IndexOf("Windshield Message", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (messageTexts[i].gameObject.activeSelf)
                    {
                        messageTexts[i].gameObject.SetActive(false);
                        changed = true;
                    }
                }
            }

            Transform background = panel.Find("Background");
            if (background != null)
            {
                Image backgroundImage = background.GetComponent<Image>();
                if (backgroundImage != null && backgroundImage.enabled)
                {
                    backgroundImage.enabled = false;
                    changed = true;
                }
            }

            RawImage image = EnsureExteriorScreenImage(panel);
            if (image != null)
            {
                RectTransform imageRect = image.rectTransform;
                if (imageRect.anchorMin != Vector2.zero || imageRect.anchorMax != Vector2.one || imageRect.offsetMin != Vector2.zero || imageRect.offsetMax != Vector2.zero)
                {
                    imageRect.anchorMin = Vector2.zero;
                    imageRect.anchorMax = Vector2.one;
                    imageRect.offsetMin = Vector2.zero;
                    imageRect.offsetMax = Vector2.zero;
                    changed = true;
                }

                if (image.texture != defaultTexture)
                {
                    image.texture = defaultTexture;
                    changed = true;
                }

                if (!image.gameObject.activeSelf)
                {
                    image.gameObject.SetActive(true);
                    changed = true;
                }

                if (!image.enabled)
                {
                    image.enabled = true;
                    changed = true;
                }

                if (image.raycastTarget)
                {
                    image.raycastTarget = false;
                    changed = true;
                }
            }

            return changed;
        }

        private static RawImage EnsureExteriorScreenImage(RectTransform panel)
        {
            Transform existing = panel.Find("Exterior Screen Image");
            if (existing != null)
            {
                RawImage existingImage = existing.GetComponent<RawImage>();
                return existingImage != null ? existingImage : existing.gameObject.AddComponent<RawImage>();
            }

            var imageObject = new GameObject("Exterior Screen Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static Vector2 GetExteriorScreenCanvasSize(Texture2D texture)
        {
            return new Vector2(ExteriorScreenReferenceWidth, ExteriorScreenReferenceWidth * ExteriorScreenSourceHeight / ExteriorScreenSourceWidth);
        }

        private static RectTransform FindRectTransform(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                {
                    return transforms[i] as RectTransform;
                }
            }

            return null;
        }

        private static bool SetObjectReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value)
            {
                return false;
            }

            property.objectReferenceValue = value;
            return true;
        }

        private static bool SetNestedBool(SerializedObject serialized, string propertyPath, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null || property.boolValue == value)
            {
                return false;
            }

            property.boolValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            return SetNestedBool(serialized, propertyName, value);
        }

        private static bool SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || Mathf.Abs(property.floatValue - value) < 0.0001f)
            {
                return false;
            }

            property.floatValue = value;
            return true;
        }

        private static bool SetVector3(SerializedObject serialized, string propertyName, Vector3 value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || Vector3.Distance(property.vector3Value, value) < 0.000001f)
            {
                return false;
            }

            property.vector3Value = value;
            return true;
        }

        private static bool SetQuaternion(SerializedObject serialized, string propertyName, Quaternion value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return false;
            }

            SerializedProperty x = property.FindPropertyRelative("x");
            SerializedProperty y = property.FindPropertyRelative("y");
            SerializedProperty z = property.FindPropertyRelative("z");
            SerializedProperty w = property.FindPropertyRelative("w");
            if (x == null || y == null || z == null || w == null)
            {
                return false;
            }

            Quaternion current = new Quaternion(x.floatValue, y.floatValue, z.floatValue, w.floatValue);
            if (Quaternion.Angle(current, value) < 0.001f)
            {
                return false;
            }

            x.floatValue = value.x;
            y.floatValue = value.y;
            z.floatValue = value.z;
            w.floatValue = value.w;
            return true;
        }

        private static void RepairHailDetectorTuning(CocoonRaiseHandDetector raiseDetector)
        {
            var serialized = new SerializedObject(raiseDetector);
            serialized.FindProperty("requiredHoldSeconds").floatValue = 2f;
            serialized.FindProperty("minimumHeightBelowHead").floatValue = 0.05f;
            serialized.FindProperty("maximumDistance").floatValue = 50f;
            serialized.FindProperty("facingDotThreshold").floatValue = 0.55f;
            serialized.FindProperty("taxiApproachDotThreshold").floatValue = 0.35f;

            Transform rightHand = FindTransform(SceneManager.GetActiveScene(), "Right Controller - Hail and UI Ray");
            Transform leftHand = FindTransform(SceneManager.GetActiveScene(), "Left Controller - Move and Teleport");
            if (rightHand != null)
            {
                serialized.FindProperty("rightHand").objectReferenceValue = rightHand;
                serialized.FindProperty("rightHandPose").objectReferenceValue = rightHand.GetComponent<CocoonXRNodePose>();
            }

            if (leftHand != null)
            {
                serialized.FindProperty("leftHand").objectReferenceValue = leftHand;
                serialized.FindProperty("leftHandPose").objectReferenceValue = leftHand.GetComponent<CocoonXRNodePose>();
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool RepairExpandedPickupLoop(Scene scene, Transform head)
        {
            Transform oldOverlay = FindTransform(scene, "Expanded Pickup Ring Overlay");
            if (oldOverlay != null)
            {
                UnityEngine.Object.DestroyImmediate(oldOverlay.gameObject);
            }

            Transform roadmap = FindTopLevelTransform(scene, "ROADMAP");
            if (roadmap == null)
            {
                Debug.LogWarning("Cocoon traffic repair skipped: ROADMAP is missing. Hardcoded fallback pickup loops are disabled so END-tagged road semantics cannot be bypassed.");
                return oldOverlay != null;
            }

            bool rebuilt = CocoonJapaneseRoadNetworkBuilder.ApplyToCurrentSceneIfAvailable(false);
            if (rebuilt)
            {
                Debug.Log("Cocoon ROADMAP traffic repair delegated to ROADMAP builder; hardcoded pickup loop is disabled.");
            }

            return oldOverlay != null || rebuilt;
        }

        private static Dictionary<string, Pose> CaptureCurrentPickupBayPoses(Scene scene, Transform overlay)
        {
            var poses = new Dictionary<string, Pose>();
            CaptureScenePickupBayPoses(scene, poses);

            if (overlay != null)
            {
                Transform[] transforms = overlay.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == "Pickup Bay Stop")
                    {
                        AddPickupBayPose(poses, transforms[i]);
                    }
                }

                for (int i = 0; i < transforms.Length; i++)
                {
                    if (IsPickupBayRootName(transforms[i].name))
                    {
                        AddPickupBayPose(poses, transforms[i]);
                    }
                }
            }

            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (stateMachine != null)
            {
                var serialized = new SerializedObject(stateMachine);
                SerializedProperty pullOverProperty = serialized.FindProperty("pullOverPoints");
                if (pullOverProperty != null)
                {
                    for (int i = 0; i < pullOverProperty.arraySize; i++)
                    {
                        Transform stop = pullOverProperty.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                        AddPickupBayPose(poses, stop);
                    }
                }

                SerializedProperty pullOverPointProperty = serialized.FindProperty("pullOverPoint");
                if (pullOverPointProperty != null)
                {
                    AddPickupBayPose(poses, pullOverPointProperty.objectReferenceValue as Transform);
                }
            }

            return poses;
        }

        private static void CaptureScenePickupBayPoses(Scene scene, Dictionary<string, Pose> poses)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == "Recessed Bay Asphalt" && IsActivePickupBayPoseSource(transforms[i]))
                    {
                        AddPickupBayPose(poses, transforms[i]);
                    }
                }

                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == "Pickup Bay Stop" && IsActivePickupBayPoseSource(transforms[i]))
                    {
                        AddPickupBayPose(poses, transforms[i]);
                    }
                }

                for (int i = 0; i < transforms.Length; i++)
                {
                    if (IsPickupBayRootName(transforms[i].name) && IsActivePickupBayPoseSource(transforms[i]))
                    {
                        AddPickupBayPose(poses, transforms[i]);
                    }
                }
            }
        }

        private static bool IsActivePickupBayPoseSource(Transform transform)
        {
            Transform bayRoot = GetPickupBayRoot(transform);
            if (bayRoot == null || !bayRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            Renderer[] renderers = bayRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled && renderers[i].gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddPickupBayPose(Dictionary<string, Pose> poses, Transform transform)
        {
            string key = GetPickupBayKey(transform);
            if (string.IsNullOrEmpty(key) || poses.ContainsKey(key))
            {
                return;
            }

            Vector3 position = transform.position;
            position.y = 0f;
            poses.Add(key, new Pose(position, transform.rotation));
        }

        private static Transform GetPickupBayRoot(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            if (IsPickupBayRootName(transform.name))
            {
                return transform;
            }

            if (transform.parent != null && IsPickupBayRootName(transform.parent.name))
            {
                return transform.parent;
            }

            return null;
        }

        private static string GetPickupBayKey(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            if (IsPickupBayRootName(transform.name))
            {
                return transform.name;
            }

            if (transform.parent != null && IsPickupBayRootName(transform.parent.name))
            {
                return transform.parent.name;
            }

            switch (transform.name)
            {
                case "Pickup Bay West Stop A":
                    return "West Pickup Bay A";
                case "Pickup Bay West Stop B":
                    return "West Pickup Bay B";
                case "Pickup Bay South Stop":
                    return "South Pickup Bay";
                case "Pickup Bay East Stop A":
                    return "East Pickup Bay A";
                case "Pickup Bay East Stop B":
                    return "East Pickup Bay B";
                case "Pickup Bay North Stop":
                    return "North Pickup Bay";
                default:
                    return null;
            }
        }

        private static bool IsPickupBayRootName(string name)
        {
            return name == "West Pickup Bay A" ||
                name == "West Pickup Bay B" ||
                name == "South Pickup Bay" ||
                name == "East Pickup Bay A" ||
                name == "East Pickup Bay B" ||
                name == "North Pickup Bay";
        }

        private static Vector3 GetPickupBayPosition(string name, Dictionary<string, Pose> overrides, Vector3 fallback)
        {
            return overrides != null && overrides.TryGetValue(name, out Pose pose) ? pose.position : fallback;
        }

        private static Quaternion GetPickupBayRotation(string name, Dictionary<string, Pose> overrides, Quaternion fallback)
        {
            return overrides != null && overrides.TryGetValue(name, out Pose pose) ? pose.rotation : fallback;
        }

        private static Transform[] BuildExpandedRoadOverlay(
            Transform parent,
            Material roadMat,
            Material sidewalkMat,
            Material curbMat,
            Material stripeMat,
            Material[] facadeMats,
            Material windowMat,
            Material shopfrontMat,
            Material awningMat,
            Material planterMat,
            Material streetFurnitureMat,
            Dictionary<string, Pose> pickupBayOverrides)
        {
            Transform roads = CreateChildGroup("Expanded Roads", parent);
            Transform sidewalks = CreateChildGroup("Expanded Sidewalks", parent);
            Transform curbs = CreateChildGroup("Expanded Curbs", parent);
            Transform markings = CreateChildGroup("Expanded Markings", parent);
            Transform crosswalks = CreateChildGroup("Expanded Crosswalks", parent);
            Transform buildings = CreateChildGroup("Expanded Buildings", parent);
            Transform streetFurniture = CreateChildGroup("Expanded Street Furniture", parent);

            CreateSceneCube("West Avenue Road", roads, new Vector3(0f, -0.031f, 0f), Quaternion.identity, new Vector3(6f, 0.055f, 82f), roadMat, true);
            CreateSceneCube("East Avenue Road", roads, new Vector3(34f, -0.031f, 0f), Quaternion.identity, new Vector3(6f, 0.055f, 82f), roadMat, true);
            CreateSceneCube("North Street Road", roads, new Vector3(17f, -0.029f, 28f), Quaternion.identity, new Vector3(46f, 0.055f, 6f), roadMat, true);
            CreateSceneCube("South Street Road", roads, new Vector3(17f, -0.029f, -28f), Quaternion.identity, new Vector3(46f, 0.055f, 6f), roadMat, true);

            CreateSceneCube("Sidewalk - Player West", sidewalks, new Vector3(-5.35f, 0.021f, 0f), Quaternion.identity, new Vector3(4.3f, 0.075f, 86f), sidewalkMat, true);
            CreateSceneCube("Sidewalk - East Outer", sidewalks, new Vector3(39.35f, 0.021f, 0f), Quaternion.identity, new Vector3(4.3f, 0.075f, 86f), sidewalkMat, true);
            CreateSceneCube("Sidewalk - North Outer", sidewalks, new Vector3(17f, 0.022f, 33.35f), Quaternion.identity, new Vector3(50f, 0.075f, 4.3f), sidewalkMat, true);
            CreateSceneCube("Sidewalk - South Outer", sidewalks, new Vector3(17f, 0.022f, -33.35f), Quaternion.identity, new Vector3(50f, 0.075f, 4.3f), sidewalkMat, true);
            CreateSceneCube("Central City Block Walk", sidewalks, new Vector3(17f, 0.024f, 0f), Quaternion.identity, new Vector3(22f, 0.075f, 44f), sidewalkMat, true);

            CreateSceneCube("West Avenue Outer Curb", curbs, new Vector3(-3.05f, 0.081f, 0f), Quaternion.identity, new Vector3(0.18f, 0.16f, 82f), curbMat, true);
            CreateSceneCube("West Avenue Inner Curb", curbs, new Vector3(3.05f, 0.081f, 0f), Quaternion.identity, new Vector3(0.18f, 0.16f, 82f), curbMat, true);
            CreateSceneCube("East Avenue Inner Curb", curbs, new Vector3(30.95f, 0.081f, 0f), Quaternion.identity, new Vector3(0.18f, 0.16f, 82f), curbMat, true);
            CreateSceneCube("East Avenue Outer Curb", curbs, new Vector3(37.05f, 0.081f, 0f), Quaternion.identity, new Vector3(0.18f, 0.16f, 82f), curbMat, true);
            CreateSceneCube("North Street Outer Curb", curbs, new Vector3(17f, 0.081f, 31.05f), Quaternion.identity, new Vector3(46f, 0.16f, 0.18f), curbMat, true);
            CreateSceneCube("South Street Outer Curb", curbs, new Vector3(17f, 0.081f, -31.05f), Quaternion.identity, new Vector3(46f, 0.16f, 0.18f), curbMat, true);

            Transform[] pickupStops =
            {
                CreateScenePickupBay("West Pickup Bay A", parent, GetPickupBayPosition("West Pickup Bay A", pickupBayOverrides, new Vector3(-4.05f, 0f, 10f)), GetPickupBayRotation("West Pickup Bay A", pickupBayOverrides, Quaternion.LookRotation(Vector3.back, Vector3.up)), roadMat, curbMat, stripeMat),
                CreateScenePickupBay("West Pickup Bay B", parent, GetPickupBayPosition("West Pickup Bay B", pickupBayOverrides, new Vector3(-4.05f, 0f, -10f)), GetPickupBayRotation("West Pickup Bay B", pickupBayOverrides, Quaternion.LookRotation(Vector3.back, Vector3.up)), roadMat, curbMat, stripeMat),
                CreateScenePickupBay("South Pickup Bay", parent, GetPickupBayPosition("South Pickup Bay", pickupBayOverrides, new Vector3(14f, 0f, -32.05f)), GetPickupBayRotation("South Pickup Bay", pickupBayOverrides, Quaternion.LookRotation(Vector3.right, Vector3.up)), roadMat, curbMat, stripeMat),
                CreateScenePickupBay("East Pickup Bay A", parent, GetPickupBayPosition("East Pickup Bay A", pickupBayOverrides, new Vector3(38.05f, 0f, -10f)), GetPickupBayRotation("East Pickup Bay A", pickupBayOverrides, Quaternion.LookRotation(Vector3.forward, Vector3.up)), roadMat, curbMat, stripeMat),
                CreateScenePickupBay("East Pickup Bay B", parent, GetPickupBayPosition("East Pickup Bay B", pickupBayOverrides, new Vector3(38.05f, 0f, 18f)), GetPickupBayRotation("East Pickup Bay B", pickupBayOverrides, Quaternion.LookRotation(Vector3.forward, Vector3.up)), roadMat, curbMat, stripeMat),
                CreateScenePickupBay("North Pickup Bay", parent, GetPickupBayPosition("North Pickup Bay", pickupBayOverrides, new Vector3(20f, 0f, 32.05f)), GetPickupBayRotation("North Pickup Bay", pickupBayOverrides, Quaternion.LookRotation(Vector3.left, Vector3.up)), roadMat, curbMat, stripeMat)
            };

            for (int i = 0; i < 19; i++)
            {
                float z = -36f + i * 4f;
                CreateSceneCube("West Avenue Lane Dash " + i, markings, new Vector3(0f, 0.027f, z), Quaternion.identity, new Vector3(0.08f, 0.02f, 1.55f), stripeMat, false);
                CreateSceneCube("East Avenue Lane Dash " + i, markings, new Vector3(34f, 0.027f, z), Quaternion.identity, new Vector3(0.08f, 0.02f, 1.55f), stripeMat, false);
            }

            for (int i = 0; i < 13; i++)
            {
                float x = -4f + i * 3.5f;
                CreateSceneCube("North Street Lane Dash " + i, markings, new Vector3(x, 0.027f, 28f), Quaternion.identity, new Vector3(1.35f, 0.02f, 0.08f), stripeMat, false);
                CreateSceneCube("South Street Lane Dash " + i, markings, new Vector3(x, 0.027f, -28f), Quaternion.identity, new Vector3(1.35f, 0.02f, 0.08f), stripeMat, false);
            }

            CreateSceneCrosswalk("West North Crosswalk", crosswalks, new Vector3(0f, 0.036f, 28f), true, stripeMat);
            CreateSceneCrosswalk("West South Crosswalk", crosswalks, new Vector3(0f, 0.036f, -28f), true, stripeMat);
            CreateSceneCrosswalk("East North Crosswalk", crosswalks, new Vector3(34f, 0.036f, 28f), true, stripeMat);
            CreateSceneCrosswalk("East South Crosswalk", crosswalks, new Vector3(34f, 0.036f, -28f), true, stripeMat);
            BuildSceneUrbanEnvironment(buildings, streetFurniture, facadeMats, windowMat, shopfrontMat, awningMat, planterMat, streetFurnitureMat, curbMat, stripeMat);

            return pickupStops;
        }

        private static void BuildSceneUrbanEnvironment(
            Transform buildings,
            Transform streetFurniture,
            Material[] facadeMats,
            Material windowMat,
            Material shopfrontMat,
            Material awningMat,
            Material planterMat,
            Material streetFurnitureMat,
            Material curbMat,
            Material stripeMat)
        {
            for (int i = 0; i < 12; i++)
            {
                float z = -38f + i * 6.8f;
                float width = 2.5f + (i % 3) * 0.42f;
                float depth = 3.1f + (i % 2) * 0.55f;
                float height = 3.0f + (i % 5) * 0.55f;
                CreateSceneArchitecturalBuilding("West Mixed Use Building " + i, buildings, new Vector3(-9.75f, 0f, z), Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector3(width, height, depth), SelectSceneFacade(facadeMats, i), windowMat, shopfrontMat, awningMat, i);
            }

            for (int i = 0; i < 12; i++)
            {
                float z = -36f + i * 6.8f;
                float width = 2.8f + (i % 2) * 0.55f;
                float depth = 3.0f + (i % 4) * 0.28f;
                float height = 3.4f + (i % 4) * 0.72f;
                CreateSceneArchitecturalBuilding("East Arcade Building " + i, buildings, new Vector3(43.35f, 0f, z + 1.25f), Quaternion.LookRotation(Vector3.left, Vector3.up), new Vector3(width, height, depth), SelectSceneFacade(facadeMats, i + 3), windowMat, shopfrontMat, awningMat, i + 10);
            }

            for (int i = 0; i < 7; i++)
            {
                float x = -1f + i * 6.0f;
                float height = 3.0f + (i % 3) * 0.8f;
                CreateSceneArchitecturalBuilding("North Street Building " + i, buildings, new Vector3(x, 0f, 37.9f), Quaternion.LookRotation(Vector3.back, Vector3.up), new Vector3(4.2f, height, 3.0f), SelectSceneFacade(facadeMats, i + 17), windowMat, shopfrontMat, awningMat, i + 20);
                CreateSceneArchitecturalBuilding("South Street Building " + i, buildings, new Vector3(x + 1.8f, 0f, -37.9f), Quaternion.LookRotation(Vector3.forward, Vector3.up), new Vector3(4.0f, height + 0.45f, 3.0f), SelectSceneFacade(facadeMats, i + 24), windowMat, shopfrontMat, awningMat, i + 30);
            }

            for (int i = 0; i < 9; i++)
            {
                float x = 7.2f + (i % 3) * 4.8f;
                float z = -14f + (i / 3) * 9.0f;
                float height = 2.4f + (i % 4) * 0.45f;
                CreateSceneArchitecturalBuilding("Central Courtyard Building " + i, buildings, new Vector3(x, 0f, z), Quaternion.LookRotation(i % 2 == 0 ? Vector3.back : Vector3.forward, Vector3.up), new Vector3(3.2f, height, 2.8f), SelectSceneFacade(facadeMats, i + 31), windowMat, shopfrontMat, awningMat, i + 40);
            }

            CreateSceneTransitShelter("West Pickup Shelter", streetFurniture, new Vector3(-6.35f, 0f, 10f), Quaternion.LookRotation(Vector3.right, Vector3.up), streetFurnitureMat, shopfrontMat, stripeMat);
            CreateSceneTransitShelter("East Pickup Shelter", streetFurniture, new Vector3(40.55f, 0f, 18f), Quaternion.LookRotation(Vector3.left, Vector3.up), streetFurnitureMat, shopfrontMat, stripeMat);

            for (int i = 0; i < 7; i++)
            {
                float z = -28f + i * 9.5f;
                CreateScenePlanter("West Planter " + i, streetFurniture, new Vector3(-6.55f, 0f, z), Quaternion.identity, planterMat, curbMat);
                CreateScenePlanter("East Planter " + i, streetFurniture, new Vector3(39.95f, 0f, z + 2.5f), Quaternion.identity, planterMat, curbMat);
            }

            for (int i = 0; i < 5; i++)
            {
                float z = -24f + i * 12f;
                CreateSceneBench("West Bench " + i, streetFurniture, new Vector3(-5.9f, 0f, z), Quaternion.Euler(0f, 90f, 0f), streetFurnitureMat);
                CreateSceneBench("East Bench " + i, streetFurniture, new Vector3(39.0f, 0f, z + 5f), Quaternion.Euler(0f, -90f, 0f), streetFurnitureMat);
            }
        }

        private static Material SelectSceneFacade(Material[] facadeMats, int seed)
        {
            if (facadeMats == null || facadeMats.Length == 0)
            {
                return null;
            }

            return facadeMats[Mathf.Abs(seed) % facadeMats.Length];
        }

        private static void CreateSceneArchitecturalBuilding(
            string name,
            Transform parent,
            Vector3 groundCenter,
            Quaternion rotation,
            Vector3 size,
            Material facadeMat,
            Material windowMat,
            Material shopfrontMat,
            Material awningMat,
            int seed)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = groundCenter;
            root.transform.rotation = rotation;

            float width = Mathf.Max(1f, size.x);
            float height = Mathf.Max(1.8f, size.y);
            float depth = Mathf.Max(1f, size.z);
            float frontZ = depth * 0.5f + 0.026f;

            CreateSceneCubeChild("Facade Mass", root.transform, new Vector3(0f, height * 0.5f, 0f), Quaternion.identity, new Vector3(width, height, depth), facadeMat, false);
            CreateSceneCubeChild("Roof Cap", root.transform, new Vector3(0f, height + 0.08f, 0f), Quaternion.identity, new Vector3(width + 0.18f, 0.16f, depth + 0.18f), facadeMat, false);
            CreateSceneCubeChild("Ground Floor Shopfront", root.transform, new Vector3(0f, 0.66f, frontZ), Quaternion.identity, new Vector3(width * 0.78f, 0.72f, 0.04f), shopfrontMat, false);
            CreateSceneCubeChild("Awning Band", root.transform, new Vector3(0f, 1.13f, frontZ + 0.035f), Quaternion.identity, new Vector3(width * 0.86f, 0.12f, 0.1f), awningMat, false);

            int columns = Mathf.Clamp(Mathf.FloorToInt(width / 0.62f), 2, 6);
            int floors = Mathf.Clamp(Mathf.FloorToInt((height - 1.55f) / 0.62f), 1, 8);
            float windowWidth = Mathf.Min(0.42f, width / (columns + 1) * 0.55f);
            float startX = -(columns - 1) * width / (columns + 1) * 0.5f;
            float stepX = width / (columns + 1);
            for (int floor = 0; floor < floors; floor++)
            {
                float y = 1.65f + floor * 0.62f;
                for (int column = 0; column < columns; column++)
                {
                    if (((column + floor + seed) % 7) == 0)
                    {
                        continue;
                    }

                    float x = startX + column * stepX;
                    CreateSceneCubeChild("Window " + floor + "-" + column, root.transform, new Vector3(x, y, frontZ + 0.012f), Quaternion.identity, new Vector3(windowWidth, 0.34f, 0.035f), windowMat, false);
                }
            }

            if (seed % 2 == 0)
            {
                CreateSceneCubeChild("Side Sign", root.transform, new Vector3(width * 0.42f, 1.58f, frontZ + 0.04f), Quaternion.identity, new Vector3(0.12f, 0.72f, 0.055f), awningMat, false);
            }
        }

        private static void CreateSceneCrosswalk(string name, Transform parent, Vector3 center, bool acrossRoadX, Material stripeMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = center;
            for (int i = 0; i < 7; i++)
            {
                Vector3 local = acrossRoadX ? new Vector3(-2.1f + i * 0.7f, 0f, 0f) : new Vector3(0f, 0f, -2.1f + i * 0.7f);
                Vector3 scale = acrossRoadX ? new Vector3(0.38f, 0.025f, 5.6f) : new Vector3(5.6f, 0.025f, 0.38f);
                CreateSceneCubeChild("Stripe " + (i + 1), root.transform, local, Quaternion.identity, scale, stripeMat, false);
            }
        }

        private static void CreateSceneTransitShelter(string name, Transform parent, Vector3 position, Quaternion rotation, Material frameMat, Material glassMat, Material signMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = rotation;
            CreateSceneCubeChild("Shelter Roof", root.transform, new Vector3(0f, 1.72f, 0f), Quaternion.identity, new Vector3(1.65f, 0.1f, 0.64f), frameMat, false);
            CreateSceneCubeChild("Shelter Back Glass", root.transform, new Vector3(0f, 0.95f, -0.28f), Quaternion.identity, new Vector3(1.55f, 1.18f, 0.045f), glassMat, false);
            CreateSceneCubeChild("Shelter Side Glass", root.transform, new Vector3(-0.78f, 0.95f, 0f), Quaternion.identity, new Vector3(0.045f, 1.18f, 0.5f), glassMat, false);
            CreateSceneCubeChild("Shelter Seat", root.transform, new Vector3(0f, 0.42f, 0.05f), Quaternion.identity, new Vector3(1.18f, 0.1f, 0.26f), frameMat, false);
            CreateSceneCubeChild("Shelter Pickup Sign", root.transform, new Vector3(0f, 1.48f, 0.34f), Quaternion.identity, new Vector3(1.0f, 0.28f, 0.05f), signMat, false);
        }

        private static void CreateScenePlanter(string name, Transform parent, Vector3 position, Quaternion rotation, Material plantMat, Material baseMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = rotation;
            CreateSceneCubeChild("Planter Base", root.transform, new Vector3(0f, 0.18f, 0f), Quaternion.identity, new Vector3(0.78f, 0.34f, 0.42f), baseMat, false);
            CreateSceneCubeChild("Plant Mass", root.transform, new Vector3(0f, 0.42f, 0f), Quaternion.identity, new Vector3(0.66f, 0.22f, 0.34f), plantMat, false);
        }

        private static void CreateSceneBench(string name, Transform parent, Vector3 position, Quaternion rotation, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = rotation;
            CreateSceneCubeChild("Seat", root.transform, new Vector3(0f, 0.42f, 0f), Quaternion.identity, new Vector3(1.05f, 0.08f, 0.28f), material, false);
            CreateSceneCubeChild("Back", root.transform, new Vector3(0f, 0.66f, -0.15f), Quaternion.identity, new Vector3(1.05f, 0.34f, 0.06f), material, false);
            CreateSceneCubeChild("Left Leg", root.transform, new Vector3(-0.42f, 0.23f, 0f), Quaternion.identity, new Vector3(0.08f, 0.32f, 0.08f), material, false);
            CreateSceneCubeChild("Right Leg", root.transform, new Vector3(0.42f, 0.23f, 0f), Quaternion.identity, new Vector3(0.08f, 0.32f, 0.08f), material, false);
        }

        private static void DisableLegacyPickupGeometry(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    string transformName = transforms[i].name;
                    if (!transformName.StartsWith("Pickup Bay", StringComparison.Ordinal) &&
                        !transformName.StartsWith("Pickup Sign", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Renderer[] renderers = transforms[i].GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        renderers[rendererIndex].enabled = false;
                    }

                    Collider[] colliders = transforms[i].GetComponentsInChildren<Collider>(true);
                    for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                    {
                        colliders[colliderIndex].enabled = false;
                    }
                }
            }
        }

        private static void DisableLegacyStreetBlock(Transform streetRoot)
        {
            for (int i = 0; i < streetRoot.childCount; i++)
            {
                Transform child = streetRoot.GetChild(i);
                if (child.name == "Expanded Pickup Ring Overlay")
                {
                    continue;
                }

                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    renderers[rendererIndex].enabled = false;
                }

                Collider[] colliders = child.GetComponentsInChildren<Collider>(true);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    colliders[colliderIndex].enabled = false;
                }
            }
        }

        private static CocoonSafePickupZone RepairProjectedSafePickupZone(Transform pickupRoot, Transform head, Material zoneMat, Material arrowMat)
        {
            const float detectionRadius = 1.25f;
            Transform zoneTransform = FindTransform(SceneManager.GetActiveScene(), "Safe Pickup Zone");
            if (zoneTransform == null)
            {
                zoneTransform = new GameObject("Safe Pickup Zone").transform;
            }

            zoneTransform.SetParent(pickupRoot, true);
            zoneTransform.position = new Vector3(-6.6f, 0.08f, 10f);
            zoneTransform.rotation = Quaternion.identity;

            for (int i = zoneTransform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(zoneTransform.GetChild(i).gameObject);
            }

            SphereCollider trigger = zoneTransform.GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = zoneTransform.gameObject.AddComponent<SphereCollider>();
            }

            trigger.isTrigger = true;
            trigger.radius = detectionRadius;

            CreateScenePickupVisualChild("Door Light Wash", zoneTransform, Vector3.zero, new Vector3(1.65f, 0.035f, 2.45f), zoneMat);
            CreateScenePickupVisualChild("Door Beam Core", zoneTransform, new Vector3(-0.9f, 0.04f, 0f), new Vector3(1.9f, 0.025f, 0.72f), arrowMat);
            CreateScenePickupVisualChild("Door Beam Front Edge", zoneTransform, new Vector3(-0.92f, 0.045f, 0.86f), new Vector3(1.8f, 0.025f, 0.07f), arrowMat);
            CreateScenePickupVisualChild("Door Beam Rear Edge", zoneTransform, new Vector3(-0.92f, 0.045f, -0.86f), new Vector3(1.8f, 0.025f, 0.07f), arrowMat);
            CreateScenePickupVisualChild("Pickup Boundary Left", zoneTransform, new Vector3(-0.84f, 0.055f, 0f), new Vector3(0.055f, 0.06f, 2.5f), arrowMat);
            CreateScenePickupVisualChild("Pickup Boundary Right", zoneTransform, new Vector3(0.84f, 0.055f, 0f), new Vector3(0.055f, 0.06f, 2.5f), arrowMat);
            CreateScenePickupVisualChild("Pickup Boundary Front", zoneTransform, new Vector3(0f, 0.055f, 1.25f), new Vector3(1.72f, 0.06f, 0.055f), arrowMat);
            CreateScenePickupVisualChild("Pickup Boundary Back", zoneTransform, new Vector3(0f, 0.055f, -1.25f), new Vector3(1.72f, 0.06f, 0.055f), arrowMat);

            CocoonSafePickupZone safeZone = zoneTransform.GetComponent<CocoonSafePickupZone>();
            if (safeZone == null)
            {
                safeZone = zoneTransform.gameObject.AddComponent<CocoonSafePickupZone>();
            }

            safeZone.Configure(head, zoneTransform, detectionRadius);
            safeZone.SetZoneActive(false);
            return safeZone;
        }

        private static GameObject RepairProjectedGuidanceRoot(Transform pickupRoot, Material arrowMat)
        {
            Transform guidance = FindTransform(SceneManager.GetActiveScene(), "Guidance To Safe Pickup Zone");
            if (guidance == null)
            {
                guidance = new GameObject("Guidance To Safe Pickup Zone").transform;
            }

            guidance.SetParent(pickupRoot, true);
            guidance.position = new Vector3(-6.6f, 0.08f, 10f);
            guidance.rotation = Quaternion.identity;

            for (int i = guidance.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(guidance.GetChild(i).gameObject);
            }

            CreateSceneGuidanceArrow(guidance, "Projected Pickup Arrow Back", new Vector3(0f, 0.09f, -1.75f), Vector3.forward, arrowMat);
            CreateSceneGuidanceArrow(guidance, "Projected Pickup Arrow Mid", new Vector3(0f, 0.09f, -0.62f), Vector3.forward, arrowMat);
            CreateScenePickupVisualChild("Safe Zone Glow Landing", guidance, Vector3.zero, new Vector3(1.9f, 0.025f, 2.7f), arrowMat);
            guidance.gameObject.SetActive(false);
            return guidance.gameObject;
        }

        private static Transform CreateScenePickupBay(string name, Transform parent, Vector3 center, Quaternion rotation, Material roadMat, Material curbMat, Material stripeMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = center;
            root.transform.rotation = rotation;

            var stop = new GameObject("Pickup Bay Stop");
            stop.transform.SetParent(root.transform, false);
            stop.transform.localPosition = Vector3.zero;
            stop.transform.localRotation = Quaternion.identity;

            CreateSceneCubeChild("Recessed Bay Asphalt", root.transform, new Vector3(0f, 0.055f, 0f), Quaternion.identity, new Vector3(2.05f, 0.035f, 5.35f), roadMat, true);
            CreateSceneCubeChild("Outer Curb Return", root.transform, new Vector3(1.12f, 0.075f, 0f), Quaternion.identity, new Vector3(0.16f, 0.14f, 5.65f), curbMat, true);
            CreateSceneCubeChild("Front Curb Return", root.transform, new Vector3(0f, 0.075f, 2.82f), Quaternion.identity, new Vector3(2.25f, 0.14f, 0.16f), curbMat, true);
            CreateSceneCubeChild("Rear Curb Return", root.transform, new Vector3(0f, 0.075f, -2.82f), Quaternion.identity, new Vector3(2.25f, 0.14f, 0.16f), curbMat, true);
            CreateSceneCubeChild("White Bay Edge", root.transform, new Vector3(-0.98f, 0.082f, 0f), Quaternion.identity, new Vector3(0.06f, 0.025f, 5.08f), stripeMat, false);
            CreateSceneCubeChild("Stop Bar", root.transform, new Vector3(0f, 0.084f, 1.72f), Quaternion.identity, new Vector3(1.7f, 0.025f, 0.08f), stripeMat, false);
            return stop.transform;
        }

        private static void CreateSceneGuidanceArrow(Transform parent, string name, Vector3 localPosition, Vector3 direction, Material material)
        {
            var arrow = new GameObject(name);
            arrow.transform.SetParent(parent, false);
            arrow.transform.localPosition = localPosition;
            arrow.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            CreateScenePickupVisualChild("Stem", arrow.transform, Vector3.zero, new Vector3(0.12f, 0.025f, 0.5f), material);
            GameObject headLeft = CreateScenePickupVisualChild("Head Left", arrow.transform, new Vector3(-0.1f, 0f, 0.23f), new Vector3(0.1f, 0.025f, 0.32f), material);
            headLeft.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
            GameObject headRight = CreateScenePickupVisualChild("Head Right", arrow.transform, new Vector3(0.1f, 0f, 0.23f), new Vector3(0.1f, 0.025f, 0.32f), material);
            headRight.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
        }

        private static GameObject CreateScenePickupVisualChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return CreateSceneCubeChild(name, parent, localPosition, Quaternion.identity, localScale, material, false);
        }

        private static GameObject CreateSceneCube(string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, true);
            cube.transform.position = position;
            cube.transform.rotation = rotation;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                RemovePrimitiveCollider(cube);
            }

            return cube;
        }

        private static GameObject CreateSceneCubeChild(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material, bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = localRotation;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                RemovePrimitiveCollider(cube);
            }

            return cube;
        }

        private static Transform CreateSceneMarker(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, true);
            marker.transform.position = position;
            marker.transform.rotation = rotation;
            return marker.transform;
        }

        private static Transform CreateChildGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Transform FindOrCreateTransform(Scene scene, string name)
        {
            Transform existing = FindTransform(scene, name);
            if (existing != null)
            {
                return existing;
            }

            var created = new GameObject(name);
            return created.transform;
        }

        private static Text CreateWindshieldDisplay(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = localPosition;
            canvasObject.transform.localRotation = localRotation;
            canvasObject.transform.localScale = localScale;

            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = GetExteriorScreenCanvasSize(null);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            RawImage image = EnsureExteriorScreenImage(rect);
            image.texture = Resources.Load<Texture2D>(ExteriorScreenResourcePath);
            return null;
        }

        private static void DisableLegacyTaxiVisuals(Transform taxi)
        {
            for (int i = 0; i < taxi.childCount; i++)
            {
                Transform child = taxi.GetChild(i);
                if (child.name == "Taxi Pod Visuals" || child.name == "Door-side Onboarding UI")
                {
                    continue;
                }

                Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    renderers[rendererIndex].enabled = false;
                }

                Collider[] colliders = child.GetComponentsInChildren<Collider>(true);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    colliders[colliderIndex].enabled = false;
                }
            }
        }

        private static GameObject CreateTaxiCubeChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = localRotation;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            RemovePrimitiveCollider(cube);
            return cube;
        }

        private static GameObject CreateTaxiCylinderChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localRotation = localRotation;
            cylinder.transform.localScale = localScale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            RemovePrimitiveCollider(cylinder);
            return cylinder;
        }

        private static void RemovePrimitiveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static Material LoadMaterial(string name, Color fallbackColor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", fallbackColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", fallbackColor);
            }

            return material;
        }

        private static bool EnsureHeadLockedUiTuning(Scene scene)
        {
            Transform uiRoot = FindTransform(scene, "06_UI");
            CocoonTaxiStateMachine stateMachine = FindComponent<CocoonTaxiStateMachine>(scene);
            if (uiRoot == null || stateMachine == null)
            {
                return false;
            }

            bool changed = false;
            CocoonHeadLockedUITuning tuning = uiRoot.GetComponent<CocoonHeadLockedUITuning>();
            if (tuning == null)
            {
                tuning = uiRoot.gameObject.AddComponent<CocoonHeadLockedUITuning>();
                changed = true;
                Debug.Log("Cocoon head-locked UI tuning added to 06_UI. Select 06_UI to adjust face-locked panel size and text scale.");
            }

            var serialized = new SerializedObject(stateMachine);
            changed |= SetBool(serialized, "suppressHeadLockedFlowUi", true);
            changed |= SetObjectReference(serialized, "headLockedUiTuning", tuning);
            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                changed = true;
            }

            return changed;
        }

        private static Vector2 HeadLockedSize(float width01, float height01)
        {
            return new Vector2(HeadLockedPanelWidth * width01, HeadLockedPanelHeight * height01);
        }

        private static Vector2 HeadLockedPos(float x01, float y01)
        {
            return new Vector2(HeadLockedPanelWidth * x01, HeadLockedPanelHeight * y01);
        }

        private static int HeadLockedFont(float baseSize)
        {
            return Mathf.RoundToInt(Mathf.Clamp(baseSize * HeadLockedTextScale, 12f, 220f));
        }

        private static void RepairStreetPanel(Canvas canvas, Transform head)
        {
            ConfigureCanvas(canvas, new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight), HeadLockedUiLocalScale);
            canvas.transform.position = new Vector3(-4.92f, 1.8f, -9.12f);
            canvas.transform.rotation = Quaternion.Euler(0f, 80f, 0f);

            CocoonBillboard billboard = canvas.GetComponent<CocoonBillboard>();
            if (billboard == null)
            {
                billboard = canvas.gameObject.AddComponent<CocoonBillboard>();
            }

            if (head != null)
            {
                billboard.Configure(head);
            }

            EnsureHeadLockedUi(canvas.gameObject, head);

            SetPanelBackground(canvas.transform, new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight));
            RemoveDuplicateTextComponents(canvas.transform);
            ConfigureText(canvas.transform, "Headline", HeadLockedPos(0f, 0.33f), HeadLockedSize(0.9f, 0.24f), HeadLockedFont(96f), TextAnchor.MiddleLeft);
            ConfigureText(canvas.transform, "Status", HeadLockedPos(0f, 0.12f), HeadLockedSize(0.9f, 0.28f), HeadLockedFont(64f), TextAnchor.MiddleLeft);
            ConfigureText(canvas.transform, "Instructions", HeadLockedPos(-0.09f, -0.12f), HeadLockedSize(0.72f, 0.28f), HeadLockedFont(56f), TextAnchor.MiddleLeft);
            ConfigureText(canvas.transform, "Timer", HeadLockedPos(0.32f, -0.36f), HeadLockedSize(0.28f, 0.18f), HeadLockedFont(62f), TextAnchor.MiddleRight);
            LogPanelMetrics(canvas, "Street");
        }

        private static GameObject CreateDoorPanel(Transform doorHinge, CocoonTaxiStateMachine stateMachine, out Text headline, out Text status, out Text timer, out Text destination)
        {
            var canvasObject = new GameObject("Door-side Onboarding UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            CreatePanelBackground(canvasObject.transform, new Color(0.015f, 0.025f, 0.032f, 0.985f), new Vector2(760f, 560f));
            headline = CreateDoorText("Door Panel Title", canvasObject.transform, new Vector2(0f, 194f), new Vector2(650f, 72f), 78, Color.white, TextAnchor.MiddleCenter, "BOARD COCOON?");
            status = CreateDoorText("Door Panel Status", canvasObject.transform, new Vector2(0f, 104f), new Vector2(650f, 64f), 40, new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, "Right controller: A confirms, B leaves.");
            destination = CreateDoorText("Door Panel Destination", canvasObject.transform, new Vector2(0f, -4f), new Vector2(650f, 152f), 38, new Color(0.1f, 1f, 0.55f), TextAnchor.MiddleLeft, "Destination: Duomo\nMode: Solo\nPickup: Door-side safe zone\nPayment: Contactless ready");
            timer = CreateDoorText("Door Panel Timer", canvasObject.transform, new Vector2(0f, -226f), new Vector2(540f, 46f), 40, new Color(0.95f, 0.86f, 0.38f), TextAnchor.MiddleCenter, "Decision");

            CreateDoorButton("Confirm Ride Button", canvasObject.transform, new Vector2(-142f, -142f), new Vector2(248f, 78f), "A\nCONFIRM", stateMachine, CocoonButtonAction.ConfirmRide, "");
            CreateDoorButton("Leave Button", canvasObject.transform, new Vector2(142f, -142f), new Vector2(248f, 78f), "B\nLEAVE", stateMachine, CocoonButtonAction.DeclineRide, "");

            canvasObject.SetActive(false);
            Debug.Log("Cocoon repair created missing Door-side Onboarding UI on the passenger door.");
            return canvasObject;
        }

        private static GameObject CreateManualDoorSidePanel(Transform uiRoot, string panelName, Vector3 localPosition, CocoonTaxiStateMachine stateMachine)
        {
            var canvasObject = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(uiRoot, false);
            rect.sizeDelta = new Vector2(760f, 560f);
            canvasObject.transform.localPosition = localPosition;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.00042f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            EnsureManualDoorPanelContents(canvasObject.transform, stateMachine);
            WireDoorPanelButtons(canvasObject.transform, stateMachine);
            Debug.Log("Cocoon repair created " + panelName + " under 06_UI for manual door-side placement.");
            return canvasObject;
        }

        private static void EnsureManualDoorPanelContents(Transform panelTransform, CocoonTaxiStateMachine stateMachine)
        {
            if (panelTransform == null)
            {
                return;
            }

            RectTransform panelRect = panelTransform as RectTransform;
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight);
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
                scaler.dynamicPixelsPerUnit = HeadLockedCanvasPixelsPerUnit;
                scaler.referencePixelsPerUnit = 100f;
            }

            Transform background = panelTransform.Find("Background");
            if (background == null)
            {
                CreatePanelBackground(panelTransform, new Color(0.015f, 0.025f, 0.032f, 0.985f), new Vector2(760f, 560f));
            }
            else
            {
                SetPanelBackground(panelTransform, new Vector2(760f, 560f));
            }

            EnsureManualDoorText("Door Panel Title", panelTransform, new Vector2(0f, 194f), new Vector2(650f, 72f), 78, Color.white, TextAnchor.MiddleCenter, "BOARD COCOON?");
            EnsureManualDoorText("Door Panel Status", panelTransform, new Vector2(0f, 104f), new Vector2(650f, 64f), 40, new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, "Right controller: A confirms, B leaves.");
            EnsureManualDoorText("Door Panel Destination", panelTransform, new Vector2(0f, -4f), new Vector2(650f, 152f), 38, new Color(0.1f, 1f, 0.55f), TextAnchor.MiddleLeft, "Destination: Duomo\nMode: Solo\nPickup: Door-side\nPayment: Contactless ready");
            EnsureManualDoorText("Door Panel Timer", panelTransform, new Vector2(0f, -226f), new Vector2(540f, 46f), 40, new Color(0.95f, 0.86f, 0.38f), TextAnchor.MiddleCenter, "Decision");

            EnsureDoorButton(panelTransform, "Confirm Ride Button", new Vector2(-142f, -142f), new Vector2(248f, 78f), "A\nCONFIRM", 36);
            EnsureDoorButton(panelTransform, "Leave Button", new Vector2(142f, -142f), new Vector2(248f, 78f), "B\nLEAVE", 36);
            WireDoorPanelButtons(panelTransform, stateMachine);
        }

        private static Text EnsureManualDoorText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing == null)
            {
                return CreateDoorText(name, parent, anchoredPosition, size, fontSize, color, alignment, value);
            }

            Text text = existing.GetComponent<Text>();
            if (text == null)
            {
                text = existing.gameObject.AddComponent<Text>();
                text.font = GetEditorRuntimeFont();
            }

            if (string.IsNullOrEmpty(text.text))
            {
                text.text = value;
            }

            text.color = color;
            ConfigureText(text, anchoredPosition, size, fontSize, alignment);
            return text;
        }

        private static GameObject CreateBaggageQuestionPanel(Transform uiRoot, CocoonTaxiStateMachine stateMachine)
        {
            var canvasObject = new GameObject(BaggageQuestionPanelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(uiRoot, false);
            rect.sizeDelta = new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight);
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * HeadLockedUiLocalScale;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = HeadLockedCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            EnsureBaggageQuestionPanelContents(canvasObject.transform, stateMachine);
            WireBaggageQuestionButtons(canvasObject.transform, stateMachine);
            canvasObject.SetActive(false);
            Debug.Log("Cocoon repair created head-locked baggage question panel under 06_UI.");
            return canvasObject;
        }

        private static GameObject CreateHeadLockedPromptPanel(Transform uiRoot, string name, string title, string status, string primaryButton, string secondaryButton)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.SetParent(uiRoot, false);
            rect.sizeDelta = new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight);
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * HeadLockedUiLocalScale;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = HeadLockedCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            EnsureHeadLockedPromptPanelContents(canvasObject.transform, title, status, primaryButton, secondaryButton);
            canvasObject.SetActive(false);
            Debug.Log("Cocoon repair created head-locked prompt panel " + name + " under 06_UI.");
            return canvasObject;
        }

        private static void EnsureHeadLockedPromptPanelContents(Transform panelTransform, string title, string status, string primaryButton, string secondaryButton)
        {
            if (panelTransform == null)
            {
                return;
            }

            RectTransform panelRect = panelTransform as RectTransform;
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight);
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
                scaler.dynamicPixelsPerUnit = HeadLockedCanvasPixelsPerUnit;
                scaler.referencePixelsPerUnit = 100f;
            }

            Transform background = panelTransform.Find("Background");
            if (background == null)
            {
                CreatePanelBackground(panelTransform, new Color(0.015f, 0.025f, 0.032f, 0.985f), new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight));
            }
            else
            {
                SetPanelBackground(panelTransform, new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight));
            }

            EnsureBaggageQuestionText("Prompt Title", panelTransform, HeadLockedPos(0f, 0.29f), HeadLockedSize(0.9f, 0.3f), HeadLockedFont(96f), Color.white, TextAnchor.MiddleCenter, title);
            EnsureBaggageQuestionText("Prompt Status", panelTransform, HeadLockedPos(0f, 0.07f), HeadLockedSize(0.9f, 0.24f), HeadLockedFont(62f), new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, status);

            if (!string.IsNullOrEmpty(primaryButton) && !string.IsNullOrEmpty(secondaryButton))
            {
                EnsureDoorButton(panelTransform, "Prompt Primary Button", HeadLockedPos(-0.21f, -0.31f), HeadLockedSize(0.32f, 0.24f), primaryButton, HeadLockedFont(58f));
                EnsureDoorButton(panelTransform, "Prompt Secondary Button", HeadLockedPos(0.21f, -0.31f), HeadLockedSize(0.32f, 0.24f), secondaryButton, HeadLockedFont(58f));
                RemoveWorldButtonComponent(panelTransform, "Prompt Primary Button");
                RemoveWorldButtonComponent(panelTransform, "Prompt Secondary Button");
            }
            else if (!string.IsNullOrEmpty(primaryButton))
            {
                EnsureDoorButton(panelTransform, "Prompt Primary Button", HeadLockedPos(0f, -0.31f), HeadLockedSize(0.38f, 0.24f), primaryButton, HeadLockedFont(60f));
                RemoveWorldButtonComponent(panelTransform, "Prompt Primary Button");
                SetChildActive(panelTransform, "Prompt Secondary Button", false);
            }

            if (string.IsNullOrEmpty(primaryButton))
            {
                SetChildActive(panelTransform, "Prompt Primary Button", false);
            }

            if (string.IsNullOrEmpty(secondaryButton))
            {
                SetChildActive(panelTransform, "Prompt Secondary Button", false);
            }
        }

        private static void RemoveWorldButtonComponent(Transform panelTransform, string buttonName)
        {
            Transform button = panelTransform != null ? panelTransform.Find(buttonName) : null;
            if (button == null)
            {
                return;
            }

            CocoonWorldButton worldButton = button.GetComponent<CocoonWorldButton>();
            if (worldButton != null)
            {
                UnityEngine.Object.DestroyImmediate(worldButton);
            }
        }

        private static void EnsureBaggageQuestionPanelContents(Transform panelTransform, CocoonTaxiStateMachine stateMachine)
        {
            if (panelTransform == null)
            {
                return;
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
                scaler.dynamicPixelsPerUnit = HeadLockedCanvasPixelsPerUnit;
                scaler.referencePixelsPerUnit = 100f;
            }

            Transform background = panelTransform.Find("Background");
            if (background == null)
            {
                CreatePanelBackground(panelTransform, new Color(0.015f, 0.025f, 0.032f, 0.985f), new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight));
            }
            else
            {
                SetPanelBackground(panelTransform, new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight));
            }

            EnsureBaggageQuestionText("Baggage Question Title", panelTransform, HeadLockedPos(0f, 0.29f), HeadLockedSize(0.9f, 0.3f), HeadLockedFont(90f), Color.white, TextAnchor.MiddleCenter, "ARE YOU CARRYING LUGGAGE?");
            EnsureBaggageQuestionText("Baggage Question Status", panelTransform, HeadLockedPos(0f, 0.07f), HeadLockedSize(0.9f, 0.24f), HeadLockedFont(58f), new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, "Right controller: press A for YES, B for NO.");

            EnsureDoorButton(panelTransform, "Baggage Yes Button", HeadLockedPos(-0.21f, -0.31f), HeadLockedSize(0.32f, 0.24f), "A / YES", HeadLockedFont(58f));
            EnsureDoorButton(panelTransform, "Baggage No Button", HeadLockedPos(0.21f, -0.31f), HeadLockedSize(0.32f, 0.24f), "B / NO", HeadLockedFont(58f));
            WireBaggageQuestionButtons(panelTransform, stateMachine);
        }

        private static Text EnsureBaggageQuestionText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing == null)
            {
                return CreateDoorText(name, parent, anchoredPosition, size, fontSize, color, alignment, value);
            }

            Text text = existing.GetComponent<Text>();
            if (text == null)
            {
                text = existing.gameObject.AddComponent<Text>();
                text.font = GetEditorRuntimeFont();
            }

            text.text = value;

            text.color = color;
            ConfigureText(text, anchoredPosition, size, fontSize, alignment);
            return text;
        }

        private static bool MatchHeadLockedPanelSettings(Canvas targetPanel, Canvas sourcePanel)
        {
            if (targetPanel == null || sourcePanel == null)
            {
                return false;
            }

            bool changed = false;
            RectTransform targetRect = targetPanel.GetComponent<RectTransform>();
            Vector2 standardSize = new Vector2(HeadLockedPanelWidth, HeadLockedPanelHeight);
            if (targetRect != null && targetRect.sizeDelta != standardSize)
            {
                targetRect.sizeDelta = standardSize;
                changed = true;
            }

            CanvasScaler targetScaler = targetPanel.GetComponent<CanvasScaler>();
            CanvasScaler sourceScaler = sourcePanel.GetComponent<CanvasScaler>();
            if (targetScaler != null && sourceScaler != null)
            {
                if (Mathf.Abs(targetScaler.dynamicPixelsPerUnit - HeadLockedCanvasPixelsPerUnit) > 0.0001f)
                {
                    targetScaler.dynamicPixelsPerUnit = HeadLockedCanvasPixelsPerUnit;
                    changed = true;
                }

                if (Mathf.Abs(targetScaler.referencePixelsPerUnit - sourceScaler.referencePixelsPerUnit) > 0.0001f)
                {
                    targetScaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
                    changed = true;
                }
            }

            CocoonHeadLockedUI targetHeadLocked = targetPanel.GetComponent<CocoonHeadLockedUI>();
            CocoonHeadLockedUI sourceHeadLocked = sourcePanel.GetComponent<CocoonHeadLockedUI>();
            if (targetHeadLocked != null && sourceHeadLocked != null)
            {
                var source = new SerializedObject(sourceHeadLocked);
                var target = new SerializedObject(targetHeadLocked);
                changed |= CopySerializedProperty(source, target, "target");
                changed |= CopySerializedProperty(source, target, "localPosition");
                changed |= CopySerializedProperty(source, target, "localEuler");
                changed |= SetFloat(target, "localScale", HeadLockedUiLocalScale);
                if (target.ApplyModifiedPropertiesWithoutUndo())
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static bool CopySerializedProperty(SerializedObject source, SerializedObject target, string propertyName)
        {
            SerializedProperty sourceProperty = source.FindProperty(propertyName);
            SerializedProperty targetProperty = target.FindProperty(propertyName);
            if (sourceProperty == null || targetProperty == null)
            {
                return false;
            }

            switch (sourceProperty.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    if (targetProperty.objectReferenceValue == sourceProperty.objectReferenceValue)
                    {
                        return false;
                    }

                    targetProperty.objectReferenceValue = sourceProperty.objectReferenceValue;
                    return true;
                case SerializedPropertyType.Vector3:
                    if (targetProperty.vector3Value == sourceProperty.vector3Value)
                    {
                        return false;
                    }

                    targetProperty.vector3Value = sourceProperty.vector3Value;
                    return true;
                case SerializedPropertyType.Float:
                    if (Mathf.Abs(targetProperty.floatValue - sourceProperty.floatValue) < 0.0001f)
                    {
                        return false;
                    }

                    targetProperty.floatValue = sourceProperty.floatValue;
                    return true;
                default:
                    return false;
            }
        }

        private static void WireBaggageQuestionButtons(Transform panelTransform, CocoonTaxiStateMachine stateMachine)
        {
            WireDoorPanelButton(panelTransform, "Baggage Yes Button", stateMachine, CocoonButtonAction.BaggageYes, "");
            WireDoorPanelButton(panelTransform, "Baggage No Button", stateMachine, CocoonButtonAction.BaggageNo, "");
        }

        private static bool EnsureHeadLockedUi(GameObject panelObject, Transform head)
        {
            if (panelObject == null)
            {
                return false;
            }

            bool changed = false;
            CocoonHeadLockedUI headLockedUi = panelObject.GetComponent<CocoonHeadLockedUI>();
            if (headLockedUi == null)
            {
                headLockedUi = panelObject.AddComponent<CocoonHeadLockedUI>();
                changed = true;
            }

            if (head != null)
            {
                var serialized = new SerializedObject(headLockedUi);
                changed |= SetObjectReference(serialized, "target", head);
                changed |= SetFloat(serialized, "localScale", HeadLockedUiLocalScale);
                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    changed = true;
                }
            }

            CanvasScaler scaler = panelObject.GetComponent<CanvasScaler>();
            if (scaler != null && Mathf.Abs(scaler.dynamicPixelsPerUnit - HeadLockedCanvasPixelsPerUnit) > 0.0001f)
            {
                scaler.dynamicPixelsPerUnit = HeadLockedCanvasPixelsPerUnit;
                changed = true;
            }

            CocoonBillboard billboard = panelObject.GetComponent<CocoonBillboard>();
            if (billboard != null && billboard.enabled)
            {
                billboard.enabled = false;
                changed = true;
            }

            return changed;
        }

        private static Transform CreatePassengerLuggage(Transform uiRoot)
        {
            var luggageObject = new GameObject(PassengerLuggageName);
            luggageObject.transform.SetParent(uiRoot, false);
            luggageObject.transform.localPosition = Vector3.zero;
            luggageObject.transform.localRotation = Quaternion.identity;
            luggageObject.transform.localScale = Vector3.one;

            Material shell = LoadMaterial("LuggageShell", new Color(0.055f, 0.075f, 0.095f));
            Material trim = LoadMaterial("LuggageTrim", new Color(0.1f, 1f, 0.75f));
            Material wheel = LoadMaterial("LuggageWheel", new Color(0.015f, 0.015f, 0.018f));

            CreateSceneCubeChild("Suitcase Body", luggageObject.transform, new Vector3(0f, 0.28f, 0f), Quaternion.identity, new Vector3(0.34f, 0.5f, 0.18f), shell, false);
            CreateSceneCubeChild("Suitcase Front Ridge", luggageObject.transform, new Vector3(0f, 0.28f, -0.095f), Quaternion.identity, new Vector3(0.3f, 0.42f, 0.018f), trim, false);
            CreateSceneCubeChild("Suitcase Handle Left", luggageObject.transform, new Vector3(-0.09f, 0.57f, 0f), Quaternion.identity, new Vector3(0.026f, 0.18f, 0.026f), wheel, false);
            CreateSceneCubeChild("Suitcase Handle Right", luggageObject.transform, new Vector3(0.09f, 0.57f, 0f), Quaternion.identity, new Vector3(0.026f, 0.18f, 0.026f), wheel, false);
            CreateSceneCubeChild("Suitcase Handle Top", luggageObject.transform, new Vector3(0f, 0.66f, 0f), Quaternion.identity, new Vector3(0.22f, 0.026f, 0.026f), wheel, false);
            CreateTaxiCylinderChild("Suitcase Wheel L", luggageObject.transform, new Vector3(-0.12f, 0.045f, -0.055f), new Vector3(0.045f, 0.026f, 0.045f), Quaternion.Euler(90f, 0f, 0f), wheel);
            CreateTaxiCylinderChild("Suitcase Wheel R", luggageObject.transform, new Vector3(0.12f, 0.045f, -0.055f), new Vector3(0.045f, 0.026f, 0.045f), Quaternion.Euler(90f, 0f, 0f), wheel);

            luggageObject.SetActive(false);
            Debug.Log("Cocoon repair created passenger luggage suitcase under 06_UI.");
            return luggageObject.transform;
        }

        private static bool EnsurePassengerLuggageModel(Transform luggage)
        {
            if (luggage == null)
            {
                return false;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PassengerLuggageModelPath);
            if (modelAsset == null)
            {
                AssetDatabase.ImportAsset(PassengerLuggageModelPath, ImportAssetOptions.ForceUpdate);
                modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PassengerLuggageModelPath);
            }

            if (modelAsset == null)
            {
                return false;
            }

            Transform model = luggage.Find(PassengerLuggageModelName);
            if (model != null)
            {
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject);
                if (string.IsNullOrEmpty(sourcePath) || PathsEqual(sourcePath, PassengerLuggageModelPath))
                {
                    RemoveProceduralSuitcaseChildren(luggage);
                    RemoveColliders(model);
                    return ApplyPassengerLuggageVisualBaseline(model);
                }

                UnityEngine.Object.DestroyImmediate(model.gameObject);
            }

            RemoveProceduralSuitcaseChildren(luggage);

            GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (modelInstance == null)
            {
                modelInstance = UnityEngine.Object.Instantiate(modelAsset);
            }

            modelInstance.name = PassengerLuggageModelName;
            modelInstance.transform.SetParent(luggage, false);
            ApplyPassengerLuggageVisualBaseline(modelInstance.transform);
            RemoveColliders(modelInstance.transform);
            return true;
        }

        private static bool ApplyPassengerLuggageVisualBaseline(Transform model)
        {
            return ApplyImportedAccessoryVisualBaseline(
                model,
                PassengerLuggageBaselineName,
                PassengerLuggageFallbackScale,
                PassengerLuggageFallbackRotation);
        }

        private static bool ApplyWheelchairVisualBaseline(Transform model)
        {
            return ApplyImportedAccessoryVisualBaseline(
                model,
                WheelchairBaselineName,
                WheelchairFallbackScale,
                WheelchairFallbackRotation);
        }

        private static bool ApplyImportedAccessoryVisualBaseline(
            Transform model,
            string baselineName,
            Vector3 fallbackScale,
            Quaternion fallbackRotation)
        {
            if (model == null)
            {
                return false;
            }

            Vector3 targetScale = fallbackScale;
            Quaternion targetRotation = fallbackRotation;
            Scene scene = model.gameObject.scene;
            if (scene.IsValid())
            {
                Transform baseline = FindTransform(scene, baselineName);
                if (baseline != null && baseline != model)
                {
                    targetScale = baseline.localScale;
                    targetRotation = baseline.localRotation;
                }
            }

            bool changed = false;
            if (Vector3.Distance(model.localPosition, Vector3.zero) > 0.000001f)
            {
                model.localPosition = Vector3.zero;
                changed = true;
            }

            if (Quaternion.Angle(model.localRotation, targetRotation) > 0.01f)
            {
                model.localRotation = targetRotation;
                changed = true;
            }

            if (Vector3.Distance(model.localScale, targetScale) > 0.0000001f)
            {
                model.localScale = targetScale;
                changed = true;
            }

            if (scene.IsValid())
            {
                Transform baseline = FindTransform(scene, baselineName);
                if (baseline != null &&
                    baseline != model &&
                    TryGetRendererBounds(baseline, out Bounds baselineBounds) &&
                    TryGetRendererBounds(model, out Bounds modelBounds))
                {
                    float baselineSize = Mathf.Max(baselineBounds.size.x, baselineBounds.size.y, baselineBounds.size.z);
                    float modelSize = Mathf.Max(modelBounds.size.x, modelBounds.size.y, modelBounds.size.z);
                    if (baselineSize > 0.0001f && modelSize > 0.0001f)
                    {
                        float scaleFactor = Mathf.Clamp(baselineSize / modelSize, 0.05f, 20f);
                        if (Mathf.Abs(scaleFactor - 1f) > 0.01f)
                        {
                            model.localScale *= scaleFactor;
                            changed = true;
                        }
                    }
                }
            }

            return changed;
        }

        private static void RemoveProceduralSuitcaseChildren(Transform luggage)
        {
            for (int i = luggage.childCount - 1; i >= 0; i--)
            {
                Transform child = luggage.GetChild(i);
                if (child != null && child.name.StartsWith("Suitcase ", StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void RemoveColliders(Transform root)
        {
            Collider[] colliders = root != null ? root.GetComponentsInChildren<Collider>(true) : null;
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(colliders[i]);
                }
            }
        }

        private static bool EnsureLuggageFollower(Transform luggage, Transform leftHand, Transform head)
        {
            Type followerType = FindType("CocoonPrototype.CocoonLuggageFollower");
            if (luggage == null || followerType == null)
            {
                return false;
            }

            bool changed = false;
            Component follower = luggage.GetComponent(followerType);
            if (follower == null)
            {
                follower = luggage.gameObject.AddComponent(followerType);
                changed = true;
            }

            Transform target = leftHand != null ? leftHand : head;
            if (target != null)
            {
                var serialized = new SerializedObject(follower);
                changed |= SetObjectReference(serialized, "target", target);
                changed |= SetObjectReference(serialized, "fallbackTarget", head);
                SerializedProperty preferLeftHandProperty = serialized.FindProperty("preferLeftHandTarget");
                if (preferLeftHandProperty != null && !preferLeftHandProperty.boolValue)
                {
                    preferLeftHandProperty.boolValue = true;
                    changed = true;
                }

                SerializedProperty scaleOffsetProperty = serialized.FindProperty("scaleWithExperience");
                if (scaleOffsetProperty != null && scaleOffsetProperty.boolValue)
                {
                    scaleOffsetProperty.boolValue = false;
                    changed = true;
                }

                SerializedProperty scaleModelProperty = serialized.FindProperty("scaleModelWithExperience");
                if (scaleModelProperty != null && scaleModelProperty.boolValue)
                {
                    scaleModelProperty.boolValue = false;
                    changed = true;
                }

                SerializedProperty alignBoundsProperty = serialized.FindProperty("alignRendererBoundsToTarget");
                if (alignBoundsProperty != null && !alignBoundsProperty.boolValue)
                {
                    alignBoundsProperty.boolValue = true;
                    changed = true;
                }

                SerializedProperty groundOffsetProperty = serialized.FindProperty("groundYOffset");
                if (groundOffsetProperty != null && Mathf.Abs(groundOffsetProperty.floatValue) > 0.0001f)
                {
                    groundOffsetProperty.floatValue = 0f;
                    changed = true;
                }

                SerializedProperty modelScaleProperty = serialized.FindProperty("modelScale");
                if (modelScaleProperty != null && modelScaleProperty.floatValue <= 0.001f)
                {
                    modelScaleProperty.floatValue = 1f;
                    changed = true;
                }

                SerializedProperty offsetProperty = serialized.FindProperty("localOffset");
                if (offsetProperty != null &&
                    (Vector3.Distance(offsetProperty.vector3Value, new Vector3(0.32f, -1.55f, 0.3f)) < 0.0001f ||
                     Vector3.Distance(offsetProperty.vector3Value, new Vector3(-0.32f, -1.55f, 0.3f)) < 0.0001f ||
                     Vector3.Distance(offsetProperty.vector3Value, new Vector3(-0.08f, -0.06f, 0.04f)) < 0.0001f ||
                     Vector3.Distance(offsetProperty.vector3Value, Vector3.zero) < 0.0001f))
                {
                    offsetProperty.vector3Value = new Vector3(-0.04f, 0f, 0.02f);
                    changed = true;
                }

                SerializedProperty restOffsetProperty = serialized.FindProperty("restLocalOffset");
                if (restOffsetProperty != null &&
                    (Vector3.Distance(restOffsetProperty.vector3Value, new Vector3(-0.22f, 0f, 0.05f)) < 0.0001f ||
                     Vector3.Distance(restOffsetProperty.vector3Value, Vector3.zero) < 0.0001f))
                {
                    restOffsetProperty.vector3Value = new Vector3(-0.12f, 0f, 0.03f);
                    changed = true;
                }

                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    changed = true;
                }
            }

            return changed;
        }

        private static void CreatePanelBackground(Transform parent, Color color, Vector2 size)
        {
            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = background.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            background.GetComponent<Image>().color = color;
        }

        private static Text CreateDoorText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = GetEditorRuntimeFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = fontSize;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2.3f, -2.3f);
            return text;
        }

        private static GameObject CreateDoorButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, string label, CocoonTaxiStateMachine stateMachine, CocoonButtonAction action, string payload)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BoxCollider), typeof(CocoonWorldButton));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image image = button.GetComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.13f, 0.95f);

            BoxCollider collider = button.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(size.x, size.y, 36f);

            button.GetComponent<CocoonWorldButton>().Configure(stateMachine, action, payload, image);
            Text labelText = CreateDoorText(name + " Text", button.transform, Vector2.zero, size, 56, Color.white, TextAnchor.MiddleCenter, label);
            labelText.fontStyle = FontStyle.Bold;
            return button;
        }

        private static Text FindChildText(Transform parent, string name)
        {
            Transform child = parent != null ? parent.Find(name) : null;
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static Font GetEditorRuntimeFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void WireDoorPanelButtons(Transform panelTransform, CocoonTaxiStateMachine stateMachine)
        {
            WireDoorPanelButton(panelTransform, "Confirm Ride Button", stateMachine, CocoonButtonAction.ConfirmRide, "");
            WireDoorPanelButton(panelTransform, "Leave Button", stateMachine, CocoonButtonAction.DeclineRide, "");
        }

        private static void WireDoorPanelButton(Transform panelTransform, string buttonName, CocoonTaxiStateMachine stateMachine, CocoonButtonAction action, string payload)
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
                button.Configure(stateMachine, action, payload, tintGraphic);
            }
        }

        private static void RepairDoorPanel(Canvas canvas, Transform head)
        {
            ConfigureCanvas(canvas, new Vector2(760f, 560f), 0.00095f);
            canvas.transform.localPosition = DoorPanelLocalPosition;
            canvas.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            canvas.transform.localScale = Vector3.one * 0.00095f;

            CocoonBillboard billboard = canvas.GetComponent<CocoonBillboard>();
            if (billboard != null)
            {
                UnityEngine.Object.DestroyImmediate(billboard);
            }

            SetPanelBackground(canvas.transform, new Vector2(760f, 560f));
            RemoveDuplicateTextComponents(canvas.transform);
            ConfigureText(canvas.transform, "Door Panel Title", new Vector2(0f, 194f), new Vector2(650f, 72f), 78, TextAnchor.MiddleCenter);
            ConfigureText(canvas.transform, "Door Panel Status", new Vector2(0f, 104f), new Vector2(650f, 64f), 40, TextAnchor.MiddleCenter);
            ConfigureText(canvas.transform, "Door Panel Destination", new Vector2(0f, -4f), new Vector2(650f, 152f), 38, TextAnchor.MiddleLeft);
            ConfigureText(canvas.transform, "Door Panel Timer", new Vector2(0f, -226f), new Vector2(540f, 46f), 40, TextAnchor.MiddleCenter);
            SetChildActive(canvas.transform, "Door Panel Destination", true);
            SetChildActive(canvas.transform, "Contactless Hint", false);

            EnsureDoorButton(canvas.transform, "Confirm Ride Button", new Vector2(-142f, -142f), new Vector2(248f, 78f), "A\nCONFIRM", 36);
            EnsureDoorButton(canvas.transform, "Leave Button", new Vector2(142f, -142f), new Vector2(248f, 78f), "B\nLEAVE", 36);
            SetChildActive(canvas.transform, "Duomo Button", false);
            SetChildActive(canvas.transform, "Central Button", false);
            SetChildActive(canvas.transform, "Brera Button", false);
            SetChildActive(canvas.transform, "Solo Button", false);
            SetChildActive(canvas.transform, "Shared Button", false);
            SetChildActive(canvas.transform, "Reset Button", false);
            LogPanelMetrics(canvas, "Door");
        }

        private static void ConfigureCanvas(Canvas canvas, Vector2 size, float scale)
        {
            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
                scaler.referencePixelsPerUnit = 100f;
            }

            canvas.transform.localScale = Vector3.one * scale;
        }

        private static void SetPanelBackground(Transform panel, Vector2 size)
        {
            Transform background = panel.Find("Background");
            if (background == null)
            {
                return;
            }

            RectTransform rect = background.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = size;
                rect.anchoredPosition = Vector2.zero;
            }

            Image image = background.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.015f, 0.025f, 0.032f, 0.985f);
            }
        }

        private static void EnsureDoorButton(Transform panel, string name, Vector2 position, Vector2 size, string label, int fontSize)
        {
            Transform button = panel.Find(name);
            if (button == null)
            {
                var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BoxCollider), typeof(CocoonWorldButton));
                button = buttonObject.transform;
                button.SetParent(panel, false);
                buttonObject.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.13f, 0.95f);
                CreateDoorText(name + " Text", button, Vector2.zero, size, fontSize, Color.white, TextAnchor.MiddleCenter, label).fontStyle = FontStyle.Bold;
            }

            button.gameObject.SetActive(true);
            ConfigureButton(panel, name, position, size, fontSize);
            Text labelText = button.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                labelText.text = label;
                labelText.resizeTextForBestFit = true;
                labelText.resizeTextMinSize = 18;
                labelText.resizeTextMaxSize = Mathf.Max(18, fontSize);
                labelText.lineSpacing = 0.82f;
                labelText.verticalOverflow = VerticalWrapMode.Overflow;
                labelText.SetAllDirty();
            }
        }

        private static void SetChildActive(Transform panel, string name, bool active)
        {
            Transform child = panel.Find(name);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private static void ConfigureButton(Transform panel, string name, Vector2 position, Vector2 size, int fontSize)
        {
            Transform button = panel.Find(name);
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }

            BoxCollider collider = button.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = button.gameObject.AddComponent<BoxCollider>();
            }

            collider.isTrigger = true;
            collider.size = new Vector3(size.x, size.y, 48f);

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                ConfigureText(label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
                label.fontStyle = FontStyle.Bold;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 18;
                label.resizeTextMaxSize = Mathf.Max(18, fontSize);
                label.lineSpacing = 0.82f;
                label.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private static void ConfigureText(Transform panel, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor)
        {
            Transform child = panel.Find(name);
            if (child == null)
            {
                return;
            }

            Text text = child.GetComponent<Text>();
            if (text == null)
            {
                return;
            }

            ConfigureText(text, position, size, fontSize, anchor);
        }

        private static void ConfigureText(Text text, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor)
        {
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            text.canvasRenderer.Clear();
            text.cachedTextGenerator.Invalidate();
            text.cachedTextGeneratorForLayout.Invalidate();
            text.fontSize = fontSize;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = fontSize;
            text.resizeTextMaxSize = fontSize;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            Outline outline = text.GetComponent<Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2.3f, -2.3f);
            text.SetAllDirty();
        }

        private static void RemoveDuplicateTextComponents(Transform root)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text[] siblings = texts[i].GetComponents<Text>();
                if (siblings.Length <= 1)
                {
                    continue;
                }

                for (int siblingIndex = 1; siblingIndex < siblings.Length; siblingIndex++)
                {
                    Debug.LogWarning("Cocoon UI repair removed duplicate Text component from " + texts[i].name + ".");
                    UnityEngine.Object.DestroyImmediate(siblings[siblingIndex], true);
                }
            }
        }

        private static void LogPanelMetrics(Canvas canvas, string label)
        {
            if (!VerboseRepairLogs)
            {
                return;
            }

            RectTransform rect = canvas.GetComponent<RectTransform>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            float pixelsPerUnit = scaler != null ? scaler.dynamicPixelsPerUnit : 1f;
            Vector3 scale = canvas.transform.lossyScale;
            Vector2 worldSize = new Vector2(rect.sizeDelta.x * scale.x, rect.sizeDelta.y * scale.y);
            Text largestText = null;
            Text[] texts = canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (largestText == null || texts[i].fontSize > largestText.fontSize)
                {
                    largestText = texts[i];
                }
            }

            float largestWorldTextHeight = largestText != null ? largestText.fontSize / pixelsPerUnit * scale.y : 0f;
            Debug.Log("Cocoon UI metrics [" + label + "]: canvas " + worldSize.x.ToString("0.00") + "m x " + worldSize.y.ToString("0.00") + "m, PPU " + pixelsPerUnit.ToString("0.0") + ", largest text about " + (largestWorldTextHeight * 100f).ToString("0.0") + "cm tall.");
        }

        private static bool SetNamedRootActive(Scene scene, string name, bool active)
        {
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != null && root.name == name && root.activeSelf != active)
                {
                    root.SetActive(active);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool SetNamedTransformActive(Scene scene, string name, bool active)
        {
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform transform = transforms[i];
                    if (transform != null && transform.name == name && transform.gameObject.activeSelf != active)
                    {
                        transform.gameObject.SetActive(active);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static bool NormalizeRootTransformPreservingChildren(Transform root)
        {
            if (root == null)
            {
                return false;
            }

            bool alreadyIdentity =
                root.parent == null &&
                Vector3.Distance(root.position, Vector3.zero) < 0.0001f &&
                Quaternion.Angle(root.rotation, Quaternion.identity) < 0.01f &&
                Vector3.Distance(root.localScale, Vector3.one) < 0.0001f;
            if (alreadyIdentity)
            {
                return false;
            }

            var children = new List<Transform>();
            for (int i = 0; i < root.childCount; i++)
            {
                children.Add(root.GetChild(i));
            }

            Transform previousParent = root.parent;
            for (int i = 0; i < children.Count; i++)
            {
                children[i].SetParent(previousParent, true);
            }

            root.SetParent(null, true);
            root.position = Vector3.zero;
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;
            for (int i = 0; i < children.Count; i++)
            {
                children[i].SetParent(root, true);
            }

            Debug.Log("Normalized " + root.name + " root transform while preserving child world positions and scale.", root);
            return true;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(root != null ? root.position : Vector3.zero, Vector3.zero);
            if (root == null)
            {
                return false;
            }

            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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

        private static bool ContainsHorizontal(Bounds bounds, Vector3 point, float pad)
        {
            return point.x >= bounds.min.x - pad &&
                   point.x <= bounds.max.x + pad &&
                   point.z >= bounds.min.z - pad &&
                   point.z <= bounds.max.z + pad;
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

        private static Canvas FindCanvas(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i].name == name)
                    {
                        return canvases[i];
                    }
                }
            }

            return null;
        }

        private static Transform FindTopLevelTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != null && root.name == name)
                {
                    return root.transform;
                }
            }

            return null;
        }

        private static Transform FindTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == name)
                    {
                        return transforms[i];
                    }
                }
            }

            return null;
        }

        private static Transform FindTransformDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static Transform FindTransformDeepAny(Transform root, string[] names)
        {
            if (root == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                Transform match = FindTransformDeep(root, names[i]);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindTransformDeepAny(Transform preferredRoot, Transform fallbackRoot, string[] names)
        {
            Transform match = FindTransformDeepAny(preferredRoot, names);
            return match != null ? match : FindTransformDeepAny(fallbackRoot, names);
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

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static Type FindType(string fullName)
        {
            Type type = Type.GetType(fullName + ", Assembly-CSharp");
            if (type != null)
            {
                return type;
            }

            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
