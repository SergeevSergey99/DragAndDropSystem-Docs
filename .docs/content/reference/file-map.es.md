# Mapa de archivos

Esta página es un mapa completo del código del paquete.

Es intencionadamente más larga que otras páginas de la documentación: el objetivo aquí no es enseñar el sistema desde cero, sino ayudarte a encontrar rápidamente el tipo exacto de runtime, editor o ejemplo que necesitas.

Las tablas siguientes enumeran todos los archivos de scripts y describen las principales clases, interfaces, enum y tipos auxiliares declarados dentro de ellos.

---

## Cómo usar esta página

- Empieza por el subsistema que más se parezca a tu tarea.
- Abre el archivo indicado en la primera columna.
- Usa la columna `Types` para confirmar que estás en el lugar correcto.
- Considera los scripts de ejemplo como integraciones de referencia, no como arquitectura obligatoria.

---

## Mapa rápido

| Si buscas... | Mira |
|---|---|
| El componente principal de inventario | `UniversalInventory`, `BaseSlot`, `UniversalSlot` |
| Sincronización entre UI y datos | `InventoryDataBindingBase` y uno de los bindings list / slot-indexed / mapped |
| Lógica de transferencia | `InventoryDropProcessor`, `InventoryTransferService`, `IStrategy` |
| Reglas de aceptación | `IDragRule`, `BuiltInRules`, `RuleEvaluationService` |
| Items con forma | `PlacementInventoryDataBinding`, `IPlacementInventory`, `IPlacementShape` |
| Ejemplos de integración | páginas Demo1-Demo6 en [Ejemplos](../examples/index.md) |

---

## Puntos de entrada principales

| File | Types | Rol |
|---|---|---|
| `Scripts/DragAndDropManager.cs` | `DragAndDropManager` | Gestor global de escena para arrastres activos. Sigue el lifecycle del drag, el contexto actual y la orquestación de alto nivel. |
| `Scripts/Inventories/UniversalInventory.cs` | `UniversalInventory` | Componente principal del inventario. Posee slots, campos de strategy directos con `[SerializeReference]`, colecciones de rules y comportamiento de transferencia a nivel de inventario. |
| `Scripts/Slots/BaseSlot.cs` | `BaseSlot` | Base abstracta de slot usada por inventarios y el motor de transferencia. |
| `Scripts/Slots/UniversalSlot.cs` | `UniversalSlot` | Implementación concreta por defecto del slot usada por el paquete. |
| `Scripts/DataBinding/InventoryDataBindingBase.cs` | `InventoryDataBindingBase` | Clase base que conecta `UniversalInventory` con tu fuente de datos de juego. |

---

<details markdown="1">
<summary>Lista completa de archivos por subsistema</summary>

## Modelos y contratos básicos

| File | Types | Rol |
|---|---|---|
| `Scripts/Core/Contracts/IItemAdapter.cs` | `IItemAdapter` | Representación mínima del item almacenada en los slots y usada por la UI. |
| `Scripts/Core/Contracts/IDescribable.cs` | `IDescribable` | Interface opcional para adapters que aportan datos descriptivos ampliados para UI como tooltips. |
| `Scripts/Core/Contracts/IFilterable.cs` | `IFilterable`, `ISortable` | Interfaces opcionales para sistemas de filtrado y ordenación. |
| `Scripts/Core/Contracts/IStackSizeLimitable.cs` | `IStackSizeLimitable` | Override opcional del tamaño máximo de stack por item. |
| `Scripts/Core/Models/ItemStack.cs` | `ItemStack` | Modelo runtime del stack usado por slots, transferencia y swaps. |
| `Scripts/Core/Models/DragContext.cs` | `DragContext` | Contexto por arrastre que contiene entries de origen, info del target y flags de la operación actual. |
| `Scripts/Core/Models/ActionResult.cs` | `ActionResult` | Objeto de resultado genérico para APIs orientadas a acciones. |
| `Scripts/Core/Models/DropResult.cs` | `DropResult` | Objeto de resultado devuelto por el procesamiento de drop. |
| `Scripts/Core/Models/InventoryEvents.cs` | `InventoryItemEventContext`, `InventorySwapContext` | Payloads de eventos de transferencia y swap, incluidos los desplazamientos ordenados del multi-swap shaped. |
| `Scripts/Slots/SlotHoverEventArgs.cs` | `SlotHoverEventArgs` | Datos del evento hover para la UI de slots y sistemas relacionados. |

---

## Sistema de drop y policies

| File | Types | Rol |
|---|---|---|
| `Scripts/Core/Contracts/IDropTarget.cs` | `IDropTarget` | Contrato para cualquier cosa que pueda recibir items arrastrados. |
| `Scripts/Core/Contracts/IDropProcessor.cs` | `IDropProcessor` | Contrato para objetos que pueden procesar un intento de drop. |
| `Scripts/Core/Contracts/IDropRequestProcessor.cs` | `IDropRequestProcessor` | Interface de processor especializada usada por el drop handling basado en requests. |
| `Scripts/Core/Drop/DropAreaBase.cs` | `DropAreaBase` | Clase base para targets de drop no basados en slot, como áreas de inventario o world drop zones. |
| `Scripts/UI/InventoryDropArea.cs` | `InventoryDropArea` | Target de drop estándar para áreas de inventario construido sobre `DropAreaBase`. |
| `Scripts/Core/Drop/DropPolicy.cs` | `BlockedTargetResolutionKind`, `SwapDisplacementMode`, `PartialTransferMode`, `ResolvedDropPolicy`, `DropRequestPolicy`, `DragRequestPolicy` | Modelos de drop: rechazar, buscar otro slot, swap de una colocación o de todas las cubiertas y transferencia parcial. |
| `Scripts/Core/Drop/DropPolicySettings.cs` | `DropPolicySettings` | Ajustes para targets ocupados, colocación alternativa y transferencia parcial. |
| `Scripts/Core/Drop/DropRequestPolicySettings.cs` | `DropRequestPolicySettings` | Helper serializable para overrides temporales de drop request en acciones y triggers. |
| `Scripts/Core/Drop/DragRequestPolicySettings.cs` | `DragRequestPolicySettings` | Helper serializable para overrides temporales de cantidad al iniciar un drag. |
| `Scripts/Inventories/IDropPolicyProvider.cs` | `IDropPolicyProvider` | Interface para objetos que exponen settings de drop policy. |
| `Scripts/Inventories/InventoryAcceptanceRequest.cs` | `InventoryAcceptanceRequest` | Modelo de request usado al comprobar si un inventario puede aceptar un item/stack entrante. |
| `Scripts/Inventories/InventoryDropProcessor.cs` | `InventoryDropProcessor` | Punto de entrada desde la UI: resuelve la drop policy activa e inicia la transferencia. |

