using Cameca.CustomAnalysis.Interface;
using System.Numerics;

namespace Cameca.CustomAnalysis.Pca;

internal static class PositionScores
{
    public static Vector4[] GetScoredPositions(IGrid3DData gridData, int[] voxelIndices, float[] scores)
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
                            (float)(xStart + (xSize * x)),
                            (float)(yStart + (ySize * y)),
                            (float)(zStart + (zSize * z)),
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
