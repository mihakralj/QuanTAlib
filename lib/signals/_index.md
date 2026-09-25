# Signals

Strategy-primitive nodes that turn continuous indicator output into discrete trading logic:
predicates, triggers, combinators, and guards, built to a shared value contract (crisp `{0, 1}`,
graded `[0, 1]`, or `NaN` while cold — see `StrategyPrimitives.Spec.md`, Section 8.1). Unlike
every other category in this library, `signals/` is organized into sub-folders because its
sub-layers are tightly related and looked up as a group, not because they are independent
indicator families.

See `StrategyPrimitives.Spec.md` for the full design: the layered architecture (predicates →
triggers → combinators → guards → position state machine), the value semantics every node here
follows, and the phased build plan.

## predicates/

Layer 2: stateless-per-bar (or small fixed-window) tests that turn one or more continuous
streams into a crisp or graded signal.

| Indicator | Full Name | Description |
| :--- | :--- | :--- |
| [ABOVE](predicates/above/Above.md) | Greater-Than Predicate | Crisp: 1.0 when a > b (or a > scalar), else 0.0. |
| [ATHIGHEST](predicates/athighest/AtHighest.md) | At-Maximum Predicate | Crisp: 1.0 when the current bar is the max of an n-bar window that includes it. |
| [ATLOWEST](predicates/atlowest/AtLowest.md) | At-Minimum Predicate | Crisp: 1.0 when the current bar is the min of an n-bar window that includes it. |
| [BELOW](predicates/below/Below.md) | Less-Than Predicate | Crisp: 1.0 when a < b (or a < scalar), else 0.0. |
| [CHANNELPOSITION](predicates/channelposition/ChannelPosition.md) | Normalized Channel Position | Graded [0,1]: (value − lower) / (upper − lower), with an explicit out-of-range policy. |
| [EQUAL](predicates/equal/Equal.md) | Approximate Equality Predicate | Crisp: 1.0 when \|a − b\| ≤ epsilon. |
| [HYSTERESIS](predicates/hysteresis/Hysteresis.md) | Schmitt-Trigger Predicate | Crisp latch with a dead band; removes boundary flicker around a threshold. |
| [INSIDECHANNEL](predicates/insidechannel/InsideChannel.md) | Channel Membership Predicate | Crisp: 1.0 when lower ≤ value ≤ upper. |
| [ISFALLING](predicates/isfalling/IsFalling.md) | Falling Predicate | Crisp: 1.0 when current < min(previous n confirmed bars). |
| [ISRISING](predicates/isrising/IsRising.md) | Rising Predicate | Crisp: 1.0 when current > max(previous n confirmed bars). |
| [OUTSIDECHANNEL](predicates/outsidechannel/OutsideChannel.md) | Channel Exclusion Predicate | Crisp: 1.0 when value > upper or value < lower. |
| [PERCENTDISTANCE](predicates/percentdistance/PercentDistance.md) | Relative Distance | Raw signed ratio (a − b) / b; feeds Above/Below, not a predicate output itself. |

### Deferred

- `QuantileRank` (P1) — depends on `statistics/pctrank`, which does not exist yet.

## triggers/, combinators/, guards/

Not yet built. See `StrategyPrimitives.Spec.md` Sections 9.2–9.4 and Section 20 (Phase 1) for
the planned contents (`CrossOver`, `BecameTrue`, `BarsSince`, `Sequence`; `AllOf`, `AnyOf`,
`KOfN`, `Latch`, `Inhibit`; `Cooldown`, `SessionFilter`).
