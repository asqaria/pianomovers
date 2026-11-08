using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Грузчик. Своего игрока двигает владелец (отзывчиво, без задержки сети),
    /// у остальных он кинематический и приезжает через сеть (OwnerNetworkTransform).
    /// Движение — относительно взгляда, медленнее с грузом.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : NetworkBehaviour
    {
        static readonly Color[] Palette =
        {
            new Color(0.9f, 0.3f, 0.25f),  // красный
            new Color(0.25f, 0.5f, 0.95f), // синий
            new Color(0.3f, 0.8f, 0.35f),  // зелёный
            new Color(0.95f, 0.8f, 0.2f),  // жёлтый
        };

        /// <summary>Игрок этого компьютера.</summary>
        public static PlayerController Local { get; private set; }

        public Color color = Color.white;

        public PlayerInputState CurrentInput { get; private set; }
        public Rigidbody Body { get; private set; }
        public PlayerLook Look { get; private set; }

        IPlayerInputSource source;
        GrabSystem grab;
        PlayerStamina stamina;
        bool pendingRespawn;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            source = GetComponent<IPlayerInputSource>();
            grab = GetComponent<GrabSystem>();
            stamina = GetComponent<PlayerStamina>();
            Look = GetComponent<PlayerLook>();

            // Без трения игрок не «прилипает» к стенам и пианино.
            var col = GetComponent<Collider>();
            if (col)
            {
                col.material = new PhysicsMaterial("PlayerNoFriction")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum
                };
            }
        }

        public override void OnNetworkSpawn()
        {
            color = Palette[(int)(OwnerClientId % (ulong)Palette.Length)];

            if (IsOwner)
            {
                Local = this;
                pendingRespawn = true; // телепорт — в следующем Update, когда NetworkTransform готов
                LocalPlayerCamera.Attach(this);
            }
            else
            {
                Body.isKinematic = true;
                Body.interpolation = RigidbodyInterpolation.None;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this)
            {
                Local = null;
                LocalPlayerCamera.Detach();
            }
        }

        /// <summary>Вернуть своего игрока на точку появления (новый раунд).</summary>
        public void Respawn()
        {
            if (!IsOwner) return;
            if (grab) grab.Release(ReleaseReason.Voluntary);
            if (stamina) stamina.Refill();

            var (position, yaw) = RoundManager.SpawnPoint(OwnerClientId);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Body.linearVelocity = Vector3.zero;
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Look.SetYaw(yaw);

            var netTransform = GetComponent<NetworkTransform>();
            if (netTransform)
            {
                try { netTransform.Teleport(position, rotation, transform.localScale); }
                catch (System.Exception e) { Debug.LogWarning("Телепорт через сеть не удался: " + e.Message); }
            }
        }

        void Update()
        {
            if (!IsOwner) return;

            if (pendingRespawn)
            {
                pendingRespawn = false;
                Respawn();
            }

            if (source == null) return;
            CurrentInput = source.ReadInput();
            Look.ApplyLook(CurrentInput.Look, CurrentInput.FreeLook);
        }

        void FixedUpdate()
        {
            if (!IsOwner) return;
            var s = PhysicsTuning.Current;
            if (!s) return;

            Body.mass = s.playerMass;
            Body.MoveRotation(Look.BodyRotation);

            bool carrying = grab && grab.IsHolding;
            Vector2 m = CurrentInput.Move;
            Vector3 dir = Look.BodyRotation * new Vector3(m.x, 0f, m.y);
            float speed = s.moveSpeed * (carrying ? s.carrySpeedMultiplier : 1f);

            Vector3 v = Body.linearVelocity;
            Vector3 horizontal = new Vector3(v.x, 0f, v.z);
            Vector3 accel = Vector3.ClampMagnitude((dir * speed - horizontal) / Time.fixedDeltaTime, s.acceleration);
            Body.AddForce(accel, ForceMode.Acceleration);
        }
    }
}
