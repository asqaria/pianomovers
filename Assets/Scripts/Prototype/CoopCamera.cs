using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Общая камера: держит в кадре всех игроков и пианино, отъезжает, когда они расходятся.
    /// </summary>
    public class CoopCamera : MonoBehaviour
    {
        public Transform[] targets;
        public Vector3 offset = new Vector3(0f, 9f, -8f);
        public float minScale = 1f;
        public float maxScale = 1.8f;
        [Tooltip("При таком разбросе целей камера отъезжает на maxScale")]
        public float spreadForMaxScale = 12f;
        public float smoothing = 4f;

        void LateUpdate()
        {
            if (targets == null) return;

            Bounds bounds = default;
            int count = 0;
            foreach (var t in targets)
            {
                if (!t) continue;
                if (count == 0) bounds = new Bounds(t.position, Vector3.zero);
                else bounds.Encapsulate(t.position);
                count++;
            }
            if (count == 0) return;

            float scale = Mathf.Lerp(minScale, maxScale, bounds.size.magnitude / spreadForMaxScale);
            Vector3 desired = bounds.center + offset * scale;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
        }
    }
}
