# Strategy Primitives Specification

Status: Proposed (supersedes the 2026-09-23 draft)

Scope: governs four new categories, `lib/signals/`, `lib/regimes/`, `lib/stops/` and `lib/sizing/`, plus the supporting measures they add to existing categories. This file lives in `lib/signals/` because the signal contract is the one the other three build on.

## 1. Purpose

QuanTAlib computes indicators. This layer turns indicator values into trading intent: conditions, one-shot events, market regimes, protective levels and normalized size factors. It sits above the indicators and below a host execution platform such as Quantower, QuantConnect Lean or a custom engine.

The boundary is **intent, not fills**. The library emits levels, conditions and size factors. The host owns orders, fills, intrabar ordering, gaps, equity, margin, lot rounding and position reconciliation.

```text
Layer 1  Indicators and transforms         (existing lib/* categories)
             |                              TValue
             v
Layer 2  Predicates        signals/predicates   TValue in [0, 1]  (state)
Layer 3  Triggers          signals/triggers     TValue in {0, 1}  (event)
Layer 4  Combinators       signals/combinators  TValue in [0, 1]
         Guards            signals/guards       TValue in {0, 1}  (inhibit)
             |
             +---- Regimes    regimes/          TValue state code + properties
             |
             v
Layer 5  Position state machine (host-owned, library-assisted)   TPosition, TOrder
             |          ^
             |          | Arm(TTradeContext) at entry fill
             v          |
         Stops         stops/                   TValue stop level + properties
         Sizing        sizing/                  TValue size factor + properties
             |
             v
         Strategy intent -> host executes
```

## 2. Scope and Boundary

### 2.1 In scope

| Area | Category | Primary output |
| :--- | :--- | :--- |
| Composing entry and exit conditions from indicators | `signals/` | `TValue` in `[0, 1]` |
| Classifying market regime (trend, range, compression, expansion) | `regimes/` | `TValue` state code, with `Trend`, `Vol`, `Confidence`, `BarsInState` properties |
| Levels and exits tied to a position: invalidation, trailing, targets, time | `stops/` | `TValue` stop level, with `Target`, `Flags`, `Reason` properties |
| Account-independent sizing: risk per unit, volatility scalars, pure sizing functions with host-supplied account inputs | `sizing/` | `TValue` size factor, or `double` from static functions |

### 2.2 Out of scope for this phase

| Item | Why it leaks across the boundary | Phase |
| :--- | :--- | :--- |
| Fill price, slippage, commissions, intrabar ordering, gap fills | Execution | Backtesting |
| Deciding that a stop "was hit" as a fact (the library emits touch flags only) | Execution | Backtesting |
| Equity, margin, buying power, lot rounding, contract specs as owned state | Account | Host inputs only |
| Kelly, optimal f, fixed ratio (Jones), drawdown-based scaling | Needs trade or equity history | Backtesting / portfolio |
| Max portfolio drawdown, max intraday loss, max consecutive loss days | Account state | Portfolio |
| Cross-instrument guards, sector exposure, correlation caps, Turtle correlated-unit limits | Multi-asset | Portfolio |
| Order types (bracket, OCO, MIT, simulated stops), ATM templates | Execution | Host |
| Market scanning over universes | Excluded by decision | Later |
| Inferring position from entry signals | State ownership | Host (Layer 5) |

### 2.3 Admission test

A class ships in this phase only if it can be computed from:

1. the bar or value stream,
2. a host-supplied `TTradeContext` (for `stops/` and `sizing/`),
3. constant construction parameters, and
4. explicit host-supplied scalars passed per call (for account-dependent sizing functions).

Anything that needs equity history, fills, other instruments or closed-trade outcomes does not ship in this phase.

## 3. Repository Baseline and Non-Duplication Rules

This layer builds on existing contracts and must not redefine them:

| Existing asset | Location | Role for this layer |
| :--- | :--- | :--- |
| `AbstractBase` | `lib/core/AbstractBase.cs` | Lifecycle for single-input nodes: `Name`, `WarmupPeriod`, `IsHot`, `Prime`, `Update(TValue, isNew)`, `Reset`, `Pub` |
| `BiInputIndicatorBase` | `lib/core/BiInputIndicatorBase.cs` | Lifecycle and rollback for two-input nodes (`Above(a, b)`, `CrossOver(a, b)`, `Add`/`Sub`/`Mul`/`Div` in `numerics/`); used with `period = 1` for stateless per-bar nodes; extended with `Subscribe(a, b)` for the §8.5 time-join rule on event-chained construction |
| `RingBuffer`, `MonotonicDeque` | `lib/core/ringbuffer/`, `lib/core/collections/` | Fixed-capacity windows and O(1) amortized rolling extremes |
| `TValue`, `TBar`, `TSeries`, `TBarSeries`, `ITValuePublisher` | `lib/core/types/` | The one stream type and its chaining contract |
| `TPosition`, `TOrder`, `Direction`, `OrderAction` | `lib/core/types/` | Layer 5 outputs; kept as they are |
| `SignalBase` | `lib/core/SignalBase.cs` | Layer 5 base publishing `TPosition` (see Section 16) |
| `TTradeContext`, `BracketFlags`, `ExitReason` | `lib/core/types/ttradecontext/` | Host-supplied trade description and stop-touch/exit-reason enums (Section 8.2), shipped in Phase 0 |
| `TrendState`, `VolState` | `lib/core/types/regimestate/` | Orthogonal regime-classifier axes (Section 8.2), shipped in Phase 0 ahead of any `regimes/` classifier |
| `SignalConstants`, `OutOfRangePolicy` | `lib/core/SignalConstants.cs` | `DefaultEpsilon`, `TruthThreshold`, and the cold/saturate out-of-range policy (Section 8.1), shipped in Phase 0 |
| `ConfirmOnClose` | `lib/core/ConfirmOnClose.cs` | Bar-confirmation wrapper (Section 8.6), shipped in Phase 0 |
| `ComparePredicateBase<TOp>`, `AboveOp`/`BelowOp`/`EqualOp` | `lib/core/CompareOp.cs` | Generic comparison core (Section 8.8), shipped in Phase 1 with `Above`/`Below`/`Equal` |
| `MultiInputBase` | `lib/core/MultiInputBase.cs` | Fixed-arity (2–8) multi-input base (Section 8.3), shipped in Phase 1 with the 3-input channel predicates |

Existing indicators that already cover parts of this scope stay where they are:

| Need | Existing coverage | Rule |
| :--- | :--- | :--- |
| Trailing and stop-and-reverse levels | `reversals/`: `Chandelier`, `Ckstop`, `Atrstop`, `Vstop`, `Sar`, `Sarext`; `dynamics/Super` | Stay as level indicators that don't know about the position. `stops/LevelTrail` adapts them; no algorithm is rewritten |
| Swing and pivot structure | `reversals/`: `Swings`, `Fractals`, `Pivot*` | Inputs to `PivotExtremum` and `StructuralStop` |
| Trend strength | `dynamics/`: `Adx`, `Dx`, `PlusDi`, `MinusDi`, `Aroon`, `Vhf`, `Vortex`, `HtTrendmode`, `TtmTrend`; `oscillators/Er`; `statistics/LinReg` (`Slope`, `RSquared`) | Inputs to `regimes/` classifiers |
| Range and chop | `dynamics/Chop`, `dynamics/Vhf`, `statistics/Adf`, `statistics/Hurst` | Inputs to `regimes/` classifiers |
| Compression | `dynamics/TtmSqueeze` (`SqueezeOn`, `SqueezeFired`), `volatility/Bbw`, `Bbwn`, `Bbwp` | Inputs to `SqueezeRegime` and `SqueezeRelease` |
| Volatility level | `volatility/`: `Atr`, `Natr`, `Rv`, `Hv`, `Yzv`, `Gkv`, `Rsv`, `Jvolty` | Inputs to `VolatilityRegime`, `VolTargetScalar`, `InitialStop` |
| Z-score | `statistics/Zscore` | Feeds `Above` / `Below` directly; not rewritten |

Continuous measures belong in existing categories. `regimes/` holds only classifiers that emit categories.

## 4. Prior Art

Every surveyed platform separates indicator math from order and account logic. QuanTAlib copies the math and leaves the engine alone.

