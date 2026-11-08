using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Меню подключения (OnGUI).
    /// Через интернет — Unity Relay: хост получает короткий код, друг вводит его. Порты открывать не нужно.
    /// Без интернета — прямое подключение по IP (два окна на одном ПК: 127.0.0.1).
    /// </summary>
    public class ConnectionMenu : MonoBehaviour
    {
        const int MaxPlayers = 4;
        const ushort LocalPort = 7777;

        string joinCode = "";
        string ip = "127.0.0.1";
        string status = "";
        string hostedCode;
        ISession session;
        bool busy;
        GUIStyle title, big;

        void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (!nm) return;

            title ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big ??= new GUIStyle(GUI.skin.button) { fontSize = 18 };
            var field = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            var label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };

            if (nm.IsListening)
            {
                DrawSessionPanel(nm, label);
                return;
            }

            float w = 420f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, Screen.height * 0.2f, w, 520f), GUI.skin.box);
            GUILayout.Label("Piano Movers — прототип", title, GUILayout.Height(44f));
            GUI.enabled = !busy;

            GUILayout.Space(10f);
            GUILayout.Label("Через интернет", label);
            if (GUILayout.Button("Создать игру", big, GUILayout.Height(44f))) HostRelay();

            GUILayout.Space(8f);
            GUILayout.Label("Код от друга:", label);
            joinCode = GUILayout.TextField(joinCode, 12, field, GUILayout.Height(36f)).ToUpperInvariant();
            if (GUILayout.Button("Подключиться по коду", big, GUILayout.Height(44f)) && joinCode.Trim().Length > 0)
                JoinRelay(joinCode.Trim());

            GUILayout.Space(16f);
            GUILayout.Label("Без интернета (тест на одном ПК или в локальной сети)", label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Хост", big, GUILayout.Height(36f))) HostLocal(nm);
            ip = GUILayout.TextField(ip, 40, field, GUILayout.Height(36f));
            if (GUILayout.Button("Клиент", big, GUILayout.Height(36f))) JoinLocal(nm);
            GUILayout.EndHorizontal();

            GUI.enabled = true;
            if (!string.IsNullOrEmpty(status))
            {
                GUILayout.Space(10f);
                GUILayout.Label(status, label);
            }
            GUILayout.EndArea();
        }

        void DrawSessionPanel(NetworkManager nm, GUIStyle label)
        {
            float w = 300f;
            GUILayout.BeginArea(new Rect(Screen.width - w - 10f, 10f, w, 140f), GUI.skin.box);
            if (!string.IsNullOrEmpty(hostedCode))
            {
                GUILayout.Label($"Код для друга: <b>{hostedCode}</b>", new GUIStyle(label) { richText = true, fontSize = 20 });
                if (GUILayout.Button("Скопировать код")) GUIUtility.systemCopyBuffer = hostedCode;
            }
            int players = nm.IsServer ? nm.ConnectedClientsIds.Count : -1;
            GUILayout.Label(players > 0 ? $"Игроков: {players}" : (nm.IsHost ? "Хост" : "Подключено"), label);
            if (GUILayout.Button("Выйти")) Leave(nm);
            GUILayout.EndArea();
        }

        // ---------- Через интернет (Unity Relay) ----------

        async void HostRelay()
        {
            busy = true;
            status = "Подключаемся к сервисам Unity…";
            try
            {
                await EnsureSignedIn();
                status = "Создаём игру…";
                var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                hostedCode = session.Code;
                status = "";
            }
            catch (Exception e)
            {
                status = "Не удалось создать игру: " + e.Message;
                Debug.LogException(e);
            }
            busy = false;
        }

        async void JoinRelay(string code)
        {
            busy = true;
            status = "Подключаемся…";
            try
            {
                await EnsureSignedIn();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                status = "";
            }
            catch (Exception e)
            {
                status = "Не удалось подключиться: " + e.Message;
                Debug.LogException(e);
            }
            busy = false;
        }

        static async Task EnsureSignedIn()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                // Свой анонимный профиль на каждый запуск — чтобы два окна на одном ПК были разными игроками.
                var options = new InitializationOptions();
                options.SetProfile("p" + UnityEngine.Random.Range(0, 1_000_000));
                await UnityServices.InitializeAsync(options);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // ---------- Без интернета ----------

        void HostLocal(NetworkManager nm)
        {
            if (nm.NetworkConfig.NetworkTransport is UnityTransport utp)
                utp.SetConnectionData("127.0.0.1", LocalPort, "0.0.0.0");
            status = nm.StartHost() ? "" : "Не удалось запустить хост (порт 7777 занят?)";
            hostedCode = null;
        }

        void JoinLocal(NetworkManager nm)
        {
            if (nm.NetworkConfig.NetworkTransport is UnityTransport utp)
                utp.SetConnectionData(ip.Trim(), LocalPort);
            status = nm.StartClient() ? "Подключаемся…" : "Не удалось запустить клиент";
        }

        void Leave(NetworkManager nm)
        {
            if (session != null)
            {
                _ = session.LeaveAsync(); // сессия Relay закроется сама, ждать не нужно
                session = null;
            }
            nm.Shutdown();
            hostedCode = null;
            status = "";
        }
    }
}
