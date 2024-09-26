# The Math Behind DEMA

## Components of DEMA

DEMA is composed of two main components:

1. Exponential Moving Average (EMA)
2. A "double smoothing" factor

Let's break these down:

### EMA Calculation

The Exponential Moving Average (EMA) is calculated as:

$ EMA_t = \alpha \cdot P_t + (1 - \alpha) \cdot EMA_{t-1} $

Where:
- $EMA_t$ is the EMA value at time $t$
- $P_t$ is the price at time $t$
- $\alpha$ is the smoothing factor, calculated as $\frac{2}{n+1}$
- $n$ is the number of periods

### DEMA Formula

The DEMA is then calculated using the following formula:

$ DEMA_t = 2 \cdot EMA_t - EMA(EMA_t) $

Where:
- $DEMA_t$ is the DEMA value at time $t$
- $EMA_t$ is the EMA of the price
- $EMA(EMA_t)$ is the EMA of the EMA

## Calculation Process

1. Calculate the EMA of the price series.
2. Calculate another EMA on the result of step 1.
3. Multiply the first EMA by 2.
4. Subtract the second EMA from the result of step 3.

This process effectively reduces lag while maintaining smoothness.

## Parameter

DEMA uses a single parameter:

- **Period** ($n$): Determines the number of periods used in the EMA calculations. This affects the overall reactivity and smoothness of the indicator.
