using System.Collections.Generic;

namespace Core.Extensions;

public static class IEnumerableExtensions
{
    public static IEnumerable<(T?, T?)> Pairwise<T>(this IEnumerable<T> source)
    {
        T? previous = default;
        using IEnumerator<T> it = source.GetEnumerator();

        if (it.MoveNext())
            previous = it.Current;

        while (it.MoveNext())
            yield return (previous, previous = it.Current);
    }
}
