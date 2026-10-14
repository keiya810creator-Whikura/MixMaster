using System;
using System.Collections.Generic;
using UnityEngine;

namespace MixMaster.Core
{
    /// <summary>
    /// Procedural room coordinates and connected walkable floor cells.
    /// Contains no scene/GameObject state; future boss/monster placement
    /// can use the start room, deepest room and the room center cells.
    /// </summary>
    public sealed class DungeonRoom
    {
        public int Index { get; internal set; }
        public Vector2Int GridPosition { get; internal set; }
        public Vector2Int CenterCell { get; internal set; }
        public int DistanceFromEntrance { get; internal set; }
        public bool IsBossRoom { get; internal set; }
    }

    public sealed class DungeonLayout
    {
        public readonly List<DungeonRoom> Rooms = new List<DungeonRoom>();
        public readonly HashSet<Vector2Int> FloorCells = new HashSet<Vector2Int>();
        public readonly List<Vector2Int> ConnectedRoomPairs =
            new List<Vector2Int>();

        public DungeonRoom EntranceRoom => Rooms.Count > 0 ? Rooms[0] : null;
        public DungeonRoom BossRoom { get; internal set; }
    }

    public static class DungeonLayoutBuilder
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right,
            Vector2Int.down, Vector2Int.left
        };

        /// <summary>
        /// Creates 10x10 rooms joined by 3-wide corridors by default.
        /// Room centers lie roomSize + gap cells apart on a sparse grid.
        /// Every new room joins an existing room, so the map is connected
        /// even when there are zero optional extra branch connections.
        /// </summary>
        public static DungeonLayout Generate(
            DungeonGenerationSettings settings, int roomSize = 10,
            int gap = 6, int corridorWidth = 3)
        {
            if (settings == null)
                settings = new DungeonGenerationSettings();

            int roomCount = Mathf.Clamp(settings.roomCount, 2, 64);
            roomSize = Mathf.Clamp(roomSize, 6, 24);
            gap = Mathf.Clamp(gap, 2, 32);
            corridorWidth = Mathf.Clamp(corridorWidth, 1,
                Mathf.Min(7, roomSize - 2));
            int spacing = roomSize + gap;

            var random = new System.Random(
                settings.seed != 0 ? settings.seed : 1);
            var layout = new DungeonLayout();
            var usedGrid = new Dictionary<Vector2Int, int>();
            AddRoom(layout, usedGrid, Vector2Int.zero, spacing);

            if (settings.forceLinearPath)
            {
                // Each step increases X or Y monotonically, so the
                // route is always a single non-intersecting chain.
                // It may turn, but can never fork or loop.
                int horizontalSign = random.Next(2) == 0 ? -1 : 1;
                int verticalSign = random.Next(2) == 0 ? -1 : 1;
                for (int i = 1; i < roomCount; i++)
                {
                    Vector2Int direction = random.Next(2) == 0
                        ? new Vector2Int(horizontalSign, 0)
                        : new Vector2Int(0, verticalSign);
                    Vector2Int nextGrid =
                        layout.Rooms[i - 1].GridPosition + direction;
                    int next = AddRoom(layout, usedGrid, nextGrid, spacing);
                    Connect(layout, i - 1, next, corridorWidth);
                }

                FillRooms(layout, roomSize);
                MarkDeepestRoom(layout);
                return layout;
            }

            // Randomized tree growth (with deterministic fallback) guarantees
            // every room is reachable and no rooms overlap.
            while (layout.Rooms.Count < roomCount)
            {
                int from = -1;
                Vector2Int gridCell = default;
                for (int tries = 0; tries < 64; tries++)
                {
                    int candidate = random.Next(layout.Rooms.Count);
                    Vector2Int pos = layout.Rooms[candidate].GridPosition +
                        Directions[random.Next(Directions.Length)];
                    if (usedGrid.ContainsKey(pos)) continue;
                    from = candidate;
                    gridCell = pos;
                    break;
                }

                if (from < 0)
                {
                    // Occupied-neighbor exhaustion is unlikely but must
                    // never leave dungeon generation stuck in a loop.
                    for (int i = 0; i < layout.Rooms.Count && from < 0; i++)
                        for (int d = 0; d < Directions.Length; d++)
                        {
                            Vector2Int pos =
                                layout.Rooms[i].GridPosition + Directions[d];
                            if (usedGrid.ContainsKey(pos)) continue;
                            from = i;
                            gridCell = pos;
                            break;
                        }
                }

                if (from < 0) throw new InvalidOperationException(
                    "Unable to find a free dungeon room location.");

                int to = AddRoom(layout, usedGrid, gridCell, spacing);
                Connect(layout, from, to, corridorWidth);
            }

            // Optional loops offer exploration choices without producing
            // disconnected islands or duplicate corridors.
            var candidates = new List<Vector2Int>();
            for (int i = 0; i < layout.Rooms.Count; i++)
                for (int d = 0; d < Directions.Length; d++)
                {
                    Vector2Int neighborGrid =
                        layout.Rooms[i].GridPosition + Directions[d];
                    if (!usedGrid.TryGetValue(neighborGrid, out int j) ||
                        i >= j || HasEdge(layout, i, j))
                        continue;
                    candidates.Add(new Vector2Int(i, j));
                }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                (candidates[i], candidates[swap]) =
                    (candidates[swap], candidates[i]);
            }

            int extra = Mathf.Clamp(settings.branchCount, 0, candidates.Count);
            for (int i = 0; i < extra; i++)
                Connect(layout, candidates[i].x, candidates[i].y,
                    corridorWidth);

            FillRooms(layout, roomSize);
            MarkDeepestRoom(layout);
            return layout;
        }

        private static int AddRoom(DungeonLayout layout,
            Dictionary<Vector2Int, int> usedGrid,
            Vector2Int gridPos, int spacing)
        {
            int index = layout.Rooms.Count;
            Vector2Int center = gridPos * spacing;
            layout.Rooms.Add(new DungeonRoom
            {
                Index = index,
                GridPosition = gridPos,
                CenterCell = center
            });
            usedGrid.Add(gridPos, index);
            return index;
        }

        private static void Connect(DungeonLayout layout,
            int firstIndex, int secondIndex, int corridorWidth)
        {
            Vector2Int start = layout.Rooms[firstIndex].CenterCell;
            Vector2Int end = layout.Rooms[secondIndex].CenterCell;
            int low = -(corridorWidth / 2);
            int high = low + corridorWidth;

            if (start.x == end.x)
            {
                int min = Mathf.Min(start.y, end.y);
                int max = Mathf.Max(start.y, end.y);
                for (int y = min; y <= max; y++)
                    for (int dx = low; dx < high; dx++)
                        layout.FloorCells.Add(new Vector2Int(start.x + dx, y));
            }
            else if (start.y == end.y)
            {
                int min = Mathf.Min(start.x, end.x);
                int max = Mathf.Max(start.x, end.x);
                for (int x = min; x <= max; x++)
                    for (int dy = low; dy < high; dy++)
                        layout.FloorCells.Add(new Vector2Int(x, start.y + dy));
            }
            else
            {
                throw new InvalidOperationException(
                    "Dungeon rooms must connect along a cardinal direction.");
            }

            layout.ConnectedRoomPairs.Add(
                new Vector2Int(firstIndex, secondIndex));
        }

        private static bool HasEdge(DungeonLayout layout, int a, int b)
        {
            for (int i = 0; i < layout.ConnectedRoomPairs.Count; i++)
            {
                Vector2Int edge = layout.ConnectedRoomPairs[i];
                if ((edge.x == a && edge.y == b) ||
                    (edge.x == b && edge.y == a))
                    return true;
            }
            return false;
        }

        private static void MarkDeepestRoom(DungeonLayout layout)
        {
            int count = layout.Rooms.Count;
            var distance = new int[count];
            for (int i = 0; i < distance.Length; i++) distance[i] = -1;
            var queue = new Queue<int>();
            queue.Enqueue(0);
            distance[0] = 0;

            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                foreach (Vector2Int edge in layout.ConnectedRoomPairs)
                {
                    int neighbor = edge.x == node ? edge.y :
                        edge.y == node ? edge.x : -1;
                    if (neighbor < 0 || distance[neighbor] >= 0) continue;
                    distance[neighbor] = distance[node] + 1;
                    queue.Enqueue(neighbor);
                }
            }

            int deepest = 0;
            for (int i = 0; i < count; i++)
            {
                if (distance[i] < 0)
                    throw new InvalidOperationException("Disconnected dungeon.");
                layout.Rooms[i].DistanceFromEntrance = distance[i];
                if (distance[i] > distance[deepest])
                    deepest = i;
            }

            layout.BossRoom = layout.Rooms[deepest];
            layout.BossRoom.IsBossRoom = true;
        }

        /// <summary>
        /// Adds the actual square room floors after corridors are placed.
        /// Keeping floor cells in a set eliminates all overlapping tiles.
        /// </summary>
        public static void FillRooms(DungeonLayout layout, int roomSize = 10)
        {
            if (layout == null) return;
            roomSize = Mathf.Clamp(roomSize, 6, 24);
            int half = roomSize / 2;
            foreach (DungeonRoom room in layout.Rooms)
                for (int y = 0; y < roomSize; y++)
                    for (int x = 0; x < roomSize; x++)
                        layout.FloorCells.Add(new Vector2Int(
                            room.CenterCell.x + x - half,
                            room.CenterCell.y + y - half));
        }
    }
}
