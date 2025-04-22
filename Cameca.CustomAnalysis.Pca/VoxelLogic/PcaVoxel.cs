using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using Prism.Regions;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using System.Security.Cryptography;
using System.Windows.Input;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Xml.Schema;
using Cameca.CustomAnalysis.Interface;

public struct VoxelID : IComparable<VoxelID>
{

    public int intValue;

    public VoxelID(int voxelId)
    {
        this.intValue = voxelId;
    }

    public VoxelID(ThreeDGridDimensions dims, int x, int y, int z)
    {
        this.intValue = dims.VoxelIdIntValueFor(x, y, z);
    }

    static VoxelID? VoxelIDIfPossible(ThreeDGridDimensions dims, int x, int y, int z)
    {
        if (dims.VoxelExists(x, y, z))
        {
            return new VoxelID(dims, x, y, z);
        }
        return null;
    }
    static public List<VoxelID> ListFromIntArray(int[] integerIds)
    {
        List<VoxelID> newList = new List<VoxelID>();
        foreach(int n in integerIds)
        {
            newList.Add(new VoxelID(n));
        }
        return newList;
    }

    public (int, int, int) xyzCoordsFor(ThreeDGridDimensions dims)
    {
        int zOffset = dims.xy;
        int yOffset = dims.x;
        int zCoord = this.intValue / zOffset;
        int rem = this.intValue % zOffset;
        int yCoord = rem / yOffset;
        rem = rem % yOffset;
        int xCoord = rem;
        return (xCoord, yCoord, zCoord);
    }

    public ThreeDGridCoord GridCoordFor(ThreeDGridDimensions dims)
    {
        int z = 0;
        int y = 0;
        int x = 0;
        (x, y, z) = xyzCoordsFor(dims);
        return new ThreeDGridCoord(x, y, z);
    }

    public int CompareTo(VoxelID other)
    {
        return other.intValue > intValue ? -1 : other.intValue < intValue ? 1 : 0;
    }

    internal void AddIfPossible(int x, int y, int z, ThreeDGridDimensions dims, List<VoxelID> neighbors)
    {
        VoxelID? voxelId = VoxelID.VoxelIDIfPossible(dims, x, y, z);
        if (voxelId != null)
        {
            neighbors.Add(voxelId.Value);
        }
    }
    public List<VoxelID> NeighborVoxels(ThreeDGridDimensions dims)
    {
        (int x, int y, int z) = this.xyzCoordsFor(dims);
        List<VoxelID> neighbors = new List<VoxelID>();
        AddIfPossible(x - 1, y, z, dims, neighbors);
        AddIfPossible(x + 1, y, z, dims, neighbors);
        AddIfPossible(x, y - 1, z, dims, neighbors);
        AddIfPossible(x, y + 1, z, dims, neighbors);
        AddIfPossible(x, y, z - 1, dims, neighbors);
        AddIfPossible(x, y, z + 1, dims, neighbors);
        return neighbors;
    }

    public string DebugStr(ThreeDGridDimensions dims)
    {
        int z;
        int y;
        int x;
        (x, y, z) = xyzCoordsFor(dims);
        return intValue.ToString() + ":{" + x + "," + y + " + " + z + "}";
    }
}

// PcaVoxel represents a voxel and its PCA scores returned from the PCA algorithm
public struct PcaVoxel
{
    public VoxelID voxelId; // the voxelIndex from compResults.VoxelIndices;  (between 0 and nTotalVoxels - 1)
    public float[] scoreVector;

    public PcaVoxel(VoxelID vId, float[] vec)
    {
        voxelId = vId;
        scoreVector = vec;
    }
}

 





