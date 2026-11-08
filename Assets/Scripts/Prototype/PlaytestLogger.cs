using System.IO;
using System.Text;
using UnityEngine;

namespace PianoMovers
{
    /// <summary>
    /// Пишет метрики каждого раунда своего игрока (от подключения или R до следующего R / выхода) в CSV:
    /// в редакторе — PianoMovers/Playtests/playtests.csv, в сборке — Application.persistentDataPath.
    /// Разделитель «;» — файл сразу открывается в Excel с русской локалью.
    /// Добавляется автоматически из PrototypeHud.
    /// </summary>
    public class PlaytestLogger : MonoBehaviour
    {
        float startTime;
        readonly float[] secondsPerPreset = new float[3];
        int presetSwitches;
        int lastPreset = -1;

        float firstGrab = -1f;
        float firstTeamLift = -1f;
        float teamLiftSeconds;
        int voluntaryReleases, overstretchedReleases, exhaustedReleases;

        DeliveryZone delivery;
        PianoCondition piano;
        bool written;

        // Снимок по ходу игры: при перезапуске сцены объекты могут уничтожиться раньше логгера.
        bool delivered;
        float deliveryTime;
        int score, hardHits;
        float averageDetune;

        public static string FilePath => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Playtests", "playtests.csv"))
            : Path.Combine(Application.persistentDataPath, "playtests.csv");

        void OnEnable()
        {
            GrabSystem.Grabbed += OnGrabbed;
            GrabSystem.Released += OnReleased;
            RoundManager.RoundReset += OnRoundReset;
        }

        void OnDisable()
        {
            GrabSystem.Grabbed -= OnGrabbed;
            GrabSystem.Released -= OnReleased;
            RoundManager.RoundReset -= OnRoundReset;
        }

        void OnRoundReset()
        {
            Write();
            ResetCounters();
        }

        void ResetCounters()
        {
            startTime = Time.time;
            System.Array.Clear(secondsPerPreset, 0, secondsPerPreset.Length);
            presetSwitches = 0;
            lastPreset = -1;
            firstGrab = -1f;
            firstTeamLift = -1f;
            teamLiftSeconds = 0f;
            voluntaryReleases = overstretchedReleases = exhaustedReleases = 0;
            delivered = false;
            deliveryTime = 0f;
            score = hardHits = 0;
            averageDetune = 0f;
            written = false;
        }

        void Start()
        {
            startTime = Time.time;
            delivery = FindFirstObjectByType<DeliveryZone>();
            piano = FindFirstObjectByType<PianoCondition>();
            Debug.Log("Плейтест: метрики пишутся в " + FilePath);
        }

        float Elapsed => Time.time - startTime;

        void Update()
        {
            // Пока не подключились — раунд не идёт.
            if (!PlayerController.Local)
            {
                if (!written && Elapsed > 5f && firstGrab >= 0f) Write(); // вышли из игры
                ResetCounters();
                return;
            }

            int preset = PhysicsTuning.CurrentIndex;
            if (preset != lastPreset)
            {
                if (lastPreset >= 0) presetSwitches++;
                lastPreset = preset;
            }
            if (preset >= 0 && preset < secondsPerPreset.Length) secondsPerPreset[preset] += Time.deltaTime;

            int lifting = 0;
            foreach (var grab in GrabSystem.All)
                if (grab.IsLifting) lifting++;

            if (lifting >= 2)
            {
                if (firstTeamLift < 0f) firstTeamLift = Elapsed;
                teamLiftSeconds += Time.deltaTime;
            }

            if (piano)
            {
                hardHits = piano.HardHits;
                averageDetune = piano.AverageDetune;
            }
            if (!delivered && delivery && delivery.Delivered)
            {
                delivered = true;
                deliveryTime = delivery.DeliveryTime;
                score = delivery.FinalScore;
            }
        }

        void OnGrabbed(GrabSystem grab)
        {
            if (firstGrab < 0f) firstGrab = Elapsed;
        }

        void OnReleased(GrabSystem grab, ReleaseReason reason)
        {
            switch (reason)
            {
                case ReleaseReason.Voluntary: voluntaryReleases++; break;
                case ReleaseReason.Overstretched: overstretchedReleases++; break;
                case ReleaseReason.Exhausted: exhaustedReleases++; break;
            }
        }

        void OnDestroy() => Write();
        void OnApplicationQuit() => Write();

        void Write()
        {
            if (written || Elapsed < 5f) return; // случайные перезапуски не пишем
            written = true;

            string[] presetNames = { "Реализм", "Середина", "Аркада" };
            int mainPreset = 0;
            for (int i = 1; i < secondsPerPreset.Length; i++)
                if (secondsPerPreset[i] > secondsPerPreset[mainPreset]) mainPreset = i;

            string[] row =
            {
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                F(Elapsed),
                presetNames[mainPreset],
                F(secondsPerPreset[0]), F(secondsPerPreset[1]), F(secondsPerPreset[2]),
                presetSwitches.ToString(),
                F(firstGrab), F(firstTeamLift), F(teamLiftSeconds),
                voluntaryReleases.ToString(), overstretchedReleases.ToString(), exhaustedReleases.ToString(),
                hardHits.ToString(),
                averageDetune.ToString("0.00"),
                delivered ? "да" : "нет",
                delivered ? F(deliveryTime) : "",
                delivered ? score.ToString() : "",
            };

            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var encoding = new UTF8Encoding(true);
                if (!File.Exists(path))
                {
                    File.WriteAllText(path,
                        "Дата;Длительность, с;Основной пресет;Реализм, с;Середина, с;Аркада, с;Смен пресета;" +
                        "Первый хват, с;Первый подъём вдвоём, с;Несли вдвоём, с;" +
                        "Отпустили сами;Сорвалось;Выдохлись;Сильных ударов;Средняя расстройка;" +
                        "Доставлено;Время доставки, с;Оплата\n", encoding);
                }
                File.AppendAllText(path, string.Join(";", row) + "\n", encoding);
                Debug.Log("Плейтест: забег записан в " + path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Плейтест: не удалось записать метрики — " + e.Message);
            }
        }

        static string F(float seconds) => seconds < 0f ? "" : seconds.ToString("0.0");
    }
}
