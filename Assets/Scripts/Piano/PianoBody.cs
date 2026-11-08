using System.Collections.Generic;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Корпус пианино. Физику считает только хост (на клиентах Rigidbody кинематический —
    /// это делает NetworkRigidbody, а позицию приносит NetworkTransform).
    /// Применяет параметры активного пресета и стабилизирует пианино, пока его держат.
    /// На лестнице: ложится вдоль наклона и скользит по кромкам ступеней (меньше трение).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PianoBody : MonoBehaviour
    {
        public static readonly List<PianoBody> All = new List<PianoBody>();

        public Rigidbody Body { get; private set; }

        /// <summary>Скорость по смещению — работает и у клиента, где Rigidbody кинематический.</summary>
        public Vector3 NetVelocity { get; private set; }
        Vector3 lastPosition;

        /// <summary>Пианино сейчас на лестнице (отмечает StairBumps у всех игроков).</summary>
        public bool OnStairs => Time.fixedTime - lastStairTime < 0.1f;
        float lastStairTime = -1f;
        Vector3 stairNormal = Vector3.up;

        Collider[] colliders;
        PhysicsMaterial material;

        public bool IsHeld
        {
            get
            {
                foreach (var grab in GrabSystem.All)
                    if (grab.IsHolding && grab.HeldPiano == this) return true;
                return false;
            }
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            lastPosition = transform.position;

            // Свой материал трения: его коэффициент меняем на лету (пол / лестница).
            material = new PhysicsMaterial("PianoFriction")
            {
                dynamicFriction = 0.5f,
                staticFriction = 0.5f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.sharedMaterial = material;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Вызывает StairBumps каждый физический шаг, пока центр пианино над лестницей.</summary>
        public void MarkOnStairs(Vector3 surfaceNormal)
        {
            lastStairTime = Time.fixedTime;
            stairNormal = surfaceNormal;
        }

        void FixedUpdate()
        {
            Vector3 position = transform.position;
            NetVelocity = (position - lastPosition) / Time.fixedDeltaTime;
            lastPosition = position;

            if (!Net.IsServer || Body.isKinematic) return;
            var s = PhysicsTuning.Current;
            if (!s) return;

            Body.mass = s.pianoMass;
            Body.linearDamping = s.pianoLinearDamping;
            Body.angularDamping = s.pianoAngularDamping;
            Body.maxLinearVelocity = s.pianoMaxSpeed;
            Body.maxAngularVelocity = s.pianoMaxAngularSpeed;

            bool held = IsHeld;
            float friction = OnStairs ? (held ? s.stairFrictionHeld : s.stairFrictionFree) : s.floorFriction;
            if (!Mathf.Approximately(material.dynamicFriction, friction))
            {
                material.dynamicFriction = friction;
                material.staticFriction = friction;
            }

            if (held) Stabilize(s, OnStairs ? stairNormal : Vector3.up);
        }

        /// <summary>
        /// Пока пианино держат, оно мягко выравнивается: на полу — вертикально,
        /// на лестнице — вдоль наклона (лежит на кромках ступеней, а не упирается ребром),
        /// и не крутится. Наклонить всё ещё можно, перевернуть случайным рывком — нет.
        /// Брошенное пианино падает по честной физике — это смешно и так задумано.
        /// </summary>
        void Stabilize(PianoPhysicsSettings s, Vector3 targetUp)
        {
            Vector3 w = Body.angularVelocity;
            Vector3 tiltVelocity = w - Vector3.up * w.y;
            Vector3 towardTarget = Vector3.Cross(transform.up, targetUp);

            Vector3 torque = towardTarget * s.heldUprightStrength
                             - tiltVelocity * s.heldUprightDamping
                             - Vector3.up * (w.y * s.heldYawDamping);
            Body.AddTorque(torque, ForceMode.Acceleration);
        }
    }
}
