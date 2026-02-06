# [CODE: Full name of the indicator]

> short witty quote or insight about the indicator

One paragraph describing the indicator and its purpose to a trader.

## API

**Class**: `[ClassName]`

| Parameter | Type | Default | Range | Description |
| :--- | :--- | :--- | :--- | :--- |
| `period` | `int` | `14` | `>0` | The window size for the calculation. |
| `input` | `TValue` | — | `any` | Initial input source (optional). |

**Properties**
- `Value` (`double`): The current value of the indicator.
- `IsHot` (`bool`): Returns `true` if valid data is available (warmup complete).

**Methods**
- `Calc(TValue input)`: Updates the indicator with a new data point and returns the result.

## C# Example

```csharp
using QuanTAlib;

// Initialize
var indicator = new [ClassName](period: 14);

// Update Loop
foreach (var bar in quotes)
{
    var result = indicator.Calc(bar.Close);
    
    // Use valid results
    if (indicator.IsHot)
    {
        Console.WriteLine($"{bar.Date}: {result.Value}");
    }
}
```

## Historical Context

2-3 paragraphs about the origin of the indicator, who created it, and any relevant historical context. This should include the motivation behind its creation and how it fits into the broader landscape of technical analysis.

## Architecture & Physics

High-level description of calculation steps - both standard/naive and the optimized version. Use Mermaid diagram describing calculation pipeline if indicator is complex.

### Calculation Step 1..n

Mathematical formulas in LaTeX format, followed by with explanations of what each variable represents and how it contributes to the final output.

## Performance Profile

Describe the computational complexity of the indicator, including any optimizations that have been made. Explain if original is O(n) and how it was optimized to O(1) or O(log n) if applicable.

### Operation Count - Single value

| Operation | Count | Cost (cycles) | Subtotal |
| :--- | :---: | :---: | :---: |
| SUB (Sum - oldest) | 1 | 1 | 1 |
| ADD (Sum + newest) | 1 | 1 | 1 |
| DIV (Sum / N) | 1 | 15 | 15 |
| **Total** | **3** | — | **~17 cycles** |

### Operation Count - Batch processing

Explain if/why vectorization accelerates calculations.

| Operation | Scalar Ops | SIMD Ops (AVX-512) | Acceleration |
| :--- | :---: | :---: | :---: |
| Initial N-sum | N | N/8 | 8× |
| Running update (per bar) | 3 | ~1 | ~3× |

## Validation

What are validation sources - if any. If no external sources, describe how the indicator was validated.

| Library | Status | Notes |
| :--- | :--- | :--- |
| **TA-Lib** | ✅ | Matches `TA_FUNC` |
| **Skender** | ✅ | Matches `Indicator` |
| **Pandas-TA**| ✅ | Matches `ta.func` |

## Usage & Pitfalls

* List of practical tips for using the indicator effectively, including common pitfalls to avoid.
