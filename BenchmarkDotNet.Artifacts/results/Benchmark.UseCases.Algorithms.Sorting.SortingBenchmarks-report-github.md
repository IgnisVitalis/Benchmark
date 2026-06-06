```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22000.2538/21H2/SunValley)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET SDK 9.0.312
  [Host]   : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method    | N      | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|---------- |------- |-----------:|----------:|---------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| **ArraySort** | **10000**  |   **375.4 μs** | **308.79 μs** | **16.93 μs** |  **1.00** |    **0.05** |   **5.8594** |        **-** |        **-** |  **39.09 KB** |        **1.00** |
| QuickSort | 10000  |   421.0 μs |  39.84 μs |  2.18 μs |  1.12 |    0.04 |   5.8594 |        - |        - |  39.09 KB |        1.00 |
|           |        |            |           |          |       |         |          |          |          |           |             |
| **ArraySort** | **100000** | **4,597.9 μs** |  **77.14 μs** |  **4.23 μs** |  **1.00** |    **0.00** | **117.1875** | **117.1875** | **117.1875** | **390.69 KB** |        **1.00** |
| QuickSort | 100000 | 5,376.0 μs | 513.81 μs | 28.16 μs |  1.17 |    0.01 | 117.1875 | 117.1875 | 117.1875 | 390.69 KB |        1.00 |
