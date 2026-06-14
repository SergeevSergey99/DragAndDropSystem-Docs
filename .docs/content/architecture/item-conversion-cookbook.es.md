# Cookbook: Item Conversion

Esta página responde a una pregunta práctica:
"¿cómo debería configurar la conversión entre dos inventarios que usan modelos de adapter distintos?"

La visión arquitectónica general ya existe en [Transfer Pipeline](transfer-pipeline.md).
Esta página se centra en reglas de trabajo reales y errores comunes.

---

## Cuándo hace falta un converter

Hace falta un converter cuando dos inventarios usan representaciones distintas del mismo item.

Ejemplos típicos:

- un comerciante almacena `ScriptableObject`s mientras el jugador usa runtime models
- un inventario orientado a UI usa adapters ligeros mientras el modelo de dominio usa instancias ricas
- un límite de inventario dentro de un item contenedor usa otro modelo de adapter

Si ambos lados ya usan el mismo tipo de adapter, normalmente no hace falta converter.

---

## Dónde vive el converter

El converter se declara en el lado del inventory binding:

```csharp
protected override IItemAdapterConverter CreateItemConverter()
{
    return new MyItemAdapterConverter();
}
```

Así, el binding define:

- cómo este inventario exporta un item
- cómo este inventario importa un item

Por defecto, el sistema usa un identity converter.

---

## Quién llama a la conversión

### Preview

Durante el preview, la conversión está orquestada por `TransferItemConversionUtility`.

Esto es necesario para que las rules del target y los hooks del binding vean un target-side adapter en lugar del source-side adapter original.

### Ejecución normal

Para una transferencia normal, la cadena es:

1. el item se toma del slot de origen
2. se ejecuta `source outgoing`
3. después se ejecuta `target incoming`
4. solo después de eso se coloca el item en el inventario objetivo

### Swap

El swap no es una conversión simétrica única.
Son dos cadenas separadas:

- `A -> B`
- `B -> A`

Cada una pasa por su propia secuencia `outgoing -> incoming`.

---

## Modelo mental correcto

No pienses en términos de "el inventario de origen entrega el objeto final del target".

Piensa mejor así:

```text
source adapter
  -> source outgoing
  -> intermediate representation
  -> target incoming
  -> target adapter
```

La representación intermedia no necesita ser un tipo dedicado.
Lo importante es que los límites de origen y destino permanezcan independientes.

---

## Qué debe preservar un adapter

Si tus items tienen state de instancia, el adapter debe transportarlo con seguridad a través de la conversión:

- un `ItemId` estable, si la semántica de stacking depende de él
- campos runtime de la instancia concreta
- una referencia a la entidad de dominio, si el item es único
- datos que luego usa `CanStartDrag`, `CanDrop`, tooltips y side effects

Si el item "parece correcto después de la transferencia pero el drag falla más tarde", la causa habitual es que el target recibió el tipo de adapter equivocado o el state de instancia equivocado.

---

## Qué no debes hacer

### No uses un solo adapter como representante de todo el stack

Si un stack contiene distintas instancias runtime, no clones un solo adapter mediante `Repeat`.

Eso provoca:

- pérdida de state de instancia
- divergencia entre preview y execution
- payloads de remove/add distorsionados

### No implementes el swap entre inventarios como un raw stack exchange

Si el swap simplemente intercambia dos `ItemStack`:

- el slot objetivo recibe un tipo de adapter extranjero
- el siguiente `CanStartDrag` o `CanDrop` empieza a fallar por type checks

### No dependas de que preview y execution compartan la misma referencia de objeto

El preview stack y el execution stack pueden ser objetos distintos.
La estabilidad debe venir de los datos y de la semántica de conversión, no de la igualdad por referencia.

---

## Cuándo un converter debería devolver `null`

`null` no significa "no quiero hacerlo ahora mismo", sino "este límite no puede exportar/importar este item".

Eso es apropiado cuando:

- el item no debe cruzar nunca este límite
- el binding no puede materializar el target adapter requerido
- la pérdida de datos sería inaceptable

Si la operación está solo temporalmente prohibida por lógica de negocio, ese no es trabajo del converter.
Usa:

- rules
- `CanStartTransfer` / `CanStartTransferAsync`
- `CanCommitTransfer`

---

## Cómo diagnosticar errores de conversión

### Síntoma: `Wrong item type`

Normalmente significa:

- el target binding recibió el source adapter type
- el swap se confirmó como raw exchange
- el preview convirtió correctamente, pero la execution no

### Síntoma: el primer swap funciona y el segundo se rompe

Normalmente significa:

- el commit tuvo éxito, pero se guardó el tipo de adapter incorrecto en el slot
- después del primer swap, el slot contiene físicamente un objeto del otro límite de inventario

### Síntoma: el preview pasa, pero el commit falla

Normalmente significa:

- el preview stack se montó correctamente
- pero la execution usó una ruta de conversión diferente

---

## Mini checklist para converters nuevos

- el binding realmente sobreescribe `CreateItemConverter()`
- outgoing e incoming son tan simétricos como requiere tu modelo de dominio
- cada adapter del stack se convierte individualmente
- el target almacena su propio inventory-specific adapter type después del commit
- el swap se ejecuta como dos cadenas de conversión independientes

---

## Dónde continuar

- [Pipeline de transferencia](transfer-pipeline.md) — el orden completo de transferencia
- [Demo4 Trading](../examples/demo4-trading.md) — ejemplo funcional de conversión entre merchant/player/equipment
- [Troubleshooting](../reference/troubleshooting.md) — síntomas y causas comunes

