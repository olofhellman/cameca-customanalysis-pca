using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;
 
 
 

public interface IScoresProvider
{
    public float[] GetScores(int voxelIndex);
}
public class PcaScoresGrid
{
    Dictionary<int, PcaVoxel> pcaVoxels; // the key is the 'id' for the voxel, from which you can
                                         // calculate the x,y,z position of the voxel on the grid if
                                         // the grid dimensions are known
    // Dictionary<int, SlopeSum> slopeSums;
    int scoreDims;
    int xDim;
    int yDim;
    int zDim;
    int nVoxels;

    int VoxelIndexFor(GridCoord gridCoord)
    {
        return gridCoord.x + (gridCoord.y * xDim) + (gridCoord.z * xDim * yDim);
    }

    GridCoord GridCoordFor(int voxelIndex)
    {
        int zOffset = xDim * yDim;
        int yOffset = xDim;
        int zCoord = voxelIndex / zOffset;
        int rem = voxelIndex % zOffset;
        int yCoord = rem / yOffset;
        rem = rem % yOffset;
        int xCoord = rem;
        return new GridCoord(xCoord, yCoord, zCoord);
    }

    // returns a list of 27 indices, unless the voxel is near the edge
    List<int> NeighborIndicesFor(GridCoord gridCoord)
    {
        List<int> neighborIndices = new List<int>();
        //List<int> xIndices = new List<int>();
        //List<int> yIndices = new List<int>();
        //List<int> zIndices = new List<int>();
        int minx = Math.Max(0, gridCoord.x - 1);
        int maxx = Math.Min(xDim - 1, gridCoord.x + 1);
        int miny = Math.Max(0, gridCoord.y - 1);
        int maxy = Math.Min(yDim - 1, gridCoord.y + 1);
        int minz = Math.Max(0, gridCoord.z - 1);
        int maxz = Math.Min(zDim - 1, gridCoord.z + 1);
        for (int z = minz; z <= maxz; ++z)
        {
            for (int y = miny; y <= maxy; ++y)
            {
                for (int x = minx; x <= maxx; ++x)
                {
                    neighborIndices.Add(x + (y * xDim) + (z * xDim * yDim));
                }
            }
        }
        return neighborIndices;
    }
    public PcaScoresGrid(IScoresProvider scoresProvider, List<int> voxelIndices, int scoreDimensions, int[] gridDimensions)
    {
        scoreDims = scoreDimensions;

        pcaVoxels = pcaVoxelsInit(scoresProvider, voxelIndices, gridDimensions);
        //  slopeSums = calculateSlopeSums(pcaVoxels, gridDimensions);
        xDim = gridDimensions[0];
        yDim = gridDimensions[1];
        zDim = gridDimensions[2];

        nVoxels = xDim * yDim * zDim;
    }

    /*
    public GridCoord? findMinDeltaVoxelWithStartingPoint(GridCoord gridCoord)
    {
        // find the voxel with the minimum delta
        // given the starting point, search the adjacent 26 voxels to find one with the least slope
        // repeat with the new voxel
        int voxelIndex = VoxelIndexFor(gridCoord);
        var currentVoxel = pcaVoxels[voxelIndex];
        int prevVoxel = -1;
        while (prevVoxel != voxelIndex)
        {
            prevVoxel = voxelIndex;
            List<int> neightborIndices = NeighborIndicesFor(GridCoordFor(voxelIndex));
            int bestVoxelIndex = neightborIndices[0];
            float bestScore = slopeSums[bestVoxelIndex].deltaSum;
            for (int i = 1; i < neightborIndices.Count; ++i)
            {
                int neighborIndex = neightborIndices[i];
                float neighborScore = slopeSums[neighborIndex].deltaSum;
                if (neighborScore < bestScore)
                {
                    bestVoxelIndex = neighborIndex;
                    bestScore = neighborScore;
                }
            }
            voxelIndex = bestVoxelIndex;
        }
        // if the best score is less than the current score, return the new voxel

        return GridCoordFor(voxelIndex);
    }
    */

