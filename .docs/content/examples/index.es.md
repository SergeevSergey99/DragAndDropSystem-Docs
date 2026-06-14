# Ejemplos

Esta sección describe las **escenas demo incluidas en `Examples/`**: cómo están compuestas, qué patrones demuestran y qué archivos merece la pena leer primero.

La sección de ejemplos responde a preguntas como:

- cómo está estructurada cada demo por capas
- dónde viven los datos, adapters, bindings y UI
- cómo funciona el flujo principal de interacción
- qué archivos conviene inspeccionar si quieres reutilizar el patrón en tu propio juego

---

## Cómo leer esta sección

Cada página de demo explica:

1. **Qué muestra la demo**
2. **Cómo está estructurada**
3. **Cómo funciona el escenario principal**
4. **Qué archivos inspeccionar**

El objetivo es ayudarte a entender rápidamente la forma arquitectónica de la escena.

---

## Demos incluidas

### [Demo1 Inventories](demo1-inventories.md)

Úsala para:

- el ejemplo más simple de inventario basado en lista
- entender `ListInventoryDataBinding` sin complejidad adicional de dominio
- overrides locales de `CanStartDrag` y `CanDrop`

Muestra:

- `ListInventoryDataBinding<ItemExampleSO, ItemAdapterSoAdapter>`
- cargar una lista en la UI
- drop areas
- reglas básicas de inventario

`Examples/Demo1 Inventories/*`

### [Demo2 Loot](demo2-loot.md)

Úsala para:

- flujo mundo -> evento -> UI -> inventario
- cofres y apertura de UI basada en interacción
- pickup / drop conectados con objetos del mundo

Muestra:

- cambio de la fuente de datos del DataBinding del cofre
- integración world drop / pickup
- filtros de item

`Examples/Demo2 Loot/*`

### [Demo3 Craft](demo3-Craft.md)

Úsala para:

- inventarios indexados por slot
- una crafting grid más un slot de resultado dedicado
- reglas de stack máximo por slot

Muestra:

- `SlotIndexedInventoryDataBinding`
- bindings separados para hotbar / inventory / craft table
- `CraftingManager` como fuente de verdad del dominio
- `CraftResultDataBinding` como inventario de salida personalizado de solo lectura

`Examples/Demo3 Craft/*`

### [Demo4 Trading](demo4-trading.md)

Úsala para:

- cuando se necesitan conversiones de datos entre inventarios
- cuando la operación depende del dinero, los precios y las comprobaciones en el momento de la transferencia

Muestra:

- inventarios de jugador / comerciantes / equipo
- `ListInventoryDataBinding` y `MappedSlotInventoryDataBinding`
- cambios de modelo de datos durante transferencias entre inventarios

`Examples/Demo4 Trading/*`

### [Demo5 Containers](demo5-containers.md)

Úsala para:

- items que contienen su propio inventario
- abrir un contenedor anidado desde un menú contextual
- el item debe soltarse en el slot ocupado por el item contenedor y debe estar protegido frente a ciclos

Muestra:

- item instances en lugar de filas simples de ScriptableObject
- un contenedor que actúa tanto como item como fuente de datos
- `ContainerUIController` y el cambio de contenedor activo
- protecciones como "un contenedor no puede colocarse dentro de sí mismo"

`Examples/Demo5 Containers/*`

---

## Cómo elegir una demo

| Si necesitas | Empieza por |
|---|---|
| Lista básica de inventario + hooks simples | [Demo1 Inventories](demo1-inventories.md) |
| UI de cofre e interacción con el mundo | [Demo2 Loot](demo2-loot.md) |
| Crafting grid y datos indexados por slot | [Demo3 Craft](demo3-Craft.md) |
| Comercio, conversión y lógica de oro | [Demo4 Trading](demo4-trading.md) |
| Contenedores anidados y menú contextual | [Demo5 Containers](demo5-containers.md) |

---

## Nota importante

- Los ejemplos muestran **patrones de integración**, no la única arquitectura válida.
- Un proyecto real puede combinar partes de varias demos.
- Si tu modelo de dominio es diferente, normalmente adaptas primero bindings, adapters y converters, no el transfer pipeline en sí.

---

## Dónde continuar

- [Quick Start](../getting-started/quick-start.md) — para una configuración básica desde cero
- [Data Binding](../architecture/data-binding.md) — para ver el lifecycle completo y los hooks
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — para detalles del orden de transferencia y rollback

