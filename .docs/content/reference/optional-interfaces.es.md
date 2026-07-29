# Interfaces opcionales

Estas interfaces no son necesarias para el drag & drop básico. Úsalas cuando necesites añadir comportamiento encima de una transferencia normal.

## Qué elegir

| Si necesitas... | Usa |
|---|---|
| Bloquear una transferencia por dinero, permisos, propietario del item o estado de la tienda | `ITransferDomainHandler` |
| Preguntar a un servidor u otro sistema externo antes de transferir | `IAsyncTransferDomainHandler` |
| Añadir comportamiento especial al soltar sobre un slot ocupado: insertar en contenedor, equipar, abrir un item | `IPreRuleOccupiedSlotDropHandler` o `IPostRuleOccupiedSlotDropHandler` |
| Definir distintos límites de stack para distintos items | `IStackSizeLimitable` |
| Mostrar una descripción del item en un tooltip u otra UI | `IDescribable` |

## ITransferDomainHandler

Usa `ITransferDomainHandler` cuando la decisión depende de lógica del juego, no solo del slot y del item.

Ejemplos:

- si el jugador tiene suficiente oro para comprar
- si este item puede venderse
- si el item pertenece al jugador
- si la tienda está abierta
- si se pueden mover items entre estos dos contenedores

Normalmente la interface se implementa en un DataBinding:

```csharp
public class ShopInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>, ITransferDomainHandler
{
    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
    {
        return _shopIsOpen ? RuleResult.Success() : RuleResult.Failure("Shop is closed");
    }

    public RuleResult CanCommitTransfer(TransferDomainContext context)
    {
        return HasEnoughMoney(context)
            ? RuleResult.Success()
            : RuleResult.Failure("Not enough money");
    }

    public void OnTransferSucceeded(TransferDomainContext context)
    {
        SpendMoney(context);
    }
}
```

Métodos:

| Método | Cuándo se ejecuta | Para qué sirve |
|---|---|---|
| `CanStartTransfer` | una vez antes de la transferencia | rechazar toda la operación |
| `CanCommitTransfer` | antes de confirmar una colocación concreta | comprobar dinero, permisos, propietario y lógica similar |
| `OnTransferSucceeded` | después de una colocación exitosa | gastar dinero, enviar analytics, actualizar un sistema externo |

No pongas aquí una comprobación normal de "este tipo de item puede ir en este slot". Para eso encajan mejor rules, `CanDrop` o la configuración de fixed slots.

## IAsyncTransferDomainHandler

`IAsyncTransferDomainHandler` sirve cuando la transferencia debe esperar una respuesta externa.

Por ejemplo:

- el servidor valida la transferencia
- se comprueban datos guardados en disco
- un sistema externo comprueba permisos

```csharp
public async Task<RuleResult> CanStartTransferAsync(
    DragContext context,
    IInventory targetInventory,
    CancellationToken cancellationToken)
{
    return await _serverApi.ValidateTransferAsync(context, cancellationToken);
}
```

Reglas importantes:

- la comprobación se aplica a toda la transferencia
- se ejecuta antes de modificar inventarios
- si la comprobación falla, la transferencia no empieza
- si la comprobación es rápida y local, normalmente basta con `ITransferDomainHandler`

## IOccupiedSlotDropHandler

`IOccupiedSlotDropHandler` sirve cuando soltar sobre un slot ocupado debe significar una acción propia, no swap normal ni colocación alternativa.

Ejemplos:

- soltar un item sobre una bolsa para meterlo dentro
- soltar un item sobre un slot equipado para hacer un reemplazo especial
- soltar una llave sobre un contenedor para abrirlo

No implementes directamente `IOccupiedSlotDropHandler`. Implementa una de las interfaces de timing:

| Interface | Cuándo se ejecuta | Para qué sirve |
|---|---|---|
| `IPreRuleOccupiedSlotDropHandler` | antes de las drop rules del slot destino | el drop realmente va dirigido al objeto dentro del slot, por ejemplo un contenedor |
| `IPostRuleOccupiedSlotDropHandler` | después de las drop rules del slot destino | las reglas normales del destino deben permitir el drop primero |

