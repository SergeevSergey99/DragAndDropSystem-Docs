# Conversión de objetos

Esta página explica cómo mover objetos entre inventarios que usan modelos de datos distintos.

Ejemplo: un comerciante guarda mercancía como `ScriptableObject`, el jugador guarda los
objetos comprados como modelos runtime, y el equipamiento usa slots fijos con sus propias
comprobaciones.

## Cuándo hace falta un converter

Hace falta un converter cuando un objeto debe pasar de un tipo de adapter a otro.

Casos típicos:

- comerciante y jugador guardan objetos en modelos distintos
- inventario de equipamiento acepta solo adapters especiales
- un contenedor dentro de un objeto guarda instancias runtime
- el mismo objeto debe verse de forma distinta en diferentes inventarios

Si ambos inventarios usan el mismo tipo de adapter, normalmente no hace falta converter.

## Dónde configurarlo

El converter se configura en el binding:

```csharp
protected override IItemAdapterConverter CreateItemConverter()
{
    return new MyItemAdapterConverter();
}
```

El binding le dice al sistema:

- cómo se ven los objetos de este inventario cuando salen
- cómo deben verse los objetos cuando entran en este inventario

Si no se configura converter, el objeto se deja tal cual.

## Modelo simple

Cuando un objeto se mueve de un inventario a otro, el sistema necesita un adapter que el
inventario destino entienda.

```text
adapter del inventario origen
  -> converter
  -> adapter del inventario destino
```

El objetivo principal del converter es no perder el significado del objeto al pasar de un modelo de datos a otro.

## Las reglas ven el objeto ya convertido

Las comprobaciones del inventario destino se ejecutan **después** de la conversión. `CanDrop`, las
reglas del inventario y las del slot reciben el objeto tal como existirá una vez dentro, ya en el
tipo de adapter del inventario destino.

Eso es lo que hace que los bindings tipados funcionen entre modelos de datos distintos. Un binding
declarado como `MappedSlotInventoryDataBinding<TradableItemModel, TradableItemAdapterModelAdapter>`
recibe su propio tipo de adapter aunque el objeto venga de un mercader que guarda
`ScriptableObject`:

```csharp
canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Weapon
    ? RuleResult.Success()
    : RuleResult.Failure("Only weapons can be placed in this slot")
```

No llames al converter dentro de una regla. Si lo haces, creas un segundo objeto que no es el que la
transferencia va a guardar, y lo pagas en cada frame mientras el cursor está encima.

`CanStartDrag`, en cambio, se ejecuta del lado del origen y ve el adapter del inventario origen.

## La conversión ocurre durante la vista previa

El converter se llama mientras el jugador solo está pasando por encima, mucho antes de soltar nada.
Dos consecuencias.

**El converter debe ser una fábrica pura.** Nada de registrar el objeto nuevo, ni de tomar un id de
un contador, ni de instanciar objetos. Un hover que el jugador abandona no debe dejar rastro. Ese
trabajo va en `ITransferDomainHandler.OnTransferSucceeded`, que solo se ejecuta tras una
transferencia confirmada.

**El objeto que construyes en la vista previa es el que se guarda.** Las conversiones se resuelven
una vez por arrastre y se reutilizan, así que el adapter que validó `CanDrop` es la misma instancia
que acaba en el slot destino. No hace falta abaratar la conversión: ocurre una sola vez.

## Qué debe conservar un converter

Si los objetos son únicos, el converter debe conservar más que icono y nombre.

Comprueba que conserva:

- `ItemId`, si afecta al stacking
- cantidad de objetos en el stack
- estado runtime único
- referencia al modelo de dominio, si el objeto no es solo un `ScriptableObject`
- datos usados por `CanDrop`, tooltip, precio, rareza o equipamiento

Si después de la transferencia “el objeto se ve bien, pero ya no se puede arrastrar”, probablemente el slot destino recibió un adapter incorrecto o perdió datos necesarios.

