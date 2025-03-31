using Cameca.CustomAnalysis.Interface;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

public sealed class ComponentsResults
{
    public IGrid3DData Grid3DData { get; }

    public int[] VoxelIndices { get; }

    public List<ComponentResults> Components { get; }

    public ComponentsResults(IGrid3DData grid3DData, int[] voxelIndices, List<ComponentResults> components)
    {
        Grid3DData = grid3DData;
        VoxelIndices = voxelIndices;
        Components = components;
    }

}

internal sealed class PhaseIdResults
{
    Dictionary<int, int> identifiedPhase;

    public PhaseIdResults(int[] voxelIndices, int maxIndex)
    {
        identifiedPhase = new Dictionary<int, int>();
        // not-yet-identified voxels are identified as 0
        for (int i = 0; i < voxelIndices.Length; ++i)
        {
            identifiedPhase[voxelIndices[i]] = 0;
        }
    }

    public void IdentifyVoxelAs(int voxelId, int phase)
    {
        identifiedPhase[voxelId] = phase;
    }

    public int? PhaseForVoxel(int voxelId)
    {
        return identifiedPhase[voxelId];
    }

    public List<int> UnidentifiedVoxels()
    {
        var unidentifiedIndices = new List<int>();
        foreach ( KeyValuePair<int, int> voxel in identifiedPhase )
        {
            if (voxel.Value == 0) {
                unidentifiedIndices.Add(voxel.Key);
            }
        }
        return unidentifiedIndices;
    }
}