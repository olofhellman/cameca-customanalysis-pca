using System.Collections.Generic;
using System;


// TwoDGridCoord is a pair of integers identifying a point on a Two Dimensional grid
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

// GridCoord is a triplet of integers identifying a point on a Three Dimensional grid
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





