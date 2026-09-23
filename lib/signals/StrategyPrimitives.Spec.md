# Strategy Primitives Specification

Status: Proposed

## Purpose

This directory defines the pluggable signal primitives that sit above QuanTAlib indicators and below a host execution platform such as Quantower or Lean.

The boundary is **target exposure and trading intent**. These primitives may produce conditions, events, allocations, and protective levels. They must not place orders, simulate fills, model slippage, manage broker connections, or own a backtesting loop.

The pipeline is:

```text
Transforms / Indicators
        |
        v
Comparative Predicates
        |
        v
Temporal Triggers
        |
        v
Logic Combinators
        |
        v
Position State Machine ---> StrategyIntent
```

Layer 1 remains in the existing indicator and transform namespaces. This directory owns Layers 2 through 4 and the contracts needed to feed a host-owned Layer 5 position state machine.

## Repository Baseline and Non-Duplication Rules

The current library already contains the indicator-side contracts that the signal primitive layer must build on. The signal primitive spec must not duplicate or re-define these behaviors with a different vocabulary.

Relevant existing abstractions:

- `lib/core/AbstractBase.cs`: owns the base indicator lifecycle contract (`Name`, `WarmupPeriod`, `IsHot`, `Prime`, `Update`, `Reset`, `Pub`).
- `lib/core/BiInputIndicatorBase.cs`: demonstrates the canonical `isNew` correction and rollback pattern for bar updates, including snapshot/restore behaviors.
- `lib/core/ringbuffer/RingBuffer.cs`: provides the fixed-capacity, zero-allocation state container pattern for rolling windows and running sums.
- `lib/core/tvalue/tvalue.cs`: defines `TValue` as the timestamped streaming value type used throughout the library.
- `lib/core/tseries/ITValuePublisher.cs`: defines the event-publishing contract used by indicators to emit value updates.

The design goal is therefore not to duplicate the indicator lifecycle. Signal primitives will add their own typed outputs and state only where required for predicates, triggers, composition, and host-neutral intent. The lifecycle behavior for those primitives remains a Phase 1 implementation concern.

## Ecosystem Context

The design deliberately combines useful ideas from established quantitative ecosystems without importing their unsuitable trade-offs:

| Ecosystem | Relevant strength | Constraint for QuanTAlib |
| :--- | :--- | :--- |
| TradingView Pine Script | Mature temporal primitives such as `crossover`, `barssince`, and `valuewhen`. | Series history references are expressive, but rolling buffers are not a zero-allocation streaming model. |
| Backtrader and Lean | Operator-based composition, composite indicators, event-driven windows, and reusable indicator graphs. | Their graph and window abstractions must be narrowed to fixed O(1) state for hot-path use here. |
| VectorBT | Fast batch signal post-processing such as entry/exit cleaning, first-signal selection, and holding limits. | Its T x N boolean-mask model is batch-only and cannot define the live rollback contract. |
| NautilusTrader | Event-driven state nodes backed by a systems-language runtime. | Its event/state separation is a useful model; QuanTAlib keeps the execution boundary outside the library. |

The target is declarative algebra over streaming components, not a general-purpose backtester.

## Design Requirements

Every primitive MUST satisfy these requirements:

1. Streaming updates use O(1) state per primitive and do not allocate on the hot path.
2. `IsHot` propagates recursively through composed components.
3. `isNew` updates are deterministic for both confirmed bars and forming-bar ticks.
4. A forming-bar update can be rolled back without leaking state into the next bar.
5. Components can be composed without a custom strategy loop in user code.
6. The output is deterministic for the same input stream and lifecycle sequence.
7. Execution remains outside this library.

## Core Value Types

The C# API should use small value types for hot-path results:

```csharp
public enum Direction : sbyte
{
    Short = -1,
    Flat = 0,
    Long = 1
}

public readonly record struct TPosition(long Time, Direction Direction, bool IsHot);

public enum OrderAction : sbyte
{
        BTO = 2,
        BTC = 1,
        NIL = 0,
        STC = -1,
        STO = -2
}

public readonly record struct TOrder(long Time, OrderAction Action, bool IsHot);

```

`SizeFactor` is normalized to `[0, 1]`. A host may translate it to quantity, notional, or risk budget. A primitive must not assume account size, exposure, margin, or broker semantics.

