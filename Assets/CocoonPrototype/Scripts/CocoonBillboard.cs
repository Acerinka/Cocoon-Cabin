using UnityEngine;

namespace CocoonPrototype
{
    public sealed class CocoonBillboard : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private bool keepUpright = true;

        public void Configure(Transform lookTarget)
        {
            target = lookTarget;
        }

        private void LateUpdate()
        {
            Transform lookTarget = target != null ? target : Camera.main != null ? Camera.main.transform : null;
            if (lookTarget == null)
            {
                return;
            }

            Vector3 direction = transform.position - lookTarget.position;
            if (keepUpright)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }
}
