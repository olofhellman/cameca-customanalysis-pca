using Cameca.CustomAnalysis.Interface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca;

internal static class PcaCalculator
{
    /*
     * Per Mike Keenan regarding adjustments to scores and loadings:
     * The scores are O(10-3). The loadings in this case are O(1000).
     * The data matrix is modeled as a product of the scores and loadings.
     * So, if you multiply the scores by 1000 and divide the loadings by 1000,
     * both factors will be O(1), which is more in line with the expected magnitude of data-matrix elements.
     * This would probably be a good thing to do.
     */
    private const float ScoresCoefficient = 1000f;
    private const float LoadingsCoefficient = 0.001f;

    public static EigenvalueResults GetEignevalues(IIonData ionData, IGrid3DData gridData)
    {
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];
        int nFeatures = ionData.Ions.Count;

        var localBuffer = Enumerable.Range(0, nFeatures)
            .Select(ionIndex => gridData.GetDataForIon(ionIndex).ToArray())
            .ToArray();
        var nonEmptyVoxels = new List<int>();

        // string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        // string outputFilename = System.IO.Path.Combine(docPath, "FilledVoxels.surf");
 
        // using (StreamWriter outputFile = new StreamWriter(outputFilename))
        // {
            // outputFile.WriteLine("surf file");
            // outputFile.WriteLine("triangle vertices:");

 
            for (int voxelIndex = 0; voxelIndex < nAllVoxels; voxelIndex++)
            {
                for (int ionIndex = 0; ionIndex < nFeatures; ionIndex++)
                {
                    if (localBuffer[ionIndex][voxelIndex] != 0f)
                    {
                        nonEmptyVoxels.Add(voxelIndex);
                       //  outputFile.Write("x");
                        break;
                    }
                    
                }

                // outputFile.Write("."); // need to add logic to not write if we wrote an x
            }

            // outputFile.Close();
        // }


        int nVoxels = nonEmptyVoxels.Count;

            var dataBuffer = new float[nFeatures * nVoxels];
            for (int featureIndex = 0; featureIndex < nFeatures; featureIndex++)
            {
                int x = 0;
                foreach (int voxelIndex in nonEmptyVoxels)
                {
                    dataBuffer[(featureIndex * nVoxels) + x++] = localBuffer[featureIndex][voxelIndex];
                }
            }

            int nevals = nFeatures;  // Input?

            float[] evals = new float[nevals];
   

        PcaLib.doEigen(nVoxels, nFeatures, dataBuffer, nevals, evals);