## Lifecycle Contract

### Confirmed and working state

Each stateful primitive has two logical snapshots:

- **Confirmed state**: state at the last completed bar.
- **Working state**: state after applying the current forming-bar update.

For `isNew == false`, update only the working state. Repeated ticks for the same bar replace the working state rather than advancing confirmed history.

For `isNew == true`:

1. Promote the previous working state to confirmed state.
2. Start the new working state from the promoted confirmed state.
3. Apply the new bar value.

A host may expose an explicit `Commit()` operation if its feed semantics distinguish a final tick from the first tick of the next bar. The implementation must document which convention it uses and test it with repeated intra-bar updates.

### Priming

`IsHot` is false until all required input history is available. A composite is hot only when every child required for its current evaluation is hot.

A strategy state machine MUST remain `Flat` with zero size and no brackets until its complete dependency graph is hot. Warm-up must never produce an entry or exit event.

### Reset

Every stateful primitive MUST support reset to its initial, cold state. Reset clears confirmed and working snapshots, previous values, counters, and cached event results.

## Layer 2: Predicates

Predicates convert indicator values into boolean states or normalized values. They do not create one-shot events.

### Required primitives

| Primitive | Meaning |
| :--- | :--- |
| `LessThan` | Test whether the left value is less than the right value. The right operand may be a scalar or a streaming value. |
| `GreaterThan` | Test whether the left value is greater than the right value. The right operand may be a scalar or a streaming value. |
| `Equal` | Test whether two values are equal within an optional epsilon. The default epsilon is the documented library comparison tolerance. |
| `InsideChannel` | Test whether a value is inside or outside channel bounds. |
| `OutsideChannel` | Test whether a value is above the upper or below the lower bound. |
| `ChannelPosition` | Return normalized position within channel bounds. |
| `IsRising` | Test whether a value is increasing over the requested observation. |
| `IsFalling` | Test whether a value is decreasing over the requested observation. |
| `StrictMonotonic` | Require every adjacent observation to move strictly in one direction. |
| `QuantileRank` | Return the current value's rank within a fixed lookback window. |
| `ZScoreNormalize` | Convert a value into a normalized z-score output. |

`LessThan`, `GreaterThan`, and `Equal` are the canonical comparison functions. Each accepts either a scalar or a streaming value as its right operand, so separate comparison primitives are unnecessary. The scalar overload must use a specialized allocation-free path and must not create a constant child node on every update.

The initial API may expose factory methods instead of operators. Operator syntax is a surface goal, not permission to weaken type safety or introduce hidden allocations. Where the type system permits it, `<` maps to `LessThan`, `>` maps to `GreaterThan`, and `==` may map to `Equal` on a library-owned expression type. `<=` and `>=` are not required public predicates; they remain available internally for trigger boundary definitions.

Example target syntax in C#:

```csharp
var bullish = fast > slow;
var oversold = rsi < 30.0;
var nearMean = Equal(source, mean, epsilon: 1e-8);
var inChannel = InsideChannel(source, lower, upper);
```

Each predicate exposes its current normalized `TValue` through an allocation-free update method. Predicate values are expected to remain in the inclusive range `[0, 1]`; producers must reject or explicitly handle values outside that range rather than silently clamp them. A predicate must not advance a child indicator implicitly; input ownership and update ordering must be explicit. Directional position signals use `TPosition`; order action signals use `TOrder`. A future host-intent layer may add allocation and bracket information.

Mathematical definitions for the normalized predicates are:

```text
Equal:             |A_t - B_t| <= epsilon, where epsilon uses the library default when omitted
InsideChannel:     Lower_t <= A_t <= Upper_t
OutsideChannel:    A_t > Upper_t or A_t < Lower_t
ChannelPosition:   (A_t - Lower_t) / (Upper_t - Lower_t)
IsRising:          A_t > A_(t-k)
IsFalling:         A_t < A_(t-k)
StrictMonotonic:   A_(t-i+1) > A_(t-i), for every i in [1, k]
QuantileRank:      rank(A_t) / N
ZScoreNormalize:   (A_t - mean_t) / standard_deviation_t
```