---

## Capa de Data Binding

| File | Types | Rol |
|---|---|---|
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | `ListInventoryDataBinding<TData, TAdapter>` | Base de binding para inventarios respaldados por listas. |
| `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` | `MappedSlotInventoryDataBinding<TData, TAdapter>` | Base de binding para slots semánticos o fijos con nombre, como layouts de equipamiento. |
| `Scripts/DataBinding/SlotIndexedInventoryDataBinding.cs` | `SlotIndexedInventoryDataBinding<TData, TAdapter>` | Base de binding para datos indexados por slot; lee y escribe la lista completa de items/adapters de cada slot. |
| `Scripts/DataBinding/GameManagerExample.cs` | `GameManagerExample`, `ItemData` | Ejemplo que muestra cómo un manager externo puede actuar como fuente de datos de un binding. |

---

## Pipeline de inventario

| File | Types | Rol |
|---|---|---|
| `Scripts/Inventories/IInventory.cs` | `IInventory` | Contrato base de inventario consumido por el motor de transferencia, validators y services. |
| `Scripts/Inventories/ITransferDomainHandler.cs` | `ITransferDomainHandler` | Comprobaciones de negocio síncronas antes de confirmar la transferencia y hook después del éxito. |
| `Scripts/Inventories/IAsyncTransferDomainHandler.cs` | `IAsyncTransferDomainHandler` | Comprobación asíncrona antes de transferir, por ejemplo validación de servidor. |
| `Scripts/Inventories/IItemAdapterConverter.cs` | `IItemAdapterConverter` | Convierte item adapters al cruzar límites entre inventarios con distintos modelos de item. |
| `Scripts/Inventories/IdentityItemAdapterConverter.cs` | `IdentityItemAdapterConverter` | Converter pass-through usado cuando no se necesita conversión de modelo. |
| `Scripts/Inventories/TransferKind.cs` | `TransferKind` | Enum que describe el tipo de flujo de transferencia que se está ejecutando. |
| `Scripts/Inventories/TransferDomainContext.cs` | `TransferDomainContext` | Objeto de contexto pasado a los domain handlers. |
| `Scripts/Inventories/InventoryTransferEngine.cs` | `InventoryTransferService`, `TransferEntryRequest` | Servicio de transferencia: procesa entradas una por una, soporta rollback, multi-swap atómico topology-aware y búsqueda automática. |
| `Scripts/Inventories/InventoryTransferService.cs` | `TransferProbe` | Resultado de sondeo orientativo para preview y aceptación. |
| `Scripts/Inventories/PlacementCandidate.cs` | `PlacementCandidate`, `PlacementCandidateKind` | Descripción de una posible colocación: unir stack, usar un slot existente o crear un slot dinámico. |
| `Scripts/Inventories/PlacementCandidateOrderer.cs` | placement candidate orderers | Orden usado solo para colocación automática. |
| `Scripts/Inventories/IPlacementGeometry.cs` | `IPlacementGeometry` | Contrato topology-aware para anchor, footprint, occupancy y covered slots. |
| `Scripts/Inventories/InventorySnapshot.cs` | `InventorySnapshot`, `IInventorySnapshotProvider` | Estado guardado del inventario para lectura segura, preview, sorting y rollback de operaciones fallidas. |
| `Scripts/Inventories/InventorySnapshotUtility.cs` | `InventorySnapshotUtility` | Métodos helper para construir y leer estado guardado del inventario. |
| `Scripts/Inventories/AutoTransferService.cs` | `AutoTransferService` | Servicio que realiza movimientos de estilo quick-transfer entre inventarios. |
| `Scripts/Inventories/TransferItemConversionUtility.cs` | `TransferItemConversionUtility` | Utility interna que aplica de forma consistente la conversión de adapters de origen/target en preview y execution. |
| `Scripts/Inventories/TransferConversionSession.cs` | `TransferConversionSession` | Memo de adapters convertidos durante el arrastre: preview, probe y commit comparten una instancia. |
| `Scripts/Inventories/DropVerdict.cs` | `DropVerdict` | Veredicto de la vista previa activa, leído por visuales de feedback como `CrossFeedbackSlot`. |

---

## Strategies de inventario

| File | Types | Rol |
|---|---|---|
| `Scripts/Inventories/Strategies/IStrategy.cs` | `IStrategy` | Contrato de solo lectura: validación de target explícito, enumeración de candidates automáticos y capacity. |
| `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` | `InventoryStrategyBase` | Clase base para estrategias de solo lectura que producen candidatos. |
| `Scripts/Inventories/Strategies/StackBasedInventoryStrategyBase.cs` | `StackBasedInventoryStrategyBase` | Base compartida orientada a stacks con límite de stack y soporte de override por item. |
| `Scripts/Inventories/Strategies/UniqueItemStrategy.cs` | `UniqueItemStrategy` | Strategy en la que cada slot contiene una unidad o stack independiente. |
| `Scripts/Inventories/Strategies/StackableItemStrategy.cs` | `StackableItemStrategy` | Strategy centrada en el comportamiento normal de fusión de stacks. |
| `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs` | `SeparableStacksStrategy` | Strategy para stacks que soportan split y movimientos parciales más granulares. |
| `Scripts/Inventories/Strategies/SlotManagementSettingsBase.cs` | `SlotManagementSettingsBase` | Clase base para modos de ciclo de vida de slots seleccionados directamente en `UniversalInventory`. |
| `Scripts/Inventories/Strategies/FixedSlotManagementSettings.cs` | `FixedSlotManagementSettings` | Modo fijo de ciclo de vida de slots. |
| `Scripts/Inventories/Strategies/DynamicSlotManagementSettings.cs` | `DynamicSlotManagementSettings` | Modo dinámico con mantenimiento de slots libres y hooks de trimming. |
| `Scripts/Inventories/Strategies/StrategyConfiguration.cs` | strategy configuration types | Configuración actual de strategy, slot management y reglas de merge de stacks. |

---

## Sistema de rules

