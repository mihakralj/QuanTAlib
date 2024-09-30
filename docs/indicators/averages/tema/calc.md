# The Math Behind TEMA

## Components of TEMA

TEMA is composed of three main components:

1. Exponential Moving Average (EMA)
2. A "triple smoothing" factor

Let's break these down:

### EMA Calculation

The Exponential Moving Average (EMA) is calculated as:

$ EMA_t = \alpha \cdot P_t + (1 - \alpha) \cdot EMA_{t-1} $

Where:
- $EMA_t$ is the EMA value at time $t$
- $P_t$ is the price at time $t$
- $\alpha$ is the smoothing factor, calculated as $\frac{2}{n+1}$
- $n$ is the number of periods

### TEMA Formula

The TEMA is then calculated using the following formula:

$ TEMA_t = 3 \cdot EMA_t - 3 \cdot EMA(EMA_t) + EMA(EMA(EMA_t)) $

Where:
- $TEMA_t$ is the TEMA value at time $t$
- $EMA_t$ is the EMA of the price
- $EMA(EMA_t)$ is the EMA of the EMA
- $EMA(EMA(EMA_t))$ is the EMA of the EMA of the EMA

## Calculation Process

1. Calculate the EMA of the price series (EMA1).
2. Calculate another EMA on the result of step 1 (EMA2).
3. Calculate a third EMA on the result of step 2 (EMA3).
4. Multiply EMA1 by 3.
5. Multiply EMA2 by 3.
6. Subtract EMA2 * 3 from EMA1 * 3.
7. Add EMA3 to the result.

This process effectively reduces lag while maintaining smoothness and attempting to minimize overshooting.

## Parameter

TEMA uses a single parameter:

- **Period** ($n$): Determines the number of periods used in the EMA calculations. This affects the overall reactivity and smoothness of the indicator.