        return new EigenvalueResults(evals);
    }

    // manipulate the results of PCA to identify phases per voxel
    public static PhaseIdResults GetPhases(IIonData ionData, ComponentsResults compResults)
    {
        return GetPhasesStrategyB(ionData, compResults);
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
    public static PhaseIdResults GetPhasesStrategyB(IIonData ionData, ComponentsResults compResults)
    {
        int nAllVoxels = compResults.Grid3DData.NumVoxels[0] * compResults.Grid3DData.NumVoxels[1] * compResults.Grid3DData.NumVoxels[2];

        PhaseIdResults phaseIdResults = new PhaseIdResults(compResults.VoxelIndices, nAllVoxels);
        PcaScoresGrid scoresGrid = new PcaScoresGrid(compResults);
        ErosionFinder erosionFinder = new ErosionFinder(compResults.VoxelIndices, compResults.Grid3DData);
        var insideVoxels = erosionFinder.FindInnerVoxels();

        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string outputFilename = System.IO.Path.Combine(docPath, "CoreVoxels.surf");
        using (StreamWriter outputFile = new StreamWriter(outputFilename))
        {
            outputFile.WriteLine("voxels file");
            foreach (int voxelIndex in insideVoxels)
            {
                int zDelta = compResults.Grid3DData.NumVoxels[0] * compResults.Grid3DData.NumVoxels[1];
                int z = (int)(voxelIndex / zDelta);
                int rem = voxelIndex % zDelta;
                int yDelta = compResults.Grid3DData.NumVoxels[0];
                int y = (int)(rem / yDelta);
                int x = rem % yDelta;
                outputFile.WriteLine(x + " " + y + " " + z);
            }
            outputFile.Close();
        }

        return phaseIdResults;
    }

    //static float[] scoreForVoxel()
   //{
    //    return [0.0];
   //}

    // strategy:
    // Prep:  have list of voxel candidates containing all voxels
    // 1) use cluster analysis on the componentsResults to find a likely seed value for a vector in scores
    // 2) identify all voxels within a certain distance of the seed value
    // 3) possible: refine seed value based on better statistics and repeat step 2
    // 4) label voxels identified in step 2 as "Phase 1"
    // 5) remove labelled voxels from candidates list
    // return to step 1 using new candidates list
    // if no likely seed value can be generated, algorithm is finished
    public static PhaseIdResults GetPhasesStrategyA(IIonData ionData, ComponentsResults compResults)
    {
        var gridData = compResults.Grid3DData;
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];
        PhaseIdResults phaseIdResults = new PhaseIdResults(compResults.VoxelIndices, nAllVoxels);
        int nComponents = compResults.Components.Count;

        var clusterFinder = new ClusterFinder(compResults);
        int clusterSamples = 25;
       // var clusterVector = clusterFinder.FindClusterVector(clusterSamples);
       // while (clusterVector != NULL)
       // {
       //     clusterVector = clusterFinder.FindClusterVector(clusterSamples);
       // }


        return phaseIdResults;
    }

    public static ComponentsResults GetComponents(IIonData ionData, IGrid3DData gridData, int nComponents)
    {
        ulong rangedCount = ionData.GetIonTypeCounts().Values.Sum();
        int nIons = (int)rangedCount;

        // Remove empty voxels
        int nAllVoxels = gridData.NumVoxels[0] * gridData.NumVoxels[1] * gridData.NumVoxels[2];
        int nFeatures = ionData.Ions.Count;

        var localBuffer = Enumerable.Range(0, nFeatures)
            .Select(ionIndex => gridData.GetDataForIon(ionIndex).ToArray())
            .ToArray();

        var nonEmptyVoxels = new List<int>();
        for (int voxelIndex = 0; voxelIndex < nAllVoxels; voxelIndex++)
        {
            for (int ionIndex = 0; ionIndex < nFeatures; ionIndex++)
            {
                if (localBuffer[ionIndex][voxelIndex] != 0f)
                {
                    nonEmptyVoxels.Add(voxelIndex);
                    break;
                }
            }
        }
        int nVoxels = nonEmptyVoxels.Count;

        var dataBuffer = new float[nFeatures * nVoxels];
        for (int featureIndex = 0; featureIndex < nFeatures; featureIndex++)
        {
            int x = 0;
            foreach (int voxelIndex in nonEmptyVoxels)
            {
                dataBuffer[(featureIndex * nVoxels) + x++] = localBuffer[featureIndex][voxelIndex];
            }
        }

        int nevals = nFeatures;  // Input?


        // Allocated output buffers
        int scoresArrayLength = nVoxels * nComponents;
        int loadsArrayLength = nFeatures * nComponents;
        float[] scores = new float[scoresArrayLength];
        float[] loads = new float[loadsArrayLength];
        float[] evals = new float[nevals];

        // Call the doPCA function
        PcaLib.doPCA(nVoxels, nFeatures, dataBuffer, nIons, nComponents, nevals, scores, loads, evals);

        // Normalization
        for (int i = 0; i < scores.Length; i++)
        {
            scores[i] *= ScoresCoefficient;
        }
        for (int i = 0; i < loads.Length; i++)
        {
            loads[i] *= LoadingsCoefficient;
        }

        var components = new List<ComponentResults>(nComponents);
        for (int i = 0; i < nComponents; i++)
        {
            var compScores = scores.Skip(i * nVoxels).Take(nVoxels).ToArray();
            var compLoads = loads.Skip(i * nFeatures).Take(nFeatures).ToArray();
            components.Add(new ComponentResults(compScores, compLoads));
        }
        return new ComponentsResults(gridData, nonEmptyVoxels.ToArray(), components);
    }
}

public class ErosionFinder
{
    List<int> innerVoxels = new List<int>();
    List<int> edgeVoxels = new List<int>();
    int dimA;
    int dimB;
    int dimC; 
    int nVoxels;

    public ErosionFinder(int[] startingVoxels, IGrid3DData gridData)
    {
        var startingIndices = new List<int>(startingVoxels);
        dimA = gridData.NumVoxels[0];
        dimB = gridData.NumVoxels[1];
        dimC = gridData.NumVoxels[2];

        nVoxels = dimA * dimB * dimC;
        (innerVoxels, edgeVoxels) = FindFirstEdges(startingIndices);
    }
    public List<int> FindInnerVoxels()
    {
        List<int> shell = edgeVoxels;
        List<int> core = innerVoxels;
        (shell, core) = FindShell(innerVoxels, edgeVoxels);
        while ( core.Count > 0 )
        {
            (shell, core) = FindShell(core, shell);
        }

        // now the indices in shell are the innermost core of the first erosion algorithm
        // we can winnow further by removing voxels with fewer neighbors
        shell = WinnowShell(shell);

        return (shell);
    }