| File | Types | Rol |
|---|---|---|
| `Scripts/Rules/IDragRule.cs` | `RuleResult`, `IDragRule`, `IGlobalRule`, `IInventoryRule`, `ISlotRule`, `DragRuleBase` | Contratos centrales de rules y tipo de resultado usados para validar el inicio del drag y el drop. |
| `Scripts/Rules/RuleEvaluationService.cs` | `RuleEvaluationService` | Servicio de alto nivel que ejecuta los rule validators relevantes para una drag entry. |
| `Scripts/Rules/RuleValidator.cs` | `RuleValidator<TRule>`, `GlobalRuleValidator`, `InventoryRuleValidator`, `SlotRuleValidator` | Helpers de colección y ejecución de rules para ámbitos concretos. |
| `Scripts/Rules/CompositeRule.cs` | `CompositeRule<TRule>`, `CompositeGlobalRule`, `CompositeInventoryRule`, `CompositeSlotRule`, `CompositeRuleMode` | Rules compuestas que combinan múltiples child rules usando semántica `AND` u `OR`. |
| `Scripts/Rules/BuiltInRules.cs` | `SameSlotRule`, `SameInventoryRule`, `ItemIdFilterRule`, `UniqueItemLimitRule`, `SlotLockRule`, `CustomRule` | Implementaciones integradas de rules para restricciones comunes de drag/drop. |
| `Scripts/Rules/RuleNameFilter.cs` | `RuleNameFilter`, `NameFilterType` | Rule que filtra por nombre/patrón de rule y puede reutilizarse para comportamiento selectivo allow/deny. |
| `Scripts/Rules/Presets/RulePreset.cs` | `RulePreset<TRule>` | Contenedor genérico ScriptableObject para conjuntos reutilizables de rules. |
| `Scripts/Rules/Presets/GlobalRulePreset.cs` | `GlobalRulePreset` | Asset preset para global rules. |
| `Scripts/Rules/Presets/InventoryRulePreset.cs` | `InventoryRulePreset` | Asset preset para inventory-scoped rules. |
| `Scripts/Rules/Presets/SlotRulePreset.cs` | `SlotRulePreset` | Asset preset para slot-scoped rules. |

---

## Interaction e input

| File | Types | Rol |
|---|---|---|
| `Scripts/Interaction/InteractionBindingsProfile.cs` | `InteractionBindingsProfile` | Perfil ScriptableObject que almacena input bindings para drag, click, hold, context y selection actions. |
| `Scripts/Interaction/InputEventRouter.cs` | `InputEventRouter`, `RuntimeState` | Router runtime central que resuelve bindings y traduce input en acciones de slot/inventario. |
| `Scripts/Interaction/SlotInputAdapter.cs` | `SlotInputAdapter` | Adapter orientado a UI adjunto a los visuals del slot para que participen en navigation, pointer y routed input. |
| `Scripts/Interaction/FocusSource.cs` | `FocusSource` | Enum que rastrea de dónde proviene el focus actual del slot. |
| `Scripts/Interaction/InputModalityTracker.cs` | `InputModalityTracker`, `InputModality` | Rastrea si el jugador está usando input de tipo pointer o navigation tanto en proyectos legacy como con Input System. |
| `Scripts/Interaction/InventoryExtraInteractionBinder.cs` | `InventoryExtraInteractionBinder` | Helper que vincula acciones adicionales a nivel de inventario e integration points más allá del input básico de slot. |
| `Scripts/Interaction/HoldDragSettings.cs` | `HoldDragSettings` | ScriptableObject para configurar tiempos y umbrales de hold-to-drag. |
| `Scripts/Interaction/HoldDragPreviewDisplay.cs` | `HoldDragPreviewDisplay` | Componente de feedback visual para el estado de preparación de hold-drag. |
| `Scripts/Interaction/HoldDragActions.cs` | `StartHoldCountAction`, `StartHoldDragAction` | Slot interaction actions relacionadas con hold counting y el inicio de drag por pulsación mantenida. |
| `Scripts/Interaction/SlotInteractionActions.cs` | `SlotInteractionAction`, `AssetSafeSlotInteractionAction`, `StartDragAction`, `CompleteDragAction`, `SplitDropAction`, `RotateDragAction`, `CancelDragAction`, `InventorySlotAction` | Tipos de acción centrales invocados por el input router para los flujos de interacción de slot e inventario. |

---

## Modelos de input binding

| File | Types | Rol |
|---|---|---|
| `Scripts/Interaction/Bindings/PointerBinding.cs` | `PointerBinding` | Definición runtime de pointer binding. |
| `Scripts/Interaction/Bindings/KeyBinding.cs` | `KeyBinding` | Definición runtime de binding para teclas legacy. |
| `Scripts/Interaction/Bindings/LegacyInputActionBinding.cs` | `LegacyInputActionBinding` | Definición runtime de binding del Input Manager antiguo por nombre de botón. |
| `Scripts/Interaction/Bindings/InputActionBinding.cs` | `InputActionBinding` | Definición runtime de binding del Input System. |
| `Scripts/Interaction/Bindings/AssetPointerBinding.cs` | `AssetPointerBinding` | Entrada serializada de asset/profile para pointer binding. |
| `Scripts/Interaction/Bindings/AssetKeyBinding.cs` | `AssetKeyBinding` | Entrada serializada de asset/profile para key binding legacy. |
| `Scripts/Interaction/Bindings/AssetLegacyInputActionBinding.cs` | `AssetLegacyInputActionBinding` | Entrada serializada de asset/profile para binding del Input Manager antiguo por nombre de botón. |
| `Scripts/Interaction/Bindings/AssetInputActionBinding.cs` | `AssetInputActionBinding` | Entrada serializada de asset/profile para Input System binding. |
| `Scripts/Interaction/Bindings/ModifierKeyHelper.cs` | `ModifierKeyHelper` | Helper para comprobaciones de modifier keys usadas durante la evaluación de bindings. |
| `Scripts/Interaction/Bindings/KeyCodeInput.cs` | `KeyCodeInput` | Lee un `KeyCode` a través del backend de input activo. Si en Player Settings está seleccionado el Input System, la clase legacy `Input` lanza excepción, así que el `KeyCode` se traduce a un control del Input System. Permite que los mismos key bindings funcionen en ambos backends. |

---

## Actions

| File | Types | Rol |
|---|---|---|
| `Scripts/Actions/InventoryActionBase.cs` | `InventoryActionBase` | Clase base para acciones de inventario de alcance general que pueden dispararse mediante bindings. |
| `Scripts/Actions/Enums.cs` | `PointerTriggerPhase`, `TriggerPhaseEnum`, `ModifierKey`, `KeyTriggerPhase` | Enum compartidos para configuración de bindings/actions y fases de activación. |
| `Scripts/Actions/Inheritors/AutoTransferAction.cs` | `AutoTransferAction` | Acción de inventario que invoca comportamiento de quick transfer. |
| `Scripts/Actions/Inheritors/SortInventoryAction.cs` | `SortInventoryAction` | Acción de inventario que ordena items físicamente mediante un `ISlotSorter`. |

---

## Sistema de selección

