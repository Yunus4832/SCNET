namespace Game.Utils;

public static class CollectionUtils
{
    public static T ElementAt<T, TE>(TE enumerator, int index) where TE : IEnumerator<T>
    {
        var num = 0;
        do
        {
            if (!enumerator.MoveNext())
            {
                throw new IndexOutOfRangeException("ElementAt() index out of range.");
            }

            num++;
        } while (num <= index);

        return enumerator.Current;
    }

    public static void RandomShuffle<T>(this IList<T> list, Func<int, int> random)
    {
        for (var num = list.Count - 1; num > 0; num--)
        {
            var index = random(num + 1);
            (list[index], list[num]) = (list[num], list[index]);
        }
    }

    public static int FirstIndex<T>(this IEnumerable<T> collection, T value)
    {
        var num = 0;
        foreach (var item in collection)
        {
            if (Equals(item, value))
            {
                return num;
            }

            num++;
        }

        return -1;
    }

    public static int FirstIndex<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
    {
        var num = 0;
        foreach (var item in collection)
        {
            if (predicate(item))
            {
                return num;
            }

            num++;
        }

        return -1;
    }

    public static T SelectNth<T>(this IList<T> list, int n, IComparer<T> comparer)
    {
        if (list == null || list.Count <= n)
        {
            throw new ArgumentException();
        }

        var num = 0;
        var num2 = list.Count - 1;
        while (num < num2)
        {
            var num3 = num;
            var num4 = num2;
            var y = list[(num3 + num4) / 2];
            while (num3 < num4)
            {
                if (comparer.Compare(list[num3], y) >= 0)
                {
                    (list[num4], list[num3]) = (list[num3], list[num4]);
                    num4--;
                }
                else
                {
                    num3++;
                }
            }

            if (comparer.Compare(list[num3], y) > 0)
            {
                num3--;
            }

            if (n <= num3)
            {
                num2 = num3;
            }
            else
            {
                num = num3 + 1;
            }
        }

        return list[n];
    }
}
