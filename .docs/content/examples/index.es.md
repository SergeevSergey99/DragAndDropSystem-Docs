# Ejemplos

Esta sección describe las **escenas demo de `Examples/`**: de qué partes constan, qué
patrones muestran y qué archivos conviene mirar en el código.

Usa esta página como mapa. Si ya sabes qué tarea estás resolviendo, ve directamente a la
demo correspondiente; si no, empieza por la tabla.

## Qué muestra cada demo

| Demo | Contenido |
|---|---|
| [Demo1 Inventories](demo1-inventories.md) | Lista básica de inventario, reglas simples y drop areas. La lista de objetos más simple, binding básico y comprobaciones locales de drag/drop. |
| [Demo2 Loot](demo2-loot.md) | Sincronización de slots con datos. Cofre, pickup/drop y conexión entre UI y objetos del mundo. Flujo "mundo -> evento -> UI -> inventario" con cofres, pickup/drop y filtros. |
| [Demo3 Craft](demo3-Craft.md) | Grid de crafting y slot de resultado. |
| [Demo4 Trading](demo4-trading.md) | Transferencia entre distintos modelos de datos, precios, compra y venta, e inventario de equipo. |
| [Demo5 Containers](demo5-containers.md) | Contenedor como item y como fuente de datos, inventario anidado y protección frente a ciclos. |
| [Demo6 Shaped Items](demo6-shaped-items.md) | Objetos de forma compleja que ocupan varios slots en una grid rectangular: rotación y dibujo del objeto sobre los slots. Compatible con inventarios y estrategias normales. |

## Cómo leer las páginas de demo

Cada página de ejemplo responde a cuatro preguntas:

- qué muestra la demo
- qué objetos runtime participan
- cómo funciona el escenario principal
- qué archivos mirar si quieres llevar el patrón a tu propio proyecto

## Notas importantes

- Las demos muestran **patrones de integración**, no la única arquitectura correcta.
- Puedes combinar partes de distintas demos, por ejemplo contenedores de Demo5 y slots fijos de Demo4.
- Si tu modelo de dominio es distinto, normalmente cambian bindings, adapters y converters, no el transfer pipeline base.

Ver también:

- [Quick Start](../getting-started/quick-start.md) — para una configuración básica desde cero
- [Data Binding](../architecture/data-binding.md) — para ver el lifecycle completo y los hooks
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — para el orden de transferencia y rollback
- [Mapa de archivos](../reference/file-map.md) — para encontrar rápidamente un tipo runtime o example concreto
