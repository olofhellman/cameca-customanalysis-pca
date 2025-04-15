using System.Runtime.InteropServices;

namespace Cameca.CustomAnalysis.Pca;

public static class PcaLib
{
    [DllImport("Cameca.CustomAnalysis.PcaLib.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void doPCA(
        int nVoxels,
        int nFeatures,
        float[] data,
        int nIons,
        int nComponents,
        int nevals,
        float[] scores,
        float[] loads,
        float[] evals);

    [DllImport("Cameca.CustomAnalysis.PcaLib.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void doEigen(
        int nVoxels,
        int nFeatures,
        float[] data,
        int nevals,
        float[] evals);
}