## Swap entre inventarios distintos

Swap entre distintos tipos de inventario no es un simple intercambio de dos stacks.

Cada objeto debe convertirse al modelo del inventario al que entra:

```text
objeto A -> modelo del inventario B
objeto B -> modelo del inventario A
```

Si simplemente intercambias dos adapters, el siguiente drag/drop puede romperse porque un slot guarda un objeto en formato incorrecto.

Ambas direcciones se comprueban también contra las reglas del inventario en el que aterrizan. El
objeto que vuelve del destino debe poder salir de su slot y entrar en el slot origen, exactamente
como si lo hubieras soltado ahí. Un swap no es una forma de esquivar una regla que rechazaría un drop
normal.

## Cuándo devolver `null`

Un converter puede devolver `null` si el objeto no se puede convertir de forma segura al modelo requerido.

Es apropiado cuando:

- el objeto no debe entrar en este inventario
- no se puede crear el tipo de adapter destino
- la conversión perdería datos importantes

No uses `null` para bloqueos temporales como “no hay dinero suficiente” o “la tienda está cerrada”.
Para eso usa rules o `ITransferDomainHandler`.

Devolver `null` rechaza el drop igual que lo hace una regla: el slot muestra el rechazo mientras el
jugador sigue arrastrando, en lugar de que el arrastre termine sin que pase nada.

## Errores comunes

### Aparece `Wrong Item Type` después de la transferencia

Comprueba:

- si `CreateItemConverter()` está implementado
- si converter devuelve el adapter del inventario destino
- si quedó en el slot el adapter del inventario origen

### El destino rechaza todo lo que viene de otro inventario

Si un slot con una comprobación tipada rechaza cualquier objeto proveniente de un inventario con otro
modelo de datos, hay que mirar el converter, no la regla. La regla ya recibe el objeto convertido, así
que un rechazo significa que la conversión no produjo el tipo de adapter esperado.

Comprueba:

- si el binding destino sobreescribe `CreateItemConverter()`
- si `TryConvertIncoming` contempla el tipo de adapter del origen (un `switch` sin rama que encaje
  devuelve `null` y rechaza el drop)
- si el converter conserva los campos que lee la regla

### Una transferencia parcial se comporta de forma extraña

Al arrastrar parte de un stack se mueven los **últimos** objetos de ese stack. Si tu propio código
construye un stack para predecir qué se moverá, constrúyelo igual (`stack.CreateCopy(count)`); si no,
tu predicción y la transferencia real hablan de instancias distintas. Solo se nota en movimientos
parciales, nunca en los de stack completo.

### El primer swap funciona, el segundo se rompe

Comprueba:

- si existe conversión en ambas direcciones
- si dos stacks se intercambian directamente sin converter
- qué adapter queda en cada slot después del primer swap

### Se perdieron datos del objeto

Comprueba:

- si se transfiere el estado runtime de la instancia
- si todos los objetos del stack se crean desde un solo adapter
- si se pierden precio, rareza, durabilidad, dueño u otros campos del modelo

## Checklist para un converter nuevo

- inventarios origen y destino realmente usan tipos de adapter distintos
- binding sobreescribe `CreateItemConverter()`
- converter crea el tipo de adapter esperado por el inventario destino
- converter es una fábrica pura: sin registros, sin contadores de id, sin instanciar
- converter conserva los campos que leen las reglas del destino (`ItemId`, tipo, precio, …)
- cada objeto único conserva su propio estado
- swap está probado en ambas direcciones
- la transferencia parcial está probada, no solo los movimientos de stack completo
- después de la transferencia, el siguiente drag/drop desde el slot destino funciona

Ver también:

- [Demo4 Trading](../examples/demo4-trading.md)
- [Troubleshooting](../reference/troubleshooting.md)
- [Pipeline de transferencia](transfer-pipeline.md)
