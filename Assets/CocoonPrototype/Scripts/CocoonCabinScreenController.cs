using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonCabinScreenController : MonoBehaviour
    {
        [Serializable]
        private sealed class ScreenPresentationSettings
        {
            public bool flipX;
            public bool flipY;
            public bool rotate180;
            [Range(0f, 0.45f)] public float fitPadding = 0.03f;
        }

        [Header("Screen References")]
        [SerializeField] private Transform interiorRoot;
        [SerializeField] private Transform led1;
        [SerializeField] private Transform led2;
        [SerializeField] private Transform led3;
        [SerializeField] private Transform oled1;
        [SerializeField] private Transform oled2;
        [SerializeField] private Transform oled3;
        [SerializeField] private Transform miniScreen;
        [SerializeField] private Transform miniScreenAnchorOverride;

        [Header("Display Panels")]
        [SerializeField] private RectTransform cabinLedEnvPanel;
        [SerializeField] private RectTransform cabinLedTripPanel;
        [SerializeField] private RectTransform cabinLedMainPanel;
        [SerializeField] private RectTransform cabinOledEnvPanel;
        [SerializeField] private RectTransform cabinOledTripPanel;
        [SerializeField] private RectTransform cabinOledMainPanel;
        [SerializeField] private RectTransform cabinMiniPanel;

        [Header("Cabin Textures")]
        [SerializeField] private Texture2D envScreen;
        [SerializeField] private Texture2D tripScreen;
        [SerializeField] private Texture2D sideEnvScreen;
        [SerializeField] private Texture2D sideTripScreen;
        [SerializeField] private Texture2D[] mainUiScreens = new Texture2D[11];
        [SerializeField] private Texture2D[] miniUiScreens = new Texture2D[14];
        [SerializeField] private Texture2D mainDefaultScreen;
        [SerializeField] private Texture2D mainOnboardScreen;
        [SerializeField] private Texture2D mainDestinationScreen;
        [SerializeField] private Texture2D[] seatedMainUiScreens = new Texture2D[4];
        [SerializeField] private Texture2D[] seekScreens = new Texture2D[3];
        [SerializeField] private Texture2D[] roScreens = new Texture2D[7];

        [Header("Behavior")]
        [SerializeField] private bool applyDefaultsOnStart = true;
        [SerializeField] private bool createMissingPanelsAtRuntime = true;
        [SerializeField] private bool enableCabinUiCycling = false;
        [SerializeField] private bool logScreenChanges = false;
        [SerializeField] private bool autoCycleMainUi = true;
        [SerializeField] private bool autoCycleMiniUi = true;
        [SerializeField] private float mainUiHomeSeconds = 15f;
        [SerializeField] private float mainUiPageSeconds = 10f;
        [SerializeField] private float miniUiHomeSeconds = 10f;
        [SerializeField] private float miniUiPageSeconds = 3f;
        [SerializeField] private float defaultLargePanelWorldWidth = 0.08f;
        [SerializeField] private float defaultMiniPanelWorldWidth = 0.04f;

        [Header("Panel Fit")]
        [SerializeField] private ScreenPresentationSettings led1Fit = new ScreenPresentationSettings();
        [SerializeField] private ScreenPresentationSettings led2Fit = new ScreenPresentationSettings();
        [SerializeField] private ScreenPresentationSettings led3Fit = new ScreenPresentationSettings();
        [SerializeField] private ScreenPresentationSettings oled1Fit = new ScreenPresentationSettings();
        [SerializeField] private ScreenPresentationSettings oled2Fit = new ScreenPresentationSettings();
        [SerializeField] private ScreenPresentationSettings oled3Fit = new ScreenPresentationSettings();
        [SerializeField] private ScreenPresentationSettings miniFit = new ScreenPresentationSettings();

        private bool hasLoggedBinding;
        private bool hasLoggedMissing;
        private bool hasLoggedMiniSeatFollowBinding;
        private bool hasLoggedMiniSeatAlreadyChild;
        private Transform miniSeatFollowSeat;
        private bool hasMiniSeatFollowOffset;
        private bool miniScreenIsSeatChild;
        private Vector3 miniSeatLocalPosition;
        private Quaternion miniSeatLocalRotation = Quaternion.identity;
        private Vector3 miniSeatRelativeScale = Vector3.one;

        private float mainUiCycleTimer;
        private int mainUiCycleSlot;
        private float miniUiCycleTimer;
        private int miniUiCycleSlot;
        private bool seatedMainSequenceActive;
        private float seatedMainSequenceTimer;
        private int seatedMainSequenceIndex;
        private bool seatedMiniSequenceActive;
        private float seatedMiniSequenceTimer;
        private int seatedMiniSequenceIndex;

        private const string InteriorRootName = "Final Taxi Interior Structure";
        private const string SeatAnchorName = "seat1 Static Anchor";
        private const string SeatV2AssemblyName = "Seat V2 Assembly";
        private const string SeatV2BodyName = "Seat V2 Body";
        private const string MiniScreenName = "MIINIscreen1";
        private const string CabinResourceRoot = "CabinScreens/";
        private const string CabinMainResourceRoot = "CabinScreens/mainUI/";
        private const string MiniResourceRoot = "CabinScreens/miniUI/";
        private const string MiniSeekResourceRoot = "CabinScreens/miniUI/seek/";
        private const string MiniRoResourceRoot = "CabinScreens/miniUI/ro/";
        private const string CabinSideResourceRoot = "CabinScreens/sideUI/";

        private const string LedEnvPanelName = "Cabin LED Env Display Panel";
        private const string LedTripPanelName = "Cabin LED Trip Display Panel";
        private const string LedMainPanelName = "Cabin LED Main Display Panel";
        private const string OledEnvPanelName = "Cabin OLED Env Display Panel";
        private const string OledTripPanelName = "Cabin OLED Trip Display Panel";
        private const string OledMainPanelName = "Cabin OLED Main Display Panel";
        private const string MiniPanelName = "Cabin Mini UI Display Panel";
        private const string DefaultScreenImageName = "Screen Image";
        private const string MiniScreenImageName = "Screen Image_final";

        private const float EnvAspect = 4f;
        private const float TripAspect = 4f;
        private const float MainAspect = 2f;
        private const float MiniAspect = 1f;
        private const float PanelReferenceWidth = 1024f;
        private const float PanelDynamicPixelsPerUnit = 768f;

        private void Start()
        {
            ResolveReferences();
            if (applyDefaultsOnStart)
            {
                ApplyDefaultScreens();
            }
            ResetAutoCycleTimers();
        }

        private void Update()
        {
            UpdateSeatedMainSequence();
            UpdateSeatedMiniSequence();
            UpdateAutoCycle();
        }

        [ContextMenu("Apply Default Cabin Screens")]
        public void ApplyDefaultScreens()
        {
            ResolveReferences();
            ShowEnvScreen();
            ShowTripScreen();
            ShowMainDefault();
            ShowMiniUi(0);
            StopSeatedMainSequence();
            StopSeatedMiniSequence();
            ResetAutoCycleTimers();
            LogBindingSummary();
        }

        public void ResetAutoCycleTimers()
        {
            mainUiCycleSlot = 0;
            miniUiCycleSlot = 0;
            mainUiCycleTimer = Mathf.Max(0.1f, mainUiHomeSeconds);
            miniUiCycleTimer = Mathf.Max(0.1f, miniUiHomeSeconds);
        }

        private void UpdateAutoCycle()
        {
            if (!enableCabinUiCycling)
            {
                return;
            }

            if (autoCycleMainUi)
            {
                mainUiCycleTimer -= Time.deltaTime;
                if (mainUiCycleTimer <= 0f)
                {
                    mainUiCycleSlot = (mainUiCycleSlot + 1) % 11;
                    int index = mainUiCycleSlot == 0 ? 0 : mainUiCycleSlot;
                    ShowMainUi(index);
                    mainUiCycleTimer = Mathf.Max(0.1f, index == 0 ? mainUiHomeSeconds : mainUiPageSeconds);
                }
            }

            if (autoCycleMiniUi)
            {
                miniUiCycleTimer -= Time.deltaTime;
                if (miniUiCycleTimer <= 0f)
                {
                    miniUiCycleSlot = (miniUiCycleSlot + 1) % 8;
                    int index = miniUiCycleSlot == 0 ? 0 : 4 + miniUiCycleSlot;
                    ShowMiniUi(index);
                    miniUiCycleTimer = Mathf.Max(0.1f, index == 0 ? miniUiHomeSeconds : miniUiPageSeconds);
                }
            }
        }

        public void ShowEnvScreen()
        {
            Texture2D sideEnv = GetSideTexture(ref sideEnvScreen, "env-screen", ref envScreen);
            Texture2D sideTrip = GetSideTexture(ref sideTripScreen, "trip-screen", ref tripScreen);
            ApplyTextureToPanel(ref cabinLedEnvPanel, led1, LedEnvPanelName, sideEnv, "led env-screen", true, led1Fit, EnvAspect, defaultLargePanelWorldWidth, false);
            ApplyTextureToPanel(ref cabinOledEnvPanel, oled1, OledEnvPanelName, sideTrip, "oled trip-screen", true, oled1Fit, TripAspect, defaultLargePanelWorldWidth, false);
        }

        public void ShowTripScreen()
        {
            Texture2D sideEnv = GetSideTexture(ref sideEnvScreen, "env-screen", ref envScreen);
            Texture2D sideTrip = GetSideTexture(ref sideTripScreen, "trip-screen", ref tripScreen);
            ApplyTextureToPanel(ref cabinLedTripPanel, led2, LedTripPanelName, sideTrip, "led trip-screen", true, led2Fit, TripAspect, defaultLargePanelWorldWidth, false);
            ApplyTextureToPanel(ref cabinOledTripPanel, oled2, OledTripPanelName, sideEnv, "oled env-screen", true, oled2Fit, EnvAspect, defaultLargePanelWorldWidth, false);
        }

        public bool ShowMainUi(int index)
        {
            index = Mathf.Clamp(index, 0, 10);
            Texture2D texture = GetTexture(mainUiScreens, index, GetMainUiResourcePath(index));
            bool applied = false;
            applied |= ApplyTextureToPanel(ref cabinLedMainPanel, led3, LedMainPanelName, texture, "led main-UI-" + index, true, led3Fit, MainAspect, defaultLargePanelWorldWidth);
            applied |= ApplyTextureToPanel(ref cabinOledMainPanel, oled3, OledMainPanelName, texture, "oled main-UI-" + index, true, oled3Fit, MainAspect, defaultLargePanelWorldWidth);
            return applied;
        }

        public bool ShowMainDefault()
        {
            StopSeatedMainSequence();
            Texture2D texture = GetTexture(ref mainDefaultScreen, CabinMainResourceRoot + "default");
            return ApplyMainLifecycleTexture(texture, "main default");
        }

        public bool ShowMainOnboard()
        {
            StopSeatedMainSequence();
            Texture2D texture = GetTexture(ref mainOnboardScreen, CabinMainResourceRoot + "onboard");
            return ApplyMainLifecycleTexture(texture, "main onboard");
        }

        public bool ShowMainDestination()
        {
            StopSeatedMainSequence();
            Texture2D texture = GetTexture(ref mainDestinationScreen, CabinMainResourceRoot + "destination");
            return ApplyMainLifecycleTexture(texture, "main destination");
        }

        public void StartSeatedMainSequence()
        {
            ResolveReferences();
            seatedMainSequenceActive = true;
            seatedMainSequenceIndex = 0;
            seatedMainSequenceTimer = Mathf.Max(0.1f, mainUiPageSeconds);
            ShowSeatedMainSequenceFrame(0);
        }

        public void StopSeatedMainSequence()
        {
            seatedMainSequenceActive = false;
            seatedMainSequenceTimer = 0f;
            seatedMainSequenceIndex = 0;
        }

        public void StartSeatedMiniSequence()
        {
            ResolveReferences();
            seatedMiniSequenceActive = true;
            seatedMiniSequenceIndex = 0;
            seatedMiniSequenceTimer = 5f;
            ShowSeatedMiniSequenceFrame(0);
        }

        public void StopSeatedMiniSequence()
        {
            seatedMiniSequenceActive = false;
            seatedMiniSequenceTimer = 0f;
            seatedMiniSequenceIndex = 0;
        }

        public bool ShowMiniUi(int index)
        {
            ResolveReferences();
            index = Mathf.Clamp(index, 0, 13);
            Texture2D texture = GetTexture(miniUiScreens, index, GetMiniUiResourcePath(index));
            return ApplyTextureToPanel(ref cabinMiniPanel, miniScreen, MiniPanelName, texture, "mini main-UI-" + index, false, miniFit, MiniAspect, defaultMiniPanelWorldWidth, false);
        }

        public bool ShowMiniSeek1()
        {
            StopSeatedMiniSequence();
            ResolveReferences();
            Texture2D texture = GetTexture(seekScreens, 0, MiniSeekResourceRoot + "SEEK1");
            return ApplyTextureToPanel(ref cabinMiniPanel, miniScreen, MiniPanelName, texture, "mini SEEK1", false, miniFit, MiniAspect, defaultMiniPanelWorldWidth, false);
        }

        private bool ApplyMainLifecycleTexture(Texture2D texture, string label)
        {
            ResolveReferences();
            bool applied = false;
            applied |= ApplyTextureToPanel(ref cabinLedMainPanel, led3, LedMainPanelName, texture, "led " + label, true, led3Fit, MainAspect, defaultLargePanelWorldWidth, false);
            applied |= ApplyTextureToPanel(ref cabinOledMainPanel, oled3, OledMainPanelName, texture, "oled " + label, true, oled3Fit, MainAspect, defaultLargePanelWorldWidth, false);
            return applied;
        }

        private void UpdateSeatedMainSequence()
        {
            if (!seatedMainSequenceActive)
            {
                return;
            }

            seatedMainSequenceTimer -= Time.deltaTime;
            if (seatedMainSequenceTimer > 0f)
            {
                return;
            }

            int mainSequenceCount = seatedMainUiScreens != null && seatedMainUiScreens.Length > 0 ? seatedMainUiScreens.Length : 4;
            seatedMainSequenceIndex = (seatedMainSequenceIndex + 1) % mainSequenceCount;
            ShowSeatedMainSequenceFrame(seatedMainSequenceIndex);
            seatedMainSequenceTimer = Mathf.Max(0.1f, mainUiPageSeconds);
        }

        private void UpdateSeatedMiniSequence()
        {
            if (!seatedMiniSequenceActive)
            {
                return;
            }

            seatedMiniSequenceTimer -= Time.deltaTime;
            if (seatedMiniSequenceTimer > 0f)
            {
                return;
            }

            seatedMiniSequenceIndex++;
            ShowSeatedMiniSequenceFrame(seatedMiniSequenceIndex);
            seatedMiniSequenceTimer = seatedMiniSequenceIndex == 1 ? 3.5f : 10f;
        }

        private void ShowSeatedMainSequenceFrame(int index)
        {
            int mainSequenceCount = seatedMainUiScreens != null && seatedMainUiScreens.Length > 0 ? seatedMainUiScreens.Length : 4;
            index = Mathf.Clamp(index, 0, Mathf.Max(0, mainSequenceCount - 1));
            Texture2D texture = GetTexture(seatedMainUiScreens, index, GetSeatedMainResourcePath(index));
            ApplyMainLifecycleTexture(texture, "seated main " + index);
        }

        private void ShowSeatedMiniSequenceFrame(int index)
        {
            Texture2D texture;
            int seekCount = seekScreens != null && seekScreens.Length > 0 ? seekScreens.Length : 3;
            int roCount = roScreens != null && roScreens.Length > 0 ? roScreens.Length : 7;
            if (index < seekCount)
            {
                texture = GetTexture(seekScreens, index, MiniSeekResourceRoot + "SEEK" + (index + 1));
            }
            else
            {
                int roIndex = (index - seekCount) % roCount;
                texture = GetTexture(roScreens, roIndex, GetRoResourcePath(roIndex));
            }

            ApplyTextureToPanel(ref cabinMiniPanel, miniScreen, MiniPanelName, texture, "mini seated sequence " + index, false, miniFit, MiniAspect, defaultMiniPanelWorldWidth, false);
        }

        public void ResolveReferences()
        {
            if (interiorRoot == null || !IsDescendantOrSelf(interiorRoot, transform))
            {
                interiorRoot = FindExactNamedDescendant(transform, InteriorRootName);
            }

            if (interiorRoot == null)
            {
                interiorRoot = FindSceneTransform(InteriorRootName);
            }

            led1 = ResolveExactNamedReference(interiorRoot, led1, "led1");
            led2 = ResolveExactNamedReference(interiorRoot, led2, "led2");
            led3 = ResolveExactNamedReference(interiorRoot, led3, "led3");
            oled1 = ResolveExactNamedReference(interiorRoot, oled1, "oled1");
            oled2 = ResolveExactNamedReference(interiorRoot, oled2, "oled2");
            oled3 = ResolveExactNamedReference(interiorRoot, oled3, "oled3");

            Transform preferredSeatV2MiniScreen = ResolvePreferredSeatV2MiniScreen();
            if (preferredSeatV2MiniScreen != null)
            {
                miniScreen = preferredSeatV2MiniScreen;
            }
            else if (miniScreenAnchorOverride != null)
            {
                miniScreen = miniScreenAnchorOverride;
            }

            if (miniScreen == null || (!IsDescendantOrSelf(miniScreen, transform) && miniScreenAnchorOverride == null))
            {
                Transform seatAnchor = FindExactNamedDescendant(transform, SeatAnchorName);
                miniScreen = FindExactNamedDescendant(seatAnchor, MiniScreenName);
                if (miniScreen == null)
                {
                    miniScreen = FindExactNamedDescendant(transform, MiniScreenName);
                }
            }

            cabinLedEnvPanel = ResolvePanelReference(cabinLedEnvPanel, LedEnvPanelName);
            cabinLedTripPanel = ResolvePanelReference(cabinLedTripPanel, LedTripPanelName);
            cabinLedMainPanel = ResolvePanelReference(cabinLedMainPanel, LedMainPanelName);
            cabinOledEnvPanel = ResolvePanelReference(cabinOledEnvPanel, OledEnvPanelName);
            cabinOledTripPanel = ResolvePanelReference(cabinOledTripPanel, OledTripPanelName);
            cabinOledMainPanel = ResolvePanelReference(cabinOledMainPanel, OledMainPanelName);
            cabinMiniPanel = ResolveMiniPanelReference(cabinMiniPanel);
            EnsureMiniPanelUsesCurrentMiniScreen();
        }

        public void SetMiniScreenAnchorOverride(Transform anchor)
        {
            if (miniScreenAnchorOverride == anchor)
            {
                return;
            }

            miniScreenAnchorOverride = anchor;
            if (anchor != null)
            {
                miniScreen = anchor;
            }

            hasMiniSeatFollowOffset = false;
        }

        private Transform ResolvePreferredSeatV2MiniScreen()
        {
            Transform seatV2Assembly = FindExactNamedDescendant(transform, SeatV2AssemblyName);
            Transform seatV2Body = FindExactNamedDescendant(seatV2Assembly, SeatV2BodyName);
            Transform mini = FindExactNamedDescendant(seatV2Body, MiniScreenName);
            if (mini != null)
            {
                return mini;
            }

            return null;
        }

        public void BindMiniScreenToSeatMotion(Transform seat)
        {
            ResolveReferences();
            if (seat == null || miniScreen == null)
            {
                return;
            }

            if (hasMiniSeatFollowOffset && miniSeatFollowSeat == seat)
            {
                return;
            }

            miniSeatFollowSeat = seat;
            miniScreenIsSeatChild = IsDescendantOrSelf(miniScreen, seat);
            if (miniScreenIsSeatChild)
            {
                hasMiniSeatFollowOffset = true;
                if (!hasLoggedMiniSeatAlreadyChild)
                {
                    hasLoggedMiniSeatAlreadyChild = true;
                    CocoonDebugLog.Info("CabinUI", "MIINIscreen1 is already parented under seat1 and will follow seat motion naturally.", this);
                }

                return;
            }

            miniSeatLocalPosition = seat.InverseTransformPoint(miniScreen.position);
            miniSeatLocalRotation = Quaternion.Inverse(seat.rotation) * miniScreen.rotation;
            miniSeatRelativeScale = SafeDivide(miniScreen.lossyScale, seat.lossyScale);
            hasMiniSeatFollowOffset = true;

            if (!hasLoggedMiniSeatFollowBinding)
            {
                hasLoggedMiniSeatFollowBinding = true;
                CocoonDebugLog.Info(
                    "CabinUI",
                    "MIINIscreen1 seat-follow offset captured relative to " + GetTransformPath(seat) + ".",
                    this);
            }
        }

        public void SyncMiniScreenToSeatMotion(Transform seat)
        {
            if (seat == null)
            {
                return;
            }

            if (miniSeatFollowSeat != seat || miniScreen == null)
            {
                BindMiniScreenToSeatMotion(seat);
            }

            if (miniScreen == null || miniScreenIsSeatChild)
            {
                SyncMiniPanelToMiniScreenMotion();
                return;
            }

            miniScreen.position = seat.TransformPoint(miniSeatLocalPosition);
            miniScreen.rotation = seat.rotation * miniSeatLocalRotation;
            SetWorldScale(miniScreen, Vector3.Scale(seat.lossyScale, miniSeatRelativeScale));
            SyncMiniPanelToMiniScreenMotion();
        }

        private bool ApplyTextureToPanel(
            ref RectTransform panel,
            Transform reference,
            string panelName,
            Texture2D texture,
            string label,
            bool defaultFlipX,
            ScreenPresentationSettings settings,
            float fallbackAspect,
            float fallbackWorldWidth,
            bool preservePanelAspect = true)
        {
            if (texture == null)
            {
                LogMissingTargetOrTexture(panel, texture, label);
                return false;
            }

            if (settings == null)
            {
                settings = new ScreenPresentationSettings();
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.wrapModeU = TextureWrapMode.Clamp;
            texture.wrapModeV = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = Mathf.Max(texture.anisoLevel, 16);
            if (texture.mipmapCount > 1)
            {
                texture.mipMapBias = Mathf.Min(texture.mipMapBias, -1f);
            }

            panel = EnsureDisplayPanel(panel, reference, panelName, fallbackAspect, fallbackWorldWidth);
            if (panel == null)
            {
                LogMissingTargetOrTexture(reference, texture, label);
                return false;
            }

            ConfigurePanelCanvasQuality(panel);
            bool preserveImageRect = string.Equals(panelName, MiniPanelName, StringComparison.Ordinal);
            RawImage image = EnsureRawImage(
                panel,
                !preserveImageRect,
                preserveImageRect ? MiniScreenImageName : DefaultScreenImageName,
                fallbackAspect);
            image.texture = texture;
            image.color = Color.white;
            image.material = null;
            image.uvRect = BuildUvRect(settings, defaultFlipX);
            image.enabled = true;
            image.gameObject.SetActive(true);

            float aspect = texture.height > 0 ? (float)texture.width / texture.height : fallbackAspect;
            if (preservePanelAspect)
            {
                PreservePanelWidthAndApplyAspect(panel, aspect);
            }

            panel.gameObject.SetActive(true);

            if (logScreenChanges)
            {
                CocoonDebugLog.Info(
                    "CabinUI",
                    label + " displayed on " + GetTransformPath(panel) +
                    ". texture=" + texture.name + " " + texture.width + "x" + texture.height +
                    ", aspect=" + aspect.ToString("0.###") +
                    ", uv=" + image.uvRect + ".",
                    this);
            }

            return true;
        }

        private RectTransform EnsureDisplayPanel(
            RectTransform current,
            Transform reference,
            string panelName,
            float aspect,
            float fallbackWorldWidth)
        {
            bool miniPanel = string.Equals(panelName, MiniPanelName, StringComparison.Ordinal);
            if (current != null &&
                string.Equals(current.name, panelName, StringComparison.Ordinal) &&
                (!miniPanel || miniScreen == null || IsDescendantOrSelf(current, miniScreen)))
            {
                return current;
            }

            RectTransform existing = miniPanel ? ResolveMiniPanelReference(current) : ResolvePanelReference(current, panelName);
            if (existing != null)
            {
                return existing;
            }

            if (!createMissingPanelsAtRuntime)
            {
                return null;
            }

            Transform parent = string.Equals(panelName, MiniPanelName, StringComparison.Ordinal) && miniScreen != null
                ? miniScreen
                : transform;
            RectTransform created = CreateWorldRawImagePanel(panelName, parent, reference, aspect, fallbackWorldWidth);
            CocoonDebugLog.Warn(
                "CabinUI",
                panelName + " was missing and was created at runtime. Adjust this panel in the scene and let repair bind it for persistent placement.",
                this);
            return created;
        }

        private RectTransform ResolvePanelReference(RectTransform current, string panelName)
        {
            if (current != null && string.Equals(current.name, panelName, StringComparison.Ordinal) && IsDescendantOrSelf(current, transform))
            {
                return current;
            }

            Transform found = FindExactNamedDescendant(transform, panelName);
            return found as RectTransform;
        }

        private RectTransform ResolveMiniPanelReference(RectTransform current)
        {
            if (miniScreen != null)
            {
                Transform preferred = FindExactNamedDescendant(miniScreen, MiniPanelName);
                RectTransform preferredRect = preferred as RectTransform;
                if (preferredRect != null)
                {
                    return preferredRect;
                }

                if (current != null &&
                    string.Equals(current.name, MiniPanelName, StringComparison.Ordinal) &&
                    IsDescendantOrSelf(current, miniScreen))
                {
                    return current;
                }

                return null;
            }

            if (current != null &&
                string.Equals(current.name, MiniPanelName, StringComparison.Ordinal) &&
                IsDescendantOrSelf(current, transform))
            {
                return current;
            }

            return ResolvePanelReference(current, MiniPanelName);
        }

        private RectTransform CreateWorldRawImagePanel(
            string panelName,
            Transform parent,
            Transform reference,
            float aspect,
            float fallbackWorldWidth)
        {
            var panelObject = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.SetParent(parent != null ? parent : transform, false);
            rect.sizeDelta = new Vector2(PanelReferenceWidth, PanelReferenceWidth / Mathf.Max(0.001f, aspect));

            Canvas canvas = panelObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                canvas.worldCamera = mainCamera;
            }

            CanvasScaler scaler = panelObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = PanelDynamicPixelsPerUnit;
            scaler.referencePixelsPerUnit = 100f;

            GraphicRaycaster raycaster = panelObject.GetComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = true;

            bool miniPanel = string.Equals(panelName, MiniPanelName, StringComparison.Ordinal);
            RawImage image = EnsureRawImage(
                rect,
                !miniPanel,
                miniPanel ? MiniScreenImageName : DefaultScreenImageName,
                aspect);
            image.raycastTarget = false;

            Vector3 worldPosition = reference != null ? GetReferenceWorldCenter(reference) : rect.position;
            Quaternion worldRotation = GetInitialPanelRotation(reference, panelName);
            rect.position = worldPosition;
            rect.rotation = worldRotation;

            float widthWorld = reference != null ? EstimateReferenceWorldWidth(reference) : 0f;
            if (widthWorld <= 0.001f)
            {
                widthWorld = Mathf.Max(0.001f, fallbackWorldWidth);
            }

            SetWorldScale(rect, Vector3.one * (widthWorld / PanelReferenceWidth));
            return rect;
        }

        private static void ConfigurePanelCanvasQuality(RectTransform panel)
        {
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
                scaler.dynamicPixelsPerUnit = PanelDynamicPixelsPerUnit;
                scaler.referencePixelsPerUnit = 100f;
            }
        }

        private static RawImage EnsureRawImage(RectTransform panel, bool stretchToPanel, string imageName, float aspect = 1f)
        {
            Transform imageTransform = panel.Find(imageName);
            RawImage image = imageTransform != null ? imageTransform.GetComponent<RawImage>() : null;
            bool created = false;
            if (image == null)
            {
                var imageObject = new GameObject(imageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                imageObject.transform.SetParent(panel, false);
                RectTransform imageRect = imageObject.GetComponent<RectTransform>();
                if (stretchToPanel)
                {
                    imageRect.anchorMin = Vector2.zero;
                    imageRect.anchorMax = Vector2.one;
                    imageRect.offsetMin = Vector2.zero;
                    imageRect.offsetMax = Vector2.zero;
                }
                else
                {
                    imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                    imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                    imageRect.pivot = new Vector2(0.5f, 0.5f);
                    imageRect.sizeDelta = new Vector2(PanelReferenceWidth, PanelReferenceWidth / Mathf.Max(0.001f, aspect));
                    imageRect.localPosition = Vector3.zero;
                    imageRect.localRotation = Quaternion.identity;
                    imageRect.localScale = Vector3.one;
                }

                image = imageObject.GetComponent<RawImage>();
                created = true;
            }

            RectTransform rect = image.GetComponent<RectTransform>();
            if (rect != null && (stretchToPanel || created))
            {
                if (stretchToPanel)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.localPosition = Vector3.zero;
                    rect.localRotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                }
            }

            image.color = Color.white;
            image.material = null;
            image.raycastTarget = false;
            return image;
        }

        private void EnsureMiniPanelUsesCurrentMiniScreen()
        {
            if (miniScreen == null || cabinMiniPanel == null)
            {
                return;
            }

            if (!IsDescendantOrSelf(cabinMiniPanel, miniScreen))
            {
                cabinMiniPanel = null;
            }
        }

        private void EnsureMiniPanelFacesPositiveZ()
        {
            if (miniScreen == null || cabinMiniPanel == null)
            {
                return;
            }

            Vector3 desiredForward = miniScreen.forward;
            Vector3 desiredUp = miniScreen.up;
            if (desiredForward.sqrMagnitude < 0.0001f || desiredUp.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (Vector3.Dot(cabinMiniPanel.forward, desiredForward.normalized) >= 0f)
            {
                return;
            }

            Vector3 up = Vector3.ProjectOnPlane(desiredUp.normalized, desiredForward.normalized);
            if (up.sqrMagnitude < 0.0001f)
            {
                up = cabinMiniPanel.up;
            }

            cabinMiniPanel.rotation = Quaternion.LookRotation(desiredForward.normalized, up.normalized);
        }

        private static void PreservePanelWidthAndApplyAspect(RectTransform panel, float aspect)
        {
            if (panel == null)
            {
                return;
            }

            Vector2 size = panel.sizeDelta;
            float width = size.x > 1f ? size.x : PanelReferenceWidth;
            panel.sizeDelta = new Vector2(width, width / Mathf.Max(0.001f, aspect));
        }

        private static Rect BuildUvRect(ScreenPresentationSettings settings, bool defaultFlipX)
        {
            bool flipX = defaultFlipX ^ (settings != null && settings.flipX) ^ (settings != null && settings.rotate180);
            bool flipY = (settings != null && settings.flipY) ^ (settings != null && settings.rotate180);
            return new Rect(flipX ? 1f : 0f, flipY ? 1f : 0f, flipX ? -1f : 1f, flipY ? -1f : 1f);
        }

        private Quaternion GetInitialPanelRotation(Transform reference, string panelName)
        {
            if (reference == null)
            {
                return transform.rotation;
            }

            if (interiorRoot != null && !string.Equals(panelName, MiniPanelName, StringComparison.Ordinal))
            {
                bool oled = panelName.IndexOf("OLED", StringComparison.OrdinalIgnoreCase) >= 0;
                Vector3 normal = oled ? -interiorRoot.up : interiorRoot.up;
                Vector3 up = Vector3.ProjectOnPlane(interiorRoot.forward, normal);
                if (normal.sqrMagnitude > 0.0001f && up.sqrMagnitude > 0.0001f)
                {
                    return Quaternion.LookRotation(normal.normalized, up.normalized);
                }
            }

            return reference.rotation;
        }

        private static Vector3 GetReferenceWorldCenter(Transform reference)
        {
            Renderer[] renderers = reference.GetComponentsInChildren<Renderer>(true);
            if (TryGetCombinedBounds(renderers, out Bounds bounds))
            {
                return bounds.center;
            }

            return reference.position;
        }

        private float EstimateReferenceWorldWidth(Transform reference)
        {
            Renderer[] renderers = reference.GetComponentsInChildren<Renderer>(true);
            if (!TryGetCombinedBounds(renderers, out Bounds bounds))
            {
                return 0f;
            }

            Vector3 axis = interiorRoot != null ? interiorRoot.right : reference.right;
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

        private void BindMiniPanelToMiniScreenMotion()
        {
            if (miniScreen == null || cabinMiniPanel == null)
            {
                return;
            }

        }

        private void SyncMiniPanelToMiniScreenMotion()
        {
            // The mini display panel is now an authored canvas. Runtime only changes its texture.
        }

        private Texture2D GetTexture(ref Texture2D texture, string resourcePath)
        {
            if (texture == null)
            {
                texture = Resources.Load<Texture2D>(resourcePath);
            }

            return texture;
        }

        private Texture2D GetSideTexture(ref Texture2D sideTexture, string textureName, ref Texture2D legacyTexture)
        {
            Texture2D texture = GetTexture(ref sideTexture, CabinSideResourceRoot + textureName);
            return texture != null ? texture : GetTexture(ref legacyTexture, CabinResourceRoot + textureName);
        }

        private Texture2D GetTexture(Texture2D[] textures, int index, string resourcePath)
        {
            if (textures != null && index >= 0 && index < textures.Length && textures[index] != null)
            {
                return textures[index];
            }

            Texture2D loaded = Resources.Load<Texture2D>(resourcePath);
            if (textures != null && index >= 0 && index < textures.Length)
            {
                textures[index] = loaded;
            }

            return loaded;
        }

        private static string GetMainUiResourcePath(int index)
        {
            return index <= 0 ? CabinResourceRoot + "main-UI" : CabinResourceRoot + "main-UI-" + index;
        }

        private static string GetSeatedMainResourcePath(int index)
        {
            return index <= 0 ? CabinMainResourceRoot + "main-UI" : CabinMainResourceRoot + "main-UI-" + index;
        }

        private static string GetMiniUiResourcePath(int index)
        {
            return index <= 0 ? MiniResourceRoot + "main-UI" : MiniResourceRoot + "main-UI-" + index;
        }

        private static string GetRoResourcePath(int index)
        {
            return index <= 0 ? MiniRoResourceRoot + "RO" : MiniRoResourceRoot + "RO-" + index;
        }

        private void LogBindingSummary()
        {
            if (hasLoggedBinding)
            {
                return;
            }

            hasLoggedBinding = true;
            CocoonDebugLog.Info(
                "CabinUI",
                "Cabin display panels bound: ledEnv=" + GetTransformPath(cabinLedEnvPanel) +
                ", ledTrip=" + GetTransformPath(cabinLedTripPanel) +
                ", ledMain=" + GetTransformPath(cabinLedMainPanel) +
                ", oledEnv=" + GetTransformPath(cabinOledEnvPanel) +
                ", oledTrip=" + GetTransformPath(cabinOledTripPanel) +
                ", oledMain=" + GetTransformPath(cabinOledMainPanel) +
                ", miniPanel=" + GetTransformPath(cabinMiniPanel) +
                ", miniImage=" + GetTransformPath(GetMiniRawImageTransform()) +
                ", miniReference=" + GetTransformPath(miniScreen) + ".",
                this);
        }

        private Transform GetMiniRawImageTransform()
        {
            if (cabinMiniPanel == null)
            {
                return null;
            }

            Transform image = cabinMiniPanel.Find(MiniScreenImageName);
            return image;
        }

        private void LogMissingTargetOrTexture(UnityEngine.Object target, Texture2D texture, string label)
        {
            if (hasLoggedMissing)
            {
                return;
            }

            hasLoggedMissing = true;
            CocoonDebugLog.Warn(
                "CabinUI",
                label + " could not be applied. target=" + (target != null ? target.name : "<missing>") +
                ", texture=" + (texture != null ? texture.name : "<missing>") + ".",
                this);
        }

        private static bool TryGetCombinedBounds(Renderer[] renderers, out Bounds bounds)
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

        private static Transform ResolveExactNamedReference(Transform root, Transform current, string name)
        {
            if (current != null && string.Equals(current.name, name, StringComparison.Ordinal) && IsDescendantOrSelf(current, root))
            {
                return current;
            }

            return FindExactNamedDescendant(root, name);
        }

        private static Transform FindExactNamedDescendant(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && string.Equals(transforms[i].name, name, StringComparison.Ordinal))
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static Transform FindSceneTransform(string name)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null ||
                    candidate.hideFlags != HideFlags.None ||
                    !candidate.gameObject.scene.IsValid() ||
                    !string.Equals(candidate.name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static bool IsDescendantOrSelf(Transform candidate, Transform root)
        {
            if (candidate == null || root == null)
            {
                return false;
            }

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

        private static Vector3 SafeDivide(Vector3 numerator, Vector3 denominator)
        {
            return new Vector3(
                Mathf.Abs(denominator.x) > 0.0001f ? numerator.x / denominator.x : 1f,
                Mathf.Abs(denominator.y) > 0.0001f ? numerator.y / denominator.y : 1f,
                Mathf.Abs(denominator.z) > 0.0001f ? numerator.z / denominator.z : 1f);
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

            target.localScale = SafeDivide(worldScale, parent.lossyScale);
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<missing>";
            }

            var parts = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts.ToArray());
        }
    }
}
