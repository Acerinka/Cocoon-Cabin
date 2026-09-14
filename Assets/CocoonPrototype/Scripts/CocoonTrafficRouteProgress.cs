using UnityEngine;

namespace CocoonPrototype
{
    public struct CocoonTrafficRouteProgress
    {
        public CocoonTrafficLanePath LanePath;
        public int TargetIndex;
        public float Distance;
        public float TotalLength;
        public Vector3 Position;

        public bool IsValid => LanePath != null && TotalLength > 0.001f;

        public float ForwardDistanceTo(CocoonTrafficRouteProgress target)
        {
            if (!IsValid || !target.IsValid || target.LanePath != LanePath)
            {
                return float.MaxValue;
            }

            return LanePath.GetForwardDistance(Distance, target.Distance);
        }
    }
}
