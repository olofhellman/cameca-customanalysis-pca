using Cameca.CustomAnalysis.Interface;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Cameca.CustomAnalysis.Pca;

public partial class PcaGlobalOptions : ObservableObject
{
    [ObservableProperty]
    [field:Display(Name = "Components Render Noise", Description = "Applies a small random noise the points render position in the Components plots. This only affects the render, making the plots easier to see from certain perspectives so all the points don't line up exactly. Value is the desired standard deviation for a gaussian distribution to generate the random values used to offset the positions. A good value to apply this noise is about 0.4")]
    private float jitterStdDev = 0f;

    [ObservableProperty]
    [field: Display(Name = "Default Color Map ")]
    private ColorMapPreset colorMapPreset = ColorMapPreset.Plasma;
}