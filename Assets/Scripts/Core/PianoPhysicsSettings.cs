using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Все параметры физики в одном месте. Создаётся 3 пресета: «Реализм», «Середина», «Аркада».
    /// Меняются на лету клавишами 1/2/3 (см. PhysicsTuning).
    /// </summary>
    [CreateAssetMenu(menuName = "Piano Movers/Physics Settings", fileName = "PianoPhysicsSettings")]
    public class PianoPhysicsSettings : ScriptableObject
    {
        [Header("Пресет")]
        public string presetName = "Середина";

        [Header("Пианино")]
        [Tooltip("Не реальные 250 кг. Держите соотношение к массе игрока не больше ~10:1, иначе физика нестабильна.")]
        [Min(1f)] public float pianoMass = 80f;
        public float pianoLinearDamping = 0.6f;
        public float pianoAngularDamping = 2.5f;
        public float pianoMaxSpeed = 6f;
        public float pianoMaxAngularSpeed = 3f;

        [Header("Игрок")]
        [Min(1f)] public float playerMass = 10f;
        public float moveSpeed = 4.5f;
        [Tooltip("Максимальное ускорение игрока, м/с²")]
        public float acceleration = 25f;
        [Range(0.1f, 1f)] public float carrySpeedMultiplier = 0.55f;
        [Tooltip("Градусов в секунду")]
        public float turnSpeed = 720f;

        [Header("Хват")]
        public float grabRadius = 1.4f;
        [Tooltip("Расстояние от центра игрока до руки")]
        public float reach = 0.7f;
        [Tooltip("Высота руки относительно центра игрока, когда просто держим")]
        public float carryHandHeight = -0.5f;
        [Tooltip("Высота руки относительно центра игрока, когда поднимаем")]
        public float liftHandHeight = 0.2f;
        public float gripSpring = 1500f;
        public float gripDamper = 150f;
        [Tooltip("Сила одного игрока. Подобрана так, чтобы один не мог поднять пианино, а двое могли.")]
        public float maxGripForce = 520f;
        [Tooltip("Какая доля силы тянет самого игрока. 1 = честная физика, меньше = прощающая.")]
        [Range(0f, 1f)] public float reactionShare = 0.25f;
        [Tooltip("Если рука дальше от точки хвата дольше breakGraceTime — пианино срывается")]
        public float breakDistance = 1.6f;
        [Tooltip("Сколько секунд можно быть «перетянутым», прежде чем сорвётся")]
        public float breakGraceTime = 0.4f;

        [Header("Стабилизация, пока держат (прощающая физика)")]
        [Tooltip("Насколько пианино само стремится стоять ровно, пока его держат. 0 — чистая физика.")]
        public float heldUprightStrength = 30f;
        [Tooltip("Гасит раскачивание вперёд-назад и вбок, пока держат")]
        public float heldUprightDamping = 6f;
        [Tooltip("Гасит вращение вокруг вертикали, пока держат")]
        public float heldYawDamping = 4f;

        [Header("Выносливость")]
        public float staminaMax = 100f;
        public float staminaDrainPerSecond = 18f;
        public float staminaRegenPerSecond = 25f;
        [Tooltip("Доля выносливости, после которой снова можно хвататься")]
        [Range(0f, 1f)] public float staminaRegrabThreshold = 0.3f;

        [Header("Удары (в м/с изменения скорости пианино)")]
        [Tooltip("Слабее — тишина")]
        public float noteThreshold = 0.4f;
        [Tooltip("Удар этой силы звучит на полную громкость")]
        public float loudImpact = 4f;
        [Tooltip("Сильнее — расстраивает клавиши")]
        public float damageThreshold = 2.2f;
        [Tooltip("Полутонов расстройки за единицу урона")]
        public float detunePerDamage = 0.35f;
        public float maxDetuneSemitones = 1.5f;
    }
}
