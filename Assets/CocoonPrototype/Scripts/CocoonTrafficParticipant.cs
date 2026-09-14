using System.Collections.Generic;
using UnityEngine;

namespace CocoonPrototype
{
    public sealed class CocoonTrafficParticipant : MonoBehaviour
    {
        private static readonly List<CocoonTrafficParticipant> Participants = new List<CocoonTrafficParticipant>();
        private static readonly List<CocoonTrafficParticipant> ParticipantSnapshot = new List<CocoonTrafficParticipant>();
        private static int participantSnapshotFrame = -1;
        private const float SameLaneProgressEpsilon = 0.001f;
        private const float TrafficTimeHeadwaySeconds = 1.05f;
        private const float TrafficComfortBrakingMetersPerSecond = 3.8f;

        [SerializeField] private CocoonTrafficLanePath lanePath;
        [SerializeField] private CocoonRoadGraph roadGraph;
        [SerializeField] private CocoonRouteCursor graphCursor = CocoonRouteCursor.Invalid();
        [SerializeField] private Transform trackedRoot;
        [SerializeField] private float vehicleLength = 3.2f;
        [SerializeField] private float vehicleWidth = 1.4f;
        [SerializeField] private float minimumGap = 1.2f;
        [SerializeField] private float slowDistance = 6.5f;
        [SerializeField] private bool blocksTraffic = true;
        [SerializeField] private int targetIndex = 1;

        private float progress;
        private float currentSpeed;
        private bool manualProgress;
        private CocoonTrafficControlPoint activeControlPoint;
        private CocoonTrafficControlPoint servedControlPoint;
        private float controlStopTimer;
        private string lastStopReason = "";

        public float Progress => progress;
        public float VehicleLength => Mathf.Max(0.04f, vehicleLength);
        public float VehicleWidth => Mathf.Max(0.04f, vehicleWidth);
        public bool BlocksTraffic => blocksTraffic;
        public string LaneName => lanePath != null ? lanePath.name : "No Lane";
        public int TargetIndex => targetIndex;
        public Vector3 TrackedPosition => trackedRoot != null ? trackedRoot.position : transform.position;
        public string LastStopReason => string.IsNullOrEmpty(lastStopReason) ? "none" : lastStopReason;
        public CocoonRoadGraph RoadGraph => roadGraph;
        public bool HasGraphCursor => roadGraph != null && graphCursor.IsValid;

        public bool TryGetRouteProgress(out CocoonTrafficRouteProgress routeProgress)
        {
            routeProgress = default;
            if (lanePath == null || trackedRoot == null)
            {
                return false;
            }

            return lanePath.TryGetRouteProgress(targetIndex, trackedRoot.position, out routeProgress);
        }

        private struct SpeedLimitResult
        {
            public float Speed;
            public bool HardStopped;
            public string Reason;

            public SpeedLimitResult(float speed, bool hardStopped, string reason)
            {
                Speed = Mathf.Max(0f, speed);
                HardStopped = hardStopped;
                Reason = reason ?? "";
            }

            public static SpeedLimitResult Clear(float speed)
            {
                return new SpeedLimitResult(speed, false, "");
            }

            public static SpeedLimitResult Stop()
            {
                return Stop("traffic stop");
            }

            public static SpeedLimitResult Stop(string reason)
            {
                return new SpeedLimitResult(0f, true, reason);
            }

            public SpeedLimitResult Limit(SpeedLimitResult other)
            {
                if (HardStopped)
                {
                    return this;
                }

                if (other.HardStopped)
                {
                    return other;
                }

                return Clear(Mathf.Min(Speed, other.Speed));
            }
        }

        public void Configure(CocoonTrafficLanePath path, Transform root, float length, int initialTargetIndex)
        {
            Configure(path, root, length, Mathf.Max(0.04f, length * 0.45f), initialTargetIndex);
        }

        public void Configure(CocoonTrafficLanePath path, Transform root, float length, float width, int initialTargetIndex)
        {
            lanePath = path;
            trackedRoot = root;
            vehicleLength = Mathf.Max(0.04f, length);
            vehicleWidth = Mathf.Max(0.04f, width);
            targetIndex = initialTargetIndex;
            manualProgress = false;
            RefreshProgress();
        }

