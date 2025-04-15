using System.Collections.Generic;
using System.Linq;
using System;


// PhaseIDResults represents an assignment of each voxel to an integer phase.
// in the identifiedPhase Dictionary, the Key is a VoxelID, and the value is its 'phase', 
// or 0 if no phase is identified
public class PhaseIdResults
{
    Dictionary<VoxelID, int> identifiedPhase; 

    public PhaseIdResults(List<VoxelID> voxelIds)
    {
        identifiedPhase = new Dictionary<VoxelID, int>();
        // not-yet-identified voxels are identified as 0
        for (int i = 0; i < voxelIds.Count; ++i)
        {
            identifiedPhase[voxelIds[i]] = 0;
        }
    }

    public void IdentifyVoxelAs(VoxelID voxelId, int phase)
    {
        identifiedPhase[voxelId] = phase;
    }    
    public void IdentifyVoxelsAs(List<VoxelID> voxelIds, int phase)
    {
        foreach (VoxelID voxelId in voxelIds) {
            identifiedPhase[voxelId] = phase;
        }
    }

    public int? PhaseForVoxel(VoxelID voxelId)
    {
        if (identifiedPhase.ContainsKey(voxelId))
        {
            return identifiedPhase[voxelId];
        }
        return null;
    }

    public int? PhaseForVoxelIntValue(int voxelIndex)
    {
        VoxelID voxelId = new VoxelID(voxelIndex);
        if (identifiedPhase.ContainsKey(voxelId))
        {
            return identifiedPhase[voxelId];
        }
        return null;
    }

    public List<VoxelID> UnidentifiedVoxels()
    {
        var unidentifiedIndices = new List<VoxelID>();
        foreach ( KeyValuePair<VoxelID, int> voxel in identifiedPhase )
        {
            if (voxel.Value == 0) {
                unidentifiedIndices.Add(voxel.Key);
            }
        }
        return unidentifiedIndices;
    }
}
    
  