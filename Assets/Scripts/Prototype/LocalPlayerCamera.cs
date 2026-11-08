using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Камера своего игрока. Когда свой игрок появился в сети — общая камера сцены выключается,
    /// создаётся камера в глазах игрока со слушателем звука. Своё тело своей камере не видно
    /// (отдельный слой), видно только руки; чужих игроков видно полностью.
    /// </summary>
    public static class LocalPlayerCamera
    {
        const int OwnBodyLayer = 20; // безымянный пользовательский слой

        static GameObject cameraObject;
        static GameObject sceneCamera;

        public static Camera Current { get; private set; }

        public static void Attach(PlayerController player)
        {
            if (Camera.main && Camera.main.gameObject != cameraObject) sceneCamera = Camera.main.gameObject;
            if (sceneCamera) sceneCamera.SetActive(false);

            cameraObject = new GameObject("LocalPlayerCamera");
            var cam = cameraObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<FirstPersonCamera>().target = player.Look;
            player.Look.ViewCamera = cam;

            var visual = player.transform.Find("Visual");
            if (visual) SetLayerRecursive(visual, OwnBodyLayer);
            cam.cullingMask = ~(1 << OwnBodyLayer);

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
    /// Камера в глазах игрока. Позиция и поворот — из PlayerLook каждый кадр.
    /// Угол обзора задаётся по горизонтали, чтобы предметы не казались мелкими.
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        public PlayerLook target;
        public float horizontalFov = 90f;
        public float maxVerticalFov = 80f;

        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (!target) return;
            transform.SetPositionAndRotation(target.EyePosition, target.ViewRotation);
            if (cam)
                cam.fieldOfView = Mathf.Min(Camera.HorizontalToVerticalFieldOfView(horizontalFov, cam.aspect), maxVerticalFov);
        }
    }
}