The default epsilon must be a named, documented library constant. It must not be `double.Epsilon`, which is a machine-level spacing value and is generally unsuitable as a financial comparison tolerance. Callers may override it when the scale or precision of the input requires a different tolerance. `Equal` is a value predicate, not C# object identity or structural equality.

Division by a zero-width channel and a zero-variance z-score must have explicit policies. The default is `IsHot = false` for the normalized result and no event until the denominator becomes valid.

## Layer 3: Temporal Triggers

Triggers convert current and prior predicate or value states into one-shot events.

### Required primitives

| Primitive | Meaning |
| :--- | :--- |
| `CrossOver` | Previous left was less than or equal to right; current left is greater. |
| `CrossUnder` | Previous left was greater than or equal to right; current left is less. |
| `BecameTrue` | Predicate changed from false to true. |
| `BecameFalse` | Predicate changed from true to false. |
| `BarsSince` | Number of confirmed bars since a predicate was true. |
| `ValueWhen` | Capture the latest value when a predicate fires. |
| `OccurredWithin` | Test whether an event occurred within a confirmed window. |
| `CountOccurrences` | Count event firings over a fixed lookback. |
| `HoldFor` | Fire after a predicate remains true for N bars. |
| `PivotExtremum` | Confirm a local high or low after the required right-side bars close. |
| `DivergenceDetector` | Compare structural price and indicator swings for regular or hidden divergence. |

Triggers MUST define equality behavior, cold-start behavior, and whether an event can fire during an unconfirmed tick. The default event policy is:

- Events may be visible while the current bar is forming.
- An unconfirmed event must be reversible.
- A confirmed event may advance strategy state only through the state machine lifecycle contract.

A crossover must use a confirmed previous sample and a working current sample. It must not use a previous intra-bar tick as the prior bar value.

`ValueWhen` must define occurrence numbering (`0` means the most recent event), and `PivotExtremum` must disclose its confirmation delay. A pivot cannot be reported as confirmed until all `right_bars` observations are confirmed; it must never leak future information into a live signal.

## Layer 4: Logic Combinators

Combinators compose predicates and triggers while preserving hot-state and rollback semantics.

### Required primitives

| Primitive | Meaning |
| :--- | :--- |
| `All` / `And` | Every child is true. |
| `Any` / `Or` | At least one child is true. |
| `KOfN` | At least K of N children are true. |
| `RegimeFork` | Select a branch according to a regime predicate. |
| `Inhibit` | Suppress a trigger while a guard is true. |
| `Debounce` | Prevent repeated events during a cooldown window. |
| `FirstSignalOnly` | Emit the first entry event and suppress later entries until an exit resets the gate. |
| `PriorityCascade` | Select the first eligible event by declared priority. |

`&`, `|`, and `!` may be provided for compatible C# predicate types. Trigger composition must be explicit where boolean algebra would hide event semantics. For example, `trigger & filter` means the trigger fires only when the filter is true; it does not turn the trigger into a persistent state.

Combinators MUST:

- Short-circuit only when doing so cannot change child lifecycle state.
- Evaluate child updates in a deterministic declaration order.
- Report `IsHot` as the conjunction of required child readiness.
- Snapshot internal counters and cooldowns for forming-bar rollback.

`RegimeFork` routes intent rather than merely combining booleans. For example, an `ADX > 25` regime may select a momentum branch while the alternate branch handles range conditions. `PriorityCascade` is evaluated in declaration order so risk overrides, such as a volatility shock stop, mask lower-priority profit-taking rules.

The boolean algebra target is:

```csharp
var entry = CrossOver(fast, slow) & (rsi < 30.0) & volumeAboveAverage;
var cleanEntry = FirstSignalOnly(entry, exit);
var protectedEntry = Inhibit(cleanEntry, newsGuard);
```

Python adapters should expose equivalent factories and operator overloads where Python's operand types make the result unambiguous.

## O(1) State Models

The following state budgets are normative for the first implementation:

| Primitive family | Required state |
| :--- | :--- |
| `LessThan`, `GreaterThan`, and `Equal` predicates | No history beyond child values. |
| `CrossOver`, `CrossUnder`, `BecameTrue`, `BecameFalse` | Previous confirmed sample plus current working sample. |
| `BarsSince`, `OccurredWithin`, `HoldFor` | One fixed-width bar counter and one hot flag. |
| `ValueWhen` | One latched value, presence flag, and event occurrence state. |
| `CountOccurrences` | Fixed-size ring buffer and count; the lookback is fixed at construction. |
| `PivotExtremum` | Ring buffer of `left_bars + right_bars + 1` values. |
| `DivergenceDetector` | Fixed-size swing registers and the configured lookback state. |
| `All`, `Any`, `KOfN` | Immutable child references plus scalar aggregate counters. |
| `Debounce` | One cooldown counter and one confirmed/working event state. |
| `FirstSignalOnly` | One gate state reset by the configured exit event. |

No primitive may grow a history collection in response to input. A ring buffer is allowed only when its capacity is fixed during construction and its storage is owned by the component. Batch-only conveniences such as VectorBT-style masks belong in adapters, not in the streaming core.

## Host-Owned Position State Machine

A future host-neutral state machine may consume Layer 3 and Layer 4 outputs and emit a host-facing intent. It must not execute orders.

The state machine owns only logical position state:

- `Flat`, `Long`, or `Short`.
- Entry reference price or host-provided reference.
- Allocation or conviction factor.
- Stop-loss and take-profit intent.
- Confirmed and working snapshots for rollback.

Before priming, it emits a flat, zero-size intent with no brackets:

```csharp
Direction.Flat, 0.0, null, null
```

The host owns order reconciliation, fills, partial fills, rejected orders, realized position, slippage, commissions, and broker communication. The state machine must accept host position feedback rather than assuming every intent was filled.

## C# and Python Surface

C# is the reference implementation for zero-allocation streaming behavior. Python should expose the same conceptual graph for batch and streaming users:

```python
entry = crossover(fast, slow) & (rsi < 30.0)
exit = crossunder(fast, slow)
strategy = strategy_pipeline(entry_long=entry, exit_long=exit, risk_stop=atr_stop)
intent = strategy.update(bar, is_new=True)
```

Python adapters may use native extensions or Python objects at the boundary, but the native streaming core must retain O(1) state and avoid per-update heap allocation. Operator overloads should preserve the same semantics as the C# surface and must not silently evaluate indicators twice.

## Allocation and Performance Rules

- No LINQ, closures, boxing, or per-update collection creation in native hot paths.
- Child components are wired during construction, not discovered during updates.
- Composite child storage is immutable after construction.
- Counters use fixed-width integer fields with documented overflow behavior.
- Optional values use value types or explicit presence flags in hot paths.
- Diagnostics and tracing are opt-in and must not affect signal semantics.

## Testing Requirements

Every primitive requires tests for:

1. Cold, warming, hot, and reset states.
2. Exact equality at threshold and crossover boundaries.
3. Repeated unconfirmed ticks that oscillate around a boundary.
4. Rollback after an intra-bar trigger that is absent at bar close.
5. Confirmed event behavior across the next bar.
6. Composite short-circuit and evaluation ordering.
7. Zero allocation after construction for representative update loops.
8. C# and Python parity for the same deterministic input stream.

Property-based tests should cover arbitrary tick sequences within a bar and compare a rollback-enabled implementation with a reference model that recomputes the current bar from confirmed state.

## Phased Implementation Plan

The implementation should proceed as small, independently verifiable increments. Each phase produces a usable capability and has an explicit exit gate. No phase may introduce order execution, fill simulation, slippage, broker communication, or a general-purpose backtesting loop.

### Phase 0: API and Boundary Review

**Goal:** Freeze the vocabulary and ownership boundaries before adding code.

**Status:** Complete as an API and boundary decision record. No signal primitive implementation is included in Phase 0.

**Deliverables:**

- Signal primitives belong under `QuanTAlib.Signals`; indicator and transform implementations remain in their existing namespaces under `lib/*`.
- Primitives reuse the established indicator vocabulary: `IsHot`, `WarmupPeriod`, `Reset`, `Update`, and `isNew`. They do not inherit `AbstractBase` merely to gain indicator behavior, and they do not advance child indicators implicitly.
- The host updates indicators first; primitives then consume their current values. A primitive's update contract must make input ownership and update ordering explicit.
- `LessThan`, `GreaterThan`, and `Equal` are the canonical comparison predicates. Scalar and streaming right operands use the same semantics; `<` and `>` are optional operator facades on library-owned expression types.
- `Equal` accepts an optional epsilon and uses a documented library default when omitted. `<=` and `>=` remain internal boundary relations for crossover definitions rather than public predicate types.
- Python adapters use the same names and semantics (`less_than`, `greater_than`, `equal`, `is_new`, `is_hot`, and `reset`) without introducing a second execution or state model.