    List<int> keysWithLowestValue(Dictionary<int, int> dict)
    {
        var keys = new List<int>();
        if (dict.Count < 2)
        {
            keys.Add(dict.Keys);
        }
        else
        {
            var list = sortByValue(dict);
            var firstVal = list[0].Value;
            int listLen = list.Length;
            int i = 1;
            keys.Add(list[0].Key);
            while ((i< listLen) && (list[i].Value == firstVal))
            {
                keys.Add(list[i].Key);
                ++i;
            }
        }
        return keys;
    }

    int? keyWithHighestValue(Dictionary<int, int> dict)
    {
        if (dict.Count < 2)
        {
            if (dict.Count == 1)
            {
                return dict.First().Key
            }
            else
            {
                return null;
            }
        }
        else
        {
            var list = sortByValue(dict);
            var last = list[list.Count - 1];
            var second = list[list.Count - 2];
            if (last.Value > second.Value)
            {
                return last.Key;
            }
            else
            {
                return null;
            }
        }
    }

    List<KeyValuePair<int, int>> sortByValue(Dictionary<int, int> dict)
    {
        List<KeyValuePair<int, int>> myList = dict.ToList();

        myList.Sort(
            delegate (KeyValuePair<int, int> pair1,
            KeyValuePair<int, int> pair2)
            {
                return pair1.Value.CompareTo(pair2.Value);
            }
        );

        return myList;
    }

    List<int> WinnowShell(List<int> voxelsIn)
    {
        // for each voxel, rank according to how many neighbors it has,
        // secondary rank according to sum of how many neighbors each neighbor has
        // remove lowest tier and retry until a single voxel has highest rank, or until all voxels ae lowest rank
        // if all voxels lowest rank, choose the one closest to origin
        Dictionary<int, int> ranking = new Dictionary<int, int>();
        List<int> neighborOffsets = new List<int>();
        int zOffset = dimA * dimB;
        int yOffset = dimA;
        neighborOffsets.Add(-zOffset);
        neighborOffsets.Add(-yOffset);
        neighborOffsets.Add(-1);
        neighborOffsets.Add(1);
        neighborOffsets.Add(yOffset);
        neighborOffsets.Add(zOffset);
        List<int> edgyOffsets = new List<int>();
        edgyOffsets.Add(-zOffset - yOffset);
        edgyOffsets.Add(-zOffset - 1);
        edgyOffsets.Add(-zOffset + yOffset);
        edgyOffsets.Add(-zOffset + 1);
        edgyOffsets.Add(-yOffset - 1);
        edgyOffsets.Add(-yOffset + 1);
        edgyOffsets.Add(yOffset - 1);
        edgyOffsets.Add(yOffset + 1);
        edgyOffsets.Add(zOffset - yOffset);
        edgyOffsets.Add(zOffset - 1);
        edgyOffsets.Add(zOffset + yOffset);
        edgyOffsets.Add(zOffset + 1);

        foreach (int v in voxelsIn)
        {
            ranking[v] = 0;
        }

        int previousNumberOfVoxels;
        do
        {
            previousNumberOfVoxels = ranking.Count;
            foreach (int v in voxelsIn)
            {
                int vScore = 0;
                foreach (int offset in neighborOffsets)
                {
                    if (voxelsIn.Contains(v + offset))
                    {
                        vScore += 100;
                    }
                }
                foreach (int offset in edgyOffsets)
                {
                    if (voxelsIn.Contains(v + offset))
                    {
                        vScore += 1;
                    }
                }
                ranking[v] = ranking[v] + vScore;
            }

            // see if single highest value voxel exists
            var chosen = keyWithHighestValue(ranking);
            if (chosen.HasValue)
            {
                return chosen.Value;
            }

            // prune keys with lowest Value
            var lowestRankers = keysWithLowestValue(ranking);
            foreach (int v in lowestRankers) { }
            ranking[v] = null;

        } while (ranking.Count < previousNumberOfVoxels);

        // if we get here, winnowing the voxels has not resulted in a single winner
        // the results are either symmetric (a 2x2x2 cube) or disjoint
        // (two single voxels which are not neighbors)
        // in this case, pick the one with the lower index

        return ranking.Keys;
    }