| Platform | What QuanTAlib takes | What stays with the host |
| :--- | :--- | :--- |
| TradingView Pine v6 | `ta.crossover`, `ta.crossunder`, `ta.barssince`, `ta.valuewhen`, `ta.rising`; `strategy.exit` level semantics (`profit`, `loss`, `trail_points`, `trail_offset`) | `strategy.*` broker emulator, `strategy.risk.*` |
| QuantConnect Lean | Insight shape (direction, magnitude, confidence); trailing-from-peak math of `TrailingStopRiskManagementModel` | Portfolio construction, risk models reading holdings, execution |
| vectorbt / PRO | `clean_entries_exits`, first-signal selection; OHLC stop exit generators (`sl_stop`, `tp_stop`, `sl_trail`, `stop_ladder`) as pure functions | Portfolio simulator |
| Amibroker | `ApplyStop` types (loss, profit, trailing, N-bar), `ActivationFloor`, `SetStopPrecedence` ordering | Backtester, `SetPositionSize` against equity |
| EasyLanguage / MultiCharts | `SetBreakeven`, `SetPercentTrailing`, `SetDollarTrailing` level semantics | Intrabar stop evaluation |
| NinjaTrader 8 | `CalculationMode` vocabulary (price, ticks, percent) | ATM strategies, live order management |
| NautilusTrader | Event and state separation; trailing activation price | Order emulator, `RiskEngine` |
| Freqtrade | Per-instrument `CooldownPeriod`, `StoplossGuard` semantics | Cross-pair protections, stake management |
| Skender (FacioQuo) | Stop-and-reverse indicators as ordinary levels | n/a (no signal layer) |

No .NET library surveyed ships a zero-allocation, rollback-aware streaming signal algebra. That is the gap this layer fills.

## 5. Design Requirements

Every primitive MUST satisfy these:

1. Streaming updates use O(1) state, or a fixed-capacity buffer allocated at construction, and do not allocate on the hot path.
2. `IsHot` propagates recursively: a node is hot only when every input it needs is hot.
3. A cold node publishes `NaN`. It never publishes a truth value, a non-flat intent, an armed stop or a non-zero size factor. `NaN` (rather than `0.0`) is required so that `Not` of a cold input stays cold instead of becoming true.
4. `isNew` semantics are deterministic for confirmed bars and forming-bar ticks, and match `AbstractBase`.
5. A forming-bar update can be rolled back without leaking state into the next bar.
6. Multi-input nodes evaluate only on time-aligned inputs (Section 8.4).
7. Output is deterministic for the same input stream and lifecycle sequence.
8. Execution remains outside the library.
9. NaN and Infinity inputs follow the library's last-valid substitution rule. A node whose required denominator is invalid reports `IsHot = false` until it becomes valid.

## 6. Taxonomy

```text
lib/
  core/types/        + ttradecontext, + enums (TrendState, VolState, BracketFlags, ExitReason)
  core/              + MultiInputBase, RegimeBase, StopBase, SizerBase
  dynamics/          (unchanged; supplies inputs)
  statistics/        + pctrank, varratio, cusum, pagehinkley, bocpd
  volatility/        + garch11
  reversals/         (unchanged level indicators) + mstructure
  signals/
    predicates/      Above, Below, Equal, InsideChannel, ...
    triggers/        CrossOver, BecameTrue, BarsSince, Sequence, ...
    combinators/     AllOf, AnyOf, KOfN, Latch, Inhibit, ...
    guards/          Cooldown, SessionFilter, VolatilityShockGuard, ...
  regimes/           TrendRegime, VolatilityRegime, SqueezeRegime, ...
  stops/             InitialStop, EntryTrail, ActivationTrail, ProfitTarget, TimeStop, ...
  sizing/            RiskPerUnit, VolTargetScalar, FixedFractionalSize, ExposureCap, ...
```

Reasons:

1. **One stream type.** Every node publishes `TValue` through `ITValuePublisher`. Signals, regimes, stops and sizers therefore chain into existing indicators, `TSeries` batch paths, Quantower plotting and the Python NativeAOT bridge with no new publisher interfaces. Rich per-bar detail (regime axes, target level, touch flags) is exposed as properties on the node, the same way `Bbands` exposes `Upper` and `Lower`.
2. **Continuous measures stay in existing categories; only classifiers go in `regimes/`.** A variance ratio is as general as RSI. The classifier consumes it. This avoids pairs like "ADX" and "ADX regime" living in two places.
3. **`signals/` is the one two-level folder.** The four sub-layers share one value convention and are looked for as a group. Flattening would scatter about 40 small classes across the top level.
4. **"Risk control" and "trade stops" are one category.** An invalidation stop, a trailing stop, a target and a time stop are all functions of `(bar stream, TTradeContext)` that output a level or an exit condition. They differ only in when they arm and whether they ratchet. Splitting them would duplicate the context plumbing and the precedence logic.
5. **Divergence is a trigger, not a regime.** It is defined by two dated pivots and fires once, when the second pivot is confirmed. A "divergence regime" is `OccurredWithin(DivergenceDetector, n)`, a composition.
6. **`sizing/` holds only the account-independent part.** Functions that need equity take it as an argument and keep no state between calls.

Each class follows the existing per-indicator layout: `lib/<category>/<name>/<Name>.cs`, `<Name>.md`, `tests/<Name>.Tests.cs`, `tests/<Name>.Validation.Tests.cs`, and a Quantower adapter where a chart representation makes sense.

## 7. Vocabulary

One name per concept. No aliases: every alias doubles the surface of the Python wrapper and the Quantower adapters.

| Concept | Name | Python | Reason |
| :--- | :--- | :--- | :--- |
| a > b | `Above` | `above` | Trader vocabulary; pairs with `CrossOver`; matches vectorbt `close_above` |
| a < b | `Below` | `below` | Same |
| \|a − b\| ≤ ε | `Equal` | `equal` | Only numeric-sounding name; exact float equality is never used |
| inside [lower, upper] | `InsideChannel` | `inside_channel` | Symmetric pair |
| outside bands | `OutsideChannel` | `outside_channel` | Symmetric pair |
| crossing up / down | `CrossOver` / `CrossUnder` | `cross_over` / `cross_under` | Pine `ta.crossover` / `ta.crossunder` |
| AND / OR / NOR / threshold | `AllOf` / `AnyOf` / `NoneOf` / `KOfN` | `all_of` / `any_of` / `none_of` / `k_of_n` | `All` / `Any` read as LINQ extension methods in any file that imports `System.Linq` |
| bars since, value when | `BarsSince`, `ValueWhen` | `bars_since`, `value_when` | Pine parity |

This replaces `LessThan`, `GreaterThan`, `All` / `And` and `Any` / `Or` from the previous draft. `<=` and `>=` remain internal boundary relations for crossover definitions, not public predicates.

Namespace: `QuanTAlib`, consistent with every existing indicator and with the types in `lib/core/types/`. The previous draft's `QuanTAlib.Signals` namespace is dropped.

## 8. Core Contracts

### 8.1 Value semantics

Signal nodes publish `TValue`. The value range is part of each node's contract:

| Kind | Value while hot | Value while cold |
| :--- | :--- | :--- |
| Crisp predicate, trigger, guard | exactly `0.0` or `1.0` | `NaN` |
| Graded predicate (`ChannelPosition`, `QuantileRank`) | inclusive `[0, 1]` | `NaN` |
| Combinator | inclusive `[0, 1]`; crisp when every input is crisp | `NaN` |
| Counter trigger (`BarsSince`, `CountOccurrences`) | non-negative count (documented exception to the range rule) | `NaN` |
| `ValueWhen` | the captured source value (documented exception) | `NaN` until the first occurrence |

Range rule: a node that expects `[0, 1]` input must reject or explicitly handle values outside that range; it never clamps silently. Every graded predicate takes an `OutOfRange` policy parameter with two values:

- `Cold` (default): publish `NaN` and report `IsHot = false` for that bar.
- `Saturate`: clamp to `[0, 1]`, as an explicit, documented choice made by the caller.

