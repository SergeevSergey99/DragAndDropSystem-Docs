using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Tests
{
    /// <summary>
    /// Fluent builder that assembles a minimal, fully-initialized UniversalInventory
    /// for integration tests. Handles the ceremony: GameObject hierarchy, slot container,
    /// TestSlot prefab, reflection-injection of [SerializeField] privates, and manual Awake.
    ///
    /// Usage:
    ///   var inv = new InventoryBuilder()
    ///                 .WithStrategy(new UniqueItemStrategy())
    ///                 .WithFixedSlots(4)
    ///                 .Build();
    ///   // ... test ...
    ///   InventoryBuilder.Destroy(inv); // or destroy root GameObject in [TearDown]
    /// </summary>
    public sealed class InventoryBuilder
    {
        private InventoryStrategyBase _strategy = new StackableItemStrategy();
        private int _slotCount = 4;
        private int? _maxStackSize;
        private bool _allowItemOverride;
        private DropPolicySettings _dropPolicy;
        private SlotManagementSettingsBase _slotManagementSettings = new FixedSlotManagementSettings();
        private bool _useGridTopology;
        private GridTopology _gridTopology = new GridTopology(1, 1);
        private string _name = "TestInventory";
        private int _preexistingSlotCount;
        private bool _invokeLifecycleStart = true;

        public InventoryBuilder WithStrategy(InventoryStrategyBase strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            return this;
        }

        public InventoryBuilder WithFixedSlots(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            _slotCount = count;
            return this;
        }

        /// <summary>
        /// Only applies to stack-based strategies (Stackable / SeparableStacks).
        /// 0 = unlimited. Silently ignored for UniqueItemStrategy.
        /// </summary>
        public InventoryBuilder WithMaxStackSize(int max, bool allowItemOverride = false)
        {
            _maxStackSize = max;
            _allowItemOverride = allowItemOverride;
            return this;
        }

        public InventoryBuilder WithDropPolicy(DropPolicySettings policy)
        {
            _dropPolicy = policy;
            return this;
        }

        public InventoryBuilder WithSlotManagementSettings(SlotManagementSettingsBase settings)
        {
            _slotManagementSettings = settings ?? throw new ArgumentNullException(nameof(settings));
            return this;
        }

        public InventoryBuilder WithGridTopology(int columns, int rows)
        {
            _useGridTopology = true;
            _gridTopology = new GridTopology(columns, rows);
            return this;
        }

        public InventoryBuilder WithName(string name)
        {
            _name = name ?? "TestInventory";
            return this;
        }

        /// <summary>
        /// Puts slots inside the slot container and into the serialized _slots list before the
        /// inventory component exists, reproducing a prefab that ships with design-time slots
        /// already cached in the Inspector.
        /// </summary>
        public InventoryBuilder WithPreexistingSlots(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            _preexistingSlotCount = count;
            return this;
        }

        /// <summary>
        /// Skips the manual Start() call, leaving the inventory in the state a freshly
        /// instantiated prefab is in: fields serialized, strategy not yet initialized.
        /// PlayMode tests use this to control when initialization actually happens.
        /// </summary>
        public InventoryBuilder WithoutLifecycleStart()
        {
            _invokeLifecycleStart = false;
            return this;
        }

        public UniversalInventory Build() => Build<UniversalInventory>();

        public T Build<T>() where T : UniversalInventory
        {
            if (_maxStackSize.HasValue && _strategy is StackBasedInventoryStrategyBase)
                _strategy.SetMaxStackSize(_maxStackSize.Value, _allowItemOverride);

            var root = new GameObject(_name);

            var containerGo = new GameObject("SlotContainer");
            containerGo.transform.SetParent(root.transform);

            // Slot prefab lives outside the slot container so CacheSlots does not pick it up.
            var prefabGo = new GameObject("SlotPrefab");
            prefabGo.transform.SetParent(root.transform);
            var prefab = prefabGo.AddComponent<TestSlot>();

            // Slots the "prefab" was authored with: children of the container and already listed
            // in _slots, the way the Inspector would have serialized them.
            var preexistingSlots = new List<BaseSlot>();
            for (int i = 0; i < _preexistingSlotCount; i++)
            {
                var slotGo = new GameObject($"DesignTimeSlot{i}");
                slotGo.transform.SetParent(containerGo.transform);
                preexistingSlots.Add(slotGo.AddComponent<TestSlot>());
            }

            var inventory = root.AddComponent<T>();

            SetField(inventory, "_slotContainer", containerGo.transform);
            SetField(inventory, "baseSlotPrefab", prefab);
            SetField(inventory, "_initialSlotCount", _slotCount);
            SetField(inventory, "_inventoryStrategy", _strategy);
            SetField(inventory, "_slots", preexistingSlots);
            SetField(inventory, "_slotManagementSettings", _slotManagementSettings);
            SetField(inventory, "_useGridTopology", _useGridTopology);
            SetField(inventory, "_gridTopology", _gridTopology);
            if (_dropPolicy != null)
                SetField(inventory, "_dropPolicy", _dropPolicy);

            // In EditMode, Unity lifecycle methods do not fire for this test object.
            // Invoke Start manually after fields are injected so slots/strategies are ready.
            if (_invokeLifecycleStart)
                InvokeLifecycleMethod(inventory, "Start");

            return inventory;
        }

        /// <summary>
        /// Destroys the inventory's root GameObject (and all created slots). Safe to call on null.
        /// </summary>
        public static void Destroy(UniversalInventory inventory)
        {
            if (inventory == null) return;
            var go = inventory.gameObject;
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
        }

        // --- reflection helpers -------------------------------------------------

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            if (field == null)
                throw new InvalidOperationException(
                    $"InventoryBuilder: field '{fieldName}' not found on {target.GetType().Name}");

            field.SetValue(target, value);
        }

        private static void InvokeLifecycleMethod(MonoBehaviour target, string methodName)
        {
            var type = target.GetType();
            MethodInfo method = null;
            while (type != null && method == null)
            {
                method = type.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            method?.Invoke(target, null);
        }
    }
}
