using UnityEngine;

namespace CocoonPrototype
{
    public sealed class CocoonBoardingDoorUISettings : MonoBehaviour
    {
        [SerializeField] private CocoonBoardingDoorSideMode doorSideMode = CocoonBoardingDoorSideMode.AutoCurbSide;
        [SerializeField] private bool useManualPanelTransform = true;
        [SerializeField] private Vector2 panelSize = new Vector2(760f, 560f);
        [SerializeField] private Vector3 panelLocalOffset = Vector3.zero;
        [SerializeField] private float panelScale = 0.00042f;
        [SerializeField] private float panelOutwardOffset = 0.055f;
        [SerializeField] private float doorPushDistance = 0.08f;
        [SerializeField] private float doorSlideDistance = 0.22f;
        [SerializeField] private float doorPushFraction = 0.35f;
        [SerializeField] private float doorOpenSpeed = 0.2f;
        [SerializeField] private float doorCloseSpeed = 0.25f;
        [SerializeField] private float riderInsideRequiredSeconds = 5f;

        public CocoonBoardingDoorSideMode DoorSideMode => doorSideMode;
        public bool UseManualPanelTransform => useManualPanelTransform;
        public Vector2 PanelSize => panelSize;
        public Vector3 PanelLocalOffset => panelLocalOffset;
        public float PanelScale => panelScale;
        public float PanelOutwardOffset => panelOutwardOffset;
        public float DoorPushDistance => doorPushDistance;
        public float DoorSlideDistance => doorSlideDistance;
        public float DoorPushFraction => doorPushFraction;
        public float DoorOpenSpeed => doorOpenSpeed;
        public float DoorCloseSpeed => doorCloseSpeed;
        public float RiderInsideRequiredSeconds => riderInsideRequiredSeconds;
    }
}
