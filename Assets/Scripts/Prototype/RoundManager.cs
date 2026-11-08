using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Раунды и точки появления. R на хосте — новый раунд для всех:
    /// пианино на старт и снова настроено, зона сброшена, каждый игрок возвращается на свою точку.
    /// </summary>
    public class RoundManager : NetworkBehaviour
    {
        public static RoundManager Instance { get; private set; }

        /// <summary>У каждого игрока: начался новый раунд (для метрик плейтеста).</summary>
        public static event System.Action RoundReset;

        static readonly Vector3[] SpawnPositions =
        {
            new Vector3(-2f, 0.9f, -8.5f),
            new Vector3(2f, 0.9f, -8.5f),
            new Vector3(-1f, 0.9f, -10f),
            new Vector3(1f, 0.9f, -10f),
        };

        public static (Vector3 position, float yaw) SpawnPoint(ulong clientId) =>
            (SpawnPositions[(int)(clientId % (ulong)SpawnPositions.Length)], 0f);

        PianoBody piano;
        Vector3 pianoStartPosition;
        Quaternion pianoStartRotation;

        void Awake() => Instance = this;

        public override void OnNetworkSpawn()
        {
            piano = FindFirstObjectByType<PianoBody>();
            if (piano)
            {
                pianoStartPosition = piano.transform.position;
                pianoStartRotation = piano.transform.rotation;
            }
        }

        /// <summary>Попросить новый раунд (только хост может его начать).</summary>
        public void RequestReset()
        {
            if (IsServer) ResetRound();
        }

        void ResetRound()
        {
            if (!piano) OnNetworkSpawn(); // пианино появилось позже нас — запомнить старт сейчас
            if (piano)
            {
                var body = piano.Body;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = pianoStartPosition;
                body.rotation = pianoStartRotation;
                piano.transform.SetPositionAndRotation(pianoStartPosition, pianoStartRotation);

                var netTransform = piano.GetComponent<NetworkTransform>();
                if (netTransform)
                {
                    try { netTransform.Teleport(pianoStartPosition, pianoStartRotation, piano.transform.localScale); }
                    catch (System.Exception e) { Debug.LogWarning("Телепорт пианино не удался: " + e.Message); }
                }

                var condition = piano.GetComponent<PianoCondition>();
                if (condition) condition.ResetState();
            }

            if (DeliveryZone.Current) DeliveryZone.Current.ResetZone();
            RoundResetRpc();
        }

        [Rpc(SendTo.Everyone)]
        void RoundResetRpc()
        {
            RoundReset?.Invoke();
            if (PlayerController.Local) PlayerController.Local.Respawn();
        }
    }
}
