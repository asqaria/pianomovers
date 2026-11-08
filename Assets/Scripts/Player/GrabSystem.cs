using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace PianoMovers
{
    public enum ReleaseReason
    {
        Voluntary,     // отпустил кнопку
        Overstretched, // сорвалось
        Exhausted,     // кончились силы
        Delivered      // заказ доставлен
    }

    /// <summary>
    /// Хват от первого лица по сети.
    ///
    /// Владелец (у себя): смотрит на пианино и держит кнопку → решает «схватился»,
    /// пишет в сетевые переменные, ЗА ЧТО и КАК держит (точка на пианино, дистанция, подъём).
    /// Ещё владелец считает ту же «пружину» у себя — для отдачи в своё тело,
    /// усилия (выносливость) и срыва при перетягивании.
    ///
    /// Хост: по этим переменным каждый физический шаг тянет пианино к руке каждого игрока.
    /// Пружина ограничена maxGripForce: один не поднимет пианино, двое — да.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class GrabSystem : NetworkBehaviour
    {
        public static readonly List<GrabSystem> All = new List<GrabSystem>();

        /// <summary>Свой игрок схватился (для метрик плейтеста).</summary>
        public static event System.Action<GrabSystem> Grabbed;
        /// <summary>Свой игрок отпустил и почему (для метрик плейтеста).</summary>
        public static event System.Action<GrabSystem, ReleaseReason> Released;

        static NetworkVariable<T> OwnerVar<T>(T value = default) =>
            new NetworkVariable<T>(value, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        readonly NetworkVariable<bool> netHolding = OwnerVar<bool>();
        readonly NetworkVariable<NetworkObjectReference> netPiano = OwnerVar<NetworkObjectReference>();
        readonly NetworkVariable<Vector3> netAnchor = OwnerVar<Vector3>();
        readonly NetworkVariable<float> netHoldDistance = OwnerVar(1f);
        readonly NetworkVariable<float> netHoldHeight = OwnerVar(0f);
        readonly NetworkVariable<bool> netLifting = OwnerVar<bool>();

        public bool IsHolding => netHolding.Value && HeldPiano;
        public bool IsLifting => IsHolding && netLifting.Value;
        public PianoBody HeldPiano
        {
            get
            {
                if (!netHolding.Value) return null;
                return netPiano.Value.TryGet(out NetworkObject obj) ? obj.GetComponent<PianoBody>() : null;
            }
        }
        /// <summary>Мировая точка, за которую держим.</summary>
        public Vector3 HoldPosition
        {
            get
            {
                var piano = HeldPiano;
                return piano ? piano.transform.TransformPoint(netAnchor.Value) : transform.position;
            }
        }
        /// <summary>Пианино под прицелом и в досягаемости (только у владельца, для HUD).</summary>
        public PianoBody LookTarget { get; private set; }
        /// <summary>Насколько игрок напрягается: 0..1 (только у владельца).</summary>
        public float Effort01 { get; private set; }

        PlayerController player;
        PlayerStamina stamina;
        Vector3 lookHitPoint;
        float overstretchTime;
        bool releasedForDelivery;

        // Скорость тела по смещению: у чужих игроков на хосте Rigidbody кинематический.
        Vector3 lastPosition;
        Vector3 bodyVelocity;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            stamina = GetComponent<PlayerStamina>();
            if (!GetComponent<PlayerHands>()) gameObject.AddComponent<PlayerHands>();
            lastPosition = transform.position;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public override void OnNetworkDespawn()
        {
            if (IsOwner) Release();
        }

        void FixedUpdate()
        {
            var s = PhysicsTuning.Current;
            if (!s || !IsSpawned) return;

            Vector3 position = transform.position;
            bodyVelocity = (position - lastPosition) / Time.fixedDeltaTime;
            lastPosition = position;

            if (IsOwner) OwnerTick(s);
            if (IsServer && IsHolding) ApplyForceToPiano(s);
        }

        // ---------- Владелец ----------

        void OwnerTick(PianoPhysicsSettings s)
        {
            UpdateLookTarget(s);

            var input = player.CurrentInput;
            bool canHold = !stamina || !stamina.Exhausted;

            var zone = DeliveryZone.Current;
            bool delivered = zone && zone.Delivered;
            if (!delivered) releasedForDelivery = false;

            if (delivered && !releasedForDelivery)
            {
                releasedForDelivery = true;
                Release(ReleaseReason.Delivered);
            }
            else if (!canHold) Release(ReleaseReason.Exhausted);
            else if (!input.Grab) Release(ReleaseReason.Voluntary);
            else if (!netHolding.Value && LookTarget) GrabAt(LookTarget, lookHitPoint, s);

            if (netHolding.Value)
            {
                if (netLifting.Value != input.Lift) netLifting.Value = input.Lift;
                OwnerFeel(s);
            }
            else Effort01 = 0f;

            if (stamina) stamina.Tick(netHolding.Value ? Mathf.Max(Effort01, 0.15f) : 0f, Time.fixedDeltaTime);
        }

        void UpdateLookTarget(PianoPhysicsSettings s)
        {
            LookTarget = null;
            var look = player.Look;
            // Луч из глаз; собственную капсулу он не задевает — начинается внутри неё.
            if (Physics.Raycast(look.EyePosition, look.LookDirection, out RaycastHit hit,
                    s.grabRadius + 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var piano = hit.collider.GetComponentInParent<PianoBody>();
                if (piano)
                {
                    LookTarget = piano;
                    lookHitPoint = hit.point;
                }
            }
        }

        void GrabAt(PianoBody piano, Vector3 point, PianoPhysicsSettings s)
        {
            var obj = piano.GetComponent<NetworkObject>();
            if (!obj) return;

            Vector3 offset = point - player.Body.position;
            float height = Mathf.Clamp(offset.y, s.carryHandHeight, s.liftHandHeight);
            offset.y = 0f;

            netPiano.Value = obj;
            netAnchor.Value = piano.transform.InverseTransformPoint(point);
            netHoldDistance.Value = Mathf.Clamp(offset.magnitude, 0.6f, 1.2f);
            netHoldHeight.Value = height;
            netLifting.Value = false;
            netHolding.Value = true;
            overstretchTime = 0f;

            Grabbed?.Invoke(this);
        }

        /// <summary>Та же пружина, что и на хосте, — но только для своих ощущений: отдача, усилие, срыв.</summary>
        void OwnerFeel(PianoPhysicsSettings s)
        {
            var piano = HeldPiano;
            if (!piano)
            {
                Release(ReleaseReason.Voluntary);
                return;
            }

            Vector3 force = SpringForce(s, piano, player.Body.linearVelocity, out bool overstretched);
            if (overstretched)
            {
                overstretchTime += Time.fixedDeltaTime;
                if (overstretchTime > s.breakGraceTime)
                {
                    Release(ReleaseReason.Overstretched);
                    return;
                }
            }
            else overstretchTime = 0f;

            player.Body.AddForce(-force * s.reactionShare, ForceMode.Force);
            Effort01 = force.magnitude / s.maxGripForce;
        }

        public void Release(ReleaseReason reason = ReleaseReason.Voluntary)
        {
            if (!IsSpawned || !IsOwner || !netHolding.Value) return;
            netHolding.Value = false;
            netLifting.Value = false;
            Effort01 = 0f;
            Released?.Invoke(this, reason);
        }

        // ---------- Хост ----------

        void ApplyForceToPiano(PianoPhysicsSettings s)
        {
            var piano = HeldPiano;
            if (!piano) return;
            Vector3 force = SpringForce(s, piano, bodyVelocity, out _);
            piano.Body.AddForceAtPosition(force, HoldPosition, ForceMode.Force);
        }

        // ---------- Общее ----------

        /// <summary>Сила, с которой рука тянет точку хвата на пианино.</summary>
        Vector3 SpringForce(PianoPhysicsSettings s, PianoBody piano, Vector3 myVelocity, out bool overstretched)
        {
            Vector3 grabPos = HoldPosition;
            Vector3 forward = player.Look.BodyRotation * Vector3.forward;
            float height = netLifting.Value ? s.liftHandHeight : netHoldHeight.Value;
            Vector3 hand = transform.position + forward * netHoldDistance.Value + Vector3.up * height;
            Vector3 delta = hand - grabPos;

            overstretched = delta.magnitude > s.breakDistance;
            if (overstretched) delta = delta.normalized * s.breakDistance;

            Vector3 pianoVelocity = piano.Body.isKinematic ? piano.NetVelocity : piano.Body.GetPointVelocity(grabPos);
            Vector3 relativeVelocity = myVelocity - pianoVelocity;
            return Vector3.ClampMagnitude(delta * s.gripSpring + relativeVelocity * s.gripDamper, s.maxGripForce);
        }
    }
}
