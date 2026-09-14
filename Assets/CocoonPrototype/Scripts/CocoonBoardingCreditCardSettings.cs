using UnityEngine;

namespace CocoonPrototype
{
    [DisallowMultipleComponent]
    public sealed class CocoonBoardingCreditCardSettings : MonoBehaviour
    {
        [Header("Boarding Credit Card")]
        [Tooltip("Position of the card anchor relative to the right hand/controller.")]
        [SerializeField] private Vector3 localPosition = new Vector3(0.025f, -0.015f, 0.055f);

        [Tooltip("Euler rotation of the card anchor relative to the right hand/controller.")]
        [SerializeField] private Vector3 localRotation = new Vector3(70f, 0f, 8f);

        [Tooltip("Target visible world size of the credit card. This is compensated against XR rig/hand parent scale.")]
        [SerializeField] private Vector3 worldSize = new Vector3(0.085f, 0.052f, 0.004f);

        [Tooltip("Extra bounds padding used when detecting card overlap with DOORUI.")]
        [SerializeField] private float boundsPadding = 0.012f;

        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalRotation => localRotation;
        public Vector3 WorldSize => new Vector3(
            Mathf.Max(0.0005f, Mathf.Abs(worldSize.x)),
            Mathf.Max(0.0005f, Mathf.Abs(worldSize.y)),
            Mathf.Max(0.0002f, Mathf.Abs(worldSize.z)));

        public float BoundsPadding => Mathf.Clamp(boundsPadding, 0.001f, 0.08f);
    }
}
