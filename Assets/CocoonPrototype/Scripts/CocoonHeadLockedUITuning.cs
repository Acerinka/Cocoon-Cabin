using UnityEngine;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonHeadLockedUITuning : MonoBehaviour
    {
        [Header("Head-Locked UI Tuning")]
        [Tooltip("When enabled, runtime applies one shared readable layout to every face-locked prompt panel. Disable this only if you want to hand-edit each child Text/RectTransform in 06_UI.")]
        [SerializeField] private bool applyRuntimeLayout = true;

        [Tooltip("Canvas layout size in UI pixels. Smaller values simplify the panel; physical size is controlled mostly by Panel Local Scale.")]
        [SerializeField] private Vector2 panelSize = new Vector2(720f, 320f);

        [Tooltip("Physical scale of the face-locked panel. Lower this when the panel feels too large in the headset.")]
        [SerializeField] private float panelLocalScale = 0.0014f;

        [Tooltip("World-space canvas density. Increase this if text edges look soft; 128 is a good VR starting point.")]
        [SerializeField] private float canvasPixelsPerUnit = 128f;

        [Tooltip("Shared multiplier for all face-locked UI text. Increase this when the panel size is good but text is too small.")]
        [SerializeField] private float textScale = 1.55f;

        public bool ApplyRuntimeLayout => applyRuntimeLayout;

        public Vector2 PanelSize => new Vector2(
            Mathf.Max(240f, panelSize.x),
            Mathf.Max(160f, panelSize.y));

        public float PanelLocalScale => Mathf.Clamp(panelLocalScale, 0.0004f, 0.004f);

        public float CanvasPixelsPerUnit => Mathf.Clamp(canvasPixelsPerUnit, 24f, 256f);

        public float TextScale => Mathf.Max(0.2f, textScale);
    }
}
