using System.Collections.Generic;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Корпус пианино. Физику считает только хост (на клиентах Rigidbody кинематический —
    /// это делает NetworkRigidbody, а позицию приносит NetworkTransform).
    /// Применяет параметры активного пресета и стабилизирует пианино, пока его держат.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PianoBody : MonoBehaviour
    {
        public static readonly List<PianoBody> All = new List<PianoBody>();

        public Rigidbody Body { get; private set; }

        /// <summary>Скорость по смещению — работает и у клиента, где Rigidbody кинематический.</summary>
        public Vector3 NetVelocity { get; private set; }
        Vector3 lastPosition;

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
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

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

            if (IsHeld) Stabilize(s);
        }

        /// <summary>
        /// Пока пианино держат, оно мягко стремится стоять ровно и не крутиться.
        /// Наклонить всё ещё можно (поднять один край), но не перевернуть случайным рывком.
        /// Брошенное пианино падает по честной физике — это смешно и так задумано.
        /// </summary>
        void Stabilize(PianoPhysicsSettings s)
        {
            Vector3 w = Body.angularVelocity;
            Vector3 tiltVelocity = w - Vector3.up * w.y;
            Vector3 towardUpright = Vector3.Cross(transform.up, Vector3.up);

            Vector3 torque = towardUpright * s.heldUprightStrength
                             - tiltVelocity * s.heldUprightDamping
                             - Vector3.up * (w.y * s.heldYawDamping);
            Body.AddTorque(torque, ForceMode.Acceleration);
        }
    }
}
