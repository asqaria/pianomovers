using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Ввод одного игрока за кадр. Физика работает только с этой структурой и не знает,
    /// откуда пришёл ввод. На этапе 2 сюда же придёт ввод клиентов по сети (FishNet).
    /// </summary>
    public struct PlayerInputState
    {
        public Vector2 Move;
        /// <summary>Поворот взгляда за этот кадр, в градусах (x — влево/вправо, y — вверх/вниз).</summary>
        public Vector2 Look;
        public bool Grab;
        public bool Lift;
    }

    public interface IPlayerInputSource
    {
        PlayerInputState ReadInput();
    }
}
