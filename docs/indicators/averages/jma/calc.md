# JMA Calculation

### Initial Parameters:

$\beta = factor \cdot \frac{period - 1}{factor \cdot (period - 1) + 2}$

$len1 = \frac{\ln(\sqrt{period - 1})}{\ln(2)} + 2$

$pow1 = \max(len1 - 2, 0.5)$

$phase \in [0.5, 2.5]$ (clamped to $(phase \cdot 0.01) + 1.5$)

### Volatility Calculations:

$del1_t = price_t - upperBand_{t-1}$

$del2_t = price_t - lowerBand_{t-1}$

$volty_t = \max(|del1_t|, |del2_t|)$

$vSum_t = \frac{\sum_{i=t-buffer+1}^t volty_i}{buffer}$

$avgVolty_t = \text{mean}(vSum_{t-64:t})$

$rVolty_t = \text{clamp}(\frac{volty_t}{avgVolty_t}, 1, len1^{1/pow1})$

### Band Calculations:

$pow2_t = rVolty_t^{pow1}$

$K_v = \beta^{\sqrt{pow2_t}}$


$upperBand_t = price_t - K_v \cdot del1_t$



$\alpha_t = \beta^{pow2_t}$

$ma1_t = price_t + \alpha_t(ma1_{t-1} - price_t)$

$det0_t = price_t + \beta(det0_{t-1} - price_t + ma1_t) - ma1_t$

$ma2_t = ma1_t + phase \cdot det0_t$

$det1_t = (ma2_t - jma_{t-1})(1-\alpha_t)^2 + \alpha_t^2 \cdot det1_{t-1}$

$jma_t = jma_{t-1} + det1_t$