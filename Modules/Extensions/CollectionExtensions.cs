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
    }
}