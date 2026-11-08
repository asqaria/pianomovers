using Unity.Netcode;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Хост при старте сам создаёт сетевой мир из префабов: пианино, зону доставки и объект игры
    /// (пресеты физики, раунды). Клиенты получают их по сети.
    /// Так надёжнее, чем сетевые объекты, лежащие в сцене: тем нужно совпадение внутренних ID
    /// у всех игроков, и оно ломается при пересохранении сцены на разных машинах.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        public GameObject pianoPrefab;
        public GameObject deliveryZonePrefab;
        public GameObject gamePrefab;

        public Vector3 pianoPosition = new Vector3(0f, 0.75f, -6f);
        public Vector3 deliveryZonePosition = new Vector3(0f, 3.6f, 11.5f);

        NetworkManager subscribed;

        void Start()
        {
            subscribed = NetworkManager.Singleton;
            if (subscribed) subscribed.OnServerStarted += SpawnWorld;
        }

        void OnDestroy()
        {
            if (subscribed) subscribed.OnServerStarted -= SpawnWorld;
        }

        void SpawnWorld()
        {
            // Порядок важен: RoundManager (в объекте игры) запоминает стартовую позицию пианино.
            Spawn(pianoPrefab, pianoPosition);
            Spawn(deliveryZonePrefab, deliveryZonePosition);
            Spawn(gamePrefab, Vector3.zero);
        }

        static void Spawn(GameObject prefab, Vector3 position)
        {
            if (!prefab)
            {
                Debug.LogError("NetworkBootstrap: не назначен префаб — пересоберите сцену (Piano Movers → Build Prototype Scene).");
                return;
            }
            var instance = Instantiate(prefab, position, prefab.transform.rotation);
            instance.GetComponent<NetworkObject>().Spawn();
        }
    }
}
