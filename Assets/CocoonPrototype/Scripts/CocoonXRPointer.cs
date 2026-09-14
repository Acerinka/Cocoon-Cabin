using UnityEngine;

namespace CocoonPrototype
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CocoonXRPointer : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform rightHand;
        [SerializeField] private CocoonXRNodePose rightHandPose;
        [SerializeField] private CocoonConfirmGestureDetector confirmGestureDetector;
        [SerializeField] private CocoonTaxiStateMachine taxiStateMachine;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private LayerMask interactableLayers = ~0;

        private LineRenderer lineRenderer;
        private CocoonWorldButton hoveredButton;
        private bool lastPanelActive;

        public void Configure(
            Transform headTransform,
            Transform rightHandTransform,
            CocoonXRNodePose handPose,
            CocoonConfirmGestureDetector confirmDetector,
            CocoonTaxiStateMachine stateMachine)
        {
            head = headTransform;
            rightHand = rightHandTransform;
            rightHandPose = handPose;
            confirmGestureDetector = confirmDetector;
            taxiStateMachine = stateMachine;
        }

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.012f;
            lineRenderer.endWidth = 0.004f;
        }

        private void Update()
        {
            bool panelActive = taxiStateMachine != null && taxiStateMachine.IsDoorPanelInteractive;
            if (panelActive != lastPanelActive)
            {
                CocoonDebugLog.Verbose("Pointer", "Right ray " + (panelActive ? "enabled" : "disabled") + ".", this);
                lastPanelActive = panelActive;
            }

            if (!panelActive)
            {
                ClearHover();
                lineRenderer.enabled = false;
                return;
            }

            lineRenderer.enabled = true;
            Ray ray = BuildRay();
            Vector3 endPoint = ray.origin + ray.direction * maxDistance;

            if (TryRaycastInteractiveButton(ray, out RaycastHit hit, out CocoonWorldButton button))
            {
                endPoint = hit.point;
                SetHover(button);

                if (button != null && confirmGestureDetector != null && confirmGestureDetector.ConsumeConfirmDown())
                {
                    CocoonDebugLog.Verbose("Pointer", "Trigger pressed while pointing at " + button.name + ".", this);
                    button.Press();
                }
            }
            else
            {
                ClearHover();
                if (confirmGestureDetector != null)
                {
                    confirmGestureDetector.ConsumeConfirmDown();
                }
            }

            lineRenderer.SetPosition(0, ray.origin);
            lineRenderer.SetPosition(1, endPoint);
        }

        private bool TryRaycastInteractiveButton(Ray ray, out RaycastHit chosenHit, out CocoonWorldButton button)
        {
            chosenHit = default;
            button = null;

            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, interactableLayers, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            float nearestDistance = float.MaxValue;
            float nearestButtonDistance = float.MaxValue;
            bool hasNearest = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    chosenHit = hit;
                    hasNearest = true;
                }

                CocoonWorldButton candidate = hit.collider.GetComponentInParent<CocoonWorldButton>();
                if (candidate != null && hit.distance < nearestButtonDistance)
                {
                    nearestButtonDistance = hit.distance;
                    chosenHit = hit;
                    button = candidate;
                }
            }

            return hasNearest;
        }

        private Ray BuildRay()
        {
            bool useHand = rightHand != null && (rightHandPose == null || rightHandPose.IsTracked || Application.isEditor);
            Transform source = useHand ? rightHand : head;
            if (source == null)
            {
                return new Ray(transform.position, transform.forward);
            }

            return new Ray(source.position, source.forward);
        }

        private void SetHover(CocoonWorldButton button)
        {
            if (hoveredButton == button)
            {
                return;
            }

            ClearHover();
            hoveredButton = button;
            if (hoveredButton != null)
            {
                hoveredButton.SetHighlighted(true);
                CocoonDebugLog.Verbose("Pointer", "Hover " + hoveredButton.name + ".", this);
            }
        }

        private void ClearHover()
        {
            if (hoveredButton != null)
            {
                CocoonDebugLog.Verbose("Pointer", "Hover cleared from " + hoveredButton.name + ".", this);
                hoveredButton.SetHighlighted(false);
                hoveredButton = null;
            }
        }
    }
}
