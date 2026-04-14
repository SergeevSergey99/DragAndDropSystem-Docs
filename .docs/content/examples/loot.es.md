# Loot

El patron de loot suele combinar:

- una fuente de datos del mundo
- una ventana de UI abierta por interaccion
- un binding para el contenedor
- un binding para el jugador

## Flujo tipico

1. El jugador interactua con un cofre o pickup.
2. Se abre la UI adecuada.
3. El binding carga los datos en el inventario visual.
4. Las transferencias actualizan la fuente de datos real.

Para una implementacion completa, revisa `Demo2 Loot`.

