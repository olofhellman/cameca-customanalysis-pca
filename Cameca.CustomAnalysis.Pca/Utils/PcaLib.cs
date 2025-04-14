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

    [DllImport("Cameca.CustomAnalysis.PcaLib.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EstimateRankF(
        float[] evals,
        int nevals,
        int nObs,
        int nGaps = 1,
        int P = 1,
        bool refine = false);

    [DllImport("Cameca.CustomAnalysis.PcaLib.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int NoiseEvals(
        int gapRank,
        int nevals,
        int nObs,
        float[] y);
}