        public void ConfigureGraph(CocoonRoadGraph graph, Transform root, float length, float width, CocoonRouteCursor initialCursor)
        {
            roadGraph = graph;
            trackedRoot = root;
            vehicleLength = Mathf.Max(0.04f, length);
            vehicleWidth = Mathf.Max(0.04f, width);
            graphCursor = initialCursor;
            manualProgress = false;
        }

        public void ConfigureGraphIfNeeded(CocoonRoadGraph graph, Transform root, float length, float width, CocoonRouteCursor cursor)
        {
            float safeLength = Mathf.Max(0.04f, length);
            float safeWidth = Mathf.Max(0.04f, width);
            if (roadGraph != graph ||
                trackedRoot != root ||
                Mathf.Abs(vehicleLength - safeLength) > 0.0001f ||
                Mathf.Abs(vehicleWidth - safeWidth) > 0.0001f)
            {
                ConfigureGraph(graph, root, safeLength, safeWidth, cursor);
                return;
            }

            graphCursor = cursor;
            manualProgress = false;
        }

        public void SetGraphCursor(CocoonRouteCursor cursor)
        {
            graphCursor = cursor;
        }

        public bool TryGetGraphCursor(out CocoonRouteCursor cursor)
        {
            cursor = graphCursor;
            return roadGraph != null && graphCursor.IsValid;
        }

        public void SetLaneTargetIndex(int laneTargetIndex)
        {
            targetIndex = laneTargetIndex;
            manualProgress = false;
            RefreshProgress();
        }

        public void SetManualProgress(float laneProgress)
        {
            progress = laneProgress;
            manualProgress = true;
        }

        public void SetBlocksTraffic(bool blocks)
        {
            blocksTraffic = blocks;
        }

        public void ReportSpeed(float speed)
        {
            currentSpeed = Mathf.Max(0f, speed);
        }

        public static bool HasHardBlockInPath(CocoonTrafficParticipant self, Transform root, Vector3 targetPosition, float selfLength, float minimumClearance, out CocoonTrafficParticipant blocker)
        {
            return HasHardBlockInPath(self, root, targetPosition, selfLength, minimumClearance, -1f, out blocker);
        }

        public static bool HasHardBlockInPath(
            CocoonTrafficParticipant self,
            Transform root,
            Vector3 targetPosition,
            float selfLength,
            float minimumClearance,
            float maxLookAhead,
            out CocoonTrafficParticipant blocker)
        {
            return HasHardBlockInPath(self, root, targetPosition, selfLength, minimumClearance, maxLookAhead, true, out blocker);
        }

        public static bool HasHardBlockInPath(
            CocoonTrafficParticipant self,
            Transform root,
            Vector3 targetPosition,
            float selfLength,
            float minimumClearance,
            float maxLookAhead,
            bool includeCrossLaneBlocks,
            out CocoonTrafficParticipant blocker)
        {
            blocker = null;
            if (root == null)
            {
                return false;
            }

            float experienceScale = CocoonExperienceScale.RoadmapScale;
            float scaledClearance = Mathf.Max(0.06f, minimumClearance);
            float safeSelfLength = Mathf.Max(0.04f, selfLength);
            float safeSelfWidth = self != null ? self.VehicleWidth : safeSelfLength * 0.45f;
            Vector3 position = Flatten(root.position);
            Vector3 toTarget = Flatten(targetPosition) - position;
            float pathLength = toTarget.magnitude;
            Vector3 direction = pathLength > 0.001f ? toTarget / pathLength : Flatten(root.forward).normalized;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float lookAhead = Mathf.Max(pathLength + safeSelfLength * 0.5f + scaledClearance, safeSelfLength + scaledClearance);
            if (maxLookAhead > 0f)
            {
                float minimumLookAhead = safeSelfLength * 0.5f + scaledClearance;
                lookAhead = Mathf.Min(lookAhead, Mathf.Max(minimumLookAhead, maxLookAhead));
            }

            List<CocoonTrafficParticipant> participants = GetParticipantSnapshot();
            for (int i = 0; i < participants.Count; i++)
            {
                CocoonTrafficParticipant other = participants[i];
                if (other == null || other == self || !other.blocksTraffic || other.trackedRoot == null || other.trackedRoot == root)
                {
                    continue;
                }

                if (!includeCrossLaneBlocks && self != null && self.lanePath != null && other.lanePath != null && other.lanePath != self.lanePath)
                {
                    continue;
                }

                Vector3 offset = Flatten(other.trackedRoot.position) - position;
                float longitudinal = Vector3.Dot(direction, offset);
                float combinedHalfLength = (safeSelfLength + other.VehicleLength) * 0.5f;
                if (longitudinal < -combinedHalfLength || longitudinal > lookAhead + combinedHalfLength)
                {
                    continue;
                }

                Vector3 lateralOffset = offset - direction * longitudinal;
                float lateralDistance = lateralOffset.magnitude;
                float lateralConflictWidth = GetLateralConflictWidth(safeSelfWidth, other.VehicleWidth, experienceScale);
                float clearGap = longitudinal - combinedHalfLength;
                if (lateralDistance <= lateralConflictWidth && clearGap <= scaledClearance)
                {
                    blocker = other;
                    return true;
                }
            }

            return false;
        }

