using System.Collections.Generic;
using System;
 
 

public struct GridCoord
{

    public int x;
    public int y;
    public int z;

    public GridCoord(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
    public void ToConsole(string prefix) {  
        Console.WriteLine(prefix + this.x, ", ", + this.y, ", ", + this.z);
    }
};

 



