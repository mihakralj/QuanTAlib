# Release Note: CCI WarmupPeriod — Static to Instance Migration

## Summary

`Cci.WarmupPeriod` has been changed from a **static** property to an **instance** property.
This allows each `Cci` instance to report the warmup period for its configured `period` parameter,
rather than a single hard-coded default.

## Breaking Change

Code that previously accessed `Cci.WarmupPeriod` as a static member will no longer compile:

```csharp
// ❌ Before (no longer compiles)
int warmup = Cci.WarmupPeriod;
```

## Migration

### Option A — Use the instance property (recommended)

```csharp
var cci = new Cci(period: 14);
int warmup = cci.WarmupPeriod; // returns 14
```

### Option B — Use the obsolete static accessor (temporary bridge)

A static `DefaultWarmupPeriod` property has been added and marked `[Obsolete]` to ease migration:

```csharp
// ⚠️ Compiles with a warning; will be removed in a future major version.
#pragma warning disable CS0618
int warmup = Cci.DefaultWarmupPeriod; // returns 20 (the default period)
#pragma warning restore CS0618
```

## Timeline

| Milestone | Action |
|-----------|--------|
| Current release | `Cci.DefaultWarmupPeriod` available as `[Obsolete]` static bridge |
| Next major version | `Cci.DefaultWarmupPeriod` will be removed |

## Related Changes

- **Ppo.Update(TSeries):** Fixed state synchronization — `_p_state = _state` is now
  assigned after the batch loop, matching the pattern used in `Pmo.Update(TSeries)`.
- **Ppo.Batch(ReadOnlySpan):** Added `fastPeriod >= slowPeriod` guard to match the
  constructor validation, ensuring invalid parameter combinations are rejected early.
