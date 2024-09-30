# AFIRMA: Autoregressive Finite Impulse Response Moving Average

## Concept

AFIRMA indicator is a hybrid moving average that combines the benefits of digital filtering and cubic spline fitting to provide a smooth and accurate representation of price movement without significant time lag.

## Origin

The AFIRMA indicator is based on the principles of digital signal processing and curve fitting. It was developed to address the limitations of traditional moving averages, which often suffer from time lag or fail to accurately track price movements.

## Key Features

- **Digital Filter**: The AFIRMA indicator uses a digital filter to smooth out price movements.
- **Cubic Spline Fitting**: The latest candlesticks are smoothed using cubic spline fitting with the least square method to ensure a seamless transition.
- **Combined Moving Average**: The indicator combines the digital filter and cubic spline fitting to create a smooth moving average that accurately tracks prices without time lag.
- **Customizable Parameters**: The AFIRMA indicator allows users to adjust the Periods, Taps, and Window parameters to fine-tune the indicator's performance.

## Advantages

- **Accurate Price Tracking**: The AFIRMA indicator provides a smooth and accurate representation of price movement without time lag.
- **Hybrid Approach**: The combination of digital filtering and cubic spline fitting provides a unique and effective approach to moving average calculation.

## Considerations

**Complexity**: The AFIRMA indicator is a complex filter that requires some understanding of digital signal processing and curve fitting to use it right.
- **Parameter Optimization**: Finding the optimal parameters for the AFIRMA indicator may require some experimentation and testing.
- **Computational Resources**: The AFIRMA indicator is  computationally more intensive than traditional moving averages due to the use of cubic spline fitting.