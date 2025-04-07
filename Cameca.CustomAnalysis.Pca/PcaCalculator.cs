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

    public float[] GetScores(int voxelIndex)
    {
        float[] scores = new float[nComponents];
        for (int i = 0; i < nComponents; i++)
        {
            scores[i] = compResults.Components[i].Scores[voxelIndex];
        }
        return scores;
    }

    public PcaScoresGrid ScoresGrid()
    {

        int nAllVoxels = compResults.Grid3DData.NumVoxels[0] * compResults.Grid3DData.NumVoxels[1] * compResults.Grid3DData.NumVoxels[2];
        int nComponents = compResults.Components.Count;
        List<int> voxelIndices = new List<int>(compResults.VoxelIndices);

        return new PcaScoresGrid(this, voxelIndices, nComponents, compResults.Grid3DData.NumVoxels);
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
        // make a PcaGrid, then call grid.GetPhases
        PcaScoresGridProducer producer = new PcaScoresGridProducer(compResults);

        PcaScoresGrid scoresGrid = producer.ScoresGrid();
        return scoresGrid.GetPhasesStrategyB();
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
    /*
    public static PhaseIdResults GetPhasesStrategyB(IIonData ionData, ComponentsResults compResults)
    {
 
        int nAllVoxels = compResults.Grid3DData.NumVoxels[0] * compResults.Grid3DData.NumVoxels[1] * compResults.Grid3DData.NumVoxels[2];
 
        PhaseIdResults phaseIdResults = new PhaseIdResults(compResults.VoxelIndices, nAllVoxels);
        var gridProducer = new PcaScoresGridProducer(compResults);
        PcaScoresGrid scoresGrid = gridProducer.ScoresGrid();
        List<int> voxelIndices = new List<int>(compResults.VoxelIndices);
        int[] gridDimensions = compResults.Grid3DData.NumVoxels;
        ErosionFinder erosionFinder = new ErosionFinder(voxelIndices, gridDimensions);
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
    */
    //static float[] scoreForVoxel()
   //{
    //    return [0.0];
   //}

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
 
