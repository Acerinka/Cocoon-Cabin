using System;
using System.Collections.Generic;
using System.Linq;
using CocoonPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CocoonPrototype.Editor
{
    public static class CocoonJapaneseRoadNetworkBuilder
    {
        private const string ScenePath = "Assets/Scenes/Cocoon_OnboardingVR.unity";
        private const string RoadmapRootName = "ROADMAP";
        private const string LooseRoadTileName = "jcRoad22mC_b_1545 (1)";
        private const string NetworkRootName = "ROADMAP Two-Way Traffic Network";
        private const string PickupRootName = "ROADMAP Pickup Bays";
        private const string LegacyNetworkRootName = "Road Tile Traffic Network";
        private const string LegacyPickupRootName = "Road Tile Pickup Markers";
        private const string EndRoadTag = "END";
        private const int MinimumRoadTiles = 4;

        private enum RoadAxis
        {
            Horizontal,
            Vertical,
            Intersection
        }

        private sealed class RoadTile
        {
            public Transform Transform;
            public Bounds Bounds;
            public Vector3 Center;
            public int Row;
            public int Column;
            public RoadAxis Axis;
            public bool IsIntersection;
        }

        private sealed class StreetLine
        {
            public bool Horizontal;
            public List<RoadTile> Tiles;
            public float Width;
        }

        private sealed class PickupCandidate
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Priority;
        }

        private struct RoadBoundary
        {
            public float MinX;
            public float MaxX;
            public float MinZ;
            public float MaxZ;

            public bool Contains(Vector3 point)
            {
                return point.x >= MinX && point.x <= MaxX && point.z >= MinZ && point.z <= MaxZ;
            }
        }

        [MenuItem("Cocoon/Rebuild ROADMAP Two-Way Traffic")]
        public static void RebuildOpenSceneMenu()
        {
            if (ApplyToCurrentSceneIfAvailable(false))
            {
                Debug.Log("ROADMAP two-way traffic network rebuilt in the open scene. Inspect it, then save the scene when it looks right.");
            }
        }

        [MenuItem("Cocoon/Rebuild ROADMAP Two-Way Traffic And Save")]
        public static void RebuildOpenSceneAndSaveMenu()
        {
            if (ApplyToCurrentSceneIfAvailable(true))
            {
                Debug.Log("ROADMAP two-way traffic network rebuilt and saved.");
            }
        }

        [MenuItem("Cocoon/Rebuild Saved ROADMAP Two-Way Traffic And Validate")]
        public static void RebuildSavedSceneAndValidate()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!ApplyToCurrentSceneIfAvailable(true) || !ValidateCurrentScene())
            {
                throw new InvalidOperationException("ROADMAP traffic network validation failed. See the Unity log for details.");
            }

            AssetDatabase.SaveAssets();
        }

        public static bool ApplyToCurrentSceneIfAvailable(bool saveScene)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            Transform roadmap = FindRoadmapRoot(scene);
            if (roadmap == null)
            {
                Debug.LogWarning("ROADMAP traffic rebuild skipped: a top-level parent named ROADMAP was not found in the open scene.");
                return false;
            }

            MoveLooseRoadTileIntoRoadmap(scene, roadmap);

            List<RoadTile> roadTiles = CollectRoadTiles(roadmap);
            roadTiles = FilterRoadTilesToStatisticalBoundary(roadTiles);
            if (roadTiles.Count < MinimumRoadTiles)
            {
                Debug.LogWarning("ROADMAP traffic rebuild skipped: only " + roadTiles.Count + " road tiles were found under ROADMAP.");
                return false;
            }

            SnapRoadTileClusters(roadTiles);
            Dictionary<int, List<int>> adjacency = BuildAdjacency(roadTiles);
            List<int> largestComponent = FindLargestConnectedComponent(adjacency);
            if (largestComponent.Count > 0 && largestComponent.Count < roadTiles.Count)
            {
                roadTiles = largestComponent.Select(index => roadTiles[index]).ToList();
                SnapRoadTileClusters(roadTiles);
                adjacency = BuildAdjacency(roadTiles);
                Debug.LogWarning("ROADMAP traffic using largest connected road component: " + roadTiles.Count + " connected road tiles.");
            }

            List<int> route = BuildPerimeterRoadLoop(roadTiles, adjacency);
            if (route.Count < 4)
            {
                route = BuildDebugTraversalRoute(roadTiles, adjacency);
                Debug.LogWarning("ROADMAP did not find a legal perimeter loop. Runtime traffic will use the ROADMAP graph; legacy lane path is debug/fallback only with " + route.Count + " waypoint tile references.");
            }

            if (route.Count < 2)
            {
                Debug.LogWarning("ROADMAP traffic rebuild skipped: graph has too few connected drivable road tiles.");
                return false;
            }

            Transform trafficRoot = FindOrCreateRoot(scene, "03_Traffic");
            Transform pickupRoot = FindOrCreateRoot(scene, "05_Pickup_Guidance");
            Transform taxiRoot = FindOrCreateRoot(scene, "04_Cocoon_Taxi");
            NormalizeRootTransformPreservingChildren(trafficRoot);
            NormalizeRootTransformPreservingChildren(taxiRoot);
            PreserveTrafficVehiclesFromGeneratedRoots(scene, trafficRoot);
            RemoveChildIfPresent(trafficRoot, NetworkRootName);
            RemoveChildIfPresent(trafficRoot, LegacyNetworkRootName);
            RemoveChildIfPresent(pickupRoot, LegacyPickupRootName);

            var networkObject = new GameObject(NetworkRootName);
            networkObject.transform.SetParent(trafficRoot, false);
            Transform networkRoot = networkObject.transform;

            float experienceScale = ResolveRoadmapExperienceScale(roadmap);
            float laneY = EstimateLaneY(roadTiles, experienceScale);
            VehicleDimensions taxiDimensions = EstimateTaxiDimensions(scene, experienceScale);
            float roadWidth = EstimateMedianMinorExtent(roadTiles);
            float laneOffset = EstimateLaneOffset(roadWidth, taxiDimensions.Width);
            RoadBoundary boundary = CalculateRoadBoundary(roadTiles, roadWidth);
            Debug.Log("ROADMAP traffic source: " + roadTiles.Count + " road tile(s), bounds x[" +
                      boundary.MinX.ToString("0.00") + ", " + boundary.MaxX.ToString("0.00") + "] z[" +
                      boundary.MinZ.ToString("0.00") + ", " + boundary.MaxZ.ToString("0.00") + "].");

            CocoonTrafficLanePath forwardLane = BuildLanePath(networkRoot, "ROADMAP Forward Lane", roadTiles, adjacency, route, laneY, laneOffset, boundary, experienceScale);
            List<int> reverseRoute = new List<int>(route);
            reverseRoute.Reverse();
            CocoonTrafficLanePath reverseLane = BuildLanePath(networkRoot, "ROADMAP Reverse Lane", roadTiles, adjacency, reverseRoute, laneY, laneOffset, boundary, experienceScale);
            CocoonTrafficLanePath[] builtLanes = { forwardLane, reverseLane };
            int offRoadWaypoints = CountLaneWaypointsOffRoad(builtLanes, roadTiles);
            int offRoadSegments = CountLaneSegmentsOffRoad(builtLanes, roadTiles);
            if (offRoadWaypoints > 0 || offRoadSegments > 0)
            {
                Debug.LogError("ROADMAP traffic rebuild failed: generated " + offRoadWaypoints +
                               " off-road waypoint(s) and " + offRoadSegments +
                               " off-road route segment(s). The route must stay on actual ROADMAP road tiles.");
                UnityEngine.Object.DestroyImmediate(networkObject);
                return false;
            }

            Transform[] pickupStops = BuildPickupStops(pickupRoot, roadTiles, forwardLane, laneY, roadWidth, laneOffset, taxiDimensions, boundary, experienceScale, out int[] pickupIndices);
            CocoonRoadGraph roadGraph = BuildRoadGraph(networkRoot, roadTiles, adjacency, pickupStops, laneY, laneOffset, boundary, experienceScale);
            RepositionTrafficVehicles(scene, trafficRoot, forwardLane, reverseLane, roadGraph, experienceScale);
            ReconfigureTaxi(scene, forwardLane, reverseLane, roadGraph, pickupStops, pickupIndices);
            ReconfigureXrStart(scene, forwardLane, pickupStops, roadTiles);

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("ROADMAP traffic rebuilt: " + roadTiles.Count + " road tiles, " + forwardLane.Count +
                      " forward waypoints, " + reverseLane.Count + " reverse waypoints, graphEdges=" +
                      (roadGraph != null ? roadGraph.EdgeCount.ToString() : "0") + ", graphComponents=" +
                      (roadGraph != null ? roadGraph.ConnectedComponentCount.ToString() : "0") + ", " +
                      pickupStops.Length + " pickup bays, " + CountTrafficVehicles(scene) + " traffic vehicles.");
            return true;
        }

        public static bool ValidateCurrentScene()
        {
            var errors = new List<string>();
            Scene scene = SceneManager.GetActiveScene();
            Transform roadmap = FindRoadmapRoot(scene);
            if (roadmap == null)
            {
                errors.Add("ROADMAP is missing.");
            }

            List<RoadTile> roadTiles = roadmap != null ? CollectRoadTiles(roadmap) : new List<RoadTile>();
            int roadTileCount = roadTiles.Count;
            if (roadTileCount < MinimumRoadTiles)
            {
                errors.Add("Expected at least " + MinimumRoadTiles + " ROADMAP road tiles, found " + roadTileCount + ".");
            }

            Transform networkRoot = FindTransformDeep(scene, NetworkRootName);
            if (networkRoot == null)
            {
                errors.Add(NetworkRootName + " is missing.");
            }

            CocoonTrafficLanePath[] lanes = networkRoot != null ? networkRoot.GetComponentsInChildren<CocoonTrafficLanePath>(true) : new CocoonTrafficLanePath[0];
            if (lanes.Length < 2 || lanes.Any(lane => lane == null || lane.Count < 4))
            {
                errors.Add("Expected two ROADMAP lane paths with at least 4 waypoints each.");
            }

            int controlPointCount = networkRoot != null ? networkRoot.GetComponentsInChildren<CocoonTrafficControlPoint>(true).Length : 0;
            if (controlPointCount < 2)
            {
                errors.Add("Expected at least 2 shared traffic control points, found " + controlPointCount + ".");
            }

            Transform pickupRoot = FindTransformDeep(scene, PickupRootName);
            int pickupCount = CountTransformsNamed(pickupRoot, "Pickup Bay Stop");
            if (pickupCount < 2)
            {
                errors.Add("Expected at least 2 pickup stops, found " + pickupCount + ".");
            }

            int vehicleCount = CountTrafficVehicles(scene);
            if (vehicleCount < 1)
            {
                errors.Add("Expected at least one traffic vehicle.");
            }

            if (roadTiles.Count >= MinimumRoadTiles)
            {
                RoadBoundary boundary = CalculateRoadBoundary(roadTiles, EstimateMedianMinorExtent(roadTiles));
                int outOfBoundsWaypoints = CountLaneWaypointsOutside(lanes, boundary);
                if (outOfBoundsWaypoints > 0)
                {
                    errors.Add(outOfBoundsWaypoints + " ROADMAP lane waypoint(s) are outside the active ROADMAP road bounds.");
                }

                int outOfBoundsVehicles = CountTrafficVehiclesOutside(scene, boundary);
                if (outOfBoundsVehicles > 0)
                {
                    errors.Add(outOfBoundsVehicles + " traffic vehicle(s) are outside the active ROADMAP road bounds.");
                }

                int offRoadWaypoints = CountLaneWaypointsOffRoad(lanes, roadTiles);
                if (offRoadWaypoints > 0)
                {
                    errors.Add(offRoadWaypoints + " ROADMAP lane waypoint(s) are inside the ROADMAP rectangle but not on any road tile.");
                }

                int offRoadSegments = CountLaneSegmentsOffRoad(lanes, roadTiles);
                if (offRoadSegments > 0)
                {
                    errors.Add(offRoadSegments + " ROADMAP lane segment(s) cross non-road space between valid road tiles.");
                }

                int offRoadVehicles = CountTrafficVehiclesOffRoad(scene, roadTiles);
                if (offRoadVehicles > 0)
                {
                    errors.Add(offRoadVehicles + " traffic vehicle(s) are inside the ROADMAP rectangle but not on any road tile.");
                }
            }

            if (errors.Count > 0)
            {
                for (int i = 0; i < errors.Count; i++)
                {
                    Debug.LogError("ROADMAP traffic validation failed: " + errors[i]);
                }

                return false;
            }

            Debug.Log("ROADMAP traffic validation passed: " + roadTileCount + " road tiles, " +
                      lanes.Length + " lanes, " + controlPointCount + " controls, " +
                      pickupCount + " pickup stops, " + vehicleCount + " traffic vehicles.");
            return true;
        }

        private static void MoveLooseRoadTileIntoRoadmap(Scene scene, Transform roadmap)
        {
            Transform looseTile = FindTransformDeep(scene, LooseRoadTileName);
            if (looseTile == null || looseTile == roadmap || IsChildOf(looseTile, roadmap))
            {
                return;
            }

            looseTile.SetParent(roadmap, true);
            Debug.Log("Moved loose road tile " + LooseRoadTileName + " under ROADMAP without changing its world transform.", looseTile);
        }

        private static List<RoadTile> CollectRoadTiles(Transform roadmap)
        {
            var tiles = new List<RoadTile>();
            List<Transform> roadRoots = CollectRoadmapRoadRoots(roadmap);
            for (int i = 0; i < roadRoots.Count; i++)
            {
                Transform roadRoot = roadRoots[i];
                if (roadRoot == null || !TryGetRoadGroupBounds(roadRoot, out Bounds bounds))
                {
                    continue;
                }

                RoadAxis axis = ResolveRoadAxis(roadRoot.name, bounds);
                tiles.Add(new RoadTile
                {
                    Transform = roadRoot,
                    Bounds = bounds,
                    Center = new Vector3(bounds.center.x, 0f, bounds.center.z),
                    Axis = axis,
                    IsIntersection = axis == RoadAxis.Intersection
                });
            }

            Debug.Log("ROADMAP road roots read: " + roadRoots.Count + ", drivable road surfaces: " + tiles.Count + ".");
            return tiles;
        }

        private static List<Transform> CollectRoadmapRoadRoots(Transform roadmap)
        {
            var roadRoots = new List<Transform>();
            int skippedEndRoadRoots = 0;
            for (int i = 0; i < roadmap.childCount; i++)
            {
                Transform child = roadmap.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy && IsRoadTileName(child.name))
                {
                    if (IsEndTaggedRoadRoot(child))
                    {
                        skippedEndRoadRoots++;
                        continue;
                    }

                    roadRoots.Add(child);
                }
            }

            if (roadRoots.Count < MinimumRoadTiles)
            {
                Debug.LogWarning("ROADMAP has only " + roadRoots.Count +
                                 " direct road root(s). Falling back to ROADMAP descendants only.");
                Transform[] descendants = roadmap.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < descendants.Length; i++)
                {
                    Transform transform = descendants[i];
                    if (transform != null && transform != roadmap && transform.gameObject.activeInHierarchy && IsRoadTileName(transform.name))
                    {
                        if (roadRoots.Contains(transform))
                        {
                            continue;
                        }

                        if (IsEndTaggedRoadRoot(transform))
                        {
                            skippedEndRoadRoots++;
                            continue;
                        }

                        roadRoots.Add(transform);
                    }
                }
            }

            if (skippedEndRoadRoots > 0)
            {
                Debug.Log("ROADMAP END-tagged road roots excluded from drivable traffic graph: " + skippedEndRoadRoots + ".");
            }

            return roadRoots;
        }

        private static bool IsEndTaggedRoadRoot(Transform roadRoot)
        {
            if (roadRoot == null)
            {
                return false;
            }

            if (HasEndRoadTag(roadRoot))
            {
                return true;
            }

            Transform parent = roadRoot.parent;
            while (parent != null)
            {
                if (HasEndRoadTag(parent))
                {
                    return true;
                }

                parent = parent.parent;
            }

            Transform[] children = roadRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && HasEndRoadTag(children[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEndRoadTag(Transform candidate)
        {
            return candidate != null && candidate.gameObject != null && candidate.gameObject.tag == EndRoadTag;
        }

        private static bool IsRoadTileName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.StartsWith("jcRoad", StringComparison.OrdinalIgnoreCase);
        }

        private static RoadAxis ResolveRoadAxis(string name, Bounds bounds)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("cross") || lower.Contains("roadt"))
            {
                return RoadAxis.Intersection;
            }

            Vector3 size = bounds.size;
            if (size.x > size.z * 1.22f)
            {
                return RoadAxis.Horizontal;
            }

            if (size.z > size.x * 1.22f)
            {
                return RoadAxis.Vertical;
            }

            return RoadAxis.Intersection;
        }

        private static List<RoadTile> FilterRoadTilesToStatisticalBoundary(List<RoadTile> tiles)
        {
            if (tiles.Count < 8)
            {
                return tiles;
            }

            List<float> xs = tiles.Select(tile => tile.Center.x).OrderBy(value => value).ToList();
            List<float> zs = tiles.Select(tile => tile.Center.z).OrderBy(value => value).ToList();
            float typicalSpacing = EstimateTypicalSpacing(tiles);
            float pad = Mathf.Max(typicalSpacing * 1.4f, EstimateMedianMajorExtent(tiles) * 1.2f);
            float minX = Percentile(xs, 0.03f) - pad;
            float maxX = Percentile(xs, 0.97f) + pad;
            float minZ = Percentile(zs, 0.03f) - pad;
            float maxZ = Percentile(zs, 0.97f) + pad;
            List<RoadTile> filtered = tiles
                .Where(tile => tile.Center.x >= minX && tile.Center.x <= maxX && tile.Center.z >= minZ && tile.Center.z <= maxZ)
                .ToList();

            if (filtered.Count < MinimumRoadTiles)
            {
                return tiles;
            }

            int removed = tiles.Count - filtered.Count;
            if (removed > 0)
            {
                Debug.LogWarning("ROADMAP boundary filter removed " + removed + " outlier road tile(s) before lane generation.");
            }

            return filtered;
        }

        private static RoadBoundary CalculateRoadBoundary(List<RoadTile> tiles, float roadWidth)
        {
            var boundary = new RoadBoundary
            {
                MinX = float.MaxValue,
                MaxX = float.MinValue,
                MinZ = float.MaxValue,
                MaxZ = float.MinValue
            };

            for (int i = 0; i < tiles.Count; i++)
            {
                Bounds bounds = tiles[i].Bounds;
                boundary.MinX = Mathf.Min(boundary.MinX, bounds.min.x);
                boundary.MaxX = Mathf.Max(boundary.MaxX, bounds.max.x);
                boundary.MinZ = Mathf.Min(boundary.MinZ, bounds.min.z);
                boundary.MaxZ = Mathf.Max(boundary.MaxZ, bounds.max.z);
            }

            float pad = Mathf.Max(0.2f, roadWidth * 0.08f);
            boundary.MinX -= pad;
            boundary.MaxX += pad;
            boundary.MinZ -= pad;
            boundary.MaxZ += pad;
            return boundary;
        }

        private static void SnapRoadTileClusters(List<RoadTile> tiles)
        {
            float tolerance = Mathf.Max(0.2f, EstimateMedianMinorExtent(tiles) * 0.62f);
            List<float> xClusters = BuildClusters(tiles.Select(tile => tile.Center.x), tolerance);
            List<float> zClusters = BuildClusters(tiles.Select(tile => tile.Center.z), tolerance);
            for (int i = 0; i < tiles.Count; i++)
            {
                RoadTile tile = tiles[i];
                tile.Column = FindNearestCluster(tile.Center.x, xClusters);
                tile.Row = FindNearestCluster(tile.Center.z, zClusters);
                tile.Center = new Vector3(xClusters[tile.Column], 0f, zClusters[tile.Row]);
            }
        }

        private static Dictionary<int, List<int>> BuildAdjacency(List<RoadTile> tiles)
        {
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < tiles.Count; i++)
            {
                adjacency[i] = new List<int>();
            }

            float roadWidth = EstimateMedianMinorExtent(tiles);
            foreach (IGrouping<int, RoadTile> row in tiles.GroupBy(tile => tile.Row))
            {
                RoadTile[] ordered = row
                    .Where(tile => tile.Axis == RoadAxis.Horizontal || tile.Axis == RoadAxis.Intersection)
                    .OrderBy(tile => tile.Center.x)
                    .ToArray();
                ConnectConsecutiveTiles(tiles, adjacency, ordered, roadWidth, true);
            }

            foreach (IGrouping<int, RoadTile> column in tiles.GroupBy(tile => tile.Column))
            {
                RoadTile[] ordered = column
                    .Where(tile => tile.Axis == RoadAxis.Vertical || tile.Axis == RoadAxis.Intersection)
                    .OrderBy(tile => tile.Center.z)
                    .ToArray();
                ConnectConsecutiveTiles(tiles, adjacency, ordered, roadWidth, false);
            }

            int edgeCount = adjacency.Values.Sum(neighbors => neighbors.Count) / 2;
            Debug.Log("ROADMAP adjacency built from mesh-neighbor checks: " + edgeCount + " edge(s).");
            return adjacency;
        }

        private static void ConnectConsecutiveTiles(List<RoadTile> allTiles, Dictionary<int, List<int>> adjacency, RoadTile[] ordered, float roadWidth, bool horizontal)
        {
            for (int i = 1; i < ordered.Length; i++)
            {
                if (!CanConnectRoadTiles(ordered[i - 1], ordered[i], roadWidth, horizontal))
                {
                    continue;
                }

                int a = allTiles.IndexOf(ordered[i - 1]);
                int b = allTiles.IndexOf(ordered[i]);
                if (a < 0 || b < 0)
                {
                    continue;
                }

                AddEdge(adjacency, a, b);
                AddEdge(adjacency, b, a);
            }
        }

        private static bool CanConnectRoadTiles(RoadTile a, RoadTile b, float roadWidth, bool horizontal)
        {
            float lateralDelta = horizontal ? Mathf.Abs(a.Center.z - b.Center.z) : Mathf.Abs(a.Center.x - b.Center.x);
            float lateralTolerance = Mathf.Max(roadWidth * 0.42f, 0.12f);
            if (lateralDelta > lateralTolerance)
            {
                return false;
            }

            float gap = horizontal
                ? Mathf.Max(0f, Mathf.Max(a.Bounds.min.x, b.Bounds.min.x) - Mathf.Min(a.Bounds.max.x, b.Bounds.max.x))
                : Mathf.Max(0f, Mathf.Max(a.Bounds.min.z, b.Bounds.min.z) - Mathf.Min(a.Bounds.max.z, b.Bounds.max.z));
            float maxSurfaceGap = Mathf.Max(roadWidth * ((a.IsIntersection || b.IsIntersection) ? 1.15f : 0.55f), 0.16f);
            if (gap > maxSurfaceGap)
            {
                return false;
            }

            float overlap = horizontal
                ? Mathf.Min(a.Bounds.max.z, b.Bounds.max.z) - Mathf.Max(a.Bounds.min.z, b.Bounds.min.z)
                : Mathf.Min(a.Bounds.max.x, b.Bounds.max.x) - Mathf.Max(a.Bounds.min.x, b.Bounds.min.x);
            return overlap > Mathf.Max(roadWidth * 0.18f, 0.04f) || a.IsIntersection || b.IsIntersection;
        }

        private static List<int> FindLargestConnectedComponent(Dictionary<int, List<int>> adjacency)
        {
            var best = new List<int>();
            var visited = new HashSet<int>();
            foreach (int start in adjacency.Keys.OrderBy(index => index))
            {
                if (visited.Contains(start))
                {
                    continue;
                }

                var component = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                visited.Add(start);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    component.Add(current);
                    if (!adjacency.TryGetValue(current, out List<int> neighbors))
                    {
                        continue;
                    }

                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        int neighbor = neighbors[i];
                        if (visited.Contains(neighbor))
                        {
                            continue;
                        }

                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                if (component.Count > best.Count)
                {
                    best = component;
                }
            }

            best.Sort();
            return best;
        }

        private static List<int> BuildRoadTour(List<RoadTile> tiles, Dictionary<int, List<int>> adjacency)
        {
            int start = FindRouteStart(tiles);
            var route = new List<int>();
            var visited = new HashSet<int>();
            DepthFirstTour(start, -1, tiles, adjacency, visited, route);

            for (int i = 0; i < tiles.Count; i++)
            {
                if (visited.Contains(i))
                {
                    continue;
                }

                List<int> connector = FindShortestPath(adjacency, route.Count > 0 ? route[route.Count - 1] : i, i);
                if (connector.Count > 0)
                {
                    AppendPath(route, connector);
                }

                DepthFirstTour(i, -1, tiles, adjacency, visited, route);
            }

            if (route.Count > 2)
            {
                List<int> closing = FindShortestPath(adjacency, route[route.Count - 1], route[0]);
                if (closing.Count > 1)
                {
                    AppendPath(route, closing.Skip(1).ToList());
                }
            }

            RemoveConsecutiveDuplicates(route);
            return route;
        }

        private static int FindRouteStart(List<RoadTile> tiles)
        {
            int start = 0;
            float bestScore = float.MaxValue;
            for (int i = 0; i < tiles.Count; i++)
            {
                float score = tiles[i].Center.z * 10000f + tiles[i].Center.x;
                if (score < bestScore)
                {
                    bestScore = score;
                    start = i;
                }
            }

            return start;
        }

        private static List<int> BuildPerimeterRoadLoop(List<RoadTile> tiles, Dictionary<int, List<int>> adjacency)
        {
            if (tiles.Count == 0)
            {
                return new List<int>();
            }

            List<int> selectedLoop = BuildLargestValidatedRectangularLoop(tiles, adjacency);
            if (selectedLoop.Count >= 4)
            {
                Debug.Log("ROADMAP directed traffic loop selected without END repair shortcuts: " +
                    selectedLoop.Count + " waypoint tile references, length=" +
                    CalculateClosedRouteLength(tiles, selectedLoop).ToString("0.00") + "m.");
                return selectedLoop;
            }

            Debug.LogWarning("ROADMAP did not find a closed legal directed loop after END filtering. No shortest-path repair will be used across disconnected roads.");
            return new List<int>();
        }

        private static List<int> BuildDebugTraversalRoute(List<RoadTile> tiles, Dictionary<int, List<int>> adjacency)
        {
            var route = new List<int>();
            if (tiles == null || tiles.Count == 0 || adjacency == null || adjacency.Count == 0)
            {
                return route;
            }

            int start = adjacency
                .OrderByDescending(pair => pair.Value != null ? pair.Value.Count : 0)
                .ThenBy(pair => pair.Key)
                .Select(pair => pair.Key)
                .FirstOrDefault();
            DepthFirstTour(start, -1, tiles, adjacency, new HashSet<int>(), route);
            RemoveConsecutiveDuplicates(route);
            return route;
        }

        private static List<int> BuildLargestValidatedRectangularLoop(List<RoadTile> tiles, Dictionary<int, List<int>> adjacency)
        {
            var bestRoute = new List<int>();
            float bestLength = -1f;
            List<int> rows = tiles.Select(tile => tile.Row).Distinct().OrderBy(value => value).ToList();
            List<int> columns = tiles.Select(tile => tile.Column).Distinct().OrderBy(value => value).ToList();

            for (int bottomIndex = 0; bottomIndex < rows.Count - 1; bottomIndex++)
            {
                for (int topIndex = bottomIndex + 1; topIndex < rows.Count; topIndex++)
                {
                    for (int leftIndex = 0; leftIndex < columns.Count - 1; leftIndex++)
                    {
                        for (int rightIndex = leftIndex + 1; rightIndex < columns.Count; rightIndex++)
                        {
                            List<int> candidate = BuildRectangularRoute(
                                tiles,
                                rows[bottomIndex],
                                rows[topIndex],
                                columns[leftIndex],
                                columns[rightIndex]);
                            if (!IsClosedRouteValid(candidate, adjacency, tiles))
                            {
                                continue;
                            }

                            float length = CalculateClosedRouteLength(tiles, candidate);
                            if (length > bestLength)
                            {
                                bestLength = length;
                                bestRoute = candidate;
                            }
                        }
                    }
                }
            }

            return bestRoute;
        }

        private static List<int> BuildRectangularRoute(List<RoadTile> tiles, int bottomRow, int topRow, int leftColumn, int rightColumn)
        {
            var route = new List<int>();
            AppendOrderedTileIndices(
                route,
                tiles,
                tiles.Where(tile => tile.Row == bottomRow &&
                                    tile.Column >= leftColumn &&
                                    tile.Column <= rightColumn &&
                                    (tile.Axis == RoadAxis.Horizontal || tile.Axis == RoadAxis.Intersection))
                    .OrderBy(tile => tile.Center.x));
            AppendOrderedTileIndices(
                route,
                tiles,
                tiles.Where(tile => tile.Column == rightColumn &&
                                    tile.Row >= bottomRow &&
                                    tile.Row <= topRow &&
                                    (tile.Axis == RoadAxis.Vertical || tile.Axis == RoadAxis.Intersection))
                    .OrderBy(tile => tile.Center.z));
            AppendOrderedTileIndices(
                route,
                tiles,
                tiles.Where(tile => tile.Row == topRow &&
                                    tile.Column >= leftColumn &&
                                    tile.Column <= rightColumn &&
                                    (tile.Axis == RoadAxis.Horizontal || tile.Axis == RoadAxis.Intersection))
                    .OrderByDescending(tile => tile.Center.x));
            AppendOrderedTileIndices(
                route,
                tiles,
                tiles.Where(tile => tile.Column == leftColumn &&
                                    tile.Row >= bottomRow &&
                                    tile.Row <= topRow &&
                                    (tile.Axis == RoadAxis.Vertical || tile.Axis == RoadAxis.Intersection))
                    .OrderByDescending(tile => tile.Center.z));

            RemoveConsecutiveDuplicates(route);
            if (route.Count > 1 && route[route.Count - 1] == route[0])
            {
                route.RemoveAt(route.Count - 1);
            }

            return route;
        }

        private static bool IsClosedRouteValid(List<int> route, Dictionary<int, List<int>> adjacency, List<RoadTile> tiles)
        {
            if (route == null || route.Count < 4)
            {
                return false;
            }

            if (route.Distinct().Count() != route.Count)
            {
                return false;
            }

            for (int i = 0; i < route.Count; i++)
            {
                int current = route[i];
                int next = route[(i + 1) % route.Count];
                if (current == next)
                {
                    return false;
                }

                if (!adjacency.TryGetValue(current, out List<int> neighbors) || !neighbors.Contains(next))
                {
                    return false;
                }

                if (!IsRoadSegmentOnRoadTiles(tiles[current].Center, tiles[next].Center, tiles))
                {
                    return false;
                }
            }

            return true;
        }

        private static float CalculateClosedRouteLength(List<RoadTile> tiles, List<int> route)
        {
            float length = 0f;
            if (route == null || route.Count < 2)
            {
                return length;
            }

            for (int i = 0; i < route.Count; i++)
            {
                Vector3 a = tiles[route[i]].Center;
                Vector3 b = tiles[route[(i + 1) % route.Count]].Center;
                length += Vector3.Distance(a, b);
            }

            return length;
        }

        private static void AppendOrderedTileIndices(List<int> route, List<RoadTile> allTiles, IEnumerable<RoadTile> orderedTiles)
        {
            foreach (RoadTile tile in orderedTiles)
            {
                int index = allTiles.IndexOf(tile);
                if (index >= 0)
                {
                    AppendIndex(route, index);
                }
            }
        }

        private static List<int> RepairRouteWithAdjacency(List<int> route, Dictionary<int, List<int>> adjacency)
        {
            var repaired = new List<int>();
            for (int i = 0; i < route.Count; i++)
            {
                int next = route[i];
                if (repaired.Count == 0)
                {
                    AppendIndex(repaired, next);
                    continue;
                }

                int previous = repaired[repaired.Count - 1];
                if (adjacency.TryGetValue(previous, out List<int> neighbors) && neighbors.Contains(next))
                {
                    AppendIndex(repaired, next);
                    continue;
                }

                List<int> connector = FindShortestPath(adjacency, previous, next);
                if (connector.Count > 1)
                {
                    AppendPath(repaired, connector.Skip(1).ToList());
                }
            }

            if (repaired.Count > 2)
            {
                int last = repaired[repaired.Count - 1];
                int first = repaired[0];
                if (!adjacency.TryGetValue(last, out List<int> neighbors) || !neighbors.Contains(first))
                {
                    List<int> closing = FindShortestPath(adjacency, last, first);
                    if (closing.Count > 1)
                    {
                        AppendPath(repaired, closing.Skip(1).ToList());
                    }
                }
            }

            RemoveConsecutiveDuplicates(repaired);
            return repaired;
        }

        private static void DepthFirstTour(int current, int previous, List<RoadTile> tiles, Dictionary<int, List<int>> adjacency, HashSet<int> visited, List<int> route)
        {
            AppendIndex(route, current);
            visited.Add(current);

            List<int> neighbors = adjacency.ContainsKey(current) ? adjacency[current] : null;
            if (neighbors == null)
            {
                return;
            }

            Vector3 inbound = previous >= 0 ? (tiles[current].Center - tiles[previous].Center).normalized : Vector3.forward;
            List<int> orderedNeighbors = neighbors
                .Where(index => index != previous)
                .OrderBy(index => TurnCost(inbound, tiles[index].Center - tiles[current].Center))
                .ThenBy(index => Vector3.Distance(tiles[current].Center, tiles[index].Center))
                .ToList();

            for (int i = 0; i < orderedNeighbors.Count; i++)
            {
                int next = orderedNeighbors[i];
                if (visited.Contains(next))
                {
                    continue;
                }

                DepthFirstTour(next, current, tiles, adjacency, visited, route);
                AppendIndex(route, current);
            }
        }

        private static float TurnCost(Vector3 inbound, Vector3 outbound)
        {
            inbound.y = 0f;
            outbound.y = 0f;
            if (inbound.sqrMagnitude < 0.0001f || outbound.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            return 1f - Vector3.Dot(inbound.normalized, outbound.normalized);
        }

        private static CocoonTrafficLanePath BuildLanePath(Transform networkRoot, string name, List<RoadTile> tiles, Dictionary<int, List<int>> adjacency, List<int> route, float laneY, float laneOffset, RoadBoundary boundary, float experienceScale)
        {
            var laneObject = new GameObject(name);
            laneObject.transform.SetParent(networkRoot, false);
            var waypoints = new Transform[route.Count];
            for (int i = 0; i < route.Count; i++)
            {
                int tileIndex = route[i];
                RoadTile tile = tiles[tileIndex];
                Vector3 forward = ResolveRouteForward(tiles, route, i);
                Vector3 lanePosition = OffsetForLeftHandLane(tile.Center, forward, tile.IsIntersection ? laneOffset * 0.52f : laneOffset);
                lanePosition = ClampToTileBounds(lanePosition, tile.Bounds, EstimateMedianMinorExtent(tiles) * 0.08f);
                lanePosition = ClampToBoundary(lanePosition, boundary);
                lanePosition.y = laneY;

                var waypointObject = new GameObject(name + " WP " + i + " - " + tile.Transform.name);
                waypointObject.transform.SetParent(laneObject.transform, false);
                waypointObject.transform.position = lanePosition;
                waypointObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

                if (tile.IsIntersection || (adjacency.ContainsKey(tileIndex) && adjacency[tileIndex].Count >= 3))
                {
                    CocoonTrafficControlPoint control = waypointObject.AddComponent<CocoonTrafficControlPoint>();
                    control.Configure(
                        CocoonTrafficControlType.IntersectionYield,
                        "ROADMAP-" + tile.Row + "-" + tile.Column,
                        Mathf.Max(2.2f * experienceScale, EstimateMedianMinorExtent(tiles) * 0.42f),
                        0.35f,
                        0.95f,
                        0.48f,
                        0.88f);
                }

                waypoints[i] = waypointObject.transform;
            }

            var lane = laneObject.AddComponent<CocoonTrafficLanePath>();
            bool isClosedLoop = IsClosedRouteValid(route, adjacency, tiles);
            lane.Configure(waypoints, isClosedLoop);
            if (!isClosedLoop)
            {
                Debug.LogWarning("ROADMAP lane " + name + " was generated as a non-loop path. Vehicles will stop at the end instead of wrapping across a missing/END road.");
            }

            return lane;
        }

        private static CocoonRoadGraph BuildRoadGraph(Transform networkRoot, List<RoadTile> tiles, Dictionary<int, List<int>> adjacency, Transform[] pickupStops, float laneY, float laneOffset, RoadBoundary boundary, float experienceScale)
        {
            var graphObject = new GameObject("ROADMAP Directed Road Graph");
            graphObject.transform.SetParent(networkRoot, false);
            CocoonRoadGraph graph = graphObject.AddComponent<CocoonRoadGraph>();

            var nodes = new List<CocoonRoadGraph.Node>();
            for (int i = 0; i < tiles.Count; i++)
            {
                RoadTile tile = tiles[i];
                nodes.Add(new CocoonRoadGraph.Node
                {
                    Id = i,
                    Row = tile.Row,
                    Column = tile.Column,
                    Position = new Vector3(tile.Center.x, laneY, tile.Center.z)
                });
            }

            var edges = new List<CocoonRoadGraph.Edge>();
            float roadWidth = EstimateMedianMinorExtent(tiles);
            int rejectedEdges = 0;
            foreach (KeyValuePair<int, List<int>> pair in adjacency)
            {
                int from = pair.Key;
                List<int> neighbors = pair.Value;
                if (neighbors == null || from < 0 || from >= tiles.Count)
                {
                    continue;
                }

                for (int i = 0; i < neighbors.Count; i++)
                {
                    int to = neighbors[i];
                    if (to < 0 || to >= tiles.Count || from == to)
                    {
                        continue;
                    }

                    RoadTile fromTile = tiles[from];
                    RoadTile toTile = tiles[to];
                    Vector3 forward = toTile.Center - fromTile.Center;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.0001f)
                    {
                        continue;
                    }

                    forward.Normalize();
                    Vector3 start = OffsetForLeftHandLane(fromTile.Center, forward, fromTile.IsIntersection ? laneOffset * 0.52f : laneOffset);
                    Vector3 end = OffsetForLeftHandLane(toTile.Center, forward, toTile.IsIntersection ? laneOffset * 0.52f : laneOffset);
                    start = ClampToTileBounds(start, fromTile.Bounds, roadWidth * 0.08f);
                    end = ClampToTileBounds(end, toTile.Bounds, roadWidth * 0.08f);
                    start = ClampToBoundary(start, boundary);
                    end = ClampToBoundary(end, boundary);
                    start.y = laneY;
                    end.y = laneY;

                    if (!IsRoadSegmentOnRoadTiles(start, end, tiles))
                    {
                        rejectedEdges++;
                        continue;
                    }

                    edges.Add(new CocoonRoadGraph.Edge
                    {
                        Id = edges.Count,
                        FromNode = from,
                        ToNode = to,
                        Start = start,
                        End = end,
                        Length = Vector3.Distance(start, end)
                    });
                }
            }

            graph.Configure(nodes, edges, pickupStops, Mathf.Max(roadWidth * 1.8f, 1.5f * experienceScale));
            Debug.Log("ROADMAP directed graph built: nodes=" + graph.NodeCount +
                      ", directedEdges=" + graph.EdgeCount +
                      ", components=" + graph.ConnectedComponentCount +
                      ", pickupBindings=" + graph.BayCount +
                      ", rejectedENDOrOffRoadEdges=" + rejectedEdges + ".");
            return graph;
        }

        private static Vector3 ResolveRouteForward(List<RoadTile> tiles, List<int> route, int routeIndex)
        {
            Vector3 current = tiles[route[routeIndex]].Center;
            Vector3 previous = tiles[route[(routeIndex - 1 + route.Count) % route.Count]].Center;
            Vector3 next = tiles[route[(routeIndex + 1) % route.Count]].Center;
            Vector3 forward = next - current;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = current - previous;
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.y = 0f;
            return forward.normalized;
        }

        private static Vector3 OffsetForLeftHandLane(Vector3 center, Vector3 forward, float laneOffset)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            return center - right * laneOffset;
        }

        private static Vector3 ClampToBoundary(Vector3 point, RoadBoundary boundary)
        {
            point.x = Mathf.Clamp(point.x, boundary.MinX, boundary.MaxX);
            point.z = Mathf.Clamp(point.z, boundary.MinZ, boundary.MaxZ);
            return point;
        }

        private static Vector3 ClampToTileBounds(Vector3 point, Bounds bounds, float inset)
        {
            float minX = bounds.min.x + inset;
            float maxX = bounds.max.x - inset;
            float minZ = bounds.min.z + inset;
            float maxZ = bounds.max.z - inset;
            if (minX > maxX)
            {
                minX = maxX = bounds.center.x;
            }

            if (minZ > maxZ)
            {
                minZ = maxZ = bounds.center.z;
            }

            point.x = Mathf.Clamp(point.x, minX, maxX);
            point.z = Mathf.Clamp(point.z, minZ, maxZ);
            return point;
        }

        private static Transform[] BuildPickupStops(
            Transform pickupRoot,
            List<RoadTile> tiles,
            CocoonTrafficLanePath lanePath,
            float laneY,
            float roadWidth,
            float laneOffset,
            VehicleDimensions taxiDimensions,
            RoadBoundary boundary,
            float experienceScale,
            out int[] pickupIndices)
        {
            if (TryCollectExistingPickupStops(pickupRoot, lanePath, out Transform[] existingStops, out pickupIndices))
            {
                return existingStops;
            }

            Transform markerRoot = FindDirectChild(pickupRoot, PickupRootName);
            if (markerRoot == null)
            {
                markerRoot = new GameObject(PickupRootName).transform;
                markerRoot.SetParent(pickupRoot, false);
            }

            Material asphaltMat = LoadMaterial("Road", new Color(0.055f, 0.065f, 0.072f));
            Material edgeMat = LoadMaterial("GuidanceArrow", new Color(0.1f, 0.95f, 0.58f));
            Material lineMat = LoadMaterial("Crosswalk", new Color(0.93f, 0.94f, 0.9f));
            float bayLengthMin = taxiDimensions.Length + 0.45f * experienceScale;
            float bayLengthMax = Mathf.Max(bayLengthMin, roadWidth * 1.05f);
            float bayLength = Mathf.Clamp(taxiDimensions.Length * 1.35f, bayLengthMin, bayLengthMax);
            float bayWidthMin = taxiDimensions.Width + 0.22f * experienceScale;
            float bayWidthMax = Mathf.Max(bayWidthMin, roadWidth * 0.38f);
            float bayWidth = Mathf.Clamp(taxiDimensions.Width * 1.25f, bayWidthMin, bayWidthMax);
            float curbOffset = Mathf.Clamp(roadWidth * 0.5f - bayWidth * 0.5f, laneOffset + bayWidth * 0.15f, roadWidth * 0.5f);
            List<PickupCandidate> candidates = BuildPickupCandidates(tiles, CalculateCenter(tiles), curbOffset, laneY, experienceScale);
            int maxPickupStops = Mathf.Clamp(candidates.Count, 2, 28);
            List<PickupCandidate> boundedCandidates = candidates
                .Where(candidate => boundary.Contains(candidate.Position))
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.Position.z)
                .ThenBy(candidate => candidate.Position.x)
                .Take(maxPickupStops)
                .ToList();
            if (boundedCandidates.Count == 0)
            {
                boundedCandidates = candidates
                    .Select(candidate =>
                    {
                        candidate.Position = ClampToBoundary(candidate.Position, boundary);
                        return candidate;
                    })
                    .OrderByDescending(candidate => candidate.Priority)
                    .ThenBy(candidate => candidate.Position.z)
                    .ThenBy(candidate => candidate.Position.x)
                    .Take(maxPickupStops)
                    .ToList();
            }

            candidates = boundedCandidates;

            var stops = new List<Transform>();
            var indices = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                Transform stop = CreatePickupBay(markerRoot, "ROADMAP Pickup Bay " + i, candidates[i].Position, candidates[i].Rotation, bayWidth, bayLength, experienceScale, asphaltMat, edgeMat, lineMat);
                stops.Add(stop);
                indices.Add(FindNearestPathIndex(lanePath, stop.position));
            }

            pickupIndices = indices.ToArray();
            return stops.ToArray();
        }

        private static bool TryCollectExistingPickupStops(Transform pickupRoot, CocoonTrafficLanePath lanePath, out Transform[] pickupStops, out int[] pickupIndices)
        {
            pickupStops = new Transform[0];
            pickupIndices = new int[0];
            Transform markerRoot = FindDirectChild(pickupRoot, PickupRootName);
            if (markerRoot == null)
            {
                return false;
            }

            var stops = new List<Transform>();
            for (int i = 0; i < markerRoot.childCount; i++)
            {
                Transform child = markerRoot.GetChild(i);
                Transform stop = child.name == "Pickup Bay Stop" ? child : FindChildDeep(child, "Pickup Bay Stop");
                if (stop != null)
                {
                    HidePickupBayVisuals(stop);
                    stops.Add(stop);
                }
                else
                {
                    Debug.LogWarning("ROADMAP manual pickup bay " + child.name + " has no Pickup Bay Stop anchor. It was left untouched and skipped for taxi pickup selection.", child);
                }
            }

            var indices = new int[stops.Count];
            for (int i = 0; i < stops.Count; i++)
            {
                indices[i] = FindNearestPathIndex(lanePath, stops[i].position);
            }

            pickupStops = stops.ToArray();
            pickupIndices = indices;
            Debug.Log("ROADMAP manual pickup bays protected: " + pickupStops.Length + " stop anchor(s). Rebuild refreshed taxi references only; bay transforms were not generated, moved, or repaired.");
            return true;
        }

        private static int[] BuildPickupPathIndices(CocoonTrafficLanePath lanePath, Transform[] pickupStops)
        {
            if (pickupStops == null)
            {
                return new int[0];
            }

            var indices = new int[pickupStops.Length];
            for (int i = 0; i < pickupStops.Length; i++)
            {
                indices[i] = pickupStops[i] != null ? FindNearestPathIndex(lanePath, pickupStops[i].position) : -1;
            }

            return indices;
        }

        private static List<PickupCandidate> BuildPickupCandidates(List<RoadTile> tiles, Vector3 networkCenter, float curbOffset, float laneY, float experienceScale)
        {
            var candidates = new List<PickupCandidate>();
            List<StreetLine> lines = BuildStreetLines(tiles);
            for (int i = 0; i < lines.Count; i++)
            {
                StreetLine line = lines[i];
                if (line.Tiles.Count < 2)
                {
                    continue;
                }

                RoadTile[] ordered = line.Horizontal
                    ? line.Tiles.OrderBy(tile => tile.Center.x).ToArray()
                    : line.Tiles.OrderBy(tile => tile.Center.z).ToArray();
                Vector3 start = ordered[0].Center;
                Vector3 end = ordered[ordered.Length - 1].Center;
                float length = Vector3.Distance(start, end);
                if (length < line.Width * 1.7f)
                {
                    continue;
                }

                for (int sample = 0; sample < 2; sample++)
                {
                    float t = sample == 0 ? 0.31f : 0.69f;
                    Vector3 lanePoint = Vector3.Lerp(start, end, t);
                    lanePoint.y = laneY + Mathf.Max(0.003f, 0.035f * experienceScale);
                    Vector3 side;
                    Vector3 forward;
                    if (line.Horizontal)
                    {
                        bool northSide = lanePoint.z >= networkCenter.z;
                        side = northSide ? Vector3.forward : Vector3.back;
                        forward = northSide ? Vector3.right : Vector3.left;
                    }
                    else
                    {
                        bool eastSide = lanePoint.x >= networkCenter.x;
                        side = eastSide ? Vector3.right : Vector3.left;
                        forward = eastSide ? Vector3.back : Vector3.forward;
                    }

                    candidates.Add(new PickupCandidate
                    {
                        Position = lanePoint + side * curbOffset,
                        Rotation = Quaternion.LookRotation(forward, Vector3.up),
                        Priority = length
                    });
                }
            }

            return candidates;
        }

        private static List<StreetLine> BuildStreetLines(List<RoadTile> tiles)
        {
            var lines = new List<StreetLine>();
            foreach (IGrouping<int, RoadTile> row in tiles.GroupBy(tile => tile.Row))
            {
                List<RoadTile> lineTiles = row
                    .Where(tile => tile.Axis == RoadAxis.Horizontal || tile.Axis == RoadAxis.Intersection)
                    .OrderBy(tile => tile.Center.x)
                    .ToList();
                if (lineTiles.Count >= 2)
                {
                    lines.Add(new StreetLine { Horizontal = true, Tiles = lineTiles, Width = Median(lineTiles.Select(tile => Mathf.Min(tile.Bounds.size.x, tile.Bounds.size.z)).ToList()) });
                }
            }

            foreach (IGrouping<int, RoadTile> column in tiles.GroupBy(tile => tile.Column))
            {
                List<RoadTile> lineTiles = column
                    .Where(tile => tile.Axis == RoadAxis.Vertical || tile.Axis == RoadAxis.Intersection)
                    .OrderBy(tile => tile.Center.z)
                    .ToList();
                if (lineTiles.Count >= 2)
                {
                    lines.Add(new StreetLine { Horizontal = false, Tiles = lineTiles, Width = Median(lineTiles.Select(tile => Mathf.Min(tile.Bounds.size.x, tile.Bounds.size.z)).ToList()) });
                }
            }

            return lines;
        }

        private static Transform CreatePickupBay(Transform parent, string name, Vector3 position, Quaternion rotation, float width, float length, float experienceScale, Material asphaltMat, Material edgeMat, Material lineMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = rotation;

            var stop = new GameObject("Pickup Bay Stop");
            stop.transform.SetParent(root.transform, false);
            stop.transform.localPosition = Vector3.zero;
            stop.transform.localRotation = Quaternion.identity;

            float slabHeight = Mathf.Max(0.003f, 0.025f * experienceScale);
            float lineHeight = Mathf.Max(0.003f, 0.03f * experienceScale);
            float lineWidth = Mathf.Max(0.004f, 0.05f * experienceScale);
            CreateCubeChild("Bay Footprint", root.transform, Vector3.zero, new Vector3(width, slabHeight, length), asphaltMat);
            CreateCubeChild("Door Edge", root.transform, new Vector3(-width * 0.48f, slabHeight, 0f), new Vector3(lineWidth, lineHeight, length), edgeMat);
            CreateCubeChild("Front Tick", root.transform, new Vector3(0f, lineHeight, length * 0.43f), new Vector3(width, lineHeight, lineWidth), lineMat);
            CreateCubeChild("Rear Tick", root.transform, new Vector3(0f, lineHeight, -length * 0.43f), new Vector3(width, lineHeight, lineWidth), lineMat);
            HidePickupBayVisuals(stop.transform);
            return stop.transform;
        }

        private static void HidePickupBayVisuals(Transform bayReference)
        {
            Transform bayRoot = ResolvePickupBayVisualRoot(bayReference);
            if (bayRoot == null)
            {
                return;
            }

            Renderer[] renderers = bayRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private static Transform ResolvePickupBayVisualRoot(Transform bayReference)
        {
            if (bayReference == null)
            {
                return null;
            }

            if (bayReference.name == "Pickup Bay Stop" && bayReference.parent != null)
            {
                return bayReference.parent;
            }

            Transform explicitStop = bayReference.Find("Pickup Bay Stop");
            if (explicitStop != null)
            {
                return bayReference;
            }

            return bayReference.name.IndexOf("Pickup Bay", StringComparison.OrdinalIgnoreCase) >= 0 ? bayReference : null;
        }

        private static void RepositionTrafficVehicles(Scene scene, Transform trafficRoot, CocoonTrafficLanePath forwardLane, CocoonTrafficLanePath reverseLane, CocoonRoadGraph roadGraph, float experienceScale)
        {
            List<CocoonTrafficVehicle> vehicles = FindTrafficVehicles(scene);
            if (vehicles.Count == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    vehicles.Add(CreateFallbackTrafficVehicle(trafficRoot, i, experienceScale).AddComponent<CocoonTrafficVehicle>());
                }
            }

            float[] speeds = { 2.2f, 2.55f, 2.0f, 2.8f, 2.35f, 2.65f, 2.15f, 2.45f, 2.7f, 2.3f, 2.6f, 2.05f };
            Material tireMat = LoadMaterial("Tires", new Color(0.015f, 0.015f, 0.018f));
            int forwardAssignments = 0;
            int reverseAssignments = 0;
            for (int i = 0; i < vehicles.Count; i++)
            {
                CocoonTrafficLanePath vehicleLane = ResolveBestTrafficLaneForAuthoredVehicle(vehicles[i], forwardLane, reverseLane);
                if (vehicleLane == null || vehicleLane.Count < 2)
                {
                    continue;
                }

                if (trafficRoot != null && vehicles[i].transform.parent != trafficRoot)
                {
                    vehicles[i].transform.SetParent(trafficRoot, true);
                }

                EnsureTrafficVehicleWheelVisuals(vehicles[i].transform, tireMat, experienceScale);
                int startIndex = Mathf.FloorToInt((i + 0.5f) / Mathf.Max(1, vehicles.Count) * vehicleLane.Count) % vehicleLane.Count;
                SetPreserveAuthoredTrafficSpawn(vehicles[i], true);
                vehicles[i].Configure(vehicleLane, speeds[i % speeds.Length], startIndex, EstimateVehicleLength(vehicles[i].gameObject, experienceScale));
                vehicles[i].ConfigureGraph(roadGraph);
                if (vehicleLane == forwardLane)
                {
                    forwardAssignments++;
                }
                else if (vehicleLane == reverseLane)
                {
                    reverseAssignments++;
                }
            }

            Debug.Log("ROADMAP traffic references refreshed for " + vehicles.Count + " authored traffic vehicle(s); spawn transforms were preserved on " +
                      forwardAssignments + " forward and " + reverseAssignments + " reverse lane assignment(s).");
        }

        private static CocoonTrafficLanePath ResolveBestTrafficLaneForAuthoredVehicle(CocoonTrafficVehicle vehicle, CocoonTrafficLanePath forwardLane, CocoonTrafficLanePath reverseLane)
        {
            if (vehicle == null)
            {
                return reverseLane != null ? reverseLane : forwardLane;
            }

            float forwardScore = GetLaneFitScore(forwardLane, vehicle.transform);
            float reverseScore = GetLaneFitScore(reverseLane, vehicle.transform);
            if (forwardScore == float.MaxValue && reverseScore == float.MaxValue)
            {
                return reverseLane != null ? reverseLane : forwardLane;
            }

            return forwardScore <= reverseScore ? forwardLane : reverseLane;
        }

        private static float GetLaneFitScore(CocoonTrafficLanePath lane, Transform vehicle)
        {
            if (lane == null || lane.Count < 2 || vehicle == null)
            {
                return float.MaxValue;
            }

            int targetIndex = lane.FindNearestSegmentTargetIndex(vehicle.position);
            if (targetIndex < 0)
            {
                return float.MaxValue;
            }

            int previousIndex = lane.NormalizeIndex(targetIndex - 1);
            Transform previous = lane.GetWaypoint(previousIndex);
            Transform target = lane.GetWaypoint(targetIndex);
            if (previous == null || target == null)
            {
                return float.MaxValue;
            }

            Vector3 flatPosition = vehicle.position;
            flatPosition.y = 0f;
            Vector3 start = previous.position;
            Vector3 end = target.position;
            start.y = 0f;
            end.y = 0f;
            Vector3 segment = end - start;
            float segmentLengthSqr = segment.sqrMagnitude;
            Vector3 closest = segmentLengthSqr > 0.000001f
                ? start + segment * Mathf.Clamp01(Vector3.Dot(flatPosition - start, segment) / segmentLengthSqr)
                : start;
            float distanceScore = Vector3.SqrMagnitude(flatPosition - closest);

            Vector3 vehicleForward = vehicle.forward;
            vehicleForward.y = 0f;
            Vector3 laneForward = lane.GetSegmentForward(targetIndex);
            float directionPenalty = 0f;
            if (vehicleForward.sqrMagnitude > 0.0001f && laneForward.sqrMagnitude > 0.0001f)
            {
                directionPenalty = (1f - Mathf.Clamp01(Vector3.Dot(vehicleForward.normalized, laneForward.normalized))) * 0.0004f;
            }

            return distanceScore + directionPenalty;
        }

        private static void SetPreserveAuthoredTrafficSpawn(CocoonTrafficVehicle vehicle, bool preserve)
        {
            if (vehicle == null)
            {
                return;
            }

            var serialized = new SerializedObject(vehicle);
            SerializedProperty preserveProperty = serialized.FindProperty("preserveAuthoredSpawnTransform");
            if (preserveProperty == null || preserveProperty.boolValue == preserve)
            {
                return;
            }

            preserveProperty.boolValue = preserve;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CenterVehicleVisualOnRoot(Transform vehicleRoot)
        {
            if (vehicleRoot == null || !TryGetRendererBounds(vehicleRoot, out Bounds bounds))
            {
                return;
            }

            Vector3 horizontalOffset = bounds.center - vehicleRoot.position;
            horizontalOffset.y = 0f;
            if (horizontalOffset.sqrMagnitude < 0.0009f)
            {
                return;
            }

            Vector3 localOffset = vehicleRoot.InverseTransformVector(horizontalOffset);
            for (int i = 0; i < vehicleRoot.childCount; i++)
            {
                Transform child = vehicleRoot.GetChild(i);
                if (child != null)
                {
                    child.localPosition -= localOffset;
                }
            }
        }

        private static void ReconfigureTaxi(Scene scene, CocoonTrafficLanePath lanePath, CocoonTrafficLanePath oppositeLanePath, CocoonRoadGraph roadGraph, Transform[] pickupStops, int[] pickupIndices)
        {
            Transform taxi = FindTransformDeep(scene, "Cocoon Autonomous Taxi");
            CocoonTaxiStateMachine stateMachine = FindComponentDeep<CocoonTaxiStateMachine>(scene);
            if (stateMachine == null || lanePath == null)
            {
                return;
            }

            var serialized = new SerializedObject(stateMachine);
            serialized.FindProperty("cruisePath").objectReferenceValue = lanePath;
            serialized.FindProperty("primaryCruisePath").objectReferenceValue = lanePath;
            serialized.FindProperty("oppositeCruisePath").objectReferenceValue = oppositeLanePath;
            serialized.FindProperty("cruiseStart").objectReferenceValue = lanePath.GetWaypoint(0);
            serialized.FindProperty("cruiseEnd").objectReferenceValue = lanePath.GetWaypoint(1);
            serialized.FindProperty("cruiseTargetIndex").intValue = 1;
            SerializedProperty roadGraphProperty = serialized.FindProperty("roadGraph");
            if (roadGraphProperty != null)
            {
                roadGraphProperty.objectReferenceValue = roadGraph;
            }

            SerializedProperty useRoadGraphProperty = serialized.FindProperty("useRoadGraph");
            if (useRoadGraphProperty != null)
            {
                useRoadGraphProperty.boolValue = roadGraph != null;
            }

            SerializedProperty preserveSpawnProperty = serialized.FindProperty("preserveAuthoredTaxiSpawnTransform");
            if (preserveSpawnProperty != null)
            {
                preserveSpawnProperty.boolValue = true;
            }
            serialized.FindProperty("pullOverPoint").objectReferenceValue = pickupStops != null && pickupStops.Length > 0 ? pickupStops[0] : null;

            SerializedProperty baysProperty = serialized.FindProperty("pullOverPoints");
            int pickupStopCount = pickupStops != null ? pickupStops.Length : 0;
            baysProperty.arraySize = pickupStopCount;
            for (int i = 0; i < pickupStopCount; i++)
            {
                baysProperty.GetArrayElementAtIndex(i).objectReferenceValue = pickupStops[i];
            }

            SerializedProperty indicesProperty = serialized.FindProperty("pullOverPathIndices");
            int pickupIndexCount = pickupIndices != null ? pickupIndices.Length : 0;
            indicesProperty.arraySize = pickupIndexCount;
            for (int i = 0; i < pickupIndexCount; i++)
            {
                indicesProperty.GetArrayElementAtIndex(i).intValue = pickupIndices[i];
            }

            if (pickupStopCount == 0)
            {
                Debug.LogWarning("ROADMAP manual pickup bays protected but no Pickup Bay Stop anchors were found. Taxi pickup arrays were cleared; no bay transforms were generated, moved, or repaired.", stateMachine);
            }

            CocoonSafePickupZone safeZone = FindComponentDeep<CocoonSafePickupZone>(scene);
            if (safeZone != null)
            {
                serialized.FindProperty("safePickupZone").objectReferenceValue = safeZone;
            }

            Transform guidance = FindTransformDeep(scene, "Guidance To Safe Pickup Zone");
            if (guidance != null)
            {
                serialized.FindProperty("guidanceRoot").objectReferenceValue = guidance.gameObject;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReconfigureXrStart(Scene scene, CocoonTrafficLanePath lanePath, Transform[] pickupStops, List<RoadTile> roadTiles)
        {
            Transform xrOrigin = FindTransformDeep(scene, "XR Origin - Sidewalk Rider");
            Transform mainCamera = FindTransformDeep(scene, "Main Camera");
            if (mainCamera != null)
            {
                mainCamera.gameObject.SetActive(true);
                Camera camera = mainCamera.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.enabled = true;
                }
            }

            if (xrOrigin == null)
            {
                return;
            }

            float experienceScale = ApplyRoadmapVrScale(scene, xrOrigin);

            Vector3 lookAt;
            Vector3 position;
            float floorY = EstimateLaneY(roadTiles, experienceScale);
            if (pickupStops != null && pickupStops.Length > 0 && pickupStops[0] != null)
            {
                Transform stop = pickupStops[0];
                lookAt = stop.position;
                position = stop.position - stop.forward * (1.85f * experienceScale);
            }
            else if (lanePath != null && lanePath.Count > 1)
            {
                Transform first = lanePath.GetWaypoint(0);
                Transform next = lanePath.GetWaypoint(1);
                Vector3 forward = next != null ? next.position - first.position : Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.forward;
                }

                forward.Normalize();
                Vector3 left = -Vector3.Cross(Vector3.up, forward).normalized;
                lookAt = first.position;
                position = first.position + left * (2.25f * experienceScale) - forward * (0.8f * experienceScale);
            }
            else
            {
                Vector3 center = CalculateCenter(roadTiles);
                lookAt = center;
                position = center + new Vector3(-2.5f * experienceScale, 0f, -2.5f * experienceScale);
            }

            position.y = floorY;
            xrOrigin.position = position;
            Vector3 direction = lookAt - position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                xrOrigin.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            Debug.Log("ROADMAP VR start configured at " + xrOrigin.position.ToString("F2") + ".", xrOrigin);
        }

        private static float ApplyRoadmapVrScale(Scene scene, Transform xrOrigin)
        {
            Transform roadmap = FindRoadmapRoot(scene);
            float experienceScale = ResolveRoadmapExperienceScale(roadmap);
            Vector3 targetScale = Vector3.one * experienceScale;
            if (Vector3.Distance(xrOrigin.localScale, targetScale) < 0.0001f)
            {
                if (CocoonExperienceScale.CalibrateCharacterController(xrOrigin))
                {
                    Debug.Log("ROADMAP VR CharacterController calibrated for scaled sidewalk locomotion.", xrOrigin);
                }

                return experienceScale;
            }

            xrOrigin.localScale = targetScale;
            Debug.Log("ROADMAP VR rider scale set to " + experienceScale.ToString("0.###") +
                      " so headset height matches the scaled Japanese street.", xrOrigin);
            if (CocoonExperienceScale.CalibrateCharacterController(xrOrigin))
            {
                Debug.Log("ROADMAP VR CharacterController calibrated for scaled sidewalk locomotion.", xrOrigin);
            }

            return experienceScale;
        }

        private static float ResolveRoadmapExperienceScale(Transform roadmap)
        {
            if (roadmap == null)
            {
                return 1f;
            }

            Vector3 scale = roadmap.lossyScale;
            float horizontalScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.z)) * 0.5f;
            if (horizontalScale < 0.02f || horizontalScale > 0.35f)
            {
                return 1f;
            }

            return horizontalScale;
        }

        private static void PreserveTrafficVehiclesFromGeneratedRoots(Scene scene, Transform trafficRoot)
        {
            List<CocoonTrafficVehicle> vehicles = FindTrafficVehicles(scene);
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] != null && FindGeneratedAncestor(vehicles[i].transform) != null)
                {
                    vehicles[i].transform.SetParent(trafficRoot, true);
                }
            }
        }

        private static Transform FindGeneratedAncestor(Transform transform)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.name == NetworkRootName || current.name == LegacyNetworkRootName)
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private static List<CocoonTrafficVehicle> FindTrafficVehicles(Scene scene)
        {
            var vehicles = new List<CocoonTrafficVehicle>();
            var seen = new HashSet<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CocoonTrafficVehicle[] existingVehicles = root.GetComponentsInChildren<CocoonTrafficVehicle>(true);
                for (int i = 0; i < existingVehicles.Length; i++)
                {
                    AddTrafficVehicle(existingVehicles[i], vehicles, seen);
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (!IsTrafficVehicleRootCandidate(candidate))
                    {
                        continue;
                    }

                    CocoonTrafficVehicle vehicle = candidate.GetComponent<CocoonTrafficVehicle>();
                    if (vehicle == null)
                    {
                        vehicle = candidate.gameObject.AddComponent<CocoonTrafficVehicle>();
                    }

                    AddTrafficVehicle(vehicle, vehicles, seen);
                }
            }

            return vehicles;
        }

        private static void AddTrafficVehicle(CocoonTrafficVehicle vehicle, List<CocoonTrafficVehicle> vehicles, HashSet<GameObject> seen)
        {
            if (vehicle == null || vehicle.gameObject == null || !vehicle.gameObject.activeInHierarchy || seen.Contains(vehicle.gameObject) || !IsTrafficVehicleName(vehicle.name))
            {
                return;
            }

            seen.Add(vehicle.gameObject);
            vehicles.Add(vehicle);
        }

        private static bool IsTrafficVehicleRootCandidate(Transform transform)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy || !IsTrafficVehicleName(transform.name))
            {
                return false;
            }

            CocoonTrafficVehicle parentVehicle = transform.parent != null ? transform.parent.GetComponentInParent<CocoonTrafficVehicle>() : null;
            if (parentVehicle != null)
            {
                return false;
            }

            return HasVehicleLikeBounds(transform);
        }

        private static bool IsTrafficVehicleName(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Traffic ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return name.StartsWith("Traffic ROADMAP ", StringComparison.OrdinalIgnoreCase) ||
                   lower.Contains("sedan") ||
                   lower.Contains("compact") ||
                   lower.Contains("van") ||
                   lower.Contains("bus") ||
                   lower.Contains("car");
        }

        private static bool HasVehicleLikeBounds(Transform transform)
        {
            if (!TryGetRendererBounds(transform, out Bounds bounds))
            {
                return false;
            }

            float horizontalLong = Mathf.Max(bounds.size.x, bounds.size.z);
            float horizontalShort = Mathf.Min(bounds.size.x, bounds.size.z);
            if (horizontalLong < 0.5f || horizontalLong > 12f || horizontalShort < 0.25f || horizontalShort > 5f)
            {
                return false;
            }

            return bounds.size.y <= Mathf.Max(4f, horizontalLong * 1.4f);
        }

        private static int CountTrafficVehicles(Scene scene)
        {
            return FindTrafficVehicles(scene).Count;
        }

        private static int CountTrafficVehiclesOutside(Scene scene, RoadBoundary boundary)
        {
            int outside = 0;
            List<CocoonTrafficVehicle> vehicles = FindTrafficVehicles(scene);
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] != null && !boundary.Contains(vehicles[i].transform.position))
                {
                    outside++;
                }
            }

            return outside;
        }

        private static int CountLaneWaypointsOutside(CocoonTrafficLanePath[] lanes, RoadBoundary boundary)
        {
            int outside = 0;
            if (lanes == null)
            {
                return outside;
            }

            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                CocoonTrafficLanePath lane = lanes[laneIndex];
                if (lane == null)
                {
                    continue;
                }

                for (int waypointIndex = 0; waypointIndex < lane.Count; waypointIndex++)
                {
                    Transform waypoint = lane.GetWaypoint(waypointIndex);
                    if (waypoint != null && !boundary.Contains(waypoint.position))
                    {
                        outside++;
                    }
                }
            }

            return outside;
        }

        private static int CountLaneWaypointsOffRoad(CocoonTrafficLanePath[] lanes, List<RoadTile> roadTiles)
        {
            int offRoad = 0;
            if (lanes == null)
            {
                return offRoad;
            }

            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                CocoonTrafficLanePath lane = lanes[laneIndex];
                if (lane == null)
                {
                    continue;
                }

                for (int waypointIndex = 0; waypointIndex < lane.Count; waypointIndex++)
                {
                    Transform waypoint = lane.GetWaypoint(waypointIndex);
                    if (waypoint != null && !IsPointOnAnyRoadTile(waypoint.position, roadTiles))
                    {
                        offRoad++;
                    }
                }
            }

            return offRoad;
        }

        private static int CountLaneSegmentsOffRoad(CocoonTrafficLanePath[] lanes, List<RoadTile> roadTiles)
        {
            int offRoad = 0;
            if (lanes == null)
            {
                return offRoad;
            }

            for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
            {
                CocoonTrafficLanePath lane = lanes[laneIndex];
                if (lane == null || lane.Count < 2)
                {
                    continue;
                }

                int segmentCount = lane.IsClosedLoop ? lane.Count : lane.Count - 1;
                for (int waypointIndex = 0; waypointIndex < segmentCount; waypointIndex++)
                {
                    Transform start = lane.GetWaypoint(waypointIndex);
                    Transform end = lane.IsClosedLoop && waypointIndex == lane.Count - 1
                        ? lane.GetWaypoint(0)
                        : lane.GetWaypoint(waypointIndex + 1);
                    if (start == null || end == null)
                    {
                        continue;
                    }

                    if (!IsRoadSegmentOnRoadTiles(start.position, end.position, roadTiles))
                    {
                        offRoad++;
                    }
                }
            }

            return offRoad;
        }

        private static int CountTrafficVehiclesOffRoad(Scene scene, List<RoadTile> roadTiles)
        {
            int offRoad = 0;
            List<CocoonTrafficVehicle> vehicles = FindTrafficVehicles(scene);
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] == null)
                {
                    continue;
                }

                bool rootOnRoad = IsPointOnAnyRoadTile(vehicles[i].transform.position, roadTiles);
                bool visualOnRoad = !TryGetRendererBounds(vehicles[i].transform, out Bounds bounds) ||
                                    IsPointOnAnyRoadTile(bounds.center, roadTiles);
                if (!rootOnRoad || !visualOnRoad)
                {
                    offRoad++;
                }
            }

            return offRoad;
        }

        private static bool IsRoadSegmentOnRoadTiles(Vector3 start, Vector3 end, List<RoadTile> roadTiles)
        {
            const int Samples = 5;
            for (int i = 1; i < Samples; i++)
            {
                Vector3 sample = Vector3.Lerp(start, end, (float)i / Samples);
                if (!IsPointOnAnyRoadTile(sample, roadTiles))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPointOnAnyRoadTile(Vector3 point, List<RoadTile> roadTiles)
        {
            if (roadTiles == null)
            {
                return false;
            }

            for (int i = 0; i < roadTiles.Count; i++)
            {
                RoadTile tile = roadTiles[i];
                if (tile != null && ContainsPointInTileBounds(point, tile.Bounds, EstimateRoadContainmentPadding(tile)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPointInTileBounds(Vector3 point, Bounds bounds, float padding)
        {
            return point.x >= bounds.min.x - padding &&
                   point.x <= bounds.max.x + padding &&
                   point.z >= bounds.min.z - padding &&
                   point.z <= bounds.max.z + padding;
        }

        private static float EstimateRoadContainmentPadding(RoadTile tile)
        {
            float minor = Mathf.Min(tile.Bounds.size.x, tile.Bounds.size.z);
            return Mathf.Clamp(minor * 0.08f, 0.03f, 0.18f);
        }

        private static GameObject CreateFallbackTrafficVehicle(Transform parent, int index, float experienceScale)
        {
            Material bodyMat = LoadMaterial(index % 3 == 0 ? "TrafficBlue" : index % 3 == 1 ? "TrafficRed" : "TrafficYellow", new Color(0.24f, 0.35f, 0.5f));
            Material glassMat = LoadMaterial("TaxiGlass", new Color(0.08f, 0.16f, 0.2f));
            Material tireMat = LoadMaterial("Tires", new Color(0.015f, 0.015f, 0.018f));
            var root = new GameObject("Traffic ROADMAP " + index);
            root.transform.SetParent(parent, false);
            CreateCubeChild("Body", root.transform, new Vector3(0f, 0.42f, 0f) * experienceScale, new Vector3(1.4f, 0.55f, 3.0f) * experienceScale, bodyMat);
            CreateCubeChild("Cabin", root.transform, new Vector3(0f, 0.88f, -0.08f) * experienceScale, new Vector3(1.05f, 0.5f, 1.25f) * experienceScale, glassMat);
            CreateCylinderChild("Wheel FL", root.transform, new Vector3(-0.76f, 0.22f, 0.96f) * experienceScale, new Vector3(0.28f, 0.12f, 0.28f) * experienceScale, Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateCylinderChild("Wheel FR", root.transform, new Vector3(0.76f, 0.22f, 0.96f) * experienceScale, new Vector3(0.28f, 0.12f, 0.28f) * experienceScale, Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateCylinderChild("Wheel RL", root.transform, new Vector3(-0.76f, 0.22f, -0.96f) * experienceScale, new Vector3(0.28f, 0.12f, 0.28f) * experienceScale, Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateCylinderChild("Wheel RR", root.transform, new Vector3(0.76f, 0.22f, -0.96f) * experienceScale, new Vector3(0.28f, 0.12f, 0.28f) * experienceScale, Quaternion.Euler(0f, 0f, 90f), tireMat);
            return root;
        }

        private static void EnsureTrafficVehicleWheelVisuals(Transform vehicleRoot, Material tireMat, float experienceScale)
        {
            if (vehicleRoot == null || CountWheelVisuals(vehicleRoot) >= 4 || !TryGetLocalRendererBounds(vehicleRoot, out Bounds bounds))
            {
                return;
            }

            bool lengthAlongX = bounds.size.x > bounds.size.z * 1.18f;
            float scale = Mathf.Max(experienceScale, 0.02f);
            float radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.13f, 0.07f * scale, 0.26f * scale);
            float thickness = Mathf.Clamp(radius * 0.42f, 0.035f * scale, 0.12f * scale);
            float wheelY = bounds.min.y + Mathf.Max(radius, bounds.size.y * 0.18f);
            Vector3 center = bounds.center;

            if (lengthAlongX)
            {
                float frontX = center.x + bounds.extents.x * 0.62f;
                float rearX = center.x - bounds.extents.x * 0.62f;
                float leftZ = center.z - bounds.extents.z - thickness * 0.45f;
                float rightZ = center.z + bounds.extents.z + thickness * 0.45f;
                CreateWheelIfMissing(vehicleRoot, "FL", new Vector3(frontX, wheelY, leftZ), new Vector3(radius, thickness, radius), Quaternion.Euler(90f, 0f, 0f), tireMat);
                CreateWheelIfMissing(vehicleRoot, "FR", new Vector3(frontX, wheelY, rightZ), new Vector3(radius, thickness, radius), Quaternion.Euler(90f, 0f, 0f), tireMat);
                CreateWheelIfMissing(vehicleRoot, "RL", new Vector3(rearX, wheelY, leftZ), new Vector3(radius, thickness, radius), Quaternion.Euler(90f, 0f, 0f), tireMat);
                CreateWheelIfMissing(vehicleRoot, "RR", new Vector3(rearX, wheelY, rightZ), new Vector3(radius, thickness, radius), Quaternion.Euler(90f, 0f, 0f), tireMat);
                return;
            }

            float frontZ = center.z + bounds.extents.z * 0.62f;
            float rearZ = center.z - bounds.extents.z * 0.62f;
            float leftX = center.x - bounds.extents.x - thickness * 0.45f;
            float rightX = center.x + bounds.extents.x + thickness * 0.45f;
            CreateWheelIfMissing(vehicleRoot, "FL", new Vector3(leftX, wheelY, frontZ), new Vector3(radius, thickness, radius), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateWheelIfMissing(vehicleRoot, "FR", new Vector3(rightX, wheelY, frontZ), new Vector3(radius, thickness, radius), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateWheelIfMissing(vehicleRoot, "RL", new Vector3(leftX, wheelY, rearZ), new Vector3(radius, thickness, radius), Quaternion.Euler(0f, 0f, 90f), tireMat);
            CreateWheelIfMissing(vehicleRoot, "RR", new Vector3(rightX, wheelY, rearZ), new Vector3(radius, thickness, radius), Quaternion.Euler(0f, 0f, 90f), tireMat);
        }

        private static void CreateWheelIfMissing(Transform vehicleRoot, string slot, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material tireMat)
        {
            if (HasWheelSlot(vehicleRoot, slot))
            {
                return;
            }

            CreateCylinderChild("Auto Wheel " + slot, vehicleRoot, localPosition, localScale, localRotation, tireMat);
        }

        private static int CountWheelVisuals(Transform root)
        {
            int count = 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled && renderers[i].gameObject.activeInHierarchy && IsWheelName(renderers[i].transform.name))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasWheelSlot(Transform root, string slot)
        {
            string lowerSlot = slot.ToLowerInvariant();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null && IsWheelName(transform.name) && transform.name.ToLowerInvariant().Contains(lowerSlot))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWheelName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("wheel") || lower.Contains("tire") || lower.Contains("tyre");
        }

        private struct VehicleDimensions
        {
            public float Width;
            public float Length;
        }

        private static VehicleDimensions EstimateTaxiDimensions(Scene scene, float experienceScale)
        {
            Transform taxi = FindTransformDeep(scene, "Cocoon Autonomous Taxi");
            if (taxi == null || !TryGetRendererBounds(taxi, out Bounds bounds))
            {
                return new VehicleDimensions { Width = 1.65f * experienceScale, Length = 3.9f * experienceScale };
            }

            float width = Mathf.Min(bounds.size.x, bounds.size.z);
            float length = Mathf.Max(bounds.size.x, bounds.size.z);
            return new VehicleDimensions
            {
                Width = Mathf.Max(0.05f, width),
                Length = Mathf.Max(0.08f, length)
            };
        }

        private static float EstimateVehicleLength(GameObject vehicle, float experienceScale)
        {
            return TryGetRendererBounds(vehicle.transform, out Bounds bounds) ? Mathf.Max(0.04f, Mathf.Max(bounds.size.x, bounds.size.z)) : 3f * experienceScale;
        }

        private static float EstimateLaneY(List<RoadTile> tiles, float experienceScale)
        {
            var values = new List<float>();
            for (int i = 0; i < tiles.Count; i++)
            {
                values.Add(tiles[i].Bounds.max.y + Mathf.Max(0.003f, 0.045f * experienceScale));
            }

            return Median(values);
        }

        private static float EstimateLaneOffset(float roadWidth, float taxiWidth)
        {
            float minimum = Mathf.Max(0.35f, taxiWidth * 0.38f);
            float preferred = roadWidth * 0.24f;
            float maximum = Mathf.Max(minimum, roadWidth * 0.36f);
            return Mathf.Clamp(preferred, minimum, maximum);
        }

        private static float EstimateMedianMinorExtent(List<RoadTile> tiles)
        {
            return Median(tiles.Select(tile => Mathf.Min(tile.Bounds.size.x, tile.Bounds.size.z)).Where(value => value > 0.01f).ToList());
        }

        private static float EstimateMedianMajorExtent(List<RoadTile> tiles)
        {
            return Median(tiles.Select(tile => Mathf.Max(tile.Bounds.size.x, tile.Bounds.size.z)).Where(value => value > 0.01f).ToList());
        }

        private static float EstimateTypicalSpacing(List<RoadTile> tiles)
        {
            var distances = new List<float>();
            for (int i = 0; i < tiles.Count; i++)
            {
                float nearest = float.MaxValue;
                for (int j = 0; j < tiles.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(tiles[i].Center, tiles[j].Center);
                    if (distance > 0.05f && distance < nearest)
                    {
                        nearest = distance;
                    }
                }

                if (nearest < float.MaxValue)
                {
                    distances.Add(nearest);
                }
            }

            return distances.Count > 0 ? Median(distances) : EstimateMedianMajorExtent(tiles);
        }

        private static Vector3 CalculateCenter(List<RoadTile> tiles)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < tiles.Count; i++)
            {
                center += tiles[i].Center;
            }

            return tiles.Count > 0 ? center / tiles.Count : Vector3.zero;
        }

        private static List<float> BuildClusters(IEnumerable<float> values, float tolerance)
        {
            var clusters = new List<float>();
            foreach (float value in values.OrderBy(value => value))
            {
                if (clusters.Count == 0 || Mathf.Abs(value - clusters[clusters.Count - 1]) > tolerance)
                {
                    clusters.Add(value);
                }
                else
                {
                    clusters[clusters.Count - 1] = (clusters[clusters.Count - 1] + value) * 0.5f;
                }
            }

            return clusters;
        }

        private static int FindNearestCluster(float value, List<float> clusters)
        {
            int nearest = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < clusters.Count; i++)
            {
                float distance = Mathf.Abs(value - clusters[i]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static float Median(List<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return 1f;
            }

            values.Sort();
            int middle = values.Count / 2;
            return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) * 0.5f : values[middle];
        }

        private static float Percentile(List<float> sortedValues, float percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0)
            {
                return 0f;
            }

            float index = Mathf.Clamp01(percentile) * (sortedValues.Count - 1);
            int lower = Mathf.FloorToInt(index);
            int upper = Mathf.CeilToInt(index);
            if (lower == upper)
            {
                return sortedValues[lower];
            }

            return Mathf.Lerp(sortedValues[lower], sortedValues[upper], index - lower);
        }

        private static List<int> FindShortestPath(Dictionary<int, List<int>> adjacency, int start, int target)
        {
            if (start == target)
            {
                return new List<int> { start };
            }

            var queue = new Queue<int>();
            var previous = new Dictionary<int, int>();
            queue.Enqueue(start);
            previous[start] = -1;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!adjacency.TryGetValue(current, out List<int> neighbors))
                {
                    continue;
                }

                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];
                    if (previous.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    previous[neighbor] = current;
                    if (neighbor == target)
                    {
                        return ReconstructPath(previous, target);
                    }

                    queue.Enqueue(neighbor);
                }
            }

            return new List<int>();
        }

        private static List<int> ReconstructPath(Dictionary<int, int> previous, int target)
        {
            var path = new List<int>();
            int current = target;
            while (current >= 0)
            {
                path.Add(current);
                current = previous.ContainsKey(current) ? previous[current] : -1;
            }

            path.Reverse();
            return path;
        }

        private static void AppendPath(List<int> route, List<int> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                AppendIndex(route, path[i]);
            }
        }

        private static void AppendIndex(List<int> route, int index)
        {
            if (route.Count == 0 || route[route.Count - 1] != index)
            {
                route.Add(index);
            }
        }

        private static void RemoveConsecutiveDuplicates(List<int> route)
        {
            for (int i = route.Count - 1; i > 0; i--)
            {
                if (route[i] == route[i - 1])
                {
                    route.RemoveAt(i);
                }
            }
        }

        private static int FindNearestPathIndex(CocoonTrafficLanePath path, Vector3 position)
        {
            if (path == null || path.Count == 0)
            {
                return -1;
            }

            int nearest = 0;
            float nearestDistance = float.MaxValue;
            position.y = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                Transform waypoint = path.GetWaypoint(i);
                if (waypoint == null)
                {
                    continue;
                }

                Vector3 waypointPosition = waypoint.position;
                waypointPosition.y = 0f;
                float distance = Vector3.SqrMagnitude(position - waypointPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static Quaternion RotationToward(Vector3 from, Vector3 to, Quaternion fallback)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : fallback;
        }

        private static void AddEdge(Dictionary<int, List<int>> adjacency, int a, int b)
        {
            if (!adjacency[a].Contains(b))
            {
                adjacency[a].Add(b);
            }
        }

        private static bool TryGetRoadGroupBounds(Transform roadRoot, out Bounds bounds)
        {
            bounds = new Bounds(roadRoot != null ? roadRoot.position : Vector3.zero, Vector3.zero);
            if (roadRoot == null || !roadRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (TryGetPrimaryRoadSurfaceBounds(roadRoot, out bounds))
            {
                return true;
            }

            bool hasBounds = false;
            if (TryGetRoadTileBounds(roadRoot, out Bounds directBounds))
            {
                bounds = directBounds;
                hasBounds = true;
            }

            Transform[] transforms = roadRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform child = transforms[i];
                if (child == null || child == roadRoot || !child.gameObject.activeInHierarchy || !IsPrimaryRoadSurfaceName(child.name))
                {
                    continue;
                }

                if (!TryGetRoadTileBounds(child, out Bounds childBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = childBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(childBounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetPrimaryRoadSurfaceBounds(Transform roadRoot, out Bounds bounds)
        {
            bounds = new Bounds(roadRoot != null ? roadRoot.position : Vector3.zero, Vector3.zero);
            if (roadRoot == null)
            {
                return false;
            }

            Transform best = null;
            int bestScore = int.MaxValue;
            Transform[] transforms = roadRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform child = transforms[i];
                if (child == null || child == roadRoot || !child.gameObject.activeInHierarchy || !IsPrimaryRoadSurfaceName(child.name))
                {
                    continue;
                }

                int score = GetRoadSurfaceDepth(roadRoot, child) * 10;
                if (child.name.EndsWith("_1", StringComparison.OrdinalIgnoreCase))
                {
                    score -= 6;
                }

                if (score < bestScore)
                {
                    best = child;
                    bestScore = score;
                }
            }

            return best != null && TryGetRoadTileBounds(best, out bounds);
        }

        private static int GetRoadSurfaceDepth(Transform root, Transform child)
        {
            int depth = 0;
            Transform current = child;
            while (current != null && current != root)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static bool IsPrimaryRoadSurfaceName(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("jcRoad", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("22m") || lower.Contains("cross");
        }

        private static bool TryGetRoadTileBounds(Transform tile, out Bounds bounds)
        {
            bounds = new Bounds(tile.position, Vector3.zero);
            if (tile == null || !tile.gameObject.activeInHierarchy)
            {
                return false;
            }

            Renderer renderer = tile.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled)
            {
                bounds = renderer.bounds;
                return true;
            }

            MeshFilter meshFilter = tile.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Bounds localBounds = meshFilter.sharedMesh.bounds;
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
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

            bounds = new Bounds(tile.TransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
            {
                bounds.Encapsulate(tile.TransformPoint(corners[i]));
            }

            return true;
        }

        private static bool TryGetLocalRendererBounds(Transform root, out Bounds localBounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            localBounds = new Bounds(Vector3.zero, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
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
                    Vector3 localPoint = root.InverseTransformPoint(renderer.transform.TransformPoint(corners[cornerIndex]));
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

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(root.position, Vector3.zero);
            bool hasBounds = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
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

            return hasBounds;
        }

        private static GameObject CreateCubeChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return cube;
        }

        private static GameObject CreateCylinderChild(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localRotation = localRotation;
            cylinder.transform.localScale = localScale;
            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return cylinder;
        }

        private static Material LoadMaterial(string materialName, Color fallbackColor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/CocoonPrototype/Materials/" + materialName + ".mat");
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            material.name = materialName + " Runtime Fallback";
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", fallbackColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", fallbackColor);
            }

            return material;
        }

        private static Transform FindOrCreateRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }
            }

            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject.transform;
        }

        private static Transform FindRoadmapRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root != null && root.name == RoadmapRootName)
                {
                    return root.transform;
                }
            }

            Transform nested = FindTransformDeep(scene, RoadmapRootName);
            if (nested != null)
            {
                Debug.LogWarning("Nested ROADMAP ignored. Move the active ROADMAP object to the scene root so traffic generation uses only the selected top-level road network.", nested);
            }

            return null;
        }

        private static void NormalizeRootTransformPreservingChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            bool isIdentity =
                root.parent == null &&
                Vector3.Distance(root.position, Vector3.zero) < 0.0001f &&
                Quaternion.Angle(root.rotation, Quaternion.identity) < 0.01f &&
                Vector3.Distance(root.localScale, Vector3.one) < 0.0001f;
            if (isIdentity)
            {
                return;
            }

            var children = new List<Transform>();
            for (int i = 0; i < root.childCount; i++)
            {
                children.Add(root.GetChild(i));
            }

            Transform previousParent = root.parent;
            for (int i = 0; i < children.Count; i++)
            {
                children[i].SetParent(previousParent, true);
            }

            root.SetParent(null, true);
            root.position = Vector3.zero;
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;
            for (int i = 0; i < children.Count; i++)
            {
                children[i].SetParent(root, true);
            }

            Debug.Log("Normalized " + root.name + " root transform while preserving child world positions and sizes.", root);
        }

        private static Transform FindTransformDeep(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i] != null && transforms[i].name == name)
                    {
                        return transforms[i];
                    }
                }
            }

            return null;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindChildDeep(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static T FindComponentDeep<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static int CountTransformsNamed(Transform root, string name)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                {
                    count++;
                }
            }

            return count;
        }

        private static void RemoveChildIfPresent(Transform parent, string childName)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == childName)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static bool IsChildOf(Transform transform, Transform potentialParent)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current == potentialParent)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