Normalmente la interface se implementa en el DataBinding del inventario destino:

```csharp
public class ContainerInventoryBinding
    : SlotIndexedInventoryDataBinding<ItemModel, ItemModelAdapter>,
      IPreRuleOccupiedSlotDropHandler
{
    public bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot)
    {
        return occupiedSlot.Stack?.PrimaryAdapter is ContainerAdapter;
    }

    public OccupiedSlotDropResult ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot)
    {
        return TryPutIntoContainer(entry, occupiedSlot)
            ? OccupiedSlotDropResult.Handled
            : OccupiedSlotDropResult.Rejected;
    }
}
```

`ExecuteOccupiedSlotDrop` devuelve:

| Resultado | Qué hace el pipeline |
|---|---|
| `Handled` | el handler ejecutó la acción; no se ejecutan el drop normal, swap ni colocación alternativa |
| `Rejected` | el handler rechazó la acción; la transferencia se revierte |
| `Fallthrough` | el handler decide no interceptar; la transferencia continúa como un drop normal sobre un slot ocupado |

Si el handler devuelve `Handled` o `Rejected`, termina ese intento de transferencia. El sistema no ejecuta swap ni colocación alternativa después.

## IStackSizeLimitable

`IStackSizeLimitable` permite que un item concreto defina su propio límite de stack:

```csharp
public class AmmoAdapter : IItemAdapter, IStackSizeLimitable
{
    public int MaxStackSize => 120;
}
```

Ejemplos:

- pociones con stack de 20
- flechas con stack de 999
- armas con stack de 1
- recursos y herramientas con límites distintos

Para que este límite se use, activa `Allow Item Stack Override` **en la estrategia del inventario**, no en el propio `UniversalInventory`.

Ambos ajustes viven en `StackBasedInventoryStrategyBase`, así que en el Inspector están en el grupo `Strategy`, dentro de la estrategia seleccionada:

| Ajuste | Dónde buscarlo |
|---|---|
| `Max Stack Size` | `UniversalInventory` → `Strategy` → estrategia seleccionada |
| `Allow Item Stack Override` | en el mismo sitio, **solo aparece cuando `Max Stack Size` es mayor que 0** |

!!! warning La estrategia debe ser stack-based
    Estos campos solo existen en `StackableItemStrategy` y `SeparableStacksStrategy`.
    `UniqueItemStrategy` no tiene stacks, así que `IStackSizeLimitable` no le afecta.

Si el override está desactivado, se usa el `Max Stack Size` general de la estrategia.
Si está activado y el item implementa `IStackSizeLimitable`, el límite del item sustituye al límite general.

Ejemplos:

| Configuración | Resultado |
|---|---|
| estrategia `Max Stack Size = 20`, override desactivado, item `MaxStackSize = 99` | límite `20` |
| estrategia `Max Stack Size = 20`, override activado, item `MaxStackSize = 99` | límite `99` |
| estrategia `Max Stack Size = 20`, override activado, item `MaxStackSize = 5` | límite `5` |

## IDescribable

`IDescribable` permite que un adapter entregue una descripción del item para la UI.

```csharp
public class ItemAdapter : IItemAdapter, IDescribable
{
    public string Description => _item.Description;
}
```

El ejemplo estándar `DefaultTooltipView` muestra `Description` cuando el adapter implementa `IDescribable`.

También puedes usar esta interface en tu propia UI:

- tooltip
- panel de inspección
- hover card
- detalles en menú contextual

## Interfaces pequeñas propias

Si `IItemAdapter` no basta, puedes añadir interfaces pequeñas propias para datos específicos del proyecto.

Por ejemplo:

- `IItemStatsProvider`
- `IRarityProvider`
- `IFlavorTextProvider`

El core del inventario no depende de ellas. Solo deberían leerlas los sistemas de UI o gameplay que necesiten esos datos.
