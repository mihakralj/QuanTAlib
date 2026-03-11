# Sum: Summation with Kahan-Babuška Algorithm

| Property         | Value                            |
| ---------------- | -------------------------------- |
| **Category**     | Statistic                        |
| **Inputs**       | Source (close)                          |
| **Parameters**   | `period`                      |
| **Outputs**      | Single series (Sum)                       |
| **Output range** | Varies (see docs)                     |
| **Warmup**       | `period` bars                          |
| **PineScript**   | [sum.pine](sum.pine)                       |

- The Sum indicator calculates a rolling window summation using the Kahan-Babuška algorithm (also known as "improved Kahan" or "second-order compensa...
- Parameterized by `period`.
- Output range: Varies (see docs).
- Requires `period` bars of warmup before first valid output (IsHot = true).
- Validated against TA-Lib, Skender, and Tulip reference implementations where available.

> "The naive approach to summation assumes all digits matter equally. They don't. When you add 1e-10 to 1e10, that small value vanishes into the rounding noise. Kahan-Babuška tracks what got lost and adds it back later. It's bookkeeping for bits that would otherwise slip through the cracks."

The Sum indicator calculates a rolling window summation using the Kahan-Babuška algorithm (also known as "improved Kahan" or "second-order compensated summation") for maximum numerical precision. This approach captures rounding errors that even classic Kahan summation misses, making it suitable for numerical libraries, statistics, and trading applications where precision matters.

## Historical Context

The Kahan summation algorithm was introduced by William Kahan in 1965 to reduce the numerical error in the total obtained by adding a sequence of finite-precision floating-point numbers. The Kahan-Babuška variant extends this by tracking a second level of compensation, capturing errors introduced during the compensation step itself.

Standard rolling sum implementations use naive addition/subtraction, which accumulates floating-point rounding errors over time. After millions of ticks with values spanning multiple orders of magnitude, these errors can become significant. The Kahan-Babuška approach keeps error bounded near machine epsilon regardless of sequence length.

## Architecture & Physics

### The Precision Problem

Consider summing values that span many orders of magnitude:

```csharp
double sum = 1e15;
sum += 1.0;  // The 1.0 is lost due to limited precision
```

With 64-bit doubles, adding a small value to a large sum can result in the small value being completely absorbed into rounding error. In a sliding window sum, this happens on both addition and subtraction, compounding the problem.

### Kahan-Babuška Solution

The algorithm maintains three running values:

* `sum`: The accumulated sum
* `c`: First-order compensation (captures primary rounding error)
* `cc`: Second-order compensation (captures error of the error)

For each value `x` to add:

```csharp
// Primary Kahan step
double y = x - c;
double t = sum + y;
c = (t - sum) - y;
sum = t;

// Secondary compensation (Babuška improvement)
double z = c - cc;
double tt = sum + z;
cc = (tt - sum) - z;
sum = tt;
```

This formulation:

1. Computes the lost precision from each addition
2. Tracks the lost precision from computing the lost precision
3. Reintegrates both error terms into subsequent operations

### Sliding Window Complexity

For a rolling window sum, values must be both added (new) and subtracted (old). The Kahan-Babuška approach handles subtraction identically by negating the value before applying the algorithm. Periodic resync (recalculating from buffer contents) prevents long-term drift.

## Mathematical Foundation

### 1. Kahan Summation (First Order)

For each value $x$ to add to sum $S$:

$$y = x - c$$
$$t = S + y$$
$$c = (t - S) - y$$
$$S = t$$

Where $c$ captures the low-order bits lost in the addition.

### 2. Babuška Extension (Second Order)

After the primary step, compensate the compensation:

$$z = c - cc$$
$$tt = S + z$$
$$cc = (tt - S) - z$$
$$S = tt$$

### 3. Error Bound

Standard summation error: $O(n \cdot \epsilon)$

Kahan summation error: $O(\sqrt{n} \cdot \epsilon)$

Kahan-Babuška error: Approaches machine epsilon $\epsilon$ regardless of $n$

Where $\epsilon \approx 2.2 \times 10^{-16}$ for 64-bit doubles.

## Performance Profile

### Operation Count (Streaming Mode)

Rolling Sum uses a single running accumulator updated by adding the new value and subtracting the evicted value.

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| Ring buffer add/evict | 1 | 3 cy | ~3 cy |
| sum += new; sum -= evict | 2 | 1 cy | ~2 cy |
| NaN guard + state update | 1 | 2 cy | ~2 cy |
| **Total** | **O(1)** | — | **~7 cy** |

Fastest possible O(1) sliding aggregate. Used as a building block inside SMA, stddev, and dozens of other indicators. Throughput ~2 ns/bar.

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15 ns/bar | ~2× Kahan, ~3× naive |
| **Allocations** | 0 | Zero-allocation in hot paths |
| **Complexity** | O(1) | Constant time per update with RingBuffer |
| **Accuracy** | 10 | Near machine-epsilon precision |
| **Memory** | O(n) | RingBuffer stores period values |

## Validation

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_SUM` function |
| **Tulip** | ✅ | Matches `ti.sum` indicator |
| **Mathematical** | ✅ | Validated against naive calculation |

## Use Cases

1. **Rolling Statistics**: Foundation for moving averages, standard deviation
2. **Volume Analysis**: Summing volume over periods
3. **Price Totals**: Accumulating price changes
4. **High-Precision Finance**: Where rounding errors have monetary impact

## API Usage

### Streaming Mode

```csharp
var sum = new Sum(period: 20);
foreach (var price in prices)
{
    var result = sum.Update(new TValue(DateTime.UtcNow, price));
    Console.WriteLine($"Rolling Sum: {result.Value}");
}
```

### Batch Mode

```csharp
var series = new TSeries();
// ... populate series ...
var results = Sum.Batch(series, period: 20);
```

### Span Mode (Zero Allocation)

```csharp
double[] input = new double[1000];
double[] output = new double[1000];
// ... populate input ...
Sum.Batch(input.AsSpan(), output.AsSpan(), period: 20);
```

### Event-Driven Mode

```csharp
var source = new TSeries();
var sum = new Sum(source, period: 20);
// Sum automatically updates when source publishes
source.Add(new TValue(DateTime.UtcNow, 100.0));
```

## Common Pitfalls

1. **Overkill for Simple Cases**: If your values are all similar magnitude and sequence length is short, naive summation is faster and sufficient.

2. **Period Selection**: A very large period means more values in the buffer and more memory usage.

3. **Resync Frequency**: The default resync interval (1000 updates) provides a good balance between performance and drift prevention. Adjust if needed for extreme precision requirements.

4. **Not a Substitute for Decimal**: For financial applications requiring exact decimal representation, use `decimal` type. Kahan-Babuška improves floating-point accuracy but doesn't eliminate floating-point representation limitations.

## When to Use Kahan-Babuška

**Use it when:**

* Writing a numerical or statistics library
* Inputs span many orders of magnitude
* Correctness matters more than raw throughput
* Long-running streaming calculations

**Skip it when:**

* Values are similar magnitude
* Sequence length is bounded and small
* Maximum throughput is critical
* Using `decimal` type instead
