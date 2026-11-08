using Unity.Netcode;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Звук пианино без ассетов: тон синтезируется при старте (затухающие гармоники),
    /// нужная клавиша получается сдвигом pitch. Расстройка клавиши добавляется к сдвигу.
    /// По сети: ноты решает хост и рассылает всем готовую высоту — у всех звучит одинаково.
    /// Позже заменить на сэмплы настоящего пианино.
    /// </summary>
    [RequireComponent(typeof(PianoCondition))]
    public class PianoSound : NetworkBehaviour
    {
        [SerializeField] int voices = 10;

        const float BaseFrequency = 261.63f; // C4

        AudioSource[] pool;
        int next;
        PianoCondition condition;

        void Awake()
        {
            condition = GetComponent<PianoCondition>();
            AudioClip tone = BuildTone();

            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.8f;
                source.minDistance = 3f;
                source.maxDistance = 40f;
                source.clip = tone;
                pool[i] = source;
            }
        }

        /// <summary>Сыграть клавишу. Работает на хосте (рассылает всем) или без сети.</summary>
        public void PlayKey(int key, float volume)
        {
            key = Mathf.Clamp(key, 0, PianoCondition.KeyCount - 1);
            float semitones = key - PianoCondition.MiddleCKey + condition.GetDetune(key);
            float pitch = Mathf.Pow(2f, semitones / 12f);

            if (!IsSpawned) PlayLocal(pitch, volume);
            else if (IsServer) PlayPitchRpc(pitch, volume);
            // клиент сам ноты не играет — их присылает хост
        }

        [Rpc(SendTo.Everyone)]
        void PlayPitchRpc(float pitch, float volume) => PlayLocal(pitch, volume);

        void PlayLocal(float pitch, float volume)
        {
            var source = pool[next];
            next = (next + 1) % pool.Length;
            source.Stop();
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume);
            source.Play();
        }

        static AudioClip BuildTone()
        {
            const int rate = 44100;
            const float length = 1.6f;
            int samples = (int)(rate * length);
            float[] harmonics = { 1f, 0.55f, 0.3f, 0.18f, 0.1f };
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-3.2f * t);
                float value = 0f;
                for (int h = 0; h < harmonics.Length; h++)
                {
                    value += harmonics[h]
                             * Mathf.Sin(2f * Mathf.PI * BaseFrequency * (h + 1) * t)
                             * Mathf.Exp(-h * 1.5f * t);
                }
                data[i] = value * envelope * 0.35f;
            }

            var clip = AudioClip.Create("PianoTone", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
