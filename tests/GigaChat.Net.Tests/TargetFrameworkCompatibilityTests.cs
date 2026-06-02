using System.Reflection;
using System.Runtime.Versioning;
using GigaChat.Net.AspNetCore;

namespace GigaChat.Net.Tests;

public class TargetFrameworkCompatibilityTests
{
    [Fact]
    public void TestRunUsesMatchingSupportedTargetFramework()
    {
        var expectedFramework = GetExpectedTargetFramework();

        Assert.Equal(expectedFramework, GetTargetFramework(typeof(TargetFrameworkCompatibilityTests).Assembly));
        Assert.Equal(expectedFramework, GetTargetFramework(typeof(Settings).Assembly));
        Assert.Equal(expectedFramework, GetTargetFramework(typeof(GigaChatOptions).Assembly));
    }

    private static string GetExpectedTargetFramework()
    {
#if NET6_0
        return ".NETCoreApp,Version=v6.0";
#elif NET7_0
        return ".NETCoreApp,Version=v7.0";
#elif NET8_0
        return ".NETCoreApp,Version=v8.0";
#elif NET9_0
        return ".NETCoreApp,Version=v9.0";
#elif NET10_0
        return ".NETCoreApp,Version=v10.0";
#else
        throw new InvalidOperationException("Tests must run on net6.0, net7.0, net8.0, net9.0, or net10.0.");
#endif
    }

    private static string GetTargetFramework(Assembly assembly)
    {
        return assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
            ?? throw new InvalidOperationException($"Assembly {assembly.GetName().Name} does not declare TargetFrameworkAttribute.");
    }
}
