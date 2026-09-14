using UnityEngine;

namespace CocoonPrototype
{
    public sealed class CocoonTrafficVehicle : MonoBehaviour
    {
        private const float TrafficSpeedMultiplier = 1.45f;
        private const float MinimumTrafficCruiseSpeed = 3.6f;
        private const float MinimumTurnMoveScale = 0.55f;
        private const float TrafficAccelerationMetersPerSecond = 4.8f;
        private const float TrafficBrakingMetersPerSecond = 12f;
        private const int ExpectedWheelVisualCount = 4;

        [SerializeField] private CocoonTrafficLanePath lanePath;
        [SerializeField] private CocoonRoadGraph roadGraph;
        [SerializeField] private bool useRoadGraph = true;
        [SerializeField] private CocoonRouteCursor graphCursor = CocoonRouteCursor.Invalid();
        [SerializeField] private float speed = 3f;
        [SerializeField] private int targetIndex = 1;
        [SerializeField] private float waypointArrivalDistance = 0.2f;
        [SerializeField] private float vehicleLength = 0.3f;
        [SerializeField] private bool autoDetectVisualForward = true;
        [SerializeField] private bool preserveAuthoredSpawnTransform;
        [SerializeField] private float visualYawOffset;
        [SerializeField] private float turnDegreesPerSecond = 540f;
        [SerializeField] private float graphTurnRadiusMeters = 2.2f;
        [SerializeField] private float graphTurnRateDegreesPerSecond = 360f;
        [SerializeField] private bool snapToGroundOnStart = true;
        [SerializeField] private float groundYOffset;
        [SerializeField] private float groundRaycastHeight = 0.6f;
        [SerializeField] private float groundRaycastDistance = 1.8f;
        [SerializeField] private LayerMask groundMask = ~0;

        private CocoonTrafficParticipant participant;
        private CocoonTaxiStateMachine taxiStateMachine;
        private float currentMoveSpeed;
        private float stoppedDiagnosticTimer;
        private float nextStoppedDiagnosticLogTime;
        private bool hasLoggedGroundSnap;
        private bool hasLoggedGraphDeadEnd;

        private void Awake()
        {
            if (!IsTrafficVehicleRuntimeName(name))
            {
                DisableTrafficParticipation(true);
                enabled = false;
                return;
            }

            vehicleLength = Mathf.Max(vehicleLength, EstimateVehicleWorldLength());
            EnsureWheelVisuals();
            DisablePhysicalCollision();
            DetectVisualForwardYawOffset();
            if (preserveAuthoredSpawnTransform)
            {
                targetIndex = ResolveTargetIndexFromCurrentPosition();
                CocoonDebugLog.Info("Traffic", name + " preserving authored spawn at " + FormatPosition(transform.position) + ", target=" + targetIndex + ".", this);
            }
            else
            {
                ResolveInitialLaneAwayFromTaxiDirection();
            }

            SnapRootToGround("startup");
            EnsureParticipant();
            participant.Configure(lanePath, transform, vehicleLength, EstimateVehicleWorldWidth(), targetIndex);
            ConfigureGraph(roadGraph);
            participant.SetBlocksTraffic(true);
        }

        public bool PreserveAuthoredSpawnTransform => preserveAuthoredSpawnTransform;

        public void SetPreserveAuthoredSpawnTransform(bool preserve)
        {
            preserveAuthoredSpawnTransform = preserve;
        }

        public void Configure(CocoonTrafficLanePath path, float travelSpeed, int startIndex)
        {
            Configure(path, travelSpeed, startIndex, 3f);
        }

