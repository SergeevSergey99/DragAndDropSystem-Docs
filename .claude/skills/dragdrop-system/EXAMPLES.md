# Examples & Demo Scenes

Detailed breakdown of all demo scenes.

## Demo1: Basic Inventory

**Location**: `Examples/Demo1/`

**Purpose**: Teach basic system usage.

### Features

- Basic drag & drop
- Simple DataBinding with `List<ItemSO>`
- Slot rules (weapon slot, armor slot, consumable slot)
- Fixed and Dynamic slot management

### Key Components

**ItemsSOInventoryDataBinding**:
**Location**: → `Examples/Demo1/DataBindings/ItemsSOInventoryDataBinding.cs`

**Purpose**: Simplest DataBinding example - syncs UI with List of ItemSO.

**How It Works**:
- `OnItemAddedToUI()` → Adds item to list
- `OnItemRemovedFromUI()` → Removes item from list
- `SyncToUI()` → Loads list into UI inventory

**ItemTypeExampleFilterRule**:
**Location**: → `Examples/Demo1/ItemTypeExampleFilterRule.cs`

**Purpose**: Example slot rule that filters by ItemType enum.

**How It Works**:
- Checks if `context.DraggedStack.Item` is `ItemExampleSO`
- Validates ItemType matches `_allowedType`
- Returns Success or Failure with message

### Adapters

**Purpose**: Wrap different item types to implement IInventoryItem interface.

**ItemModelAdapter** → `Examples/Demo1/Adapters/ItemModelAdapter.cs`:
- Wraps C# model classes
- Adapter pattern for POCO objects

**ItemSOAdapter** → `Examples/Demo1/Adapters/ItemSOAdapter.cs`:
- Wraps ScriptableObject items
- Most common adapter for Unity projects

**ItemSOWith3DAdapter** → `Examples/Demo1/Adapters/ItemSOWith3DAdapter.cs`:
- Extends ItemSOAdapter
- Implements IWorld3DAdapter for 3D world integration
- Provides 3D prefab and spawn offset

### Testing

Check these scenarios:
- Drag item to weapon slot → only weapons allowed
- Drag potion to armor slot → should fail with message
- Dynamic inventory: add 20 items → slots auto-create
- Remove items → excess slots removed

---

## Demo2: Trading System

**Location**: `Examples/Demo2 Trading/`

**Purpose**: Advanced DataBinding with economy and money validation.

### Features

- Trading: Player ↔ Merchants
- Money validation on transactions
- Block merchant-to-merchant trading
- Centralized `TradingEconomyManager`
- Buy/Sell prices in ScriptableObject

### Architecture

```
TradingEconomyManager (Singleton)
├── PlayerEconomyData (money + inventory)
└── Dictionary<MerchantData> (money + inventory per merchant)

PlayerInventoryDataBinding
├─ CanDropInternal() → Check if player has enough money
└─ OnItemAddedToUI() → Add money from selling

MerchantInventoryDataBinding
├─ CanStartDragInternal() → Check player money for purchase
├─ CanDropInternal() → Check merchant money + block merchant-to-merchant
└─ OnItemAddedToUI() → Deduct player money
```

### Key Components

**TradingEconomyManager**:
**Location**: → `Examples/Demo2 Trading/Data/TradingEconomyManager.cs`

**Purpose**: Centralized economy management.

**Contains**:
- Player money and inventory data
- Dictionary of merchant data
- Money transaction methods
- Price calculation (buy/sell)

**PlayerInventoryDataBinding**:
**Location**: → `Examples/Demo2 Trading/DataBindings/PlayerInventoryDataBinding.cs`

**Key Features**:
- `CanDropInternal()` - Validates player has enough money when buying
- `OnItemAddedToUI()` - Checks if bought from merchant, deducts money
- `OnItemRemovedFromUI()` - Adds money when selling to merchant

**MerchantInventoryDataBinding**:
**Location**: → `Examples/Demo2 Trading/DataBindings/MerchantInventoryDataBinding.cs`