| File | Types | Rol |
|---|---|---|
| `Scripts/Selection/SelectionContext.cs` | `SelectionContext` | Contenedor runtime del estado actual de selección y datos auxiliares. |
| `Scripts/Selection/SelectionManager.cs` | `SelectionManager` | Coordinador global de selección usado por los flujos de multi-select y multi-drag. |
| `Scripts/Selection/SlotSelectionView.cs` | `SlotSelectionView` | Componente visual que renderiza el estado de selección sobre un slot. |
| `Scripts/Selection/SelectionSlotAction.cs` | `SelectionSlotAction` | Acción de slot que integra el comportamiento de selección en el input pipeline. |
| `Scripts/Selection/StartMultiDragAction.cs` | `StartMultiDragAction` | Acción de slot que inicia un drag desde la selección actual. |
| `Scripts/Selection/Operations/SelectionOperationBase.cs` | `SelectionOperationBase` | Clase base para comandos de selección reutilizables. |
| `Scripts/Selection/Operations/ClearSelectionOperation.cs` | `ClearSelectionOperation` | Limpia la selección actual. |
| `Scripts/Selection/Operations/ClearAndSelectOperation.cs` | `ClearAndSelectOperation` | Limpia la selección previa y selecciona un nuevo slot/conjunto. |
| `Scripts/Selection/Operations/SelectSlotOperation.cs` | `SelectSlotOperation` | Selecciona un slot concreto. |
| `Scripts/Selection/Operations/ToggleFilledSlotOperation.cs` | `ToggleFilledSlotOperation` | Alterna el estado de selección de un slot no vacío. |
| `Scripts/Selection/Operations/RangeSelectOperation.cs` | `RangeSelectOperation` | Selecciona un rango de slots. |
| `Scripts/Selection/Operations/SelectAllOperation.cs` | `SelectAllOperation` | Selecciona todos los slots disponibles/elegibles. |
| `Scripts/Selection/Operations/SelectByConditionOperation.cs` | `SelectByConditionOperation` | Clase base para selección en bloque basada en condiciones. |
| `Scripts/Selection/Triggers/SelectionTriggerBase.cs` | `SelectionTriggerBase` | Clase base para componentes trigger que invocan operaciones de selección. |
| `Scripts/Selection/Triggers/ButtonSelectionTrigger.cs` | `ButtonSelectionTrigger` | Trigger mediante botón UI para una operación de selección. |
| `Scripts/Selection/Triggers/InputActionSelectionTrigger.cs` | `InputActionSelectionTrigger`, `TriggerPhase` | Trigger del Input System para operaciones de selección. |

---

## Sistema de tooltip

| File | Types | Rol |
|---|---|---|
| `Scripts/UI/Tooltip/BaseTooltipView.cs` | `BaseTooltipView` | Clase base para implementaciones de renderizado de tooltip. |
| `Scripts/UI/Tooltip/DefaultTooltipView.cs` | `DefaultTooltipView` | Implementación lista para usar de la vista de tooltip. |
| `Scripts/UI/Tooltip/TooltipManager.cs` | `TooltipManager`, `TooltipAnchor` | Controlador global de tooltip que posiciona, abre y oculta vistas de tooltip. |

---

## Drag visuals y layout

| File | Types | Rol |
|---|---|---|
| `Scripts/UI/IDragVisual.cs` | `IDragVisual` | Interface para implementaciones de drag visual. |
| `Scripts/UI/DefaultDragVisual.cs` | `DefaultDragVisual` | Presentador básico de drag visual. |
| `Scripts/UI/FancyDragVisual.cs` | `FancyDragVisual` | Drag visual más estilizado con presentación/comportamiento de animación más rico. |
| `Scripts/UI/DragVisualPresenter.cs` | `DragVisualPresenter`, `VisualInstance` | Presentador global que crea y actualiza el drag visual activo. |
| `Scripts/UI/InventoryDragVisualBinder.cs` | `InventoryDragVisualBinder` | Vincula un inventario o setup de escena con una configuración elegida de drag visual. |
| `Scripts/UI/FreeFormSlotLayout.cs` | `FreeFormSlotLayout` | Helper de layout para inventarios cuyos slots se colocan manualmente o de forma semimanual en lugar de en una cuadrícula regular. |

---

## Animación de auto transfer

| File | Types | Rol |
|---|---|---|
| `Scripts/Core/AutoTransfer/AutoTransferAnimationStrategy.cs` | `AutoTransferAnimationStrategy` | Clase base para strategies de animación de quick-transfer. |
| `Scripts/Core/AutoTransfer/TweenAutoTransferAnimation.cs` | `TweenAutoTransferAnimation` | Strategy de animación basada en tween para efectos de auto-transfer. |
| `Scripts/Core/AutoTransfer/AutoTransferContext.cs` | `InventoryList` | Modelo auxiliar relacionado con auto-transfer usado por los flujos de animación/selección de quick-transfer. |

---

## Sistema de context menu

| File | Types | Rol |
|---|---|---|
| `Scripts/ContextMenu/IContextMenuEntry.cs` | `IContextMenuEntry` | Contrato para cualquier cosa que pueda aparecer como entrada de context menu. |
| `Scripts/ContextMenu/ContextMenuContext.cs` | context menu context types | Objeto payload que describe el slot/inventario/item para el que se está construyendo un menú. |
| `Scripts/ContextMenu/ContextMenuEntryDefinitionSO.cs` | `ContextMenuEntryDefinitionSO` | Base ScriptableObject para definiciones reutilizables de entradas de menú. |
| `Scripts/ContextMenu/ContextMenuSceneEntryBase.cs` | `ContextMenuSceneEntryBase` | Clase base de scene object para entradas de contexto que necesitan referencias runtime de la escena. |
| `Scripts/ContextMenu/ContextMenuPreset.cs` | `ContextMenuPreset` | Asset que agrupa varias entradas de menú en un preset reutilizable. |
| `Scripts/ContextMenu/ContextMenuManager.cs` | `ContextMenuManager` | Gestor global que construye y abre context menus. |
| `Scripts/ContextMenu/ContextMenuBinder.cs` | `ContextMenuBinder` | Vincula presets/entradas de menú a un inventario o a un objeto de escena. |
| `Scripts/ContextMenu/InventoryContextMenuViewBinder.cs` | `InventoryContextMenuViewBinder` | Conecta inventarios con una implementación concreta de context menu view. |
| `Scripts/ContextMenu/ShowContextMenuAction.cs` | `ShowContextMenuAction` | Acción de slot que solicita un menú para el slot/contexto actual. |
| `Scripts/ContextMenu/UI/ContextMenuViewBase.cs` | `ContextMenuViewBase` | Clase base para implementaciones de UI de context menu. |
| `Scripts/ContextMenu/UI/UniversalContextMenuView.cs` | `UniversalContextMenuView` | Implementación por defecto de UI de context menu incluida en el paquete. |
| `Scripts/ContextMenu/UI/ContextMenuEntryView.cs` | `ContextMenuEntryView` | Elemento de UI para una fila/botón de entrada de menú. |
| `Scripts/ContextMenu/BuiltInEntries/SortContextMenuEntrySO.cs` | `SortContextMenuEntrySO` | Entrada de menú integrada que dispara la ordenación del inventario. |
| `Scripts/ContextMenu/BuiltInEntries/DebugContextMenuEntrySO.cs` | `DebugContextMenuEntrySO` | Entrada de menú integrada para diagnóstico/debug. |

