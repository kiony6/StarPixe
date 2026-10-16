using StarPixelSurge.Framework;
using UnityEngine;

namespace StarPixelSurge.Audio
{
    /// <summary>
    /// 音频管理器：BGM 播放与分段强度切换、音效播放。
    /// </summary>
    public class AudioManager : MonoSingleton<AudioManager>
    {
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;

        protected override void OnSingletonAwake()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
        }

        public void PlayBgm(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }

            if (_bgmSource.clip == clip && _bgmSource.isPlaying)
            {
                return;
            }

            _bgmSource.clip = clip;
            _bgmSource.volume = Mathf.Clamp01(volume);
            _bgmSource.Play();
        }

        public void StopBgm()
        {
            if (_bgmSource != null)
            {
                _bgmSource.Stop();
            }
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _sfxSource == null)
            {
                return;
            }

            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>按波次强度切换 BGM 分段与音量，占位待接入音频资源。</summary>
        public void SetIntensity(int level)
        {
        }
    }
}
