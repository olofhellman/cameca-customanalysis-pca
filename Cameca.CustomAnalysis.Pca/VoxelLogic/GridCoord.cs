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

// ThreeDGridDimensions represents the size of a voxel grid -- 
// To specify a specific voxel in the grid, use a ThreeDGridCoord instead
public struct ThreeDGridDimensions
{
    public int x;
    public int y;
    public int z;
    public int xy;  // we often use this product, so multiple once and use that
    public int xyz;  // this is the number of voxels

    public ThreeDGridDimensions(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        int xy = x * y;
        this.xy = xy;
        this.xyz = xy * z;
    }

    public void ToConsole(string prefix)
    {
        Console.WriteLine(prefix + this.x, ", ", +this.y, ", ", +this.z);
    }
    public bool VoxelExists(int p, int q, int r)
    {
         return p >= 0 && q >= 0 && r >= 0 && p < x && q < y && r < z;
    }

    public VoxelID VoxelIdFor(int p, int q, int r)
    {
        int voxelIdIntValue = VoxelIdIntValueFor(p, q, r);
        return new VoxelID(voxelIdIntValue);
    }

    public int VoxelIdIntValueFor(int p, int q, int r)
    {
        return  p + (q * x) + (r * xy);
    }

    public int NumVoxels()
    {
        return this.xyz; 
    }
}

// GridCoord is a triplet of integers identifying a point on a Three Dimensional grid
public struct ThreeDGridCoord
{
    public int x;
    public int y;
    public int z;

    public ThreeDGridCoord(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public VoxelID VoxelIdFor(ThreeDGridDimensions gridDims)
    {
        return gridDims.VoxelIdFor(x, y, z);
    }

    public void ToConsole(string prefix)
    {
        Console.WriteLine(prefix + this.x, ", ", +this.y, ", ", +this.z);
    }
};