    (List<int>, List<int>) FindShell(List<int> voxelsIn, List<int> edgeIn)
    {
        List<int> core = new List<int>();
        List<int> shell = new List<int>();
    
        foreach (int v in voxelsIn)
        {
            int zOffset = dimA * dimB;
            int yOffset = dimA;
            List<int> neighborOffsets = new List<int>();
            neighborOffsets.Add(-zOffset);
            neighborOffsets.Add(-yOffset);
            neighborOffsets.Add(-1);
            neighborOffsets.Add(1);
            neighborOffsets.Add(yOffset);
            neighborOffsets.Add(zOffset);

            bool foundEdge = false;
            foreach (int offset in neighborOffsets)
            {
                if (edgeIn.Contains(v + offset))
                {
                    foundEdge = true;
                    break;
                }
            }

            if (foundEdge)
            {
                shell.Add(v);
            }
            else
            {
                core.Add(v);
            }

        }
        return (shell, core);
    }

    (List<int>, List<int>) FindFirstEdges(List<int> voxels)
    {
        // firstEdges are all the voxels in voxels adjacent to a voxel not in voxels
        // plus all voxels on the box edges
        // first, make a list of voxelIndices not in voxels
        List<int> notInVoxels = new List<int>(); 
        List<int> innerVoxels = new List<int>();
        List<int> edgeVoxels = new List<int>();
        for (int i = 0; i < nVoxels; ++i)
        {
            if (!voxels.Contains(i))
            {
                notInVoxels.Add(i);
            }
        }

        List<int> cellEdges = CellEdges(dimA, dimB, dimC);
        foreach (int v in voxels)
        {
            int zOffset = dimA * dimB;
            int yOffset = dimA;
            List<int> neighborOffsets = new List<int>();
            neighborOffsets.Add(-zOffset);
            neighborOffsets.Add(-yOffset);
            neighborOffsets.Add(-1);
            neighborOffsets.Add(1);
            neighborOffsets.Add(yOffset);
            neighborOffsets.Add(zOffset);

            if (cellEdges.Contains(v))
            {
                edgeVoxels.Add(v);
            } 
            else
            {
                // check if any of its neighbors are in notInVoxels
                // we know that v is not at the edge, so neighbor index calculation is easy
                bool foundEdge = false;
                foreach (int offset in neighborOffsets) {
                    if (notInVoxels.Contains(v+ offset))
                    {
                        foundEdge = true;
                        break;
                    }
                }

                if (foundEdge) {
                    edgeVoxels.Add(v);
                }
                else
                {
                    innerVoxels.Add(v);
                }
                // int[] neighbors = [v - zOffset, v - yOffset, v - 1, v + 1, v + yOffset, v + zOffset];

            }
        }
        return (innerVoxels, edgeVoxels);
    }

    // return a List of voxel indices corresponding to all the voxels on the edge of the cell
    List<int> CellEdges(int nx, int ny, int nz)
    {
        // add voxel indices for the top face, i.e. at z = 0
        List<int> cellEdges = new List<int>();
        for (int v = 0; v < nx * ny; ++v)
        {
            cellEdges.Add(v);
        }
        // add voxel indices for corners and edges for each plane of z not at the top or bottom

        for (int z = 1; z <(nz- 1); ++z)
        {
            int zOffset = z * (nx * ny);
            // add row of pixels at the y = 0 edge
            for (int x = 0; x < nx; ++x)
            {
                cellEdges.Add(x + zOffset);
            }

            // add the two cells at x = 0 and x = nx-1 for each y
            for (int y = 1; y < (ny - 1); ++y)
            {
                cellEdges.Add((y * nx) + zOffset);
                cellEdges.Add((y * nx) + (nx - 1) + zOffset);
            }

            int yOffset = (ny - 1) * nx;
            // add row of pixels at the y = ny - 1 edge
            for (int x = 0; x < nx; ++x)
            {
                cellEdges.Add(x + yOffset + zOffset);
            }
        }

        // add voxel indices for the bottom face, i.e. at z = nz - 1
        int bottomFaceOffset = (nz - 1) * (nx * ny);
        for (int v = 0; v < nx * ny; ++v)
        {
            cellEdges.Add(v + bottomFaceOffset);
        }

        return cellEdges;
    }

}

public struct ClusterableVoxel
{
    public int voxelIndex; // the voxelIndex from compResults.VoxelIndices;  (between 0 and nTotalVoxels - 1)
    public int arrayIndex; // the index of this voxel in compResults.VoxelIndices;  (between 0 and compResults.VoxelIndices.Count - 1
    public float[] scoreVector;
    public ClusterableVoxel(int vIndex, int aIndex, float[] vec)
    {
        voxelIndex = vIndex;
        arrayIndex = aIndex;
        scoreVector = vec;
    }
}

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

