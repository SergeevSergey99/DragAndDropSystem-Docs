# Mapa de archivos

Esta pagina es una guia rapida para encontrar el tipo correcto dentro del codigo.

## Puntos de entrada principales

| Archivo | Uso |
|---|---|
| `Scripts/Inventories/UniversalInventory.cs` | componente principal del inventario |
| `Scripts/DataBinding/InventoryDataBindingBase.cs` | base del binding |
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | inventarios basados en listas |
| `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` | slots fijos o semanticos |
| `Scripts/DataBinding/SlotIndexedInventoryDataBinding.cs` | inventarios indexados por slot |
| `Scripts/Inventories/TransferPlanner.cs` | planning del drag/drop |
| `Scripts/Inventories/TransferPlanExecutor.cs` | ejecucion, commit y rollback |
| `Scripts/Rules/IDragRule.cs` | contratos y base del sistema de rules |
| `Scripts/Interaction/InputEventRouter.cs` | router central de input |

## Subsystems utiles

### Strategies

- `Scripts/Inventories/Strategies/*`

Define como el inventario acepta, consulta y coloca items.

### Selection

- `Scripts/Selection/*`

Gestiona seleccion multiple, range select y multi-drag.

### Context Menu

- `Scripts/ContextMenu/*`

Gestiona acciones contextuales por slot o item.

### Tooltip

- `Scripts/UI/Tooltip/*`

Gestiona vistas y posicionamiento de tooltips.

### Filter y Sort

- `Scripts/Filter/*`

Contiene presets y componentes UI para filtrar y ordenar.

## Ejemplos

| Carpeta | Uso |
|---|---|
| `Examples/Demo1 Inventories/*` | inventario simple |
| `Examples/Demo2 Loot/*` | loot y objetos del mundo |
| `Examples/Demo3 Minecraft/*` | crafting e inventarios indexados |
| `Examples/Demo4 Trading/*` | comercio y conversion de items |
| `Examples/Demo5 Containers/*` | contenedores anidados |

## Recomendacion

Si vas a extender el asset, abre primero el archivo de la capa correcta:

- `binding` para sincronizacion de datos
- `rules` para restricciones de drag/drop
- `planner/executor` para pipeline de transferencia
- `interaction` para input

