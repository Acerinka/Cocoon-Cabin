using System;
using System.Collections.Generic;
using UnityEngine;

namespace CocoonPrototype
{
    [Serializable]
    public struct CocoonRouteCursor
    {
        public int EdgeId;
        public float Progress;

        public bool IsValid => EdgeId >= 0;

        public static CocoonRouteCursor Invalid()
        {
            return new CocoonRouteCursor { EdgeId = -1, Progress = 0f };
        }
    }

    public sealed class CocoonRoadGraph : MonoBehaviour
    {
        [Serializable]
        public sealed class Node
        {
            public int Id;
            public int Row;
            public int Column;
            public Vector3 Position;
        }

        [Serializable]
        public sealed class Edge
        {
            public int Id;
            public int FromNode;
            public int ToNode;
            public Vector3 Start;
            public Vector3 End;
            public float Length;
            public int[] NextEdgeIds = new int[0];
        }

        [Serializable]
        public sealed class BayBinding
        {
            public Transform Stop;
            public int EdgeId = -1;
            public float Progress;
            public Vector3 Position;
            public Vector3 Forward = Vector3.forward;
            public float SideSign;
        }

        [SerializeField] private List<Node> nodes = new List<Node>();
        [SerializeField] private List<Edge> edges = new List<Edge>();
        [SerializeField] private List<BayBinding> bays = new List<BayBinding>();
        [SerializeField] private List<int> preferredNextEdgeIds = new List<int>();
        [SerializeField] private List<int> preferredPreviousEdgeIds = new List<int>();
        [SerializeField] private int connectedComponentCount;
        [SerializeField] private float projectionMaxDistance = 0.65f;

        private readonly Dictionary<int, List<int>> outgoingEdgesByNode = new Dictionary<int, List<int>>();

        public int EdgeCount => edges != null ? edges.Count : 0;
        public int NodeCount => nodes != null ? nodes.Count : 0;
        public int BayCount => bays != null ? bays.Count : 0;
        public int ConnectedComponentCount => connectedComponentCount;

        public IReadOnlyList<Edge> Edges => edges;
        public IReadOnlyList<BayBinding> Bays => bays;

        public void Configure(List<Node> graphNodes, List<Edge> graphEdges, Transform[] pickupStops, float maxProjectionDistance)
        {
            nodes = graphNodes ?? new List<Node>();
            edges = graphEdges ?? new List<Edge>();
            projectionMaxDistance = Mathf.Max(0.02f, maxProjectionDistance);
            NormalizeEdgeIds();
            RebuildConnectivity();
            RebuildPreferredNextEdges();
            RebuildPreferredPreviousEdges();
            connectedComponentCount = CountConnectedComponents();
            BindPickupStops(pickupStops);
        }

        public BayBinding GetBay(int index)
        {
            return bays != null && index >= 0 && index < bays.Count ? bays[index] : null;
        }

        public Edge GetEdge(int edgeId)
        {
            return edges != null && edgeId >= 0 && edgeId < edges.Count ? edges[edgeId] : null;
        }

        public bool TryGetBayIndex(Transform stop, out int bayIndex)
        {
            bayIndex = -1;
            if (stop == null || bays == null)
            {
                return false;
            }

            for (int i = 0; i < bays.Count; i++)
            {
                if (bays[i] != null && bays[i].Stop == stop)
                {
                    bayIndex = i;
                    return true;
                }
            }

            return false;
        }

        public bool TryProjectToGraph(Vector3 worldPosition, out CocoonRouteCursor cursor)
        {
            cursor = CocoonRouteCursor.Invalid();
            if (edges == null || edges.Count == 0)
            {
                return false;
            }

            Vector3 flat = Flatten(worldPosition);
            float bestDistanceSqr = float.MaxValue;
            int bestEdgeId = -1;
            float bestProgress = 0f;
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge == null || edge.Length <= 0.001f)
                {
                    continue;
                }

