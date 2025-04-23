using Cameca.CustomAnalysis.Interface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cameca.CustomAnalysis.Pca;

public delegate float[] GetScoresDelegate(int voxelIndex);

public class PcaScoresGridProducer: IScoresProvider {

    ComponentsResults compResults;
    int nComponents;

    public PcaScoresGridProducer(ComponentsResults results)
    {
        compResults = results;
        nComponents = compResults.Components.Count;
    }

    public (VoxelID, float[]) GetIDAndScores(int voxelIndex)
    {
        float[] scores = new float[nComponents];
        for (int i = 0; i < nComponents; i++)
        {
            scores[i] = compResults.Components[i].Scores[voxelIndex];
        }
        VoxelID voxelId = new VoxelID(compResults.VoxelIndices[voxelIndex]);
        return (voxelId, scores);
    }

    public PcaScoresGrid ScoresGrid()
    {
        int nComponents = compResults.Components.Count;

        int x = compResults.Grid3DData.NumVoxels[0];
        int y = compResults.Grid3DData.NumVoxels[1];
        int z = compResults.Grid3DData.NumVoxels[2];

        ThreeDGridDimensions gridDimensions = new ThreeDGridDimensions(x, y, z);
        return new PcaScoresGrid(this, compResults.VoxelIndices.Count(), nComponents, gridDimensions);
    }
}

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
        // make a PcaGrid, then call grid.GetPhases
        PcaScoresGridProducer producer = new PcaScoresGridProducer(compResults);

        PcaScoresGrid scoresGrid = producer.ScoresGrid();
        return scoresGrid.GetPhasesStrategyC();
    }

    public static NoiseEigenvalueResults GetNoiseEigenvalues(float[] evals, int gaps, int significance, bool refine)
    {
        int nevals = evals.Length;
        int rank = PcaLib.EstimateRankF(evals, nevals, nevals, gaps, significance, refine);

        float[] noiseEvals = new float[nevals - rank];
        PcaLib.NoiseEvals(rank, nevals, nevals, noiseEvals);
        return new NoiseEigenvalueResults(rank, noiseEvals);
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
        float[] scores = new float[nVoxels * nComponents];
        float[] loads = new float[nFeatures * nComponents];
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
