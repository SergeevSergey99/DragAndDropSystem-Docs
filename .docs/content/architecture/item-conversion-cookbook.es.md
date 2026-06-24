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

## Cuándo devolver `null`

Un converter puede devolver `null` si el objeto no se puede convertir de forma segura al modelo requerido.

Es apropiado cuando:

- el objeto no debe entrar en este inventario
- no se puede crear el tipo de adapter destino
- la conversión perdería datos importantes

No uses `null` para bloqueos temporales como “no hay dinero suficiente” o “la tienda está cerrada”.
Para eso usa rules o `ITransferDomainHandler`.

## Errores comunes

### Aparece `Wrong Item Type` después de la transferencia

Comprueba:

- si `CreateItemConverter()` está implementado
- si converter devuelve el adapter del inventario destino
- si quedó en el slot el adapter del inventario origen

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
- cada objeto único conserva su propio estado
- swap está probado en ambas direcciones
- después de la transferencia, el siguiente drag/drop desde el slot destino funciona

Ver también:

- [Demo4 Trading](../examples/demo4-trading.md)
- [Troubleshooting](../reference/troubleshooting.md)
- [Pipeline de transferencia](transfer-pipeline.md)
