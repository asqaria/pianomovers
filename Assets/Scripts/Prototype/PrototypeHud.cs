using UnityEngine;
using UnityEngine.InputSystem;

namespace PianoMovers
{
    /// <summary>
    /// Отладочный HUD (OnGUI) для своего игрока: прицел, подсказка, выносливость,
    /// состояние пианино и итог раунда.
    /// Хост: 1/2/3 — пресет, R — новый раунд. Все: C — концерт, Esc — отпустить мышь, клик — захватить.
    /// </summary>
    public class PrototypeHud : MonoBehaviour
    {
        DeliveryZone delivery;
        PianoCondition piano;

        GUIStyle label, center;

        void Start()
        {
            if (!FindFirstObjectByType<PlaytestLogger>()) gameObject.AddComponent<PlaytestLogger>();
        }

        void Update()
        {
            // Пианино и зону создаёт хост по сети — находим их, когда появятся.
            if (!delivery) delivery = DeliveryZone.Current;
            if (!piano) piano = FindFirstObjectByType<PianoCondition>();

            var local = PlayerController.Local;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // Мышь: Esc — отпустить (чтобы нажать «Выйти»), клик — снова захватить.
            if (local)
            {
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked
                         && !OverSessionPanel(mouse.position.ReadValue()))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            if (kb == null || !local) return;
            if (kb.rKey.wasPressedThisFrame && RoundManager.Instance) RoundManager.Instance.RequestReset();
            if (kb.cKey.wasPressedThisFrame && piano)
            {
                var concert = piano.GetComponent<ConcertPlayer>();
                if (concert) concert.RequestPlay();
            }
        }

        // Панель «Выйти» в правом верхнем углу (её рисует ConnectionMenu).
        static bool OverSessionPanel(Vector2 mouse) => mouse.x > Screen.width - 320f && mouse.y > Screen.height - 160f;

        void OnGUI()
        {
            var local = PlayerController.Local;
            if (!local) return;

            label ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            center ??= new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };

            DrawPlayer(local);
            DrawGlobal(Net.IsServer);
        }

        void DrawGlobal(bool host)
        {
            float y = 6f;
            var s = PhysicsTuning.Current;
            string keys = host ? "[1][2][3] — пресет   R — новый раунд   " : "";
            Shadowed(new Rect(10f, y, 1200f, 24f),
                $"Пресет: {(s ? s.presetName : "—")}   {keys}C — концерт   Esc — мышь", label);
            y += 22f;

            if (piano)
            {
                Shadowed(new Rect(10f, y, 1200f, 24f),
                    $"Сильных ударов: {piano.HardHits}   Расстройка: {piano.AverageDetune:0.00} полутона", label);
                y += 22f;
            }

            if (delivery)
            {
                if (delivery.Delivered)
                    Shadowed(new Rect(10f, y, 1200f, 24f), $"ДОСТАВЛЕНО за {delivery.DeliveryTime:0} с. Оплата: {delivery.FinalScore} из 1000", label);
                else if (delivery.SettleProgress > 0f)
                    Shadowed(new Rect(10f, y, 1200f, 24f), $"Ставим на место… {delivery.SettleProgress * 100f:0}%", label);
            }
        }

        void DrawPlayer(PlayerController p)
        {
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var grab = p.GetComponent<GrabSystem>();
            var stamina = p.GetComponent<PlayerStamina>();
            bool gamepad = Gamepad.current != null && Mouse.current == null;
            string grabKey = gamepad ? "RT" : "ЛКМ / Space";
            string liftKey = gamepad ? "LT" : "ПКМ / Shift";

            // Прицел: точка; на пианино в досягаемости — крупнее и жёлтая; держишь — цвет игрока.
            Color dot = new Color(1f, 1f, 1f, 0.8f);
            float size = 6f;
            string hint = null;
            if (grab && grab.IsHolding)
            {
                dot = p.color;
                size = 12f;
                hint = grab.IsLifting ? "поднимаешь" : $"держишь — {liftKey}: поднять";
            }
            else if (grab && grab.LookTarget)
            {
                dot = new Color(1f, 0.85f, 0.1f);
                size = 12f;
                hint = $"{grabKey} (держать): схватить";
            }

            Fill(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), dot);
            if (hint != null) Shadowed(new Rect(c.x - 200f, c.y + 14f, 400f, 24f), hint, center);

            if (stamina)
            {
                float w = 260f, h = 10f;
                var bar = new Rect(c.x - w / 2f, Screen.height - 30f, w, h);
                Fill(bar, new Color(0f, 0f, 0f, 0.5f));
                Color fillColor = stamina.Exhausted ? new Color(0.9f, 0.25f, 0.2f) : p.color;
                Fill(new Rect(bar.x, bar.y, w * stamina.Normalized, h), fillColor);
                if (stamina.Exhausted) Shadowed(new Rect(c.x - 200f, bar.y - 26f, 400f, 24f), "выдохся!", center);
            }
        }

        static void Fill(Rect r, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);
            GUI.color = old;
            GUI.Label(r, text, style);
        }
    }
}