    static SlopeSum slopeSumForVoxel(Dictionary<int, PcaVoxel> voxels, int voxelIndex, int? prevZ, int? nextZ, int? prevY, int? nextY, int? prevX, int? nextX)
    {
        float slopeSum = 0;
        float deltaSum = 0;
        PcaVoxel voxel;
        if (!voxels.TryGetValue(voxelIndex, out voxel))
        {
            return new SlopeSum(0.0f, 0.0f);
        }

        int scoreDims = voxel.scoreVector.Length;
        PcaVoxel emptyVoxel = new PcaVoxel(-1, new float[scoreDims]);

        float[] prevZScores = new float[scoreDims];
        float[] nextZScores = new float[scoreDims];
        float[] prevYScores = new float[scoreDims];
        float[] nextYScores = new float[scoreDims];
        float[] prevXScores = new float[scoreDims];
        float[] nextXScores = new float[scoreDims];
        PcaVoxel neighborVoxel;
        if (prevZ.HasValue)
        {
            int prevZIndex = prevZ.Value;
            if (!voxels.TryGetValue(prevZIndex, out neighborVoxel))
            {
                neighborVoxel = emptyVoxel;
            }
            for (int i = 0; i < scoreDims; ++i)
            {
                float ithScore = neighborVoxel.scoreVector[i];
                prevZScores[i] = ithScore;
                deltaSum += Math.Abs(voxel.scoreVector[i] - ithScore);
            }

        }
        if (nextZ.HasValue)
        {
            int nextZIndex = nextZ.Value;
            if (!voxels.TryGetValue(nextZIndex, out neighborVoxel))
            {
                neighborVoxel = emptyVoxel;
            }
            for (int i = 0; i < scoreDims; ++i)
            {
                float ithScore = neighborVoxel.scoreVector[i];
                nextZScores[i] = ithScore;
                deltaSum += Math.Abs(voxel.scoreVector[i] - ithScore);
            }

        }
        if (prevY.HasValue)
        {
            int prevYIndex = prevY.Value;
            if (!voxels.TryGetValue(prevYIndex, out neighborVoxel))
            {
                neighborVoxel = emptyVoxel;
            }
            for (int i = 0; i < scoreDims; ++i)
            {
                float ithScore = neighborVoxel.scoreVector[i];
                prevYScores[i] = ithScore;
                deltaSum += Math.Abs(voxel.scoreVector[i] - ithScore);
            }
        }
        if (nextY.HasValue)
        {
            int nextYIndex = nextY.Value;
            if (!voxels.TryGetValue(nextYIndex, out neighborVoxel))
            {
                neighborVoxel = emptyVoxel;
            }
            for (int i = 0; i < scoreDims; ++i)
            {
                float ithScore = neighborVoxel.scoreVector[i];
                nextYScores[i] = ithScore;
                deltaSum += Math.Abs(voxel.scoreVector[i] - ithScore);
            }
        }
        if (prevX.HasValue)
        {
            int prevXIndex = prevX.Value;
            if (!voxels.TryGetValue(prevXIndex, out neighborVoxel))
            {
                neighborVoxel = emptyVoxel;
            }
            for (int i = 0; i < scoreDims; ++i)
            {
                float ithScore = neighborVoxel.scoreVector[i];
                prevXScores[i] = ithScore;
                deltaSum += Math.Abs(voxel.scoreVector[i] - ithScore);
            }
        }
        if (nextX.HasValue)
        {
            int nextXIndex = nextX.Value;
            if (!voxels.TryGetValue(nextXIndex, out neighborVoxel))
            {
                neighborVoxel = emptyVoxel;
            }
            for (int i = 0; i < scoreDims; ++i)
            {
                float ithScore = neighborVoxel.scoreVector[i];
                nextXScores[i] = ithScore;
                deltaSum += Math.Abs(voxel.scoreVector[i] - ithScore);
            }
        }

        for (int i = 0; i < scoreDims; ++i)
        {
            slopeSum += Math.Abs(nextXScores[i] - prevXScores[i]);
            slopeSum += Math.Abs(nextYScores[i] - prevYScores[i]);
            slopeSum += Math.Abs(nextZScores[i] - prevZScores[i]);
        }
        return new SlopeSum(slopeSum, deltaSum);
    }

    static Dictionary<int, SlopeSum> calculateSlopeSums(Dictionary<int, PcaVoxel> voxels, int[] gridDimensions)
    {
        int xDim = gridDimensions[0];
        int yDim = gridDimensions[1];
        int zDim = gridDimensions[2];
        var sums = new Dictionary<int, SlopeSum>();
        int zMult = xDim * yDim;
        for (int z = 0; z < zDim; ++z)
        {
            int zOffset = z * zMult;
            int? prevZ = (z == 0) ? null : (z - 1);
            int? nextZ = (z == zDim - 1) ? null : (z + 1);
            for (int y = 0; y < yDim; ++y)
            {
                int yzOffset = zOffset + (y * xDim);
                int? prevY = (y == 0) ? null : (y - 1);
                int? nextY = (y == yDim - 1) ? null : (y + 1);
                for (int x = 0; x < xDim; ++x)
                {
                    int? prevX = (x == 0) ? null : (x - 1);
                    int? nextX = (x == xDim - 1) ? null : (x + 1);
                    int voxelIndex = yzOffset + x;
                    SlopeSum slopeSum = slopeSumForVoxel(voxels, voxelIndex, prevZ, nextZ, prevY, nextY, prevX, nextX);
                    sums[voxelIndex] = slopeSum;
                }
            }
        }
        return sums;
    }

