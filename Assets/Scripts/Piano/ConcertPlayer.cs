using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Финальный «концерт заказчика»: мелодия играется на текущем состоянии пианино,
    /// поэтому каждая расстроенная клавиша слышна. Играет хост, слышат все.
    /// Мелодия — «Ода к радости» (Бетховен, общественное достояние).
    /// </summary>
    [RequireComponent(typeof(PianoSound))]
    public class ConcertPlayer : NetworkBehaviour
    {
        // Полутоны от C4 и длительности в долях.
        static readonly int[] Notes = { 4, 4, 5, 7, 7, 5, 4, 2, 0, 0, 2, 4, 4, 2, 2 };
        static readonly float[] Beats = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.5f, 0.5f, 2 };

        [SerializeField] float beatSeconds = 0.38f;

        PianoSound sound;

        public bool IsPlaying { get; private set; }

        void Awake() => sound = GetComponent<PianoSound>();

        /// <summary>Попросить хост сыграть концерт (можно с любого компьютера).</summary>
        public void RequestPlay()
        {
            if (IsServer) Play();
            else if (IsSpawned) RequestPlayRpc();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void RequestPlayRpc() => Play();

        /// <summary>Только на хосте.</summary>
        public void Play()
        {
            if (IsServer && !IsPlaying) StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            for (int i = 0; i < Notes.Length; i++)
            {
                sound.PlayKey(PianoCondition.MiddleCKey + Notes[i], 0.9f);
                yield return new WaitForSeconds(Beats[i] * beatSeconds);
            }
            IsPlaying = false;
        }
    }
}
