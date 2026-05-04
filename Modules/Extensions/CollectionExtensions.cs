using System;
using System.Collections.Generic;
using System.Linq;
using Hazel;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ForCanBeConvertedToForeach

namespace EHR;

public static class CollectionExtensions
{
    /// <param name="dictionary">The <see cref="Dictionary{TKey,TValue}" /> to search</param>
    /// <typeparam name="TKey">The type of the keys in the <paramref name="dictionary" /></typeparam>
    /// <typeparam name="TValue">The type of the values in the <paramref name="dictionary" /></typeparam>
    extension<TKey, TValue>(Dictionary<TKey, TValue> dictionary)
    {
        /// <summary>
        ///     Returns the key of a dictionary by its value
        /// </summary>
        /// <param name="value">The <typeparamref name="TValue" /> used to search for the corresponding key</param>
        /// <returns>
        ///     The key of the <paramref name="dictionary" /> that corresponds to the given <paramref name="value" />, or the
        ///     default value of <typeparamref name="TKey" /> if the <paramref name="value" /> is not found in the
        ///     <paramref name="dictionary" />
        /// </returns>
        public TKey GetKeyByValue(TValue value)
        {
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            {
                if (pair.Value.Equals(value))
                    return pair.Key;
            }

            return default(TKey);
        }

        /// <summary>
        ///     Sets the value for all existing keys in a dictionary to a specific value
        /// </summary>
        /// <param name="value"></param>
        public void SetAllValues(TValue value)
        {
            foreach (TKey key in dictionary.Keys.ToArray())
                dictionary[key] = value;
        }

        /// <summary>
        ///     Adjusts the value for all existing keys in a dictionary
        /// </summary>
        /// <param name="adjust">The function to adjust the values with</param>
        public void AdjustAllValues(Func<TValue, TValue> adjust)
        {
            foreach (TKey key in dictionary.Keys.ToArray())
                dictionary[key] = adjust(dictionary[key]);
        }

        /// <summary>
        ///     Adds a range of elements to a dictionary
        /// </summary>
        /// <param name="other">The dictionary containing the elements to add</param>
        /// <param name="overrideExistingKeys">
        ///     Whether to override existing keys in the <paramref name="dictionary" /> with the
        ///     same keys in the <paramref name="other" /> dictionary. If <c>true</c>, the same keys in the
        ///     <paramref name="dictionary" /> will be overwritten with the values from the <paramref name="other" /> dictionary.
        ///     If <c>false</c>, the same keys in the <paramref name="dictionary" /> will be kept and the values from the
        ///     <paramref name="other" /> dictionary will be ignored
        /// </param>
        /// <returns>The <paramref name="dictionary" /> with the elements from the <paramref name="other" /> dictionary added</returns>
        public Dictionary<TKey, TValue> AddRange(Dictionary<TKey, TValue> other, bool overrideExistingKeys = true)
        {
            foreach ((TKey key, TValue value) in other)
            {
                if (overrideExistingKeys || !dictionary.ContainsKey(key))
                    dictionary[key] = value;
            }

            return dictionary;
        }
    }

    /// <param name="collection">The collection</param>
    /// <typeparam name="T">The type of the collection</typeparam>
    extension<T>(IEnumerable<T> collection)
    {
        /// <summary>
        ///     Returns a random element from a collection
        /// </summary>
        /// <returns>
        ///     A random element from the collection, or the default value of <typeparamref name="T" /> if the collection is
        ///     empty
        /// </returns>
        public T RandomElement()
        {
            if (collection is IReadOnlyList<T> list) return list.RandomElement();
            return collection.ToList().RandomElement();
        }

        /// <summary>
        ///     Combines multiple collections into a single collection
        /// </summary>
        /// <param name="collections">The other collections to add to <paramref name="collection" /></param>
        /// <returns>
        ///     A collection containing all elements of <paramref name="collection" /> and all
        ///     <paramref name="collections" />
        /// </returns>
        public IEnumerable<T> CombineWith(params IEnumerable<T>[] collections)
        {
            return collection.Concat(collections.Flatten());
        }

        /// <summary>
        ///     Executes an action for each element in a collection
        /// </summary>
        /// <param name="action">The action to execute for each element</param>
        [Annotations.CollectionAccess(Annotations.CollectionAccessType.Read)]
        public void Do([Annotations.InstantHandle] Action<T> action)
        {
            if (collection is List<T> list)
            {
                for (var i = 0; i < list.Count; i++) action(list[i]);

                return;
            }

            foreach (T element in collection) action(element);
        }