        public float GetAllowedSpeed(float desiredSpeed)
        {
            desiredSpeed = Mathf.Max(0f, desiredSpeed);
            if (desiredSpeed <= 0f)
            {
                lastStopReason = "zero desired speed";
                return 0f;
            }

            RefreshProgress();
            SpeedLimitResult constrainedSpeed = SpeedLimitResult.Clear(desiredSpeed);
            if (roadGraph != null && graphCursor.IsValid)
            {
                constrainedSpeed = constrainedSpeed.Limit(GetGraphLeaderAllowedSpeed(desiredSpeed));
            }
            else if (lanePath != null && lanePath.Count >= 2)
            {
                float totalLength = lanePath.TotalLength;
                if (totalLength > 0.01f)
                {
                    constrainedSpeed = constrainedSpeed.Limit(GetSameLaneAllowedSpeed(desiredSpeed, totalLength));
                }
            }

            constrainedSpeed = constrainedSpeed.Limit(GetPhysicalProximityAllowedSpeed(desiredSpeed));
            if (roadGraph == null || !graphCursor.IsValid)
            {
                constrainedSpeed = constrainedSpeed.Limit(ApplyControlPointRules(constrainedSpeed.Speed));
            }
            lastStopReason = constrainedSpeed.HardStopped ? constrainedSpeed.Reason : "";
            return constrainedSpeed.HardStopped ? 0f : Mathf.Clamp(constrainedSpeed.Speed, 0f, desiredSpeed);
        }

        private SpeedLimitResult GetGraphLeaderAllowedSpeed(float desiredSpeed)
        {
            if (roadGraph == null || !graphCursor.IsValid)
            {
                return SpeedLimitResult.Clear(desiredSpeed);
            }

            float closestGap = float.MaxValue;
            float closestLeaderSpeed = 0f;
            string closestReason = "graph leader";
            List<CocoonTrafficParticipant> participants = GetParticipantSnapshot();
            for (int i = 0; i < participants.Count; i++)
            {
                CocoonTrafficParticipant other = participants[i];
                if (other == null || other == this || other.roadGraph != roadGraph || !other.blocksTraffic || !other.graphCursor.IsValid)
                {
                    continue;
                }

                if (!roadGraph.TryGetForwardDistance(graphCursor, other.graphCursor, out float distanceAhead, Mathf.Max(16, roadGraph.EdgeCount + 8)))
                {
                    continue;
                }

                if (distanceAhead <= SameLaneProgressEpsilon)
                {
                    continue;
                }

                float clearGap = distanceAhead - (VehicleLength + other.VehicleLength) * 0.5f;
                if (clearGap < closestGap)
                {
                    closestGap = clearGap;
                    closestLeaderSpeed = other.currentSpeed;
                    closestReason = DescribeParticipant("graph leader", other);
                }
            }

            return closestGap == float.MaxValue ? SpeedLimitResult.Clear(desiredSpeed) : CalculateGapLimitedSpeed(desiredSpeed, closestGap, closestLeaderSpeed, closestReason);
        }

