# Core

Price transforms and fundamental building blocks. These indicators compute derived prices from OHLCV bars and serve as inputs to higher-order indicators.

## Indicators

| Indicator | Full Name | Description |
| :-------- | :-------- | :---------- |
| [AVGPRICE](avgprice/Avgprice.md) | Average Price | (O+H+L+C) * 0.25 via FMA |
| [HA](ha/Ha.md) | Heikin-Ashi | Modified OHLC candles. Smoothed trend visualization. Output is TBar. |
| [MEDPRICE](medprice/Medprice.md) | Median Price | (H+L) * 0.5 |
| [MIDBODY](midbody/Midbody.md) | Open-Close Average | (O+C) * 0.5 |
| [MIDPOINT](midpoint/Midpoint.md) | Rolling Midpoint | (Max+Min) * 0.5 over lookback window |
| [MIDPRICE](midprice/Midprice.md) | Mid Price | (Highest High + Lowest Low) * 0.5 |
| [TYPPRICE](typprice/Typprice.md) | Typical Price | (H+L+C) * OneThird via FMA |
| [WCLPRICE](wclprice/Wclprice.md) | Weighted Close Price | (H+L+2C) * 0.25 via FMA |

## Architecture

All Core indicators share common traits:

- **Zero allocation** in `Update` hot path
- **FMA optimization** where applicable (Avgprice, Typprice, Wclprice)
- **Multiplication over division** (0.25 instead of /4, OneThird instead of /3)
- **NaN/Infinity guard** via last-valid-value substitution
- **Bar correction** via `isNew` rollback pattern
- **Dual API** with stateful `Update` + stateless static `Calculate`
- **SIMD batch** via `ReadOnlySpan<double>` / `Span<double>` overloads

### TBar-Based vs TValue-Based

| Type | Indicators | Input |
| :--- | :--------- | :---- |
| TBar | AVGPRICE, MEDPRICE, MIDPRICE, MIDBODY, TYPPRICE, WCLPRICE | OHLCV bars |
| TValue | MIDPOINT | Single value series |

