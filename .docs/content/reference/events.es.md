# Events

El sistema emite eventos durante transferencias y swaps.

## Tipos principales

- `InventoryItemEventContext`
- `InventorySwapContext`

## Para que sirven

- actualizar otras capas del UI
- disparar audio o feedback visual
- sincronizar estado secundario del juego

## Recomendacion

Usa los eventos para reaccionar al resultado del pipeline. No los uses para reemplazar la logica principal de binding o commit.

