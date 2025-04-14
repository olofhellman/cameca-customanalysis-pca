using Cameca.CustomAnalysis.Interface;
using System;
using System.Numerics;

namespace Cameca.CustomAnalysis.Pca;

internal static class PositionScores
{
    public static Vector4[] GetScoredPositions(IGrid3DData gridData, int[] voxelIndices, float[] scores, float jitterStdDev = 0f)
    {


        int xVoxels = gridData.NumVoxels[0];
        double xSize = gridData.VoxelSize[0];
        double xStart = gridData.GridRange[0, 0] + (xSize / 2d);

        int yVoxels = gridData.NumVoxels[1];
        double ySize = gridData.VoxelSize[1];
        double yStart = gridData.GridRange[1, 0] + (ySize / 2d);

        int zVoxels = gridData.NumVoxels[2];
        double zSize = gridData.VoxelSize[2];
        double zStart = gridData.GridRange[2, 0] + (zSize / 2d);

        int indexer = 0;
        var positionsWithValues = new Vector4[voxelIndices.Length];

        int seed = 0;
        var r = new Random(seed);
        for (int z = 0; z < zVoxels; z++)
        {
            for (int y = 0; y < yVoxels; y++)
            {
                for (int x = 0; x < xVoxels; x++)
                {
                    int voxelIndex = (z * yVoxels * xVoxels) + (y * xVoxels) + x;
                    if (voxelIndices[indexer] == voxelIndex)
                    {
                        positionsWithValues[indexer] = new Vector4(
                            (float)(xStart + (xSize * x)) + r.NextSingleNormal(stdDev: jitterStdDev),
                            (float)(yStart + (ySize * y)) + r.NextSingleNormal(stdDev: jitterStdDev),
                            (float)(zStart + (zSize * z)) + r.NextSingleNormal(stdDev: jitterStdDev),
                            scores[indexer]);
                        indexer++;
                        if (indexer >= voxelIndices.Length)
                        {
                            break;
                        }
                    }
                }
                if (indexer >= voxelIndices.Length)
                {
                    break;
                }
            }
            if (indexer >= voxelIndices.Length)
            {
                break;
            }
        }

        return positionsWithValues;
    }

}

internal static class RandomExtensions
{
    public static float NextSingleNormal(this Random random, float mean = 0, float stdDev = 1)
    {
        float u1 = 1f - random.NextSingle();
        float u2 = 1f - random.NextSingle();
        float randStdNormal = MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Sin(2f * MathF.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
