# Data Binding

`DataBinding` conecta `UniversalInventory` con tus propios datos.

Esta es la capa que responde a la pregunta:
"¿qué debe actualizar el estado de la UI y qué debe actualizar mi modelo de juego?"

---

## Modelo mental simple

```mermaid
flowchart LR
    Data["Tus datos"] <--> Binding["DataBinding"]
    Binding <--> UI["UniversalInventory"]
```

- `UniversalInventory` gestiona slots, transferencias y eventos
- `DataBinding` traduce esos eventos en cambios sobre tus datos
- tus datos permanecen en tu modelo, no dentro del inventario de UI

---

## Tres plantillas

Elige una plantilla según la estructura de tus datos:

| Plantilla | Úsala para |
|---|---|
| `ListInventoryDataBinding<TData, TAdapter>` | mochila, cofre, loot, listas generales de items |
| `SlotIndexedInventoryDataBinding<TData, TAdapter>` | hotbar, array de slots con índices numéricos |
| `MappedSlotInventoryDataBinding<TData, TAdapter>` | equipamiento, quickbar, slots fijos con nombre |

Para una descripción detallada de cada plantilla, qué métodos implementar y cómo funcionan internamente, consulta [Binding Templates](binding-templates.md).

---

## Lifecycle de transferencia

```mermaid
flowchart TD
    A["El jugador arrastra un item"] --> B["CanStartDrag\n(¿se puede coger?)"]
    B -->|OK| C["CanDrop\n(reglas mecánicas)"]
    C -->|OK| D["CanCommitTransfer\n(comprobaciones de negocio)"]
    D -->|OK| E["Execute transfer"]
    E --> F["OnTransferSucceeded\n(side effects)"]
    F --> G["OnItemRemoved / OnItemAdded\n→ RemoveFromData / AddToData"]

    B -->|Rejected| X["Cancelado"]
    C -->|Rejected| X
    D -->|Rejected| X
```

---

## Qué va en cada sitio

| Hook | Cuándo se ejecuta | Úsalo para |
|---|---|---|
| `CanStartDrag` | antes de que empiece el drag | bloquear coger un item desde el origen |
| `CanDrop` | durante preview y validación del objetivo | restricciones mecánicas, compatibilidad de slot |
| `CanStartTransfer` | una vez, antes de la primera mutación | veto de toda la operación (tienda cerrada, propiedad) |
| `CanCommitTransfer` | antes de confirmar cada colocación concreta | comprobaciones locales por colocación, dinero, veto de dominio |
| `CanStartTransferAsync` | una vez, antes de la primera mutación (solo camino async) | veto async de toda la transferencia: servidor, fichero, base de datos, perfil externo |
| `OnTransferSucceeded` | tras un commit exitoso | cambios de moneda, analíticas, side effects de dominio |
| `AddToData` / `RemoveFromData` | tras los eventos del inventario | sincronizar tus datos |

La separación importante es:

- `CanDrop` es para la mecánica
- `CanStartTransfer` / `CanStartTransferAsync` vetan **toda** la operación antes de que empiece
- `CanCommitTransfer` valida cada colocación **concreta** antes de confirmarla
- `AddToData/RemoveFromData` son solo para sincronización

`CanStartTransfer` y `CanStartTransferAsync` son de toda la transferencia y se ejecutan una
vez, antes de la primera mutación. `CanStartTransferAsync` solo corre en el camino de
ejecución asíncrono; si hay un handler async, una transferencia síncrona se rechaza en lugar
de saltarse la comprobación.

Para más sobre los tres tipos de comprobaciones (rules, business checks, notifications), consulta la página [Transfer Pipeline](transfer-pipeline.md).

---

## Ejemplo de binding de lista normal

```csharp
public class BackpackBinding : ListInventoryDataBinding<ItemSO, ItemSOAdapter>
{
    [SerializeField] private List<ItemSO> _items;

    protected override IReadOnlyList<ItemSO> GetItems() => _items;
    protected override ItemSOAdapter CreateAdapter(ItemSO item) => new(item);
    protected override void AddToData(ItemSOAdapter adapter) => _items.Add(adapter.Data);
    protected override void RemoveFromData(ItemSOAdapter adapter) => _items.Remove(adapter.Data);
}
```

Aquí no hay lógica de negocio. Solo lectura y escritura de datos. Para detalles sobre `ListInventoryDataBinding` y las otras dos plantillas, consulta [Binding Templates](binding-templates.md).

---

## Hook de negocio a nivel de transferencia

