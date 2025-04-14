using System;
using System.Collections;
using System.Collections.Generic;

namespace PcaExtensionMethods
{
    public static class ListExt
    {
        // AddSorted assumes that the list is already sorted. An item is added 
        // so that the sort order is maintained.
        // there is no attempt made to put "ties" either before or after items 
        // which already exist in the list.  
        // that is, if the list contains {H1, K2, L3}, (sorting by the number values) 
        // and item M2 is added the result could be either {H1, K2, M2, L3}  or {H1, M2, K2, L3}
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