        public void Configure(CocoonTrafficLanePath path, float travelSpeed, int startIndex, float length)
        {
            lanePath = path;
            speed = travelSpeed;
            vehicleLength = Mathf.Max(0.04f, length);
            if (preserveAuthoredSpawnTransform)
            {
                targetIndex = ResolveTargetIndexFromCurrentPosition(path);
            }
            else if (path != null && path.TryGetNextIndex(startIndex, out int nextIndex))
            {
                targetIndex = nextIndex;
            }
            else
            {
                targetIndex = path != null ? path.NormalizeIndex(startIndex) : startIndex + 1;
            }

            currentMoveSpeed = 0f;
            DisablePhysicalCollision();
            DetectVisualForwardYawOffset();
            CocoonDebugLog.Info("Traffic", name + " configured. speed=" + speed.ToString("0.00") + ", pathPoints=" + (lanePath != null ? lanePath.Count.ToString() : "none") + ", startIndex=" + startIndex + ", preserveSpawn=" + preserveAuthoredSpawnTransform + ".", this);

            Transform start = lanePath != null ? lanePath.GetWaypoint(startIndex) : null;
            Transform next = lanePath != null ? lanePath.GetWaypoint(targetIndex) : null;
            if (start != null && !preserveAuthoredSpawnTransform)
            {
                transform.position = start.position;
            }

            if (next != null && !preserveAuthoredSpawnTransform)
            {
                Vector3 direction = next.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    ApplyFacing(direction, true);
                }
            }

            SnapRootToGround("configure");
            EnsureParticipant();
            participant.Configure(lanePath, transform, vehicleLength, EstimateVehicleWorldWidth(), targetIndex);
            ConfigureGraph(roadGraph);
            participant.SetBlocksTraffic(true);
        }

        public void ConfigureGraph(CocoonRoadGraph graph)
        {
            roadGraph = graph != null ? graph : roadGraph;
            if (roadGraph == null)
            {
                roadGraph = FindObjectOfType<CocoonRoadGraph>();
            }

            if (roadGraph == null)
            {
                return;
            }

            if (!graphCursor.IsValid || roadGraph.GetEdge(graphCursor.EdgeId) == null)
            {
                roadGraph.TryProjectToGraph(transform.position, out graphCursor);
            }

            EnsureParticipant();
            if (graphCursor.IsValid)
            {
                participant.ConfigureGraphIfNeeded(roadGraph, transform, vehicleLength, EstimateVehicleWorldWidth(), graphCursor);
            }
        }

        private int ResolveTargetIndexFromCurrentPosition()
        {
            return ResolveTargetIndexFromCurrentPosition(lanePath);
        }

        private int ResolveTargetIndexFromCurrentPosition(CocoonTrafficLanePath path)
        {
            if (path == null || path.Count < 2)
            {
                return Mathf.Max(1, targetIndex);
            }

            return path.FindNearestSegmentTargetIndex(transform.position);
        }

