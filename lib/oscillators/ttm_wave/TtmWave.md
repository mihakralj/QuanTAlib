# TTM_WAVE: TTM Wave

> **Pending Implementation** - Placeholder for John Carter's TTM Wave indicator

## Historical Context

John Carter developed TTM Wave as a multi-period MACD composite indicator using Fibonacci-based periods. The indicator displays three "waves" (A, B, C) that help traders identify the alignment of multiple timeframes and the strength of momentum across different cycle lengths.

## Algorithm

### Wave A (Short-term momentum)
```
Wave_A1 = EMA(close, 8) - EMA(close, 34)
Wave_A2 = EMA(Wave_A1, 34)
```

### Wave B (Medium-term momentum)
```
Wave_B1 = EMA(close, 8) - EMA(close, 55)
Wave_B2 = EMA(Wave_B1, 55)
```

### Wave C (Long-term momentum using Fibonacci periods)
```
e1 = EMA(close, 34)
e2 = EMA(close, 55)
e3 = EMA(close, 89)
e4 = EMA(close, 144)
e5 = EMA(close, 233)
e6 = EMA(close, 377)

Wave_C = e1 + e2 + e3 + e4 + e5 + e6 - 6 * EMA(close, some_avg_period)
```

## Fibonacci Periods

| Period | Fibonacci |
|:-------|:----------|
| 8 | F(6) |
| 34 | F(9) |
| 55 | F(10) |
| 89 | F(11) |
| 144 | F(12) |
| 233 | F(13) |
| 377 | F(14) |

## Outputs

| Output | Type | Description |
|:-------|:-----|:------------|
| WaveA | double | Fast momentum oscillator (red/magenta histogram) |
| WaveB | double | Medium momentum oscillator (dark red/magenta histogram) |
| WaveC | double | Slow momentum composite (blue histogram) |

## Trading Interpretation

1. **All waves aligned:** Strong trend - ride the move
2. **Wave A diverges from C:** Early warning of potential reversal
3. **Waves crossing zero:** Momentum shift in progress
4. **Wave C color change:** Major cycle direction changing

## Category

**Oscillators** - Multi-period momentum composite oscillating around zero line.

## See Also

- [MACD: Moving Average Convergence Divergence](../../momentum/macd/Macd.md)
- [AO: Awesome Oscillator](../ao/Ao.md)
- [TTM_SQUEEZE: TTM Squeeze](../../dynamics/ttm_squeeze/TtmSqueeze.md)
