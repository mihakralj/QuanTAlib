# ADL: Accumulation/Distribution Line

> *Volume precedes price.*

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Volume                        |
| **Inputs**       | OHLCV bar (TBar)                          |
| **Parameters**   | None                      |
| **Outputs**      | Single series (ADL)                       |
| **Output range** | Unbounded                     |
| **Warmup**       | 1 bar                          |
| **PineScript**   | [adl.pine](adl.pine)                       |

- The Accumulation/Distribution Line (ADL) is the bedrock of volume analysis.
- No configurable parameters; computation is stateless per bar.
- Output range: Unbounded.
- Requires 1 bar of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

The Accumulation/Distribution Line (ADL) is the bedrock of volume analysis. It attempts to answer a single, vital question: "Are the big players buying or selling?"

Unlike On-Balance Volume (OBV), which treats every up-day as 100% buying, ADL is nuanced. It looks at *where* the price closed within the day's range. A close near the high on massive volume screams "Accumulation." A close near the low on massive volume screams "Distribution."

## Historical Context

Developed by Marc Chaikin, the ADL was originally designed to spot divergences. Chaikin noticed that if a stock made a new high but the ADL failed to make a new high, a crash was imminent. He essentially quantified the "smart money" flow.

## Architecture & Physics

ADL is a cumulative indicator, meaning it has infinite memory. Today's value depends on the sum of all yesterdays.

The core mechanic is the **Money Flow Multiplier (MFM)**, also known as the Close Location Value (CLV). This value ranges from -1 to +1:

* **+1**: Close = High (Maximum Accumulation)
* **-1**: Close = Low (Maximum Distribution)
* **0**: Close is exactly in the middle

This multiplier is then applied to the volume to determine the "Money Flow Volume" for the period.

## Mathematical Foundation

### 1. Money Flow Multiplier (MFM)

$$
MFM = \frac{(Close - Low) - (High - Close)}{High - Low}
$$

### 2. Money Flow Volume (MFV)

$$
MFV = MFM \times Volume
$$

### 3. Accumulation/Distribution Line (ADL)

$$
ADL_t = ADL_{t-1} + MFV_t
$$

## Performance Profile

### Operation Count (Streaming Mode)

ADL computes Money Flow Multiplier (MFM) from bar data, multiplies by volume, and accumulates cumulatively — O(1).

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| MFM = ((C-L)-(H-C)) / (H-L) | 1 | 5 cy | ~5 cy |
| MFV = MFM * Volume | 1 | 3 cy | ~3 cy |
| ADL += MFV (cumulative sum) | 1 | 1 cy | ~1 cy |
| Zero guard on H-L | 1 | 2 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~13 cy** |

O(1) cumulative indicator — no window, no buffer. Throughput ~4 ns/bar. Division is the critical path (H-L guard prevents divide-by-zero on doji bars).

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | 10 | High; O(1) calculation with simple arithmetic. |
| **Allocations** | 0 | Zero-allocation in hot paths. |
| **Complexity** | O(1) | Constant time per update. |
| **Accuracy** | 10 | Matches all standard libraries exactly. |
| **Timeliness** | 10 | No lag; updates immediately with each bar. |
| **Overshoot** | N/A | Cumulative indicator; concept doesn't apply. |
| **Smoothness** | 2 | Jagged; reflects raw volume and price location. |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **QuanTAlib** | ✅ | Validated. |
| **TA-Lib** | ✅ | Matches `TA_AD` exactly. |
| **Skender** | ✅ | Matches `GetAdl` exactly. |
| **Tulip** | ✅ | Matches `ad` exactly. |
| **Ooples** | ✅ | Matches `CalculateAccumulationDistributionLine`. |

### Common Pitfalls

* **Gaps**: ADL ignores gaps. If a stock gaps up but closes near its low, ADL will register distribution, even if the price is higher than yesterday.
* **Scale**: The absolute value of ADL is meaningless; it depends on the start date of the data. Only the *trend* and *divergence* matter.
* **Volume Spikes**: A single bad data point with erroneous volume can permanently skew the ADL. Sanitize your data.
