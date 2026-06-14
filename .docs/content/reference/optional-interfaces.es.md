# Optional Interfaces

Estas interfaces no son necesarias para el flujo básico de drag & drop.
Habilitan comportamiento adicional cuando un subsistema concreto sabe cómo leerlas.

Normalmente esto cae en una de estas dos categorías:

- un binding quiere participar en la lógica de negocio de la transferencia
- un item adapter quiere exponer metadatos extra para la UI o para la lógica de strategies

---

## Mapa rápido

| Interface | Suele implementarse en | Cuándo se usa | Propósito |
|---|---|---|---|
| `ITransferDomainHandler` | normalmente `InventoryDataBinding` | una vez antes de la primera mutación (`CanStartTransfer`), antes de confirmar cada colocación (`CanCommitTransfer`) y tras un commit exitoso (`OnTransferSucceeded`) | validación de negocio a nivel de transferencia y side effects |
| `IAsyncTransferDomainHandler` | normalmente `InventoryDataBinding` | una vez antes de la primera mutación, solo camino async (`CanStartTransferAsync`) | veto async externo de toda la transferencia: servidor, fichero, base de datos |
| `IStackSizeLimitable` | `IItemAdapter` | cuando una stacking strategy calcula la capacidad del stack | límite de stack por item |
| `IDescribable` | `IItemAdapter` | cuando la UI quiere mostrar una descripción | metadatos extra para tooltips y sistemas similares |

---

## ITransferDomainHandler

`ITransferDomainHandler` sirve para la lógica de dominio alrededor de una transferencia.
No es un sustituto de las rules ni otra capa genérica de validación.

La interface tiene tres métodos: un veto de toda la transferencia (`CanStartTransfer`), una
comprobación por colocación (`CanCommitTransfer`) y un hook de éxito (`OnTransferSucceeded`).
Hay que implementar los tres.

Normalmente se implementa en un binding:

