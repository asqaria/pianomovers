using Unity.Netcode;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Состояние инструмента: расстройка каждой клавиши (в полутонах) и счётчик сильных ударов.
    /// 25 клавиш: от C3 (0) до C5 (24), C4 — клавиша 12.
    /// Расстройку хранит хост (он же играет все ноты); игрокам уходят только итоги для HUD.
    /// </summary>
    public class PianoCondition : NetworkBehaviour
    {
        public const int KeyCount = 25;
        public const int MiddleCKey = 12;

        readonly float[] detune = new float[KeyCount];
        readonly NetworkVariable<int> netHardHits = new NetworkVariable<int>();
        readonly NetworkVariable<float> netAverageDetune = new NetworkVariable<float>();

        public int HardHits => netHardHits.Value;
        public float AverageDetune => netAverageDetune.Value;

        public float GetDetune(int key) => detune[Mathf.Clamp(key, 0, KeyCount - 1)];

        /// <summary>Оплата за заказ (прототип): 1000 минус штрафы за расстройку и удары.</summary>
        public int Score => Mathf.Max(0, Mathf.RoundToInt(1000f - AverageDetune * 800f - HardHits * 15f));

        /// <summary>Только на хосте.</summary>
        public void ApplyDamage(int nearKey, float severity, PianoPhysicsSettings s)
        {
            if (!IsServer) return;

            int keysHit = 1 + Mathf.FloorToInt(severity);
            for (int i = 0; i < keysHit; i++)
            {
                int key = Mathf.Clamp(nearKey + Random.Range(-3, 4), 0, KeyCount - 1);
                float sign = Random.value < 0.5f ? -1f : 1f;
                float amount = s.detunePerDamage * Random.Range(0.5f, 1f) * severity;
                detune[key] = Mathf.Clamp(detune[key] + sign * amount, -s.maxDetuneSemitones, s.maxDetuneSemitones);
            }

            netHardHits.Value++;
            netAverageDetune.Value = ComputeAverageDetune();
        }

        /// <summary>Новый раунд: пианино снова настроено. Только на хосте.</summary>
        public void ResetState()
        {
            if (!IsServer) return;
            System.Array.Clear(detune, 0, detune.Length);
            netHardHits.Value = 0;
            netAverageDetune.Value = 0f;
        }

        float ComputeAverageDetune()
        {
            float sum = 0f;
            foreach (float d in detune) sum += Mathf.Abs(d);
            return sum / KeyCount;
        }
    }
}
