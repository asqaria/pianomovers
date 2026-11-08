using Unity.Netcode;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Зона доставки (решает хост): если центр пианино внутри и оно стоит спокойно settleSeconds —
    /// заказ выполнен, звучит концерт, считается оплата. Итог синхронизируется всем;
    /// каждый игрок, увидев «доставлено», сам отпускает пианино.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DeliveryZone : NetworkBehaviour
    {
        public static DeliveryZone Current { get; private set; }

        [SerializeField] float settleSeconds = 2f;
        [SerializeField] float maxSpeed = 0.25f;

        readonly NetworkVariable<bool> netDelivered = new NetworkVariable<bool>();
        readonly NetworkVariable<int> netScore = new NetworkVariable<int>();
        readonly NetworkVariable<float> netTime = new NetworkVariable<float>();
        readonly NetworkVariable<float> netProgress = new NetworkVariable<float>();

        public bool Delivered => netDelivered.Value;
        public int FinalScore => netScore.Value;
        public float DeliveryTime => netTime.Value;
        public float SettleProgress => netProgress.Value;

        BoxCollider zone;
        float settled;
        float roundStart;

        void Awake()
        {
            Current = this;
            zone = GetComponent<BoxCollider>();
            zone.isTrigger = true;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) roundStart = Time.time;
        }

        /// <summary>Новый раунд. Только на хосте.</summary>
        public void ResetZone()
        {
            if (!IsServer) return;
            settled = 0f;
            roundStart = Time.time;
            netDelivered.Value = false;
            netScore.Value = 0;
            netTime.Value = 0f;
            netProgress.Value = 0f;
        }

        void FixedUpdate()
        {
            if (!IsServer || Delivered) return;

            PianoBody inside = null;
            foreach (var piano in PianoBody.All)
            {
                if (zone.bounds.Contains(piano.transform.position))
                {
                    inside = piano;
                    break;
                }
            }

            if (inside && inside.Body.linearVelocity.magnitude < maxSpeed) settled += Time.fixedDeltaTime;
            else settled = 0f;

            float progress = Mathf.Clamp01(settled / settleSeconds);
            if (Mathf.Abs(progress - netProgress.Value) > 0.05f || (progress == 0f && netProgress.Value != 0f))
                netProgress.Value = progress;

            if (inside && settled >= settleSeconds) Deliver(inside);
        }

        void Deliver(PianoBody piano)
        {
            var condition = piano.GetComponent<PianoCondition>();
            netScore.Value = condition ? condition.Score : 0;
            netTime.Value = Time.time - roundStart;
            netDelivered.Value = true;

            var concert = piano.GetComponent<ConcertPlayer>();
            if (concert) concert.Play();
        }
    }
}
