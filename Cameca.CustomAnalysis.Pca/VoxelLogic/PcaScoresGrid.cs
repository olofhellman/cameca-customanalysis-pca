using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;
using System.Windows.Controls;
using System.Collections;
using System.Windows.Input;
using System.Text;
using System.Windows.Media.Animation;
using System.Reflection.PortableExecutable;


// PcaScoresGrid represents a three dimensional grid containing the PCA scores for a collection of voxels
// PcaScoresGrid is initialized with the size of the grid in x y and z, so that it can then map 
// a particular [x,y,z] grid coordinate to an index (and back) 
//
// Of particular interest to the PCA data processing logic, this class implements the logic for 
// calculating PhaseIdResults -- using the PCA score data to identify which voxels belong to which 
// 'phases'

public interface IScoresProvider
{
    public (VoxelID, float[]) GetIDAndScores(int voxelIndex);
}
public class PcaScoresGrid
{
    Dictionary<VoxelID, PcaVoxel> pcaVoxels; // the key is the 'id' for the voxel, from which you can
                                         // calculate the x,y,z position of the voxel on the grid if
                                         // the grid dimensions are known

    int scoreDims;
    ThreeDGridDimensions gridDims;
    int nVoxels;

    public VoxelID VoxelIDFor(ThreeDGridCoord gridCoord)
    {
        return gridCoord.VoxelIdFor(gridDims);
    }

    public ThreeDGridCoord GridCoordFor(VoxelID voxelId)
    {
        return voxelId.GridCoordFor(gridDims);
    }

    // really just the ASCII value of the first character
    internal int ASCIIValueForChar(char s)
    {
        return (int)s;
    }

    internal char CharValueForASCII(int a)
    {
        return (char)a;  
    }
    // returns a list of 27 indices, unless the voxel is near the edge
    // note: also includes self
    List<VoxelID> NeighborIDsFor(ThreeDGridCoord gridCoord)
    {
        List<VoxelID> neighborIndices = new List<VoxelID>();

        int minx = Math.Max(0, gridCoord.x - 1);
        int maxx = Math.Min(gridDims.x - 1, gridCoord.x + 1);
        int miny = Math.Max(0, gridCoord.y - 1);
        int maxy = Math.Min(gridDims.y - 1, gridCoord.y + 1);
        int minz = Math.Max(0, gridCoord.z - 1);
        int maxz = Math.Min(gridDims.z - 1, gridCoord.z + 1);
        for (int z = minz; z <= maxz; ++z)
        {
            for (int y = miny; y <= maxy; ++y)
            {
                for (int x = minx; x <= maxx; ++x)
                {
                    VoxelID voxelId = new VoxelID(x + (y * gridDims.x) + (z * gridDims.xy));
                    neighborIndices.Add(voxelId);
                }
            }
        }
        return neighborIndices;
    }

    public PcaScoresGrid(IScoresProvider scoresProvider, int nIndices, int scoreDimensions, ThreeDGridDimensions gridDimensions)
    {
        scoreDims = scoreDimensions;

        pcaVoxels = pcaVoxelsInit(scoresProvider, nIndices);

        nVoxels = gridDimensions.NumVoxels();
    }

    static Dictionary<VoxelID, PcaVoxel> pcaVoxelsInit(IScoresProvider scoresProvider, int nIndices)
    {
        var voxels = new Dictionary<VoxelID, PcaVoxel>();

        for (int i = 0; i < nIndices; ++i)
        {
            float[] nthVector;
            VoxelID voxelId;
            
            (voxelId, nthVector) = scoresProvider.GetIDAndScores(i);

            var pcaVoxel = new PcaVoxel(voxelId, nthVector);
            voxels[voxelId] = pcaVoxel;
        }

        return voxels;
    }