**Exit gate:** API review confirms that every proposed type has one owner, one update contract, no execution responsibility, and no duplicate lifecycle semantics versus the existing indicator infrastructure.

### Phase 1: Lifecycle Kernel

**Goal:** Establish deterministic hot/working/confirmed state behavior using the library's established lifecycle pattern.

**Repository baseline:** the indicator lifecycle exists in `AbstractBase`, with concrete rollback behavior demonstrated by `BiInputIndicatorBase` and `RingBuffer`. A signal-specific lifecycle kernel does not yet exist and must be designed in Phase 1 without duplicating indicator responsibilities.

**Deliverables:**

- Reuse and clarify the internal lifecycle contract with `IsHot`, `Reset`, confirmed state, and working state.
- Snapshot helpers for scalar values, counters, optional values, and event flags, following the same patterns already used in `BiInputIndicatorBase` and `RingBuffer`.
- Explicit behavior for `isNew == true` and `isNew == false` updates that matches the existing `AbstractBase` and ring-buffer rollback conventions.
- Cold-start behavior and warm-up propagation through a component graph, with no entry or exit intent before the full dependency graph is hot.
- Allocation and mutation benchmarks for an empty lifecycle node, measured against the current hot-path conventions of `TValue`, `RingBuffer`, and the existing indicator implementations.

**Tests:**

- Repeated forming-bar updates replace working state without mutating confirmed history.
- New-bar updates promote working state exactly once and preserve the prior confirmed snapshot.
- Reset returns all state to cold.
- A cold graph cannot emit a signal or non-flat intent.
- A comparison against the existing indicator lifecycle tests confirms the signal kernel does not duplicate indicator behavior.

**Exit gate:** Lifecycle tests pass across repeated tick sequences, the kernel remains compatible with the existing `AbstractBase`/`RingBuffer` conventions, and there is no duplicate state machine competing with the repository's current lifecycle model.

### Phase 2: Core Predicates

**Goal:** Implement allocation-free continuous states and normalized metrics.

**Deliverables:**

- `LessThan` and `GreaterThan` with scalar and streaming right-operand overloads.
- `Equal` with an optional non-negative epsilon and a documented default.
- `InsideChannel`, `OutsideChannel`, and `ChannelPosition` with zero-width policy.
- `IsRising`, `IsFalling`, and `StrictMonotonic` using fixed history state.
- `ZScoreNormalize` using a numerically stable fixed-state accumulator.
- `QuantileRank` only when its fixed-capacity storage and update policy are defined.

**Tests:**

- Boundary equality, NaN/infinite inputs, cold values, and zero denominators.
- Predicate parity against straightforward reference calculations.
- No child indicator is updated implicitly.

**Exit gate:** Core predicates are deterministic, hot-state aware, and allocation-free in representative streaming loops.

### Phase 3: Basic Temporal Triggers

**Goal:** Convert continuous states into reversible one-shot events.

**Deliverables:**

- `CrossOver` and `CrossUnder` with confirmed previous samples.
- `BecameTrue` and `BecameFalse`.
- Event result type carrying `Value`, `IsHot`, and event/confirmation status if required by the host.
- Forming-bar rollback for every edge detector.

**Tests:**

- Equality before a cross and equality on the current sample.
- Cross, uncross, recross, and repeated intra-bar oscillation.
- No event during warm-up.
- A forming-bar event disappears when the bar closes without the event.

**Exit gate:** The same input stream produces identical results under scalar replay and tick-by-tick updates.

### Phase 4: Temporal Memory and Structural Events

**Goal:** Support event sequencing and delayed structural confirmation.

**Deliverables:**

- `BarsSince`, `OccurredWithin`, `CountOccurrences`, and `HoldFor`.
- `ValueWhen` with occurrence indexing and presence state.
- `PivotExtremum` with explicit right-bar confirmation delay.
- `DivergenceDetector` only after swing representation and lookback memory are specified.

