using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Руки игрока: две кисти и «руки-линии» от плеч.
    /// Свободен — руки по бокам. Зажал хват — тянет руки вперёд.
    /// Держит — кисти прямо на точке хвата (руки тянутся, если пианино отстаёт).
    /// Только визуал, на физику не влияет. Добавляется автоматически из GrabSystem.
    /// </summary>
    [RequireComponent(typeof(GrabSystem))]
    public class PlayerHands : MonoBehaviour
    {
        [SerializeField] float shoulderHeight = 0.3f;
        [SerializeField] float shoulderWidth = 0.3f;
        [SerializeField] float handSize = 0.2f;
        [SerializeField] float armWidth = 0.09f;
        [SerializeField] float handSpeed = 20f;

        GrabSystem grab;
        PlayerController player;
        readonly Transform[] hands = new Transform[2];
        readonly LineRenderer[] arms = new LineRenderer[2];
        readonly Vector3[] handPositions = new Vector3[2];
        MaterialPropertyBlock tint;
        Color appliedColor;

        void Start()
        {
            grab = GetComponent<GrabSystem>();
            player = GetComponent<PlayerController>();
            var bodyRenderer = GetComponentInChildren<Renderer>(); // тело в униформе
            Material mat = bodyRenderer ? bodyRenderer.sharedMaterial : null;

            for (int i = 0; i < 2; i++)
            {
                var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hand.name = i == 0 ? "Hand_L" : "Hand_R";
                var col = hand.GetComponent<Collider>();
                col.enabled = false;
                Destroy(col);
                hand.transform.localScale = Vector3.one * handSize;
                if (mat) hand.GetComponent<Renderer>().sharedMaterial = mat;
                hands[i] = hand.transform;

                var arm = new GameObject(i == 0 ? "Arm_L" : "Arm_R").AddComponent<LineRenderer>();
                arm.positionCount = 2;
                arm.useWorldSpace = true;
                arm.widthMultiplier = armWidth;
                arm.numCapVertices = 4;
                arm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (mat) arm.sharedMaterial = mat;
                arms[i] = arm;

                handPositions[i] = RestPosition(i);
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < 2; i++)
            {
                if (hands[i]) Destroy(hands[i].gameObject);
                if (arms[i]) Destroy(arms[i].gameObject);
            }
        }

        void LateUpdate()
        {
            if (!hands[0]) return;

            // Руки в цвет униформы игрока.
            if (player && player.color != appliedColor)
            {
                appliedColor = player.color;
                tint ??= new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", appliedColor);
                tint.SetColor("_Color", appliedColor);
                for (int k = 0; k < 2; k++)
                {
                    hands[k].GetComponent<Renderer>().SetPropertyBlock(tint);
                    arms[k].SetPropertyBlock(tint);
                }
            }

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 shoulder = transform.position + Vector3.up * shoulderHeight + transform.right * side * shoulderWidth;

                if (grab.IsHolding)
                {
                    // Кисти прямо на точке хвата, чуть разведены.
                    handPositions[i] = grab.HoldPosition + transform.right * side * 0.12f;
                }
                else
                {
                    Vector3 target = player.CurrentInput.Grab ? ReachPosition(i) : RestPosition(i);
                    handPositions[i] = Vector3.Lerp(handPositions[i], target, 1f - Mathf.Exp(-handSpeed * Time.deltaTime));
                }

                hands[i].position = handPositions[i];
                arms[i].SetPosition(0, shoulder);
                arms[i].SetPosition(1, handPositions[i]);
            }
        }

        Vector3 RestPosition(int i)
        {
            float side = i == 0 ? -1f : 1f;
            return transform.position + transform.right * side * 0.45f - Vector3.up * 0.15f;
        }

        Vector3 ReachPosition(int i)
        {
            float side = i == 0 ? -1f : 1f;
            // Тянемся туда, куда смотрим.
            Vector3 eye = player.Look ? player.Look.EyePosition : transform.position;
            Vector3 dir = player.Look ? player.Look.LookDirection : transform.forward;
            return eye + dir * 0.8f - Vector3.up * 0.25f + transform.right * side * 0.2f;
        }
    }
}
