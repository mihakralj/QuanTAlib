### Is QuanTAlib fast?

Well, _no_, but actually *yes*. QuanTAlib works on an additive principle, meaning that even when served a full list of quotes, QuanTAlib will process them one item at the time, rolling forward throug the given time series of data.
- If the last bar is still forming (parameter `update: true`), QuanTAlib can easily recalculates the last entry as often as needed without any need to recalculate the history.
- If a new bar is added to the input, QuanTAlib will process that one item (default parameter `update: false`) and add that one result to the List. No recalculation of the history needed.

So, if you test QuanTAlib with small set of 500 historical bars and calculate EMA(20) on it, the performance of QuanTAlib will be dead last compared to all other TA libraries.

But when the system uses 10,000 or historical bars,  updates the current data at every new tick, and adds a new bar every minute, QuanTAlib has no rivals; all other libraries will need to re-calculate the full length of the indicator on each update/addition to the time series, while QuanTAlib will just update the last entry or add a single new value to the series. No back calculations are performed - ever. Longer the input data series and more updates/additions it gets, the greater  advantage there is for QuanTAlib.

### Are results of QuanTAlib valid?

QuanTAlib includes battery of tests to compare its results with four well-known and reviewed Technical Analysis libraries to assure accuracy and validity of results:

- [TA-LIB](https://www.ta-lib.org/function.html)
- [Skender Stock Indicators](https://dotnet.stockindicators.dev/)
- [Pandas-TA](https://twopirllc.github.io/pandas-ta/)
- [Tulip Indicators](https://tulipindicators.org/)

Not all indicators are implemented by all libraries - and sometimes results of the four reference libraries disagree with each other. Indicators that return equivalent result set to **all four** libraries are marked with ⭐ on [the list of indicators](indicators.md) - these are indicators that can be trusted most. Each verified equivalency of results is marked with ✔️, and each discrepancy is marked with ❌.

For each discrepancy the research was done to establish the reason and to select the implementation that is the most faithful to the original description of the indicator.

_For example, CMO indicator was described by Tushar S. Chande in his book The New Technical Trader, where he includes the example of calculating 10-day CMO on a given data. QuanTAlib, Skender.NET and Tulip libraries can all replicate the results, while TA-LIB and Pandas-TA return something very different:_

| #| Input | **QuanTAlib** | TA-LIB | Skender | Pandas-TA | Tulip |
|--|:--:|:--:|:--:|:--:|:--:|:--:|
| 0| 101.03|**0.00**| NaN| NaN| NaN| NaN|
| 1| 101.03|**0.00**| NaN| NaN| NaN| NaN|
| 2| 101.12|**100.00**| NaN| NaN| NaN| NaN|
| 3| 101.97|**100.00**| NaN| NaN| NaN| NaN|
| 4| 102.78|**100.00**| NaN| NaN| NaN| NaN|
| 5| 103.00|**100.00**| NaN| NaN| NaN| NaN|
| 6| 102.97|**96.87**| NaN| NaN| NaN| NaN|
| 7| 103.06|**97.01**| NaN| NaN| NaN| NaN|
| 8| 102.94|**85.91**| NaN| NaN| NaN| NaN|
| 9| 102.72|**69.23**| NaN| NaN| NaN| NaN|
|10| 102.75|**69.62**| 69.62| 69.62| ~55.22~| 69.62|
|11| 102.91|**71.43**| ~71.62~| 71.43| ~60.09~| 71.43|
|12| 102.97|**71.08**| ~72.42~| 71.08| ~61.93~| 71.08|