**Tests:**

- Counter behavior on event, non-event, reset, and overflow boundaries.
- Value latching on the exact event bar.
- Pivot confirmation never uses unconfirmed future data.
- Structural triggers roll back pending candidates correctly.

**Exit gate:** Every memory primitive has a fixed state budget, documented confirmation timing, and rollback coverage.

### Phase 5: Boolean Gates and Signal Hygiene

**Goal:** Compose conditions and events without semantic ambiguity.

**Deliverables:**

- `All` / `And`, `Any` / `Or`, and `KOfN`.
- `Inhibit` and `Debounce`.
- `FirstSignalOnly` with an exit-controlled reset.
- Deterministic child ordering and lifecycle propagation.
- Operator syntax where the type system can preserve predicate-versus-trigger meaning.

**Tests:**

- Empty and singleton composites, invalid K values, and short-circuit behavior.
- Child evaluation order and no duplicate child updates.
- Cooldown rollback and first-entry gate rollback.
- Composite hot-state behavior with mixed warm-up periods.

**Exit gate:** Compound graphs behave identically to a reference evaluator and remain zero-allocation after construction.

### Phase 6: Regime Routing and Priority

**Goal:** Add contextual branch selection and conflict resolution.

**Deliverables:**

- `RegimeFork` for selecting one active branch.
- `PriorityCascade` for ordered risk and signal overrides.
- Explicit behavior when multiple branches are hot, cold, or simultaneously eligible.
- Diagnostics that are opt-in and do not mutate signal state.

**Tests:**

- Regime transitions on confirmed and forming bars.
- Branch state isolation and reset behavior.
- Priority masking and deterministic tie handling.
- No evaluation of inactive branches when lifecycle semantics permit skipping.

**Exit gate:** Regime and priority behavior is deterministic under replay and does not create hidden execution semantics.

### Phase 7: Strategy Intent Boundary

**Goal:** Produce host-consumable target exposure without placing trades.

**Deliverables:**

- Host-neutral `Direction` and host-intent output.
- A position state machine for `Flat`, `Long`, and `Short` transitions.
- Priming gate that emits flat, zero-size intent until the full graph is hot.
- Pluggable stop and take-profit intent providers, including ATR-based examples.
- Host position feedback contract for fills, partial fills, rejects, and external position changes.

**Tests:**

- Entry and exit transitions, reversal policy, and repeated identical intents.
- Intra-bar entry rollback and confirmed-bar promotion.
- Bracket recalculation without execution side effects.
- Intent stability when host-reported position differs from requested intent.

**Exit gate:** The state machine emits only deterministic intent values and has no dependency on a broker, order, fill, or account API.

### Phase 8: Python Parity

**Goal:** Expose the same semantics to Python users without weakening the streaming core.

**Deliverables:**

- Python factories and operators for the supported primitive subset.
- Consistent naming, hot-state, reset, and `is_new` semantics.
- Native-backed streaming adapter where performance requires it.
- Batch adapter that reuses the same reference semantics rather than defining a second behavior.

**Tests:**

- C# and Python golden-stream parity.
- Same results for bar-only and forming-tick sequences.
- Python operator precedence and invalid-composition errors.
- Allocation and throughput benchmarks for native and adapter paths.

**Exit gate:** Cross-language parity tests pass for all released primitives and differences are documented where language syntax requires them.

### Phase 9: Hardening and Release Readiness

**Goal:** Make the primitive layer safe to evolve and suitable for production hosts.

**Deliverables:**

- Full property-based tests for arbitrary intra-bar tick sequences.
- Allocation regression tests and benchmark baselines.
- Documentation examples for Quantower and Lean-style hosts.
- Versioning and compatibility notes for public interfaces and operator behavior.
- Review of analyzer, security, NativeAOT, and multi-target build behavior.

**Exit gate:** Full solution validation passes, performance budgets are recorded, and the public API has documented compatibility guarantees.

Stateful signal primitives should not proceed beyond their API scaffolding until the Phase 1 lifecycle tests are complete. This staged approach keeps the primitive layer composable without turning QuanTAlib into an execution engine or bloated backtester.
