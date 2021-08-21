using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace DiskeyesCore
{
    struct ValueEntry<T>
    {
        public readonly T Value;
        public readonly int Index;
        public ValueEntry(int index, T value)
        {
            Index = index;
            Value = value;
        }
    }
    class RetrievalQueue<T>
    {
        private Dictionary<int, HashSet<IProgress<SearchBatch<T>>>> ToCollect;
        public bool IsEmpty
        {
            get { return ToCollect.Count > 0; }
        }
        public RetrievalQueue()
        {
            ToCollect = new Dictionary<int, HashSet<IProgress<SearchBatch<T>>>>();
        }

        public void Add(IProgress<SearchBatch<T>> progress, IEnumerable<int> linesToRetrieve)
        {
            foreach (var line in linesToRetrieve)
            {
                if (ToCollect.ContainsKey(line))
                    ToCollect[line].Add(progress);

                else
                    ToCollect[line] = new HashSet<IProgress<SearchBatch<T>>> { progress };
            }
        }

        public HashSet<IProgress<SearchBatch<T>>> Seekers(int index)
        {
            if (ToCollect.ContainsKey(index))
            {
                var collection = ToCollect[index];
                ToCollect.Remove(index);
                return collection;
            }

            else
                return null;
        }

        public void RemoveUnreachable(int startIndex)
        {
            var unreachable = from index in ToCollect.Keys
                              where index >= startIndex
                              select index;
            foreach (int index in unreachable.ToArray())
            {
                ToCollect.Remove(index);
            }
        }
    }
}
