using System;

namespace CryptoApp.Services.Interfaces;

public interface IBenchmarkService
{
    void StartTimeBenchmark();
    void StopTimeBenchmark();
    TimeSpan GetResult();
}