        private void Update()
        {
            if (taxiStateMachine == null)
            {
                taxiStateMachine = FindObjectOfType<CocoonTaxiStateMachine>();
            }

            if (taxiStateMachine != null && !CocoonTaxiStateMachine.IsPreRideTrafficReleased)
            {
                currentMoveSpeed = 0f;
                if (participant != null)
                {
                    participant.ReportSpeed(0f);
                }

                return;
            }

            if (useRoadGraph && TryUpdateGraphMotion())
            {
                return;
            }

            if (lanePath == null || lanePath.Count < 2)
            {
                return;
            }

            Transform target = lanePath.GetWaypoint(targetIndex);
            if (target == null)
            {
                return;
            }

            Vector3 before = transform.position;
            EnsureParticipant();
            participant.SetLaneTargetIndex(targetIndex);
            float experienceScale = CocoonExperienceScale.RoadmapScale;
            float allowedSpeed = participant.GetAllowedSpeed(GetDesiredCruiseSpeed() * experienceScale);
            UpdateStoppedDiagnostics(allowedSpeed);
            if (allowedSpeed <= 0.0001f)
            {
                currentMoveSpeed = 0f;
                participant.ReportSpeed(0f);
                return;
            }

            float acceleration = allowedSpeed > currentMoveSpeed ? TrafficAccelerationMetersPerSecond : TrafficBrakingMetersPerSecond;
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, allowedSpeed, acceleration * experienceScale * Time.deltaTime);
            Vector3 desiredDirection = GetFlatDirectionTo(target.position);
            ApplyFacing(desiredDirection, false);
            float turnMoveScale = GetTurnMoveScale(desiredDirection);
            Vector3 moveTarget = target.position;
            moveTarget.y = transform.position.y;
            float stepDistance = currentMoveSpeed * turnMoveScale * Time.deltaTime;
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, moveTarget, stepDistance);
            if (CocoonTrafficParticipant.HasHardBlockInPath(
                    participant,
                    transform,
                    nextPosition,
                    vehicleLength,
                    0.08f * experienceScale,
                    Mathf.Max(vehicleLength, stepDistance + vehicleLength * 0.5f),
                    true,
                    out _))
            {
                currentMoveSpeed = 0f;
                participant.ReportSpeed(0f);
                return;
            }

            transform.position = nextPosition;
            Vector3 delta = transform.position - before;
            delta.y = 0f;

            participant.ReportSpeed(Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f);

            if (Vector3.Distance(transform.position, moveTarget) <= Mathf.Max(0.01f, waypointArrivalDistance * experienceScale))
            {
                if (!lanePath.TryGetNextIndex(targetIndex, out int nextIndex))
                {
                    currentMoveSpeed = 0f;
                    participant.ReportSpeed(0f);
                    CocoonDebugLog.Warn("Traffic", name + " reached the end of non-loop lane " + lanePath.name + "; stopping instead of wrapping through an END gap.", this);
                    return;
                }

                targetIndex = nextIndex;
                participant.SetLaneTargetIndex(targetIndex);
                Transform nextTarget = lanePath.GetWaypoint(targetIndex);
                if (nextTarget != null)
                {
                    ApplyFacing(GetFlatDirectionTo(nextTarget.position), false);
                }
            }
        }

        private bool TryUpdateGraphMotion()
        {
            if (roadGraph == null)
            {
                roadGraph = FindObjectOfType<CocoonRoadGraph>();
            }

            if (roadGraph == null)
            {
                return false;
            }

            EnsureParticipant();
            if (!graphCursor.IsValid && !roadGraph.TryProjectToGraph(transform.position, out graphCursor))
            {
                return false;
            }

            participant.ConfigureGraphIfNeeded(roadGraph, transform, vehicleLength, EstimateVehicleWorldWidth(), graphCursor);
            float experienceScale = CocoonExperienceScale.RoadmapScale;
            float allowedSpeed = participant.GetAllowedSpeed(GetDesiredCruiseSpeed() * experienceScale);
            UpdateStoppedDiagnostics(allowedSpeed);
            if (allowedSpeed <= 0.0001f)
            {
                currentMoveSpeed = 0f;
                participant.ReportSpeed(0f);
                return true;
            }

            float acceleration = allowedSpeed > currentMoveSpeed ? TrafficAccelerationMetersPerSecond : TrafficBrakingMetersPerSecond;
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, allowedSpeed, acceleration * experienceScale * Time.deltaTime);
            float stepDistance = currentMoveSpeed * Time.deltaTime;
            CocoonRouteCursor nextCursor = graphCursor;
            if (!roadGraph.TryAdvance(ref nextCursor, stepDistance, out bool hitDeadEnd) && hitDeadEnd)
            {
                currentMoveSpeed = 0f;
                participant.ReportSpeed(0f);
                if (!hasLoggedGraphDeadEnd)
                {
                    hasLoggedGraphDeadEnd = true;
                    CocoonDebugLog.Warn("Traffic", name + " reached a ROADMAP graph dead end at edge " + graphCursor.EdgeId + "; stopping instead of turning through END.", this);
                }

                return true;
            }

            Vector3 before = transform.position;
            Vector3 nextPosition;
            Vector3 graphForward;
            float turnRadius = Mathf.Max(0f, graphTurnRadiusMeters) * experienceScale;
            if (!roadGraph.EvaluateSmoothedPose(nextCursor, turnRadius, out nextPosition, out graphForward))
            {
                nextPosition = roadGraph.GetPosition(nextCursor);
                graphForward = roadGraph.GetForward(nextCursor.EdgeId);
            }

            nextPosition.y = transform.position.y;
            if (CocoonTrafficParticipant.HasHardBlockInPath(
                    participant,
                    transform,
                    nextPosition,
                    vehicleLength,
                    0.08f * experienceScale,
                    Mathf.Max(vehicleLength, stepDistance + vehicleLength * 0.5f),
                    true,
                    out _))
            {
                currentMoveSpeed = 0f;
                participant.ReportSpeed(0f);
                return true;
            }

            ApplyFacing(graphForward, false, graphTurnRateDegreesPerSecond);
            transform.position = nextPosition;
            graphCursor = nextCursor;
            participant.SetGraphCursor(graphCursor);
            Vector3 delta = transform.position - before;
            delta.y = 0f;
            participant.ReportSpeed(Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f);
            hasLoggedGraphDeadEnd = false;
            return true;
        }

        private void ResolveInitialLaneAwayFromTaxiDirection()
        {
            if (lanePath == null || lanePath.Count < 2)
            {
                return;
            }

            CocoonTaxiStateMachine taxi = FindObjectOfType<CocoonTaxiStateMachine>();
            CocoonTrafficLanePath taxiLane = taxi != null ? taxi.PrimaryCruiseLane : null;
            CocoonTrafficLanePath oppositeLane = taxi != null ? taxi.OppositeCruiseLane : null;
            if (taxiLane == null || oppositeLane == null || oppositeLane.Count < 2 || lanePath != taxiLane)
            {
                return;
            }

            int startIndex = oppositeLane.NormalizeIndex(targetIndex - 1);
            lanePath = oppositeLane;
            targetIndex = oppositeLane.NormalizeIndex(startIndex + 1);
            Transform start = lanePath.GetWaypoint(startIndex);
            Transform next = lanePath.GetWaypoint(targetIndex);
            if (start != null)
            {
                transform.position = start.position;
            }

            if (start != null && next != null)
            {
                Vector3 direction = next.position - start.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    ApplyFacing(direction, true);
                }
            }

            CocoonDebugLog.Info("Traffic", name + " moved to opposite lane at startup so the taxi's initial lane stays clear.", this);
        }

        private void DisableTrafficParticipation(bool hideVisuals)
        {
            CocoonTrafficParticipant existingParticipant = GetComponent<CocoonTrafficParticipant>();
            if (existingParticipant != null)
            {
                existingParticipant.SetBlocksTraffic(false);
                existingParticipant.enabled = false;
            }

            if (!hideVisuals)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            CocoonDebugLog.Verbose("Traffic", name + " hidden because it is a legacy static traffic visual with no runtime lane.", this);
        }

        private void EnsureParticipant()
        {
            if (participant == null)
            {
                participant = GetComponent<CocoonTrafficParticipant>();
                if (participant == null)
                {
                    participant = gameObject.AddComponent<CocoonTrafficParticipant>();
                }
            }
        }

        private void DisablePhysicalCollision()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rigidbody = rigidbodies[i];
                if (rigidbody == null)
                {
                    continue;
                }

                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }
        }

        private void ApplyFacing(Vector3 direction, bool immediate)
        {
            ApplyFacing(direction, immediate, turnDegreesPerSecond);
        }

        private void ApplyFacing(Vector3 direction, bool immediate, float degreesPerSecond)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion lookRotation = GetFacingRotation(direction);
            transform.rotation = immediate ? lookRotation : Quaternion.RotateTowards(transform.rotation, lookRotation, Mathf.Max(1f, degreesPerSecond) * Time.deltaTime);
        }

        private float GetTurnMoveScale(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || turnDegreesPerSecond <= 0f)
            {
                return 1f;
            }

            float angle = Quaternion.Angle(transform.rotation, GetFacingRotation(direction));
            if (angle <= 12f)
            {
                return 1f;
            }

            if (angle >= 70f)
            {
                return MinimumTurnMoveScale;
            }

            return Mathf.Lerp(MinimumTurnMoveScale, 1f, Mathf.InverseLerp(70f, 12f, angle));
        }

        private float GetDesiredCruiseSpeed()
        {
            return Mathf.Max(speed * TrafficSpeedMultiplier, MinimumTrafficCruiseSpeed);
        }

        private void UpdateStoppedDiagnostics(float allowedSpeed)
        {
            if (allowedSpeed > 0.02f)
            {
                stoppedDiagnosticTimer = 0f;
                return;
            }

            stoppedDiagnosticTimer += Time.deltaTime;
            if (stoppedDiagnosticTimer < 1.5f || Time.time < nextStoppedDiagnosticLogTime)
            {
                return;
            }

            nextStoppedDiagnosticLogTime = Time.time + 3f;
            CocoonDebugLog.Info("Traffic", name + " still held. lane=" + (lanePath != null ? lanePath.name : "No Lane") +
                ", target=" + targetIndex +
                ", reason=" + (participant != null ? participant.LastStopReason : "no participant") +
                ", pos=" + FormatPosition(transform.position) +
                ", speed=" + currentMoveSpeed.ToString("0.00") + ".", this);
        }

        private Quaternion GetFacingRotation(Vector3 direction)
        {
            direction.y = 0f;
            return Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, visualYawOffset, 0f);
        }

        private Vector3 GetFlatDirectionTo(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            return direction;
        }

        private void EnsureWheelVisuals()
        {
            if (CountWheelVisuals() >= ExpectedWheelVisualCount || !TryGetLocalRendererBounds(out Bounds bounds))
            {
                return;
            }

            bool lengthAlongX = bounds.size.x > bounds.size.z * 1.18f;
            Material wheelMaterial = ResolveWheelMaterial();
            Vector3 center = bounds.center;
            float scale = Mathf.Max(CocoonExperienceScale.RoadmapScale, 0.02f);
            float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.13f, 0.07f * scale, 0.26f * scale);
            float thickness = Mathf.Clamp(radius * 0.42f, 0.035f * scale, 0.12f * scale);
            float wheelY = bounds.min.y + Mathf.Max(radius, bounds.size.y * 0.18f);

            if (lengthAlongX)
            {
                float frontX = center.x + bounds.extents.x * 0.62f;
                float rearX = center.x - bounds.extents.x * 0.62f;
                float leftZ = center.z - bounds.extents.z - thickness * 0.45f;
                float rightZ = center.z + bounds.extents.z + thickness * 0.45f;
                CreateWheelIfMissing("FL", new Vector3(frontX, wheelY, leftZ), new Vector3(radius, thickness, radius), Quaternion.Euler(90f, 0f, 0f), wheelMaterial);
                CreateWheelIfMissing("FR", new Vector3(frontX, wheelY, rightZ), new Vector3(radius, thickness, radius), Quaternion.Euler(90f, 0f, 0f), wheelMaterial);
                CreateWheelIfMissing("RL", new Vector3(rearX, wheelY, leftZ), new Vector3(radius, thickness, radius), Quaternion.Euler(90f, 0f, 0f), wheelMaterial);
                CreateWheelIfMissing("RR", new Vector3(rearX, wheelY, rightZ), new Vector3(radius, thickness, radius), Quaternion.Euler(90f, 0f, 0f), wheelMaterial);
                return;
            }

            float frontZ = center.z + bounds.extents.z * 0.62f;
            float rearZ = center.z - bounds.extents.z * 0.62f;
            float leftX = center.x - bounds.extents.x - thickness * 0.45f;
            float rightX = center.x + bounds.extents.x + thickness * 0.45f;
            CreateWheelIfMissing("FL", new Vector3(leftX, wheelY, frontZ), new Vector3(radius, thickness, radius), Quaternion.Euler(0f, 0f, 90f), wheelMaterial);
            CreateWheelIfMissing("FR", new Vector3(rightX, wheelY, frontZ), new Vector3(radius, thickness, radius), Quaternion.Euler(0f, 0f, 90f), wheelMaterial);
            CreateWheelIfMissing("RL", new Vector3(leftX, wheelY, rearZ), new Vector3(radius, thickness, radius), Quaternion.Euler(0f, 0f, 90f), wheelMaterial);
            CreateWheelIfMissing("RR", new Vector3(rightX, wheelY, rearZ), new Vector3(radius, thickness, radius), Quaternion.Euler(0f, 0f, 90f), wheelMaterial);
        }

        private void CreateWheelIfMissing(string slot, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            if (HasWheelSlot(slot))
            {
                return;
            }

            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Runtime Wheel " + slot;
            wheel.transform.SetParent(transform, false);
            wheel.transform.localPosition = localPosition;
            wheel.transform.localScale = localScale;
            wheel.transform.localRotation = localRotation;

            Collider collider = wheel.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = wheel.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private int CountWheelVisuals()
        {
            int count = 0;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && IsWheelName(renderer.transform.name))
                {
                    count++;
                }
            }

            return count;
        }

        private bool HasWheelSlot(string slot)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            string lowerSlot = slot.ToLowerInvariant();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && IsWheelName(candidate.name) && candidate.name.ToLowerInvariant().Contains(lowerSlot))
                {
                    return true;
                }
            }

            return false;
        }

        private Material ResolveWheelMaterial()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && IsWheelName(renderer.transform.name) && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            material.name = "Runtime Traffic Wheel Material";
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.015f, 0.015f, 0.018f));
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.015f, 0.015f, 0.018f));
            }

            return material;
        }

        private static bool IsWheelName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string lower = value.ToLowerInvariant();
            return lower.Contains("wheel") || lower.Contains("tire") || lower.Contains("tyre");
        }

        private static bool IsTrafficVehicleRuntimeName(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("Traffic ", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (value.StartsWith("Traffic ROADMAP ", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string lower = value.ToLowerInvariant();
            return lower.Contains("sedan") ||
                   lower.Contains("compact") ||
                   lower.Contains("van") ||
                   lower.Contains("bus") ||
                   lower.Contains("car");
        }

        private void DetectVisualForwardYawOffset()
        {
            if (!autoDetectVisualForward || !TryGetLocalRendererBounds(out Bounds bounds))
            {
                return;
            }

            Vector3 size = bounds.size;
            visualYawOffset = size.x > size.z * 1.18f ? -90f : 0f;
        }

        private void SnapRootToGround(string reason)
        {
            if (!snapToGroundOnStart || !TryGetGroundContactBounds(out Bounds contactBounds))
            {
                return;
            }

            float groundY = ResolveGroundY(contactBounds.center) + groundYOffset;
            float correctionY = groundY - contactBounds.min.y;
            if (Mathf.Abs(correctionY) <= 0.0005f)
            {
                return;
            }

            transform.position += Vector3.up * correctionY;
            if (!hasLoggedGroundSnap)
            {
                hasLoggedGroundSnap = true;
                CocoonDebugLog.Info(
                    "Traffic",
                    name + " ground snap " + reason + ": correctionY=" + correctionY.ToString("0.###") +
                    ", groundY=" + groundY.ToString("0.###") +
                    ", contactBottom=" + contactBounds.min.y.ToString("0.###") + ".",
                    this);
            }
        }

        private bool TryGetGroundContactBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool hasBounds = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !IsWheelName(renderer.transform.name))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds || TryGetWorldRendererBounds(out bounds);
        }

        private float ResolveGroundY(Vector3 referencePosition)
        {
            Vector3 origin = referencePosition + Vector3.up * Mathf.Max(0.01f, groundRaycastHeight);
            float distance = Mathf.Max(0.05f, groundRaycastHeight + groundRaycastDistance);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return 0f;
        }

        private bool TryGetLocalRendererBounds(out Bounds localBounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            localBounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.localBounds;
                Vector3 min = rendererBounds.min;
                Vector3 max = rendererBounds.max;
                Vector3[] corners =
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, max.y, max.z)
                };

                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 localPoint = transform.InverseTransformPoint(renderer.transform.TransformPoint(corners[cornerIndex]));
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }

            return hasBounds;
        }

        private bool TryGetWorldRendererBounds(out Bounds worldBounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            worldBounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (renderer.transform.name.StartsWith("Runtime Wheel ", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    worldBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private float EstimateVehicleWorldLength()
        {
            return TryGetWorldRendererBounds(out Bounds bounds) ? Mathf.Max(0.04f, bounds.size.x, bounds.size.z) : 0.04f;
        }

        private float EstimateVehicleWorldWidth()
        {
            return TryGetWorldRendererBounds(out Bounds bounds) ? Mathf.Max(0.04f, Mathf.Min(bounds.size.x, bounds.size.z)) : Mathf.Max(0.04f, vehicleLength * 0.45f);
        }

        private static string FormatPosition(Vector3 position)
        {
            return "(" + position.x.ToString("0.00") + ", " + position.y.ToString("0.00") + ", " + position.z.ToString("0.00") + ")";
        }
    }
}
