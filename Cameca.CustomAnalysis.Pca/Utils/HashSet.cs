using System;
using System.Collections;
using System.Collections.Generic;

namespace PcaExtensionMethods
{
    public static class HashSetExt
    {
        // AddSorted assumes that the list is already sorted. An item is added 
        // so that the sort order is maintained.
        // there is no attempt made to put "ties" either before or after items 
        // which already exist in the list.  
        // that is, if the list contains {H1, K2, L3}, (sorting by the number values) 
        // and item M2 is added the result could be either {H1, K2, M2, L3}  or {H1, M2, K2, L3}
        public static bool ContainsAny<T>(this HashSet<T> self, IEnumerable<T> collection)  
        {
            foreach(T t in collection)
            {
                if (self.Contains(t)) return true;
            }
            return false;
        }
    }
}
