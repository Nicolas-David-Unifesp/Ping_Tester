using System;
using System.Collections.Generic;
using System.Linq;

namespace PingTester.Core.Tests;

/// <summary>
/// Minimal zero-dependency test framework.
///
/// This exists ONLY because this sandbox has no network access to restore
/// xUnit from NuGet. It intentionally mimics the shape of xUnit assertions
/// (AssertEqual/AssertTrue/AssertThrows) so the test bodies can be migrated
/// to xUnit on a Windows machine with almost no changes:
///   - replace [Test] with [Fact]
///   - replace Assert.Equal(...) calls (names already match)
///   - delete this file and the Runner
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
    public string? Name { get; }
    public TestAttribute(string? name = null) => Name = name;
}

public sealed class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}

public static class Assert
{
    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"Expected: <{Format(expected)}>  Actual: <{Format(actual)}>");
    }

    public static void True(bool condition, string? because = null)
    {
        if (!condition)
            throw new AssertionException($"Expected condition to be TRUE. {because}");
    }

    public static void False(bool condition, string? because = null)
    {
        if (condition)
            throw new AssertionException($"Expected condition to be FALSE. {because}");
    }

    public static void Null(object? value)
    {
        if (value is not null)
            throw new AssertionException($"Expected null but got <{Format(value)}>");
    }

    public static void NotNull(object? value)
    {
        if (value is null)
            throw new AssertionException("Expected non-null but got null");
    }

    public static void Empty<T>(IEnumerable<T> collection)
    {
        if (collection.Any())
            throw new AssertionException($"Expected empty collection but had {collection.Count()} item(s)");
    }

    public static void Count<T>(int expected, IEnumerable<T> collection)
    {
        var actual = collection.Count();
        if (actual != expected)
            throw new AssertionException($"Expected collection count <{expected}> but was <{actual}>");
    }

    public static void Contains<T>(T expected, IEnumerable<T> collection)
    {
        if (!collection.Contains(expected))
            throw new AssertionException($"Expected collection to contain <{Format(expected)}>");
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionException($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
        }
        throw new AssertionException($"Expected {typeof(TException).Name} but no exception was thrown");
    }

    private static string Format(object? value) => value?.ToString() ?? "null";
}
