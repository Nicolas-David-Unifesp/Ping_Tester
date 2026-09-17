using System;
using System.Linq;
using System.Reflection;

namespace PingTester.Core.Tests;

/// <summary>
/// Discovers every [Test]-annotated method in this assembly, runs it, and
/// reports pass/fail. Returns a non-zero exit code if any test fails so that
/// `dotnet run` can be used as a CI-style gate.
/// </summary>
public static class Runner
{
    public static int Main()
    {
        var testMethods = Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
            .OrderBy(m => m.DeclaringType!.Name)
            .ThenBy(m => m.Name)
            .ToList();

        int passed = 0;
        int failed = 0;
        string? currentClass = null;

        Console.WriteLine($"Running {testMethods.Count} test(s)...\n");

        foreach (var method in testMethods)
        {
            var className = method.DeclaringType!.Name;
            if (className != currentClass)
            {
                currentClass = className;
                Console.WriteLine($"  {className}");
            }

            var attr = method.GetCustomAttribute<TestAttribute>();
            var displayName = attr?.Name ?? method.Name;

            try
            {
                var instance = Activator.CreateInstance(method.DeclaringType!);
                method.Invoke(instance, null);
                Console.WriteLine($"    [PASS] {displayName}");
                passed++;
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                Console.WriteLine($"    [FAIL] {displayName}");
                Console.WriteLine($"           {tie.InnerException.Message}");
                failed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [FAIL] {displayName}");
                Console.WriteLine($"           {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"==== {passed} passed, {failed} failed ====");
        return failed == 0 ? 0 : 1;
    }
}
