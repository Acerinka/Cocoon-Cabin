using UnityEngine;

namespace CocoonPrototype
{
    public sealed class CocoonTrafficLanePath : MonoBehaviour
    {
        private const float SegmentOutlierMultiplier = 2.35f;
        private const float SegmentOutlierExtraMeters = 0.3f;

        [SerializeField] private Transform[] waypoints;
        [SerializeField] private bool closedLoop = true;
        [SerializeField] private bool logRouteDiagnostics = true;

        private bool hasLoggedRouteDiagnostics;

        public int Count => waypoints == null ? 0 : waypoints.Length;
        public float TotalLength => CalculateTotalLength();
        public bool IsClosedLoop => closedLoop && Count > 2 && IsSegmentReasonableInternal(0, true);
        public int BlockedSegmentCount => CountUnreasonableSegments();

        public void Configure(Transform[] pathWaypoints)
        {
            Configure(pathWaypoints, true);
        }

        public void Configure(Transform[] pathWaypoints, bool isClosedLoop)
        {
            waypoints = pathWaypoints;
            closedLoop = isClosedLoop;
            hasLoggedRouteDiagnostics = false;
        }

        private void Awake()
        {
            LogRouteDiagnosticsIfNeeded();
        }

        private void OnEnable()
        {
            LogRouteDiagnosticsIfNeeded();
        }

        private void LogRouteDiagnosticsIfNeeded()
        {
            if (!logRouteDiagnostics || hasLoggedRouteDiagnostics || waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            hasLoggedRouteDiagnostics = true;
            int blockedSegments = CountUnreasonableSegments();
            CocoonDebugLog.Info(
                "TrafficGraph",
                name + " route loaded. waypoints=" + Count +
                ", closedLoop=" + IsClosedLoop +
                ", length=" + TotalLength.ToString("0.00") + "m" +
                ", blockedSegments=" + blockedSegments +
                (blockedSegments > 0 ? " (long/END-gap segments will stop vehicles instead of wrapping)." : "."),
                this);
        }

        public Transform GetWaypoint(int index)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return null;
            }

            if (IsClosedLoop)
            {
                int wrapped = ((index % waypoints.Length) + waypoints.Length) % waypoints.Length;
                return waypoints[wrapped];
            }

            return index >= 0 && index < waypoints.Length ? waypoints[index] : null;
        }

