using UnityEngine;

namespace StarPixelSurge.Audio
{
    /// <summary>
    /// 音频管理器：管理BGM和音效播放。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        // 音效ID常量
        public const string SfxSkill = "skill";
        public const string SfxKill = "kill";
        public const string SfxLevelUp = "levelup";
        public const string SfxBoss = "boss";
        public const string SfxButton = "button";

        [Header("BGM")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioClip defaultBGM;

        [Header("音效")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip shootSound;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip levelUpSound;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            PlayBGM(defaultBGM);
        }

        /// <summary>播放BGM。</summary>
        public void PlayBGM(AudioClip clip)
        {
            if (bgmSource == null || clip == null) return;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        /// <summary>播放射击音效。</summary>
        public void PlayShoot()
        {
            PlaySFX(shootSound);
        }

        /// <summary>播放命中音效。</summary>
        public void PlayHit()
        {
            PlaySFX(hitSound);
        }

        /// <summary>播放按钮点击音效。</summary>
        public void PlayButtonClick()
        {
            PlaySFX(buttonClickSound);
        }

        /// <summary>播放升级音效。</summary>
        public void PlayLevelUp()
        {
            PlaySFX(levelUpSound);
        }

        /// <summary>播放音效。</summary>
        private void PlaySFX(AudioClip clip)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>根据ID播放音效。</summary>
        public void PlaySfx(string sfxId)
        {
            Debug.Log($"[AudioManager] 播放音效: {sfxId}");
            // TODO: 根据sfxId播放对应的音效
        }

        /// <summary>设置BGM音量。</summary>
        public void SetBGMVolume(float volume)
        {
            if (bgmSource != null)
            {
                bgmSource.volume = volume;
            }
        }

        /// <summary>设置音效音量。</summary>
        public void SetSFXVolume(float volume)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = volume;
            }
        }

        /// <summary>设置音乐强度。</summary>
        public void SetIntensity(int intensity)
        {
            // TODO: 根据强度调整BGM
            Debug.Log($"[AudioManager] 音乐强度: {intensity}");
        }
    }
}
