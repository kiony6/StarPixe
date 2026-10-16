using System.Collections.Generic;
using UnityEngine;

namespace StarPixelSurge.Audio
{
    /// <summary>程序化音频波形。</summary>
    public enum Waveform
    {
        Sine = 0,
        Square = 1,
        Triangle = 2,
        Saw = 3
    }

    /// <summary>
    /// 运行时程序化音频片段生成器。缺少音频资源时合成短促音效与循环垫音，
    /// 与 RuntimeSprite 的程序化美术方案一致，保证接入真实音频前后都不中断。
    /// 生成的片段按参数缓存。
    /// </summary>
    public static class RuntimeTone
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>生成带起止包络的短促音效。</summary>
        public static AudioClip Blip(float frequency, float seconds, Waveform wave = Waveform.Square, float attack = 0.005f, float release = 0.08f, float amplitude = 0.5f)
        {
            return Tone(frequency, seconds, wave, attack, release, amplitude, false);
        }

        /// <summary>生成循环垫音，整段为整数个周期以保证循环无缝。</summary>
        public static AudioClip Pad(float frequency, float seconds, Waveform wave = Waveform.Sine, float amplitude = 0.35f)
        {
            return Tone(frequency, seconds, wave, 0f, 0f, amplitude, true);
        }

        public static AudioClip Tone(float frequency, float seconds, Waveform wave, float attack, float release, float amplitude, bool loop)
        {
            var key = string.Format("{0:F2}|{1:F3}|{2}|{3:F3}|{4:F3}|{5:F2}|{6}", frequency, seconds, (int)wave, attack, release, amplitude, loop ? 1 : 0);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[samples];
            var attackSamples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * attack));
            var releaseSamples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * release));

            var cycles = Mathf.Max(1, Mathf.RoundToInt(frequency * seconds));
            var phaseStep = cycles / (float)samples;

            for (var i = 0; i < samples; i++)
            {
                var phase = (i * phaseStep) % 1f;
                var envelope = 1f;

                if (loop)
                {
                    // 循环垫音用缓慢震音替代包络，保持接缝连续。
                    envelope = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * (i / (float)samples));
                }
                else if (i < attackSamples)
                {
                    envelope = i / (float)attackSamples;
                }
                else if (samples - i < releaseSamples)
                {
                    envelope = Mathf.Max(0f, (samples - i) / (float)releaseSamples);
                }

                data[i] = Sample(wave, phase) * amplitude * envelope;
            }

            var clip = AudioClip.Create("tone_" + key, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        private static float Sample(Waveform wave, float phase)
        {
            switch (wave)
            {
                case Waveform.Square:
                    return phase < 0.5f ? 1f : -1f;
                case Waveform.Triangle:
                    return 1f - 4f * Mathf.Abs(phase - 0.5f);
                case Waveform.Saw:
                    return phase * 2f - 1f;
                default:
                    return Mathf.Sin(phase * Mathf.PI * 2f);
            }
        }

        /// <summary>清空缓存，用于释放程序化片段。</summary>
        public static void Clear()
        {
            Cache.Clear();
        }
    }
}
