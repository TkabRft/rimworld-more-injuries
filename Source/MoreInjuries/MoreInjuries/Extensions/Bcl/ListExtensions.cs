using MoreInjuries.Roslyn.Future.Extensions;
using System.Collections.Generic;
using Verse;

namespace MoreInjuries.Extensions.Bcl;

public static class ListExtensions
{
    public static T? SelectRandomOrDefault<T>(this IReadOnlyList<T> list)
    {
        if (list.Count == 0)
        {
            return default;
        }
        return list[Rand.Range(0, list.Count)];
    }
}
