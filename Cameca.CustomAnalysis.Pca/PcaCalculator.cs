using Cameca.CustomAnalysis.Interface;
using System;
using System.Collections.Generic;
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
        PcaScoresGrid scoresGrid = new PcaScoresGrid(compResults);
    }

    float[] scoreForVoxel()
    {
         
    }
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
        int clusterSamples = 25
        var clusterVector = clusterFinder.FindClusterVector(clusterSamples);
        while (clusterVector != NULL)
        {
            clusterVector = clusterFinder.FindClusterVector(clusterSamples);
        }


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

internal class ErosionFinder
{
    List<int> indices = new List<int>();
    List<int> edgeVoxels = new List<int>();
    int dimA;
    int dimB;
    int dimC; 
    int nVoxels;

    ErosionFinder(List<int> startingVoxels, IGrid3DData gridData)
    {
        indices = new List<int>(startingVoxels);
        dimA = gridData.NumVoxels[0];
        dimB = gridData.NumVoxels[1];
        dimC = gridData.NumVoxels[2];

        nVoxels = dimA * dimB * dimC;
        indices.Add(dimA);
        edgeVoxels = findFirstEdges(indices);
        edgeVoxels.ForEach(indices.Remove);
    }

    List<int> findFirstEdges(List<int> voxels)  
    {
        // firstEdges are all the voxels in voxels adjacent to a voxel not in voxels
        // plus all voxels on the box edges
        // first, make a list of voxelIndices not in voxels
        List<int> notInVoxels = new List<int>();
        List<int> firstEdge = new List<int>();
        for (int i = 0; i < nVoxels; ++i)
        {
            if (!indices.Contains(i))
            {
                notInVoxels.Add(i);
            }
        }

    }

}

internal struct ClusterableVoxel
{
    public int voxelIndex; // the voxelIndex from compResults.VoxelIndices;  (between 0 and nTotalVoxels - 1)
    public int arrayIndex; // the index of this voxel in compResults.VoxelIndices;  (between 0 and compResults.VoxelIndices.Count - 1
    public float[] scoreVector;
    ClusterableVoxel(int vIndex, int aIndex, float[] vec)
    {
        voxelIndex = vIndex;
        arrayIndex = aIndex;
        scoreVector = vec;
    }
}

internal struct PcaVoxel
{
    public int voxelIndex; // the voxelIndex from compResults.VoxelIndices;  (between 0 and nTotalVoxels - 1)
    ClusterableVoxel(int vIndex, float[] vec)
    {
        voxelIndex = vIndex;
        scoreVector = vec;
    }
}

internal class PcaScoresGrid
{
    Dictionary<int, PcaVoxel> pcaVoxels;

    PcaScoresGrid(ComponentsResults compResults)
    { 
        pcaVoxels = new Dictionary<int, PcaVoxel>();
        for (int i = 0; i<nIndices; ++i)
        {
            float[] nthVector = new float[Dimensions];
            for (int c = 0; c<nComponents; ++c)
            {
                nthVector[c] = compResults.Components[c].Scores[i];
            }
            var pcaVoxel = new PcaVoxel(compResults.VoxelIndices[i], nthVector);
            pcaVoxels.[VoxelIndices[i]] = pcaVoxel;
        } 
    }

internal class ClusterFinder
{

    List<ClusterableVoxel> clusterableVoxels;

    int Dimensions;

    ClusterFinder(ComponentsResults compResults)
    {
        Components = compResults.Components;
        VoxelIndices = compResults.VoxelIndices;
        Grid3DData = compResults.Grid3DData;

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
    float[] FindClusterVector(int numSamples)
    {
        List<ClusterableVoxel> samples;
        if (samples > clusterableVoxels.Count)
        {
            samples = clusterableVoxels
        } else {
            samples = new List<ClusterableVoxel>()
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
        for (int n = 0; n < numSamples - 1; ++n
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



}