    // When assigning each voxel to a phase based on which peak it might be in,
    // We want to avoid mapping each pixel to a peak more than once
    // SO, first  put all the voxels into a different list corresponding to each pixel
    // then we can do the lookup once and assign all the voxels from that pixel to 
    // the same phase
    Dictionary<PixelID, List<VoxelID>> aggregateVoxelsIntoListsPerPixel(List<VoxelID> voxelIds, DensityPlane grid, Dictionary<VoxelID, PcaVoxel> pcaVoxels, int firstDim, int secondDim)
    {
        // For each voxel, assign the voxel to the List corresponding with its pixel
        Dictionary<PixelID, List<VoxelID>> voxelLists = new Dictionary<PixelID, List<VoxelID>>();
        foreach (VoxelID voxelId in voxelIds)
        {
            PcaVoxel voxel = pcaVoxels[voxelId];
            PixelID nthPixelId = grid.PixelIDFor(voxel.scoreVector[firstDim], voxel.scoreVector[secondDim]);
            List<VoxelID>? voxelIDListForGridCoord;
            if (voxelLists.TryGetValue(nthPixelId, out voxelIDListForGridCoord))
            {
                voxelIDListForGridCoord.Add(voxelId);
            }
            else
            {
                voxelIDListForGridCoord = new List<VoxelID>();
                voxelIDListForGridCoord.Add(voxelId);
                voxelLists[nthPixelId] = voxelIDListForGridCoord;
            }
        }
        return voxelLists;
    }

    // VoxelBuckets essentially inverts the dictionary passed in
    // returns a dictionary where the key is the pcaCode string,
    // and the value is a HashSet of VoxelIDs with that pcaCode
    internal Dictionary<string, HashSet<VoxelID>> VoxelBuckets(Dictionary<VoxelID, string> pcaCodes)
    {
        Dictionary<string, HashSet<VoxelID>> buckets = new Dictionary<string, HashSet<VoxelID>>();
        foreach (KeyValuePair<VoxelID, string> kvp in pcaCodes)
        {
            if (buckets.ContainsKey(kvp.Key))
            {
                buckets[kvp.Key].Add(kvp.Value);
            }
            else
            {
                HashSet<VoxelID> newSet = new HashSet<VoxelID>();
                newSet.Add(kvp.Value)
                buckets[kvp.Key] = newSet;
            }
        }
        return buckets;
    }
    internal Dictionary<string, int> PcaCodeCounts(Dictionary<string, HashSet<VoxelID>> voxelBuckets)
    {
        Dictionary<string, int> pcaCodeCounts = new Dictionary<string, int>();
        foreach (KeyValuePair<string, HashSet<VoxelID>> kvp in voxelBuckets)
        {
            pcaCodeCounts.[kvp.Key] = kvp.Value.Count;
        }
        return pcaCodeCounts;
    }
    internal void DumpPcaCodeCounts(Dictionary<string, int> pcaCodeCounts)
    {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string outputFilename = System.IO.Path.Combine(docPath, "PcaCodeStats.txt");

        using (StreamWriter outputFile = new StreamWriter(outputFilename))
        {
            List<string> sortedCodes = pcaCodeCounts.Keys.ToList();
            sortedCodes.Sort();
            outputFile.WriteLine("PcaCodeStats file");
            foreach (string pcaCode in sortedCodes)
            {
                int count = pcaCodeCounts[pcaCode];
                outputFile.WriteLine("Code: \t" + pcaCode + "\t Count:\t" + count);
            }
            outputFile.Close();
        }
    }
    // GetPhasesStrategyC is produces PhaseIdResults based on the first two 
    // PCA dimensions only (later strategies will incorporate more dimensions)
    //
    // The strategy goes like this:
    //
    // A) make a 2D grid representing the density of voxels with PCA scores in 
    //    the first two PCA dimensions at x,y
    // B) identify peaks in that 2D grid -- each peak corresponds to a 'phase'
    // C) assign voxels with PCA scores in each peak to the corresponding phase
    public PhaseIdResults GetPhasesStrategyC()
    {
        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();

        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIds);

        float binSeparation = 0.5f;
        int gridDims = Math.Min(2, this.scoreDims); // 2 is the simplest choice 
        Dictionary<int, DensityPlane> twoDGrids = new Dictionary<int, DensityPlane>();
        Dictionary<int, List<List<PixelID>>> partitions = new Dictionary<int, List<List<PixelID>>>();
        