    static Dictionary<int, PcaVoxel> pcaVoxelsInit(IScoresProvider scoresProvider, List<int> voxelIndices, int[] gridDimensions)
    {
        var voxels = new Dictionary<int, PcaVoxel>();
        int nIndices = voxelIndices.Count;

        for (int i = 0; i < nIndices; ++i)
        {
            float[] nthVector = scoresProvider.GetScores(i);

            var pcaVoxel = new PcaVoxel(voxelIndices[i], nthVector);
            voxels[voxelIndices[i]] = pcaVoxel;
        }

        return voxels;
    }

    /*
    bool candidateWithinThreshold(int voxelIndex, float threshold)
    {
        var slopeSum = slopeSums[voxelIndex];
        float deltaSum = slopeSum.deltaSum;
        return deltaSum < threshold;
    }
    bool candidateWithinThresholdOfReferenceScore(int voxelIndex, float[] referenceScore, float threshold)
    {
        var candidateVoxel = pcaVoxels[voxelIndex];
        float deltaSum = 0.0f;
        for (int i = 0; i < referenceScore.Length; ++i)
        {
            deltaSum += Math.Abs(candidateVoxel.scoreVector[i] - referenceScore[i]);
        }
        return deltaSum < threshold;
    }
    */
    /*
    // Return a List<int> of the voxelIndices in a contiguous region around the seedVoxel that are similar enough
    // to the seed voxel to be considered in the same component
    List<int> seedFill(List<int> voxelIndices, int seedVoxelIndex, float[] referenceScore, float threshhold)
    {
        var groupedVoxels = new List<int>();
        var rejectedVoxels = new List<int>();
        var candidates = new List<int>();
        var remainingVoxels = new List<int>(voxelIndices);
        remainingVoxels.Remove(seedVoxelIndex);
        candidates.Add(seedVoxelIndex);
        while (candidates.Count > 0)
        {
            var currentVoxelIndex = candidates[0];
            candidates.RemoveAt(0);
            if (candidateWithinThresholdOfReferenceScore(currentVoxelIndex, referenceScore, threshhold))
            {
                groupedVoxels.Add(currentVoxelIndex);
                var neighborIndices = NeighborIndicesFor(GridCoordFor(currentVoxelIndex));
                foreach (int neighborIndex in neighborIndices)
                {
                    if (remainingVoxels.Contains(neighborIndex))
                    {
                        remainingVoxels.Remove(neighborIndex);
                        candidates.Add(neighborIndex);
                    }
                }
            }
            else
            {
                rejectedVoxels.Add(currentVoxelIndex);
            }
        }
        return groupedVoxels;
    }
    */

