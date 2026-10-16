using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.GameCore
{
    /// <summary>
    /// 技能效果生成器：生成各种技能特效。
    /// </summary>
    public static class SkillEffectSpawner
    {
        /// <summary>生成分裂弹幕。</summary>
        public static void SpawnSplitProjectiles(Vector2 position, Vector2 direction, int count, float spreadAngle, float damage)
        {
            var baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            for (var i = 0; i < count; i++)
            {
                var angle = baseAngle - spreadAngle / 2f + (spreadAngle / (count - 1)) * i;
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                SpawnSingleProjectile(position, dir, damage, Color.cyan);
            }
        }

        /// <summary>生成环形弹幕（新星）。</summary>
        public static void SpawnNovaWave(Vector2 position, int count, float damage)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = (360f / count) * i;
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                SpawnSingleProjectile(position, dir, damage, Color.magenta);
            }
        }

        /// <summary>生成激光束（占位：用长条方块代替）。</summary>
        public static GameObject SpawnLaserBeam(Vector2 start, Vector2 direction, float length, float damage)
        {
            var go = new GameObject("LaserBeam");
            go.transform.position = start;
            go.transform.right = direction;

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 8), new Vector2(0.5f, 0.5f), 1f);
            sprite.color = new Color(0.5f, 0.8f, 1f, 0.8f);
            go.transform.localScale = new Vector3(length, 1f, 1f);

            // 自动消失
            Object.Destroy(go, 0.3f);
            return go;
        }

        /// <summary>生成链式闪电（占位：用连线代替）。</summary>
        public static void SpawnChainLightning(Vector2 from, Vector2 to, float damage)
        {
            var go = new GameObject("ChainLightning");
            go.transform.position = (from + to) / 2f;

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            sprite.color = new Color(0.8f, 0.9f, 1f, 0.9f);

            var distance = Vector2.Distance(from, to);
            var angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.transform.localScale = new Vector3(distance, 2f, 1f);

            Object.Destroy(go, 0.15f);
        }

        private static void SpawnSingleProjectile(Vector2 position, Vector2 direction, float damage, Color color)
        {
            var go = new GameObject("Projectile");
            go.transform.position = position;

            var projectile = go.AddComponent<Projectile>();
            projectile.Launch(direction, damage, 1f, "split", color, 0.8f);
        }
    }
}
