using System;
using System.Diagnostics;
using CryptoApp.Services.Interfaces;
using Serilog;

namespace CryptoApp.Services.Implementations;

public class BenchmarkService : IBenchmarkService
{
    private Stopwatch _stopwatch = new();

    public void StartTimeBenchmark()
    {
        _stopwatch.Reset();
        _stopwatch.Start();
    }

    public void StopTimeBenchmark()
    {
        _stopwatch.Stop();
    }

    public TimeSpan GetResult()
    {
        return _stopwatch.Elapsed;
    }
}