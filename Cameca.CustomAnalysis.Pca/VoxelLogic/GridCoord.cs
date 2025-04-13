using System.Collections.Generic;
using System;



public struct TwoDGridCoord
{

    public int x;
    public int y;

    public TwoDGridCoord(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
    public void ToConsole(string prefix)
    {
        Console.WriteLine(prefix + this.x, ", ", + this.y);
    }
}; 
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
    public void ToConsole(string prefix)
    {
        Console.WriteLine(prefix + this.x, ", ", +this.y, ", ", +this.z);
    }
};