        public int NormalizeIndex(int index)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return 0;
            }

            if (IsClosedLoop)
            {
                return ((index % waypoints.Length) + waypoints.Length) % waypoints.Length;
            }

            return Mathf.Clamp(index, 0, waypoints.Length - 1);
        }

        public bool TryGetNextIndex(int currentTargetIndex, out int nextIndex)
        {
            nextIndex = -1;
            if (waypoints == null || waypoints.Length < 2)
            {
                return false;
            }

            int current = NormalizeIndex(currentTargetIndex);
            if (IsClosedLoop)
            {
                nextIndex = NormalizeIndex(current + 1);
                return IsSegmentReasonable(nextIndex);
            }

            if (current >= waypoints.Length - 1)
            {
                return false;
            }

            nextIndex = current + 1;
            return IsSegmentReasonable(nextIndex);
        }

        public bool HasSegmentTarget(int targetIndex)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return false;
            }

            if (IsClosedLoop)
            {
                return true;
            }

            return targetIndex > 0 && targetIndex < waypoints.Length;
        }

        public bool IsSegmentReasonable(int targetIndex)
        {
            return IsSegmentReasonableInternal(NormalizeIndex(targetIndex), IsClosedLoop);
        }

        public float GetProgressAtSegmentTarget(int targetIndex, Vector3 position)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return 0f;
            }

            int normalizedTarget = NormalizeIndex(targetIndex);
            if (!IsClosedLoop && normalizedTarget <= 0)
            {
                return 0f;
            }

            if (!IsSegmentReasonable(normalizedTarget))
            {
                return GetDistanceToWaypoint(Mathf.Max(0, normalizedTarget - 1));
            }

            int previousIndex = IsClosedLoop ? NormalizeIndex(normalizedTarget - 1) : normalizedTarget - 1;
            Vector3 start = Flatten(waypoints[previousIndex].position);
            Vector3 end = Flatten(waypoints[normalizedTarget].position);
            Vector3 segment = end - start;
            float segmentLength = segment.magnitude;
            if (segmentLength < 0.001f)
            {
                return GetDistanceToWaypoint(previousIndex);
            }

            float t = Mathf.Clamp01(Vector3.Dot(Flatten(position) - start, segment) / (segmentLength * segmentLength));
            return GetDistanceToWaypoint(previousIndex) + segmentLength * t;
        }

        public bool TryGetRouteProgress(int targetIndex, Vector3 position, out CocoonTrafficRouteProgress routeProgress)
        {
            routeProgress = default;
            if (waypoints == null || waypoints.Length < 2)
            {
                return false;
            }

            float totalLength = TotalLength;
            if (totalLength <= 0.001f)
            {
                return false;
            }

            int normalizedTarget = NormalizeIndex(targetIndex);
            if (!HasSegmentTarget(normalizedTarget))
            {
                return false;
            }

            if (!IsSegmentReasonable(normalizedTarget))
            {
                return false;
            }

            float distance = GetProgressAtSegmentTarget(normalizedTarget, position);
            if (IsClosedLoop)
            {
                distance = Mathf.Repeat(distance, totalLength);
            }
            else
            {
                distance = Mathf.Clamp(distance, 0f, totalLength);
            }

            routeProgress = new CocoonTrafficRouteProgress
            {
                LanePath = this,
                TargetIndex = normalizedTarget,
                Distance = distance,
                TotalLength = totalLength,
                Position = position
            };
            return true;
        }

        public float GetForwardDistance(float fromProgress, float toProgress)
        {
            float totalLength = TotalLength;
            if (totalLength <= 0.001f)
            {
                return float.MaxValue;
            }

            if (IsClosedLoop)
            {
                float distance = Mathf.Repeat(toProgress, totalLength) - Mathf.Repeat(fromProgress, totalLength);
                if (distance < 0f)
                {
                    distance += totalLength;
                }

                return distance;
            }

            float forward = Mathf.Clamp(toProgress, 0f, totalLength) - Mathf.Clamp(fromProgress, 0f, totalLength);
            return forward >= 0f ? forward : float.MaxValue;
        }

        public Vector3 GetSegmentForward(int targetIndex)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return Vector3.forward;
            }

            int normalizedTarget = NormalizeIndex(targetIndex);
            if (!IsClosedLoop && normalizedTarget <= 0)
            {
                normalizedTarget = 1;
            }

            if (!IsSegmentReasonable(normalizedTarget))
            {
                return Vector3.forward;
            }

            int previousIndex = IsClosedLoop ? NormalizeIndex(normalizedTarget - 1) : normalizedTarget - 1;
            Transform previous = waypoints[previousIndex];
            Transform current = waypoints[normalizedTarget];
            Vector3 forward = current != null && previous != null ? current.position - previous.position : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        public int FindNearestSegmentTargetIndex(Vector3 position)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return waypoints == null || waypoints.Length == 0 ? -1 : 0;
            }

            Vector3 flatPosition = Flatten(position);
            int bestTarget = -1;
            float bestDistance = float.MaxValue;
            int firstTarget = IsClosedLoop ? 0 : 1;
            for (int targetIndex = firstTarget; targetIndex < waypoints.Length; targetIndex++)
            {
                if (!IsSegmentReasonable(targetIndex))
                {
                    continue;
                }

                int previousIndex = IsClosedLoop ? NormalizeIndex(targetIndex - 1) : targetIndex - 1;
                Transform previous = waypoints[previousIndex];
                Transform target = waypoints[targetIndex];
                if (previous == null || target == null)
                {
                    continue;
                }

                Vector3 start = Flatten(previous.position);
                Vector3 end = Flatten(target.position);
                Vector3 segment = end - start;
                float segmentLengthSqr = segment.sqrMagnitude;
                Vector3 closest = segmentLengthSqr > 0.000001f
                    ? start + segment * Mathf.Clamp01(Vector3.Dot(flatPosition - start, segment) / segmentLengthSqr)
                    : start;
                float distance = Vector3.SqrMagnitude(closest - flatPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = targetIndex;
                }
            }

            return bestTarget >= 0 ? NormalizeIndex(bestTarget) : 1;
        }

        public float GetDistanceToWaypoint(int index)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return 0f;
            }

            int normalizedIndex = NormalizeIndex(index);
            float distance = 0f;
            for (int i = 1; i <= normalizedIndex; i++)
            {
                if (!IsSegmentReasonable(i))
                {
                    continue;
                }

                Transform previous = waypoints[i - 1];
                Transform current = waypoints[i];
                if (previous != null && current != null)
                {
                    distance += Vector3.Distance(Flatten(previous.position), Flatten(current.position));
                }
            }

            return distance;
        }

        private float CalculateTotalLength()
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return 0f;
            }

            float length = 0f;
            for (int i = 1; i < waypoints.Length; i++)
            {
                if (!IsSegmentReasonable(i))
                {
                    continue;
                }

                Transform previous = waypoints[i - 1];
                Transform current = waypoints[i];
                if (previous != null && current != null)
                {
                    length += Vector3.Distance(Flatten(previous.position), Flatten(current.position));
                }
            }

            if (IsClosedLoop)
            {
                Transform last = waypoints[waypoints.Length - 1];
                Transform first = waypoints[0];
                if (last != null && first != null)
                {
                    length += Vector3.Distance(Flatten(last.position), Flatten(first.position));
                }
            }

            return length;
        }

        private bool IsSegmentReasonableInternal(int targetIndex, bool allowClosingSegment)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return false;
            }

            int normalizedTarget = targetIndex;
            if (allowClosingSegment)
            {
                normalizedTarget = ((targetIndex % waypoints.Length) + waypoints.Length) % waypoints.Length;
            }
            else if (targetIndex < 0 || targetIndex >= waypoints.Length)
            {
                return false;
            }

            int previousIndex;
            if (normalizedTarget == 0)
            {
                if (!allowClosingSegment)
                {
                    return false;
                }

                previousIndex = waypoints.Length - 1;
            }
            else
            {
                previousIndex = normalizedTarget - 1;
            }

            Transform previous = waypoints[previousIndex];
            Transform current = waypoints[normalizedTarget];
            if (previous == null || current == null)
            {
                return false;
            }

            float segmentLength = Vector3.Distance(Flatten(previous.position), Flatten(current.position));
            float typicalLength = EstimateTypicalConsecutiveSegmentLength();
            if (typicalLength <= 0.001f)
            {
                return segmentLength > 0.001f;
            }

            float maxReasonableLength = Mathf.Max(typicalLength * SegmentOutlierMultiplier, typicalLength + SegmentOutlierExtraMeters);
            return segmentLength <= maxReasonableLength;
        }

        private float EstimateTypicalConsecutiveSegmentLength()
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return 0f;
            }

            float total = 0f;
            int count = 0;
            for (int i = 1; i < waypoints.Length; i++)
            {
                Transform previous = waypoints[i - 1];
                Transform current = waypoints[i];
                if (previous == null || current == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(Flatten(previous.position), Flatten(current.position));
                if (distance > 0.001f)
                {
                    total += distance;
                    count++;
                }
            }

            return count > 0 ? total / count : 0f;
        }

        private int CountUnreasonableSegments()
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                return 0;
            }

            int count = 0;
            for (int i = 1; i < waypoints.Length; i++)
            {
                if (!IsSegmentReasonableInternal(i, false))
                {
                    count++;
                }
            }

            if (closedLoop && !IsSegmentReasonableInternal(0, true))
            {
                count++;
            }

            return count;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
