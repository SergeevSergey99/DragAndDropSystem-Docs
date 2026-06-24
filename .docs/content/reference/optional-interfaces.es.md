# Interfaces opcionales

Estas interfaces no son necesarias para el drag & drop básico. Úsalas cuando necesites añadir comportamiento encima de una transferencia normal.

## Qué elegir

| Si necesitas... | Usa |
|---|---|
| Bloquear una transferencia por dinero, permisos, propietario del item o estado de la tienda | `ITransferDomainHandler` |
| Preguntar a un servidor u otro sistema externo antes de transferir | `IAsyncTransferDomainHandler` |
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

Para que este límite se use, `_allowItemStackOverride` debe estar activado en `UniversalInventory`.

Si `_allowItemStackOverride` está desactivado, se usa el `_maxStackSize` general del inventario.
Si está activado y el item implementa `IStackSizeLimitable`, el límite del item sustituye al límite general.

Ejemplos:

| Configuración | Resultado |
|---|---|
| inventario `_maxStackSize = 20`, override desactivado, item `MaxStackSize = 99` | límite `20` |
| inventario `_maxStackSize = 20`, override activado, item `MaxStackSize = 99` | límite `99` |
| inventario `_maxStackSize = 20`, override activado, item `MaxStackSize = 5` | límite `5` |

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
