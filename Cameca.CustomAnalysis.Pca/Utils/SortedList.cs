using System;
using System.Collections;
using System.Collections.Generic;

namespace PcaExtensionMethods
{
    public static class ListExt
    {
        public static void AddSorted<T>(this List<T> self, T item) where T : IComparable<T>
        {
            if (self.Count == 0)
            {
                self.Add(item);
                return;
            }
            if (self[self.Count - 1].CompareTo(item) <= 0)
            {
                self.Add(item);
                return;
            }
            if (self[0].CompareTo(item) >= 0)
            {
                self.Insert(0, item);
                return;
            }
            int index = self.BinarySearch(item);
            if (index < 0)
                index = ~index;
            self.Insert(index, item);
        }
    }
}
