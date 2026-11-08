using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PianoMovers
{
    /// <summary>
    /// Локальный ввод для разделённого экрана.
    /// Игрок 1: WASD + мышь, ЛКМ (или Space) — держать хват, ПКМ (или Left Shift) — поднять.
    /// Игрок 2: первый геймпад — левый стик, правый стик, RT/RB — хват, LT/LB — поднять.
    ///          Без геймпада: стрелки, Numpad 4/6/8/5 — взгляд, Numpad 0 / Right Ctrl — хват, Right Shift — поднять.
    /// Если подключено два геймпада, второй тоже управляет игроком 1.
    /// </summary>
    public class LocalInputSource : MonoBehaviour, IPlayerInputSource
    {
        public int playerIndex;
        [Tooltip("Градусов на пиксель движения мыши")]
        public float mouseSensitivity = 0.12f;
        [Tooltip("Градусов в секунду при полностью отклонённом стике")]
        public float stickLookSpeed = 160f;
        [Tooltip("Градусов в секунду при взгляде клавишами")]
        public float keyLookSpeed = 110f;

        public PlayerInputState ReadInput()
        {
            var state = new PlayerInputState();
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pads = Gamepad.all;
            float dt = Time.deltaTime;
            Gamepad pad = null;

            if (playerIndex == 0)
            {
                if (kb != null)
                {
                    state.Move = new Vector2(Axis(kb.dKey, kb.aKey), Axis(kb.wKey, kb.sKey));
                    state.Grab = kb.spaceKey.isPressed;
                    state.Lift = kb.leftShiftKey.isPressed;
                    state.FreeLook = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed; // на Mac — Option
                }
                if (mouse != null)
                {
                    state.Grab |= mouse.leftButton.isPressed;
                    state.Lift |= mouse.rightButton.isPressed;
                    if (Cursor.lockState == CursorLockMode.Locked)
                        state.Look += mouse.delta.ReadValue() * mouseSensitivity;
                }
                // По сети у каждого свой компьютер: игрок 1 = клавиатура, мышь и первый геймпад.
                if (pads.Count >= 1) pad = pads[0];
            }
            else
            {
                if (kb != null)
                {
                    state.Move = new Vector2(Axis(kb.rightArrowKey, kb.leftArrowKey), Axis(kb.upArrowKey, kb.downArrowKey));
                    state.Look = new Vector2(Axis(kb.numpad6Key, kb.numpad4Key), Axis(kb.numpad8Key, kb.numpad5Key)) * keyLookSpeed * dt;
                    state.Grab = kb.numpad0Key.isPressed || kb.rightCtrlKey.isPressed;
                    state.Lift = kb.rightShiftKey.isPressed;
                }
                if (pads.Count >= 1) pad = pads[0];
            }

            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > state.Move.sqrMagnitude) state.Move = stick;
                state.Look += pad.rightStick.ReadValue() * stickLookSpeed * dt;
                state.Grab |= pad.rightTrigger.isPressed || pad.rightShoulder.isPressed;
                state.Lift |= pad.leftTrigger.isPressed || pad.leftShoulder.isPressed;
                state.FreeLook |= pad.rightStickButton.isPressed;
            }

            state.Move = Vector2.ClampMagnitude(state.Move, 1f);
            return state;
        }

        static float Axis(KeyControl positive, KeyControl negative) =>
            (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
    }
}