        /// <summary>
        ///     Executes an action for each element in a collection if the predicate is true
        /// </summary>
        /// <param name="fast">Whether to use a fast loop or linq</param>
        /// <param name="predicate">The predicate to check for each element</param>
        /// <param name="action">The action to execute for each element that satisfies the predicate</param>
        [Annotations.CollectionAccess(Annotations.CollectionAccessType.Read)]
        public void DoIf(Func<T, bool> predicate, [Annotations.InstantHandle] Action<T> action, bool fast = true)
        {
            if (fast)
            {
                if (collection is List<T> list)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        T element = list[i];
                        if (predicate(element)) action(element);
                    }

                    return;
                }

                foreach (T element in collection)
                {
                    if (predicate(element))
                        action(element);
                }

                return;
            }

            collection.Where(predicate).ToArray().Do(action);
        }

        /// <summary>
        ///     Splits a collection into two collections based on a predicate
        /// </summary>
        /// <param name="predicate">The predicate to split the collection by</param>
        /// <returns>
        ///     A tuple containing two collections: one with elements that satisfy the predicate, and one with elements that
        ///     do not
        /// </returns>
        public (List<T> TrueList, List<T> FalseList) Split(Func<T, bool> predicate)
        {
            var list1 = new List<T>();
            var list2 = new List<T>();

            foreach (T element in collection)
            {
                if (predicate(element))
                    list1.Add(element);
                else
                    list2.Add(element);
            }

            return (list1, list2);
        }

        /// <summary>
        ///     Determines whether a collection contains any elements that satisfy a predicate and returns the first element that
        ///     satisfies the predicate
        /// </summary>
        /// <param name="predicate">The predicate to check for each element</param>
        /// <param name="element">
        ///     The first element that satisfies the predicate, or the default value of <typeparamref name="T" />
        ///     if no elements satisfy the predicate
        /// </param>
        /// <returns><c>true</c> if the collection contains any elements that satisfy the predicate, <c>false</c> otherwise</returns>
        [Annotations.CollectionAccess(Annotations.CollectionAccessType.Read)]
        public bool FindFirst([Annotations.InstantHandle] Func<T, bool> predicate, out T element)
        {
            if (collection is List<T> list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    T item = list[i];

                    if (predicate(item))
                    {
                        element = item;
                        return true;
                    }
                }

                element = default(T);
                return false;
            }

            foreach (T item in collection)
            {
                if (predicate(item))
                {
                    element = item;
                    return true;
                }
            }

            element = default(T);
            return false;
        }

        /// <summary>
        /// Takes the specified number of random elements from the collection to a List, and yields its elements for enumeration.
        /// </summary>
        /// <param name="count">The number of random elements to pick.</param>
        /// <returns>A new collection with the specified number of random elements from the original collection.</returns>
        public IEnumerable<T> TakeRandom(int count)
        {
            if (collection == null || count <= 0)
                yield break;

            var reservoir = new List<T>(count);
            int i = 0;

            foreach (var item in collection)
            {
                if (i < count)
                {
                    reservoir.Add(item);
                }
                else
                {
                    int j = IRandom.Instance.Next(i + 1);
                    if (j < count)
                        reservoir[j] = item;
                }
                i++;
            }

            // yield instead of returning list to avoid extra allocation
            foreach (var item in reservoir)
                yield return item;
        }

        /// <summary>
        /// Takes the specified number of random elements from the collection, and adds them to a new List.
        /// </summary>
        /// <param name="count">The number of random elements to pick.</param>
        /// <returns>A new List with the specified number of random elements from the original collection.</returns>
        public List<T> TakeRandomToList(int count)
        {
            if (collection == null || count <= 0)
                return [];

            var reservoir = new List<T>(count);
            int i = 0;

            foreach (var item in collection)
            {
                if (i < count)
                {
                    reservoir.Add(item);
                }
                else
                {
                    int j = IRandom.Instance.Next(i + 1);
                    if (j < count)
                        reservoir[j] = item;
                }
                i++;
            }

            return reservoir;
        }

        /// <summary>
        ///     Partitions a collection into a specified number of parts
        /// </summary>
        /// <param name="parts">The number of parts to partition the collection into</param>
        /// <returns>A collection of collections, each containing a part of the original collection</returns>
        public IEnumerable<IEnumerable<T>> Partition(int parts)
        {
            List<T> list = collection.ToList();
            int length = list.Count;
            if (parts <= 0 || length == 0) yield break;

            if (parts > length) parts = length;

            int size = length / parts;
            int remainder = length % parts;
            var index = 0;

            for (var i = 0; i < parts; i++)
            {
                int partSize = size + (i < remainder ? 1 : 0);
                yield return list.Skip(index).Take(partSize);
                index += partSize;
            }
        }

        /// <summary>
        ///     Removes an element from a collection
        /// </summary>
        /// <param name="element">The element to remove</param>
        /// <returns>
        ///     A collection containing all elements of <paramref name="collection" /> except for <paramref name="element" />
        /// </returns>
        public IEnumerable<T> Without(T element)
        {
            return collection.Where(x => !x.Equals(element));
        }

        /// <summary>
        ///     Shuffles all elements in a collection randomly
        /// </summary>
        /// <returns>A new, shuffled collection as a <see cref="List{T}" /></returns>
        public List<T> Shuffle()
        {
            if (collection is not List<T> list)
                list = collection.ToList();
        
            int n = list.Count;
            var r = IRandom.Instance;

            while (n > 1)
            {
                n--;
                int k = r.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }

            return list;
        }
    }

    /// <param name="collection">The collection</param>
    /// <typeparam name="T">The type of the collection</typeparam>
    extension<T>(IReadOnlyList<T> collection)
    {
        /// <summary>
        ///     Partitions a list into a specified number of parts
        /// </summary>
        /// <param name="parts">The number of parts to partition the list into</param>
        /// <returns>A list of lists, each containing a part of the original list</returns>
        public IEnumerable<IEnumerable<T>> Partition(int parts)
        {
            int length = collection.Count;
            if (parts <= 0 || length == 0) yield break;

            if (parts > length) parts = length;

            int size = length / parts;
            int remainder = length % parts;
            var index = 0;

            for (var i = 0; i < parts; i++)
            {
                int partSize = size + (i < remainder ? 1 : 0);
                yield return collection.Skip(index).Take(partSize);
                index += partSize;
            }
        }

        /// <summary>
        /// Takes the specified number of random elements from the collection.
        /// </summary>
        /// <param name="count">The number of random elements to pick.</param>
        /// <returns>A new collection with the specified number of random elements from the original collection.</returns>
        public IEnumerable<T> TakeRandom(int count)
        {
            if (collection == null || collection.Count == 0 || count <= 0)
                yield break;

            int n = collection.Count;

            // If asking for >= all elements, just return everything
            if (count >= n)
            {
                for (int i = 0; i < n; i++)
                    yield return collection[i];
                yield break;
            }

            // If k is small relative to n → pick unique indices via HashSet
            // If k is large → invert selection (pick excluded indices instead)
            if (count <= n / 2)
            {
                var chosen = new HashSet<int>();
                while (chosen.Count < count)
                {
                    int idx = IRandom.Instance.Next(n);
                    if (chosen.Add(idx))
                        yield return collection[idx];
                }
            }
            else
            {
                // More efficient to exclude (n - count) items
                int excludeCount = n - count;
                var excluded = new HashSet<int>();

                while (excluded.Count < excludeCount)
                    excluded.Add(IRandom.Instance.Next(n));

                for (int i = 0; i < n; i++)
                {
                    if (!excluded.Contains(i))
                        yield return collection[i];
                }
            }
        }

        /// <summary>
        ///     Returns a random element from a collection
        /// </summary>
        /// <returns>
        ///     A random element from the collection, or the default value of <typeparamref name="T" /> if the collection is
        ///     empty
        /// </returns>
        public T RandomElement()
        {
            if (collection.Count == 0) return default(T);
            return collection[IRandom.Instance.Next(collection.Count)];
        }
    }

    /// <summary>
    ///     Flattens a collection of collections into a single collection
    /// </summary>
    /// <param name="collection">The collection of collections to flatten</param>
    /// <typeparam name="T">The type of the elements in the collections</typeparam>
    /// <returns>A single collection containing all elements of the collections in <paramref name="collection" /></returns>
    public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> collection)
    {
        return collection.SelectMany(x => x);
    }

    public static void NotifyPlayers(this IEnumerable<PlayerControl> players, string text, float time = 6f, bool overrideAll = false, bool log = true, bool setName = true)
    {
        var sender = CustomRpcSender.Create("NotifyPlayers", SendOption.Reliable);
        var hasValue = false;

        foreach (PlayerControl player in players)
        {
            hasValue |= sender.Notify(player, text, time, overrideAll, log, setName);

            if (sender.stream.Length > 500)
            {
                sender.SendMessage();
                sender = CustomRpcSender.Create("NotifyPlayers", SendOption.Reliable);
                hasValue = false;
            }
        }

        sender.SendMessage(dispose: !hasValue);
    }
    
    #region ToValidPlayers

    /// <summary>
    ///     Converts a collection of player IDs to a collection of <see cref="PlayerControl" /> instances
    /// </summary>
    /// <param name="playerIds"></param>
    /// <returns></returns>
    public static IEnumerable<PlayerControl> ToValidPlayers(this IEnumerable<byte> playerIds)
    {
        return playerIds.Select(Utils.GetPlayer).Where(x => x);
    }

    /// <summary>
    ///     Converts a list of player IDs to a list of <see cref="PlayerControl" /> instances
    /// </summary>
    /// <param name="playerIds"></param>
    /// <returns></returns>
    public static List<PlayerControl> ToValidPlayers(this List<byte> playerIds)
    {
        return playerIds.ConvertAll(Utils.GetPlayer).FindAll(x => x);
    }
    
    #endregion
    
    #region Without

    /// <summary>
    ///     Removes an element from a collection
    /// </summary>
    /// <param name="collection">The collection to remove the element from</param>
    /// <param name="element">The element to remove</param>
    /// <returns>
    ///     A collection containing all elements of <paramref name="collection" /> except for <paramref name="element" />
    /// </returns>
    public static IEnumerable<PlayerControl> Without(this IEnumerable<PlayerControl> collection, PlayerControl element)
    {
        return collection.Where(x => x.PlayerId != element.PlayerId);
    }

    /// <summary>
    ///     Removes an element from a collection
    /// </summary>
    /// <param name="collection">The collection to remove the element from</param>
    /// <param name="element">The element to remove</param>
    /// <returns>
    ///     A collection containing all elements of <paramref name="collection" /> except for <paramref name="element" />
    /// </returns>
    public static IEnumerable<PlainShipRoom> Without(this IEnumerable<PlainShipRoom> collection, PlainShipRoom element)
    {
        return collection.Where(x => x != element);
    }
    
    #endregion
    
    #region Shuffle

    /// <summary>
    ///     Shuffles all elements in a collection randomly
    /// </summary>
    /// <param name="collection">The collection to be shuffled</param>
    /// <typeparam name="T">The type of the collection</typeparam>
    /// <returns>The same collection with its elements shuffled</returns>
    public static List<T> Shuffle<T>(this List<T> collection)
    {
        int n = collection.Count;
        var r = IRandom.Instance;

        while (n > 1)
        {
            n--;
            int k = r.Next(n + 1);
            (collection[n], collection[k]) = (collection[k], collection[n]);
        }

        return collection;
    }

    /// <summary>
    ///     Shuffles all elements in an array randomly
    /// </summary>
    /// <param name="collection">The array to be shuffled</param>
    /// <typeparam name="T">The type of the array</typeparam>
    /// <returns>The same array with its elements shuffled</returns>
    public static T[] Shuffle<T>(this T[] collection)
    {
        int n = collection.Length;
        var r = IRandom.Instance;

        while (n > 1)
        {
            n--;
            int k = r.Next(n + 1);
            (collection[n], collection[k]) = (collection[k], collection[n]);
        }

        return collection;
    }

    /// <summary>
    ///     Shuffles all elements in an IList randomly
    /// </summary>
    /// <param name="collection">The IList to be shuffled</param>
    /// <typeparam name="T">The type of the IList</typeparam>
    /// <returns>The same IList with its elements shuffled</returns>
    public static IList<T> Shuffle<T>(this IList<T> collection)
    {
        int n = collection.Count;
        var r = IRandom.Instance;

        while (n > 1)
        {
            n--;
            int k = r.Next(n + 1);
            (collection[n], collection[k]) = (collection[k], collection[n]);
        }

        return collection;
    }
    
    #endregion
}

public static class Loop
{
    public static void Times(int count, Action<int> action)
    {
        for (var i = 0; i < count; i++) action(i);
    }
}