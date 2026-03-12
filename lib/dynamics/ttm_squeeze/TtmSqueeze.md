# TTM_SQUEEZE: TTM Squeeze

> *Volatility compression is the market holding its breath before screaming.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Dynamic                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | `bbPeriod` (default 20), `bbMult` (default 2.0), `kcPeriod` (default 20), `kcMult` (default 1.5), `momPeriod` (default 20)                      |
| **Outputs**      | Single series (TtmSqueeze)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `Math.Max(Math.Max(bbPeriod, kcPeriod), momPeriod)` bars                          |

- John Carter's TTM Squeeze detects low-volatility compression by comparing Bollinger Band width against Keltner Channel width: when BB fits inside K...
- Parameterized by `bbperiod` (default 20), `bbmult` (default 2.0), `kcperiod` (default 20), `kcmult` (default 1.5), `momperiod` (default 20).
- Output range: Varies (see docs).
- Requires `Math.Max(Math.Max(bbPeriod, kcPeriod), momPeriod)` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

John Carter's TTM Squeeze detects low-volatility compression by comparing Bollinger Band width against Keltner Channel width: when BB fits inside KC, a "squeeze" is on, signaling imminent breakout. The momentum component uses linear regression of price deviation from the Donchian midline to indicate direction. The indicator outputs a boolean squeeze state plus a continuous momentum histogram, requiring BB(20,2.0) and KC(20,1.5) as default parameters with a combined warmup of 20 bars.

## Historical Context

John Carter developed TTM Squeeze as his signature volatility breakout indicator, popularized through *Mastering the Trade* (2005) and the thinkorswim platform. The core insight combines two independent volatility measures: Bollinger's standard-deviation bands and Keltner's ATR-based channels. When the faster-reacting BB contracts inside the slower KC, it signals unusually low volatility, a condition that reliably precedes explosive directional moves. Carter added a momentum oscillator based on linear regression to provide directional bias during squeeze releases. The indicator became one of the most widely used proprietary tools in retail trading.

## Architecture & Physics

### 1. Bollinger Band Width

$$\text{BB}_{\text{upper}} = \text{SMA}(C, N_{\text{BB}}) + k_{\text{BB}} \cdot \sigma(C, N_{\text{BB}})$$

$$\text{BB}_{\text{lower}} = \text{SMA}(C, N_{\text{BB}}) - k_{\text{BB}} \cdot \sigma(C, N_{\text{BB}})$$

where $N_{\text{BB}} = 20$, $k_{\text{BB}} = 2.0$, and $\sigma$ is population standard deviation.

### 2. Keltner Channel Width

$$\text{KC}_{\text{upper}} = \text{EMA}(C, N_{\text{KC}}) + k_{\text{KC}} \cdot \text{ATR}(N_{\text{KC}})$$

$$\text{KC}_{\text{lower}} = \text{EMA}(C, N_{\text{KC}}) - k_{\text{KC}} \cdot \text{ATR}(N_{\text{KC}})$$

where $N_{\text{KC}} = 20$, $k_{\text{KC}} = 1.5$.

### 3. Squeeze Detection

$$\text{SqueezeOn} = (\text{BB}_{\text{lower}} > \text{KC}_{\text{lower}}) \text{ and } (\text{BB}_{\text{upper}} < \text{KC}_{\text{upper}})$$

When BB fits entirely inside KC, the squeeze is active. The first bar where squeeze transitions from on to off ("squeeze fires") signals the breakout.

### 4. Momentum Histogram

$$\text{midline} = \frac{\text{Highest}(H, N) + \text{Lowest}(L, N)}{2}$$

$$\delta_t = C_t - \frac{\text{midline}_t + \text{SMA}(C, N)}{2}$$

$$\text{Momentum} = \text{LinReg}(\delta, N)$$

The linear regression extracts the trend component of the deviation, filtering noise. Momentum sign indicates direction; slope indicates acceleration.

### 5. Momentum Color States

| Color | Condition |
|:------|:----------|
| Cyan | Momentum > 0 and rising |
| Blue | Momentum > 0 and falling |
| Red | Momentum < 0 and falling |
| Yellow | Momentum < 0 and rising |

### 6. Complexity

| Metric | Value |
|:-------|:------|
| Time | O(1) per bar (incremental BB, KC, LinReg updates) |
| Space | O(N) for sliding window buffers (SMA, StdDev, ATR, high/low, LinReg) |
| Warmup | N bars (default 20) |

## Mathematical Foundation

### Parameters

| Parameter | Type | Default | Constraint | Description |
|:----------|:-----|:--------|:-----------|:------------|
| bbLength | int | 20 | > 1 | Bollinger Band period |
| bbMult | double | 2.0 | > 0 | BB standard deviation multiplier |
| kcLength | int | 20 | > 1 | Keltner Channel period |
| kcMult | double | 1.5 | > 0 | KC ATR multiplier |

### Squeeze-Fire Signal

The critical trading signal occurs on the transition bar:

$$\text{SqueezeFired}_t = \text{SqueezeOn}_{t-1} \text{ and } \neg\text{SqueezeOn}_t$$

Combined with momentum direction, this yields entry signals: long when squeeze fires with positive rising momentum, short when squeeze fires with negative falling momentum.

## Performance Profile

### Operation Count (Streaming Mode)

TTM Squeeze detects when Bollinger Bands are inside Keltner Channels (the "squeeze"), and fires momentum via a linear-regression oscillator.

**Post-warmup steady state (per bar):**

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SMA update (BB middle) + variance (O(N)) | N+5 | 1 | N+5 |
| SQRT (BB StdDev) | 1 | 20 | 20 |
| ATR update (FMA RMA) | 1 | 4 | 4 |
| BB upper/lower (ADD/SUB × 2) | 2 | 1 | 2 |
| KC upper/lower (EMA + ATR × mul, ADD/SUB × 2) | 4 | 4 | 16 |
| CMP × 2 (BB inside KC?) | 2 | 1 | 2 |
| Linear regression oscillator (O(N)) | ~3N | 3 | ~3N |
| **Total** | **~4N+35** | — | **~4N+49** |

For default $N=20$: ~129 cycles per bar. The O(N) variance + O(N) linear regression scan dominate.

### Batch Mode (SIMD Analysis)

| Operation | Vectorizable? | Notes |
| :--- | :---: | :--- |
| BB computation (prefix sum variance) | Yes | VADDPD + VMULPD for rolling variance |
| ATR (RMA) | **No** | Recursive IIR |
| Keltner EMA | **No** | Recursive IIR |
| Linear regression | Yes | Prefix sums of x×y and x² enable O(1) window regression |
| Squeeze detection | Yes | VCMPPD after bands computed |

Regression can be recast as prefix-sum dot products for SIMD acceleration; ATR/EMA chains remain sequential.

### Quality Metrics

| Metric | Score | Notes |
| :--- | :---: | :--- |
| **Accuracy** | 9/10 | SQRT precision adequate; linear regression high fidelity |
| **Timeliness** | 5/10 | N-bar windows on all components; squeeze detection has inherent N/2 lag |
| **Smoothness** | 7/10 | Linear regression oscillator is smooth by construction |
| **Noise Rejection** | 7/10 | Dual-channel squeeze reduces false momentum triggers |

## Resources

- Carter, J. (2005). *Mastering the Trade*. McGraw-Hill.
- Bollinger, J. (2001). *Bollinger on Bollinger Bands*. McGraw-Hill.
- Keltner, C. (1960). *How to Make Money in Commodities*. The Keltner Statistical Service.
