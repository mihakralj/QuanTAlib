# RMSE: Root Mean Squared Error

> "MSE's more interpretable sibling that speaks the language of your data."

Root Mean Squared Error (RMSE) is the square root of MSE, providing an error metric in the same units as the original data while retaining sensitivity to large errors.

## Mathematical Foundation

### Formula

$$RMSE = \sqrt{\frac{1}{n} \sum_{i=1}^{n} (y_i - \hat{y}_i)^2} = \sqrt{MSE}$$

## Properties

- **Non-negative**: RMSE ≥ 0
- **Same units**: Unlike MSE, RMSE is in original data units
- **Outlier sensitive**: Inherits MSE's penalty for large errors
- **Always ≥ MAE**: RMSE ≥ MAE due to Jensen's inequality

## Usage

```csharp
var rmse = new Rmse(period: 20);
var result = rmse.Update(actualValue, predictedValue);

// Batch calculation
var results = Rmse.Calculate(actualSeries, predictedSeries, period: 20);
```

## Performance Profile

| Metric | Score | Notes |
| :--- | :--- | :--- |
| **Throughput** | ~15 ns/bar | O(1) with sqrt operation |
| **Allocations** | 0 | Pre-allocated ring buffer |
| **Complexity** | O(1) | Constant time per update |

## Related Indicators

- [MSE](../mse/Mse.md) - Mean Squared Error
- [MAE](../mae/Mae.md) - Mean Absolute Error
