using System;
using Splat;

namespace CryptoApp.IoC.Extensions;

public static class GetRequiredServiceExtension
{
    public static TService GetRequiredService<TService>(this IReadonlyDependencyResolver resolver)
    {
        var service = resolver.GetService<TService>();
        if (service is null) // Splat is not able to resolve type for us
        {
            throw new InvalidOperationException($"Failed to resolve object of type {typeof(TService)}"); // throw error with detailed description
        }

        return service; // return instance if not null
    }
}