        // now, make twoD grids using all pairs of dimensions
        // yes, GetPhasesStrategyC only uses 2 dimensions, so these loops are useless, 
        // but they will be used in successive strategies when there are more dimensions used
        for (int i = 0; i < (gridDims - 1); ++i)
        {
            for (int j = i + 1; j < gridDims; ++j)
            {
                int gridId = 10 * i + j;
                
                // this makes the 2D grid  --  step A) above
                var twoDGrid = CalculateTwoDDensity(voxelIds, i, j, binSeparation);
                twoDGrids[gridId] = twoDGrid;
                
                // this identifies the peaks --  step B) above
                List<List<PixelID>> partitionedIndices = IdentifyPartitions(twoDGrid, i, j);
                partitions[gridId] = partitionedIndices;
            }
        }

        // now turn the partitions array into the data needed by phaseIDResults -- step C) above
        // 
        // phaseIDResults has a method  IdentifyVoxelAs(int voxelIds, int phase)
        //
        // a voxel corresponds to the peak if its PCA scores for the given two dimensions 
        // is in a pixel of a peak. So, for each voxel, we want to call IdentifyVoxelAs()
        // with the value for 'phase' corresponding to the peak it is in, if any
        //
        // to avoid looking up whether or not a pixel is in one of the peaks multiple times,
        // first, group all the voxelIDs into a list for each pixelID
        // then do a single lookup for each pixelID

        if (gridDims > 1)
        {
            // GetPhasesStrategyC only looks at one grid -- the first one
            int firstGridId = 1;
            DensityPlane grid = twoDGrids[firstGridId];
            List<List<PixelID>> firstGridPartitions = partitions[firstGridId];

            // for debugging, dump the partitions:
            /* 
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string outputFilename = System.IO.Path.Combine(docPath, "Partitions.txt");

            using (StreamWriter outputFile = new StreamWriter(outputFilename))
            {
                outputFile.WriteLine("partitions file");
                outputFile.WriteLine("grid Info:");

                TwoDGridCoord gridMinCoord;
                TwoDGridCoord gridMaxCoord;
                (gridMinCoord, gridMaxCoord) = grid.MinMaxGridCoords();
                outputFile.WriteLine("minx: " + gridMinCoord.x);
                outputFile.WriteLine("miny: " + gridMinCoord.y);
                outputFile.WriteLine("maxx: " + gridMaxCoord.x);
                outputFile.WriteLine("maxy: " + gridMaxCoord.y);
                int peakIndex = 0;
                outputFile.WriteLine("");
                foreach (List<PixelID> pixelIdList in firstGridPartitions)
                {
                    outputFile.WriteLine("partition: " + peakIndex);
                    outputFile.WriteLine("pixelCoords: " + peakIndex);
                    outputFile.Write("{");
                    foreach (PixelID pixelId in pixelIdList)
                    {
                        int x;
                        int y;
                        (x, y) = pixelId.xyCoords();
                        outputFile.Write("{" + x + "," + y + "}");
                    }
                    outputFile.Write("}\n");
                }
            }
            */
            // end of debug dump

            Dictionary<PixelID, List<VoxelID>> voxelLists = aggregateVoxelsIntoListsPerPixel(voxelIds, grid, pcaVoxels, 0, 1);

            // now, each voxelIndex is a member of one of the lists in voxelLists
            // go through each list in voxelLists and see if its pixel is designated in a particular partiion
            // that is, for each pixel, see which peak it belongs to, if any
             
            int listN = 1;
            foreach (List<PixelID> pixelIdList in firstGridPartitions)
            {
                // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                foreach (PixelID pixelID in pixelIdList)
                {
                    // Lookup for all the voxels bucketed under this pixelId
                    List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                    foreach (VoxelID voxelId in voxelIdsForThisPixel)
                    {
                        phaseIdResults.IdentifyVoxelAs(voxelId, listN);
                    }
                    // remove that entry from voxelLists
                    voxelLists.Remove(pixelID);
                }

                ++listN;
            }

            // now, all the remaining entries in voxelLists are unassigned :  
            // assign these to component 0
            List<PixelID> unassignedPixels = voxelLists.Keys.ToList();
            foreach (PixelID pixelID in unassignedPixels)
            {
                // Lookup for all the voxels bucketed under this pixelId
                List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                foreach (VoxelID voxelId in voxelIdsForThisPixel)
                {
                    phaseIdResults.IdentifyVoxelAs(voxelId, 0);
                }
            }
        }

