using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Камера своего игрока. Когда свой игрок появился в сети — общая камера сцены выключается,
    /// создаётся камера игрока со слушателем звука.
    /// Своё тело в виде от первого лица не видно (отдельный слой), видно только руки.
    /// </summary>
    public static class LocalPlayerCamera
    {
        public const int OwnBodyLayer = 20; // безымянный пользовательский слой

        static GameObject cameraObject;
        static GameObject sceneCamera;

        public static Camera Current { get; private set; }

        /// <summary>Игрок выбрал «всегда от первого лица» (клавиша V).</summary>
        public static bool PreferFirstPerson { get; set; }

        public static void Attach(PlayerController player)
        {
            if (Camera.main && Camera.main.gameObject != cameraObject) sceneCamera = Camera.main.gameObject;
            if (sceneCamera) sceneCamera.SetActive(false);

            cameraObject = new GameObject("LocalPlayerCamera");
            var cam = cameraObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();
            var rig = cameraObject.AddComponent<PlayerCameraRig>();
            rig.target = player.Look;
            rig.grab = player.GetComponent<GrabSystem>();
            player.Look.ViewCamera = cam;

            var visual = player.transform.Find("Visual");
            if (visual) SetLayerRecursive(visual, OwnBodyLayer);

            Current = cam;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public static void Detach()
        {
            if (cameraObject) Object.Destroy(cameraObject);
            cameraObject = null;
            Current = null;
            if (sceneCamera) sceneCamera.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursive(child, layer);
        }
    }

    /// <summary>
    /// Камера игрока: от первого лица, а пока несёшь пианино — плавно отъезжает за плечо,
    /// чтобы было видно пианино, напарника и дорогу. В тесных местах (лестница) упирается
    /// в стены и подъезжает ближе. Угол обзора задаётся по горизонтали.
    /// </summary>
    public class PlayerCameraRig : MonoBehaviour
    {
        public PlayerLook target;
        public GrabSystem grab;
        public float horizontalFov = 90f;
        public float maxVerticalFov = 80f;

        [Header("Вид из-за плеча (пока несёшь)")]
        public Vector3 shoulderOffset = new Vector3(0.6f, 0.5f, -3.2f);
        public float switchSpeed = 3f;
        public float collisionRadius = 0.25f;

        Camera cam;
        float thirdPerson; // 0 — из глаз, 1 — из-за плеча
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (!target) return;

            bool wantThird = grab && grab.IsHolding && !LocalPlayerCamera.PreferFirstPerson;
            thirdPerson = Mathf.MoveTowards(thirdPerson, wantThird ? 1f : 0f, switchSpeed * Time.deltaTime);
            float blend = Mathf.SmoothStep(0f, 1f, thirdPerson);

            Quaternion rotation = target.ViewRotation;
            Vector3 eye = target.EyePosition;
            Vector3 position = eye;

            if (blend > 0f)
            {
                Vector3 desired = eye + rotation * shoulderOffset;
                position = Vector3.Lerp(eye, ClipAgainstWalls(eye, desired), blend);
            }

            transform.SetPositionAndRotation(position, rotation);

            if (cam)
            {
                cam.fieldOfView = Mathf.Min(Camera.HorizontalToVerticalFieldOfView(horizontalFov, cam.aspect), maxVerticalFov);
                // Своё тело видно только из-за плеча.
                cam.cullingMask = blend > 0.3f ? ~0 : ~(1 << LocalPlayerCamera.OwnBodyLayer);
            }
        }

        /// <summary>Не дать камере уйти в стену: ближайшее препятствие (кроме пианино и игроков).</summary>
        Vector3 ClipAgainstWalls(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float distance = dir.magnitude;
            if (distance < 0.001f) return to;
            dir /= distance;

            int count = Physics.SphereCastNonAlloc(from, collisionRadius, dir, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                var col = hits[i].collider;
                if (col.GetComponentInParent<PianoBody>() || col.GetComponentInParent<PlayerController>()) continue;
                if (hits[i].distance > 0f) nearest = Mathf.Min(nearest, hits[i].distance);
            }
            return from + dir * Mathf.Max(0.3f, nearest - 0.05f);
        }
    }
}
