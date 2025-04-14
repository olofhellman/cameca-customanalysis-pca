namespace Cameca.CustomAnalysis.Pca;

// public rather than internal because a ComponentsResults object is used to 
// call the PhaseIdResults constructor
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