---

## UI de filtrado y ordenación

| File | Types | Rol |
|---|---|---|
| `Scripts/Filter/ISlotFilter.cs` | `ISlotFilter`, `ISlotSorter`, `FilterPredicate`, `SortComparison` | Interfaces y delegados base para filtrado y ordenación. |
| `Scripts/Filter/FilterContext.cs` | `FilterContext` | Struct de contexto para filtros y sorters (Slot, Inventory, AllSlots, SlotIndex). |
| `Scripts/Filter/FilterDisplayMode.cs` | `FilterDisplayMode` | Enum: Hide, Dim, MoveToEnd. |
| `Scripts/Filter/FilterSortController.cs` | `FilterSortController` | Orquestador: aplica `ISlotFilter` / `ISlotSorter` a una UI de inventario. |
| `Scripts/Filter/FilterSortPreset.cs` | `FilterSortPreset` | ScriptableObject que combina filtro + sorter + modo de visualización. |
| `Scripts/Filter/FilterButton.cs` | `FilterButton` | Botón de UI que aplica un `SlotFilterSO`. |
| `Scripts/Filter/SortButton.cs` | `SortButton` | Botón de UI que aplica un `SlotSorterSO`. |
| `Scripts/Filter/FilterSortButton.cs` | `FilterSortButton` | Botón de UI que aplica un `FilterSortPreset` combinado. |
| `Scripts/Filter/SlotFilterSO.cs` | `SlotFilterSO` | SO wrapper para assets compartibles de `ISlotFilter`. |
| `Scripts/Filter/SlotSorterSO.cs` | `SlotSorterSO` | SO wrapper para assets compartibles de `ISlotSorter`. |
| `Scripts/Filter/Filters/*.cs` | `CategoryFilter`, `RarityRangeFilter`, `NameSearchFilter`, `CompositeFilter` | Implementaciones `[Serializable]` integradas de filtros. |
| `Scripts/Filter/Sorters/*.cs` | `NameSorter`, `CategorySorter`, `RaritySorter`, `SortValueSorter`, `StackCountSorter`, `CompositeSorter` | Implementaciones `[Serializable]` integradas de sorters. |

---

## Utilidades

| File | Types | Rol |
|---|---|---|
| `Scripts/Tools/MonoSingleton.cs` | `MonoSingleton<T>` | Base singleton genérica para MonoBehaviour usada por gestores globales de escena. |
| `Scripts/Tools/Extensions.cs` | `Extensions` | Métodos de extensión compartidos usados por el código runtime. |
| `Scripts/Tools/MiniTweenRunner.cs` | `MiniTweenEase`, `MiniTweenRunner`, `ActiveTween` | Tween runner ligero usado para animaciones runtime simples. |

---

## Inspector attributes y tooling de editor

| File | Types | Rol |
|---|---|---|
| `Scripts/Tools/Inspector/InspectorAttributes.cs` | `InfoMessageType`, `TitleAlignments`, `FoldoutGroupAttribute`, `InfoBoxAttribute`, `RequiredAttribute`, `ShowIfAttribute`, `HideLabelAttribute`, `ButtonAttribute`, `DisableInEditorModeAttribute`, `EnumToggleButtonsAttribute`, `LabelTextAttribute`, `ReadOnlyAttribute`, `ShowInInspectorAttribute`, `ListDrawerSettingsAttribute`, `TitleAttribute`, `InlinePropertyAttribute`, `PreviewFieldAttribute`, `TitleGroupAttribute`, `OnValueChangedAttribute`, `ManagedReferencePickerAttribute`, `RulePresetPickerAttribute`, `FixedArraySizeAttribute` | Custom inspector attributes usados en todo el paquete para mejorar la UX de authoring. |
| `Scripts/Tools/Inspector/Editor/InspectorDrawers.cs` | `InspectorReflectionUtility`, `FoldoutGroupStateCache`, `InspectorPreviewUtility`, `FoldoutGroupStyles`, `GroupedInspectorEditorBase`, `GroupedMonoBehaviourEditor`, `GroupedScriptableObjectEditor`, `ShowIfPropertyDrawer`, `RequiredPropertyDrawer`, `EnumToggleButtonsPropertyDrawer`, `ReadOnlyPropertyDrawer`, `HideLabelPropertyDrawer`, `LabelTextPropertyDrawer`, `InfoBoxDecoratorDrawer`, `TitleDecoratorDrawer`, `TitleGroupDecoratorDrawer`, `PreviewFieldPropertyDrawer`, `ManagedReferencePickerPropertyDrawer`, `RulePresetPickerPropertyDrawer`, `FixedArraySizePropertyDrawer` | Implementación solo-editor del sistema de custom inspector y property drawers. |
| `Scripts/Tools/Inspector/Editor/InspectorOdinBridge.cs` | `DragAndDropOdinAttributeProcessor` | Puente con Odin para que los inspector attributes del paquete convivan correctamente con Odin Inspector. |

---

## Archivos Runtime/UI adicionales

