# Trading

Trading es un caso donde el drag and drop depende del dominio.

## Normalmente necesitas

- conversion de items entre inventarios
- validacion por dinero o precio
- hooks antes del commit
- efectos de dominio despues del exito

## Puntos clave

- `IItemAdapterConverter`
- `ITransferDomainHandler`
- bindings separados para jugador y comerciante

Para una implementacion completa, revisa `Demo4 Trading`.

