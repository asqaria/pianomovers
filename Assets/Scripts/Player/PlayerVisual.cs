using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Процедурная «анимация» грузчика без аниматора:
    /// ботинки шагают, тело покачивается при ходьбе, сплющивается от натуги при подъёме,
    /// лицо краснеет, когда кончаются силы. Только визуал.
    /// Ожидает иерархию от PrototypeSceneBuilder: Visual/Body, Visual/HeadPivot/Head, Visual/Foot_L, Visual/Foot_R.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color Exhausted = new Color(0.95f, 0.3f, 0.25f);

        [SerializeField] float stepRate = 4.5f;
        [SerializeField] float stepLength = 0.18f;
        [SerializeField] float stepHeight = 0.12f;

        Transform body, head, footL, footR;
        Vector3 bodyPos, bodyScale, headPos, footLPos, footRPos;
        Renderer headRenderer;
        Color skin = Color.white;
        MaterialPropertyBlock block;

        PlayerController player;
        Renderer[] uniformParts;
        Color appliedColor;
        MaterialPropertyBlock uniformBlock;
        Vector3 lastPosition;
        Vector3 smoothedVelocity;
        GrabSystem grab;
        PlayerStamina stamina;
        PlayerLook look;
        float phase;

        void Start()
        {
            player = GetComponent<PlayerController>();
            look = GetComponent<PlayerLook>();
            lastPosition = transform.position;
            grab = GetComponent<GrabSystem>();
            stamina = GetComponent<PlayerStamina>();

            var visual = transform.Find("Visual");
            if (!visual)
            {
                enabled = false;
                return;
            }

            body = visual.Find("Body");
            head = visual.Find("HeadPivot");
            footL = visual.Find("Foot_L");
            footR = visual.Find("Foot_R");
            if (!body || !head || !footL || !footR)
            {
                enabled = false;
                return;
            }

            bodyPos = body.localPosition;
            bodyScale = body.localScale;
            headPos = head.localPosition;
            footLPos = footL.localPosition;
            footRPos = footR.localPosition;

            var headPart = head.Find("Head");
            if (headPart)
            {
                headRenderer = headPart.GetComponent<Renderer>();
                var mat = headRenderer.sharedMaterial;
                if (mat && mat.HasProperty(BaseColorId)) skin = mat.GetColor(BaseColorId);
                else if (mat && mat.HasProperty(ColorId)) skin = mat.GetColor(ColorId);
            }
            block = new MaterialPropertyBlock();
            uniformBlock = new MaterialPropertyBlock();

            // Детали униформы — красятся в цвет игрока (у каждого по сети свой).
            var parts = new System.Collections.Generic.List<Renderer>();
            foreach (var name in new[] { "Body", "HeadPivot/CapDome", "HeadPivot/CapVisor" })
            {
                var t = visual.Find(name);
                if (t && t.TryGetComponent(out Renderer r)) parts.Add(r);
            }
            uniformParts = parts.ToArray();
        }

        void LateUpdate()
        {
            // Скорость по смещению: у чужих игроков Rigidbody кинематический и скорости не знает.
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 v = (transform.position - lastPosition) / dt;
            lastPosition = transform.position;
            v.y = 0f;
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, v, 1f - Mathf.Exp(-12f * dt));
            float speed = smoothedVelocity.magnitude;

            if (player && player.color != appliedColor)
            {
                appliedColor = player.color;
                uniformBlock.SetColor(BaseColorId, appliedColor);
                uniformBlock.SetColor(ColorId, appliedColor);
                foreach (var r in uniformParts) r.SetPropertyBlock(uniformBlock);
            }
            float walk = Mathf.Clamp01(speed / 2f);
            phase += speed * stepRate * Time.deltaTime;

            // Шаги: ботинок идёт вперёд-назад и приподнимается, когда идёт вперёд.
            float s = Mathf.Sin(phase);
            footL.localPosition = footLPos + new Vector3(0f, Mathf.Max(0f, s) * stepHeight * walk, s * stepLength * walk);
            footR.localPosition = footRPos + new Vector3(0f, Mathf.Max(0f, -s) * stepHeight * walk, -s * stepLength * walk);

            // Покачивание при ходьбе и сплющивание от натуги.
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.05f * walk;
            float strain = grab && grab.IsLifting ? grab.Effort01 : 0f;
            float wobble = strain * (0.06f + 0.02f * Mathf.Sin(Time.time * 40f)); // дрожит от напряжения
            body.localScale = Vector3.Scale(bodyScale, new Vector3(1f + wobble, 1f - wobble, 1f + wobble));
            body.localPosition = bodyPos + Vector3.up * bob;
            head.localPosition = headPos + Vector3.up * (bob - 0.08f * strain);
            // Голова кивает за взглядом — напарник видит, куда ты смотришь.
            if (look) head.localRotation = Quaternion.Euler(look.Pitch * 0.6f, 0f, 0f);

            // Лицо краснеет, когда выносливость на исходе.
            if (headRenderer && stamina)
            {
                float tired = 1f - Mathf.InverseLerp(0f, 0.4f, stamina.Normalized);
                Color c = Color.Lerp(skin, Exhausted, tired * 0.8f);
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                headRenderer.SetPropertyBlock(block);
            }
        }
    }
}
