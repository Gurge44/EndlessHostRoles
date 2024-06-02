using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable ConvertIfStatementToReturnStatement

namespace EHR
{
    public static class CollectionExtensions
    {
        /// <summary>
        /// Returns the key of a dictionary by its value
        /// </summary>
        /// <param name="dictionary">The <see cref="Dictionary{TKey,TValue}"/> to search</param>
        /// <param name="value">The <typeparamref name="TValue"/> used to search for the corresponding key</param>
        /// <typeparam name="TKey">The type of the keys in the <paramref name="dictionary"/></typeparam>
        /// <typeparam name="TValue">The type of the values in the <paramref name="dictionary"/></typeparam>
        /// <returns>The key of the <paramref name="dictionary"/> that corresponds to the given <paramref name="value"/>, or the default value of <typeparamref name="TKey"/> if the <paramref name="value"/> is not found in the <paramref name="dictionary"/></returns>
        public static TKey GetKeyByValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TValue value)
        {
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            {
                if (pair.Value.Equals(value))
                {
                    return pair.Key;
                }
            }

            return default;
        }

        /// <summary>
        /// Returns a random element from a collection
        /// </summary>
        /// <param name="collection">The collection</param>
        /// <typeparam name="T">The type of the collection</typeparam>
        /// <returns>A random element from the collection, or the default value of <typeparamref name="T"/> if the collection is empty</returns>
        public static T RandomElement<T>(this IList<T> collection)
        {
            if (collection.Count == 0) return default;
            return collection[IRandom.Instance.Next(collection.Count)];
        }

        /// <summary>
        /// Shuffles all elements in a collection randomly
        /// </summary>
        /// <typeparam name="T">The type of the collection</typeparam>
        /// <param name="collection">The collection to be shuffled</param>
        /// <returns>The shuffled collection as a <see cref="List{T}"/></returns>
        public static List<T> Shuffle<T>(this IEnumerable<T> collection)
        {
            var list = collection.ToList();
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

        /// <summary>
        /// Combines multiple collections into a single collection
        /// </summary>
        /// <param name="firstCollection">The collection to start with</param>
        /// <param name="collections">The other collections to add to <paramref name="firstCollection"/></param>
        /// <typeparam name="T">The type of the elements in the collections to combine</typeparam>
        /// <returns>A collection containing all elements of <paramref name="firstCollection"/> and all <paramref name="collections"/></returns>
        public static IEnumerable<T> CombineWith<T>(this IEnumerable<T> firstCollection, params IEnumerable<T>[] collections)
        {
            return firstCollection.Concat(collections.SelectMany(x => x));
        }

        /// <summary>
        /// Executes an action for each element in a collection
        /// </summary>
        /// <param name="collection">The collection to iterate over</param>
        /// <param name="action">The action to execute for each element</param>
        /// <typeparam name="T">The type of the elements in the collection</typeparam>
        public static void Do<T>(this IEnumerable<T> collection, System.Action<T> action)
        {
            foreach (T element in collection)
            {
                action(element);
            }
        }

        /// <summary>
        /// Executes an action for each element in a collection if the predicate is true
        /// </summary>
        /// <param name="collection">The collection to iterate over</param>
        /// <param name="predicate">The predicate to check for each element</param>
        /// <param name="action">The action to execute for each element that satisfies the predicate</param>
        /// <typeparam name="T">The type of the elements in the collection</typeparam>
        public static void DoIf<T>(this IEnumerable<T> collection, System.Func<T, bool> predicate, System.Action<T> action)
        {
            var partitioner = Partitioner.Create(collection.Where(predicate));
            foreach (T element in partitioner.GetDynamicPartitions())
            {
                action(element);
            }
        }

        /// <summary>
        /// Removes an element from a collection
        /// </summary>
        /// <param name="collection">The collection to remove the element from</param>
        /// <param name="element">The element to remove</param>
        /// <typeparam name="T">The type of the elements in the collection</typeparam>
        /// <returns>A collection containing all elements of <paramref name="collection"/> except for <paramref name="element"/></returns>
        public static IEnumerable<T> Without<T>(this IEnumerable<T> collection, T element)
        {
            return collection.Where(x => !x.Equals(element));
        }

        /// <summary>
        /// Partitions a collection into a specified number of parts
        /// </summary>
        /// <param name="collection">The collection to partition</param>
        /// <param name="parts">The number of parts to partition the collection into</param>
        /// <typeparam name="T">The type of the elements in the collection</typeparam>
        /// <returns>A collection of collections, each containing a part of the original collection</returns>
        public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> collection, int parts)
        {
            var list = collection.ToList();
            int size = list.Count / parts;
            int remainder = list.Count % parts;
            int index = 0;
            for (int i = 0; i < parts; i++)
            {
                int partSize = size + (i < remainder ? 1 : 0);
                yield return list.GetRange(index, partSize);
                index += partSize;
            }
        }

        /// <summary>
        /// Partitions an array into a specified number of parts
        /// </summary>
        /// <param name="collection">The array to partition</param>
        /// <param name="parts">The number of parts to partition the array into</param>
        /// <typeparam name="T">The type of the elements in the array</typeparam>
        /// <returns>An array of arrays, each containing a part of the original array</returns>
        public static IEnumerable<IEnumerable<T>> Partition<T>(this T[] collection, int parts)
        {
            int size = collection.Length / parts;
            int remainder = collection.Length % parts;
            int index = 0;
            for (int i = 0; i < parts; i++)
            {
                int partSize = size + (i < remainder ? 1 : 0);
                yield return collection.Skip(index).Take(partSize);
                index += partSize;
            }
        }
    }
}