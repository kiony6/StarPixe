using System.Collections.Generic;
using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Audio
{
    /// <summary>BGM 强度分段。随波次层数递进，首领登场时进入 Boss 段。</summary>
    public enum MusicIntensity
    {
        Calm = 0,
        Battle = 1,
        Frenzy = 2,
        Boss = 3
    }

    /// <summary>
    /// 音频管理器：按强度分段切换 BGM 并交叉淡入淡出、播放音效与终极技低频音。
    /// 未注册音频资源时回退到 <see cref="RuntimeTone"/> 程序化合成，保证接入资源前后都可运行。
    /// </summary>
    public class AudioManager : MonoSingleton<AudioManager>
    {
        public const string SfxSkill = "sfx_skill";
        public const string SfxKill = "sfx_kill";
        public const string SfxLevelUp = "sfx_levelup";
        public const string SfxBoss = "sfx_boss";
        public const string SfxUltimate = "sfx_ultimate";
        public const string SfxPickup = "sfx_pickup";
        public const string SfxHurt = "sfx_hurt";

        [SerializeField] private float bgmVolume = 0.6f;
        [SerializeField] private float sfxVolume = 0.8f;
        [SerializeField] private float crossfadePerSecond = 1.6f;

        private AudioSource _bgmA;
        private AudioSource _bgmB;
        private AudioSource _bgmActive;
        private AudioSource _bgmFading;
        private AudioSource _sfxSource;

        private readonly Dictionary<string, AudioClip> _sfxClips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<int, AudioClip> _bgmClips = new Dictionary<int, AudioClip>();

        private MusicIntensity _intensity = MusicIntensity.Calm;

        public MusicIntensity CurrentIntensity => _intensity;

        protected override void OnSingletonAwake()
        {
            _bgmA = CreateSource("BgmA", true);
            _bgmB = CreateSource("BgmB", true);
            _sfxSource = CreateSource("Sfx", false);
        }

        private AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            return source;
        }

        /// <summary>注册命名音效资源，优先生效并替代程序化回退。</summary>
        public void RegisterSfx(string id, AudioClip clip)
        {
            if (string.IsNullOrEmpty(id) || clip == null)
            {
                return;
            }

            _sfxClips[id] = clip;
        }

        /// <summary>注册某强度分段的 BGM 资源。</summary>
        public void RegisterBgm(MusicIntensity intensity, AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            _bgmClips[(int)intensity] = clip;
        }

        /// <summary>按波次强度切换 BGM 分段（需求 14.1）。</summary>
        public void SetIntensity(int level)
        {
            var clamped = Mathf.Clamp(level, (int)MusicIntensity.Calm, (int)MusicIntensity.Boss);
            PlayBgm((MusicIntensity)clamped, bgmVolume);
        }

        /// <summary>播放指定强度分段的 BGM（需求 14.1），已有同段则忽略。</summary>
        public void PlayBgm(MusicIntensity intensity, float volume = -1f)
        {
            var clip = ResolveBgm(intensity);
            if (clip == null)
            {
                return;
            }

            if (volume >= 0f)
            {
                bgmVolume = Mathf.Clamp01(volume);
            }

            if (_intensity == intensity && _bgmActive != null && _bgmActive.clip == clip && _bgmActive.isPlaying)
            {
                return;
            }

            _intensity = intensity;

            var next = _bgmActive == _bgmA ? _bgmB : _bgmA;
            if (_bgmActive == null)
            {
                next = _bgmA;
            }
            else
            {
                _bgmFading = _bgmActive;
            }

            _bgmActive = next;
            _bgmActive.clip = clip;
            _bgmActive.volume = 0f;
            _bgmActive.Play();
        }

        public void PlayBgm(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }

            var next = _bgmActive == _bgmA ? _bgmB : _bgmA;
            if (_bgmActive == null)
            {
                next = _bgmA;
            }
            else
            {
                _bgmFading = _bgmActive;
            }

            _bgmActive = next;
            _bgmActive.clip = clip;
            _bgmActive.volume = 0f;
            _bgmActive.Play();
            bgmVolume = Mathf.Clamp01(volume);
        }

        public void StopBgm()
        {
            if (_bgmActive != null)
            {
                _bgmActive.Stop();
            }

            if (_bgmFading != null)
            {
                _bgmFading.Stop();
            }

            _bgmActive = null;
            _bgmFading = null;
        }

        /// <summary>播放命名音效（需求 14.2）。未注册时回退到程序化音效。</summary>
        public void PlaySfx(string id, float volume = 1f)
        {
            if (string.IsNullOrEmpty(id) || _sfxSource == null)
            {
                return;
            }

            var clip = ResolveSfx(id);
            if (clip == null)
            {
                return;
            }

            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _sfxSource == null)
            {
                return;
            }

            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
        }

        /// <summary>终极技低频音，与震屏同帧触发（需求 14.3）。</summary>
        public void PlayUltimate()
        {
            PlaySfx(SfxUltimate, 1f);
        }

        private void Update()
        {
            var step = crossfadePerSecond * Time.unscaledDeltaTime;

            if (_bgmActive != null)
            {
                _bgmActive.volume = Mathf.MoveTowards(_bgmActive.volume, bgmVolume, step);
            }

            if (_bgmFading != null)
            {
                _bgmFading.volume = Mathf.MoveTowards(_bgmFading.volume, 0f, step);
                if (_bgmFading.volume <= 0f)
                {
                    _bgmFading.Stop();
                    _bgmFading = null;
                }
            }
        }

        private AudioClip ResolveBgm(MusicIntensity intensity)
        {
            if (_bgmClips.TryGetValue((int)intensity, out var clip) && clip != null)
            {
                return clip;
            }

            switch (intensity)
            {
                case MusicIntensity.Battle:
                    return RuntimeTone.Pad(146.83f, 4f, Waveform.Triangle);
                case MusicIntensity.Frenzy:
                    return RuntimeTone.Pad(164.81f, 3f, Waveform.Triangle);
                case MusicIntensity.Boss:
                    return RuntimeTone.Pad(82.41f, 3f, Waveform.Saw);
                default:
                    return RuntimeTone.Pad(110f, 4f, Waveform.Sine);
            }
        }

        private AudioClip ResolveSfx(string id)
        {
            if (_sfxClips.TryGetValue(id, out var clip) && clip != null)
            {
                return clip;
            }

            switch (id)
            {
                case SfxSkill:
                    return RuntimeTone.Blip(660f, 0.08f, Waveform.Square);
                case SfxKill:
                    return RuntimeTone.Blip(220f, 0.12f, Waveform.Saw);
                case SfxLevelUp:
                    return RuntimeTone.Blip(880f, 0.25f, Waveform.Triangle);
                case SfxBoss:
                    return RuntimeTone.Blip(110f, 0.6f, Waveform.Saw);
                case SfxUltimate:
                    return RuntimeTone.Blip(70f, 0.5f, Waveform.Sine, 0.002f, 0.4f);
                case SfxPickup:
                    return RuntimeTone.Blip(1200f, 0.06f, Waveform.Square);
                case SfxHurt:
                    return RuntimeTone.Blip(160f, 0.2f, Waveform.Square);
                default:
                    return RuntimeTone.Blip(520f, 0.1f, Waveform.Square);
            }
        }
    }
}
