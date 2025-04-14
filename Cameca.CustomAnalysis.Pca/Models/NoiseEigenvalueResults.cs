namespace Cameca.CustomAnalysis.Pca;

internal sealed class NoiseEigenvalueResults
{
    public int Rank { get; }
    public float[] NoiseEvals { get; }
    public NoiseEigenvalueResults(int rank, float[] noiseEvals)
    {
        Rank = rank;
        NoiseEvals = noiseEvals;
    }
}