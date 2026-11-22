namespace Game.Utils;

public static class CollectionUtils
{
    public static T ElementAt<T, TE>(TE enumerator, int index) where TE : IEnumerator<T>
    {
        var currentIndex = 0;
        do
        {
            if (!enumerator.MoveNext())
            {
                throw new IndexOutOfRangeException("ElementAt() index out of range.");
            }

            currentIndex++;
        } while (currentIndex <= index);

        return enumerator.Current;
    }

    public static void RandomShuffle<T>(this IList<T> list, Func<int, int> random)
    {
        for (var lastIndex = list.Count - 1; lastIndex > 0; lastIndex--)
        {
            var index = random(lastIndex + 1);
            (list[index], list[lastIndex]) = (list[lastIndex], list[index]);
        }
    }

    public static int FirstIndex<T>(this IEnumerable<T> collection, T value)
    {
        var currentIndex = 0;
        foreach (var item in collection)
        {
            if (Equals(item, value))
            {
                return currentIndex;
            }

            currentIndex++;
        }

        return -1;
    }

    public static int FirstIndex<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
    {
        var currentIndex = 0;
        foreach (var item in collection)
        {
            if (predicate(item))
            {
                return currentIndex;
            }

            currentIndex++;
        }

        return -1;
    }

    public static T SelectNth<T>(this IList<T> list, int n, IComparer<T> comparer)
    {
        if (list == null || list.Count <= n)
        {
            throw new ArgumentException();
        }

        var left = 0;
        var right = list.Count - 1;
        while (left < right)
        {
            var leftScan = left;
            var rightScan = right;
            var y = list[(leftScan + rightScan) / 2];
            while (leftScan < rightScan)
            {
                if (comparer.Compare(list[leftScan], y) >= 0)
                {
                    (list[rightScan], list[leftScan]) = (list[leftScan], list[rightScan]);
                    rightScan--;
                }
                else
                {
                    leftScan++;
                }
            }

            if (comparer.Compare(list[leftScan], y) > 0)
            {
                leftScan--;
            }

            if (n <= leftScan)
            {
                right = leftScan;
            }
            else
            {
                left = leftScan + 1;
            }
        }

        return list[n];
    }
}