        return phaseIdResults;
    }
    internal void DumpPartitions(List<List<PixelID>> partitions, string gridId)
    {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string outputFilename = System.IO.Path.Combine(docPath, "PcaPeakPartitions" + gridId + ".txt");
        // int peakIndex = 1;
        using (StreamWriter outputFile = new StreamWriter(outputFilename))
        {
            outputFile.Write("peakCoords = {");
            bool firstPeak = true;
            foreach (List<PixelID> partition in partitions)
            {
                if (!firstPeak)
                {
                    outputFile.Write(",");
                }
                else
                {
                    firstPeak = false;
                }
                outputFile.Write("{");
                bool firstCoord = true;
                foreach ( PixelID pixelId in partition )
                {
                    (int x, int y) = pixelId.xyCoords();
                    if (!firstCoord) {
                        outputFile.Write(","); 
                    } 
                    else
                    {
                        firstCoord = false;
                    }
                    outputFile.Write("{" + x + "," + y + "}");
                }
                outputFile.WriteLine("}¬\n");
               //  peakIndex += 1;
            }

            outputFile.Write("}");
            outputFile.Close();
        }
    }
    // GetPhasesStrategyD is produces PhaseIdResults based on the first three 
    // PCA dimensions only  
    //
    // The strategy goes like this:
    //
    // A) make three 2D grids representing the density of voxels with PCA scores in 
    //    each combination of the first three PCA dimensions 
    //    in other words dim1xdim2,  dim1xdim3,  dim2xdim3
    // B) identify peaks in each 2D grid -- each peak corresponds to a section of a PCA code
    //      A1 is a code snippet representing the first peak of the first grid
    //      A2 is a code snippet representing the second peak of the first grid  
    //      B1 is a code snippet representing the first peak of the second grid    
    //      B0 is a code snippet representing no association with any peak of the second grid
    //      etc.
    // C) identify the top 4 buckets and assign those to phases
    //      assign all the rest to phase 0
    //  note -- steps A and B are identical  to GetPhasesStrategyD
    //          step C is just a shortcut to identifying phases to make sure the code was working
    public PhaseIdResults GetPhasesStrategyD()
    {
        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();

        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIds);

        float binSeparation = 0.5f;
        int gridDims = Math.Min(3, this.scoreDims); // 3 for strategy D
        Dictionary<string, DensityPlane> twoDGrids = new Dictionary<string, DensityPlane>();
        Dictionary<string, List<List<PixelID>>> partitions = new Dictionary<string, List<List<PixelID>>>();

        // make a dictionary for the pcaCodes and fill with enpty Strings
        Dictionary<VoxelID, string> pcaCodes = new Dictionary<VoxelID, string>();
        foreach (VoxelID voxelId in voxelIds)
        {
            pcaCodes[voxelId] = "";
        }

        int AAsciiValue = ASCIIValueForChar('A');
        int gridIndex = 0;
        // now, make twoD grids using all pairs of dimensions
        for (int i = 0; i < (gridDims - 1); ++i)
        {
            for (int j = i + 1; j < gridDims; ++j)
            {
                string gridLetter = CharValueForASCII(AAsciiValue + gridIndex).ToString();
                string gridId = gridLetter + i.ToString() + j.ToString();

                // this makes the 2D grid  --  step A) above
                var twoDGrid = CalculateTwoDDensity(voxelIds, i, j, binSeparation);
                twoDGrids[gridId] = twoDGrid;

                // this identifies the peaks --  step B) above
                List<List<PixelID>> partitionedIndices = IdentifyPartitions(twoDGrid, i, j);
                partitions[gridId] = partitionedIndices;
                DumpPartitions(partitionedIndices, gridId);

                // now label each voxel with a PCA code based on its peak association
                // for each grid, group the voxels into lists per pixel, then, knowing
                // which peaks contain which pixels, add the voxels PCA code for that grid to its entry in 
                // the PCA code dictionary
                int peakIndex = 1;
                Dictionary<PixelID, List<VoxelID>> voxelLists = aggregateVoxelsIntoListsPerPixel(voxelIds, twoDGrid, pcaVoxels, i, j);
                foreach (List<PixelID> pixelIdList in partitionedIndices)
                {
                    string pcaCode = gridLetter + peakIndex.ToString();
                    // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                    foreach (PixelID pixelID in pixelIdList)
                    {
                        // Lookup for all the voxels bucketed under this pixelId
                        List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                        foreach (VoxelID voxelId in voxelIdsForThisPixel)
                        {
                            pcaCodes[voxelId] = pcaCodes[voxelId] + pcaCode;
                        }
                        // remove that entry from voxelLists
                        voxelLists.Remove(pixelID);
                    }
                    peakIndex += 1;
                }

                // now, all the remaining entries in voxelLists are unassigned :  
                // assign these to component 0
                string unassignedPcaCode = gridLetter + "0";
                List<PixelID> unassignedPixels = voxelLists.Keys.ToList();
                foreach (PixelID pixelID in unassignedPixels)
                {
                    // Lookup for all the voxels bucketed under this pixelId
                    List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                    foreach (VoxelID voxelId in voxelIdsForThisPixel)
                    {
                        pcaCodes[voxelId] = pcaCodes[voxelId] + unassignedPcaCode;
                    }
                }
                gridIndex += 1;
            }
        }

        // now examine the groups of voxels to identify contiguous regions
        // first, lets dump some info about populations of all the different 
        // pca Codes:
        Dictionary<string, int> pcaCodeCounts = PcaCodeCounts(pcaCodes);
        DumpPcaCodeCounts(pcaCodeCounts);

        // preliminary strategy D:
        // just find the highest populated 4 buckets in pcaCodeCounts, assign those as 
        // the first 4 components
        // leave the voxel coagulation until later

        // phaseIDResults has a method  IdentifyVoxelAs(int voxelIds, int phase)
        List<KeyValuePair<string, int>> kvpList = pcaCodeCounts.ToList<KeyValuePair<string, int>>();
        List<KeyValuePair<string, int>> sortedKvpList = kvpList.OrderByDescending(kvp => kvp.Value).ToList();
        Dictionary<string, int> phaseAssignments = new Dictionary<string, int>();
        int phase = 1;
        foreach (KeyValuePair<string, int> kvp in sortedKvpList)
        {
            phaseAssignments[kvp.Key] = phase;
            phase += 1;
        }

        // and, call IdentifyVoxelAs for each voxel
        // now, all the remaining entries in voxelLists are unassigned :  
        // assign these to component 0
        foreach (VoxelID voxelId in voxelIds)
        {
            string pcaCode = pcaCodes[voxelId];
            int voxelphase = phaseAssignments[pcaCode];
            if (voxelphase > 4)
            {
                voxelphase = 0;
            }

            phaseIdResults.IdentifyVoxelAs(voxelId, voxelphase);
        }
        return phaseIdResults;
    }

    List<VoxelID> FindMatchingSets(VoxelID neighborID, Dictionary<VoxelID, HashSet<VoxelID>> voxelSets)
    {
        List<VoxelID> matchingSets = new List<VoxelID>();
        foreach (KeyValuePair<VoxelID, HashSet<VoxelID>> kvp in voxelSets)
        {
            if (kvp.Value.Contains(neighborID))
            {
                matchingSets.Add(kvp.Key);
            }
        }
        return matchingSets;
    }

    // GetPhasesStrategyE is produces PhaseIdResults based on the first three 
    // PCA dimensions only  
    //
    // The strategy goes like this:
    //
    // A) make three 2D grids representing the density of voxels with PCA scores in 
    //    each combination of the first three PCA dimensions 
    //    in other words dim1xdim2,  dim1xdim3,  dim2xdim3
    // B) identify peaks in each 2D grid -- each peak corresponds to a section of a PCA code
    //      A1 is a code snippet representing the first peak of the first grid
    //      A2 is a code snippet representing the second peak of the first grid  
    //      B1 is a code snippet representing the first peak of the second grid    
    //      B0 is a code snippet representing no association with any peak of the second grid
    //      etc.
    // C) look at all of the PCA codes of the form
    //      AhBkCl
    //    where h, k, and l are non-zero.  Regions of contiguous voxels that share 
    //    the same code will be designated as the same phase 
    // D) add voxels that abut any of the contiguous regions to those regions iff
    //    the code they have matches all code snippet for which they have a non-zero number
    //    i.e. A0B1C1  is a match for contiguous regions A1B1C1 and A2B1C1
    public PhaseIdResults GetPhasesStrategyE()
    {
        List<VoxelID> voxelIds = pcaVoxels.Keys.ToList();

        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIds);

        float binSeparation = 0.5f;
        int gridDims = Math.Min(3, this.scoreDims); // 3 for strategy D
        Dictionary<string, DensityPlane> twoDGrids = new Dictionary<string, DensityPlane>();
        Dictionary<string, List<List<PixelID>>> partitions = new Dictionary<string, List<List<PixelID>>>();
        
        // make a dictionary for the pcaCodes and fill with enpty Strings
        Dictionary<VoxelID, string> pcaCodes = new Dictionary<VoxelID, string>();
        foreach (VoxelID voxelId in voxelIds)
        {
            pcaCodes[voxelId] = "";
        }

        int AAsciiValue = ASCIIValueForChar('A');
        int gridIndex = 0;
        // now, make twoD grids using all pairs of dimensions
        for (int i = 0; i < (gridDims - 1); ++i)
        {
            for (int j = i + 1; j < gridDims; ++j)
            {
                string gridLetter = CharValueForASCII(AAsciiValue + gridIndex).ToString();
                string gridId = gridLetter + i.ToString() + j.ToString(); 

                // this makes the 2D grid  --  step A) above
                var twoDGrid = CalculateTwoDDensity(voxelIds, i, j, binSeparation);
                twoDGrids[gridId] = twoDGrid;

                // this identifies the peaks --  step B) above
                List<List<PixelID>> partitionedIndices = IdentifyPartitions(twoDGrid, i, j);
                partitions[gridId] = partitionedIndices;
                DumpPartitions(partitionedIndices, gridId);
                
                // now label each voxel with a PCA code based on its peak association
                // for each grid, group the voxels into lists per pixel, then, knowing
                // which peaks contain which pixels, add the voxels PCA code for that grid to its entry in 
                // the PCA code dictionary
                int peakIndex = 1;
                Dictionary<PixelID, List<VoxelID>> voxelLists = aggregateVoxelsIntoListsPerPixel(voxelIds, twoDGrid, pcaVoxels, i, j);
                foreach (List<PixelID> pixelIdList in partitionedIndices)
                {
                    string pcaCode = gridLetter + peakIndex.ToString();
                    // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                    foreach (PixelID pixelID in pixelIdList)
                    {
                        // Lookup for all the voxels bucketed under this pixelId
                        List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                        foreach (VoxelID voxelId in voxelIdsForThisPixel)
                        {
                            pcaCodes[voxelId] = pcaCodes[voxelId] + pcaCode;
                        }
                        // remove that entry from voxelLists
                        voxelLists.Remove(pixelID);
                    }
                    peakIndex += 1;
                }

                // now, all the remaining entries in voxelLists are unassigned :  
                // assign these to component 0
                string unassignedPcaCode = gridLetter + "0";
                List<PixelID> unassignedPixels = voxelLists.Keys.ToList();
                foreach (PixelID pixelID in unassignedPixels)
                {
                    // Lookup for all the voxels bucketed under this pixelId
                    List<VoxelID> voxelIdsForThisPixel = voxelLists[pixelID];
                    foreach (VoxelID voxelId in voxelIdsForThisPixel)
                    {
                        pcaCodes[voxelId] = pcaCodes[voxelId] + unassignedPcaCode;
                    }
                }
                gridIndex += 1;
            }
        }

        // now examine the groups of voxels to identify contiguous regions
        Dictionary<string, HashSet<VoxelID>> voxelBuckets = VoxelBuckets(pcaCodes);

        // first, lets dump some info about populations of all the different 
        // pca Codes:
        Dictionary<string, int> pcaCodeCounts = PcaCodeCounts(voxelBuckets);
        DumpPcaCodeCounts(pcaCodeCounts);

        // At this stage we want to identify which voxels are part of contiguous regions of phases
        // At first, assume that all PCA codes that have no zero value in them identify a particular phase
        // So, generate a list of voxels for each of these phases and mark them as part of a contiguous region or not
        // here, "contiguous region" just means more two or more voxels of the same PCA code adjacent to each other
        // collect data as follows:
        // for each PCA code, maintain multiple List<VoxelID>.
        // each list has an "anchor voxel" (the first voxel in the list), and the 
        // VoxelID for the anchor is used as a key in a dictionary where the Value is a List<VoxelID>
        // for each new voxel, look for neighboring voxel IDs, and see if any existing group contains those IDs.
        // If yes, put the voxel in that group.
        // If no, start a new group
        // if multiple groups contain neighboring voxels, merge the groups.
        // There are different strategies for what "neighboring voxel" means
        //   As a first stroke, we'll just look at the six immediately adjacent voxels
        //   -- the ones that share a face.
        //   A future tweak could be to include voxels that share an edge

        List<string> nonZeroPcaCodes = voxelBuckets.Keys.Where(code => !code.Contains("0")).ToList();
        Dictionary<string, Dictionary<VoxelID, HashSet<VoxelID>>> pcaCodeVoxelSets = new Dictionary<string, Dictionary<VoxelID, HashSet<VoxelID>>>();
        // pcaCodes is a Dictionary<VoxelID, string>
        foreach(string pcaCode in nonZeroPcaCodes)
        {
            Dictionary<VoxelID, HashSet<VoxelID>> voxelSets = new Dictionary<VoxelID, HashSet<VoxelID>>();
            HashSet<VoxelID> allVoxelsForThisPca = voxelBuckets[pcaCode];
            foreach(VoxelID voxelId in allVoxelsForThisPca)
            {
                List<VoxelID> neighbors = voxelId.NeighborVoxels(gridDims);
                foreach (VoxelID neighborID in neighbors)
                {
                    List<VoxelID> matchingSets = FindMatchingSets(neighborID, voxelSets);
                    if (matchingSets.Count() == 0)
                    {
                        // make a new group with this Voxel as the key
                        HashSet<VoxelID> newHashSet = new HashSet<VoxelID>();
                        newHashSet.Add(voxelId);
                        voxelSets[voxelId] = newHashSet;
                    }
                    else 
                    {
                        VoxelID matchingGroupId = matchingSets[0];
                        voxelSets[matchingGroupId].Add(voxelId);
                        while (matchingSets.Count() > 1)
                        {
                            // combine all the sets that are connected
                            VoxelID lastSetId = matchingSets.TakeLast(1);
                            HashSet<VoxelID> setToCopy = voxelSets[lastSetId];
                            voxelSets[lastSetId] = null;
                            foreach (VoxelID idToCopy in setToCopy)
                            {
                                voxelSets[matchingGroupId].Add(idToCopy);
                            }
                        }
                    }
                     
                        // join all the sets that are connected, and then add this one
                        
                  
                }
            }

            pcaCodeVoxelSets[pcaCode] = voxelSets;
        }

        // phaseIDResults has a method  IdentifyVoxelAs(int voxelIds, int phase)
        List<KeyValuePair<string, int>> kvpList = pcaCodeCounts.ToList<KeyValuePair<string, int>>();
        List<KeyValuePair<string, int>> sortedKvpList = kvpList.OrderByDescending(kvp => kvp.Value).ToList();
        Dictionary<string, int> phaseAssignments = new Dictionary<string, int>();
        int phase = 1;
        foreach (KeyValuePair<string, int> kvp in sortedKvpList)
        {
            phaseAssignments[kvp.Key] = phase;
            phase += 1;
        }

        // and, call IdentifyVoxelAs for each voxel
        // now, all the remaining entries in voxelLists are unassigned :  
        // assign these to component 0
        foreach (VoxelID voxelId in voxelIds)
        {
            string pcaCode = pcaCodes[voxelId];
            int voxelphase = phaseAssignments[pcaCode];
            if (voxelphase > 4 )
            {
                voxelphase = 0;
            }
 
           phaseIdResults.IdentifyVoxelAs(voxelId, voxelphase);
        }
        return phaseIdResults;
    }


    // identifyPartitions use the density map from the twoDGrid to separate 
    // voxels that belong to different peaks in the DensityPlane
    // first, identify the peaks and their associated pixels
    // then, for each voxel, see if it lands in on of the partitioned pixels.
    // If it does, add it to the appropriate list
    // return the list of lists
    // indices not identified are not returned in any list
    public List<List<PixelID>> IdentifyPartitions(DensityPlane twoDGrid, int dimX, int dimy)
    {
        // to identify the first maximum, just find the pixel with the highest value
        // then, accumulate neighboring pixels, avoiding neighbors with higher values
        // accumulate neighbors in order of their density value.
        // stop accumulating when a slope increase is found:
        //   this indicates there must be another maximum to look for
        // also, stop at 10% of peak max
        // to look for another maximum, find the remaining pixel with the maximum value.
        // continue accumulating pixels into all peaks

        // partitionFinder operates on the grid, identifying pixels
        // associated with the different maxima
        TwoDGridPartitionFinder partitionFinder = new TwoDGridPartitionFinder(twoDGrid);

        partitionFinder.FindPartitions(0.1f);
        var pixelLists = partitionFinder.GetPixelLists();
        partitionFinder.Clear();
        return pixelLists;
    }

    public DensityPlane CalculateTwoDDensity(List<VoxelID> voxelIds, int dimx, int dimy, float binsize)
    {
        DensityPlane densityPlane = new DensityPlane(binsize);
        for (int v = 0; v < voxelIds.Count; ++v)
        {
            VoxelID voxelId = voxelIds[v];
            var voxel = pcaVoxels[voxelId];
            var xVal = voxel.scoreVector[dimx];
            var yVal = voxel.scoreVector[dimy];
            densityPlane.AddPointAt(xVal, yVal);
        }
        return densityPlane;
    }

    // CalculateOneDDensities is not used, but here's what it does:
	// it creates a profile along each of the principal PCA dimensions 
	// of the density of voxels along that dimension 
	// That is, peaks in the density profile represent PCA score values with a high population of 
	// voxels. each Profile is a 'smoothed' histogram of populations 
	// The AP Suite already has code that produces a similar histogram, displayed
	// in one of the panes of the PCA Extension.
	// 
	// The number of points in the profile is determined by the binsize
	// If there are points between 0 and 10, there may be 23 entries, from -0.5 to 10.5
	// at spacings of 0.5
	//
	// It can be used like this:
	//
	// float binSeparation = 0.5f;
	// List<DensityProfile> oneDDensities = CalculateOneDDensities(voxelIndices, binSeparation);

    public List<DensityProfile> CalculateOneDDensities(List<VoxelID> voxelIds, float binsize)
    {
        List<DensityProfile> densities = new List<DensityProfile>();
        for (int i = 0; i < this.scoreDims; ++i)
        {
            var nthDensityList = new DensityProfile(binsize);
            densities.Add(nthDensityList);
        }

        for (int v = 0; v < voxelIds.Count; ++v)
        {
            VoxelID voxelIndex = voxelIds[v];
            var voxel = pcaVoxels[voxelIndex];
            for (int i = 0; i < this.scoreDims; ++i)
            {
                densities[i].AddPointAt(voxel.scoreVector[i]);
            }
        }
        return densities;
    }
}