Truth rule: where a node needs a boolean from its input (triggers, `Latch`, `KOfN`, guards), an input counts as true when `value >= TruthThreshold`. The default is `0.5`; it is a named library constant and a constructor parameter. For crisp inputs the threshold never matters.

Combinator algebra on `[0, 1]` uses the Zadeh operators, which reduce to boolean AND, OR and NOT on crisp inputs:

```text
AllOf(a, b, ...)  = min(a, b, ...)
AnyOf(a, b, ...)  = max(a, b, ...)
NoneOf(a, b, ...) = 1 - max(a, b, ...)
Not(a)            = 1 - a
KOfN(k, a, ...)   = 1 if count(x >= TruthThreshold) >= k else 0
WeightedVote(w, θ, a, ...) = 1 if sum(w_i * a_i) >= θ else 0
```

Any `NaN` input makes a combinator's output `NaN` for that bar. Short-circuit evaluation must not hide a cold input.

### 8.2 Types

New types live in `lib/core/types/`. All are value types with no reference fields.

```csharp
// Existing, unchanged
public readonly record struct TPosition(long Time, Direction Direction, bool IsHot);
public readonly record struct TOrder(long Time, OrderAction Action, bool IsHot);

// New: host-supplied description of the open trade (an input, not a stream)
public readonly record struct TTradeContext(
    long EntryTime,
    double EntryPrice,
    Direction Side,
    double InitialStop,   // defines 1R = |EntryPrice - InitialStop|
    double PointValue,
    double TickSize);

// New enums used by node properties
public enum TrendState : sbyte { Down = -1, Unknown = 0, Up = 1, Range = 2 }
public enum VolState   : sbyte { Unknown = 0, Compression = 1, Normal = 2, Expansion = 3 }

[Flags]
public enum BracketFlags : byte
{
    None = 0,
    StopTouched = 1,     // bar range reached the previous Stop
    TargetTouched = 2,   // bar range reached the previous Target
    BothTouched = 3,     // both inside one bar; intrabar order unknown
    GapThrough = 4,      // open already beyond Stop or Target
    EntryBar = 8,        // touch occurred on the entry bar
}

public enum ExitReason : byte
{
    None = 0, InitialStop, Structural, Trail, Breakeven, Target, Time, Stale, Session,
}
```

Why two regime enums instead of one: trend and volatility are orthogonal. A market can trend while volatility compresses, and the squeeze-release setup is exactly Range + Compression turning into Trend + Expansion. A regime node exposes both axes so a consumer can gate on either.

### 8.3 Base classes

| Base | Derives from | Inputs | `Last` publishes | Rules |
| :--- | :--- | :--- | :--- | :--- |
| (single-input nodes) | `AbstractBase` | one `TValue` | per Section 8.1 | Existing contract; used by `IsRising`, `BecameTrue`, `BarsSince`, `HoldFor`, `Not`, `Debounce` |
| (two-input nodes) | `BiInputIndicatorBase` (period = 1) | two `TValue` | per Section 8.1 | Existing rollback pattern; used by `Above`, `Below`, `Equal`, `CrossOver`, `CrossUnder`, `Latch`, `Inhibit`, and by `numerics/Add`/`Sub`/`Mul`/`Div`. `Subscribe(a, b)` wires two publishers under the §8.5 time-join rule. `ComputeError` implementations MUST always return a finite value: a non-finite result permanently poisons the base's incrementally maintained running sum, even though the underlying single-slot buffer self-corrects on the next bar (see `numerics/Div`'s stateless zero-denominator policy for the pattern to follow) |
| `MultiInputBase` | `ITValuePublisher`, `IDisposable` | N `TValue`, fixed at construction | per Section 8.1 | Time-join rule; deterministic declaration order; used by `InsideChannel`, `AllOf`, `AnyOf`, `KOfN`, `Sequence` |
| `RegimeBase` | `ITValuePublisher`, `IDisposable` | values or bars | `(double)Trend` (or `(double)Vol` for volatility-only classifiers) | Properties `Trend`, `Vol`, `Score`, `Confidence`, `BarsInState`; enter/exit thresholds; minimum dwell; `Unknown` and `NaN` while cold |
| `StopBase` | `ITValuePublisher`, `IDisposable` | `TBar`, plus `Arm(TTradeContext)` and `Disarm()` | stop level valid for the next bar | Properties `Target`, `TrailActivation`, `StopActive`, `Flags`, `Reason`; `NaN` while disarmed |
| `SizerBase` | `ITValuePublisher`, `IDisposable` | values, optional stop publisher | size factor in `[0, 1]` | Properties `RiskPerUnit`, `VolScalar`; static pure `Compute(...)` beside the streaming wrapper |
| `SignalBase` (existing) | `ISignalPublisher`, `IDisposable` | signals, stops, sizers, host feedback | `TPosition` | Layer 5 only; see Sections 14 and 16 |

Every base provides `Name`, `WarmupPeriod`, `IsHot`, `Last`, `Update(..., bool isNew = true)`, `Reset()`, `Pub` and `Dispose`, mirroring `AbstractBase`. `WarmupPeriod` = max(input warmups) + own lookback. No node advances a child indicator implicitly: the host (or event chaining) updates inputs first.

### 8.4 Confirmed and working state

Each stateful node has two snapshots:

- **Confirmed state**: state at the last completed bar.
- **Working state**: state after the current forming-bar update.

For `isNew == false`, restore working state from confirmed state and recompute. Repeated ticks for the same bar replace the working state; they never advance confirmed history.

For `isNew == true`:

1. Promote the previous working state to confirmed state.
2. Start the new working state from the promoted confirmed state.
3. Apply the new bar value.

This is the `_s` / `_ps` pattern used by every existing indicator, including the local-copy optimization in AGENTS.md §2.5.

Edge detectors compare the working current sample against the **previous confirmed** sample, never the previous working tick. Otherwise intrabar oscillation around a boundary fires repeated edges.

### 8.5 Time-join rule

Chained publishers fire one at a time. A multi-input node subscribed to `a` and `b` evaluates only after every input has published for the same `Time`. Otherwise `AllOf(a, b)` briefly mixes bar t of `a` with bar t−1 of `b`.

- Implementation: a fixed `ulong` bitmask of inputs seen for the current `Time` (up to 64 inputs), cleared when a newer `Time` arrives.
- An input publishing an older `Time` than the pending one is a host ordering error and throws in debug builds.
- `BiInputIndicatorBase` nodes adopt the same rule when event-driven.
- Pull overloads (`Update(TValue a, TValue b, bool isNew)`) serve hosts that drive nodes directly and already guarantee alignment.

### 8.6 Confirmed emission

The library cannot know a bar is final until the next `isNew == true` arrives. `ConfirmOnClose` wraps any node and re-emits the last working value of bar t, marked final, when bar t+1 opens. It is the equivalent of Pine's `barstate.isconfirmed` gating.

- Hosts that alert intrabar use the raw node.
- Hosts that act at bar close use the wrapper.
- Documentation for every trigger states which one its examples use.

`ConfirmOnClose` and the time-join rule are part of the Phase 0 contract, not optional utilities. Every correctness property downstream depends on them.

### 8.7 Batch paths

- Stateless predicates provide `static Batch(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> output)` with SIMD compare-and-select, writing `0.0` / `1.0` / `NaN`.
- Stateful nodes provide a scalar `Batch` loop that must match streaming bit for bit.
- `TSeries` overloads follow the existing `Batch` / `Calculate` conventions.

### 8.8 C# API style

Layered, from canonical to convenience:

1. **Sealed classes** as the canonical API: `new CrossOver(fast, slow)`. Matches every existing indicator.
2. **Generic comparison core** using static abstract interface members: `Compare<TOp> where TOp : struct, ICompareOp` with `static abstract bool Eval(double a, double b, double eps)`. `Above`, `Below` and `Equal` are thin sealed classes over one implementation; the JIT specializes each with no virtual dispatch.
3. **Fluent layer**: `fast.CrossesAbove(slow).And(rsi.Below(30))`, as extension methods on `ITValuePublisher`. It allocates at construction only.
4. **Operator overloads** on a `SignalExpr` wrapper (`ema > sma` builds an `Above` node; `&`, `|`, `!` build combinators). This is syntax sugar and is documented as such. `trigger & filter` means the trigger fires only while the filter is true; it does not turn the trigger into a state.
5. **Source generators**, not reflection, for Python and Quantower bindings, consistent with NativeAOT.

