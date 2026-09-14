using UnityEngine;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonLuggageRamp : MonoBehaviour
    {
        [Header("Ramp Shape")]
        [SerializeField] private bool useTransformScaleAsSize = true;
        [SerializeField] private Vector3 size = new Vector3(0.85f, 0.035f, 0.48f);

        [Header("Hinge Motion")]
        [SerializeField] private Vector3 hingeWorldOffset = Vector3.zero;
        [SerializeField] private float hingeToCenterDistance = 1.1f;
        [SerializeField] private float outerEdgeDrop = 0.1f;

        public void ApplyPose(Vector3 doorCenter, Vector3 outward, float open01, float experienceScale)
        {
            _ = experienceScale;
            open01 = Mathf.Clamp01(open01);
            if (open01 <= 0.001f)
            {
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
            Vector3 extensionDirection = outward;
            Vector3 widthDirection = Vector3.Cross(extensionDirection, Vector3.up).normalized;
            Quaternion baseRotation = Quaternion.LookRotation(widthDirection, Vector3.up);

            Vector3 appliedSize = useTransformScaleAsSize
                ? Vector3.Max(Vector3.one * 0.001f, transform.localScale)
                : Vector3.Max(Vector3.one * 0.001f, size);
            float centerDistance = Mathf.Max(0.001f, hingeToCenterDistance);
            float outerDistance = Mathf.Max(0.001f, centerDistance + appliedSize.x * 0.5f);
            float targetAngle = Mathf.Asin(Mathf.Clamp(-outerEdgeDrop / outerDistance, -0.95f, 0.95f)) * Mathf.Rad2Deg;
            float angle = Mathf.Lerp(0f, targetAngle, open01);
            Quaternion rampRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);

            Vector3 hinge = doorCenter + hingeWorldOffset;
            transform.position = hinge + rampRotation * (Vector3.right * (centerDistance * open01));
            transform.rotation = rampRotation;
            transform.localScale = appliedSize;
        }
    }
}
