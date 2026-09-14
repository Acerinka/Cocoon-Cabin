using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CocoonPrototype;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR.Features;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using InputTrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace CocoonPrototype.Editor
{
    public static class CocoonPrototypeBootstrapper
    {
        private const string ScenePath = "Assets/Scenes/Cocoon_OnboardingVR.unity";
        private const string MaterialFolder = "Assets/CocoonPrototype/Materials";
        private const float WorldCanvasPixelsPerUnit = 768f;
        private const float ExteriorScreenReferenceWidth = 1180f;
        private const float ExteriorScreenSourceWidth = 23040f;
        private const float ExteriorScreenSourceHeight = 2080f;
        private const string ExteriorScreenResourcePath = "ExteriorScreens/ext-screen";
        private static Font defaultFont;
        private static Transform runtimeRoot;
        private static Transform lightingRoot;
        private static Transform streetRoot;
        private static Transform roadsRoot;
        private static Transform sidewalksRoot;
        private static Transform curbsRoot;
        private static Transform markingsRoot;
        private static Transform crosswalksRoot;
        private static Transform buildingsRoot;
        private static Transform streetLightsRoot;
        private static Transform trafficRoot;
        private static Transform taxiRoot;
        private static Transform pickupRoot;
        private static Transform uiRoot;

        [MenuItem("Cocoon/Setup Quest VR Prototype")]
        public static void SetupProject()
        {
            ConfigureProjectSettings();
            if (HasExistingAuthoredPrototypeScene())
            {
                EnsurePrototypeSceneBuildSetting();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Cocoon setup preserved the existing authored onboarding scene; taxi visuals were not regenerated.");
                return;
            }

            CreatePrototypeScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Cocoon Quest VR prototype setup complete.");
        }

        [MenuItem("Cocoon/Build Quest APK")]
        public static void BuildQuestApk()
        {
            SetupProject();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/CocoonQuestPrototype.apk", BuildTarget.Android, BuildOptions.Development);
            Debug.Log("Cocoon APK build result: " + report.summary.result + " at Builds/CocoonQuestPrototype.apk");
        }

        private static bool HasExistingAuthoredPrototypeScene()
        {
            if (!File.Exists(ScenePath))
            {
                return false;
            }

            string sceneText = File.ReadAllText(ScenePath);
            return sceneText.Contains("m_Name: Cocoon Autonomous Taxi");
        }

        private static void EnsurePrototypeSceneBuildSetting()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int existingIndex = scenes.FindIndex(scene => scene.path.Equals(ScenePath, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                scenes[existingIndex].enabled = true;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ConfigureProjectSettings()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.companyName = "Cocoon Cabin Team";
            PlayerSettings.productName = "Cocoon Cabin";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.cocoon.vrprototype");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.MTRendering = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            QualitySettings.antiAliasing = 8;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 1.8f);
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.CloseFit;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 70f);
            QualitySettings.vSyncCount = 0;
            SetActiveInputHandlingToInputSystem();
            ConfigureOpenXRForQuestLinkAndAndroid();
        }

        private static void SetActiveInputHandlingToInputSystem()
        {
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0)
            {
                return;
            }

            var serializedSettings = new SerializedObject(settings[0]);
            SerializedProperty inputHandler = serializedSettings.FindProperty("activeInputHandler");
            if (inputHandler != null)
            {
                inputHandler.intValue = 1;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureOpenXRForQuestLinkAndAndroid()
        {
            EnsureFolder("Assets/XR");
            EnsureFolder("Assets/XR/Loaders");

            XRGeneralSettingsPerBuildTarget buildTargetSettings = GetOrCreateXRGeneralSettings();
            ConfigureOpenXRForBuildTarget(buildTargetSettings, BuildTargetGroup.Android, "OpenXRLoader_Android");
            ConfigureOpenXRForBuildTarget(buildTargetSettings, BuildTargetGroup.Standalone, "OpenXRLoader_Standalone");

            AssetDatabase.SaveAssets();
            Debug.Log("Configured Android and Standalone OpenXR loaders; CocoonXRRuntimeLifecycle starts XR after initialization.");
        }

        private static void ConfigureOpenXRForBuildTarget(XRGeneralSettingsPerBuildTarget buildTargetSettings, BuildTargetGroup targetGroup, string loaderName)
        {
            if (!buildTargetSettings.HasSettingsForBuildTarget(targetGroup))
            {
                buildTargetSettings.CreateDefaultSettingsForBuildTarget(targetGroup);
            }

            if (!buildTargetSettings.HasManagerSettingsForBuildTarget(targetGroup))
            {
                buildTargetSettings.CreateDefaultManagerSettingsForBuildTarget(targetGroup);
            }

            XRManagerSettings managerSettings = buildTargetSettings.ManagerSettingsForBuildTarget(targetGroup);
            managerSettings.automaticLoading = false;
            managerSettings.automaticRunning = false;

            string loaderPath = "Assets/XR/Loaders/" + loaderName + ".asset";
            OpenXRLoader loader = AssetDatabase.LoadAssetAtPath<OpenXRLoader>(loaderPath);
            if (loader == null)
            {
                loader = ScriptableObject.CreateInstance<OpenXRLoader>();
                loader.name = loaderName;
                AssetDatabase.CreateAsset(loader, loaderPath);
            }

            RemoveDuplicateOpenXRLoaders(managerSettings, loader);
            if (!managerSettings.activeLoaders.Contains(loader) && !managerSettings.TryAddLoader(loader))
            {
                Debug.LogWarning("OpenXR loader asset exists, but XRManagerSettings.TryAddLoader returned false.");
            }

            FeatureHelpers.RefreshFeatures(targetGroup);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.metaquest", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.oculustouch", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.metaquestplus", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handinteraction", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handinteractionposes", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.handtracking", true);
            SetOpenXRFeature(targetGroup, "com.unity.openxr.feature.input.metahandtrackingaim", true);

            EditorUtility.SetDirty(loader);
            EditorUtility.SetDirty(managerSettings);
            EditorUtility.SetDirty(buildTargetSettings);
        }

        private static void RemoveDuplicateOpenXRLoaders(XRManagerSettings managerSettings, OpenXRLoader requiredLoader)
        {
            OpenXRLoader[] duplicates = managerSettings.activeLoaders
                .OfType<OpenXRLoader>()
                .Where(existingLoader => existingLoader != requiredLoader)
                .ToArray();

            for (int i = 0; i < duplicates.Length; i++)
            {
                managerSettings.TryRemoveLoader(duplicates[i]);
            }
        }
        private static void SetOpenXRFeature(BuildTargetGroup targetGroup, string featureId, bool enabled)
        {
            OpenXRFeature feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(targetGroup, featureId);
            if (feature == null)
            {
                Debug.LogWarning("OpenXR feature not found for " + targetGroup + ": " + featureId);
                return;
            }

            feature.enabled = enabled;
            EditorUtility.SetDirty(feature);
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreateXRGeneralSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget buildTargetSettings) && buildTargetSettings != null)
            {
                return buildTargetSettings;
            }

            EnsureFolder("Assets/XR");
            EnsureFolder("Assets/XR/Settings");
            const string settingsPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
            buildTargetSettings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
            if (buildTargetSettings == null)
            {
                buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(buildTargetSettings, settingsPath);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
            return buildTargetSettings;
        }

        private static void CreatePrototypeScene()
        {
            EnsureFolder("Assets/CocoonPrototype");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/ImportedAssets");
            EnsureFolder("Assets/Scenes");

            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.88f, 0.93f);
            RenderSettings.ambientEquatorColor = new Color(0.64f, 0.68f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.44f, 0.46f, 0.48f);
            RenderSettings.reflectionIntensity = 0.78f;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            CreateHierarchyRoots();

            Material roadMat = CreateMaterial("Road", new Color(0.055f, 0.065f, 0.072f));
            Material sidewalkMat = CreateMaterial("Sidewalk", new Color(0.44f, 0.47f, 0.47f));
            Material curbMat = CreateMaterial("Curb", new Color(0.75f, 0.77f, 0.73f));
            Material stripeMat = CreateMaterial("Crosswalk", new Color(0.93f, 0.94f, 0.9f));
            Material zoneMat = CreateMaterial("SafePickupZone", new Color(0.02f, 0.74f, 0.6f), true);
            Material bodyMat = CreateMaterial("TaxiBody", new Color(0.82f, 0.88f, 0.86f));
            Material glassMat = CreateMaterial("TaxiGlass", new Color(0.08f, 0.16f, 0.2f));
            Material tireMat = CreateMaterial("Tires", new Color(0.015f, 0.015f, 0.018f));
            Material lightMat = CreateMaterial("TaxiLights", new Color(0.12f, 0.95f, 1f), true);
            Material handMat = CreateMaterial("HandProxy", new Color(0.08f, 0.28f, 1f, 1f));
            Material rayMat = CreateMaterial("PointerRay", new Color(0.25f, 0.78f, 1f), true);
            Material buildingMat = CreateMaterial("Buildings", new Color(0.18f, 0.22f, 0.24f));
            Material buildingWarmMat = CreateMaterial("BuildingsWarm", new Color(0.36f, 0.32f, 0.28f));
            Material buildingLightMat = CreateMaterial("BuildingsLight", new Color(0.46f, 0.50f, 0.48f));
            Material buildingDarkMat = CreateMaterial("BuildingsDark", new Color(0.11f, 0.14f, 0.16f));
            Material windowMat = CreateMaterial("BuildingWindows", new Color(0.1f, 0.55f, 0.72f), true);
            Material shopfrontMat = CreateMaterial("ShopfrontGlass", new Color(0.04f, 0.24f, 0.28f), true);
            Material awningMat = CreateMaterial("StreetAwnings", new Color(0.64f, 0.18f, 0.24f));
            Material planterMat = CreateMaterial("PlanterGreen", new Color(0.1f, 0.36f, 0.22f));
            Material streetFurnitureMat = CreateMaterial("StreetFurniture", new Color(0.09f, 0.095f, 0.1f));
            Material trafficBlueMat = CreateMaterial("TrafficBlue", new Color(0.18f, 0.35f, 0.62f));
            Material trafficRedMat = CreateMaterial("TrafficRed", new Color(0.62f, 0.16f, 0.12f));
            Material trafficYellowMat = CreateMaterial("TrafficYellow", new Color(0.82f, 0.62f, 0.14f));
            Material arrowMat = CreateMaterial("GuidanceArrow", new Color(0.1f, 0.95f, 0.58f), true);

            CreateLighting();
            Transform[] physicalPickupBays = BuildStreet(
                roadMat,
                sidewalkMat,
                curbMat,
                stripeMat,
                zoneMat,
                buildingMat,
                buildingWarmMat,
                buildingLightMat,
                buildingDarkMat,
                windowMat,
                shopfrontMat,
                awningMat,
                planterMat,
                streetFurnitureMat);
            CocoonJapaneseCityAssetIntegrator.ApplyToCurrentSceneIfAvailable(false, false);
            CocoonTrafficLanePath trafficLoop = BuildTraffic(trafficBlueMat, trafficRedMat, trafficYellowMat, glassMat, tireMat, physicalPickupBays, out Transform[] pickupBays, out int[] pickupBayPathIndices);

            Transform xrOrigin = BuildXRRig(handMat, out Transform head, out Transform rightHand, out Transform leftHand, out CocoonXRNodePose rightHandPose, out CocoonXRNodePose leftHandPose);
            BuildEventSystem();
            var safeZone = BuildSafePickupZone(zoneMat, arrowMat, head, out GameObject guidanceRoot);

            Transform cruiseStart = trafficLoop != null ? trafficLoop.GetWaypoint(0) : CreateMarker("Cruise Start", new Vector3(0.9f, 0f, 38f), Quaternion.Euler(0f, 180f, 0f));
            Transform cruiseEnd = trafficLoop != null ? trafficLoop.GetWaypoint(1) : CreateMarker("Cruise End", new Vector3(0.9f, 0f, 30f), Quaternion.Euler(0f, 180f, 0f));
            Transform pullOver = pickupBays != null && pickupBays.Length > 0 ? pickupBays[0] : CreateMarker("Safe Pull-over Point", new Vector3(-4.05f, 0f, 10f), Quaternion.Euler(0f, 180f, 0f));

            GameObject taxi = BuildTaxi(bodyMat, glassMat, tireMat, lightMat, out Transform doorHinge, out Transform paymentReader, out Renderer paymentReaderRenderer, out Renderer bodyRenderer, out Renderer[] lightRenderers, out Text windshieldText, out Text rearWindshieldText);
            Transform loopStart = trafficLoop != null ? trafficLoop.GetWaypoint(0) : cruiseStart;
            Transform loopNext = trafficLoop != null ? trafficLoop.GetWaypoint(1) : cruiseEnd;
            taxi.transform.position = loopStart != null ? loopStart.position : cruiseStart.position;
            if (loopStart != null && loopNext != null)
            {
                Vector3 direction = loopNext.position - loopStart.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    taxi.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            GameObject manager = new GameObject("Cocoon Experience");
            manager.transform.SetParent(runtimeRoot, false);
            manager.AddComponent<CocoonXRDisplayQuality>();
            var raiseDetector = manager.AddComponent<CocoonRaiseHandDetector>();
            raiseDetector.Configure(head, rightHand, rightHandPose, taxi.transform, leftHand, leftHandPose);
            var confirmDetector = manager.AddComponent<CocoonConfirmGestureDetector>();
            var taxiStateMachine = manager.AddComponent<CocoonTaxiStateMachine>();
            GameObject doorPanel = BuildDoorPanel(doorHinge, taxiStateMachine, out Text panelHeadline, out Text panelStatus, out Text panelTimer, out Text destinationText);
            GameObject instructionPanel = BuildInstructionPanel(head, out Text instructionHeadline, out Text instructionStatus, out Text instructionTimer, out Text instructionText);

            taxiStateMachine.Configure(
                taxi.transform,
                cruiseStart,
                cruiseEnd,
                trafficLoop,
                0,
                pullOver,
                pickupBays,
                pickupBayPathIndices,
                doorHinge,
                doorPanel,
                rightHand,
                paymentReader,
                paymentReaderRenderer,
                raiseDetector,
                confirmDetector,
                safeZone,
                guidanceRoot,
                lightRenderers,
                bodyRenderer,
                instructionPanel,
                instructionHeadline,
                instructionStatus,
                instructionTimer,
                panelHeadline,
                panelStatus,
                panelTimer,
                destinationText,
                instructionText,
                windshieldText,
                rearWindshieldText);

            var pointerObject = new GameObject("Right Hand Ray Pointer");
            pointerObject.transform.SetParent(runtimeRoot, false);
            var lineRenderer = pointerObject.AddComponent<LineRenderer>();
            lineRenderer.sharedMaterial = rayMat;
            var pointer = pointerObject.AddComponent<CocoonXRPointer>();
            pointer.Configure(head, rightHand, rightHandPose, confirmDetector, taxiStateMachine);

            doorPanel.SetActive(false);
            guidanceRoot.SetActive(false);
            Selection.activeGameObject = manager;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void CreateLighting()
        {
            var sun = new GameObject("Soft Directional Light");
            sun.transform.SetParent(lightingRoot, false);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.965f, 0.91f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.42f;
            light.shadowBias = 0.075f;
            light.shadowNormalBias = 0.42f;
            light.shadowNearPlane = 0.1f;
            light.shadowResolution = LightShadowResolution.VeryHigh;
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
        }

        private static void CreateHierarchyRoots()
        {
            runtimeRoot = CreateGroup("00_Runtime", null);
            lightingRoot = CreateGroup("01_Lighting", null);
            streetRoot = CreateGroup("02_Street_Block", null);
            roadsRoot = CreateGroup("Roads", streetRoot);
            sidewalksRoot = CreateGroup("Sidewalks", streetRoot);
            curbsRoot = CreateGroup("Curbs", streetRoot);
            markingsRoot = CreateGroup("Lane Markings", streetRoot);
            crosswalksRoot = CreateGroup("Crosswalks", streetRoot);
            buildingsRoot = CreateGroup("Buildings", streetRoot);
            streetLightsRoot = CreateGroup("Street Lights", streetRoot);
            trafficRoot = CreateGroup("03_Traffic", null);
            taxiRoot = CreateGroup("04_Cocoon_Taxi", null);
            pickupRoot = CreateGroup("05_Pickup_Guidance", null);
            uiRoot = CreateGroup("06_UI", null);
            uiRoot.gameObject.AddComponent<CocoonHeadLockedUITuning>();
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            if (parent != null)
            {
                group.transform.SetParent(parent, false);
            }

            return group.transform;
        }

        private static Transform BuildXRRig(Material handMaterial, out Transform head, out Transform rightHand, out Transform leftHand, out CocoonXRNodePose rightHandPose, out CocoonXRNodePose leftHandPose)
        {
            var origin = new GameObject("XR Origin - Sidewalk Rider");
            origin.transform.SetParent(runtimeRoot, false);
            origin.transform.position = new Vector3(-5.35f, 0f, -11.5f);
            origin.transform.rotation = Quaternion.Euler(0f, 78f, 0f);
            var characterController = origin.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.height = 1.8f;
            characterController.radius = 0.08f;

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(origin.transform, false);
            cameraOffset.transform.localPosition = Vector3.zero;

            var xrOrigin = origin.AddComponent<XROrigin>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            cameraObject.transform.localRotation = Quaternion.identity;
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.005f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.76f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CocoonXRNodePose>().Configure(XRNode.CenterEye, new Vector3(0f, 1.65f, 0f), Vector3.zero);
            AddCenterEyeTrackedPoseDriver(cameraObject);
            xrOrigin.Camera = camera;
            xrOrigin.CameraFloorOffsetObject = cameraOffset;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            head = cameraObject.transform;

            GameObject right = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            right.name = "Right Controller - Hail and UI Ray";
            right.transform.SetParent(cameraOffset.transform, false);
            right.transform.localPosition = new Vector3(0.38f, 1.24f, 0.55f);
            right.transform.localScale = Vector3.one * 0.035f;
            right.GetComponent<Renderer>().sharedMaterial = handMaterial;
            rightHandPose = right.AddComponent<CocoonXRNodePose>();
            rightHandPose.Configure(XRNode.RightHand, new Vector3(0.38f, 1.24f, 0.55f), new Vector3(18f, 0f, 0f));
            var rightController = right.AddComponent<XRController>();
            rightController.controllerNode = XRNode.RightHand;
            rightController.selectUsage = InputHelpers.Button.Trigger;
            rightController.activateUsage = InputHelpers.Button.Grip;
            right.AddComponent<XRRayInteractor>();
            rightHand = right.transform;

            GameObject left = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            left.name = "Left Controller - Move and Teleport";
            left.transform.SetParent(cameraOffset.transform, false);
            left.transform.localPosition = new Vector3(-0.35f, 1.18f, 0.52f);
            left.transform.localScale = Vector3.one * 0.032f;
            left.GetComponent<Renderer>().sharedMaterial = handMaterial;
            leftHandPose = left.AddComponent<CocoonXRNodePose>();
            leftHandPose.Configure(XRNode.LeftHand, new Vector3(-0.35f, 1.18f, 0.52f), new Vector3(18f, -10f, 0f));
            var leftController = left.AddComponent<XRController>();
            leftController.controllerNode = XRNode.LeftHand;
            leftController.selectUsage = InputHelpers.Button.Trigger;
            leftController.activateUsage = InputHelpers.Button.Grip;
            leftHand = left.transform;

            var interactionManagerObject = new GameObject("XR Interaction Manager");
            interactionManagerObject.transform.SetParent(runtimeRoot, false);
            interactionManagerObject.AddComponent<XRInteractionManager>();

            var locomotionSystem = origin.AddComponent<LocomotionSystem>();
            locomotionSystem.xrOrigin = xrOrigin;
            var moveProvider = origin.AddComponent<DeviceBasedContinuousMoveProvider>();
            moveProvider.moveSpeed = 1.45f;
            moveProvider.forwardSource = head;
            moveProvider.enabled = false;
            var snapTurnProvider = origin.AddComponent<DeviceBasedSnapTurnProvider>();
            snapTurnProvider.enabled = false;
            origin.AddComponent<TeleportationProvider>().system = locomotionSystem;

            GameObject teleportMarker = CreateCube("Teleport Target Marker", new Vector3(-5.35f, 0.035f, -11.5f), new Vector3(0.32f, 0.03f, 0.32f), handMaterial, runtimeRoot);
            teleportMarker.SetActive(false);
            var locomotionLine = origin.AddComponent<LineRenderer>();
            locomotionLine.sharedMaterial = handMaterial;
            var locomotion = origin.AddComponent<CocoonVRLocomotion>();
            locomotion.Configure(origin.transform, head, leftHand, teleportMarker);

            return origin.transform;
        }

        private static void AddCenterEyeTrackedPoseDriver(GameObject cameraObject)
        {
            var driver = cameraObject.GetComponent<InputTrackedPoseDriver>();
            if (driver == null)
            {
                driver = cameraObject.AddComponent<InputTrackedPoseDriver>();
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

        private static Transform[] BuildStreet(
            Material roadMat,
            Material sidewalkMat,
            Material curbMat,
            Material stripeMat,
            Material zoneMat,
            Material buildingMat,
            Material buildingWarmMat,
            Material buildingLightMat,
            Material buildingDarkMat,
            Material windowMat,
            Material shopfrontMat,
            Material awningMat,
            Material planterMat,
            Material streetFurnitureMat)
        {
            CreateCube("West Avenue Road", new Vector3(0f, -0.03f, 0f), new Vector3(6f, 0.06f, 82f), roadMat);
            CreateCube("East Avenue Road", new Vector3(34f, -0.03f, 0f), new Vector3(6f, 0.06f, 82f), roadMat);
            CreateCube("North Street Road", new Vector3(17f, -0.025f, 28f), new Vector3(46f, 0.06f, 6f), roadMat);
            CreateCube("South Street Road", new Vector3(17f, -0.025f, -28f), new Vector3(46f, 0.06f, 6f), roadMat);

            CreateCube("Sidewalk - Player West", new Vector3(-5.35f, 0.02f, 0f), new Vector3(4.3f, 0.08f, 86f), sidewalkMat);
            CreateCube("Sidewalk - East Outer", new Vector3(39.35f, 0.02f, 0f), new Vector3(4.3f, 0.08f, 86f), sidewalkMat);
            CreateCube("Sidewalk - North Outer", new Vector3(17f, 0.021f, 33.35f), new Vector3(50f, 0.08f, 4.3f), sidewalkMat);
            CreateCube("Sidewalk - South Outer", new Vector3(17f, 0.021f, -33.35f), new Vector3(50f, 0.08f, 4.3f), sidewalkMat);
            CreateCube("Central City Block Walk", new Vector3(17f, 0.025f, 0f), new Vector3(22f, 0.08f, 44f), sidewalkMat);

            CreateCube("West Avenue Outer Curb", new Vector3(-3.05f, 0.08f, 0f), new Vector3(0.18f, 0.16f, 82f), curbMat);
            CreateCube("West Avenue Inner Curb", new Vector3(3.05f, 0.08f, 0f), new Vector3(0.18f, 0.16f, 82f), curbMat);
            CreateCube("East Avenue Inner Curb", new Vector3(30.95f, 0.08f, 0f), new Vector3(0.18f, 0.16f, 82f), curbMat);
            CreateCube("East Avenue Outer Curb", new Vector3(37.05f, 0.08f, 0f), new Vector3(0.18f, 0.16f, 82f), curbMat);
            CreateCube("North Street Outer Curb", new Vector3(17f, 0.08f, 31.05f), new Vector3(46f, 0.16f, 0.18f), curbMat);
            CreateCube("North Street Inner Curb", new Vector3(17f, 0.08f, 24.95f), new Vector3(46f, 0.16f, 0.18f), curbMat);
            CreateCube("South Street Inner Curb", new Vector3(17f, 0.08f, -24.95f), new Vector3(46f, 0.16f, 0.18f), curbMat);
            CreateCube("South Street Outer Curb", new Vector3(17f, 0.08f, -31.05f), new Vector3(46f, 0.16f, 0.18f), curbMat);

            Transform[] pickupStops =
            {
                CreatePickupBay("West Pickup Bay A", new Vector3(-4.05f, 0f, 10f), Quaternion.LookRotation(Vector3.back, Vector3.up), roadMat, curbMat, stripeMat),
                CreatePickupBay("West Pickup Bay B", new Vector3(-4.05f, 0f, -10f), Quaternion.LookRotation(Vector3.back, Vector3.up), roadMat, curbMat, stripeMat),
                CreatePickupBay("South Pickup Bay", new Vector3(14f, 0f, -32.05f), Quaternion.LookRotation(Vector3.right, Vector3.up), roadMat, curbMat, stripeMat),
                CreatePickupBay("East Pickup Bay A", new Vector3(38.05f, 0f, -10f), Quaternion.LookRotation(Vector3.forward, Vector3.up), roadMat, curbMat, stripeMat),
                CreatePickupBay("East Pickup Bay B", new Vector3(38.05f, 0f, 18f), Quaternion.LookRotation(Vector3.forward, Vector3.up), roadMat, curbMat, stripeMat),
                CreatePickupBay("North Pickup Bay", new Vector3(20f, 0f, 32.05f), Quaternion.LookRotation(Vector3.left, Vector3.up), roadMat, curbMat, stripeMat)
            };

            for (int i = 0; i < 19; i++)
            {
                float z = -36f + i * 4f;
                CreateCube("West Avenue Lane Dash " + i, new Vector3(0f, 0.025f, z), new Vector3(0.08f, 0.025f, 1.55f), stripeMat);
                CreateCube("East Avenue Lane Dash " + i, new Vector3(34f, 0.025f, z), new Vector3(0.08f, 0.025f, 1.55f), stripeMat);
            }

            for (int i = 0; i < 13; i++)
            {
                float x = -4f + i * 3.5f;
                CreateCube("North Street Lane Dash " + i, new Vector3(x, 0.025f, 28f), new Vector3(1.35f, 0.025f, 0.08f), stripeMat);
                CreateCube("South Street Lane Dash " + i, new Vector3(x, 0.025f, -28f), new Vector3(1.35f, 0.025f, 0.08f), stripeMat);
            }

            CreateCrosswalk("West North Crosswalk", new Vector3(0f, 0.035f, 28f), true, stripeMat);
            CreateCrosswalk("West South Crosswalk", new Vector3(0f, 0.035f, -28f), true, stripeMat);
            CreateCrosswalk("East North Crosswalk", new Vector3(34f, 0.035f, 28f), true, stripeMat);
            CreateCrosswalk("East South Crosswalk", new Vector3(34f, 0.035f, -28f), true, stripeMat);

            BuildUrbanEnvironment(
                new[] { buildingMat, buildingWarmMat, buildingLightMat, buildingDarkMat },
                windowMat,
                shopfrontMat,
                awningMat,
                planterMat,
                streetFurnitureMat,
                curbMat,
                stripeMat);

            for (int i = 0; i < 9; i++)
            {
                float z = -34f + i * 8.5f;
                CreateStreetLight("Street Light West " + i, new Vector3(-3.55f, 1.45f, z), curbMat, stripeMat);
                CreateStreetLight("Street Light East " + i, new Vector3(37.55f, 1.45f, z + 2.2f), curbMat, stripeMat);
            }

            CreateCube("Pickup Sign Pole West", new Vector3(-5.85f, 0.78f, 10f), new Vector3(0.06f, 1.35f, 0.06f), curbMat);
            CreateCube("Pickup Sign Face West", new Vector3(-5.85f, 1.52f, 10f), new Vector3(0.7f, 0.4f, 0.05f), stripeMat);
            return pickupStops;
        }

        private static void BuildUrbanEnvironment(
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
                CreateArchitecturalBuilding("West Mixed Use Building " + i, new Vector3(-9.75f, 0f, z), Quaternion.LookRotation(Vector3.right, Vector3.up), new Vector3(width, height, depth), SelectFacade(facadeMats, i), windowMat, shopfrontMat, awningMat, i);
            }

            for (int i = 0; i < 12; i++)
            {
                float z = -36f + i * 6.8f;
                float width = 2.8f + (i % 2) * 0.55f;
                float depth = 3.0f + (i % 4) * 0.28f;
                float height = 3.4f + (i % 4) * 0.72f;
                CreateArchitecturalBuilding("East Arcade Building " + i, new Vector3(43.35f, 0f, z + 1.25f), Quaternion.LookRotation(Vector3.left, Vector3.up), new Vector3(width, height, depth), SelectFacade(facadeMats, i + 3), windowMat, shopfrontMat, awningMat, i + 10);
            }

            for (int i = 0; i < 7; i++)
            {
                float x = -1f + i * 6.0f;
                float height = 3.0f + (i % 3) * 0.8f;
                CreateArchitecturalBuilding("North Street Building " + i, new Vector3(x, 0f, 37.9f), Quaternion.LookRotation(Vector3.back, Vector3.up), new Vector3(4.2f, height, 3.0f), SelectFacade(facadeMats, i + 17), windowMat, shopfrontMat, awningMat, i + 20);
                CreateArchitecturalBuilding("South Street Building " + i, new Vector3(x + 1.8f, 0f, -37.9f), Quaternion.LookRotation(Vector3.forward, Vector3.up), new Vector3(4.0f, height + 0.45f, 3.0f), SelectFacade(facadeMats, i + 24), windowMat, shopfrontMat, awningMat, i + 30);
            }

            for (int i = 0; i < 9; i++)
            {
                float x = 7.2f + (i % 3) * 4.8f;
                float z = -14f + (i / 3) * 9.0f;
                float height = 2.4f + (i % 4) * 0.45f;
                CreateArchitecturalBuilding("Central Courtyard Building " + i, new Vector3(x, 0f, z), Quaternion.LookRotation(i % 2 == 0 ? Vector3.back : Vector3.forward, Vector3.up), new Vector3(3.2f, height, 2.8f), SelectFacade(facadeMats, i + 31), windowMat, shopfrontMat, awningMat, i + 40);
            }

            CreateTransitShelter("West Pickup Shelter", new Vector3(-6.35f, 0f, 10f), Quaternion.LookRotation(Vector3.right, Vector3.up), streetFurnitureMat, shopfrontMat, stripeMat);
            CreateTransitShelter("East Pickup Shelter", new Vector3(40.55f, 0f, 18f), Quaternion.LookRotation(Vector3.left, Vector3.up), streetFurnitureMat, shopfrontMat, stripeMat);

            for (int i = 0; i < 7; i++)
            {
                float z = -28f + i * 9.5f;
                CreatePlanter("West Planter " + i, new Vector3(-6.55f, 0f, z), Quaternion.identity, planterMat, curbMat);
                CreatePlanter("East Planter " + i, new Vector3(39.95f, 0f, z + 2.5f), Quaternion.identity, planterMat, curbMat);
            }

            for (int i = 0; i < 5; i++)
            {
                float z = -24f + i * 12f;
                CreateBench("West Bench " + i, new Vector3(-5.9f, 0f, z), Quaternion.Euler(0f, 90f, 0f), streetFurnitureMat);
                CreateBench("East Bench " + i, new Vector3(39.0f, 0f, z + 5f), Quaternion.Euler(0f, -90f, 0f), streetFurnitureMat);
            }
        }

        private static Material SelectFacade(Material[] facadeMats, int seed)
        {
            if (facadeMats == null || facadeMats.Length == 0)
            {
                return null;
            }

            return facadeMats[Mathf.Abs(seed) % facadeMats.Length];
        }

        private static void CreateArchitecturalBuilding(
            string name,
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
            root.transform.SetParent(buildingsRoot, true);
            root.transform.position = groundCenter;
            root.transform.rotation = rotation;

            float width = Mathf.Max(1f, size.x);
            float height = Mathf.Max(1.8f, size.y);
            float depth = Mathf.Max(1f, size.z);
            float frontZ = depth * 0.5f + 0.026f;

            CreateDecorCubeChild("Facade Mass", root.transform, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), facadeMat);
            CreateDecorCubeChild("Roof Cap", root.transform, new Vector3(0f, height + 0.08f, 0f), new Vector3(width + 0.18f, 0.16f, depth + 0.18f), facadeMat);
            CreateDecorCubeChild("Ground Floor Shopfront", root.transform, new Vector3(0f, 0.66f, frontZ), new Vector3(width * 0.78f, 0.72f, 0.04f), shopfrontMat);
            CreateDecorCubeChild("Awning Band", root.transform, new Vector3(0f, 1.13f, frontZ + 0.035f), new Vector3(width * 0.86f, 0.12f, 0.1f), awningMat);

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
                    CreateDecorCubeChild("Window " + floor + "-" + column, root.transform, new Vector3(x, y, frontZ + 0.012f), new Vector3(windowWidth, 0.34f, 0.035f), windowMat);
                }
            }

            if (seed % 2 == 0)
            {
                CreateDecorCubeChild("Side Sign", root.transform, new Vector3(width * 0.42f, 1.58f, frontZ + 0.04f), new Vector3(0.12f, 0.72f, 0.055f), awningMat);
            }
        }

        private static void CreateTransitShelter(string name, Vector3 position, Quaternion rotation, Material frameMat, Material glassMat, Material signMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(streetRoot, true);
            root.transform.position = position;
            root.transform.rotation = rotation;
            CreateDecorCubeChild("Shelter Roof", root.transform, new Vector3(0f, 1.72f, 0f), new Vector3(1.65f, 0.1f, 0.64f), frameMat);
            CreateDecorCubeChild("Shelter Back Glass", root.transform, new Vector3(0f, 0.95f, -0.28f), new Vector3(1.55f, 1.18f, 0.045f), glassMat);
            CreateDecorCubeChild("Shelter Side Glass", root.transform, new Vector3(-0.78f, 0.95f, 0f), new Vector3(0.045f, 1.18f, 0.5f), glassMat);
            CreateDecorCubeChild("Shelter Seat", root.transform, new Vector3(0f, 0.42f, 0.05f), new Vector3(1.18f, 0.1f, 0.26f), frameMat);
            CreateDecorCubeChild("Shelter Pickup Sign", root.transform, new Vector3(0f, 1.48f, 0.34f), new Vector3(1.0f, 0.28f, 0.05f), signMat);
        }

        private static void CreatePlanter(string name, Vector3 position, Quaternion rotation, Material plantMat, Material baseMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(streetRoot, true);
            root.transform.position = position;
            root.transform.rotation = rotation;
            CreateDecorCubeChild("Planter Base", root.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.78f, 0.34f, 0.42f), baseMat);
            CreateDecorCubeChild("Plant Mass", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.66f, 0.22f, 0.34f), plantMat);
        }

        private static void CreateBench(string name, Vector3 position, Quaternion rotation, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(streetRoot, true);
            root.transform.position = position;
            root.transform.rotation = rotation;
            CreateDecorCubeChild("Seat", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(1.05f, 0.08f, 0.28f), material);
            CreateDecorCubeChild("Back", root.transform, new Vector3(0f, 0.66f, -0.15f), new Vector3(1.05f, 0.34f, 0.06f), material);
            CreateDecorCubeChild("Left Leg", root.transform, new Vector3(-0.42f, 0.23f, 0f), new Vector3(0.08f, 0.32f, 0.08f), material);
            CreateDecorCubeChild("Right Leg", root.transform, new Vector3(0.42f, 0.23f, 0f), new Vector3(0.08f, 0.32f, 0.08f), material);
        }

        private static Transform CreatePickupBay(string name, Vector3 center, Quaternion rotation, Material roadMat, Material curbMat, Material stripeMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(streetRoot, true);
            root.transform.position = center;
            root.transform.rotation = rotation;

            var stop = new GameObject("Pickup Bay Stop");
            stop.transform.SetParent(root.transform, false);
            stop.transform.localPosition = Vector3.zero;
            stop.transform.localRotation = Quaternion.identity;

            CreateCubeChild("Recessed Bay Asphalt", root.transform, new Vector3(0f, 0.055f, 0f), new Vector3(2.05f, 0.035f, 5.35f), roadMat);
            CreateCubeChild("Outer Curb Return", root.transform, new Vector3(1.12f, 0.075f, 0f), new Vector3(0.16f, 0.14f, 5.65f), curbMat);
            CreateCubeChild("Front Curb Return", root.transform, new Vector3(0f, 0.075f, 2.82f), new Vector3(2.25f, 0.14f, 0.16f), curbMat);
            CreateCubeChild("Rear Curb Return", root.transform, new Vector3(0f, 0.075f, -2.82f), new Vector3(2.25f, 0.14f, 0.16f), curbMat);
            CreateCubeChild("White Bay Edge", root.transform, new Vector3(-0.98f, 0.082f, 0f), new Vector3(0.06f, 0.025f, 5.08f), stripeMat);
            CreateCubeChild("Stop Bar", root.transform, new Vector3(0f, 0.084f, 1.72f), new Vector3(1.7f, 0.025f, 0.08f), stripeMat);
            return stop.transform;
        }

        private static void CreateCrosswalk(string name, Vector3 center, bool acrossRoadX, Material stripeMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(crosswalksRoot, true);
            root.transform.position = center;
            for (int i = 0; i < 7; i++)
            {
                Vector3 local = acrossRoadX ? new Vector3(-2.1f + i * 0.7f, 0f, 0f) : new Vector3(0f, 0f, -2.1f + i * 0.7f);
                Vector3 scale = acrossRoadX ? new Vector3(0.38f, 0.025f, 5.6f) : new Vector3(5.6f, 0.025f, 0.38f);
                CreateCubeChild("Stripe " + (i + 1), root.transform, local, scale, stripeMat);
            }
        }

        private static void BuildEventSystem()
        {
            var eventObject = new GameObject("EventSystem");
            eventObject.transform.SetParent(runtimeRoot, false);
            eventObject.AddComponent<EventSystem>();
            var uiModule = eventObject.AddComponent<XRUIInputModule>();
            uiModule.enableXRInput = true;
            uiModule.enableMouseInput = true;
            uiModule.enableBuiltinActionsAsFallback = true;
            uiModule.activeInputMode = XRUIInputModule.ActiveInputMode.InputSystemActions;
        }

        private static CocoonSafePickupZone BuildSafePickupZone(Material zoneMat, Material arrowMat, Transform head, out GameObject guidanceRoot)
        {
            const float detectionRadius = 1.25f;
            var root = new GameObject("Safe Pickup Zone");
            root.transform.SetParent(pickupRoot, true);
            root.transform.position = new Vector3(-6.6f, 0.08f, 10f);

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = detectionRadius;

            CreatePickupVisualChild("Door Light Wash", root.transform, Vector3.zero, new Vector3(1.65f, 0.035f, 2.45f), zoneMat);
            CreatePickupVisualChild("Door Beam Core", root.transform, new Vector3(-0.9f, 0.04f, 0f), new Vector3(1.9f, 0.025f, 0.72f), arrowMat);
            CreatePickupVisualChild("Door Beam Front Edge", root.transform, new Vector3(-0.92f, 0.045f, 0.86f), new Vector3(1.8f, 0.025f, 0.07f), arrowMat);
            CreatePickupVisualChild("Door Beam Rear Edge", root.transform, new Vector3(-0.92f, 0.045f, -0.86f), new Vector3(1.8f, 0.025f, 0.07f), arrowMat);
            CreatePickupVisualChild("Pickup Boundary Left", root.transform, new Vector3(-0.84f, 0.055f, 0f), new Vector3(0.055f, 0.06f, 2.5f), arrowMat);
            CreatePickupVisualChild("Pickup Boundary Right", root.transform, new Vector3(0.84f, 0.055f, 0f), new Vector3(0.055f, 0.06f, 2.5f), arrowMat);
            CreatePickupVisualChild("Pickup Boundary Front", root.transform, new Vector3(0f, 0.055f, 1.25f), new Vector3(1.72f, 0.06f, 0.055f), arrowMat);
            CreatePickupVisualChild("Pickup Boundary Back", root.transform, new Vector3(0f, 0.055f, -1.25f), new Vector3(1.72f, 0.06f, 0.055f), arrowMat);

            var zone = root.AddComponent<CocoonSafePickupZone>();
            zone.Configure(head, root.transform, detectionRadius);
            zone.SetZoneActive(false);

            guidanceRoot = new GameObject("Guidance To Safe Pickup Zone");
            guidanceRoot.transform.SetParent(pickupRoot, true);
            guidanceRoot.transform.position = root.transform.position;
            guidanceRoot.transform.rotation = root.transform.rotation;
            CreateGuidanceArrow(guidanceRoot.transform, "Projected Pickup Arrow Back", new Vector3(0f, 0.09f, -1.75f), Vector3.forward, arrowMat);
            CreateGuidanceArrow(guidanceRoot.transform, "Projected Pickup Arrow Mid", new Vector3(0f, 0.09f, -0.62f), Vector3.forward, arrowMat);
            CreatePickupVisualChild("Safe Zone Glow Landing", guidanceRoot.transform, Vector3.zero, new Vector3(1.9f, 0.025f, 2.7f), arrowMat);
            guidanceRoot.SetActive(false);

            return zone;
        }

        private static CocoonTrafficLanePath BuildTraffic(Material trafficBlueMat, Material trafficRedMat, Material trafficYellowMat, Material glassMat, Material tireMat, Transform[] physicalPickupBays, out Transform[] pickupBays, out int[] pickupBayPathIndices)
        {
            var loop = BuildTrafficLanePath(
                "City Traffic Loop Lane",
                new[]
                {
                    new Vector3(0.9f, 0f, 38f),
                    new Vector3(0.9f, 0f, 30f),
                    new Vector3(0.9f, 0f, 20f),
                    new Vector3(0.9f, 0f, 10f),
                    new Vector3(0.9f, 0f, 0f),
                    new Vector3(0.9f, 0f, -10f),
                    new Vector3(0.9f, 0f, -20f),
                    new Vector3(0.9f, 0f, -28f),
                    new Vector3(6f, 0f, -28.9f),
                    new Vector3(14f, 0f, -28.9f),
                    new Vector3(24f, 0f, -28.9f),
                    new Vector3(34f, 0f, -28.9f),
                    new Vector3(34.9f, 0f, -22f),
                    new Vector3(34.9f, 0f, -10f),
                    new Vector3(34.9f, 0f, 4f),
                    new Vector3(34.9f, 0f, 18f),
                    new Vector3(34.9f, 0f, 28f),
                    new Vector3(28f, 0f, 28.9f),
                    new Vector3(20f, 0f, 28.9f),
                    new Vector3(10f, 0f, 28.9f),
                    new Vector3(2f, 0f, 28.9f)
                });

            pickupBays = physicalPickupBays != null && physicalPickupBays.Length > 0
                ? physicalPickupBays
                : new[]
                {
                    CreateMarker("Pickup Bay West Stop A", new Vector3(-4.05f, 0f, 10f), Quaternion.LookRotation(Vector3.back, Vector3.up), trafficRoot),
                    CreateMarker("Pickup Bay West Stop B", new Vector3(-4.05f, 0f, -10f), Quaternion.LookRotation(Vector3.back, Vector3.up), trafficRoot),
                    CreateMarker("Pickup Bay South Stop", new Vector3(14f, 0f, -32.05f), Quaternion.LookRotation(Vector3.right, Vector3.up), trafficRoot),
                    CreateMarker("Pickup Bay East Stop A", new Vector3(38.05f, 0f, -10f), Quaternion.LookRotation(Vector3.forward, Vector3.up), trafficRoot),
                    CreateMarker("Pickup Bay East Stop B", new Vector3(38.05f, 0f, 18f), Quaternion.LookRotation(Vector3.forward, Vector3.up), trafficRoot),
                    CreateMarker("Pickup Bay North Stop", new Vector3(20f, 0f, 32.05f), Quaternion.LookRotation(Vector3.left, Vector3.up), trafficRoot)
                };
            pickupBayPathIndices = new[] { 3, 5, 9, 13, 15, 18 };

            CreateTrafficVehicle("Traffic Sedan A", trafficBlueMat, glassMat, tireMat, 1.45f, 2.75f, 0.45f).AddComponent<CocoonTrafficVehicle>().Configure(loop, 3.0f, 2, 2.75f);
            CreateTrafficVehicle("Traffic Van A", trafficRedMat, glassMat, tireMat, 1.55f, 3.45f, 0.7f).AddComponent<CocoonTrafficVehicle>().Configure(loop, 2.35f, 5, 3.45f);
            CreateTrafficVehicle("Traffic Micro Bus", trafficYellowMat, glassMat, tireMat, 1.65f, 4.35f, 0.85f).AddComponent<CocoonTrafficVehicle>().Configure(loop, 1.95f, 8, 4.35f);
            CreateTrafficVehicle("Traffic Sedan B", trafficRedMat, glassMat, tireMat, 1.4f, 2.65f, 0.42f).AddComponent<CocoonTrafficVehicle>().Configure(loop, 3.2f, 11, 2.65f);
            CreateTrafficVehicle("Traffic Van B", trafficBlueMat, glassMat, tireMat, 1.55f, 3.25f, 0.68f).AddComponent<CocoonTrafficVehicle>().Configure(loop, 2.45f, 14, 3.25f);
            CreateTrafficVehicle("Traffic Sedan C", trafficYellowMat, glassMat, tireMat, 1.4f, 2.75f, 0.42f).AddComponent<CocoonTrafficVehicle>().Configure(loop, 2.8f, 17, 2.75f);
            CreateTrafficVehicle("Traffic Compact C", trafficBlueMat, glassMat, tireMat, 1.32f, 2.55f, 0.4f).AddComponent<CocoonTrafficVehicle>().Configure(loop, 3.15f, 20, 2.55f);

            return loop;
        }

        private static CocoonTrafficLanePath BuildTrafficLanePath(string name, Vector3[] points)
        {
            var laneObject = new GameObject(name);
            laneObject.transform.SetParent(trafficRoot, false);
            var waypoints = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Transform waypoint = CreateMarker(name + " WP " + i, points[i], Quaternion.identity);
                waypoint.SetParent(laneObject.transform, true);
                waypoints[i] = waypoint;
            }

            var lane = laneObject.AddComponent<CocoonTrafficLanePath>();
            lane.Configure(waypoints);
            return lane;
        }

        private static GameObject CreateTrafficVehicle(string name, Material bodyMat, Material glassMat, Material tireMat, float width, float length, float cabinHeight)
        {
            var root = new GameObject(name);
            root.transform.SetParent(trafficRoot, false);
            CreateCubeChild("Body", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(width, 0.55f, length), bodyMat);
            CreateCubeChild("Cabin", root.transform, new Vector3(0f, 0.88f, -0.08f), new Vector3(width * 0.76f, cabinHeight, length * 0.42f), glassMat);
            CreateCubeChild("Front Light Bar", root.transform, new Vector3(0f, 0.52f, length * 0.52f), new Vector3(width * 0.72f, 0.06f, 0.05f), glassMat);
            CreateCubeChild("Wheel FL", root.transform, new Vector3(-width * 0.52f, 0.18f, length * 0.31f), new Vector3(0.18f, 0.36f, 0.36f), tireMat);
            CreateCubeChild("Wheel FR", root.transform, new Vector3(width * 0.52f, 0.18f, length * 0.31f), new Vector3(0.18f, 0.36f, 0.36f), tireMat);
            CreateCubeChild("Wheel RL", root.transform, new Vector3(-width * 0.52f, 0.18f, -length * 0.31f), new Vector3(0.18f, 0.36f, 0.36f), tireMat);
            CreateCubeChild("Wheel RR", root.transform, new Vector3(width * 0.52f, 0.18f, -length * 0.31f), new Vector3(0.18f, 0.36f, 0.36f), tireMat);
            return root;
        }

        private static void CreateGuidanceArrow(Transform parent, string name, Vector3 localPosition, Vector3 direction, Material material)
        {
            var arrow = new GameObject(name);
            arrow.transform.SetParent(parent, false);
            arrow.transform.localPosition = localPosition;
            arrow.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            CreatePickupVisualChild("Stem", arrow.transform, new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.025f, 0.5f), material);
            GameObject headLeft = CreatePickupVisualChild("Head Left", arrow.transform, new Vector3(-0.1f, 0f, 0.23f), new Vector3(0.1f, 0.025f, 0.32f), material);
            headLeft.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
            GameObject headRight = CreatePickupVisualChild("Head Right", arrow.transform, new Vector3(0.1f, 0f, 0.23f), new Vector3(0.1f, 0.025f, 0.32f), material);
            headRight.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
        }

        private static GameObject CreatePickupVisualChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject visual = CreateCubeChild(name, parent, localPosition, localScale, material);
            RemovePrimitiveCollider(visual);
            return visual;
        }

        private static void CreateStreetLight(string name, Vector3 position, Material poleMat, Material lightMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(streetLightsRoot, true);
            root.transform.position = position;
            CreateCubeChild("Pole", root.transform, new Vector3(0f, -0.58f, 0f), new Vector3(0.055f, 1.7f, 0.055f), poleMat);
            CreateCubeChild("Arm", root.transform, new Vector3(0.23f * Mathf.Sign(position.x), 0.28f, 0f), new Vector3(0.48f, 0.045f, 0.045f), poleMat);
            CreateCubeChild("Lamp", root.transform, new Vector3(0.48f * Mathf.Sign(position.x), 0.2f, 0f), new Vector3(0.22f, 0.09f, 0.18f), lightMat);
        }

        private static GameObject BuildTaxi(Material bodyMat, Material glassMat, Material tireMat, Material lightMat, out Transform doorHinge, out Transform paymentReader, out Renderer paymentReaderRenderer, out Renderer bodyRenderer, out Renderer[] lights, out Text windshieldText, out Text rearWindshieldText)
        {
            var root = new GameObject("Cocoon Autonomous Taxi");
            root.transform.SetParent(taxiRoot, true);

            if (CocoonImportedTaxiModelBuilder.TryBuild(root.transform, bodyMat, glassMat, tireMat, lightMat, out doorHinge, out paymentReader, out paymentReaderRenderer, out bodyRenderer, out lights, out windshieldText, out rearWindshieldText))
            {
                return root;
            }

            var visualRoot = new GameObject("Taxi Pod Visuals");
            visualRoot.transform.SetParent(root.transform, false);

            GameObject body = CreateTaxiCubeChild("Rounded Lower Body", visualRoot.transform, new Vector3(0f, 0.52f, -0.02f), new Vector3(2.04f, 0.72f, 3.2f), Quaternion.identity, bodyMat);
            bodyRenderer = body.GetComponent<Renderer>();
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

            lights = new Renderer[0];

            var hingeObject = new GameObject("Passenger Door Hinge");
            hingeObject.transform.SetParent(visualRoot.transform, false);
            hingeObject.transform.localPosition = new Vector3(1.08f, 0.78f, -0.78f);
            doorHinge = hingeObject.transform;
            CreateTaxiCubeChild("Passenger Sliding Door", doorHinge, new Vector3(0.04f, 0.08f, 0.55f), new Vector3(0.08f, 1.12f, 1.1f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Passenger Door Glass", doorHinge, new Vector3(0.09f, 0.34f, 0.55f), new Vector3(0.04f, 0.58f, 0.78f), Quaternion.identity, glassMat);
            CreateTaxiCubeChild("Passenger Door Center Seam", doorHinge, new Vector3(0.11f, 0.18f, 0.55f), new Vector3(0.035f, 0.86f, 0.05f), Quaternion.identity, bodyMat);
            CreateTaxiCubeChild("Passenger Door Handle", doorHinge, new Vector3(0.13f, 0.16f, 0.58f), new Vector3(0.04f, 0.12f, 0.42f), Quaternion.identity, bodyMat);

            GameObject reader = CreateTaxiCubeChild("Contactless Payment Reader", visualRoot.transform, new Vector3(1.16f, 1.08f, 0.42f), new Vector3(0.08f, 0.34f, 0.32f), Quaternion.identity, lightMat);
            paymentReader = reader.transform;
            paymentReaderRenderer = reader.GetComponent<Renderer>();

            Vector2 exteriorScreenSize = GetExteriorScreenCanvasSize();
            GameObject display = CreateWorldCanvas("Taxi Front Windshield Display", Vector3.zero, Quaternion.identity, exteriorScreenSize, 0.00132f);
            display.transform.SetParent(visualRoot.transform, false);
            display.transform.localPosition = new Vector3(0f, 1.22f, 1.54f);
            display.transform.localRotation = Quaternion.Euler(-13f, 0f, 0f);
            display.transform.localScale = new Vector3(-0.00132f, 0.00132f, 0.00132f);
            CanvasScaler displayScaler = display.GetComponent<CanvasScaler>();
            if (displayScaler != null)
            {
                displayScaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            }

            AddExteriorScreenImage(display.transform);
            windshieldText = null;

            GameObject rearDisplay = CreateWorldCanvas("Taxi Rear Windshield Display", Vector3.zero, Quaternion.identity, exteriorScreenSize, 0.00108f);
            rearDisplay.transform.SetParent(visualRoot.transform, false);
            rearDisplay.transform.localPosition = new Vector3(0f, 1.12f, -1.91f);
            rearDisplay.transform.localRotation = Quaternion.identity;
            rearDisplay.transform.localScale = new Vector3(0.00108f, 0.00108f, 0.00108f);
            CanvasScaler rearDisplayScaler = rearDisplay.GetComponent<CanvasScaler>();
            if (rearDisplayScaler != null)
            {
                rearDisplayScaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            }

            AddExteriorScreenImage(rearDisplay.transform);
            rearWindshieldText = null;

            return root;
        }

        private static Vector2 GetExteriorScreenCanvasSize()
        {
            return new Vector2(ExteriorScreenReferenceWidth, ExteriorScreenReferenceWidth * ExteriorScreenSourceHeight / ExteriorScreenSourceWidth);
        }

        private static RawImage AddExteriorScreenImage(Transform parent)
        {
            var imageObject = new GameObject("Exterior Screen Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage image = imageObject.GetComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>(ExteriorScreenResourcePath);
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static GameObject BuildInstructionPanel(Transform head, out Text headline, out Text status, out Text timer, out Text instructions)
        {
            GameObject canvasObject = CreateWorldCanvas("Street Instruction Panel", new Vector3(-4.92f, 1.8f, -9.12f), Quaternion.Euler(0f, 80f, 0f), new Vector2(720f, 320f), 0.0014f, uiRoot);
            canvasObject.AddComponent<CocoonBillboard>().Configure(head);
            CocoonHeadLockedUI headLockedUi = canvasObject.AddComponent<CocoonHeadLockedUI>();
            headLockedUi.SetLocalScale(0.0014f);
            AddPanelBackground(canvasObject.transform, new Color(0.015f, 0.025f, 0.032f, 0.985f), new Vector2(720f, 320f));
            headline = CreateText("Headline", canvasObject.transform, new Vector2(0f, 106f), new Vector2(648f, 77f), 149, Color.white, TextAnchor.MiddleLeft, "");
            status = CreateText("Status", canvasObject.transform, new Vector2(0f, 38f), new Vector2(648f, 90f), 99, new Color(0.9f, 0.97f, 1f), TextAnchor.MiddleLeft, "");
            instructions = CreateText("Instructions", canvasObject.transform, new Vector2(-65f, -38f), new Vector2(518f, 90f), 87, new Color(0.82f, 0.88f, 0.9f), TextAnchor.MiddleLeft, "");
            timer = CreateText("Timer", canvasObject.transform, new Vector2(230f, -115f), new Vector2(202f, 58f), 96, new Color(0.1f, 1f, 0.55f), TextAnchor.MiddleRight, "");
            return canvasObject;
        }

        private static GameObject BuildDoorPanel(Transform doorHinge, CocoonTaxiStateMachine taxiStateMachine, out Text headline, out Text status, out Text timer, out Text destination)
        {
            GameObject canvasObject = CreateWorldCanvas("Door-side Onboarding UI", Vector3.zero, Quaternion.identity, new Vector2(760f, 560f), 0.00095f);
            canvasObject.transform.SetParent(doorHinge, false);
            canvasObject.transform.localPosition = new Vector3(0.24f, 0.34f, 0.55f);
            canvasObject.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.00095f;
            AddPanelBackground(canvasObject.transform, new Color(0.015f, 0.025f, 0.032f, 0.985f), new Vector2(760f, 560f));

            headline = CreateText("Door Panel Title", canvasObject.transform, new Vector2(0f, 194f), new Vector2(650f, 72f), 78, Color.white, TextAnchor.MiddleCenter, "BOARD COCOON?");
            status = CreateText("Door Panel Status", canvasObject.transform, new Vector2(0f, 104f), new Vector2(650f, 64f), 40, new Color(0.88f, 0.96f, 1f), TextAnchor.MiddleCenter, "Right controller: A confirms, B leaves.");
            destination = CreateText("Door Panel Destination", canvasObject.transform, new Vector2(0f, -4f), new Vector2(650f, 152f), 38, new Color(0.1f, 1f, 0.55f), TextAnchor.MiddleLeft, "Destination: Duomo\nMode: Solo\nPickup: Door-side safe zone\nPayment: Contactless ready");
            timer = CreateText("Door Panel Timer", canvasObject.transform, new Vector2(0f, -226f), new Vector2(540f, 46f), 40, new Color(0.95f, 0.86f, 0.38f), TextAnchor.MiddleCenter, "Decision");

            CreatePanelButton("Confirm Ride Button", canvasObject.transform, new Vector2(-142f, -142f), new Vector2(248f, 78f), "A\nCONFIRM", taxiStateMachine, CocoonButtonAction.ConfirmRide, "");
            CreatePanelButton("Leave Button", canvasObject.transform, new Vector2(142f, -142f), new Vector2(248f, 78f), "B\nLEAVE", taxiStateMachine, CocoonButtonAction.DeclineRide, "");

            return canvasObject;
        }

        private static GameObject CreatePanelButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, string label, CocoonTaxiStateMachine stateMachine, CocoonButtonAction action, string payload)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BoxCollider), typeof(CocoonWorldButton));
            var rect = button.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            var image = button.GetComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.13f, 0.95f);
            BoxCollider collider = button.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(size.x, size.y, 36f);
            button.GetComponent<CocoonWorldButton>().Configure(stateMachine, action, payload, image);
            Text labelText = CreateText(name + " Text", button.transform, Vector2.zero, size, 36, Color.white, TextAnchor.MiddleCenter, label);
            labelText.fontStyle = FontStyle.Bold;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 18;
            labelText.resizeTextMaxSize = 36;
            labelText.lineSpacing = 0.82f;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            return button;
        }

        private static GameObject CreateWorldCanvas(string name, Vector3 position, Quaternion rotation, Vector2 size, float scale, Transform parent = null)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            canvasObject.transform.position = position;
            canvasObject.transform.rotation = rotation;
            canvasObject.transform.localScale = Vector3.one * scale;
            if (parent != null)
            {
                canvasObject.transform.SetParent(parent, true);
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.worldCamera = Camera.main;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = WorldCanvasPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;
            return canvasObject;
        }

        private static void AddPanelBackground(Transform parent, Color color, Vector2 size)
        {
            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = background.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            background.GetComponent<Image>().color = color;
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, Color color, TextAnchor alignment, string value)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = defaultFont;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = fontSize;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2.3f, -2.3f);
            return text;
        }

        private static Transform CreateMarker(string name, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var marker = new GameObject(name);
            marker.transform.position = position;
            marker.transform.rotation = rotation;
            Transform resolvedParent = parent != null ? parent : GetMarkerParent(name);
            if (resolvedParent != null)
            {
                marker.transform.SetParent(resolvedParent, true);
            }

            return marker.transform;
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            Transform resolvedParent = parent != null ? parent : GetCubeParent(name);
            if (resolvedParent != null)
            {
                cube.transform.SetParent(resolvedParent, true);
            }

            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Transform GetMarkerParent(string name)
        {
            if (name.StartsWith("Cruise", StringComparison.Ordinal) || name.StartsWith("Safe Pull-over", StringComparison.Ordinal))
            {
                return taxiRoot;
            }

            return null;
        }

        private static Transform GetCubeParent(string name)
        {
            if (name.StartsWith("Pickup", StringComparison.Ordinal))
            {
                return pickupRoot;
            }

            if (name.Contains("Lane Dash"))
            {
                return markingsRoot;
            }

            if (name.Contains("Road"))
            {
                return roadsRoot;
            }

            if (name.StartsWith("Sidewalk", StringComparison.Ordinal) || name.Contains("Walk"))
            {
                return sidewalksRoot;
            }

            if (name.Contains("Curb"))
            {
                return curbsRoot;
            }

            if (name.Contains("Building"))
            {
                return buildingsRoot;
            }

            return null;
        }

        private static GameObject CreateCubeChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static GameObject CreateDecorCubeChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cube = CreateCubeChild(name, parent, localPosition, localScale, material);
            RemovePrimitiveCollider(cube);
            return cube;
        }

        private static GameObject CreateTaxiCubeChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject cube = CreateCubeChild(name, parent, localPosition, localScale, material);
            cube.transform.localRotation = localRotation;
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

        private static Material CreateMaterial(string name, Color color, bool emissive = false)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = GetPrototypeShader();
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (name.Equals("TaxiGlass", StringComparison.OrdinalIgnoreCase))
            {
                return material;
            }
            else if (NeedsShaderReplacement(material.shader) && shader != null)
            {
                material.shader = shader;
            }

            SetMaterialColor(material, color);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                SetMaterialEmission(material, color * 1.6f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                SetMaterialEmission(material, Color.black);
            }

            if (name.Equals("HandProxy", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureTransparentHandMaterial(material);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTransparentHandMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

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
        }

        private static Shader GetPrototypeShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
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

        private static void SetMaterialEmission(Material material, Color color)
        {
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
            string child = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