Fixed-arity overloads (`AllOf(a, b)` up to 8 inputs) avoid params-array allocation; beyond that, an input array allocated once at construction is acceptable.

Scalar right operands (`rsi.Below(30.0)`) use a dedicated single-input path and never create a constant child node.

## 9. Catalog: `signals/`

State notation: **O(1)** constant scalars; **Ring(n)** fixed ring buffer allocated at construction; **Deque(n)** monotonic deque for rolling extremes.

### 9.1 Predicates (Layer 2)

| Class | Inputs → value | State | Priority | Notes |
| :--- | :--- | :--- | :--- | :--- |
| `Above`, `Below` | value vs. value or scalar → crisp | O(1) | P0 | Optional `Hysteresis(enter, exit)` band removes boundary flicker |
| `Equal` | a, b, ε → crisp | O(1) | P1 | ε is a named library constant; never `double.Epsilon` |
| `InsideChannel`, `OutsideChannel` | value, lower, upper → crisp | O(1) | P0 | Works with every existing channel |
| `ChannelPosition` | value, lower, upper → graded | O(1) | P0 | `(v − lower) / (upper − lower)`; zero width → cold; outside the channel follows `OutOfRange` |
| `IsRising`, `IsFalling` | value, n → crisp | Ring(n) | P0 | Pine `ta.rising` / `ta.falling`: strictly for n bars |
| `AtHighest`, `AtLowest` | value, n → crisp | Deque(n) | P0 | Reuses `MonotonicDeque` |
| `PercentDistance` | a, b → raw ratio | O(1) | P1 | `(a − b) / b`; documented exception to the range rule; feed into `Above` / `Below` |
| `QuantileRank` | value, n → graded | Ring(n) | P1 | Built on `statistics/pctrank` |

`StrictMonotonic` from the previous draft is dropped: it is `IsRising` / `IsFalling` with the strict definition. `ZScoreNormalize` is dropped: `statistics/Zscore` already exists and feeds `Above` / `Below` directly.

### 9.2 Triggers (Layer 3)

| Class | Semantics | State | Priority | Notes |
| :--- | :--- | :--- | :--- | :--- |
| `CrossOver`, `CrossUnder` | `a[t] > b[t]` and `a[t−1] ≤ b[t−1]` (confirmed previous) | O(1) | P0 | Pine parity |
| `CrossOverThreshold`, `CrossUnderThreshold` | must exceed `b + δ` after being below `b − δ` | O(1) | P0 | Dead-band crossing; kills whipsaw at the line |
| `BecameTrue`, `BecameFalse` | rising / falling edge through `TruthThreshold` | O(1) | P0 | Generic edge detection |
| `BarsSince` | confirmed bars since the input was last true | O(1) | P0 | Pine `ta.barssince`; `NaN` until first occurrence |
| `ValueWhen` | source value captured at the k-th most recent true (k = 0 most recent) | Ring(k+1) | P0 | Pine `ta.valuewhen`; feeds structural stops |
| `OccurredWithin` | true at least once in the last n bars | O(1) | P0 | Built on the `BarsSince` counter |
| `CountOccurrences` | count of true in the last n | Ring(n) bits | P1 | Frequency filters |
| `HoldFor` | true for n consecutive bars | O(1) | P0 | Confirmation |
| `Sequence` | A then B within n bars (optionally then C within m) | O(1) per step | P0 | The most requested composition beyond AND / OR |
| `Breakout` | value crosses the prior n-bar high / low, excluding the current bar | Deque(n) | P0 | Donchian entry; exclusion avoids comparing a bar with itself |
| `ReEnterChannel` | was outside, now inside | O(1) | P1 | Band re-entry for mean reversion |
| `SqueezeRelease` | volatility regime leaves `Compression` (or `TtmSqueeze.SqueezeFired`) | O(1) | P1 | Links regimes to triggers |
| `PivotExtremum` | confirmed swing high / low, **stamped at the confirmation bar**; pivot bar offset exposed as a property | Ring(L+R+1) | P0 | No lookahead by construction |
| `DivergenceDetector` | price pivot vs. oscillator pivot; regular / hidden, bullish / bearish (property) | Ring(last k pivots) | P1 | Fires at the second pivot's confirmation bar |

Every trigger documents equality behavior, cold-start behavior, and whether it can fire on an unconfirmed tick. Default policy: events may be visible on the forming bar, are reversible until confirmed, and advance downstream state only through the lifecycle contract.

### 9.3 Combinators (Layer 4)

| Class | Semantics (Section 8.1 algebra) | State | Priority |
| :--- | :--- | :--- | :--- |
| `AllOf`, `AnyOf`, `NoneOf` | min / max / 1 − max, with the time-join rule | O(1) | P0 |
| `KOfN` | at least k of n true | O(1) | P0 |
| `Not` | 1 − a | O(1) | P0 |
| `Latch` | set on A, reset on B; reset dominates on the same bar | O(1) | P0 |
| `Inhibit` | A unless B | O(1) | P0 |
| `FirstSignalOnly` | first entry passes; later entries suppressed until the paired exit | O(1) | P0 |
| `CleanEntryExit` | pairs entry and exit streams; both on one bar → neither (vectorbt rule) or a configured priority | O(1) | P0 |
| `Debounce` | suppress re-fires within n bars | O(1) | P1 |
| `RegimeFork` | pass input X while the regime is R1, Y while R2 | O(1) | P1 |
| `PriorityCascade` | first true input wins; winning index exposed as a property | O(1) | P1 |
| `WeightedVote` | Σ wᵢ·aᵢ ≥ θ | O(1) | P2 |

Combinators evaluate children in declaration order, short-circuit only when that cannot change child lifecycle state or hide a cold input, and snapshot counters and gates for rollback. Prefer `KOfN` over long `AllOf` chains: each extra condition adds parameters, and a long AND rarely fires, which is how rule sets overfit.

### 9.4 Guards

Guards are crisp nodes meant to feed `Inhibit`. Per instrument only.

| Class | Inputs | State | Priority | Boundary |
| :--- | :--- | :--- | :--- | :--- |
| `Cooldown` | exit event (from `stops/` or host), n bars | O(1) | P0 | In |
| `SessionFilter` | bar time, session spec, time zone | O(1) | P1 | In; calendars are host configuration |
| `VolatilityShockGuard` | TR > k × ATR, or volatility percentile > p | O(1) | P1 | In |
| `GapGuard` | \|open − previous close\| > k × ATR | O(1) | P2 | In |
| `StopOutGuard` | host feeds `TradeClosed(reason, R)`; blocks after k stop-outs in n bars | Ring(k) | P2 | In for one instrument only; cross-instrument is portfolio |
| `MaxDrawdownGuard` | needs equity | n/a | Deferred | Out: account state |

## 10. Catalog: `regimes/`

### 10.1 Classifier rules

Every classifier has three flicker controls:

1. separate enter and exit thresholds (Schmitt trigger), for example enter trend at ADX 25 and leave at 20,
2. a minimum dwell of n confirmed bars before any state change,
3. `Unknown` and `NaN` while cold.

`BarsInState` is always populated so consumers can require regime maturity. Flicker is a tested metric: state changes per 100 bars on GBM data must stay under a documented bound for default parameters.

Regime nodes feed `signals/` through a membership predicate, `InRegime(regime, TrendState.Up)`, which publishes crisp 0/1. `RegimeFork` uses the same test.

### 10.2 Classes

