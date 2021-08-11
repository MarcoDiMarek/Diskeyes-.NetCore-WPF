using System;
using System.Collections.Generic;
using System.Text;

namespace DiskeyesCore
{
    /// <summary>
    /// Wraps a collection (a list, a hash set...) so that the specified maximum number of elements is not exceeded.
    /// </summary>
    /// <typeparam name="T">Type of collection (e.g. a list)</typeparam>
    /// <typeparam name="U">Type of the data stored inside the collection</typeparam>
    class CollectionCap<T,U> where T : ICollection<U>
    {
        public int MaxCount { get => maxCount; }
        public T Storage { get => storage; }

        public int Count { get => storage.Count; }

        private bool limitException;
        private int maxCount;
        private T storage;
        /// <summary>
        /// Wraps a collection (a list, a hash set...) so that the specified maximum number of elements is not exceeded.
        /// </summary>
        /// <param name="maxCount">The maximum limit</param>
        /// <param name="storage">Collection in the ready state (e.g. with initial capacity defined)</param>
        /// <param name="limitException">Throw an exception when the element to be added is over the limit.</param>
        public CollectionCap(int maxCount, T storage, bool limitException = false)
        {
            this.storage = storage;
            this.maxCount = maxCount;
            this.limitException = limitException;
        }
        public void Add(U element)
        {
            if (storage.Count < MaxCount)
                storage.Add(element);

            else if (limitException)
                throw new InsufficientMemoryException("Exceeded the collection cap");
        }

        public void Clear()
        {
            storage.Clear();
        }

        public bool Contains(U item)
        {
            return storage.Contains(item);
        }

        public bool Remove(U item)
        {
            return storage.Remove(item);
        }
    }
}