**Key Features**:
- `CanStartDragInternal()` - Shows price in failure message if can't afford
- `CanDropInternal()` - Blocks merchant-to-merchant trading, checks merchant has enough money
- `OnItemAddedToUI()` - Handles buying from player

### Key Lesson

**How to override validation methods** `CanStartDragInternal` and `CanDropInternal` for custom business logic!

### Testing

Check these scenarios:
- Buy item from merchant → money deducted
- Sell item to merchant → money added
- Try to buy with insufficient funds → blocked with message
- Drag from merchant A to merchant B → blocked

---

## Demo3: Loot System (2D RPG)

**Location**: `Examples/Demo3 Loot/`

**Purpose**: Event-driven architecture with game/UI separation.

### Features

- Player movement (WASD, Top-Down 2D)
- Interaction system (`IInteractable`)
- Dynamic chest binding
- Interaction prompts
- Mediator pattern (`LootUIController`)

### Architecture

```
┌─────────────────────────────────────┐
│        GAME WORLD (No UI!)          │
├─────────────────────────────────────┤
│  Player                              │
│  ├── PlayerController                │
│  ├── PlayerInteraction  ────┐        │
│  │   Events ───────────────┐│        │
│  └── PlayerInventoryData    ││        │
│                             ││        │
│  Chest                      ││        │
│  ├── List<ItemSO> (data)    ││        │
│  └── IInteractable          ││        │
└─────────────────────────────┼┼────────┘
                              ││ Events
                              ▼▼
┌─────────────────────────────────────┐
│           UI LAYER                  │
├─────────────────────────────────────┤
│  LootUIController (MEDIATOR)        │
│  ├─ Listens to game events          │
│  ├─ Controls UI panels              │
│  ├─ Binds DataBindings dynamically  │
│  └─ Locks player input              │
└─────────────────────────────────────┘
```

### Key Components

**Chest** (Game Logic - No UI knowledge):
**Location**: → `Examples/Demo3 Loot/Scripts/Core/Chest.cs`

**How It Works**:
- Holds `List<ItemSO>` items
- Implements `IInteractable`
- Fires `OnChestOpened` event
- Does NOT know about UI!

**LootUIController** (Mediator):
**Location**: → `Examples/Demo3 Loot/Scripts/UI/LootUIController.cs`

**Purpose**: Mediates between game events and UI.

**How It Works**:
- Listens to `Chest.OnAnyChestOpened` event
- Shows/hides loot panel
- Binds `ChestInventoryDataBinding` dynamically to opened chest
- Locks player input while UI is open
- Unbinds on close

**ChestInventoryDataBinding**:
**Location**: → `Examples/Demo3 Loot/Scripts/DataBinding/ChestInventoryDataBinding.cs`

**Key Feature**: **Dynamic Binding!**

**How It Works**:
- `BindToChest(chest, items)` - Dynamically binds to chest data
- `Unbind()` - Clears binding
- `OnItemRemovedFromUI()` - Removes item from chest list
- `SyncToUI()` - Loads chest items into UI

**PlayerInventoryDataBinding**:
**Location**: → `Examples/Demo3 Loot/Scripts/DataBinding/PlayerInventoryDataBinding.cs`

**How It Works**:
- Syncs with `PlayerInventoryData` (game data)
- Persists across chest openings

### Key Lessons

- ❌ Chest does NOT know about UI
- ❌ Player does NOT know about UI
- ✅ UI knows about Chest and Player through events
- ✅ `ChestInventoryDataBinding.BindToChest(chest)` - dynamic binding!
- ✅ Mediator pattern for complex UI orchestration
- ✅ Clean separation between game logic and UI

### Testing

Check these scenarios:
- Walk to chest, press E → loot UI opens
- Loot items from chest → items removed from chest
- Close loot UI → player can move again
- Open multiple chests → binding switches correctly

---

## Summary

**Demo1**: Learn basics - drag & drop, rules, DataBinding
**Demo2**: Learn economy - money validation, trading logic
**Demo3**: Learn architecture - event-driven design, dynamic binding, UI separation

Check each demo's README.md for detailed setup instructions.

---

**[Back to SKILL.md](./SKILL.md)**
