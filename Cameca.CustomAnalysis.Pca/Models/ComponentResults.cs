namespace Cameca.CustomAnalysis.Pca;

public sealed class ComponentResults
{
    public float[] Scores { get; }
    public float[] Loads { get; }

    public ComponentResults(float[] scores, float[] loads)
    {
        Scores = scores;
        Loads = loads;
    }
}