| File | Types | Rol |
|---|---|---|
| `Scripts/Core/Models/InventoryTopology.cs` | `IInventoryTopology`, `SlotTopology`, `RectGridTopology`, `SlotCountLimitedTopology`, `OrientationStepUtility` | Abstracción de topology para coordenadas slot/grid, orientation y proyección de footprints. |
| `Scripts/Core/Models/Placement.cs` | `GridTopology`, `PlacementRequest`, `Placement` | Modelos base de placement request, tamaño de grid y stack colocado. |
| `Scripts/Core/Models/PlacementCellUtility.cs` | `PlacementBoundsMode`, `PlacementCellUtility` | Utility para calcular celdas cubiertas con distintos modos de bounds. |
| `Scripts/Core/Models/PlacementShape.cs` | `IPlacementShape`, `IItemPlacementShapeProvider`, `RectPlacementShape`, `OffsetPlacementShape`, `ComplexPlacementShape`, `PlacementShapeUtility` | Modelos de footprint de items y helpers de shape/orientation. |
| `Scripts/Core/Models/PlacementSnapshot.cs` | `PlacementSnapshot` | Estado guardado de un placement para eventos, rollback y contexto de DataBinding. |
| `Scripts/Core/UDNDEvents.cs` | `UDNDEvents` | Eventos globales del lifecycle drag/drop/swap. |
| `Scripts/DataBinding/PlacementInventoryDataBinding.cs` | `PlacementData<TData>`, `PlacementCommitContext<TData,TAdapter>`, `PlacementInventoryDataBinding<TData,TAdapter>` | Binding template para inventarios que persisten anchor/orientation y cada instancia de item dentro de una colocación. |
| `Scripts/Interaction/RuntimeInteractionSnapshot.cs` | `InteractionInputKind`, `RuntimeInteractionSnapshot` | Contexto input actual guardado de un slot o inventario para actions. |
| `Scripts/Inventories/BaseInventory.cs` | `BaseInventory` | Base MonoBehaviour abstracta para implementaciones de inventario. |
| `Scripts/Inventories/DropPreviewController.cs` | `DropPreviewController` | Gestiona el resaltado preview de celdas cubiertas durante hover/drag. |
| `Scripts/Inventories/EntryTransferResult.cs` | `PlacementTransferOutcomeKind`, `EntryTransferStatus`, `PlacementTransferOutcome`, `EntryTransferResult`, `TransferExecutionReport` | Modelos de resultado/reporte de transfer execution. |
| `Scripts/Inventories/IInventoryInteraction.cs` | `IInventoryInteraction` | Contrato para interaction state del inventario y resolución de auto-transfer slot. |
| `Scripts/Inventories/IInventorySlotCreationCapacity.cs` | `IInventorySlotCreationCapacity` | Contrato interno de capacidad para creación dinámica de slots. |
| `Scripts/Inventories/IPlacementInventory.cs` | `IPlacementInventory`, `IShapedDragTargetResolver` | Contrato placement-aware de inventario y resolución target-side de anchors para shaped drag. |
| `Scripts/Inventories/InventoryPlacementGeometry.cs` | `InventoryPlacementGeometry` | Adapter de operaciones geometry sobre `IPlacementInventory` y topology. |
| `Scripts/Inventories/InventoryRuntimeCapabilities.cs` | `IInventoryRuleEvaluator`, `IOccupiedSlotDropHandler`, `IPreRuleOccupiedSlotDropHandler`, `IPostRuleOccupiedSlotDropHandler`, `IDynamicSlotLifecycle`, `IInventoryEventSink` | Interfaces de capacidades runtime usadas por el motor sin depender directamente de `UniversalInventory`; los occupied-slot handlers se implementan en DataBinding. |
| `Scripts/Inventories/PlacementCandidateSource.cs` | `PlacementCandidateSource` | Fuente enumerable lazy para placement candidates. |
| `Scripts/Inventories/PlacementSnapshotCodec.cs` | `PlacementSnapshotCodec` | Codec interno para capturar/restaurar placement state. |
| `Scripts/Inventories/PlacementStore.cs` | `PlacementStore` | Almacenamiento de placements y occupancy map para inventarios shaped/grid. |
| `Scripts/Inventories/ShapedPlacementAnchorStrategy.cs` | `ShapedPlacementAnchorContext`, `IShapedPlacementAnchorStrategy`, `RotatedGrabOffsetAnchorStrategy`, `SourceGrabOffsetAnchorStrategy`, `TargetSlotAnchorStrategy` | Estrategias que convierten un hovered slot en anchor para shaped items. |
| `Scripts/Slots/ISlot.cs` | `ISlot` | Contrato mínimo de slot. |
| `Scripts/Slots/ShapedColorSlot.cs` | `ShapedColorSlot` | Slot visual para preview/highlight de shaped placement. |
| `Scripts/UI/PlacementOverlay.cs` | `PlacementOverlay` | Renderer overlay para visuals de placement multi-celda. |
| `Scripts/UI/PlacementOverlayItem.cs` | `PlacementOverlayRenderState`, `PlacementOverlayItem` | UI item dentro del placement overlay. |
| `Scripts/UI/SourceSizedDragVisual.cs` | `SourceSizedDragVisual` | Drag visual que conserva el tamaño source/placement. |
| `Scripts/UI/Tooltip/FadeTooltipView.cs` | `FadeTooltipView` | Tooltip view base con animación fade. |
| `Scripts/Filter/DelegateFilter.cs` | `DelegateFilter`, `DelegateSorter` | Wrappers internos filter/sorter basados en delegates. |
| `Scripts/Filter/Filters/CategoryFilter.cs` | `CategoryFilter` | Filtro integrado por categoría. |
| `Scripts/Filter/Filters/CompositeFilter.cs` | `CompositeFilter` | Filtro compuesto integrado. |
| `Scripts/Filter/Filters/NameSearchFilter.cs` | `NameSearchFilter` | Filtro integrado de búsqueda por nombre. |
| `Scripts/Filter/Filters/RarityRangeFilter.cs` | `RarityRangeFilter` | Filtro integrado por rango de rareza. |
| `Scripts/Filter/Sorters/CategorySorter.cs` | `CategorySorter` | Sorter integrado por categoría. |
| `Scripts/Filter/Sorters/CompositeSorter.cs` | `CompositeSorter` | Sorter compuesto integrado. |
| `Scripts/Filter/Sorters/NameSorter.cs` | `NameSorter` | Sorter integrado por nombre. |
| `Scripts/Filter/Sorters/RaritySorter.cs` | `RaritySorter` | Sorter integrado por rareza. |
| `Scripts/Filter/Sorters/SortValueSorter.cs` | `SortValueSorter` | Sorter integrado por sort value. |
| `Scripts/Filter/Sorters/StackCountSorter.cs` | `StackCountSorter` | Sorter integrado por cantidad en stack. |

---

## Ejemplo: Demo1 Inventories

| File | Types | Rol |
|---|---|---|
| `Examples/Demo1 Inventories/ItemExampleSO.cs` | `ItemExampleSO` | Datos simples de item mediante ScriptableObject usados por la demo introductoria del inventario. |
| `Examples/Demo1 Inventories/Adapters/ItemAdapterSoAdapter.cs` | `ItemAdapterSoAdapter` | Adapter que expone `ItemExampleSO` al sistema de inventario. |
| `Examples/Demo1 Inventories/DataBindings/ItemsSOInventoryDataBinding.cs` | `ItemsSOInventoryDataBinding` | Binding basado en listas que conecta las listas de items de la demo con la UI del inventario. |
| `Examples/Demo1 Inventories/DataAmountInBinding.cs` | `DataAmountInBinding` | Helper UI que muestra la cantidad de elementos en el demo binding. |
| `Examples/Demo1 Inventories/ItemTypeExampleFilterRule.cs` | `ItemTypeExampleFilterRule` | Rule específica de la demo que muestra cómo restringir drops por categoría/tipo de item. |

