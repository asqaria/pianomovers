using System.Collections.Generic;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Лестница «по рёбрам»: физически пианино едет по гладкому пандусу вдоль кромок ступеней,
    /// а этот компонент даёт ощущение ступенек — глухой «бум» в басах на каждой пройденной ступени
    /// и лёгкий толчок вверх. Чем быстрее тащат, тем громче.
    /// Ступени считаются по позиции центра пианино вдоль лестницы (ось Z).
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
        public float bumpVelocity = 0.25f;

        BoxCollider zone;
        readonly Dictionary<PianoBody, int> lastStep = new Dictionary<PianoBody, int>();

        void Awake()
        {
            zone = GetComponent<BoxCollider>();
            zone.isTrigger = true;
        }

        void FixedUpdate()
        {
            if (!Net.IsServer) return; // физику и ноты ведёт хост
            foreach (var piano in PianoBody.All)
            {
                Vector3 pos = piano.transform.position;
                if (!zone.bounds.Contains(pos))
                {
                    lastStep.Remove(piano);
                    continue;
                }

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
