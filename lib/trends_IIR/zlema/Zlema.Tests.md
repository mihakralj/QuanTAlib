# ZLEMA Tests

This indicator uses a PineScript reference. Tests are split into unit and validation layers.

## Unit Coverage

- Constructor validation and naming
- Streaming updates and `isNew` correction rollback
- NaN and Infinity substitution
- Warmup and `IsHot` transitions
- Batch, span, streaming, and eventing parity
- `Prime` state initialization

## Validation Coverage

- Reference implementation parity for streaming, batch, and span paths