        private SpeedLimitResult GetSameLaneAllowedSpeed(float desiredSpeed, float totalLength)
        {
            float closestGap = float.MaxValue;
            float closestLeaderSpeed = 0f;
            string closestReason = "same-lane gap";
            Vector3 position = trackedRoot != null ? Flatten(trackedRoot.position) : Vector3.zero;
            float experienceScale = CocoonExperienceScale.RoadmapScale;
            float scaledMinimumGap = Mathf.Max(0.06f, minimumGap * experienceScale);
            List<CocoonTrafficParticipant> participants = GetParticipantSnapshot();
            for (int i = 0; i < participants.Count; i++)
            {
                CocoonTrafficParticipant other = participants[i];
                if (other == null || other == this || other.lanePath != lanePath || !other.blocksTraffic)
                {
                    continue;
                }

                other.RefreshProgress();
                float distanceAhead = other.progress - progress;
                float combinedHalfLength = (VehicleLength + other.VehicleLength) * 0.5f;
                if (Mathf.Abs(distanceAhead) <= SameLaneProgressEpsilon && trackedRoot != null && other.trackedRoot != null)
                {
                    float physicalGap = Vector3.Distance(position, Flatten(other.trackedRoot.position)) - combinedHalfLength;
                    if (physicalGap <= scaledMinimumGap)
                    {
                        if (physicalGap < closestGap)
                        {
                            closestGap = physicalGap;
                            closestLeaderSpeed = other.currentSpeed;
                            closestReason = DescribeParticipant("same-lane overlap", other);
                        }

                        continue;
                    }
                }

                if (distanceAhead <= SameLaneProgressEpsilon)
                {
                    if (!lanePath.IsClosedLoop)
                    {
                        continue;
                    }

                    distanceAhead += totalLength;
                }

                float clearGap = distanceAhead - combinedHalfLength;
                if (clearGap < closestGap)
                {
                    closestGap = clearGap;
                    closestLeaderSpeed = other.currentSpeed;
                    closestReason = DescribeParticipant("same-lane gap", other);
                }
            }

            if (closestGap == float.MaxValue)
            {
                return SpeedLimitResult.Clear(desiredSpeed);
            }

            return CalculateGapLimitedSpeed(desiredSpeed, closestGap, closestLeaderSpeed, closestReason);
        }

        private SpeedLimitResult GetPhysicalProximityAllowedSpeed(float desiredSpeed)
        {
            if (trackedRoot == null)
            {
                return SpeedLimitResult.Clear(desiredSpeed);
            }

            float experienceScale = CocoonExperienceScale.RoadmapScale;
            float scaledMinimumGap = Mathf.Max(0.06f, minimumGap * experienceScale);
            float scaledSlowDistance = GetScaledSlowDistance(scaledMinimumGap, experienceScale);
            Vector3 travelForward = GetTravelForward();
            float closestGap = float.MaxValue;
            float closestBlockerSpeed = 0f;
            string closestReason = "cross-lane proximity";
            Vector3 position = Flatten(trackedRoot.position);

            List<CocoonTrafficParticipant> participants = GetParticipantSnapshot();
            for (int i = 0; i < participants.Count; i++)
            {
                CocoonTrafficParticipant other = participants[i];
                if (other == null || other == this || !other.blocksTraffic || other.trackedRoot == null || other.trackedRoot == trackedRoot)
                {
                    continue;
                }

                Vector3 otherPosition = Flatten(other.trackedRoot.position);
                Vector3 offset = otherPosition - position;
                float distance = offset.magnitude;
                if (distance <= 0.001f)
                {
                    continue;
                }

                float combinedHalfLength = (VehicleLength + other.VehicleLength) * 0.5f;
                float longitudinal = Vector3.Dot(travelForward, offset);
                Vector3 lateralOffset = offset - travelForward * longitudinal;
                float lateralDistance = lateralOffset.magnitude;
                float lateralConflictWidth = GetLateralConflictWidth(VehicleWidth, other.VehicleWidth, experienceScale);
                bool mostlyParallel = IsMostlyParallelTo(other);
                if (mostlyParallel && lateralDistance > lateralConflictWidth)
                {
                    continue;
                }

                float clearGap = distance - combinedHalfLength;
                bool crossingOverlap = Mathf.Abs(longitudinal) <= combinedHalfLength * 0.75f && lateralDistance <= lateralConflictWidth;
                if (crossingOverlap && clearGap <= scaledMinimumGap)
                {
                    if (clearGap < closestGap)
                    {
                        closestGap = clearGap;
                        closestBlockerSpeed = mostlyParallel ? other.currentSpeed : 0f;
                        closestReason = DescribeParticipant(mostlyParallel ? "parallel body overlap" : "cross-lane overlap", other);
                    }

                    continue;
                }

                if (longitudinal <= 0f || longitudinal > combinedHalfLength + scaledSlowDistance || lateralDistance > lateralConflictWidth)
                {
                    continue;
                }

                float forwardDot = Vector3.Dot(travelForward, offset / distance);
                if (forwardDot < 0.35f)
                {
                    continue;
                }

                float forwardClearGap = longitudinal - combinedHalfLength;
                if (forwardClearGap < closestGap)
                {
                    closestGap = forwardClearGap;
                    closestBlockerSpeed = mostlyParallel ? other.currentSpeed : 0f;
                    closestReason = DescribeParticipant(mostlyParallel ? "parallel body corridor" : "cross-lane proximity", other);
                }
            }

            return closestGap == float.MaxValue ? SpeedLimitResult.Clear(desiredSpeed) : CalculateGapLimitedSpeed(desiredSpeed, closestGap, closestBlockerSpeed, closestReason);
        }

