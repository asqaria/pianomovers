using Unity.Netcode;

namespace PianoMovers
{
    /// <summary>Короткие проверки сетевой роли для обычных (не сетевых) компонентов.</summary>
    public static class Net
    {
        /// <summary>Игра запущена как хост или клиент.</summary>
        public static bool Online => NetworkManager.Singleton && NetworkManager.Singleton.IsListening;

        /// <summary>Этот экземпляр считает физику пианино (хост).</summary>
        public static bool IsServer => NetworkManager.Singleton && NetworkManager.Singleton.IsServer;
    }
}