Si un binding debe participar en la lógica de negocio de la operación, implementa `ITransferDomainHandler`:

```csharp
public class ShopInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>, ITransferDomainHandler
{
    // Veto de toda la transferencia, una vez antes de cualquier mutación.
    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
    {
        return _shopIsOpen
            ? RuleResult.Success()
            : RuleResult.Failure("La tienda está cerrada");
    }

    // Comprobación por colocación, antes de confirmarla.
    public RuleResult CanCommitTransfer(TransferDomainContext context)
    {
        return ValidateBusinessRules(context)
            ? RuleResult.Success()
            : RuleResult.Failure("La transferencia no está permitida");
    }

    public void OnTransferSucceeded(TransferDomainContext context)
    {
        ApplyDomainEffects(context);
    }
}
```

---

## Veto async de toda la transferencia

Si *toda* la transferencia debe esperar una respuesta externa antes de que algo se mueva, por ejemplo:

- una respuesta del servidor
- leer un fichero
- una consulta a base de datos
- cargar un perfil externo o datos de guardado

implementa también `IAsyncTransferDomainHandler` en el binding. Su único método,
`CanStartTransferAsync`, es un veto de toda la transferencia que se ejecuta una vez antes de
la primera mutación: la contraparte asíncrona de `CanStartTransfer`.

```csharp
public class ServerBackedInventoryBinding
    : ListInventoryDataBinding<ItemModel, ItemModelAdapter>,
      ITransferDomainHandler,
      IAsyncTransferDomainHandler
{
    // Comprobación local por colocación, sigue siendo síncrona.
    public RuleResult CanCommitTransfer(TransferDomainContext context)
    {
        return ValidateLocalState(context)
            ? RuleResult.Success()
            : RuleResult.Failure("La validación local ha fallado");
    }

    public void OnTransferSucceeded(TransferDomainContext context) { }

    // Veto síncrono de toda la transferencia. Requerido por ITransferDomainHandler.
    public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory)
        => RuleResult.Success();

    // Veto asíncrono de toda la transferencia, una vez antes de la primera mutación.
    public async Task<RuleResult> CanStartTransferAsync(
        DragContext context,
        IInventory targetInventory,
        CancellationToken cancellationToken)
    {
        bool allowed = await _serverApi.ValidateTransferAsync(context, cancellationToken);
        return allowed
            ? RuleResult.Success()
            : RuleResult.Failure("El servidor rechazó la transferencia");
    }
}
```

Cómo funciona:

- `CanDrop` sigue siendo un hook síncrono y rápido para preview
- `CanCommitTransfer` gestiona comprobaciones locales por colocación
- `CanStartTransferAsync` es de toda la transferencia y se ejecuta una vez, antes de la primera mutación
- solo corre en el camino de ejecución asíncrono; una transferencia síncrona se rechaza cuando hay un handler async, así que la comprobación nunca se salta
- si la validación asíncrona devuelve `RuleResult.Failure(...)`, se cancela toda la transferencia

Usa `IAsyncTransferDomainHandler` cuando la respuesta no pueda producirse inmediatamente.
Si la comprobación es local y rápida, `CanStartTransfer` / `CanCommitTransfer` bastan.

Para ver el orden exacto de llamadas, el papel de `TransferDomainContext` y la diferencia entre `ITransferDomainHandler` y las rules, consulta [Optional Interfaces](../reference/optional-interfaces.md).

---

## Conversión de items

Si dos inventarios usan representaciones distintas del item, un binding puede proporcionar un converter:

```csharp
protected override IItemAdapterConverter CreateItemConverter()
{
    return new MyInventoryItemConverter();
}
```

Para más detalles sobre cómo funciona la conversión dentro del transfer pipeline, consulta [Transfer Pipeline](transfer-pipeline.md).

---

## Recargar desde los datos

Si tus datos cambian fuera del pipeline de drag & drop, llama a:

```csharp
ReloadUI();
```

O explícitamente:

```csharp
ForceSyncToUI();
```

Para operaciones por lotes, usa `BeginSync()` para suprimir eventos y evitar feedback loops.

---

## Lo que normalmente no necesitas saber

En un proyecto típico no hace falta profundizar en:

- eventos internos del inventario
- el motor de transferencia interno
- clases low-level de colocación y almacenamiento

Normalmente basta con:

1. elegir la [plantilla](binding-templates.md) correcta
2. describir tu adapter
3. implementar la sincronización en `AddToData/RemoveFromData`
4. opcionalmente añadir `CanDrop` y `CanCommitTransfer`

