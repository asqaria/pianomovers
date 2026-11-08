using Unity.Netcode;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Выносливость: тратится, пока держишь (сильнее — когда поднимаешь),
    /// восстанавливается, когда отпустил. На нуле руки разжимаются.
    /// Считает владелец; остальным уходит процент — чтобы видеть, что напарник краснеет.
    /// </summary>
    public class PlayerStamina : NetworkBehaviour
    {
        readonly NetworkVariable<byte> netPercent = new NetworkVariable<byte>(
            100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        float value = 100f;
        float max = 100f;
        bool exhausted;

        bool Remote => IsSpawned && !IsOwner;
        public float Normalized => Remote ? netPercent.Value / 100f : (max > 0f ? value / max : 0f);
        public bool Exhausted => Remote ? netPercent.Value == 0 : exhausted;

        void Start()
        {
            var s = PhysicsTuning.Current;
            if (s) max = s.staminaMax;
            value = max;
        }

        public void Refill()
        {
            value = max;
            exhausted = false;
            Publish();
        }

        /// <param name="effort01">0 — отдыхаем, 1 — держим на пределе силы.</param>
        public void Tick(float effort01, float dt)
        {
            var s = PhysicsTuning.Current;
            if (!s) return;
            max = s.staminaMax;

            if (effort01 > 0.01f) value -= s.staminaDrainPerSecond * effort01 * dt;
            else value += s.staminaRegenPerSecond * dt;
            value = Mathf.Clamp(value, 0f, max);

            if (value <= 0f) exhausted = true;
            else if (exhausted && Normalized >= s.staminaRegrabThreshold) exhausted = false;

            Publish();
        }

        void Publish()
        {
            if (!IsSpawned || !IsOwner) return;
            byte percent = (byte)Mathf.RoundToInt(Mathf.Clamp01(max > 0f ? value / max : 0f) * 100f);
            if (exhausted) percent = 0;
            if (netPercent.Value != percent) netPercent.Value = percent;
        }
    }
}
