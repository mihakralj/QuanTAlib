# Rocket RSI (RRSI)

**Category:** Oscillators
**Type:** Unbounded zero-mean oscillator
**Author:** John F. Ehlers, TASC May 2018

## Description

Rocket RSI combines Ehlers' Super Smoother filter with a custom RSI calculation
and applies the Fisher Transform to produce a Gaussian-distributed oscillator
with sharp turning-point signals ideal for cyclic reversal detection.

## Mathematical Foundation

### Step 1: Half-Cycle Momentum
$$\text{Mom}_i = \text{Close}_i - \text{Close}_{i - (\text{rsiLength} - 1)}$$

### Step 2: Super Smoother Filter (2-Pole Butterworth)
Coefficients (computed once):
$$a_1 = e^{-1.414\pi / \text{smoothLength}}, \quad b_1 = 2 a_1 \cos(1.414\pi / \text{smoothLength})$$
$$c_2 = b_1, \quad c_3 = -a_1^2, \quad c_1 = 1 - c_2 - c_3$$

Filter:
$$\text{Filt}_i = c_1 \cdot \frac{\text{Mom}_i + \text{Mom}_{i-1}}{2} + c_2 \cdot \text{Filt}_{i-1} + c_3 \cdot \text{Filt}_{i-2}$$

### Step 3: Ehlers RSI (Normalized to ±1)
Over the last `rsiLength` bars of filter differences:
$$CU = \sum_{j=0}^{n-1} \max(\text{Filt}_{i-j} - \text{Filt}_{i-j-1},\ 0)$$
$$CD = \sum_{j=0}^{n-1} \max(\text{Filt}_{i-j-1} - \text{Filt}_{i-j},\ 0)$$
$$\text{RSI} = \frac{CU - CD}{CU + CD} \in [-1, 1]$$

### Step 4: Fisher Transform
$$\text{RocketRSI} = \frac{1}{2} \ln\left(\frac{1 + \text{clamp(RSI, \pm0.999)}}{1 - \text{clamp(RSI, \pm0.999)}}\right) = \text{arctanh}(\text{RSI})$$

## Parameters

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| smoothLength | 10 | > 0 | Super Smoother filter period |
| rsiLength | 10 | > 0 | RSI accumulation window |

## Interpretation

- **Values > +2**: Overbought — potential sell signal
- **Values < −2**: Oversold — potential buy signal
- **Zero crossings**: Momentum shift
- **Peaks/troughs**: Cyclic turning points

The Fisher Transform produces a nearly Gaussian distribution, meaning:
- ~68% of values fall within ±1 standard deviation
- Values beyond ±2 are statistically extreme (~5%)
- Values beyond ±3 are very rare (~0.3%)

## Key Differences from Standard RSI

1. **Super Smoother pre-filter** removes high-frequency noise
2. **Ehlers RSI** uses raw summation (not Wilder's exponential smoothing)
3. **RSI output is ±1** (not 0–100), already suited for Fisher Transform
4. **Fisher Transform** converts to Gaussian distribution with sharp reversals

## Warmup Period

`smoothLength + rsiLength` bars are needed for the IIR filter to stabilize
and the RSI accumulation window to fill.

## C# Usage

```csharp
// Streaming
var rrsi = new Rrsi(smoothLength: 10, rsiLength: 10);
foreach (var bar in series)
{
    TValue result = rrsi.Update(bar);
    // result.Value is the Rocket RSI
}

// Batch
TSeries results = Rrsi.Batch(series);

// Span
Rrsi.Batch(source, output, smoothLength: 10, rsiLength: 10);
```

## References

- Ehlers, J. F. (2018). "Rocket RSI." *Technical Analysis of Stocks & Commodities*, May 2018.
- Ehlers, J. F. (2004). *Cybernetic Analysis for Stocks and Futures*. Wiley.