        private SpeedLimitResult CalculateGapLimitedSpeed(float desiredSpeed, float closestGap, float leaderSpeed, string stopReason)
        {
            float experienceScale = CocoonExperienceScale.RoadmapScale;
            float scaledMinimumGap = Mathf.Max(0.06f, minimumGap * experienceScale);
            float scaledSlowDistance = GetScaledSlowDistance(scaledMinimumGap, experienceScale);
            if (closestGap <= scaledMinimumGap)
            {
                return SpeedLimitResult.Stop(stopReason);
            }

            if (closestGap >= scaledSlowDistance)
            {
                return SpeedLimitResult.Clear(desiredSpeed);
            }

            float availableGap = Mathf.Max(0f, closestGap - scaledMinimumGap);
            float headway = Mathf.Max(0.25f, TrafficTimeHeadwaySeconds);
            float leaderLimitedSpeed = Mathf.Max(0f, leaderSpeed) + availableGap / headway;
            float brakingDistance = currentSpeed * currentSpeed / Mathf.Max(0.1f, 2f * TrafficComfortBrakingMetersPerSecond);
            if (availableGap <= brakingDistance * 0.35f && currentSpeed > leaderSpeed + 0.02f)
            {
                leaderLimitedSpeed = Mathf.Min(leaderLimitedSpeed, Mathf.Max(0f, leaderSpeed));
            }

            float t = Mathf.InverseLerp(scaledMinimumGap, scaledSlowDistance, closestGap);
            float easedSpeed = Mathf.Lerp(0f, desiredSpeed, Mathf.SmoothStep(0f, 1f, t));
            return SpeedLimitResult.Clear(Mathf.Clamp(Mathf.Min(easedSpeed, leaderLimitedSpeed), 0f, desiredSpeed));
        }

        private float GetScaledSlowDistance(float scaledMinimumGap, float experienceScale)
        {
            float timeHeadwayDistance = currentSpeed * 0.65f;
            return Mathf.Max(scaledMinimumGap + 0.06f, slowDistance * experienceScale + timeHeadwayDistance);
        }

        private SpeedLimitResult ApplyControlPointRules(float desiredSpeed)
        {
            if (lanePath == null || trackedRoot == null || lanePath.Count < 2 || desiredSpeed <= 0f)
            {
                return desiredSpeed <= 0f ? SpeedLimitResult.Stop("prior speed limit") : SpeedLimitResult.Clear(desiredSpeed);
            }

            Transform target = lanePath.GetWaypoint(targetIndex);
            CocoonTrafficControlPoint controlPoint = target != null ? target.GetComponent<CocoonTrafficControlPoint>() : null;
            if (controlPoint == null || controlPoint.ControlType == CocoonTrafficControlType.Cruise)
            {
                activeControlPoint = null;
                if (servedControlPoint != null && target != servedControlPoint.transform)
                {
                    servedControlPoint = null;
                }

                return SpeedLimitResult.Clear(desiredSpeed);
            }

            Vector3 trackedPosition = trackedRoot.position;
            Vector3 targetPosition = target.position;
            trackedPosition.y = 0f;
            targetPosition.y = 0f;
            float distance = Vector3.Distance(trackedPosition, targetPosition);
            float approachDistance = controlPoint.ApproachDistance;

            if (servedControlPoint == controlPoint)
            {
                return SpeedLimitResult.Clear(desiredSpeed);
            }

            if (distance > approachDistance)
            {
                activeControlPoint = null;
                float slowZone = approachDistance * 1.85f;
                if (distance < slowZone)
                {
                    float t = Mathf.InverseLerp(slowZone, approachDistance, distance);
                    return SpeedLimitResult.Clear(Mathf.Lerp(desiredSpeed, desiredSpeed * controlPoint.SlowSpeedMultiplier, Mathf.Clamp01(t)));
                }

                return SpeedLimitResult.Clear(desiredSpeed);
            }

            if (activeControlPoint != controlPoint)
            {
                activeControlPoint = controlPoint;
                controlStopTimer = controlPoint.StopSeconds;
            }

            if (controlStopTimer > 0f)
            {
                controlStopTimer -= Time.deltaTime;
                return SpeedLimitResult.Stop("control-point stop");
            }

            if (!controlPoint.TryReserve(this))
            {
                return SpeedLimitResult.Stop("control-point reservation");
            }

            servedControlPoint = controlPoint;
            activeControlPoint = null;
            return SpeedLimitResult.Clear(desiredSpeed * controlPoint.ReleaseSpeedMultiplier);
        }

