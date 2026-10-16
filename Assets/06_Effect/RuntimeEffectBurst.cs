using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Effect
{
    /// <summary>
    /// 程序化特效实例。向对象池申请后自行扩张、旋转并按生命周期淡出，
    /// 结束时归还对象池。用于在没有美术预制体时提供可见的命中与终极技表现。
    /// </summary>
    public class RuntimeEffectBurst : MonoBehaviour, IPoolable
    {
        public float Lifetime = 0.32f;
        public float StartScale = 0.4f;
        public float EndScale = 1.6f;
        public float Spin = 40f;

        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;
        private float _elapsed;
        private bool _finished;

        private void Awake()
        {
            CacheRenderers();
        }

        /// <summary>缓存子渲染器与基准色，供逐帧淡出计算。</summary>
        public void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = _renderers[i].color;
            }
        }

        public void OnSpawn()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                CacheRenderers();
            }

            _elapsed = 0f;
            _finished = false;
            transform.localScale = Vector3.one * StartScale;
            ApplyAlpha(1f);
        }

        public void OnDespawn()
        {
            _elapsed = 0f;
            _finished = false;
        }

        private void Update()
        {
            if (_finished)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var progress = Lifetime > 0.0001f ? Mathf.Clamp01(_elapsed / Lifetime) : 1f;

            transform.localScale = Vector3.one * Mathf.Lerp(StartScale, EndScale, progress);
            if (Spin != 0f)
            {
                transform.Rotate(0f, 0f, Spin * Time.deltaTime);
            }

            ApplyAlpha(1f - progress);

            if (progress >= 1f)
            {
                Finish();
            }
        }

        private void ApplyAlpha(float alpha)
        {
            if (_renderers == null)
            {
                return;
            }

            var clamped = Mathf.Clamp01(alpha);
            for (var i = 0; i < _renderers.Length; i++)
            {
                var color = _baseColors[i];
                color.a = _baseColors[i].a * clamped;
                _renderers[i].color = color;
            }
        }

        private void Finish()
        {
            _finished = true;
            if (PoolManager.HasInstance && GetComponent<PooledObject>() != null)
            {
                PoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
