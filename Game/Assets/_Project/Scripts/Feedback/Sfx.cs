using System.Collections.Generic;
using UnityEngine;

namespace FacilityEscape.Feedback
{
    public enum SfxId
    {
        PulseShot, EnemyShot, HitTick, PlayerHurt,
        DoorOpen, DoorClose, DoorDenied,
        DeviceBeep, DeviceLand, Death, Alert
    }

    /// <summary>
    /// Generates every sound effect from maths at first use, so no audio files are needed.
    /// </summary>
    public static class Sfx
    {
        private const int SampleRate = 44100;
        private const float TwoPi = 6.2831853f;

        private static readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        private static AudioListener listener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            clips.Clear();
            listener = null;
        }

        /// <summary>Plays a sound at a world position (volume falls off with distance).</summary>
        public static void Play(SfxId id, Vector3 position, float volume = 1f)
        {
            AudioClip clip = Get(id);
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        /// <summary>Plays a sound at the listener, so it is always heard at full volume.</summary>
        public static void Play2D(SfxId id, float volume = 1f)
        {
            if (listener == null || !listener.isActiveAndEnabled)
                listener = Object.FindFirstObjectByType<AudioListener>();

            Vector3 position = listener != null ? listener.transform.position : Vector3.zero;
            Play(id, position, volume);
        }

        private static AudioClip Get(SfxId id)
        {
            if (clips.TryGetValue(id, out AudioClip cached) && cached != null) return cached;

            AudioClip made = Create(id);
            clips[id] = made;
            return made;
        }

        private static float Noise() => Random.value * 2f - 1f;

        // Linear frequency sweep from f0 to f1 over the clip (p = progress 0..1).
        private static float Sweep(float f0, float f1, float t, float p) =>
            Mathf.Sin(TwoPi * t * (f0 + (f1 - f0) * p * 0.5f));

        private static AudioClip Create(SfxId id)
        {
            switch (id)
            {
                case SfxId.PulseShot:
                    return Build("PulseShot", 0.22f, (t, p) =>
                        Sweep(1600f, 280f, t, p) * Mathf.Exp(-p * 6f) * 0.6f
                        + Noise() * Mathf.Exp(-p * 30f) * 0.2f);

                case SfxId.EnemyShot:
                    return Build("EnemyShot", 0.28f, (t, p) =>
                        Mathf.Sign(Sweep(700f, 140f, t, p)) * Mathf.Exp(-p * 5f) * 0.3f);

                case SfxId.HitTick:
                    return Build("HitTick", 0.07f, (t, p) =>
                        Mathf.Sin(TwoPi * 2000f * t) * Mathf.Exp(-p * 8f) * 0.5f);

                case SfxId.PlayerHurt:
                    {
                        float lp = 0f;
                        return Build("PlayerHurt", 0.3f, (t, p) =>
                        {
                            lp += 0.15f * (Noise() - lp);
                            return Sweep(120f, 55f, t, p) * Mathf.Exp(-p * 5f) * 0.8f
                                   + lp * Mathf.Exp(-p * 12f) * 1.2f;
                        });
                    }

                case SfxId.DoorOpen:
                    {
                        float lp = 0f;
                        return Build("DoorOpen", 0.45f, (t, p) =>
                        {
                            lp += 0.08f * (Noise() - lp);
                            return lp * Mathf.Sin(Mathf.PI * p) * 2.5f
                                   + Mathf.Sin(TwoPi * 70f * t) * Mathf.Exp(-p * 20f) * 0.5f;
                        });
                    }

                case SfxId.DoorClose:
                    {
                        float lp = 0f;
                        return Build("DoorClose", 0.45f, (t, p) =>
                        {
                            lp += 0.08f * (Noise() - lp);
                            float thunk = p > 0.75f
                                ? Mathf.Sin(TwoPi * 60f * t) * Mathf.Exp(-(p - 0.75f) * 25f) * 0.5f
                                : 0f;
                            return lp * Mathf.Sin(Mathf.PI * p) * 2f + thunk;
                        });
                    }

                case SfxId.DoorDenied:
                    return Build("DoorDenied", 0.25f, (t, p) =>
                    {
                        bool on = p < 0.4f || (p > 0.55f && p < 0.95f);
                        return on ? Mathf.Sin(TwoPi * 200f * t) * 0.35f : 0f;
                    });

                case SfxId.DeviceBeep:
                    return Build("DeviceBeep", 0.15f, (t, p) =>
                        Mathf.Sin(TwoPi * 990f * t) * Mathf.Min(1f, p * 20f) * (1f - p) * 0.5f);

                case SfxId.DeviceLand:
                    return Build("DeviceLand", 0.18f, (t, p) =>
                        Mathf.Sin(TwoPi * 140f * t) * Mathf.Exp(-p * 14f) * 0.7f
                        + Noise() * Mathf.Exp(-p * 40f) * 0.2f);

                case SfxId.Death:
                    {
                        float lp = 0f;
                        return Build("Death", 0.6f, (t, p) =>
                        {
                            lp += 0.15f * (Noise() - lp);
                            return lp * Mathf.Exp(-p * 4f) * 1.4f
                                   + Sweep(220f, 40f, t, p) * Mathf.Exp(-p * 5f) * 0.5f;
                        });
                    }

                case SfxId.Alert:
                    return Build("Alert", 0.2f, (t, p) =>
                        Mathf.Sin(TwoPi * (p < 0.5f ? 600f : 900f) * t) * (1f - p) * 0.35f);

                default:
                    return null;
            }
        }

        private static AudioClip Build(string name, float seconds, System.Func<float, float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float p = (float)i / count;
                data[i] = Mathf.Clamp(sample(t, p), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }
    }
}