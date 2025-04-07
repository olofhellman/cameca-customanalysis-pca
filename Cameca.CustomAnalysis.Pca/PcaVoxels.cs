using System.Collections.Generic;
using System.Linq;
using System;
using Prism.Regions;

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
    public void ToConsole(string prefix) {  
        Console.WriteLine(prefix + this.x, ", ", + this.y, ", ", + this.z);
    }
};

public struct SlopeSum
{
    public float slopeSum; // based entirely on the values of neighbors
    public float deltaSum; // sum of differences with neighbors

    public SlopeSum(float slope, float delta)
    {
        slopeSum = slope;
        deltaSum = delta;
    }
};

public struct PcaVoxel
{
    public int voxelIndex; // the voxelIndex from compResults.VoxelIndices;  (between 0 and nTotalVoxels - 1)
    public float[] scoreVector;

    public PcaVoxel(int vIndex, float[] vec)
    {
        voxelIndex = vIndex;
        scoreVector = vec;
    }
}

public interface IScoresProvider
{
    public float[] GetScores(int voxelIndex);
}
public class PcaScoresGrid
{
    Dictionary<int, PcaVoxel> pcaVoxels;
    Dictionary<int, SlopeSum> slopeSums;
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
        slopeSums = calculateSlopeSums(pcaVoxels, gridDimensions);
        xDim = gridDimensions[0];
        yDim = gridDimensions[1];
        zDim = gridDimensions[2];

        nVoxels = xDim * yDim * zDim;
    }

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
        for (int z=0; z < zDim; ++z)
        {
            int zOffset = z * zMult;
            int? prevZ = (z == 0) ? null : (z - 1);
            int? nextZ = (z == zDim - 1) ? null : (z + 1);
            for (int y=0; y < yDim; ++y)
            {
                int yzOffset = zOffset + (y * xDim) ;
                int? prevY = (y == 0) ? null : (y - 1);
                int? nextY = (y == yDim - 1) ? null : (y + 1);
                for (int x=0; x < xDim; ++x)
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

    // Return a List<int> of the voxelIndices in a contiguous region around the seedVoxel that are similar enough
    // to the seed voxel to be considered in the same component
    List<int> seedFill(List<int> voxelIndices, int seedVoxelIndex, float[] referenceScore, float threshhold) {
        var groupedVoxels = new List<int>();
        var rejectedVoxels = new List<int>();
        var candidates = new List<int>();
        var remainingVoxels = new List<int>(voxelIndices);
        remainingVoxels.Remove(seedVoxelIndex);
        candidates.Add(seedVoxelIndex);
        while (candidates.Count > 0) {
            var currentVoxelIndex = candidates[0];
            candidates.RemoveAt(0); 
            if (candidateWithinThresholdOfReferenceScore(currentVoxelIndex, referenceScore, threshhold)) {
                groupedVoxels.Add(currentVoxelIndex);
                var neighborIndices = NeighborIndicesFor(GridCoordFor(currentVoxelIndex));
                foreach (int neighborIndex in neighborIndices) {
                    if (remainingVoxels.Contains(neighborIndex)) {
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
            Console.Write("seedFill found " + filledVoxels.Count + " voxels");
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
 
}