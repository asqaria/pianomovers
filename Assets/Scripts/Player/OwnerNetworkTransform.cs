using Unity.Netcode.Components;

namespace PianoMovers
{
    /// <summary>
    /// NetworkTransform, которым управляет владелец объекта, а не сервер.
    /// Игрок двигает себя сам — без задержки на путь до хоста и обратно.
    /// </summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
