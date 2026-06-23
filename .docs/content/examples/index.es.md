# Ejemplos

Esta sección describe las **escenas demo incluidas en `Examples/`**: cómo están compuestas, qué patrones demuestran y qué archivos merece la pena leer primero.

Usa esta página como mapa de elección. Si ya sabes qué problema estás resolviendo, salta directamente a la demo adecuada; si no, empieza por la tabla.

## Elección rápida

| Tarea | Empieza por | Sistemas clave | Complejidad |
|---|---|---|---|
| Lista básica de inventario, reglas simples y drop areas | [Demo1 Inventories](demo1-inventories.md) | `ListInventoryDataBinding`, hooks locales, rules | Baja |
| UI de cofre, pickup/drop e integración con objetos del mundo | [Demo2 Loot](demo2-loot.md) | world interaction, chest binding, filters | Media |
| Hotbar, inventory, craft grid y slot de resultado | [Demo3 Craft](demo3-Craft.md) | `SlotIndexedInventoryDataBinding`, `CraftingManager`, craft result | Media |
| Comercio, oro, equipo y conversión de modelos de datos | [Demo4 Trading](demo4-trading.md) | converters, domain checks, fixed equipment slots | Alta |
| Un item que contiene su propio inventario anidado | [Demo5 Containers](demo5-containers.md) | context menu, nested binding, occupied-slot handler | Alta |
| Items multi-celda, formas, anchors, rotación y preview de celdas | [Demo6 Shaped Items](demo6-shaped-items.md) | placement topology, shapes, rotation actions | Alta |

## Qué muestra cada demo

| Demo | Qué muestra | Archivos por donde empezar |
|---|---|---|
| [Demo1 Inventories](demo1-inventories.md) | La lista de items más simple, binding básico y comprobaciones locales de drag/drop. | `Examples/Demo1 Inventories/BasicListDataBinding.cs`, `Examples/Demo1 Inventories/ItemAdapterSoAdapter.cs` |
| [Demo2 Loot](demo2-loot.md) | Flujo "mundo -> evento -> UI -> inventario", cofres, pickup/drop y filtros. | `Examples/Demo2 Loot/ChestInventoryController.cs`, `Examples/Demo2 Loot/WorldDropManager.cs` |
| [Demo3 Craft](demo3-Craft.md) | Datos indexados por slot, craft grid, hotbar e inventario de resultado dedicado. | `Examples/Demo3 Craft/CraftingManager.cs`, `Examples/Demo3 Craft/Data/CraftResultDataBinding.cs` |
| [Demo4 Trading](demo4-trading.md) | Transferencias entre modelos de datos distintos, precios, oro y slots de equipo. | `Examples/Demo4 Trading/Domain/TradeDomainHandler.cs`, `Examples/Demo4 Trading/Converters/*` |
| [Demo5 Containers](demo5-containers.md) | Un contenedor como item y como fuente de datos, inventario anidado y protección frente a ciclos. | `Examples/Demo5 Containers/ContainerItemData.cs`, `Examples/Demo5 Containers/ContainerUIController.cs` |
| [Demo6 Shaped Items](demo6-shaped-items.md) | Items con forma en grid: footprint, anchor, orientation, rotación y preview de celdas cubiertas. | `Examples/Demo6 Shaped Items/ShapedItemSO.cs`, `Examples/Demo6 Shaped Items/ShapedItemAdapter.cs` |

## Cómo leer las páginas de demo

Cada página de demo responde a cuatro preguntas:

- qué muestra la demo
- qué objetos runtime participan
- cómo fluye el escenario principal
- qué archivos inspeccionar si quieres reutilizar el patrón en tu proyecto

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
- [Mapa de archivos](../reference/file-map.md) — para encontrar rápidamente un tipo runtime o example concreto

