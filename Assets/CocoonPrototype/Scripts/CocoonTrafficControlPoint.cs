using UnityEngine;

namespace CocoonPrototype
{
    public enum CocoonTrafficControlType
    {
        Cruise,
        IntersectionYield,
        PickupMerge
    }

    public sealed class CocoonTrafficControlPoint : MonoBehaviour
    {
        private struct Reservation
        {
            public CocoonTrafficParticipant Participant;
            public float ReservedUntil;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Reservation> Reservations =
            new System.Collections.Generic.Dictionary<string, Reservation>();

        [SerializeField] private CocoonTrafficControlType controlType = CocoonTrafficControlType.Cruise;
        [SerializeField] private string controlGroup;
        [SerializeField] private float approachDistance = 3.6f;
        [SerializeField] private float stopSeconds = 0.45f;
        [SerializeField] private float crossingHoldSeconds = 1.15f;
        [SerializeField] private float slowSpeedMultiplier = 0.55f;
        [SerializeField] private float releaseSpeedMultiplier = 0.85f;

        public CocoonTrafficControlType ControlType => controlType;
        public float ApproachDistance => Mathf.Max(0.03f, approachDistance);
        public float StopSeconds => Mathf.Max(0f, stopSeconds);
        public float SlowSpeedMultiplier => Mathf.Clamp01(slowSpeedMultiplier);
        public float ReleaseSpeedMultiplier => Mathf.Clamp(releaseSpeedMultiplier, 0.1f, 1f);

        public void Configure(
            CocoonTrafficControlType type,
            float approach,
            float stopDuration,
            float holdDuration,
            float slowMultiplier,
            float releaseMultiplier)
        {
            Configure(type, null, approach, stopDuration, holdDuration, slowMultiplier, releaseMultiplier);
        }

        public void Configure(
            CocoonTrafficControlType type,
            string group,
            float approach,
            float stopDuration,
            float holdDuration,
            float slowMultiplier,
            float releaseMultiplier)
        {
            controlType = type;
            controlGroup = group;
            approachDistance = Mathf.Max(0.03f, approach);
            stopSeconds = Mathf.Max(0f, stopDuration);
            crossingHoldSeconds = Mathf.Max(0.1f, holdDuration);
            slowSpeedMultiplier = Mathf.Clamp01(slowMultiplier);
            releaseSpeedMultiplier = Mathf.Clamp(releaseMultiplier, 0.1f, 1f);
        }

        public bool TryReserve(CocoonTrafficParticipant participant)
        {
            if (controlType == CocoonTrafficControlType.Cruise || participant == null)
            {
                return true;
            }

            string key = string.IsNullOrEmpty(controlGroup) ? GetInstanceID().ToString() : controlGroup;
            if (!Reservations.TryGetValue(key, out Reservation reservation) ||
                reservation.Participant == null ||
                reservation.Participant == participant ||
                Time.time >= reservation.ReservedUntil)
            {
                Reservations[key] = new Reservation
                {
                    Participant = participant,
                    ReservedUntil = Time.time + crossingHoldSeconds
                };
                return true;
            }

            return false;
        }
    }
}
