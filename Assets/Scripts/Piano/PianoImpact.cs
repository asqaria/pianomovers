using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Хук игры: удар → нота. Сила удара → громкость, место удара → регистр
    /// (левый край — басы, правый — верха). Сильный удар — аккорд и расстройка клавиш.
    /// Сила меряется как изменение скорости пианино (импульс / масса), поэтому
    /// пороги не зависят от массы в пресете.
    /// </summary>
    [RequireComponent(typeof(PianoBody), typeof(PianoSound), typeof(PianoCondition))]
    public class PianoImpact : MonoBehaviour
    {
        [Tooltip("Половина ширины корпуса по локальной оси X")]
        public float halfWidth = 0.75f;
        [SerializeField] float minInterval = 0.06f;

        PianoBody piano;
        PianoSound sound;
        PianoCondition condition;
        float lastHitTime = -1f;

        void Awake()
        {
            piano = GetComponent<PianoBody>();
            sound = GetComponent<PianoSound>();
            condition = GetComponent<PianoCondition>();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!Net.IsServer) return; // удары считает только хост, ноты он рассылает всем
            var s = PhysicsTuning.Current;
            if (!s || collision.contactCount == 0) return;
            if (collision.collider.GetComponentInParent<PlayerController>()) return; // игроки не звенят

            float deltaV = collision.impulse.magnitude / piano.Body.mass;
            if (deltaV < s.noteThreshold || Time.time - lastHitTime < minInterval) return;
            lastHitTime = Time.time;

            Vector3 local = transform.InverseTransformPoint(collision.GetContact(0).point);
            float t = Mathf.InverseLerp(-halfWidth, halfWidth, local.x);
            int key = Mathf.RoundToInt(t * (PianoCondition.KeyCount - 1));
            float volume = 0.2f + 0.8f * Mathf.InverseLerp(s.noteThreshold, s.loudImpact, deltaV);

            if (deltaV >= s.damageThreshold)
            {
                float severity = (deltaV - s.damageThreshold) / s.damageThreshold + 0.5f;
                condition.ApplyDamage(key, severity, s);
                sound.PlayKey(key, volume);
                sound.PlayKey(key + 3, volume * 0.8f);
                sound.PlayKey(key - 4, volume * 0.8f);
            }
            else
            {
                sound.PlayKey(key, volume);
            }
        }
    }
}
