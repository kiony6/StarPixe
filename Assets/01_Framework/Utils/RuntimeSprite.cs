using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Framework
{
    /// <summary>
    /// 运行时程序化占位贴图工具。在未接入真实美术资源前，按形状与颜色生成像素风 Sprite，
    /// 为怪物、弹道、掉落物等提供可区分的视觉占位。生成的 Sprite 按参数缓存复用，
    /// 避免对象池反复生成贴图带来的开销。
    /// </summary>
    public static class RuntimeSprite
    {
        public enum Shape
        {
            Square = 0,
            Circle = 1,
            Diamond = 2,
            Triangle = 3,
            Ring = 4,
            Hexagon = 5,
            Star = 6
        }

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite CreateSquare(Color color, int size = 8)
        {
            return Get(Shape.Square, color, size);
        }

        public static Sprite CreateCircle(Color color, int size = 8)
        {
            return Get(Shape.Circle, color, size);
        }

        public static Sprite CreateDiamond(Color color, int size = 8)
        {
            return Get(Shape.Diamond, color, size);
        }

        public static Sprite CreateTriangle(Color color, int size = 8)
        {
            return Get(Shape.Triangle, color, size);
        }

        public static Sprite CreateRing(Color color, int size = 10)
        {
            return Get(Shape.Ring, color, size);
        }

        public static Sprite CreateHexagon(Color color, int size = 10)
        {
            return Get(Shape.Hexagon, color, size);
        }

        public static Sprite CreateStar(Color color, int size = 11)
        {
            return Get(Shape.Star, color, size);
        }

        /// <summary>按形状生成（或从缓存取回）Sprite。size 为像素边长，且同时作为每单位像素数。</summary>
        public static Sprite Get(Shape shape, Color color, int size)
        {
            size = Mathf.Clamp(size, 4, 64);
            var key = BuildKey(shape, color, size);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Build(shape, color, size);
            Cache[key] = sprite;
            return sprite;
        }

        private static string BuildKey(Shape shape, Color color, int size)
        {
            var c = (Color32)color;
            return $"{(int)shape}:{size}:{c.r},{c.g},{c.b},{c.a}";
        }

        private static Sprite Build(Shape shape, Color color, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            var radius = center;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    pixels[y * size + x] = Inside(shape, dx, dy, radius) ? color : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static bool Inside(Shape shape, float dx, float dy, float radius)
        {
            switch (shape)
            {
                case Shape.Circle:
                    return dx * dx + dy * dy <= radius * radius;
                case Shape.Diamond:
                    return Mathf.Abs(dx) + Mathf.Abs(dy) <= radius;
                case Shape.Triangle:
                    // 尖端朝上的等腰三角形
                    return dy >= -radius && dy <= radius
                           && Mathf.Abs(dx) <= (radius - (dy + radius) * 0.5f) * 0.9f + 1f
                           && Mathf.Abs(dx) <= radius;
                case Shape.Ring:
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    return dist <= radius && dist >= radius * 0.55f;
                case Shape.Hexagon:
                    var ax = Mathf.Abs(dx);
                    var ay = Mathf.Abs(dy);
                    return ay <= radius && ax <= radius && (ax + ay * 0.577f) <= radius * 1.15f;
                case Shape.Star:
                    return StarMask(dx, dy, radius);
                default:
                    return true;
            }
        }

        /// <summary>五角星掩码：由极角半径曲线判定像素是否落在星形内。</summary>
        private static bool StarMask(float dx, float dy, float radius)
        {
            var dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist > radius)
            {
                return false;
            }

            var angle = Mathf.Atan2(dy, dx);
            var lobes = Mathf.Cos(angle * 5f);
            var threshold = radius * (0.5f + 0.5f * lobes * lobes);
            return dist <= threshold;
        }
    }
}