```csharp
public class ShopInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>, ITransferDomainHandler
{
    // Veto de toda la transferencia, una vez antes de cualquier mutación.
    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
    {
        return _shopIsOpen ? RuleResult.Success() : RuleResult.Failure("La tienda está cerrada");
    }

    // Comprobación por colocación, antes de confirmar cada colocación concreta.
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

### Orden exacto del pipeline

No hay un plan materializado. El motor de transferencia procesa las entries de forma
secuencial contra el estado real del inventario. Para una sola entry, el orden es:

1. `CanDrop` y el resto de rules deciden si el drop es mecánicamente válido.
2. `CanStartTransfer` se ejecuta una vez, antes de la primera mutación, y puede vetar toda la
   operación. `CanStartTransferAsync` hace lo mismo en el camino de ejecución asíncrono.
3. el motor resuelve un candidato de colocación concreto y crea un `TransferDomainContext` para él.
4. `CanCommitTransfer` se ejecuta antes de mutar esa colocación (primero el binding de origen, luego el de destino si implementan `ITransferDomainHandler`).
5. solo entonces ocurre el commit real: split, conversion, placement y rollback si hace falta.
6. tras confirmar una colocación, se ejecuta `OnTransferSucceeded`.
7. solo después se despachan las notificaciones add/remove del inventario y otros deferred events.

Por tanto:

- `CanStartTransfer` sucede una vez, antes de cualquier mutación de toda la operación
- `CanCommitTransfer` sucede antes de cualquier mutación de una colocación concreta
- `OnTransferSucceeded` sucede después de un commit exitoso, pero antes de `OnItemRemoved` / `OnItemAdded`

### Qué contiene TransferDomainContext

`TransferDomainContext` proporciona al binding contexto a nivel de transferencia:

- `SourceInventory` / `TargetInventory`
- `SourceBinding` / `TargetBinding`
- `SourceBaseSlot`
- `PlannedTargetBaseSlot`
- `TargetBaseSlot` después del commit
- `SourceItemAdapter`
- `PreviewTargetItemAdapter`
- `TargetItemAdapter` después del commit
- `RequestedAmount`
- `CommittedAmount`
- `Kind`
- `IsCommitted`

Esto importa en casos donde la decisión de negocio depende del significado de la operación, no solo del contenido del slot:

- comprar a un comerciante
- vender un item
- mover algo entre facciones / contenedores / capas de autoridad
- validar una restricción externa antes del commit

### Buenos encajes

- comprobaciones de moneda
- comprobaciones de permisos
- validación del servidor
- side effects que no son sincronización normal de datos

### Malos encajes

- compatibilidad de slot
- restricciones normales del inventario
- sincronización habitual de `AddToData` / `RemoveFromData`
- lógica de preview de UI

Si la pregunta es "¿puede este tipo de item ir en este slot?", eso normalmente son rules.
Si la pregunta es "¿puede confirmarse ahora mismo esta operación ya planificada?", eso sí es candidato para `ITransferDomainHandler`.

---

## IAsyncTransferDomainHandler

`IAsyncTransferDomainHandler` amplía `ITransferDomainHandler` con un veto asíncrono de
**toda la transferencia**, `CanStartTransferAsync`, para cuando la respuesta no puede
obtenerse inmediatamente. Es la contraparte async de `CanStartTransfer`, no de `CanCommitTransfer`.

```csharp
public class ServerInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>,
      ITransferDomainHandler,
      IAsyncTransferDomainHandler
{
    public RuleResult CanCommitTransfer(TransferDomainContext context)
        => ValidateLocalState(context);

    public void OnTransferSucceeded(TransferDomainContext context) { }

    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
        => RuleResult.Success();

    public async Task<RuleResult> CanStartTransferAsync(
        DragContext context,
        IInventory targetInventory,
        CancellationToken cancellationToken)
    {
        return await _serverApi.ValidateTransferAsync(context, cancellationToken);
    }
}
```

Úsalo cuando debas esperar:

- una respuesta del servidor
- la lectura de un fichero o datos de guardado
- una base de datos
- un perfil externo o capa de autoridad

Importante:

- `CanStartTransferAsync` es de toda la transferencia y se ejecuta una vez, antes de la primera mutación
- solo corre en el camino de ejecución asíncrono; una transferencia síncrona se rechaza cuando hay un handler async, así que la comprobación nunca se salta en silencio
- un fallo en la fase asíncrona cancela toda la transferencia sin mutar los inventarios

Si la comprobación es puramente local y rápida, `CanStartTransfer` / `CanCommitTransfer` bastan.

---

## IStackSizeLimitable

`IStackSizeLimitable` permite que un item adapter defina su propio límite de stack:

```csharp
public class AmmoAdapter : IItemAdapter, IStackSizeLimitable
{
    public int MaxStackSize => 120;
}
```

Esto es útil para sistemas como:

- inventarios estilo Craft donde distintos tipos de item tienen distintos límites de stack
- inventarios RPG donde las pociones hacen stack hasta 20, las flechas hasta 999 y las armas hasta 1
- inventarios de supervivencia/crafteo donde contenedores y herramientas no hacen stack pero los recursos sí

### Dónde se lee realmente

La interface es leída por inventory strategies cuando calculan la capacidad del stack:

- `StackableItemStrategy`
- `SeparableStacksStrategy`
- el motor de transferencia a través de `UniversalInventory.GetMaxStackSizeForItem(...)`

### Nota importante sobre _allowItemStackOverride

En la implementación actual, `IStackSizeLimitable` solo se usa cuando `_allowItemStackOverride` está activado en `UniversalInventory`.

El comportamiento actual es:

- si `_allowItemStackOverride == false`, solo se usa `_maxStackSize` del inventario
- si `_allowItemStackOverride == true` y el item implementa `IStackSizeLimitable`, el `MaxStackSize` del item sustituye por completo a `_maxStackSize` del inventario

Por tanto, en el código actual esto no significa "el item solo puede elevar el límite".
Es un override completo a nivel de item, y puede ser:

- menor que el límite del inventario
- igual al límite del inventario
- mayor que el límite del inventario

Ejemplos:

- inventario `_maxStackSize = 20`, `_allowItemStackOverride = false`, item `MaxStackSize = 99` -> el límite efectivo sigue siendo `20`
- inventario `_maxStackSize = 20`, `_allowItemStackOverride = true`, item `MaxStackSize = 99` -> el límite efectivo es `99`
- inventario `_maxStackSize = 20`, `_allowItemStackOverride = true`, item `MaxStackSize = 5` -> el límite efectivo es `5`

Si necesitas una semántica distinta, como "un item solo puede bajar el límite" o "un item solo puede superar el límite hacia arriba", eso requiere una strategy personalizada.

---

## IDescribable

`IDescribable` no afecta al transfer pipeline principal.
Es un ejemplo simple de extender `IItemAdapter` con datos extra para la UI.

```csharp
public class ItemAdapter : IItemAdapter, IDescribable
{
    public string Description => _item.Description;
}
```

El caso de uso estándar dentro del asset ahora mismo es:

- `DefaultTooltipView` muestra `Description` si el adapter implementa `IDescribable`

Pero la idea es más amplia:

- tooltip personalizado
- panel de inspección
- hover card
- detalles del menú contextual
- cualquier otro sistema opcional de UI

Por eso `IDescribable` se entiende mejor no como una interface obligatoria especial para tooltip,
sino como un patrón para ampliar adapters con interfaces pequeñas y enfocadas.

Si `IItemAdapter` deja de ser suficiente, puedes añadir interfaces como:

- `IDescribable`
- `IFilterable`
- `ISortable`
- tus propias `IItemStatsProvider`, `IRarityProvider`, `IFlavorTextProvider`, etc.

El core del inventario no depende de ellas.
Solo deberían consultarlas los sistemas que realmente las necesiten.

