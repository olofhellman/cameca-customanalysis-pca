using Cameca.CustomAnalysis.Interface;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Cameca.CustomAnalysis.Pca;

public sealed class ComponentsResults
{
    public IGrid3DData Grid3DData { get; }

    public int[] VoxelIndices { get; }

    public PhaseIdResults PhaseIDResults { get; set; }

    public List<ComponentResults> Components { get; }

    public ComponentsResults(IGrid3DData grid3DData, int[] voxelIndices, List<ComponentResults> components)
    {
        Grid3DData = grid3DData;
        VoxelIndices = voxelIndices;
        Components = components;

        List<int> emptyList = new List<int>();
        emptyList.Add(0);
        emptyList[0] = 0;
        PhaseIDResults = new PhaseIdResults(emptyList, 1);
    }

}