| Class | Backing measure (existing unless marked new) | Axis | State | Priority |
| :--- | :--- | :--- | :--- | :--- |
| `TrendRegime` | `Adx` level + DI sign, or `LinReg.Slope` sign + `RSquared` | Trend | O(1) | P0 |
| `EfficiencyRegime` | `oscillators/Er` + net-change sign | Trend | Ring(n) | P0 |
| `VolatilityRegime` | percentile rank of `Atr` / `Natr` / `Rv` / `Yzv` (new `statistics/pctrank`) | Vol | Ring(n) | P0 |
| `SqueezeRegime` | `TtmSqueeze.SqueezeOn`, or `Bbwp` percentile | Vol (Compression) | O(1) | P0 |
| `InRegime` | any regime node + target state | predicate | O(1) | P0 |
| `ChopRegime` | `Chop` or `Vhf` | Trend (Range vs. Up / Down) | as source | P1 |
| `CycleTrendRegime` | `HtTrendmode` | Trend vs. Range | as source | P1 |
| `PersistenceRegime` | new `statistics/varratio` (Lo-MacKinlay), or `Hurst` | Trend (persistent) vs. Range (mean-reverting) | Ring(n) | P1 |
| `StructureRegime` | new `reversals/mstructure` (HH / HL vs. LH / LL, break of structure) | Trend | Ring(pivots) | P1 |
| `ChangePointRegime` | new `statistics/cusum`, `pagehinkley`, `bocpd` | resets `BarsInState`; sets `Confidence` | O(1) or Grid(R) | P1 |
| `RegimeVote` | any regime inputs; majority, `KOfN` or weighted | both axes | O(1) | P1 |
| `MarkovRegime` | 2 to 3 state Gaussian HMM **forward filter** with fixed parameters | Trend or Vol | O(K²) | P2 |

`Confidence` is the fraction of detectors agreeing for votes. It is a probability only for probabilistic detectors (`Bocpd`, `MarkovRegime`).

### 10.3 Streaming feasibility of backing measures

| Method | State class | Note |
| :--- | :--- | :--- |
| ADX, DMI, ER, LinReg slope and R², variance ratio | O(1) with Ring(n) for window exits | Fully streaming |
| CHOP, VHF | Deque(n) + Ring(n) | Rolling extremes |
| Percentile rank | Ring(n); O(n) per update, or O(log n) with an order-statistic structure | Fixed window |
| Hurst (R/S, DFA), fractal dimension | Ring(n); O(n) or O(n log n) per update | Fixed window, not O(1) |
| ADF test | Ring(n) with per-step regression | Expensive; use as a slow gate |
| CUSUM, Page-Hinkley | O(1) | Ideal streaming detectors |
| BOCPD (Adams and MacKay, 2007) | Grid(R), run length truncated to R hypotheses | Exact BOCPD grows with t; truncation keeps memory fixed |
| GARCH(1,1) | O(1) filter with given parameters | Parameter estimation is batch and stays outside |
| HMM / Hamilton switching | O(K²) forward filter with given parameters | Smoothed probabilities look ahead and are never used live |
| Swing structure | Ring(pivots) | Delayed by the pivot's right-side bars |

## 11. Catalog: `stops/`

### 11.1 Arming model

