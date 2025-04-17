using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;



public class PopulationSorter<T, U> : IComparer<T>
{
    Dictionary<T, HashSet<U>> dict;
    public PopulationSorter(Dictionary<T, HashSet<U>> d)
    {
        dict = d;
    }

    public int Compare(T? x, T? y)
    {
        int popX = 0;
        int popY = 0;
        if (x != null && dict.ContainsKey(x))
        {
            popX = dict[x].Count();
        }
        if (y != null && dict.ContainsKey(y))
        {
            popY = dict[x].Count();
        }
        return popX > popY;
    }
 
}




