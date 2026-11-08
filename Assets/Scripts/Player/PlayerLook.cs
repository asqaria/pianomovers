using Unity.Netcode;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Взгляд от первого лица: yaw поворачивает тело, pitch — только голову и камеру.
    /// Владелец считает взгляд сам; остальные получают yaw из поворота тела,
    /// а pitch — из сетевой переменной (чтобы видеть, куда напарник смотрит).
    /// </summary>
    public class PlayerLook : NetworkBehaviour
    {
        [Tooltip("Над центром капсулы. Игрок 1,8 м с центром на 0,9 м → глаза на ~1,6 м.")]
        public float eyeHeight = 0.7f;
        public float minPitch = -80f;
        public float maxPitch = 80f;

        readonly NetworkVariable<float> netPitch = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        float yaw, pitch;
        float freeLookYaw; // поворот камеры без поворота тела (Alt)

        bool Remote => IsSpawned && !IsOwner;
        public float Yaw => Remote ? transform.eulerAngles.y : yaw;
        public float Pitch => Remote ? netPitch.Value : pitch;

        /// <summary>Камера этого игрока (только у владельца). Нужна HUD.</summary>
        public Camera ViewCamera { get; set; }

        public Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;
        /// <summary>Куда смотрит камера (с учётом «осмотреться»).</summary>
        public Quaternion ViewRotation => Quaternion.Euler(Pitch, Yaw + (Remote ? 0f : freeLookYaw), 0f);
        public Vector3 LookDirection => ViewRotation * Vector3.forward;
        public Quaternion BodyRotation => Quaternion.Euler(0f, Yaw, 0f);

        void Awake() => yaw = transform.eulerAngles.y;

        public void SetYaw(float value)
        {
            yaw = value;
            pitch = 0f;
            freeLookYaw = 0f;
        }

        public void ApplyLook(Vector2 lookDegrees, bool freeLook = false)
        {
            if (freeLook)
            {
                // Осматриваемся: крутится только камера, тело и пианино стоят.
                freeLookYaw = Mathf.Clamp(freeLookYaw + lookDegrees.x, -160f, 160f);
            }
            else
            {
                yaw = Mathf.Repeat(yaw + lookDegrees.x, 360f);
                freeLookYaw = Mathf.MoveTowards(freeLookYaw, 0f, 540f * Time.deltaTime); // плавно назад
            }
            pitch = Mathf.Clamp(pitch - lookDegrees.y, minPitch, maxPitch);
            if (IsSpawned && IsOwner && Mathf.Abs(netPitch.Value - pitch) > 0.5f) netPitch.Value = pitch;
        }
    }
}
