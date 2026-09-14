using UnityEngine;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonHeadLockedUI : MonoBehaviour
    {
        [Header("Head-Locked Placement")]
        [SerializeField] private bool useHeadLockedUi = true;
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 localPosition = new Vector3(-0.42f, 0.22f, 1.35f);
        [SerializeField] private Vector3 localEuler = Vector3.zero;
        [SerializeField] private float localScale = 0.0014f;
        [SerializeField] private bool updateWhilePlaying = true;
        [SerializeField] private bool disableBillboardWhileLocked = true;

        public void SetLocalScale(float scale)
        {
            localScale = Mathf.Max(0.0001f, scale);
        }

        public bool TryApply(Transform fallbackTarget = null)
        {
            if (!useHeadLockedUi)
            {
                return false;
            }

            Transform resolvedTarget = ResolveTarget(fallbackTarget);
            if (resolvedTarget == null)
            {
                return false;
            }

            Transform panelTransform = transform;
            if (panelTransform.parent != resolvedTarget)
            {
                panelTransform.SetParent(resolvedTarget, false);
            }

            panelTransform.localPosition = localPosition;
            panelTransform.localRotation = Quaternion.Euler(localEuler);
            panelTransform.localScale = Vector3.one * Mathf.Max(0.0001f, localScale);
            ConfigureCanvas();
            ConfigureBillboard();
            return true;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                TryApply();
            }
        }

        private void LateUpdate()
        {
            if (updateWhilePlaying)
            {
                TryApply();
            }
        }

        private Transform ResolveTarget(Transform fallbackTarget)
        {
            if (target != null)
            {
                return target;
            }

            if (fallbackTarget != null)
            {
                target = fallbackTarget;
                return target;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                target = mainCamera.transform;
                return target;
            }

            return null;
        }

        private void ConfigureCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = false;
            canvas.worldCamera = Camera.main;
        }

        private void ConfigureBillboard()
        {
            if (!disableBillboardWhileLocked)
            {
                return;
            }

            CocoonBillboard billboard = GetComponent<CocoonBillboard>();
            if (billboard != null)
            {
                billboard.enabled = false;
            }
        }
    }
}
