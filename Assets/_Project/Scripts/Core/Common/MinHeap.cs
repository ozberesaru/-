using System.Collections.Generic;

namespace Srpg.Core
{
    // netstandard2.1 (Unity) has no PriorityQueue.
    internal sealed class MinHeap<T>
    {
        private readonly List<(int priority, T item)> items = new List<(int, T)>();

        public int Count => items.Count;

        public void Push(int priority, T item)
        {
            items.Add((priority, item));
            int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (items[parent].priority <= items[i].priority) break;
                (items[parent], items[i]) = (items[i], items[parent]);
                i = parent;
            }
        }

        public (int priority, T item) Pop()
        {
            var top = items[0];
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = i * 2 + 1;
                if (left >= items.Count) break;
                int right = left + 1;
                int smallest = right < items.Count && items[right].priority < items[left].priority ? right : left;
                if (items[i].priority <= items[smallest].priority) break;
                (items[i], items[smallest]) = (items[smallest], items[i]);
                i = smallest;
            }
            return top;
        }
    }
}
