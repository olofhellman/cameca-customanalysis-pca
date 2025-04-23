
using PcaExtensionMethods;
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
using Cameca.CustomAnalysis.Pca;
using System.Runtime.Intrinsics.Arm;


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

        gridDims = gridDimensions;
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
            if (buckets.ContainsKey(kvp.Value))
            {
                buckets[kvp.Value].Add(kvp.Key);
            }
            else
            {
                HashSet<VoxelID> newSet = new HashSet<VoxelID>();
                newSet.Add(kvp.Key);
                buckets[kvp.Value] = newSet;
            }
        }
        return buckets;
    }
    internal Dictionary<string, int> PcaCodeCounts(Dictionary<string, HashSet<VoxelID>> voxelBuckets)
    {
        Dictionary<string, int> pcaCodeCounts = new Dictionary<string, int>();
        foreach (KeyValuePair<string, HashSet<VoxelID>> kvp in voxelBuckets)
        {
            pcaCodeCounts[kvp.Key] = kvp.Value.Count;
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
                foreach (PixelID pixelId in partition)
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
                    string pcaCode = gridLetter + "." + peakIndex.ToString();
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
                string unassignedPcaCode = gridLetter + ".0";
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
        Dictionary<string, HashSet<VoxelID>> voxelBuckets = VoxelBuckets(pcaCodes);

        // first, lets dump some info about populations of all the different 
        // pca Codes:
        Dictionary<string, int> pcaCodeCounts = PcaCodeCounts(voxelBuckets);
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

    internal List<VoxelID> FindMatchingSets(VoxelID neighborID, Dictionary<VoxelID, HashSet<VoxelID>> voxelSets)
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

    // filterForMatchableCode identifies which of the pcaCodes in nonZeroPcaCodes are
    // 'one-away' matches for pcaCode
    // The reason we use a . to separate the letter from the number in PCA codes is to 
    // make it easy to identify a zero -- i.e. the algorithm here also
    // works if there are 10 peaks, because "A.10" doesn't contain ".0"
    internal HashSet<string> filterForMatchableCode(List<string> nonZeroPcaCodes, string pcaCode)
    {
        // if pcaCode contains zero 0s or more than one 0, return empty List
        HashSet<string> matches = new HashSet<string>();
        string[] components = pcaCode.Split(".0");
        if (components.Count() == 2)
        {
            // now, matching is easy -- see if both components of the split are part of a candidate code
            // and if so, its a match.  Edge case for when the second component is zero length -- no need to check there

            foreach (string candidate in nonZeroPcaCodes)
            {
                if (candidate.Contains(components[0]) &&
                     ((components[1].Length == 0) || candidate.Contains(components[1])))
                {
                    matches.Add(candidate);
                }
            }
        }
        return matches;
    }

    string interfaceIdForCodes(List<string> pcaCodes)
    {
        List<string> codesList = new List<string>(pcaCodes);
        codesList.Sort();
        string interfaceId = codesList[0];
        int numCodes = codesList.Count();
        for (int i = 1; i < numCodes; ++i)
        {
            interfaceId += "," + codesList[i];
        }
        return interfaceId;
    }

    void addVoxelsToHashSetDictionary(Dictionary<string, HashSet<VoxelID>> additions, Dictionary<string, HashSet<VoxelID>> sets)
    {
        foreach (KeyValuePair<string, HashSet<VoxelID>> kvp in additions)
        {
            HashSet<VoxelID> hashSet;
            if (sets.ContainsKey(kvp.Key))
            {
                hashSet = sets[kvp.Key];
            }
            else
            {
                hashSet = new HashSet<VoxelID>();
            }
            foreach (VoxelID voxelId in kvp.Value)
            {
                hashSet.Add(voxelId);
            }
            sets[kvp.Key] = hashSet;
        }
    }

    
    void subtractVoxelsFromHashSetDictionary(Dictionary<string, HashSet<VoxelID>> subtractions, Dictionary<string, HashSet<VoxelID>> sets)
    {
        foreach (KeyValuePair<string, HashSet<VoxelID>> kvp in subtractions)
        {
            HashSet<VoxelID> hashSet;
            if (sets.ContainsKey(kvp.Key))
            {
                hashSet = sets[kvp.Key];
            }
            else
            {
                hashSet = new HashSet<VoxelID>();
            }
            foreach (VoxelID voxelId in kvp.Value)
            {
                hashSet.Remove(voxelId);
            }
            sets[kvp.Key] = hashSet;
        }
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
        PcaStream pcaStream = new PcaStream("GetPhasesStrategyE");
        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIds);

        float binSeparation = 0.5f;
        int numDimsToInclude = Math.Min(3, this.scoreDims); // 3 for strategy D
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
        for (int i = 0; i < (numDimsToInclude - 1); ++i)
        {
            for (int j = i + 1; j < numDimsToInclude; ++j)
            {
                string gridLetter = CharValueForASCII(AAsciiValue + gridIndex).ToString();
                string gridId = gridLetter + i.ToString() + "-" + j.ToString();

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
                    string pcaCode = gridLetter + "." + peakIndex.ToString();
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
                string unassignedPcaCode = gridLetter + ".0";
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

                var projection = new TwoDPeakProjection(twoDGrid);
                phaseIdResults.SetTwoDPeakProjectionFor(gridId, projection);

                gridIndex += 1;
            }
        }

        // now examine the groups of voxels to identify contiguous regions
        // in this case, 'unassigned' means voxels not yet associated with a list of voxels in a pcaPhase
        Dictionary<string, HashSet<VoxelID>> unassignedVoxelBuckets = VoxelBuckets(pcaCodes);

        // first, lets dump some info about populations of all the different 
        // pca Codes:
        Dictionary<string, int> pcaCodeCounts = PcaCodeCounts(unassignedVoxelBuckets);
        DumpPcaCodeCounts(pcaCodeCounts);

        // At this stage we want to identify which voxels are part of contiguous regions of phases
        // At first, assume that all PCA codes that have no zero value in them identify a particular phase
        // So, generate a list of voxels for each of these phases 

        List<string> nonZeroPcaCodes = unassignedVoxelBuckets.Keys.Where(code => !code.Contains(".0")).ToList();
        Dictionary<string, HashSet<VoxelID>> pcaCodeVoxelSets = new Dictionary<string, HashSet<VoxelID>>();
        // pcaCodes is a Dictionary<VoxelID, string>
        foreach (string pcaCode in nonZeroPcaCodes)
        {
            // just copy over the voxel List to be the base list for  that phase
            pcaCodeVoxelSets[pcaCode] = unassignedVoxelBuckets[pcaCode];
            unassignedVoxelBuckets.Remove(pcaCode); 
        }

        // now pcaCodeVoxelSets is filled with data for each pcaCode,
        // These are our 'core' regions
        // now lets go through the voxels with PcaCodes that contain a single 0
        // and see if they might be adjacent to voxels in any of the core regions  
        // where that 0 is non-zero
        // three possibilities:
        //   1) not adjacent to any
        //   2) adjacent to voxels sharing a single pcaCode  ( i.e. A1B0C1 adjacent to A1B1C1)
        //   3) adjacent to voxels of multiple pcaCodes  ( i.e. A1B0C1 adjacent to both A1B1C1 and A1B2C1 voxels)
        //
        // for case 1) do nothing
        // for case 2) add the voxels to the core regions
        // for case 3) designate the voxel as an interface voxel

        Dictionary<string, HashSet<VoxelID>> unassignedSubtractions = new Dictionary<string, HashSet<VoxelID>>();
        Dictionary<string, HashSet<VoxelID>> voxelSetAdditions = new Dictionary<string, HashSet<VoxelID>>();
        Dictionary<string, HashSet<VoxelID>> interfaceVoxelSets = new Dictionary<string, HashSet<VoxelID>>();
        Dictionary<string, HashSet<VoxelID>> interfaceVoxelSetAdditions = new Dictionary<string, HashSet<VoxelID>>();
        int numNonZeroPcaCodes = nonZeroPcaCodes.Count();
        for (int nOuter = 0; nOuter < numNonZeroPcaCodes; ++nOuter)
        {
            string nonZeroPcaCode = nonZeroPcaCodes[nOuter];
            voxelSetAdditions[nonZeroPcaCode] = new HashSet<VoxelID>();
        }

        List<string> unassignedPcaCodes = unassignedVoxelBuckets.Keys.ToList();
        foreach (string pcaCode in unassignedPcaCodes)
        {
            HashSet<string> matchableCodes = filterForMatchableCode(nonZeroPcaCodes, pcaCode);
            // matchableCodes are the nonZeroPcaCodes that are "one away" from the pcaCode under consideration

            HashSet<VoxelID> potentialAdditions = unassignedVoxelBuckets[pcaCode]; 
            HashSet<VoxelID> subtractions = new HashSet<VoxelID>();
            List<string> matches = new List<string>(); 
            foreach (VoxelID voxelId in potentialAdditions)
            {
                List<VoxelID> neighbors = voxelId.NeighborVoxels(gridDims);
                matches.Clear();

                foreach (string matchingCode in matchableCodes)
                {
                    if (pcaCodeVoxelSets[matchingCode].ContainsAny(neighbors))
                    {
                        matches.Add(matchingCode);
                    }
                }

                // case 1 is no matches 
                if (matches.Count() > 0)
                {
                    if (matches.Count() == 1)
                    {
                        voxelSetAdditions[matches[0]].Add(voxelId);
                        subtractions.Add(voxelId);
                    }
                    else
                    {
                        string interfaceId = interfaceIdForCodes(matches);
                        if (interfaceVoxelSetAdditions.ContainsKey(interfaceId))
                        {
                            interfaceVoxelSetAdditions[interfaceId].Add(voxelId);
                        }
                        else
                        {
                            HashSet<VoxelID> newSet = new HashSet<VoxelID>();
                            newSet.Add(voxelId);
                            interfaceVoxelSetAdditions[interfaceId] = newSet;
                        }
                        subtractions.Add(voxelId);
                    }
                }
            }
            unassignedSubtractions[pcaCode] = subtractions;
        }
        // now, any voxel that is part of interfaceVoxelSetAdditions
        // or voxelSetAdditions should get taken out of unassignedVoxelBuckets
        // and added to interfaceVoxelSets or pcaCodeVoxelSets
        pcaStream.DumpVoxelSetStats("start pcaCodeVoxelSets", pcaCodeVoxelSets);
        pcaStream.DumpVoxelSetStats("startUnassigned", unassignedVoxelBuckets);
        pcaStream.DumpVoxelSetStats("voxelSetAdditions", voxelSetAdditions);
        pcaStream.DumpVoxelSetStats("interfaceVoxelSetAdditions", interfaceVoxelSetAdditions);
        addVoxelsToHashSetDictionary(interfaceVoxelSetAdditions, interfaceVoxelSets);
        addVoxelsToHashSetDictionary(voxelSetAdditions, pcaCodeVoxelSets);
        subtractVoxelsFromHashSetDictionary(unassignedSubtractions, unassignedVoxelBuckets);
        pcaStream.DumpVoxelSetStats("assignments sources", unassignedSubtractions);
        pcaStream.DumpVoxelSetStats("new pcaCodeVoxelSets", pcaCodeVoxelSets);
        pcaStream.DumpVoxelSetStats("stillUnassigned", unassignedVoxelBuckets);

        // now, pcaCodeVoxelSets is ready to be used for define a per-voxel component mapping
        // lets order the pcaCodeVoxelSets by population
        List<string> pcaCodesByPopulation = pcaCodeVoxelSets.Keys.ToList();
        PopulationSorter<string, VoxelID> comparator = new PopulationSorter<string, VoxelID>(pcaCodeVoxelSets);
        pcaCodesByPopulation.Sort(comparator);

        // now pcaCodesByPopulation is sorted?
        // make a pcaCode to phaseIndex map:
        // 
        Dictionary<string, int> phaseIndexMap = new Dictionary<string, int>();
        int phaseIndex = 1;
        foreach (string pcaCode in  pcaCodesByPopulation)
        {
            phaseIndexMap[pcaCode] = phaseIndex;
            phaseIndex += 1;
        }

        // and, call IdentifyVoxelAs for each voxel
        // phaseResults initializes phase ID to zero for each voxel
        // So, we only need to assign voxels that have landed in the pcaCodeVoxelSets
        foreach (VoxelID voxelId in voxelIds)
        {
            phaseIdResults.IdentifyVoxelAs(voxelId, 0);
        }
            foreach (KeyValuePair<string, HashSet<VoxelID>> kvp in pcaCodeVoxelSets)
            {
            string nthPcaCode = kvp.Key;
            
            if (phaseIndexMap.ContainsKey(nthPcaCode))
            {
                int voxelphase = phaseIndexMap[nthPcaCode];
                if (voxelphase > 4)
                {
                    voxelphase = 6;
                }
                foreach(VoxelID voxelId in kvp.Value)
                {
                    phaseIdResults.IdentifyVoxelAs(voxelId, voxelphase);
                }
            }   
        }

        // for exery interface voxel, set it to type 5:
        //  Dictionary<string, HashSet<VoxelID>> interfaceVoxelSets = new Dictionary<string, HashSet<VoxelID>>();
        foreach (KeyValuePair<string, HashSet<VoxelID>> kvp in interfaceVoxelSets)
        {
            foreach (VoxelID voxelId in kvp.Value)
            {
                phaseIdResults.IdentifyVoxelAs(voxelId, 5);
            }
        }

        pcaStream.Close();
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