                Vector3 start = Flatten(edge.Start);
                Vector3 end = Flatten(edge.End);
                Vector3 segment = end - start;
                float lengthSqr = segment.sqrMagnitude;
                if (lengthSqr <= 0.000001f)
                {
                    continue;
                }

                float t = Mathf.Clamp01(Vector3.Dot(flat - start, segment) / lengthSqr);
                Vector3 closest = start + segment * t;
                float distanceSqr = Vector3.SqrMagnitude(flat - closest);
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestEdgeId = edge.Id;
                    bestProgress = edge.Length * t;
                }
            }

            if (bestEdgeId < 0)
            {
                return false;
            }

            float maxDistance = projectionMaxDistance > 0f ? projectionMaxDistance : float.MaxValue;
            if (bestDistanceSqr > maxDistance * maxDistance)
            {
                return false;
            }

            cursor.EdgeId = bestEdgeId;
            cursor.Progress = Mathf.Clamp(bestProgress, 0f, GetEdgeLength(bestEdgeId));
            return true;
        }

        public Vector3 GetPosition(CocoonRouteCursor cursor)
        {
            Edge edge = GetEdge(cursor.EdgeId);
            if (edge == null || edge.Length <= 0.001f)
            {
                return transform.position;
            }

            float t = Mathf.Clamp01(cursor.Progress / edge.Length);
            return Vector3.Lerp(edge.Start, edge.End, t);
        }

        public Vector3 GetForward(int edgeId)
        {
            Edge edge = GetEdge(edgeId);
            if (edge == null)
            {
                return Vector3.forward;
            }

            Vector3 forward = edge.End - edge.Start;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        public bool EvaluateSmoothedPose(CocoonRouteCursor cursor, float turnRadius, out Vector3 position, out Vector3 tangent)
        {
            position = GetPosition(cursor);
            tangent = GetForward(cursor.EdgeId);
            Edge edge = GetEdge(cursor.EdgeId);
            if (edge == null || edge.Length <= 0.001f)
            {
                return false;
            }

            turnRadius = Mathf.Max(0f, turnRadius);
            if (turnRadius <= 0.001f)
            {
                return true;
            }

            float incomingWindow = Mathf.Min(turnRadius, edge.Length * 0.45f);
            float distanceToEnd = edge.Length - Mathf.Clamp(cursor.Progress, 0f, edge.Length);
            int nextEdgeId = ChooseNextEdge(cursor.EdgeId);
            Edge nextEdge = GetEdge(nextEdgeId);
            if (nextEdge != null &&
                nextEdge.Length > 0.001f &&
                distanceToEnd <= incomingWindow)
            {
                float outgoingWindow = Mathf.Min(turnRadius, nextEdge.Length * 0.45f);
                float cornerWindow = Mathf.Min(incomingWindow, outgoingWindow);
                if (cornerWindow > 0.001f)
                {
                    float t = Mathf.InverseLerp(cornerWindow, 0f, distanceToEnd) * 0.5f;
                    EvaluateCornerBezier(edge, nextEdge, cornerWindow, t, out position, out tangent);
                    return true;
                }
            }

            if (cursor.Progress <= incomingWindow &&
                TryFindPreferredPreviousEdge(cursor.EdgeId, out Edge previousEdge) &&
                previousEdge.Length > 0.001f)
            {
                float previousWindow = Mathf.Min(turnRadius, previousEdge.Length * 0.45f);
                float cornerWindow = Mathf.Min(previousWindow, incomingWindow);
                if (cornerWindow > 0.001f)
                {
                    float t = 0.5f + Mathf.Clamp01(cursor.Progress / cornerWindow) * 0.5f;
                    EvaluateCornerBezier(previousEdge, edge, cornerWindow, t, out position, out tangent);
                    return true;
                }
            }

            return true;
        }

        private void EvaluateCornerBezier(Edge incomingEdge, Edge outgoingEdge, float radius, float t, out Vector3 position, out Vector3 tangent)
        {
            Vector3 incomingForward = GetForward(incomingEdge.Id);
            Vector3 outgoingForward = GetForward(outgoingEdge.Id);
            Vector3 corner = incomingEdge.End;
            Vector3 p0 = corner - incomingForward * radius;
            Vector3 p1 = corner;
            Vector3 p2 = corner + outgoingForward * radius;
            t = Mathf.Clamp01(t);
            float oneMinusT = 1f - t;
            position = oneMinusT * oneMinusT * p0 + 2f * oneMinusT * t * p1 + t * t * p2;
            tangent = 2f * oneMinusT * (p1 - p0) + 2f * t * (p2 - p1);
            tangent.y = 0f;
            if (tangent.sqrMagnitude <= 0.0001f)
            {
                tangent = outgoingForward.sqrMagnitude > 0.0001f ? outgoingForward : incomingForward;
            }
            else
            {
                tangent.Normalize();
            }
        }

        private bool TryFindPreferredPreviousEdge(int currentEdgeId, out Edge previousEdge)
        {
            previousEdge = null;
            int edgeCount = edges != null ? edges.Count : 0;
            if (preferredPreviousEdgeIds == null || preferredPreviousEdgeIds.Count != edgeCount)
            {
                RebuildPreferredPreviousEdges();
            }

            int previousEdgeId = preferredPreviousEdgeIds != null &&
                currentEdgeId >= 0 &&
                currentEdgeId < preferredPreviousEdgeIds.Count
                    ? preferredPreviousEdgeIds[currentEdgeId]
                    : -1;
            previousEdge = GetEdge(previousEdgeId);
            return previousEdge != null;
        }

        private void RebuildPreferredPreviousEdges()
        {
            if (preferredPreviousEdgeIds == null)
            {
                preferredPreviousEdgeIds = new List<int>();
            }

            preferredPreviousEdgeIds.Clear();
            int edgeCount = edges != null ? edges.Count : 0;
            var bestScores = new List<float>(edgeCount);
            for (int i = 0; i < edgeCount; i++)
            {
                preferredPreviousEdgeIds.Add(-1);
                bestScores.Add(float.MinValue);
            }

            if (preferredNextEdgeIds == null || preferredNextEdgeIds.Count != edgeCount)
            {
                RebuildPreferredNextEdges();
            }

            for (int i = 0; i < edgeCount; i++)
            {
                Edge candidate = GetEdge(i);
                int nextEdgeId = preferredNextEdgeIds != null && i < preferredNextEdgeIds.Count ? preferredNextEdgeIds[i] : -1;
                Edge next = GetEdge(nextEdgeId);
                if (candidate == null || next == null)
                {
                    continue;
                }

                float score = Vector3.Dot(GetForward(candidate.Id), GetForward(next.Id));
                if (score > bestScores[next.Id])
                {
                    bestScores[next.Id] = score;
                    preferredPreviousEdgeIds[next.Id] = candidate.Id;
                }
            }
        }

        public float GetEdgeLength(int edgeId)
        {
            Edge edge = GetEdge(edgeId);
            return edge != null ? Mathf.Max(0f, edge.Length) : 0f;
        }

        public bool TryAdvance(ref CocoonRouteCursor cursor, float distance, out bool hitDeadEnd)
        {
            hitDeadEnd = false;
            if (!cursor.IsValid || distance <= 0f)
            {
                return cursor.IsValid;
            }

            int guard = Mathf.Max(8, EdgeCount + 4);
            float remaining = distance;
            while (remaining > 0f && guard-- > 0)
            {
                Edge edge = GetEdge(cursor.EdgeId);
                if (edge == null || edge.Length <= 0.001f)
                {
                    hitDeadEnd = true;
                    return false;
                }

                float distanceToEnd = Mathf.Max(0f, edge.Length - cursor.Progress);
                if (remaining <= distanceToEnd)
                {
                    cursor.Progress += remaining;
                    return true;
                }

                remaining -= distanceToEnd;
                int nextEdgeId = ChooseNextEdge(cursor.EdgeId);
                if (nextEdgeId < 0)
                {
                    cursor.Progress = edge.Length;
                    hitDeadEnd = true;
                    return false;
                }

                cursor.EdgeId = nextEdgeId;
                cursor.Progress = 0f;
            }

            return cursor.IsValid;
        }

        public bool TryGetForwardDistance(CocoonRouteCursor from, CocoonRouteCursor to, out float distance, int maxEdges = 128)
        {
            distance = float.MaxValue;
            if (!from.IsValid || !to.IsValid)
            {
                return false;
            }

            Edge startEdge = GetEdge(from.EdgeId);
            Edge targetEdge = GetEdge(to.EdgeId);
            if (startEdge == null || targetEdge == null)
            {
                return false;
            }

            if (from.EdgeId == to.EdgeId && to.Progress >= from.Progress)
            {
                distance = to.Progress - from.Progress;
                return true;
            }

            float total = Mathf.Max(0f, startEdge.Length - from.Progress);
            int currentEdgeId = from.EdgeId;
            var visited = new HashSet<int> { currentEdgeId };
            int guard = Mathf.Max(1, maxEdges);
            while (guard-- > 0)
            {
                int nextEdgeId = ChooseNextEdge(currentEdgeId);
                if (nextEdgeId < 0)
                {
                    return false;
                }

                currentEdgeId = nextEdgeId;
                Edge current = GetEdge(currentEdgeId);
                if (current == null)
                {
                    return false;
                }

                if (currentEdgeId == to.EdgeId)
                {
                    total += Mathf.Clamp(to.Progress, 0f, current.Length);
                    distance = total;
                    return true;
                }

                if (!visited.Add(currentEdgeId))
                {
                    return false;
                }

                total += current.Length;
            }

            return false;
        }

        public bool TryGetForwardDistanceToBay(CocoonRouteCursor from, int bayIndex, out float distance)
        {
            distance = float.MaxValue;
            BayBinding bay = GetBay(bayIndex);
            if (bay == null || bay.EdgeId < 0)
            {
                return false;
            }

            var target = new CocoonRouteCursor
            {
                EdgeId = bay.EdgeId,
                Progress = bay.Progress
            };
            return TryGetForwardDistance(from, target, out distance, Mathf.Max(16, EdgeCount + 8));
        }

        public bool IsBayOnCurrentOrNextEdge(CocoonRouteCursor from, int bayIndex, out float forwardDistance)
        {
            forwardDistance = float.MaxValue;
            BayBinding bay = GetBay(bayIndex);
            Edge fromEdge = GetEdge(from.EdgeId);
            if (bay == null || fromEdge == null)
            {
                return false;
            }

            if (bay.EdgeId == from.EdgeId && bay.Progress >= from.Progress)
            {
                forwardDistance = bay.Progress - from.Progress;
                return true;
            }

            int nextEdgeId = ChooseNextEdge(from.EdgeId);
            if (nextEdgeId >= 0 && bay.EdgeId == nextEdgeId)
            {
                Edge nextEdge = GetEdge(nextEdgeId);
                forwardDistance = Mathf.Max(0f, fromEdge.Length - from.Progress) + Mathf.Clamp(bay.Progress, 0f, nextEdge != null ? nextEdge.Length : bay.Progress);
                return true;
            }

            return false;
        }

        private void NormalizeEdgeIds()
        {
            if (edges == null)
            {
                return;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i] != null)
                {
                    edges[i].Id = i;
                    edges[i].Length = Vector3.Distance(Flatten(edges[i].Start), Flatten(edges[i].End));
                }
            }
        }

        private void RebuildConnectivity()
        {
            outgoingEdgesByNode.Clear();
            if (edges == null)
            {
                return;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge == null)
                {
                    continue;
                }

                if (!outgoingEdgesByNode.TryGetValue(edge.FromNode, out List<int> outgoing))
                {
                    outgoing = new List<int>();
                    outgoingEdgesByNode.Add(edge.FromNode, outgoing);
                }

                outgoing.Add(edge.Id);
            }

            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge == null)
                {
                    continue;
                }

                if (outgoingEdgesByNode.TryGetValue(edge.ToNode, out List<int> outgoing))
                {
                    edge.NextEdgeIds = outgoing.ToArray();
                }
                else
                {
                    edge.NextEdgeIds = new int[0];
                }
            }
        }

        private int ChooseNextEdge(int currentEdgeId)
        {
            int edgeCount = edges != null ? edges.Count : 0;
            if (preferredNextEdgeIds == null || preferredNextEdgeIds.Count != edgeCount)
            {
                RebuildPreferredNextEdges();
            }

            if (preferredNextEdgeIds != null &&
                currentEdgeId >= 0 &&
                currentEdgeId < preferredNextEdgeIds.Count &&
                preferredNextEdgeIds[currentEdgeId] >= 0)
            {
                return preferredNextEdgeIds[currentEdgeId];
            }

            return CalculatePreferredNextEdge(currentEdgeId);
        }

        private void RebuildPreferredNextEdges()
        {
            if (preferredNextEdgeIds == null)
            {
                preferredNextEdgeIds = new List<int>();
            }

            preferredNextEdgeIds.Clear();
            int count = edges != null ? edges.Count : 0;
            for (int i = 0; i < count; i++)
            {
                preferredNextEdgeIds.Add(CalculatePreferredNextEdge(i));
            }
        }

        private int CalculatePreferredNextEdge(int currentEdgeId)
        {
            Edge current = GetEdge(currentEdgeId);
            if (current == null || current.NextEdgeIds == null || current.NextEdgeIds.Length == 0)
            {
                return -1;
            }

            Vector3 currentForward = GetForward(currentEdgeId);
            int reverseEdge = FindEdge(current.ToNode, current.FromNode);
            if (current.NextEdgeIds.Length == 1 && current.NextEdgeIds[0] == reverseEdge)
            {
                return -1;
            }

            int bestEdge = -1;
            float bestScore = float.MinValue;
            bool hasLoopingCandidate = false;
            for (int i = 0; i < current.NextEdgeIds.Length; i++)
            {
                int candidateId = current.NextEdgeIds[i];
                if (candidateId == reverseEdge && current.NextEdgeIds.Length > 1)
                {
                    continue;
                }

                if (CanReachForwardCycle(candidateId, Mathf.Max(8, EdgeCount + 4)))
                {
                    hasLoopingCandidate = true;
                    break;
                }
            }

            for (int i = 0; i < current.NextEdgeIds.Length; i++)
            {
                int candidateId = current.NextEdgeIds[i];
                if (candidateId == reverseEdge && current.NextEdgeIds.Length > 1)
                {
                    continue;
                }

                bool reachesLoop = CanReachForwardCycle(candidateId, Mathf.Max(8, EdgeCount + 4));
                if (hasLoopingCandidate && !reachesLoop)
                {
                    continue;
                }

                Vector3 nextForward = GetForward(candidateId);
                float straightScore = Vector3.Dot(currentForward, nextForward);
                float rightBias = Vector3.Dot(Vector3.Cross(Vector3.up, currentForward), nextForward) * 0.05f;
                float loopScore = reachesLoop ? 1.5f : 0f;
                float score = loopScore + straightScore + rightBias - candidateId * 0.00001f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestEdge = candidateId;
                }
            }

            return bestEdge >= 0 ? bestEdge : current.NextEdgeIds[0];
        }

        private bool CanReachForwardCycle(int startEdgeId, int depthLimit)
        {
            if (GetEdge(startEdgeId) == null)
            {
                return false;
            }

            var visiting = new HashSet<int>();
            var provenDead = new HashSet<int>();
            return CanReachForwardCycleDfs(startEdgeId, depthLimit, visiting, provenDead);
        }

        private bool CanReachForwardCycleDfs(int edgeId, int remainingDepth, HashSet<int> visiting, HashSet<int> provenDead)
        {
            if (remainingDepth <= 0)
            {
                return true;
            }

            if (provenDead.Contains(edgeId))
            {
                return false;
            }

            if (!visiting.Add(edgeId))
            {
                return true;
            }

            Edge edge = GetEdge(edgeId);
            if (edge == null || edge.NextEdgeIds == null || edge.NextEdgeIds.Length == 0)
            {
                visiting.Remove(edgeId);
                provenDead.Add(edgeId);
                return false;
            }

            int reverseEdge = FindEdge(edge.ToNode, edge.FromNode);
            for (int i = 0; i < edge.NextEdgeIds.Length; i++)
            {
                int next = edge.NextEdgeIds[i];
                if (next == reverseEdge)
                {
                    continue;
                }

                if (CanReachForwardCycleDfs(next, remainingDepth - 1, visiting, provenDead))
                {
                    visiting.Remove(edgeId);
                    return true;
                }
            }

            visiting.Remove(edgeId);
            provenDead.Add(edgeId);
            return false;
        }


        private int FindEdge(int fromNode, int toNode)
        {
            if (edges == null)
            {
                return -1;
            }

            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge != null && edge.FromNode == fromNode && edge.ToNode == toNode)
                {
                    return edge.Id;
                }
            }

            return -1;
        }

        private void BindPickupStops(Transform[] pickupStops)
        {
            bays = new List<BayBinding>();
            if (pickupStops == null)
            {
                return;
            }

            for (int i = 0; i < pickupStops.Length; i++)
            {
                Transform stop = pickupStops[i];
                if (stop == null)
                {
                    continue;
                }

                if (!TryProjectToGraph(stop.position, out CocoonRouteCursor cursor))
                {
                    continue;
                }

                Vector3 lanePosition = GetPosition(cursor);
                Vector3 forward = GetForward(cursor.EdgeId);
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                float side = Vector3.Dot(Flatten(stop.position - lanePosition), right) >= 0f ? 1f : -1f;
                bays.Add(new BayBinding
                {
                    Stop = stop,
                    EdgeId = cursor.EdgeId,
                    Progress = cursor.Progress,
                    Position = stop.position,
                    Forward = forward,
                    SideSign = side
                });
            }
        }

        private int CountConnectedComponents()
        {
            if (nodes == null || nodes.Count == 0)
            {
                return 0;
            }

            var nodeIds = new HashSet<int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                {
                    nodeIds.Add(nodes[i].Id);
                }
            }

            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge == null)
                {
                    continue;
                }

                if (!adjacency.TryGetValue(edge.FromNode, out List<int> fromList))
                {
                    fromList = new List<int>();
                    adjacency.Add(edge.FromNode, fromList);
                }

                if (!adjacency.TryGetValue(edge.ToNode, out List<int> toList))
                {
                    toList = new List<int>();
                    adjacency.Add(edge.ToNode, toList);
                }

                fromList.Add(edge.ToNode);
                toList.Add(edge.FromNode);
            }

            int components = 0;
            var visited = new HashSet<int>();
            foreach (int nodeId in nodeIds)
            {
                if (visited.Contains(nodeId))
                {
                    continue;
                }

                components++;
                var queue = new Queue<int>();
                queue.Enqueue(nodeId);
                visited.Add(nodeId);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    if (!adjacency.TryGetValue(current, out List<int> neighbors))
                    {
                        continue;
                    }

                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        if (visited.Add(neighbors[i]))
                        {
                            queue.Enqueue(neighbors[i]);
                        }
                    }
                }
            }

            return components;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