        private void OnEnable()
        {
            if (!Participants.Contains(this))
            {
                Participants.Add(this);
                participantSnapshotFrame = -1;
            }
        }

        private void OnDisable()
        {
            Participants.Remove(this);
            participantSnapshotFrame = -1;
        }

        private static List<CocoonTrafficParticipant> GetParticipantSnapshot()
        {
            if (participantSnapshotFrame == Time.frameCount)
            {
                return ParticipantSnapshot;
            }

            ParticipantSnapshot.Clear();
            for (int i = 0; i < Participants.Count; i++)
            {
                CocoonTrafficParticipant participant = Participants[i];
                if (participant != null && participant.isActiveAndEnabled)
                {
                    ParticipantSnapshot.Add(participant);
                }
            }

            participantSnapshotFrame = Time.frameCount;
            return ParticipantSnapshot;
        }

        private void RefreshProgress()
        {
            if (manualProgress || lanePath == null || trackedRoot == null)
            {
                return;
            }

            progress = lanePath.GetProgressAtSegmentTarget(targetIndex, trackedRoot.position);
        }

        private Vector3 GetTravelForward()
        {
            if (roadGraph != null && graphCursor.IsValid)
            {
                Vector3 graphForward = roadGraph.GetForward(graphCursor.EdgeId);
                if (graphForward.sqrMagnitude > 0.0001f)
                {
                    return graphForward.normalized;
                }
            }

            if (trackedRoot != null && lanePath != null && lanePath.Count >= 2)
            {
                Transform target = lanePath.GetWaypoint(targetIndex);
                if (target != null)
                {
                    Vector3 towardTarget = target.position - trackedRoot.position;
                    towardTarget.y = 0f;
                    if (towardTarget.sqrMagnitude > 0.0001f)
                    {
                        return towardTarget.normalized;
                    }
                }

                Transform previous = lanePath.GetWaypoint(targetIndex - 1);
                Transform current = lanePath.GetWaypoint(targetIndex);
                if (previous != null && current != null)
                {
                    Vector3 segment = current.position - previous.position;
                    segment.y = 0f;
                    if (segment.sqrMagnitude > 0.0001f)
                    {
                        return segment.normalized;
                    }
                }
            }

            Vector3 forward = trackedRoot != null ? trackedRoot.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private bool IsMostlyParallelTo(CocoonTrafficParticipant other)
        {
            if (other == null)
            {
                return false;
            }

            if ((roadGraph == null || !graphCursor.IsValid) &&
                (lanePath == null || other.lanePath == null))
            {
                return false;
            }

            Vector3 ownForward = GetTravelForward();
            Vector3 otherForward = other.GetTravelForward();
            return Mathf.Abs(Vector3.Dot(ownForward, otherForward)) > 0.72f;
        }

        private static string DescribeParticipant(string reason, CocoonTrafficParticipant other)
        {
            if (other == null)
            {
                return reason;
            }

            string objectName = other.gameObject != null ? other.gameObject.name : "traffic";
            return reason + " with " + objectName + " lane=" + other.LaneName + " target=" + other.TargetIndex;
        }

        private static float GetLateralConflictWidth(float firstWidth, float secondWidth, float experienceScale)
        {
            return Mathf.Max(0.25f * experienceScale, (Mathf.Max(0.04f, firstWidth) + Mathf.Max(0.04f, secondWidth)) * 0.5f + 0.08f * experienceScale);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
