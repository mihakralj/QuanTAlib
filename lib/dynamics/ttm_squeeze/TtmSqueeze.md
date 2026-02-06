# TTM_SQUEEZE: TTM Squeeze

> **Pending Implementation** - Placeholder for John Carter's TTM Squeeze indicator

## Historical Context

John Carter developed TTM Squeeze as his signature volatility breakout indicator, popularized through his book *Mastering the Trade* and thinkorswim platform. The indicator combines Bollinger Bands and Keltner Channels to identify low-volatility "squeeze" conditions that typically precede explosive price moves.

## Algorithm

### Squeeze Detection
- **Squeeze On (●):** Bollinger Bands inside Keltner Channel
- **Squeeze Off (○):** Bollinger Bands outside Keltner Channel

### Momentum Histogram
Linear regression of price deviation from 20-period midline:
```
midline = (Highest(20) + Lowest(20)) / 2
momentum = LinReg(close - midline, 20)
```

### Color Coding
- **Cyan:** Momentum rising above zero
- **Blue:** Momentum falling but above zero
- **Red:** Momentum falling below zero
- **Yellow:** Momentum rising but below zero

## Default Parameters

| Parameter | Value | Description |
|:----------|:------|:------------|
| BB Length | 20 | Bollinger Band period |
| BB Mult | 2.0 | Bollinger Band standard deviation multiplier |
| KC Length | 20 | Keltner Channel period |
| KC Mult | 1.5 | Keltner Channel ATR multiplier |

## Outputs

| Output | Type | Description |
|:-------|:-----|:------------|
| Momentum | double | Linear regression momentum value |
| SqueezeOn | bool | True when BB inside KC |
| MomentumRising | bool | True when momentum increasing |
| MomentumPositive | bool | True when momentum > 0 |

## Trading Signals

1. **Squeeze Fired:** First bar where SqueezeOn transitions to false
2. **Long Entry:** Squeeze fires + momentum positive + momentum rising
3. **Short Entry:** Squeeze fires + momentum negative + momentum falling

## Category

**Dynamics** - Measures volatility compression and subsequent momentum release.

## See Also

- [BBS: Bollinger Band Squeeze](../../oscillators/bbs/Bbs.md)
- [BBW: Bollinger Band Width](../../volatility/bbw/Bbw.md)
- [KCHANNEL: Keltner Channel](../../channels/kchannel/Kchannel.md)
- [TTM: TTM Trend](../ttm/Ttm.md)
