using System;
using System.Collections.Generic;

namespace BedrockLauncher
{
    public static class CollectionExtensions
    {
        public static int FindIndex<T>(this IList<T> list, Predicate<T> match)
        {
            for (int i = 0; i < list.Count; i++)
                if (match(list[i])) return i;
            return -1;
        }
    }
}
