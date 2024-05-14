using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

// ReSharper disable InconsistentNaming

namespace EHR
{
    public static class CollectionExtensions
    {
        /// <summary>
        /// Returns the key of a dictionary by its value
        /// </summary>
        /// <param name="dict">The dictionary</param>
        /// <param name="value">The value used to search for the corresponding key</param>
        /// <typeparam name="K">The Key type of the dictionary</typeparam>
        /// <typeparam name="V">The Value type of the dictionary</typeparam>
        /// <returns>The key of the dictionary that corresponds to the value, or the default value of <typeparamref name="K"/> if the value is not found</returns>
        public static K GetKeyByValue<K, V>(this Dictionary<K, V> dict, V value)
        {
            foreach (KeyValuePair<K, V> pair in dict)
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
        /// <param name="random">An instance of a randomizer algorithm</param>
        /// <returns>The shuffled collection</returns>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> collection, IRandom random)
        {
            var list = collection.ToList();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }

            return list;
        }
    }

    // Credit: https://github.com/dabao40/TheOtherRolesGMIA/blob/main/TheOtherRoles/Utilities/EnumerationHelpers.cs

    public static class EnumerationHelpers
    {
        /// <summary>
        /// Improves the speed of the code in a foreach loop for Il2CppSystem.Collections.Generic.List
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T> GetFastEnumerator<T>(this Il2CppSystem.Collections.Generic.List<T> list) where T : Il2CppSystem.Object => new Il2CppListEnumerable<T>(list);
    }

    public unsafe class Il2CppListEnumerable<T> : IEnumerable<T>, IEnumerator<T> where T : Il2CppSystem.Object
    {
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static Func<IntPtr, T> _objFactory;

        private readonly IntPtr _arrayPointer;
        private readonly int _count;
        private int _index = -1;

        static Il2CppListEnumerable()
        {
            _elemSize = IntPtr.Size;
            _offset = 4 * IntPtr.Size;

            var constructor = typeof(T).GetConstructor([typeof(IntPtr)]);
            var ptr = Expression.Parameter(typeof(IntPtr));
            var create = Expression.New(constructor!, ptr);
            var lambda = Expression.Lambda<Func<IntPtr, T>>(create, ptr);
            _objFactory = lambda.Compile();
        }

        public Il2CppListEnumerable(Il2CppSystem.Collections.Generic.List<T> list)
        {
            var listStruct = (Il2CppListStruct*)list.Pointer;
            _count = listStruct->_size;
            _arrayPointer = listStruct->_items;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return this;
        }

        T IEnumerator<T>.Current => (T)Current;
        public object Current { get; private set; }

        public bool MoveNext()
        {
            if (++_index >= _count) return false;
            var refPtr = *(IntPtr*)IntPtr.Add(IntPtr.Add(_arrayPointer, _offset), _index * _elemSize);
            Current = _objFactory(refPtr);
            return true;
        }

        public void Reset()
        {
            _index = -1;
        }

#pragma warning disable CA1816
        public void Dispose()
        {
        }
#pragma warning restore CA1816
        private struct Il2CppListStruct
        {
#pragma warning disable CS0169 // Field is never used
            private IntPtr _unusedPtr1;
            private IntPtr _unusedPtr2;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
            public IntPtr _items;
            public int _size;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
#pragma warning restore CS0169 // Field is never used
        }

        // ReSharper disable StaticMemberInGenericType
        private static readonly int _elemSize;

        private static readonly int _offset;
        // ReSharper restore StaticMemberInGenericType
    }
}