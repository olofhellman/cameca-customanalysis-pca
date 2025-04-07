using System.Collections.Generic;
using System.Linq;
using System;

public class PhaseIdResults
{
    Dictionary<int, int> identifiedPhase;

    public PhaseIdResults(List<int> voxelIndices, int maxIndex)
    {
        identifiedPhase = new Dictionary<int, int>();
        // not-yet-identified voxels are identified as 0
        for (int i = 0; i < voxelIndices.Count; ++i)
        {
            identifiedPhase[voxelIndices[i]] = 0;
        }
    }

    public void IdentifyVoxelAs(int voxelId, int phase)
    {
        identifiedPhase[voxelId] = phase;
    }    
    public void IdentifyVoxelsAs(List<int> voxelIds, int phase)
    {
        foreach (int voxelId in voxelIds) {
            identifiedPhase[voxelId] = phase;
        }
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
    
  