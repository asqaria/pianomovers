using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PianoMovers
{
    /// <summary>
    /// Активный пресет физики. Переключает только хост (клавиши 1/2/3),
    /// номер пресета синхронизируется всем игрокам.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PhysicsTuning : NetworkBehaviour
    {
        public PianoPhysicsSettings[] presets;
        public int startIndex = 1;

        public static PianoPhysicsSettings Current { get; private set; }
        public static int CurrentIndex { get; private set; }

        readonly NetworkVariable<int> netIndex = new NetworkVariable<int>(1);

        void Awake() => Apply(startIndex);

        public override void OnNetworkSpawn()
        {
            if (IsServer) netIndex.Value = CurrentIndex;
            netIndex.OnValueChanged += OnIndexChanged;
            Apply(netIndex.Value);
        }

        public override void OnNetworkDespawn() => netIndex.OnValueChanged -= OnIndexChanged;

        void OnIndexChanged(int previous, int next) => Apply(next);

        void Update()
        {
            if (!IsServer) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) SetPreset(0);
            if (kb.digit2Key.wasPressedThisFrame) SetPreset(1);
            if (kb.digit3Key.wasPressedThisFrame) SetPreset(2);
        }

        void SetPreset(int index)
        {
            Apply(index);
            netIndex.Value = CurrentIndex;
        }

        void Apply(int index)
        {
            if (presets == null || presets.Length == 0)
            {
                Debug.LogError("PhysicsTuning: не назначены пресеты физики.");
                return;
            }
            CurrentIndex = Mathf.Clamp(index, 0, presets.Length - 1);
            Current = presets[CurrentIndex];
        }
    }
}
