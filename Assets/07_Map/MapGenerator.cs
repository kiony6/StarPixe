using System.Collections.Generic;
using StarPixelSurge.Config;
using UnityEngine;

namespace StarPixelSurge.Map
{
    /// <summary>
    /// 肉鸽式随机房间地图生成器。
    /// 采用网格随机游走放置房间，走廊连接相邻房间，生成瓦片地板网格并烘焙为单张像素贴图，
    /// 同时沿地板边界合并生成墙体碰撞。返回带 RoomMap 组件的根节点。
    /// </summary>
    public static class MapGenerator
    {
        private struct Edge
        {
            public RectInt A;
            public RectInt B;
        }

        public static RoomMap Generate(RoomMapConfig config, int seed, Transform parent = null)
        {
            if (config == null)
            {
                Debug.LogError("[MapGenerator] RoomMapConfig is null.");
                return null;
            }

            var random = new System.Random(seed);

            var cellW = config.RoomWidth + config.CorridorLength;
            var cellH = config.RoomHeight + config.CorridorLength;
            var gridW = Mathf.Max(3, config.GridColumns);
            var gridH = Mathf.Max(3, config.GridRows);

            var placed = new HashSet<Vector2Int>();
            var order = new List<Vector2Int>();
            var edges = new List<Edge>();

            var startCell = new Vector2Int(gridW / 2, gridH / 2);
            placed.Add(startCell);
            order.Add(startCell);

            var targetRooms = Next(random, config.MinRooms, config.MaxRooms + 1);
            var guard = 0;
            while (order.Count < targetRooms && guard++ < 2000)
            {
                var parentCell = order[random.Next(order.Count)];
                var neighbour = RandomFreeNeighbour(parentCell, placed, gridW, gridH, random);
                if (neighbour == null)
                {
                    continue;
                }

                var child = neighbour.Value;
                placed.Add(child);
                order.Add(child);
                edges.Add(new Edge
                {
                    A = CellRect(parentCell, cellW, cellH, config),
                    B = CellRect(child, cellW, cellH, config)
                });
            }

            var mapW = gridW * cellW;
            var mapH = gridH * cellH;

            var roomTypes = AssignTypes(order, startCell, random);
            var rooms = new List<Room>(order.Count);
            for (var i = 0; i < order.Count; i++)
            {
                rooms.Add(new Room(CellRect(order[i], cellW, cellH, config), roomTypes[i]));
            }

            var floor = new bool[mapW, mapH];
            foreach (var room in rooms)
            {
                CarveRect(room.TileRect, floor, mapW, mapH);
            }

            foreach (var edge in edges)
            {
                CarveCorridor(edge.A, edge.B, config.CorridorWidth, floor, mapW, mapH);
            }

            var startRoom = rooms[0];
            for (var i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].Type == RoomType.Start)
                {
                    startRoom = rooms[i];
                    break;
                }
            }

            var root = new GameObject("RoomMap");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            var map = root.AddComponent<RoomMap>();
            map.Initialize(floor, mapW, mapH, rooms, startRoom.WorldCenter);

            RenderMapTexture(root.transform, floor, rooms, config, mapW, mapH);

            if (config.GenerateWallColliders)
            {
                BuildWallColliders(root.transform, floor, mapW, mapH);
            }

