using Prism.Mvvm;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace Cameca.CustomAnalysis.Pca;

[XmlRoot("PcaOptions")]
public class PcaProperties : BindableBase
{
    private int components = 0;
    public int Components
    {
        get => components;
        set => SetProperty(ref components, value);
    }

    private int componentIndex = 0;
    [Display(Name = "Component Index")]
    public int ComponentIndex
    {
        get => componentIndex;
        set => SetProperty(ref componentIndex, value);
    }

    private float isovalue = 1f;
    public float Isovalue
    {
        get => isovalue;
        set => SetProperty(ref isovalue, value);
    }

    private float? min = null;
    [ReadOnly(true)]
    public float? Min
    {
        get => min;
        set => SetProperty(ref min, value);
    }

    private float? max = null;
    [ReadOnly(true)]
    public float? Max
    {
        get => max;
        set => SetProperty(ref max, value);
    }

    private bool invert = false;
    public bool Invert
    {
        get => invert;
        set => SetProperty(ref invert, value);
    }
}