---

## Ejemplo: Demo2 Loot

| File | Types | Rol |
|---|---|---|
| `Examples/Demo2 Loot/Scripts/Core/IInteractable.cs` | `IInteractable` | Contrato simple de interacción para objetos del mundo en la loot demo. |
| `Examples/Demo2 Loot/Scripts/Core/Chest.cs` | `Chest` | Cofre interactivo que posee datos de loot y comportamiento de apertura de UI. |
| `Examples/Demo2 Loot/Scripts/Core/ItemController.cs` | `ItemController` | Componente de interacción para items/cofres del lado del mundo. |
| `Examples/Demo2 Loot/Scripts/ItemExampleWith3DSO.cs` | `ItemExampleWith3DSO` | Datos de item en ScriptableObject para el ejemplo de loot/mundo. |
| `Examples/Demo2 Loot/Scripts/ItemAdapterSoWith3DAdapter.cs` | `ItemAdapterSoWith3DAdapter` | Adapter para los items de la loot demo, incluyendo datos de filtro. |
| `Examples/Demo2 Loot/Scripts/DataBinding/ChestInventoryDataBinding.cs` | `ChestInventoryDataBinding` | Binding del contenido del inventario del cofre. |
| `Examples/Demo2 Loot/Scripts/DataBinding/PlayerInventoryDataBinding.cs` | `PlayerInventoryDataBinding` | Binding del inventario del jugador en la loot demo. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerController.cs` | `PlayerController` | Controlador/movimiento simple del jugador para la escena. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerInteraction.cs` | `PlayerInteraction` | Realiza raycasts/comprobaciones de interacción y abre/usa objetos del mundo. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerInventoryData.cs` | `PlayerInventoryData` | Contenedor de datos del lado del jugador para el inventario de la demo. |
| `Examples/Demo2 Loot/Scripts/UI/LootUIController.cs` | `LootUIController` | Abre/cierra y refresca la loot UI. |
| `Examples/Demo2 Loot/Scripts/World3D/WorldItem.cs` | `WorldItem` | Representación 3D de pickup en el mundo. |
| `Examples/Demo2 Loot/Scripts/World3D/WorldDropZone.cs` | `WorldDropZone` | Target de drop en world-space para soltar items fuera de la UI y devolverlos a la escena. |

---

## Ejemplo: Demo3 Craft

| File | Types | Rol |
|---|---|---|
| `Examples/Demo3 Craft/Data/MinecraftItemSO.cs` | `CraftItemSO` | Definición de item en ScriptableObject usada por la crafting demo. |
| `Examples/Demo3 Craft/Data/RuntimeItem.cs` | `RuntimeItem` | Wrapper/model runtime del item usado por la demo donde hace falta. |
| `Examples/Demo3 Craft/Adapters/MinecraftItemAdapterAdapter.cs` | `CraftItemAdapterAdapter` | Adapter que expone los items de la crafting demo a la UI del inventario. |
| `Examples/Demo3 Craft/Crafting/CraftingRecipePattern.cs` | `CraftingRecipePattern` | Definición serializada del patrón/grid de receta. |
| `Examples/Demo3 Craft/Crafting/CraftingRecipeSO.cs` | `CraftingRecipeSO` | Asset ScriptableObject de receta. |
| `Examples/Demo3 Craft/Crafting/CraftingManager.cs` | `CraftingManager` | Controlador de dominio de la demo que evalúa recetas y mantiene el estado del resultado de craft. |
| `Examples/Demo3 Craft/DataBindings/MainInventoryDataBinding.cs` | `MainInventoryDataBinding` | Binding del inventario principal del jugador. |
| `Examples/Demo3 Craft/DataBindings/HotbarDataBinding.cs` | `HotbarDataBinding` | Binding del hotbar. |
| `Examples/Demo3 Craft/DataBindings/CraftTableDataBinding.cs` | `CraftTableDataBinding` | Binding de los slots de entrada de la crafting grid. |
| `Examples/Demo3 Craft/DataBindings/CraftResultDataBinding.cs` | `CraftResultDataBinding` | Binding orientado a lectura para el slot de salida del craft. |
| `Examples/Demo3 Craft/Editor/CraftingRecipePatternPropertyDrawer.cs` | `CraftingRecipePatternPropertyDrawer` | Custom editor drawer para authoring de recipe patterns. |

---

## Ejemplo: Demo4 Trading

| File | Types | Rol |
|---|---|---|
| `Examples/Demo4 Trading/Data/ItemType.cs` | `ItemType` | Enum de la demo que describe categorías de item para trading/equipment. |
| `Examples/Demo4 Trading/TradableItemSO.cs` | `TradableItemSO` | Definición ScriptableObject del item del comerciante. |
| `Examples/Demo4 Trading/Data/TradableItemModel.cs` | `TradableItemModel` | Modelo runtime del item comerciable usado del lado del jugador. |
| `Examples/Demo4 Trading/Data/PlayerData.cs` | `PlayerData` | Modelo de datos de dinero/inventario del jugador. |
| `Examples/Demo4 Trading/Data/MerchantData.cs` | `MerchantData` | Modelo de datos de inventario/precios del comerciante. |
| `Examples/Demo4 Trading/Data/TradingEconomyManager.cs` | `TradingEconomyManager`, `Merchant` | Gestor de la demo que almacena el estado de la economía y los datos de comercio de jugador/comerciante. |
| `Examples/Demo4 Trading/Adapters/ITradableItem.cs` | `ITradableItem` | Interface compartida para cualquier cosa que tenga metadatos específicos de comercio. |
| `Examples/Demo4 Trading/Adapters/TradableSoAdapter.cs` | `TradableSoAdapter` | Adapter para items ScriptableObject del lado del comerciante. |
| `Examples/Demo4 Trading/Adapters/TradableItemAdapterModelAdapter.cs` | `TradableItemAdapterModelAdapter` | Adapter para runtime tradable item models del jugador. |
| `Examples/Demo4 Trading/Converters/MerchantItemAdapterConverter.cs` | `MerchantItemAdapterConverter` | Convierte items entrantes al modelo de datos/adapter del comerciante. |
| `Examples/Demo4 Trading/Converters/ModelItemAdapterConverter.cs` | `ModelItemAdapterConverter` | Convierte items entrantes al modelo de datos/adapter runtime del jugador. |
| `Examples/Demo4 Trading/DataBindings/IMerchantInventory.cs` | `IMerchantInventory` | Marker interface para bindings/componentes del inventario del comerciante. |
| `Examples/Demo4 Trading/DataBindings/TradingHelper.cs` | `TradingHelper` | Métodos utility de comercio para comprobaciones de precio, asequibilidad y side effects. |
| `Examples/Demo4 Trading/DataBindings/PlayerInventoryDataBinding.cs` | `PlayerInventoryDataBinding` | Binding del inventario del jugador con transfer-domain hooks para reglas de comercio. |
| `Examples/Demo4 Trading/DataBindings/MerchantInventoryDataBinding.cs` | `MerchantInventoryDataBinding` | Binding del inventario del comerciante con conversión y comportamiento específico de trading. |
| `Examples/Demo4 Trading/DataBindings/EquipmentInventoryDataBinding.cs` | `EquipmentInventoryDataBinding` | Binding de slots de equipamiento para la escena de trading/equipment. |
| `Examples/Demo4 Trading/UI/PlayerGoldView.cs` | `PlayerGoldView` | Vista UI que muestra el oro actual del jugador. |
| `Examples/Demo4 Trading/UI/SelectedPurchasePriceView.cs` | `SelectedPurchasePriceView` | Vista UI que muestra el precio del item actualmente seleccionado. |

---

## Ejemplo: Demo5 Containers

| File | Types | Rol |
|---|---|---|
| `Examples/Demo5 Containers/Scripts/Data/BaseItemSO.cs` | `BaseItemSO` | Definición base ScriptableObject de item para la container demo. |
| `Examples/Demo5 Containers/Scripts/Data/ContainerItemSO.cs` | `ContainerItemSO` | Definición de item para items que contienen otro inventario. |
| `Examples/Demo5 Containers/Scripts/Data/IContainerizeItemInstance.cs` | `IContainerizeItemInstance` | Interface para runtime item instances que pueden exponer comportamiento de contenedor. |
| `Examples/Demo5 Containers/Scripts/Data/ItemInstance.cs` | `ItemInstance` | Implementación normal de runtime item instance. |
| `Examples/Demo5 Containers/Scripts/Data/ContainerItemInstance.cs` | `ContainerItemInstance` | Runtime item instance que posee datos de inventario anidado. |
| `Examples/Demo5 Containers/Scripts/Adapters/ContainerItemAdapterAdapter.cs` | `ContainerItemAdapterAdapter` | Adapter que expone a la UI los runtime items de la container demo. |
| `Examples/Demo5 Containers/Scripts/Bindings/PlayerContainerInventoryDataBinding.cs` | `PlayerContainerInventoryDataBinding` | Binding del inventario principal del jugador en la container demo. |
| `Examples/Demo5 Containers/Scripts/Bindings/ContainerInventoryDataBinding.cs` | `ContainerInventoryDataBinding` | Binding del inventario anidado del contenedor actualmente abierto. |
| `Examples/Demo5 Containers/Scripts/ContainerDemoManager.cs` | `ContainerDemoManager` | Scene manager que mantiene el estado del contenedor activo y coordina la demo. |
| `Examples/Demo5 Containers/Scripts/UI/ContainerUIController.cs` | `ContainerUIController` | Controla la apertura, cambio y presentación de la UI de contenedores anidados. |
| `Examples/Demo5 Containers/Scripts/ContainerViewRegistry.cs` | `ContainerViewRegistry` | Registro de las container views presentes en pantalla. Permite que un drop en un slot ocupado actualice de forma incremental un inventario anidado ya abierto, en lugar de recargarlo entero. |
| `Examples/Demo5 Containers/Scripts/ContextMenu/OpenContainerMenuEntrySO.cs` | `OpenContainerMenuEntrySO` | Entrada de context menu que abre un item contenedor. |
| `Examples/Demo5 Containers/Scripts/Events.cs` | `Events` | Nombres/helpers de eventos compartidos usados dentro de la container demo. |

---

## Ejemplo: Demo6 Shaped Items

| File | Types | Rol |
|---|---|---|
| `Examples/Demo6 Shaped Items/ShapedItemExampleSO.cs` | `ShapedItemExampleSO` | Definición ScriptableObject base para shaped items rectangulares mediante width/height. |
| `Examples/Demo6 Shaped Items/ComplexShapedItemExampleSO.cs` | `ComplexShapedItemExampleSO` | Definición ScriptableObject para un footprint no rectangular mediante una máscara bool de celdas ocupadas. |
| `Examples/Demo6 Shaped Items/Adapters/ShapedItemAdapter.cs` | `ShapedItemAdapter` | Adapter que implementa `IItemPlacementShapeProvider` y expone un `ComplexPlacementShape`. |
| `Examples/Demo6 Shaped Items/DataBindings/ShapedItemsInventoryDataBinding.cs` | `ShapedPlacementSeed`, `ShapedItemsInventoryDataBinding` | Binding placement-aware que guarda item, anchor index y orientation. |
| `Examples/Demo6 Shaped Items/Editor/ComplexShapedItemExampleSOEditor.cs` | `ComplexShapedItemExampleSOEditor` | Inspector personalizado con grid clickable para editar un footprint complejo sobre el icon sprite. |

---

</details>

---

## Qué abrir primero para tareas comunes

| Si necesitas... | Abre primero estos archivos |
|---|---|
| Vincular tus propios datos de inventario basados en lista | `InventoryDataBindingBase.cs`, `ListInventoryDataBinding.cs`, un demo binding de Demo1 o Demo4 |
| Construir slots fijos de equipamiento | `MappedSlotInventoryDataBinding.cs`, `EquipmentInventoryDataBinding.cs` |
| Entender la transferencia drag/drop | `InventoryDropProcessor.cs`, `InventoryTransferEngine.cs`, `IStrategy.cs`, `PlacementCandidate.cs` |
| Añadir drag rules personalizadas | `IDragRule.cs`, `BuiltInRules.cs`, `CompositeRule.cs` |
| Dar soporte a quick transfer | `AutoTransferService.cs`, `AutoTransferAction.cs`, `AutoTransferAnimationStrategy.cs` |
| Añadir input bindings personalizados | `InteractionBindingsProfile.cs`, `InputEventRouter.cs`, `SlotInteractionActions.cs` |
| Añadir acciones de context menu | `IContextMenuEntry.cs`, `ContextMenuEntryDefinitionSO.cs`, `ContextMenuManager.cs` |
| Añadir contenido de tooltip | `IDescribable.cs`, `TooltipManager.cs`, `DefaultTooltipView.cs` |
| Entender la integración de world drop | `DropAreaBase.cs`, `InventoryDropArea.cs`, `WorldDropZone.cs` |
| Implementar conversión de items entre inventarios | `IItemAdapterConverter.cs`, `TransferItemConversionUtility.cs`, converters de Demo4 |
| Crear items multi-celda en un grid inventory | `PlacementInventoryDataBinding.cs`, `IPlacementInventory.cs`, `ShapedItemAdapter.cs`, Demo6 Shaped Items |
