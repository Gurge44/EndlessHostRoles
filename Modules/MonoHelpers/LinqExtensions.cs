#if !IL2CPP
using System;
using System.Collections.Generic;

// Since EHR was originally made for net6.0 and uses the newer LINQ methods from it,
// and migrating to netstandard2.1 removed these,
// it's the cleanest and easiest to reimplement these as extension methods.
namespace EHR
{
    public static class LinqExtensions
    {
        // ============================================================
        // MinBy
        // ============================================================

        public static TSource MinBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            return source.MinBy(keySelector, null);
        }

        public static TSource MinBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IComparer<TKey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            using IEnumerator<TSource> enumerator = source.GetEnumerator();

            if (!enumerator.MoveNext())
                throw new InvalidOperationException("Sequence contains no elements.");

            TSource min = enumerator.Current;
            TKey minKey = keySelector(min);

            while (enumerator.MoveNext())
            {
                TSource current = enumerator.Current;
                TKey currentKey = keySelector(current);

                if (comparer.Compare(currentKey, minKey) < 0)
                {
                    min = current;
                    minKey = currentKey;
                }
            }

            return min;
        }

        public static TSource MinBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            return source.MinBy(keySelector, null);
        }

        public static TSource MinBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            Func<TSource, TKey> keySelector,
            IComparer<TKey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            int count = source.Count;

            if (count == 0)
                throw new InvalidOperationException("Sequence contains no elements.");

            TSource min = source[0];
            TKey minKey = keySelector(min);

            for (int i = 1; i < count; i++)
            {
                TSource current = source[i];
                TKey currentKey = keySelector(current);

                if (comparer.Compare(currentKey, minKey) < 0)
                {
                    min = current;
                    minKey = currentKey;
                }
            }

            return min;
        }


        // ============================================================
        // MaxBy
        // ============================================================

        public static TSource MaxBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            return source.MaxBy(keySelector, null);
        }

        public static TSource MaxBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IComparer<TKey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            using IEnumerator<TSource> enumerator = source.GetEnumerator();

            if (!enumerator.MoveNext())
                throw new InvalidOperationException("Sequence contains no elements.");

            TSource max = enumerator.Current;
            TKey maxKey = keySelector(max);

            while (enumerator.MoveNext())
            {
                TSource current = enumerator.Current;
                TKey currentKey = keySelector(current);

                if (comparer.Compare(currentKey, maxKey) > 0)
                {
                    max = current;
                    maxKey = currentKey;
                }
            }

            return max;
        }

        public static TSource MaxBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            return source.MaxBy(keySelector, null);
        }

        public static TSource MaxBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            Func<TSource, TKey> keySelector,
            IComparer<TKey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            int count = source.Count;

            if (count == 0)
                throw new InvalidOperationException("Sequence contains no elements.");

            TSource max = source[0];
            TKey maxKey = keySelector(max);

            for (int i = 1; i < count; i++)
            {
                TSource current = source[i];
                TKey currentKey = keySelector(current);

                if (comparer.Compare(currentKey, maxKey) > 0)
                {
                    max = current;
                    maxKey = currentKey;
                }
            }

            return max;
        }


        // ============================================================
        // IntersectBy
        // ============================================================

        public static IEnumerable<TSource> IntersectBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector)
        {
            return source.IntersectBy(keys, keySelector, null);
        }

        public static IEnumerable<TSource> IntersectBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            return IntersectByIterator(source, keys, keySelector, comparer);
        }

        private static IEnumerable<TSource> IntersectByIterator<TSource, TKey>(
            IEnumerable<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            var set = comparer == null
                ? new HashSet<TKey>()
                : new HashSet<TKey>(comparer);

            foreach (TKey key in keys)
                set.Add(key);

            foreach (TSource item in source)
            {
                if (set.Remove(keySelector(item)))
                    yield return item;
            }
        }

        public static IEnumerable<TSource> IntersectBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector)
        {
            return source.IntersectBy(keys, keySelector, null);
        }

        public static IEnumerable<TSource> IntersectBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            return IntersectByListIterator(source, keys, keySelector, comparer);
        }

        private static IEnumerable<TSource> IntersectByListIterator<TSource, TKey>(
            IReadOnlyList<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            var set = comparer == null
                ? new HashSet<TKey>()
                : new HashSet<TKey>(comparer);

            foreach (TKey key in keys)
                set.Add(key);

            int count = source.Count;

            for (int i = 0; i < count; i++)
            {
                TSource item = source[i];

                if (set.Remove(keySelector(item)))
                    yield return item;
            }
        }


        // ============================================================
        // ExceptBy
        // ============================================================

        public static IEnumerable<TSource> ExceptBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector)
        {
            return source.ExceptBy(keys, keySelector, null);
        }

        public static IEnumerable<TSource> ExceptBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            return ExceptByIterator(source, keys, keySelector, comparer);
        }

        private static IEnumerable<TSource> ExceptByIterator<TSource, TKey>(
            IEnumerable<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            var set = comparer == null
                ? new HashSet<TKey>()
                : new HashSet<TKey>(comparer);

            foreach (TKey key in keys)
                set.Add(key);

            foreach (TSource item in source)
            {
                if (set.Add(keySelector(item)))
                    yield return item;
            }
        }

        public static IEnumerable<TSource> ExceptBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector)
        {
            return source.ExceptBy(keys, keySelector, null);
        }

        public static IEnumerable<TSource> ExceptBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            return ExceptByListIterator(source, keys, keySelector, comparer);
        }

        private static IEnumerable<TSource> ExceptByListIterator<TSource, TKey>(
            IReadOnlyList<TSource> source,
            IEnumerable<TKey> keys,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer)
        {
            var set = comparer == null
                ? new HashSet<TKey>()
                : new HashSet<TKey>(comparer);

            foreach (TKey key in keys)
                set.Add(key);

            int count = source.Count;

            for (int i = 0; i < count; i++)
            {
                TSource item = source[i];

                if (set.Add(keySelector(item)))
                    yield return item;
            }
        }
        
        // =========================
        // DistinctBy
        // =========================

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            var seen = new HashSet<TKey>();

            foreach (var item in source)
            {
                if (seen.Add(keySelector(item)))
                    yield return item;
            }
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IReadOnlyList<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));

            var seen = new HashSet<TKey>();

            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];

                if (seen.Add(keySelector(item)))
                    yield return item;
            }
        }


        // =========================
        // Chunk
        // =========================

        public static IEnumerable<TSource[]> Chunk<TSource>(
            this IEnumerable<TSource> source,
            int size)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            var buffer = new TSource[size];
            int count = 0;

            foreach (var item in source)
            {
                buffer[count++] = item;

                if (count == size)
                {
                    yield return buffer;
                    buffer = new TSource[size];
                    count = 0;
                }
            }

            if (count > 0)
            {
                var last = new TSource[count];
                Array.Copy(buffer, last, count);
                yield return last;
            }
        }

        public static IEnumerable<TSource[]> Chunk<TSource>(
            this IReadOnlyList<TSource> source,
            int size)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            int count = source.Count;

            for (int i = 0; i < count; i += size)
            {
                int chunkSize = Math.Min(size, count - i);
                var chunk = new TSource[chunkSize];

                for (int j = 0; j < chunkSize; j++)
                    chunk[j] = source[i + j];

                yield return chunk;
            }
        }
    }
}
#endif