public class PcaScoresGrid
{
    Dictionary<int, PcaVoxel> pcaVoxels;
    int scoreDimensions;


     //   Components = compResults.Components;
     //   VoxelIndices = compResults.VoxelIndices;
     //   Grid3DData = compResults.Grid3DData;

     //   int nIndices = compResults.VoxelIndices.Length;
        


    public PcaScoresGrid(ComponentsResults compResults)
    {
        pcaVoxels = new Dictionary<int, PcaVoxel>();
        scoreDimensions = compResults.Components.Count;
        int nComponents = compResults.Components.Count;

        int dimA = compResults.Grid3DData.NumVoxels[0];
        int dimB = compResults.Grid3DData.NumVoxels[1];
        int dimC = compResults.Grid3DData.NumVoxels[2];

        int nVoxels = dimA * dimB * dimC;

        int nIndices = compResults.VoxelIndices.Length;

        for (int i = 0; i < nIndices; ++i)
        {
            float[] nthVector = new float[scoreDimensions];
            for (int c = 0; c < nComponents; ++c)
            {
                nthVector[c] = compResults.Components[c].Scores[i];
            }
            var pcaVoxel = new PcaVoxel(compResults.VoxelIndices[i], nthVector);
            pcaVoxels[compResults.VoxelIndices[i]] = pcaVoxel;
        }
    }
}

public class ClusterFinder
{

    List<ClusterableVoxel> clusterableVoxels;

    int Dimensions;

    public ClusterFinder(ComponentsResults compResults)
    {
        // Components = compResults.Components;
        // VoxelIndices = compResults.VoxelIndices;
        // Grid3DData = compResults.Grid3DData;

        int nComponents = compResults.Components.Count; 
        int nIndices = compResults.VoxelIndices.Length;
        Dimensions = compResults.Components.Count;
        clusterableVoxels = new List<ClusterableVoxel>();
        for (int i = 0; i < nIndices; ++i)
        {
            float[] nthVector = new float[Dimensions];
            for (int c = 0; c < nComponents; ++c)
            {
                nthVector[c] = compResults.Components[c].Scores[i];
            }
            var clusterable = new ClusterableVoxel(compResults.VoxelIndices[i], i, nthVector);
            clusterableVoxels.Add(clusterable);
        } 
    }
    
    // strategy:  select N clusterables at random 
    // find the one clusterable with the highest value of SUM over 1/(max(dN^2,E^2))
    // where dN is distance to each of the other (N-1) clusterables and E is a distance in vector space deemed "close enough to not matter anymore"
    // E is used to make sure that the cluster algorithm isn't dominated by two voxels at exactly the same vector
    // trick: autocalculate E to be related to the second-nearest distance.
    /* 
    float[] FindClusterVector(int numSamples)
    {
        List<ClusterableVoxel> samples;
        if (samples > clusterableVoxels.Count)
        {
            samples = clusterableVoxels;
        } else {
            samples = new List<ClusterableVoxel>();
            int stride = clusterableVoxels.Count / samples;
            int strideStart = randomStride / 2;
            for (int i = 0; i < samples;  ++i )
            {
                int nextIndex = strideStart + (stride * i);
                var nthSample = clusterableVoxels[nextIndex];
            }
        }

        // now rank each item
        int numSamples = samples.Count;
        var scores = new List<float>();
        float nearestDistanceSquared = 1E10;
        float secondNearestDistanceSquared = 2E10;
        for (int n = 0; n < numSamples - 1; ++n)
        {
            scores.Add(0);
            for (int p = n + 1; p < numSamples; ++p)
            {
                float dist = distanceSquaredBetween(samples[n], samples[p]);
                if (dist < nearestDistanceSquared)
                {
                    secondNearestDistanceSquared = nearestDistanceSquared;
                    nearestDistanceSquared = dist;
                }
            }
        }
        scores.Add(0);
        // scores now has N items in it
        float distESqu = secondNearestDistanceSquared * 0.75;
        for (int n = 0; n < numSamples; ++n
        {
            for (int p = n + 1; p < numSamples; ++p)
            {
                float distSqu = max(distESqu, distanceSquaredBetween(samples[n], samples[p]));

                float score = 1.0 / distSqu;
                scores[n] = scores[n] + score;
                scores[p] = scores[p] + score;
            }
        }

    }
    */



}