    // strategy:
    // Prep:  have list of voxel candidates containing all voxels
    // Prep: define a minimization function that can find a 'least slope' voxel for starting a seed fill
    //       'least slope' a minimum of the dS/dP (Slope and Position)
    // 0) use erosion algorithm to find likely starting voxel for step 1)
    // 1) use 'least slope' on the componentsResults to find a likely seed value for a vector in scores. If no minimum found, remove traversed voxels from candidates list and return to 0)
    // 2) use seed fill to find cluster of neighboring voxels - label as phase N -- remove labelled voxels (and neighbors?) from candidates list
    // 3) find voxels near least slope value not identified in 2)
    // 4) use seed fill on voxels identified in 3), label voxels so identified as "Phase N" -- remove labelled voxels (and neighbors?) from candidates list
    // 5) increment N and return to step 1
    // return to step 0 using new candidates list
    // if step 1 can't find a minimum, algorithm is finished
    
    
    /*
     public PhaseIdResults GetPhasesStrategyB()
    {
        List<int> voxelIndices = pcaVoxels.Keys.ToList();

        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIndices, nVoxels);
        int[] dimensions = new int[3];
        dimensions[0] = xDim;
        dimensions[1] = yDim;
        dimensions[2] = zDim;
        ErosionFinder erosionFinder = new ErosionFinder(voxelIndices, dimensions);
        var insideVoxels = erosionFinder.FindInnerVoxels();
        var startingPointGridCoord = GridCoordFor(insideVoxels[0]);
        GridCoord? bestSeedVoxel = findMinDeltaVoxelWithStartingPoint(startingPointGridCoord);
        int region = 1;

        while (bestSeedVoxel.HasValue && (region < 5))
        {
            if (bestSeedVoxel.HasValue)
            {
                bestSeedVoxel.Value.ToConsole("bestSeedVoxel: ");
            }
            var seedVoxel = bestSeedVoxel.Value;
            var seedVoxelIndex = VoxelIndexFor(seedVoxel);
            var threshold = 7.0f;

            var referenceVoxel = pcaVoxels[seedVoxelIndex];
            float[] referenceScore = referenceVoxel.scoreVector;

            var filledVoxels = seedFill(voxelIndices, seedVoxelIndex, referenceScore, threshold);
            Debug.Write("seedFill found " + filledVoxels.Count + " voxels");
            phaseIdResults.IdentifyVoxelsAs(filledVoxels, region);
            region = region + 1;
            // remove the voxel from the list of candidates
            voxelIndices = voxelIndices.Except(filledVoxels).ToList();
            erosionFinder.SetVoxelIndices(voxelIndices);

            // set up for next seed fill
            insideVoxels = erosionFinder.FindInnerVoxels();
            startingPointGridCoord = GridCoordFor(insideVoxels[0]);
            bestSeedVoxel = findMinDeltaVoxelWithStartingPoint(startingPointGridCoord);
        }

        return phaseIdResults;
    }
    */

    // This strategy looks first at finding density peaks in PCA space
    // Then, draws N dimensional spheres around each peak and assigns voxels in those spheres to be the given component
    // On a first pass, the data is binned one dimensionally in each of the PCA dimensions
    // This allows A) ranges to be set and B) predictions to be made about what the expected population of 
    // an N dimensional bin at any N dimensional coordinate if the values where uncorrelated
    // then a 2D binning is made with the first two PCA dimensions, and deviations from the calculated populations are 
    // estimated

    public PhaseIdResults GetPhasesStrategyC()
    {
        List<int> voxelIndices = pcaVoxels.Keys.ToList();

        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIndices, nVoxels);

        // oneDDensities contains one list for each PCA dimension
        // each list is a smoothed histogram of populations 
        // first float is coordinate point, second float is the population
        // on first pass, the coordinate points are 0.5 apart
        // So, if there are points between 0 and 10, there may be 23 entries, from -0.5 to 10.5
        // at spacings of 0.5
        // float binSeparation = 0.5f;
        // List<DensityProfile> oneDDensities = CalculateOneDDensities(voxelIndices, binSeparation);
        // for (int i = 0; i < this.scoreDims; ++i)
        // {
        //     oneDDensities[i].ConsoleDump("density " + i);
        // }

        float binSeparation = 0.5f;
        int gridDims = Math.Min(2, this.scoreDims); // 2 is the simplest choice 
        Dictionary<int, DensityPlane> twoDGrids = new Dictionary<int, DensityPlane>();
        Dictionary<int, List<List<PixelID>>> partitions = new Dictionary<int, List<List<PixelID>>>();
        // now, make twoD grids using all pairs of dimensions
        for (int i = 0; i < (gridDims - 1); ++i)
        {
            for (int j = i + 1; j < gridDims; ++j)
            {
                int gridId = 10 * i + j;
                var twoDGrid = CalculateTwoDDensity(voxelIndices, i, j, binSeparation);
                twoDGrid.ConsoleDump("dgrid" + i + j);
                twoDGrids[gridId] = twoDGrid;
                List<List<PixelID>> partitionedIndices = IdentifyPartitions(twoDGrid, i, j, voxelIndices);
                partitions[gridId] = partitionedIndices;
            }
        }




        // now turn the partitions array into the data needed by phaseIDResults
        // phaseIDResults has a method  IdentifyVoxelAs(int voxelIds, int phase)
        // so, for each peak identified by the 2d grid partition, 
        // make a list of voxels corresponding to that peak.
        // a voxel corresponds to the peak if its PCA scores for the given two dimensions 
        // is in a pixel of a peak.
        // 
        // to avoid looking up whether o not the pixel from each voxel is in one of the peaks
        // first, group all the voxelIDs into leist for each pixelID
        // then do a single lookup for each pixelID