- The host calls `Arm(TTradeContext)` at the entry fill and `Disarm()` at the exit fill.
- Arming is explicit and never inferred from an entry signal. An extra entry signal while in a position must not reset trade-anchored state (the failure mode in vectorbt issue #149, where a reset turned a 10% stop into an 18% loss).
- `Arm` resets all trade-anchored state: high-water mark, activation flag, bars in trade, MAE, MFE.
- While disarmed, `Last` and `Target` are `NaN` and `StopActive` is false.

### 11.2 Timing and touch semantics

- Levels computed from bar t are valid for bar t+1. Trails update from confirmed bars only. Using the forming bar's high to move a stop that the same bar can then hit makes backtests optimistic.
- On bar t+1 the node sets `Flags` against the previous levels: `StopTouched` if `Low ≤ Stop` (long), `TargetTouched` if `High ≥ Target` (long), mirrored for shorts.
- `BothTouched`: the intrabar order is unknown without lower-timeframe data. `Reason` defaults to the stop (pessimistic, as in vectorbt); a constructor option lets the host choose.
- `GapThrough`: the open is already beyond the level. A realistic fill is near the open, not at the stop. The library reports; the host prices.
- `EntryBar`: whether a touch on the entry bar counts is a constructor option, default off (EasyLanguage allows it; vectorbt cannot know the intrabar order).
- The library never marks a position closed. The host confirms and calls `Disarm()`.
- A crisp `Touched` view (`stop.Touched`, publishing 0/1) lets touches feed `signals/` nodes such as `Cooldown`.

### 11.3 Classes

| Class | Semantics | State | Priority | Reference to match |
| :--- | :--- | :--- | :--- | :--- |
| `InitialStop` | invalidation level: structural (last confirmed swing ± buffer), volatility (k × ATR at entry), percent, fixed ticks; defines 1R | O(1) after arm | P0 | Pine `loss` / `stop`; NinjaTrader `CalculationMode` |
| `StructuralStop` | `ValueWhen(PivotExtremum)` ± buffer; optional ratchet to newer confirmed swings | Ring(pivots) | P0 | Confirmation-bar stamping |
| `EntryTrail` | trail from the high-water mark since entry by percent, points, ATR or R | O(1) | P0 | Lean `TrailingStopRiskManagementModel`; EasyLanguage `SetPercentTrailing`; vectorbt `sl_trail` |
| `ActivationTrail` | trail activates after profit ≥ X (ticks, R, ATR) and stays active | O(1) | P0 | Pine `trail_points` + `trail_offset`; Amibroker `ActivationFloor` |
| `BreakevenStop` | move stop to entry ± offset after MFE ≥ X | O(1) | P0 | EasyLanguage `SetBreakeven` |
| `LevelTrail` | wraps any level publisher (`Chandelier`, `Ckstop`, `Atrstop`, `Vstop`, `Sar`, `Super`, any MA) into a monotonic trail anchored at entry | as source | P0 | Skender / FacioQuo level parity before ratcheting |
| `ProfitTarget` | R multiple, ATR multiple, percent, ticks, structural (next pivot) | O(1) | P0 | Pine `profit` / `limit`; Backtrader `limitprice` |
| `TimeStop` | exit after n bars in trade | O(1) | P0 | Amibroker `stopTypeNBar` |
| `StopComposer` | tightest-of for protective levels, explicit precedence for same-bar conflicts, `Reason` | O(k) | P0 | Amibroker `SetStopPrecedence` default order: loss, profit, trailing, N-bar |
| `ExcursionTracker` | running MAE / MFE in price and R, bars in trade, bars since MFE | O(1) | P0 | Feeds `StaleTradeExit` and host analytics |
| `RatchetStop` | stop steps in fixed increments of R or ATR | O(1) | P1 | Turtle-style 0.5N steps |
| `ScaleOutLadder` | k targets with fractions; after each, optionally move the stop (breakeven, then previous target) | O(k) | P1 | vectorbt PRO `stop_ladder` |
| `StaleTradeExit` | exit if MFE < x · R after n bars | O(1) | P1 | "No progress" rule |
| `SessionExit` | flatten at a session time | O(1) | P1 | NinjaTrader exit-on-close |

Protective stops never loosen once set, except through an explicit `Arm`.

## 12. Catalog: `sizing/`

### 12.1 API rule

Every sizing function is `static`, takes every account parameter as an argument, returns a value and keeps no state between calls. The streaming `SizerBase` wrapper caches only the volatility input and publishes a size factor in `[0, 1]`. A primitive must not assume account size, exposure, margin or broker semantics; the host multiplies the factor by its own notional or risk budget and rounds to lots.

### 12.2 Functions and classes

| Name | Formula | Account input | Priority | Notes |
| :--- | :--- | :--- | :--- | :--- |
| `RiskPerUnit` | \|entry − stop\| × pointValue | No | P1 | Links `stops/` to `sizing/` |
| `MinStopDistance` | max(distance, m × ATR, k × tick) | No | P1 | Prevents the tight-stop blow-up |
| `VolTargetScalar` | clamp(σ_target / σ̂_t, 0, maxScalar) | No | P1 | σ̂ from any existing volatility estimator; the clamp is an explicit parameter |
| `FixedFractionalSize` | floor((riskPct × equity) / RiskPerUnit) | Yes, per call | P1 | Must be used with `ExposureCap` |
| `ExposureCap` | min(size, maxExposure × equity / (price × pointValue)) | Yes, per call | P1 | Mandatory companion to fixed-fractional sizing; `maxExposure` is notional divided by equity |
| `TurtleUnit` | (riskPct × equity) / (N × pointValue), N = 20-bar ATR | Yes, per call | P1 | Canonical example of the split: N is library, equity is host |
| `InverseVolWeight` | 1 / σ̂ normalized against a reference σ | No | P2 | Single instrument only; cross-asset normalization is portfolio |
| `PyramidSchedule` | add-on levels at entry ± i × 0.5N, up to k units | No | P2 | Emits levels, not orders |
| `KellyFraction`, `OptimalF`, `FixedRatio`, `DrawdownScaler` | need trade distribution or equity curve | Yes, history | Deferred | Backtesting phase |

### 12.3 Composition hazards

- **Double-counting volatility.** A stop at k × ATR with size = risk / (k × ATR) already scales exposure by 1 / ATR. Multiplying by `VolTargetScalar` makes it roughly 1 / ATR². `SizerBase` rejects composing both unless explicitly allowed, and the docs explain why.
- **Tight-stop blow-up.** Size = risk / distance goes to infinity as distance goes to 0. `MinStopDistance` and `ExposureCap` are required in every documented example.
- **Honest framing of volatility targeting.** Moreira and Muir (2017) report higher Sharpe ratios for volatility-managed equity factors. Harvey et al. (2018) find the Sharpe benefit mainly in risk assets (equities, credit), but reduced tail risk across all asset classes. Cederburg et al. (2020) find the out-of-sample gains largely do not hold. Document `VolTargetScalar` as tail control first, and Sharpe improvement only sometimes.

## 13. Supporting Measures in Existing Categories

New continuous indicators that follow the full indicator contract (`AbstractBase`, 6-file layout, validation, Quantower adapter):

| Indicator | Folder | Purpose | State | Priority |
| :--- | :--- | :--- | :--- | :--- |
| `PctRank` | `statistics/pctrank` | Percentile rank of the current value in a window (the existing `Percentile` returns the value at a given percent, the inverse question) | Ring(n) | P0 |
| `VarRatio` | `statistics/varratio` | Lo-MacKinlay variance ratio VR(q) (distinct from `volatility/Vr`, the Volatility Ratio) | Ring(n) | P1 |
| `Cusum` | `statistics/cusum` | Two-sided CUSUM change detector | O(1) | P1 |
| `PageHinkley` | `statistics/pagehinkley` | Page-Hinkley change detector | O(1) | P1 |
| `Bocpd` | `statistics/bocpd` | Bayesian online change-point detection, truncated run length | Grid(R) | P2 |
| `Garch11` | `volatility/garch11` | GARCH(1,1) conditional variance filter with given parameters | O(1) | P2 |
| `MStructure` | `reversals/mstructure` | Market structure: HH / HL / LH / LL labels and break of structure | Ring(pivots) | P1 |

Already present and reused, not rebuilt: `oscillators/Er`, `statistics/LinReg` (`Slope`, `RSquared`), `statistics/Hurst`, `statistics/Adf`, `statistics/Zscore`, `dynamics/TtmSqueeze` (`SqueezeOn`, `SqueezeFired`), `dynamics/HtTrendmode`, `volatility/Bbwp`.

### 13.1 Series arithmetic (shipped ahead of Phase 1)

The signal algebra in Section 9 has no way to express `close > close[5]` or `(fast − slow) > k · atr` without host code: predicates and combinators compare or gate values, they do not compute them. `numerics/` already holds the single-operand half of this algebra (`Lineartrans` for `ax + b`, `Relu`, `Change`, `Mom`); it was missing the two-operand half and value delay. These four ship as ordinary `numerics/` indicators, not `signals/` nodes, because their output is unbounded and therefore fails the `[0, 1]` / crisp value contract of Section 8.1 — they are consumed by predicates, not part of the predicate layer.

| Indicator | Folder | Purpose | Base | State | Priority |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Add` | `numerics/add` | a + b | `BiInputIndicatorBase` (period = 1) | O(1) | P0 |
| `Sub` | `numerics/sub` | a − b; feeds `CrossOver(Sub(fast, slow), 0)` style spreads | `BiInputIndicatorBase` (period = 1) | O(1) | P0 |
| `Mul` | `numerics/mul` | a · b | `BiInputIndicatorBase` (period = 1) | O(1) | P0 |
| `Div` | `numerics/div` | a / b; publishes 0.0 (not NaN) for \|b\| ≤ 1e-12 — a stateless, deterministic policy chosen specifically because a non-finite `ComputeError` result would permanently poison the base's running sum (see Section 8.3) | `BiInputIndicatorBase` (period = 1) | O(1) | P0 |
| `Lag` | `numerics/lag` | x[t−n]; `Lag(0)` is identity; 0.0 during warmup, matching `Mom`'s convention | `AbstractBase` | Ring(n+1) | P0 |

`BiInputIndicatorBase` gained a `Subscribe(a, b)` helper (Section 8.3) so `Add`/`Sub`/`Mul`/`Div` can be event-chained to two independent publishers under the time-join rule; no new base class was needed. `Neg`, `Scale`, and `Offset` are not separate classes: they are `Lineartrans` calls. `Pmax`/`Pmin`/`Clamp`/`Sign`/`Select` remain deferred (P1/P2, per the original gap analysis) until a concrete `signals/` node needs them.

## 14. Host-Owned Position State Machine (Layer 5)

A `SignalBase` implementation is a helper the host may use; the host still owns the real position.

It owns only logical state:

- `Flat`, `Long` or `Short`,
- entry reference (from the host's fill report),
- size factor,
- the armed stop node (usually a `StopComposer`) and its latest levels,
- confirmed and working snapshots for rollback.

Rules:

1. Before the whole dependency graph is hot, it publishes `Flat` with size factor 0 and no armed stops.
2. It publishes intent (`TPosition` target direction, `TOrder` action, size factor, stop and target levels), never orders.
3. It accepts host feedback: filled, partially filled, rejected, externally changed position. It never assumes an intent was filled.
4. On an entry fill it calls `Arm(TTradeContext)` on its stops; on an exit fill, `Disarm()`.
5. Reversal policy (flat first vs. direct reverse) is a constructor option.

Warm-up must never produce an entry or exit event.

## 15. C# and Python Surface

C# is the reference implementation. Python exposes the same graph for streaming and batch users through the NativeAOT bridge, with generated bindings:

```csharp
var fast = new Ema(12);
var slow = new Ema(26);
var rsi  = new Rsi(14);
var atr  = new Atr(14);

var trend = new TrendRegime(new Adx(14), enter: 25, exit: 20, minDwell: 3);
var entry = new AllOf(new CrossOver(fast, slow), rsi.Below(70.0));
var gated = new AllOf(entry, new InRegime(trend, TrendState.Up));
var clean = new FirstSignalOnly(gated, exit: new CrossUnder(fast, slow));

var stops = new StopComposer(
    new InitialStop(InitialStopMode.Atr, atr, multiple: 2.0),
    new ActivationTrail(atr, activateAtR: 1.0, trailAtr: 3.0),
    new ProfitTarget(TargetMode.RMultiple, 3.0),
    new TimeStop(bars: 40));
```

```python
entry = all_of(cross_over(fast, slow), below(rsi, 70.0))
gated = all_of(entry, in_regime(trend, "up"))
clean = first_signal_only(gated, exit=cross_under(fast, slow))
stops = stop_composer(initial_stop("atr", atr, 2.0), activation_trail(atr, 1.0, 3.0),
                      profit_target("r", 3.0), time_stop(40))
v = clean.update(bar, is_new=True)   # 1.0, 0.0 or nan while cold
```

Python operators (`&`, `|`, `~`, `>`, `<`) map to the same nodes where operand types make the result unambiguous. Batch adapters reuse the streaming semantics; there is no second behavior.

## 16. Migration from Current Code

| Current | Change | Reason |
| :--- | :--- | :--- |
| `TSignal` (removed from `lib/core/types/tsignal/`) | Stays removed; delete the empty `tsignal/tests/` folder | Signals use `TValue` (Section 8.1) |
| `SignalBase` publishes `TPosition` | Keep as the Layer 5 base | Position state is Layer 5; `signals/` nodes publish `TValue` |
| `TPosition`, `TOrder`, `Direction`, `OrderAction` | Keep | Match this spec |
| Previous draft names `LessThan`, `GreaterThan`, `All`, `Any` | Replaced by Section 7 | One vocabulary |
| Previous draft `QuanTAlib.Signals` namespace | `QuanTAlib` | Matches the whole library |
| Previous draft paths `lib/core/tvalue/...`, `lib/core/tseries/...` | `lib/core/types/...` | Types moved |

## 17. Allocation and Performance Rules

- No LINQ, closures, boxing, or per-update collection creation in hot paths.
- Inputs are wired at construction and immutable afterward.
- Counters are fixed-width integers with documented saturation (bar counters saturate at `int.MaxValue`, they do not wrap).
- Optional values use `NaN` or explicit presence flags, never nullable reference types.
- Diagnostics and tracing are opt-in and never change signal semantics.
- `[SkipLocalsInit]` and `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot `Update` paths, per AGENTS.md.
- Benchmarks record per-node update cost and allocation (must be zero) for a 10-node reference graph.

## 18. Pitfalls and Mitigations

| Failure mode | Mechanism | Mitigation |
| :--- | :--- | :--- |
| Repainting | Signal evaluated on a forming bar changes before close | `ConfirmOnClose`; edges compare against confirmed previous state; docs state that raw nodes are intrabar |
| Pivot / fractal / zigzag lookahead | A pivot at bar t is known only at t + R | `PivotExtremum` stamps at the confirmation bar; `StructuralStop` uses confirmed pivots only |
| Regime flicker | Single threshold | Schmitt thresholds, minimum dwell, `BarsInState`, flicker metric in tests |
| Smoothed-state lookahead | HMM smoothed probabilities use future data | Filtered probabilities only |
| Multi-input skew | Combinator mixes bar t and t−1 | Time-join rule |
| Cold input read as false | `Not(0.0)` becomes true during warm-up | Cold nodes publish `NaN`; combinators propagate it |
| Silent range clamping | Graded value outside `[0, 1]` distorts combinator math | `OutOfRange` policy; clamping only when the caller asks for it |
| Overfit rule sets | Many AND conditions fit noise | Prefer `KOfN`; document parameter count per composite; walk-forward belongs to the backtesting phase |
| Stop and target in one bar | Intrabar order unknown | `BothTouched` flag; pessimistic default |
| Gaps | Open beyond stop | `GapThrough` flag; host prices the fill |
| Stop clustering at round numbers | Obvious levels get run | Optional buffer (ticks or ATR fraction) on structural stops |
| Double-counting volatility | ATR stop and vol-target sizing multiplied | `SizerBase` composition check |
| Tight-stop exposure blow-up | risk / distance → ∞ | `MinStopDistance` and `ExposureCap` |
| Warm-up entries | Cold inputs | `IsHot` propagation; `NaN` while cold |
| Stop reset on re-entry | Extra entry signal resets anchors | Explicit host `Arm` only |
| Assuming stops always help | Under a random walk, simple stop-loss rules reduce expected return; the premium can be positive under momentum or regime-switching (Kaminski and Lo, 2014) | Docs present stops as regime-conditional; `RegimeFork` can select stop width |

## 19. Testing and Validation

### 19.1 Required tests per primitive

1. Cold, warming, hot and reset states; `NaN` while cold; no truth value, armed stop or non-zero size while cold.
2. Exact equality at threshold and crossover boundaries.
3. Repeated forming-bar ticks oscillating around a boundary.
4. Rollback after an intrabar event that is absent at bar close.
5. Confirmed event behavior across the next bar.
6. Time-join: inputs published in every order produce the same result.
7. Range rule: out-of-range inputs follow the declared `OutOfRange` policy; nothing clamps silently.
8. Composite evaluation order and no duplicate child updates.
9. Zero allocation after construction in representative update loops.
10. Batch and streaming produce identical output.
11. C# and Python parity on the same deterministic stream.

### 19.2 Property tests

- `isNew = false` replay equals a fresh recompute of the bar from confirmed state, for arbitrary tick sequences.
- Combinators on crisp inputs equal the boolean reference (AND, OR, NOT, k-of-n).
- Protective stops never loosen between `Arm` calls.
- `Arm` fully resets trade-anchored state.
- Regime flicker stays under the documented bound for default parameters.

### 19.3 External reference parity

| Class | Reference | Compare |
| :--- | :--- | :--- |
| `CrossOver`, `CrossUnder`, `BarsSince`, `ValueWhen`, `IsRising` | Pine `ta.*` (pin the Pine version) | Bar-by-bar value |
| `FirstSignalOnly`, `CleanEntryExit` | vectorbt `clean_entries_exits` | Same-bar conflict rule |
| `InitialStop`, `EntryTrail`, `ProfitTarget` | vectorbt OHLC stop exit generators with explicit stop entry price | Exit bar and level; stop-first assumption |
| `ActivationTrail` | Pine `strategy.exit(trail_points, trail_offset)`; Amibroker `ActivationFloor` | Activation bar and trail level |
| `TimeStop`, `StopComposer` precedence | Amibroker `ApplyStop` N-bar and `SetStopPrecedence` | Exit bar and reason |
| `BreakevenStop`, percent / dollar trails | EasyLanguage `SetBreakeven`, `SetPercentTrailing` | Level after activation |
| Percent trail from peak | Lean `TrailingStopRiskManagementModel` | Trigger bar on close data |
| `LevelTrail` over `Chandelier`, `Atrstop`, `Vstop` | Skender / FacioQuo | Underlying level before ratcheting |
| `Cooldown`, `StopOutGuard` | Freqtrade `CooldownPeriod`, `StoplossGuard` (per pair) | Blocked bars |
| `Cusum`, `PageHinkley`, `Bocpd` | Synthetic GBM with injected mean and variance breaks | Detection delay and false-alarm rate |
| `MarkovRegime` | statsmodels Markov switching with fixed parameters | Filtered probabilities |

Pine v6 changed how `strategy.exit` prioritizes absolute and relative parameters for the same level compared with v5. Parity tests pin the version.

## 20. Phased Implementation Plan

Each phase ships a usable capability with an exit gate. No phase adds execution, fill simulation, slippage, broker communication or a backtesting loop.

### Phase 0: Contracts

**Deliverables:**

- [x] `TTradeContext` and the enums in `lib/core/types/`: `lib/core/types/ttradecontext/ttradecontext.cs` (`TTradeContext`, `BracketFlags`, `ExitReason`); `lib/core/types/regimestate/regimestate.cs` (`TrendState`, `VolState`).
- [x] Confirmed `AbstractBase` and `BiInputIndicatorBase` cover single- and two-input nodes without changes, beyond the `Subscribe(a, b)` time-join addition to `BiInputIndicatorBase` (Section 8.3, proven by `numerics/Add`/`Sub`/`Mul`/`Div`).
- [ ] `MultiInputBase`, `RegimeBase`, `StopBase`, `SizerBase` — deliberately **not yet built**. Each has no concrete first user yet (`InsideChannel`/`AllOf`/`KOfN`/`Sequence` for `MultiInputBase`; the Phase 3/2/4 classifiers and nodes for the other three). Building an abstract base before a real subclass proves its shape risks the same kind of mismatch the original `BiInputIndicatorBase` reuse investigation found. Build each together with its first concrete consumer instead.
- [x] Value semantics: `SignalConstants.DefaultEpsilon` (`1e-10`, matches the library-wide per-class `Epsilon` convention), `SignalConstants.TruthThreshold` (`0.5`), `OutOfRangePolicy` enum (`Cold` / `Saturate`) in `lib/core/SignalConstants.cs`.
- [x] Time-join rule: implemented as `BiInputIndicatorBase.Subscribe(a, b)` rather than a standalone utility, since its only two-input consumers so far all derive from that base.
- [x] `ConfirmOnClose` in `lib/core/ConfirmOnClose.cs`: wraps any `ITValuePublisher`, buffers every intrabar tick, and publishes a bar's settled value exactly once, when the next bar's first tick arrives. Always exactly one bar behind its source; its `Update(TSeries)` batch path therefore returns one fewer element than the input, which is documented and tested rather than padded to match input length.
- [ ] Hysteresis helper (for `Above`/`Below`'s optional enter/exit band) — deferred to when `Above`/`Below` are built (Phase 1), since its shape depends on their constructor surface.

**Exit gate:** rollback property tests pass on stub nodes (met for `ConfirmOnClose`; `MultiInputBase`/`RegimeBase`/`StopBase`/`SizerBase` stubs remain open); zero allocation measured on an empty 10-node graph (not yet measured — no graph exists to measure until Phase 1 predicates land).

### Phase 1: Signal Algebra (P0)

**Deliverables:** predicates (Section 9.1 P0), triggers including `Sequence`, `Breakout`, `PivotExtremum`, `CrossOverThreshold`; combinators including `Latch`, `KOfN`, `FirstSignalOnly`, `CleanEntryExit`; `Cooldown`.

**Predicates — shipped** (`lib/signals/predicates/`): `Above`, `Below`, `Equal`, `InsideChannel`,
`OutsideChannel`, `ChannelPosition`, `IsRising`, `IsFalling`, `AtHighest`, `AtLowest`,
`PercentDistance`, plus `Hysteresis` (the enter/exit band referenced in the `Above`/`Below` row of
Section 9.1). Two new shared bases came out of this work, each built together with its first real
consumer per the rule set in Phase 0:
- `ComparePredicateBase<TOp>` (`lib/core/CompareOp.cs`): the generic comparison core from Section
  8.8, over `BiInputIndicatorBase` at `period = 1`. `Above`, `Below`, `Equal` are thin sealed
  classes over `AboveOp`/`BelowOp`/`EqualOp`.
- `MultiInputBase` (`lib/core/MultiInputBase.cs`): the Section 8.3 multi-input base, exercised so
  far at exactly 3 inputs by `InsideChannel`/`OutsideChannel`/`ChannelPosition`. Written to support
  2–8 inputs for the Layer 4 combinators that will need more, but not yet generalized beyond 8.

**Predicates — deferred:** `QuantileRank` (needs `statistics/pctrank`, which does not exist yet).

**Design notes recorded during implementation:**
- `IsRising`/`IsFalling` exclude the current bar from their comparison window and use a
  confirm-then-push pattern (mirroring `ConfirmOnClose`) that needs no rebuild-on-correction.
  `AtHighest`/`AtLowest` include the current bar and do need a rebuild (`MonotonicDeque.RebuildMax`/
  `RebuildMin`), following the existing `channels/Dc` pattern. The two pairs are not
  interchangeable despite both being "n-bar extremum" tests — see `IsRising.md` and `AtHighest.md`.
- `ChannelPosition.IsHot` is not a one-way latch: it reflects the validity of the *most recent* bar
  (zero-width channel, or an out-of-range value under `OutOfRangePolicy.Cold`), so it can become
  false again without a `Reset()`, unlike almost every other node in the library.
- Every predicate here overrides `Reset()` to restore the `NaN` cold-state sentinel, since the
  inherited `BiInputIndicatorBase.Reset()`/`MultiInputBase.Reset()` default clears `Last` to
  `0.0`. This was caught by tests, not by inspection, and is worth checking for any future
  predicate built the same way.
- `Hysteresis` only supports the "upward latch" polarity (`exit <= enter`); a "downward latch" is
  deferred until a concrete use case needs it.

**Exit gate:** Pine and vectorbt parity; Quantower plotting works through existing `TValue` adapters; Python bindings generated.

### Phase 2: Stops (P0)

**Deliverables:** `InitialStop`, `StructuralStop`, `EntryTrail`, `ActivationTrail`, `BreakevenStop`, `LevelTrail`, `ProfitTarget`, `TimeStop`, `StopComposer`, `ExcursionTracker`.

**Exit gate:** vectorbt, Amibroker and Pine exit parity; touch and gap flags documented and tested.

### Phase 3: Regimes

**Deliverables:** `PctRank`, `VarRatio`, `Cusum`, `PageHinkley`, `MStructure` in existing folders; `TrendRegime`, `EfficiencyRegime`, `VolatilityRegime`, `SqueezeRegime`, `InRegime`, `RegimeVote`; `SqueezeRelease`, `DivergenceDetector`, `RegimeFork`, `PriorityCascade`.

**Exit gate:** flicker bound met; change-point detection delay and false-alarm rates recorded.

### Phase 4: Sizing Primitives

**Deliverables:** `RiskPerUnit`, `MinStopDistance`, `VolTargetScalar`, `FixedFractionalSize`, `ExposureCap`, `TurtleUnit`.

**Exit gate:** double-count and tight-stop checks enforced by tests.

### Phase 5: Layer 5 Intent Helper

**Deliverables:** a `SignalBase` implementation with reversal policy, priming gate, host feedback contract and stop arming.

**Exit gate:** intent is stable when the host-reported position differs from the requested one; no broker, order or account dependency.

### Phase 6: P1 / P2 Fill-in and Hardening

**Deliverables:** `Bocpd`, `Garch11`, `MarkovRegime`, `StructureRegime`, `PersistenceRegime`, `ScaleOutLadder`, `RatchetStop`, `StaleTradeExit`, `SessionExit`, `SessionFilter`, `VolatilityShockGuard`, `GapGuard`, `StopOutGuard`, `PyramidSchedule`, `InverseVolWeight`, `WeightedVote`; full property-test suite, allocation regression tests, benchmark baselines, NativeAOT review.

**Exit gate:** full solution validation passes; performance budgets recorded; public API compatibility notes written.

### Later (not this spec)

Backtesting, fill simulation, portfolio construction, Kelly / optimal f / fixed ratio / drawdown scaling, account-level guards, cross-instrument limits, market scanning.

## 21. Open Decisions for Phase 0 Review

1. Value and form of the default epsilon for `Equal` and crossover boundaries (absolute, relative, or both).
2. `TruthThreshold` default: `0.5` is proposed; the alternative is "true only at exactly `1.0`", which makes graded inputs unusable in triggers without an explicit `Above`.
3. Combinator algebra: Zadeh min / max is proposed; product / probabilistic sum is the alternative for graded inputs.
4. Whether a regime node's two axes go hot together or independently.
5. `BothTouched` default: stop first (pessimistic) is proposed.
6. Reversal policy default for the Layer 5 helper: flat first is proposed.
7. Whether operator overloads (`>`, `<`, `&`, `|`) ship in Phase 1 or wait until the class API has stabilized.

## 22. References

- Adams, R. P., and MacKay, D. J. C. (2007). Bayesian Online Changepoint Detection. arXiv:0710.3742.
- Cederburg, S., O'Doherty, M. S., Wang, F., and Yan, X. (2020). On the performance of volatility-managed portfolios. Journal of Financial Economics 138(1).
- Faith, C. (2007). Way of the Turtle. McGraw-Hill.
- Hamilton, J. D. (1989). A new approach to the economic analysis of nonstationary time series and the business cycle. Econometrica 57(2).
- Harvey, C. R., Hoyle, E., Korgaonkar, R., Rattray, S., Sargaison, M., and Van Hemert, O. (2018). The impact of volatility targeting. Journal of Portfolio Management 45(1), 14-33.
- Kaminski, K. M., and Lo, A. W. (2014). When do stop-loss rules stop losses? Journal of Financial Markets 18, 234-254.
- Kaufman, P. J. Trading Systems and Methods. Wiley.
- Lo, A. W., and MacKinlay, A. C. (1988). Stock market prices do not follow random walks: evidence from a simple specification test. Review of Financial Studies 1(1).
- Moreira, A., and Muir, T. (2017). Volatility-managed portfolios. Journal of Finance 72(4), 1611-1644.
- Page, E. S. (1954). Continuous inspection schemes. Biometrika 41(1/2).
- Tharp, V. K. Trade Your Way to Financial Freedom. McGraw-Hill.
- Vince, R. (1990). Portfolio Management Formulas. Wiley.
- Zadeh, L. A. (1965). Fuzzy sets. Information and Control 8(3).
- Platform documentation: TradingView Pine Script v6 (`ta.*`, `strategy.exit`, repainting); QuantConnect Lean risk management models; vectorbt signal generators and stop exits; Amibroker `ApplyStop`; TradeStation / MultiCharts built-in stops; NinjaTrader 8 `SetStopLoss`, `SetTrailStop`; NautilusTrader risk and order emulator; Freqtrade protections; Skender / FacioQuo Stock.Indicators.
