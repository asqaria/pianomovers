using System.Collections.Generic;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Лестница «по рёбрам»: физически пианино едет по гладкому пандусу вдоль кромок ступеней,
    /// а этот компонент даёт ощущение ступенек — глухой «бум» в басах на каждой пройденной ступени
    /// и лёгкий толчок вверх. Чем быстрее тащат, тем громче.
    /// Ступени считаются по позиции центра пианино вдоль лестницы (ось Z).
    /// Ещё отмечает пианино «на лестнице» (у всех игроков — для подсказки в HUD):
    /// там оно скользит по кромкам и ложится вдоль наклона (см. PianoBody).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class StairBumps : MonoBehaviour
    {
        public float startZ = 2f;
        public float stepDepth = 0.5f;
        [Tooltip("Медленнее этой скорости (м/с) бума нет — пианино почти стоит")]
        public float minSpeed = 0.15f;
        [Tooltip("Скорость (м/с), при которой бум на полной громкости")]
        public float loudSpeed = 1.5f;
        [Tooltip("Толчок вверх на каждой ступеньке, м/с")]
        public float bumpVelocity = 0.15f;
        [Tooltip("Угол лестницы, градусы (подъём 3 м на 6 м ≈ 26,6°)")]
        public float slopeAngle = 26.565f;

        BoxCollider zone;
        readonly Dictionary<PianoBody, int> lastStep = new Dictionary<PianoBody, int>();

        void Awake()
        {
            zone = GetComponent<BoxCollider>();
            zone.isTrigger = true;
        }

        void FixedUpdate()
        {
            // Нормаль наклона: лестница поднимается вдоль +Z.
            Vector3 slopeNormal = Quaternion.Euler(-slopeAngle, 0f, 0f) * Vector3.up;

            foreach (var piano in PianoBody.All)
            {
                Vector3 pos = piano.transform.position;
                if (!zone.bounds.Contains(pos))
                {
                    lastStep.Remove(piano);
                    continue;
                }

                piano.MarkOnStairs(slopeNormal);
                if (!Net.IsServer) continue; // ноты и толчки ведёт хост

                int step = Mathf.FloorToInt((pos.z - startZ) / stepDepth);
                if (!lastStep.TryGetValue(piano, out int previous))
                {
                    lastStep[piano] = step;
                    continue;
                }
                if (step == previous) continue;
                lastStep[piano] = step;

                float speed = piano.Body.linearVelocity.magnitude;
                if (speed < minSpeed) continue;

                // Бум: низкая нота, громкость от скорости.
                var sound = piano.GetComponent<PianoSound>();
                if (sound)
                {
                    float volume = Mathf.Lerp(0.25f, 0.8f, Mathf.InverseLerp(minSpeed, loudSpeed, speed));
                    sound.PlayKey(Random.Range(0, 5), volume);
                }

                // Пианино чуть подпрыгивает на кромке — чувствуется каждая ступенька.
                piano.Body.AddForce(Vector3.up * bumpVelocity, ForceMode.VelocityChange);
            }
        }
    }
}
