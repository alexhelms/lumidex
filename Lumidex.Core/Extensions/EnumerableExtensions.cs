namespace System.Collections.Generic;

public static class EnumerableExtensions
{
    public static IEnumerable<T> IgnoreExceptions<T>(this IEnumerable<T> source)
    {
        var enumerator = source.GetEnumerator();
        bool? hasCurrent = null;

        do
        {
            try
            {
                hasCurrent = enumerator.MoveNext();
            }
            catch (Exception)
            {
                hasCurrent = null;
            }

            if (hasCurrent ?? false)
            {
                yield return enumerator.Current;
            }
        } while (hasCurrent ?? true);
    }
}