        if (gridDims > 1)
        {
            int firstGridId = 1;
            DensityPlane grid = twoDGrids[firstGridId];
            List<List<PixelID>> firstGridPartitions = partitions[firstGridId];

            // for debugging, dump the partitions:
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

            Dictionary<PixelID, List<int>> voxelLists = new Dictionary<PixelID, List<int>>();
            foreach (int voxelId in voxelIndices)
            {
                PcaVoxel voxel = pcaVoxels[voxelId];
                PixelID nthPixelId = grid.PixelIDFor(voxel.scoreVector[0], voxel.scoreVector[1]);
                List<int>? voxelIDListForGridCoord;
                if (voxelLists.TryGetValue(nthPixelId, out voxelIDListForGridCoord))
                {
                    voxelIDListForGridCoord.Add(voxelId);
                }
                else
                {
                    voxelIDListForGridCoord = new List<int>();
                    voxelIDListForGridCoord.Add(voxelId);
                    voxelLists[nthPixelId] = voxelIDListForGridCoord;
                }
            }

            // now, each voxelIndex is a member of one of the lists in voxelLists
            // go through each list in voxelLists and see if its pixel is designated in a particular partiion
           
             
            int listN = 1;
            foreach (List<PixelID> pixelIdList in firstGridPartitions)
            {
                // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                foreach (PixelID pixelID in pixelIdList)
                {
                    // Lookup for all the voxels bucketed under this pixelId
                    List<int> voxelIds = voxelLists[pixelID];
                    foreach (int voxelId in voxelIds)
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
                List<int> voxelIds = voxelLists[pixelID];
                foreach (int voxelId in voxelIds)
                {
                    phaseIdResults.IdentifyVoxelAs(voxelId, 0);
                }
            }
        }

        // using a twoDGrid, return lists of voxel indices that are in clusters or not
        // to identify clusters, first, look for maxima in the twoD grid
        // for each maximum, find connected pixels reachable in a gradient descent,
        // stopping at 5% of the first peak
        // pixels reachable from multiple peaks should not be classified as either
        // 5% of the first peak is also the threshhold for finding maxima






        // var twoDGridAB = calculateTwoDDensity(voxelIndices, 0, 1, binSeparation);
        // twoDGridAB.ConsoleDump("dgrid01");
        // var twoDGridAC = calculateTwoDDensity(voxelIndices, 0, 2, binSeparation);
        // twoDGridAC.ConsoleDump("dgrid02");
        // var twoDGridBC = calculateTwoDDensity(voxelIndices, 1, 2, binSeparation);
        // twoDGridBC.ConsoleDump("dgrid12");




        return phaseIdResults;
    }

    // identifyPartitions use the density map from the twoDGrid to separate 
    // voxels that belong to different peaks in the DensityPlane
    // first, identify the peaks and their associated pixels
    // then, for each voxel, see if it lands in on of the partitioned pixels.
    // If it does, add it to the appropriate list
    // return the list of lists
    // indices not identified are not returned in any list
    public List<List<PixelID>> IdentifyPartitions(DensityPlane twoDGrid, int dimX, int dimy, List<int> voxelIndices)
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

    public DensityPlane CalculateTwoDDensity(List<int> voxelIndices, int dimx, int dimy, float binsize)
    {
        DensityPlane densityPlane = new DensityPlane(binsize);
        for (int v = 0; v < voxelIndices.Count; ++v)
        {
            int voxelIndex = voxelIndices[v];
            var voxel = pcaVoxels[voxelIndex];
            var xVal = voxel.scoreVector[dimx];
            var yVal = voxel.scoreVector[dimy];
            densityPlane.AddPointAt(xVal, yVal);
        }
        return densityPlane;
    }

   

    public List<DensityProfile> CalculateOneDDensities(List<int> voxelIndices, float binsize)
    {
        List<DensityProfile> densities = new List<DensityProfile>();
        for (int i = 0; i < this.scoreDims; ++i)
        {
            var nthDensityList = new DensityProfile(binsize);
            densities.Add(nthDensityList);
        }

        for (int v = 0; v < voxelIndices.Count; ++v)
        {
            int voxelIndex = voxelIndices[v];
            var voxel = pcaVoxels[voxelIndex];
            for (int i = 0; i < this.scoreDims; ++i)
            {
                densities[i].AddPointAt(voxel.scoreVector[i]);
            }
        }
        return densities;
    }
}