            return map;
        }

        private static RectInt CellRect(Vector2Int cell, int cellW, int cellH, RoomMapConfig config)
        {
            return new RectInt(cell.x * cellW, cell.y * cellH, config.RoomWidth, config.RoomHeight);
        }

        private static Vector2Int? RandomFreeNeighbour(Vector2Int cell, HashSet<Vector2Int> placed, int gridW, int gridH, System.Random random)
        {
            var candidates = new List<Vector2Int>(4);
            TryAdd(candidates, new Vector2Int(cell.x + 1, cell.y), placed, gridW, gridH);
            TryAdd(candidates, new Vector2Int(cell.x - 1, cell.y), placed, gridW, gridH);
            TryAdd(candidates, new Vector2Int(cell.x, cell.y + 1), placed, gridW, gridH);
            TryAdd(candidates, new Vector2Int(cell.x, cell.y - 1), placed, gridW, gridH);

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates[random.Next(candidates.Count)];
        }

        private static void TryAdd(List<Vector2Int> list, Vector2Int cell, HashSet<Vector2Int> placed, int gridW, int gridH)
        {
            if (cell.x >= 0 && cell.y >= 0 && cell.x < gridW && cell.y < gridH && !placed.Contains(cell))
            {
                list.Add(cell);
            }
        }

        private static List<RoomType> AssignTypes(List<Vector2Int> order, Vector2Int startCell, System.Random random)
        {
            var types = new List<RoomType>(order.Count);
            for (var i = 0; i < order.Count; i++)
            {
                types.Add(RoomType.Normal);
            }

            var startIndex = order.IndexOf(startCell);
            if (startIndex >= 0)
            {
                types[startIndex] = RoomType.Start;
            }

            var bossIndex = -1;
            var maxDistance = -1;
            for (var i = 0; i < order.Count; i++)
            {
                if (i == startIndex)
                {
                    continue;
                }

                var distance = Mathf.Abs(order[i].x - startCell.x) + Mathf.Abs(order[i].y - startCell.y);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    bossIndex = i;
                }
            }

            if (bossIndex >= 0)
            {
                types[bossIndex] = RoomType.Boss;
            }

            var special = new List<int>();
            for (var i = 0; i < order.Count; i++)
            {
                if (i != startIndex && i != bossIndex)
                {
                    special.Add(i);
                }
            }

            Shuffle(special, random);
            if (special.Count > 0)
            {
                types[special[0]] = RoomType.Treasure;
            }

            if (special.Count > 1)
            {
                types[special[1]] = RoomType.Elite;
            }

            return types;
        }

        private static void CarveRect(RectInt rect, bool[,] floor, int width, int height)
        {
            for (var x = rect.xMin; x < rect.xMax; x++)
            {
                for (var y = rect.yMin; y < rect.yMax; y++)
                {
                    if (x >= 0 && y >= 0 && x < width && y < height)
                    {
                        floor[x, y] = true;
                    }
                }
            }
        }

        private static void CarveCorridor(RectInt a, RectInt b, int corridorWidth, bool[,] floor, int width, int height)
        {
            var half = Mathf.Max(0, corridorWidth / 2);

            if (a.x != b.x)
            {
                var left = a.x < b.x ? a : b;
                var right = a.x < b.x ? b : a;
                var yCenter = a.y + a.height / 2;
                var xStart = left.xMax - 1;
                var xEnd = right.xMin + 1;
                for (var x = xStart; x <= xEnd; x++)
                {
                    for (var dy = -half; dy <= half; dy++)
                    {
                        var y = yCenter + dy;
                        if (x >= 0 && y >= 0 && x < width && y < height)
                        {
                            floor[x, y] = true;
                        }
                    }
                }
            }
            else
            {
                var bottom = a.y < b.y ? a : b;
                var top = a.y < b.y ? b : a;
                var xCenter = a.x + a.width / 2;
                var yStart = bottom.yMax - 1;
                var yEnd = top.yMin + 1;
                for (var y = yStart; y <= yEnd; y++)
                {
                    for (var dx = -half; dx <= half; dx++)
                    {
                        var x = xCenter + dx;
                        if (x >= 0 && y >= 0 && x < width && y < height)
                        {
                            floor[x, y] = true;
                        }
                    }
                }
            }
        }

        private static void RenderMapTexture(Transform parent, bool[,] floor, List<Room> rooms, RoomMapConfig config, int width, int height)
        {
            var tp = Mathf.Max(1, config.TilePixels);
            var texture = new Texture2D(width * tp, height * tp, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * tp * height * tp];

            for (var ty = 0; ty < height; ty++)
            {
                for (var tx = 0; tx < width; tx++)
                {
                    var color = ResolveTileColor(floor, rooms, config, tx, ty, width, height);

                    for (var py = 0; py < tp; py++)
                    {
                        var y = ty * tp + py;
                        for (var px = 0; px < tp; px++)
                        {
                            var x = tx * tp + px;
                            pixels[y * width * tp + x] = color;
                        }
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width * tp, height * tp),
                new Vector2(0f, 0f),
                tp);

            var go = new GameObject("MapSprite");
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -10;
        }

        private static Color32 ResolveTileColor(bool[,] floor, List<Room> rooms, RoomMapConfig config, int tx, int ty, int width, int height)
        {
            if (floor[tx, ty])
            {
                var baseColor = config.CorridorColor;
                var tile = new Vector2Int(tx, ty);
                foreach (var room in rooms)
                {
                    if (room.TileRect.Contains(tile))
                    {
                        baseColor = RoomColor(room.Type, config);
                        break;
                    }
                }

                var variation = ((tx + ty) & 1) == 0 ? 1f : 0.92f;
                return Tint(baseColor, variation);
            }

            if (HasFloorNeighbour(floor, tx, ty, width, height))
            {
                return config.WallColor;
            }

            return config.VoidColor;
        }

        private static Color RoomColor(RoomType type, RoomMapConfig config)
        {
            switch (type)
            {
                case RoomType.Start:
                    return config.StartRoomColor;
                case RoomType.Treasure:
                    return config.TreasureRoomColor;
                case RoomType.Elite:
                    return config.EliteRoomColor;
                case RoomType.Boss:
                    return config.BossRoomColor;
                default:
                    return config.FloorColor;
            }
        }

        private static Color32 Tint(Color color, float factor)
        {
            return new Color32(
                (byte)Mathf.Clamp(color.r * factor * 255f, 0f, 255f),
                (byte)Mathf.Clamp(color.g * factor * 255f, 0f, 255f),
                (byte)Mathf.Clamp(color.b * factor * 255f, 0f, 255f),
                (byte)Mathf.Clamp(color.a * 255f, 0f, 255f));
        }

        private static bool HasFloorNeighbour(bool[,] floor, int x, int y, int width, int height)
        {
            return IsFloorSafe(floor, x + 1, y, width, height)
                   || IsFloorSafe(floor, x - 1, y, width, height)
                   || IsFloorSafe(floor, x, y + 1, width, height)
                   || IsFloorSafe(floor, x, y - 1, width, height);
        }

        private static bool IsFloorSafe(bool[,] floor, int x, int y, int width, int height)
        {
            return x >= 0 && y >= 0 && x < width && y < height && floor[x, y];
        }

        private static void BuildWallColliders(Transform parent, bool[,] floor, int width, int height)
        {
            var walls = new GameObject("Walls");
            walls.transform.SetParent(parent, false);
            walls.transform.position = Vector3.zero;

            for (var y = 0; y < height; y++)
            {
                var runStart = -1;
                for (var x = 0; x <= width; x++)
                {
                    var isWall = x < width && !floor[x, y] && HasFloorNeighbour(floor, x, y, width, height);
                    if (isWall && runStart < 0)
                    {
                        runStart = x;
                    }
                    else if (!isWall && runStart >= 0)
                    {
                        CreateWallBox(walls.transform, runStart, x - runStart, y);
                        runStart = -1;
                    }
                }
            }
        }

        private static void CreateWallBox(Transform parent, int startX, int runLength, int y)
        {
            var go = new GameObject("Wall");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(startX + runLength * 0.5f, y + 0.5f, 0f);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(runLength, 1f);
        }

        private static int Next(System.Random random, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return random.Next(minInclusive, maxExclusive);
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
