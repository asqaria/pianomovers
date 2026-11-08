using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PianoMovers.EditorTools
{
    /// <summary>
    /// Меню «Piano Movers → Build Prototype Scene»: создаёт пресеты физики, материалы, сетевой префаб игрока
    /// и сетевую тестовую сцену из примитивов: старт, дверной проём, узкая лестница, площадка с зоной доставки.
    /// Игроков на сцене нет — их создаёт NetworkManager для каждого подключившегося.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string Root = "Assets/PianoMovers/Generated";
        const string ScenePath = Root + "/PrototypeScene.unity";

        // Лестница: подъём на 3 м на длине 6 м (≈26,6°).
        const float PianoScale = 1.25f;

        const float StairStartZ = 2f, StairEndZ = 8f, UpperY = 3f, StairHalfWidth = 1.2f;

        [MenuItem("Piano Movers/Build Prototype Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(Root);

            var presets = new[]
            {
                Preset("Preset_1_Realism", s =>
                {
                    s.presetName = "Реализм";
                    s.pianoMass = 100f; s.pianoLinearDamping = 0.2f; s.pianoAngularDamping = 1f;
                    s.pianoMaxSpeed = 8f; s.pianoMaxAngularSpeed = 6f;
                    s.maxGripForce = 560f; s.carrySpeedMultiplier = 0.4f; s.reactionShare = 0.5f;
                    s.staminaDrainPerSecond = 25f; s.noteThreshold = 0.5f; s.damageThreshold = 1.5f;
                }),
                Preset("Preset_2_Middle", s => { s.presetName = "Середина"; }),
                Preset("Preset_3_Arcade", s =>
                {
                    s.presetName = "Аркада";
                    s.pianoMass = 50f; s.pianoLinearDamping = 1.2f; s.pianoAngularDamping = 4f;
                    s.pianoMaxSpeed = 5f; s.pianoMaxAngularSpeed = 2f;
                    s.maxGripForce = 420f; s.carrySpeedMultiplier = 0.7f; s.reactionShare = 0.15f;
                    s.staminaDrainPerSecond = 10f; s.damageThreshold = 3f;
                }),
            };

            var mFloor = Mat("Floor", new Color(0.55f, 0.55f, 0.58f));
            var mWall = Mat("Wall", new Color(0.85f, 0.82f, 0.76f));
            var mStair = Mat("Stair", new Color(0.62f, 0.45f, 0.3f));
            var mZone = Mat("Zone", new Color(0.3f, 0.85f, 0.4f));
            var mP1 = Mat("Player1", new Color(0.9f, 0.3f, 0.25f));

            var pianoMats = new PianoMaterials
            {
                Wood = Mat("PianoWood", new Color(0.16f, 0.08f, 0.05f), smoothness: 0.8f),
                WhiteKey = Mat("KeyWhite", new Color(0.96f, 0.95f, 0.9f), smoothness: 0.6f),
                BlackKey = Mat("KeyBlack", new Color(0.05f, 0.05f, 0.05f), smoothness: 0.7f),
                Brass = Mat("Brass", new Color(0.85f, 0.65f, 0.25f), smoothness: 0.75f, metallic: 0.8f),
            };
            var mSkin = Mat("Skin", new Color(1f, 0.8f, 0.65f), smoothness: 0.2f);
            var mDark = Mat("Dark", new Color(0.15f, 0.15f, 0.17f), smoothness: 0.2f);
            var mEyeWhite = Mat("EyeWhite", Color.white, smoothness: 0.6f);
            var mPupil = Mat("Pupil", new Color(0.05f, 0.05f, 0.05f), smoothness: 0.8f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Префаб игрока: по сети его создаёт NetworkManager для каждого подключившегося.
            var looks = new PlayerMaterials { Uniform = mP1, Skin = mSkin, Dark = mDark, EyeWhite = mEyeWhite, Pupil = mPupil };
            var playerPrefab = BuildPlayerPrefab(looks);
            BuildNetworkManager(playerPrefab);

            // Общий сетевой объект: пресеты физики и раунды.
            var game = new GameObject("Game");
            game.AddComponent<NetworkObject>();
            var tuning = game.AddComponent<PhysicsTuning>();
            tuning.presets = presets;
            tuning.startIndex = 1;
            game.AddComponent<RoundManager>();

            // --- Уровень ---
            var level = new GameObject("Level").transform;
            Box("Ground", level, new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 24f), mFloor);
            Box("Wall_Left", level, new Vector3(-10f, 1.5f, -5f), new Vector3(0.3f, 3f, 14f), mWall);
            Box("Wall_Right", level, new Vector3(10f, 1.5f, -5f), new Vector3(0.3f, 3f, 14f), mWall);
            Box("Wall_Back", level, new Vector3(0f, 1.5f, -12f), new Vector3(20f, 3f, 0.3f), mWall);

            // Стена с дверным проёмом 1,8 м: пианино с людьми по бокам не пройдёт — придётся развернуть.
            Box("Door_Left", level, new Vector3(-5.45f, 1.5f, -2f), new Vector3(9.1f, 3f, 0.3f), mWall);
            Box("Door_Right", level, new Vector3(5.45f, 1.5f, -2f), new Vector3(9.1f, 3f, 0.3f), mWall);
            Box("Door_Top", level, new Vector3(0f, 2.6f, -2f), new Vector3(1.8f, 0.8f, 0.3f), mWall);

            // Стена перед лестницей с проходом шириной лестницы.
            Box("Hall_Left", level, new Vector3(-5.75f, 1.5f, StairStartZ), new Vector3(8.5f, 3f, 0.3f), mWall);
            Box("Hall_Right", level, new Vector3(5.75f, 1.5f, StairStartZ), new Vector3(8.5f, 3f, 0.3f), mWall);

            BuildStairs(level, mStair, mWall);

            // Верхняя площадка (сплошной блок) с бортиками.
            float upperDepth = 6f, upperCenterZ = StairEndZ + upperDepth / 2f;
            Box("Upper_Floor", level, new Vector3(0f, UpperY / 2f, upperCenterZ), new Vector3(10f, UpperY, upperDepth), mFloor);
            Box("Upper_Rail_Left", level, new Vector3(-5f, UpperY + 0.5f, upperCenterZ), new Vector3(0.2f, 1f, upperDepth), mWall);
            Box("Upper_Rail_Right", level, new Vector3(5f, UpperY + 0.5f, upperCenterZ), new Vector3(0.2f, 1f, upperDepth), mWall);
            Box("Upper_Rail_Back", level, new Vector3(0f, UpperY + 0.5f, StairEndZ + upperDepth), new Vector3(10f, 1f, 0.2f), mWall);

            // --- Зона доставки ---
            var zoneGo = new GameObject("DeliveryZone");
            zoneGo.transform.position = new Vector3(0f, UpperY + 0.6f, upperCenterZ + 0.5f);
            var zoneCollider = zoneGo.AddComponent<BoxCollider>();
            zoneCollider.size = new Vector3(3f, 1.2f, 3f);
            zoneCollider.isTrigger = true;
            zoneGo.AddComponent<NetworkObject>();
            var delivery = zoneGo.AddComponent<DeliveryZone>();
            Box("ZoneMarker", zoneGo.transform, zoneGo.transform.position + new Vector3(0f, -0.59f, 0f),
                new Vector3(3f, 0.02f, 3f), mZone, keepCollider: false);

            // --- Пианино ---
            var piano = BuildPiano(new Vector3(0f, 0.6f * PianoScale, -6f), pianoMats);

            // --- Обзорная камера (до подключения) и интерфейс ---
            var cam = Camera.main;
            if (cam)
            {
                var follow = cam.gameObject.AddComponent<CoopCamera>();
                follow.targets = new[] { piano.transform };
                cam.transform.position = piano.transform.position + follow.offset;
                cam.transform.rotation = Quaternion.LookRotation(-follow.offset, Vector3.up);
            }

            var hud = new GameObject("UI").AddComponent<PrototypeHud>();
            hud.delivery = delivery;
            hud.piano = piano.GetComponent<PianoCondition>();
            hud.gameObject.AddComponent<ConnectionMenu>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            // Переоткрыть и сохранить ещё раз: сетевые объекты на сцене получают свои ID,
            // только когда сцена уже лежит файлом.
            var reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(reopened);
            EditorSceneManager.SaveScene(reopened);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Piano Movers: сцена прототипа собрана → " + ScenePath + ". Нажмите Play.");
        }

        static void BuildStairs(Transform level, Material stairMat, Material wallMat)
        {
            float length = StairEndZ - StairStartZ;
            const int steps = 12;
            float stepDepth = length / steps, stepHeight = UpperY / steps;

            // Видимые ступени. Коллайдер есть только у верхней — это ровная площадка у входа наверх.
            for (int i = 0; i < steps; i++)
            {
                float h = stepHeight * (i + 1);
                Box("Step_" + i, level,
                    new Vector3(0f, h / 2f, StairStartZ + stepDepth * (i + 0.5f)),
                    new Vector3(StairHalfWidth * 2f, h, stepDepth), stairMat, keepCollider: i == steps - 1);
            }

            // Невидимый пандус ПО КРОМКАМ ступеней (как пианино реально едет по лестнице):
            // от пола за полступени до первой ступени — до кромки верхней ступени.
            // Так пианино лежит на рёбрах, а не проваливается в ступени.
            float rampStartZ = StairStartZ - stepDepth, rampEndZ = StairEndZ - stepDepth;
            float rampLength = rampEndZ - rampStartZ;
            float angle = Mathf.Atan2(UpperY, rampLength) * Mathf.Rad2Deg;
            float slopeLength = Mathf.Sqrt(rampLength * rampLength + UpperY * UpperY);
            var ramp = Box("Stair_Ramp", level, Vector3.zero, new Vector3(StairHalfWidth * 2f, 0.2f, slopeLength), stairMat);
            ramp.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 surfaceMid = new Vector3(0f, UpperY / 2f, (rampStartZ + rampEndZ) / 2f);
            ramp.transform.position = surfaceMid - ramp.transform.up * 0.1f;
            ramp.GetComponent<MeshRenderer>().enabled = false;

            // «Бум» на каждой ступеньке, когда пианино по ним едет.
            var bumps = new GameObject("StairBumps");
            bumps.transform.SetParent(level, false);
            bumps.transform.position = new Vector3(0f, UpperY / 2f + 0.75f, (StairStartZ + StairEndZ) / 2f);
            var zone = bumps.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.size = new Vector3(StairHalfWidth * 2f, UpperY + 2.5f, length);
            var stairBumps = bumps.AddComponent<StairBumps>();
            stairBumps.startZ = StairStartZ;
            stairBumps.stepDepth = stepDepth;

            // Стены лестничного пролёта — узко!
            Box("Stair_Wall_Left", level, new Vector3(-StairHalfWidth - 0.15f, 2f, (StairStartZ + StairEndZ) / 2f),
                new Vector3(0.3f, 5f, length), wallMat);
            Box("Stair_Wall_Right", level, new Vector3(StairHalfWidth + 0.15f, 2f, (StairStartZ + StairEndZ) / 2f),
                new Vector3(0.3f, 5f, length), wallMat);
        }

        class PianoMaterials
        {
            public Material Wood, WhiteKey, BlackKey, Brass;
        }

        class PlayerMaterials
        {
            public Material Uniform, Skin, Dark, EyeWhite, Pupil;
        }

        static GameObject BuildPiano(Vector3 position, PianoMaterials m)
        {
            var root = new GameObject("Piano");
            root.transform.position = position;
            // Чуть больше настоящего — от первого лица так оно ощущается тяжёлым и смешным.
            root.transform.localScale = Vector3.one * PianoScale;
            root.AddComponent<Rigidbody>();
            // Сеть: пианино считает хост, остальным приходит его положение.
            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            root.AddComponent<NetworkRigidbody>();
            root.AddComponent<PianoBody>();
            root.AddComponent<PianoCondition>();
            root.AddComponent<PianoSound>();
            root.AddComponent<PianoImpact>().halfWidth = 0.75f;
            root.AddComponent<ConcertPlayer>();

            // Физика: один простой коллайдер 1,5 × 1,2 × 0,6 м. Всё остальное — только визуал.
            var collider = new GameObject("Collider");
            collider.transform.SetParent(root.transform, false);
            collider.AddComponent<BoxCollider>().size = new Vector3(1.5f, 1.2f, 0.6f);

            // Пианино-«пианино»: клавиши смотрят на камеру (−Z).
            var v = new GameObject("Visual").transform;
            v.SetParent(root.transform, false);
            Part(PrimitiveType.Cube, "UpperCabinet", v, new Vector3(0f, 0.225f, 0.075f), new Vector3(1.5f, 0.75f, 0.45f), m.Wood);
            Part(PrimitiveType.Cube, "LowerCabinet", v, new Vector3(0f, -0.325f, 0.1f), new Vector3(1.5f, 0.55f, 0.4f), m.Wood);
            Part(PrimitiveType.Cube, "TopLid", v, new Vector3(0f, 0.62f, 0.075f), new Vector3(1.56f, 0.04f, 0.5f), m.Wood);
            Part(PrimitiveType.Cube, "KeyBed", v, new Vector3(0f, -0.1f, -0.2f), new Vector3(1.4f, 0.05f, 0.24f), m.Wood);
            Part(PrimitiveType.Cube, "Fallboard", v, new Vector3(0f, -0.01f, -0.1f), new Vector3(1.4f, 0.13f, 0.03f), m.Wood);
            Part(PrimitiveType.Cube, "Cheek_L", v, new Vector3(-0.725f, -0.05f, -0.2f), new Vector3(0.05f, 0.14f, 0.26f), m.Wood);
            Part(PrimitiveType.Cube, "Cheek_R", v, new Vector3(0.725f, -0.05f, -0.2f), new Vector3(0.05f, 0.14f, 0.26f), m.Wood);
            Part(PrimitiveType.Cube, "MusicDesk", v, new Vector3(0f, 0.22f, -0.17f), new Vector3(0.7f, 0.2f, 0.02f), m.Wood,
                Quaternion.Euler(-12f, 0f, 0f));
            Part(PrimitiveType.Cube, "Leg_L", v, new Vector3(-0.62f, -0.36f, -0.28f), new Vector3(0.07f, 0.48f, 0.07f), m.Wood);
            Part(PrimitiveType.Cube, "Leg_R", v, new Vector3(0.62f, -0.36f, -0.28f), new Vector3(0.07f, 0.48f, 0.07f), m.Wood);
            for (int i = 0; i < 3; i++)
            {
                Part(PrimitiveType.Cube, "Pedal_" + i, v, new Vector3(-0.12f + 0.12f * i, -0.56f, -0.12f),
                    new Vector3(0.05f, 0.025f, 0.12f), m.Brass);
            }

            // 21 белая клавиша (3 октавы) и чёрные по рисунку 2+3.
            var keys = new GameObject("Keys").transform;
            keys.SetParent(v, false);
            const int whiteCount = 21;
            const float keyboardWidth = 1.36f;
            float w = keyboardWidth / whiteCount;
            for (int i = 0; i < whiteCount; i++)
            {
                float x = -keyboardWidth / 2f + w * (i + 0.5f);
                Part(PrimitiveType.Cube, "White_" + i, keys, new Vector3(x, -0.062f, -0.215f),
                    new Vector3(w * 0.9f, 0.025f, 0.2f), m.WhiteKey);

                int n = i % 7; // C D E F G A B
                bool hasBlack = n == 0 || n == 1 || n == 3 || n == 4 || n == 5;
                if (hasBlack && i < whiteCount - 1)
                {
                    Part(PrimitiveType.Cube, "Black_" + i, keys, new Vector3(x + w / 2f, -0.04f, -0.17f),
                        new Vector3(w * 0.55f, 0.035f, 0.12f), m.BlackKey);
                }
            }

            // Фиксированных точек хвата нет: от первого лица хватаемся за то место, куда смотрим.
            return root;
        }

        static GameObject BuildPlayerPrefab(PlayerMaterials m)
        {
            var go = BuildPlayerObject(m);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Player.prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        static void BuildNetworkManager(GameObject playerPrefab)
        {
            var go = new GameObject("NetworkManager");
            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.ConnectionApproval = false;

            // Список сетевых префабов (в нём игрок).
            string listPath = Root + "/NetworkPrefabs.asset";
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
            if (!list)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, listPath);
            }
            bool registered = false;
            foreach (var entry in list.PrefabList)
                if (entry.Prefab == playerPrefab) registered = true;
            if (!registered) list.Add(new NetworkPrefab { Prefab = playerPrefab });
            EditorUtility.SetDirty(list);

            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
        }

        static GameObject BuildPlayerObject(PlayerMaterials m)
        {
            // Физика: корень с капсулой без масштаба. Внешность — дочерний Visual.
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0f, 0.9f, -9f);
            // Сеть: игрока двигает его владелец.
            go.AddComponent<NetworkObject>();
            var netTransform = go.AddComponent<OwnerNetworkTransform>();
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            var capsule = go.AddComponent<CapsuleCollider>();
            // Рост 1,8 м, глаза на ~1,6 м (PlayerLook.eyeHeight).
            capsule.radius = 0.36f;
            capsule.height = 1.8f;

            go.AddComponent<Rigidbody>();
            go.AddComponent<LocalInputSource>().playerIndex = 0;
            go.AddComponent<PlayerStamina>();
            go.AddComponent<PlayerLook>();
            go.AddComponent<PlayerController>();
            go.AddComponent<GrabSystem>();
            go.AddComponent<PlayerVisual>();

            var v = new GameObject("Visual").transform;
            v.SetParent(go.transform, false);
            v.localScale = Vector3.one * 0.9f; // детали размечены под 2 м

            // Тело в униформе (первый рендерер — по нему красятся руки) и ремень.
            Part(PrimitiveType.Capsule, "Body", v, new Vector3(0f, -0.15f, 0f), new Vector3(0.8f, 0.85f, 0.8f), m.Uniform);
            Part(PrimitiveType.Cylinder, "Belt", v, new Vector3(0f, -0.3f, 0f), new Vector3(0.82f, 0.04f, 0.82f), m.Dark);

            // Голова: всё лицо и кепка — дети одной опоры, чтобы двигаться вместе.
            var head = new GameObject("HeadPivot").transform;
            head.SetParent(v, false);
            head.localPosition = new Vector3(0f, 0.95f, 0f);
            Part(PrimitiveType.Sphere, "Head", head, Vector3.zero, Vector3.one * 0.6f, m.Skin);
            Part(PrimitiveType.Sphere, "Eye_L", head, new Vector3(-0.12f, 0.05f, 0.25f), Vector3.one * 0.16f, m.EyeWhite);
            Part(PrimitiveType.Sphere, "Eye_R", head, new Vector3(0.12f, 0.05f, 0.25f), Vector3.one * 0.16f, m.EyeWhite);
            Part(PrimitiveType.Sphere, "Pupil_L", head, new Vector3(-0.12f, 0.05f, 0.32f), Vector3.one * 0.07f, m.Pupil);
            Part(PrimitiveType.Sphere, "Pupil_R", head, new Vector3(0.12f, 0.05f, 0.32f), Vector3.one * 0.07f, m.Pupil);
            Part(PrimitiveType.Sphere, "CapDome", head, new Vector3(0f, 0.17f, 0f), new Vector3(0.62f, 0.34f, 0.62f), m.Uniform);
            Part(PrimitiveType.Cube, "CapVisor", head, new Vector3(0f, 0.16f, 0.3f), new Vector3(0.42f, 0.03f, 0.26f), m.Uniform);

            // Ботинки — шагают в PlayerVisual.
            Part(PrimitiveType.Cube, "Foot_L", v, new Vector3(-0.16f, -0.94f, 0.05f), new Vector3(0.2f, 0.12f, 0.34f), m.Dark);
            Part(PrimitiveType.Cube, "Foot_R", v, new Vector3(0.16f, -0.94f, 0.05f), new Vector3(0.2f, 0.12f, 0.34f), m.Dark);
            return go;
        }

        /// <summary>Визуальная деталь из примитива: без коллайдера, в локальных координатах родителя.</summary>
        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material mat, Quaternion? localRotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 worldCenter, Vector3 size, Material mat, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = worldCenter;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static PianoPhysicsSettings Preset(string fileName, Action<PianoPhysicsSettings> configure)
        {
            string path = $"{Root}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PianoPhysicsSettings>(path);
            if (existing) return existing; // не затираем то, что вы уже подкрутили

            var settings = ScriptableObject.CreateInstance<PianoPhysicsSettings>();
            configure(settings);
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        static Material Mat(string name, Color color, float smoothness = 0.3f, float metallic = 0f)
        {
            string path = $"{Root}/M_{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
