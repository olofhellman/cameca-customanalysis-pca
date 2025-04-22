using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using CommunityToolkit.HighPerformance.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using static Cameca.CustomAnalysis.Interface.IonFormula;
using CommunityToolkit.HighPerformance;
using System.Windows.Markup;
using System.Runtime.Intrinsics.Arm;

namespace Cameca.CustomAnalysis.Pca;

[DefaultView(PcaViewModel.UniqueId, typeof(PcaViewModel))]
internal partial class PrincipalComponentAnalysis : BasicCustomAnalysisBase<PcaProperties>
{
    private readonly INodeDataProvider nodeDataProvider;

    public const string UniqueId = "Cameca.CustomAnalysis.Pca.PcaNode";

    public static INodeDisplayInfo DisplayInfo { get; } = new NodeDisplayInfo("Principal Component Analysis");

    [ObservableProperty]
    public ICollection<IRenderData> noiseEigenValues = Array.Empty<IRenderData>();

    [ObservableProperty]
    private ICollection<IRenderData> componentRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    private ICollection<IRenderData> selectedGridRenderData = Array.Empty<IRenderData>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    private EigenvalueResults? eigenvalueResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateComponentsCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateComponentsCommand))]
    private ComponentsResults? componentsResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateComponentsCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateComponentsCommand))]
    private TwoDPeakProjections? twoDPeakProjections;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedComponentCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedComponentCommand))]
    private SeriesCollection loadingsSeries = new();


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedComponentCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedComponentCommand))]
    public ICollection<string> loadingsLabels = Array.Empty<string>();


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateSelectedComponentCanExecute))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedComponentCommand))]
    public ICollection<IRenderData> scoresHistogramData = Array.Empty<IRenderData>();

    public bool UpdateComponentsCanExecute => ComponentsResults is null;

    public bool UpdateSelectedComponentCanExecute =>
        !LoadingsSeries.Any() || !LoadingsLabels.Any() || !ScoresHistogramData.Any();

    public Func<double, string> AxisYLabelFormatter { get; } = (double value) => value.ToString("F3");

    public PrincipalComponentAnalysis(
        IStandardAnalysisFilterNodeBaseServices services,
        ResourceFactory resourceFactory,
        INodeDataProvider nodeDataProvider)
        : base(services, resourceFactory)
    {
        this.nodeDataProvider = nodeDataProvider;
    }

    protected override void OnDataIsValidChanged(bool isValid)
    {
        // If the underlying data was changed (e.g. ranged changed or analysis is in an ROI that was altered), then all data needs to be recomputed
        if (!isValid)
        {
            InvalidateAll();
        }
    }

    // Update from full raw data: Sets Eigenvalues only - currently noise eigenvalues must be manually analyzed to set the appropriate number of components after full data change.
    // There is not benefit of prematurely calculating all the other data until the number of components is manually set after looking at the scree plot
    protected override async Task<bool> Update(CancellationToken cancellationToken)
    {
        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
        {
            return false;
        }

        var gridNode = Resources.GetGrid();
        if (gridNode is null || await gridNode.GetDataAsync<IGrid3DData>(cancellationToken: cancellationToken) is not { } gridData)
        {
            return false;
        }

        EigenvalueResults = PcaCalculator.GetEignevalues(ionData, gridData);
        return true;
    }

    // After setting the number of components, the components data can be computed. We can follow up with the current
    // selected component data as well using the currently selected component index
    [RelayCommand(CanExecute = nameof(UpdateComponentsCanExecute))]
    public async Task UpdateComponents(CancellationToken cancellationToken)
    {
        DataStateIsError = false;
        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
        {
            DataStateIsError = true;
            return;
        }

        var gridNode = Resources.GetGrid();
        if (gridNode is null || await gridNode.GetDataAsync<IGrid3DData>(cancellationToken: cancellationToken) is not { } gridData)
        {
            DataStateIsError = true;
            return;
        }

        var compResults = PcaCalculator.GetComponents(ionData, gridData, Properties.Components);

        var phaseIDResults = PcaCalculator.GetPhases(ionData, compResults);

        compResults.PhaseIDResults = phaseIDResults;

        ComponentsResults = compResults;

        UpdateOptionsBounds();

        // Ensure that the selected component falls in the valid range of number of components
        if (Properties.ComponentIndex < 0)
        {
            Properties.ComponentIndex = 0;
        }
        else if (Properties.ComponentIndex > Properties.Components)
        {
            Properties.ComponentIndex = Properties.Components;
        }

        await UpdateSelectedComponent(cancellationToken);
    }

    // Uses the component data (or computes for all componets if necessary) to generate plots for the selected component by index
    [RelayCommand(CanExecute = nameof(UpdateSelectedComponentCanExecute))]
    public async Task UpdateSelectedComponent(CancellationToken cancellationToken)
    {
        LoadingsLabels = Array.Empty<string>();
        ScoresHistogramData = Array.Empty<IRenderData>();

        if (await Resources.GetIonData(cancellationToken: cancellationToken) is not { } ionData)
        {
            DataStateIsError = true;
            return;
        }

        int numComponents = Properties.Components;
        int selectedIndex = Properties.ComponentIndex;
        
        if (ComponentsResults is null){
            await UpdateComponents(cancellationToken);
        }
        if (ComponentsResults?.Components.ElementAtOrDefault(selectedIndex) is not { Scores: { } scores, Loads: { } loadingData })
        {
            return;
        }

        // Loading
        int features = loadingData.Length;
        var ions = ionData.Ions;
        if (ions.Count() != features)
        {
            throw new InvalidOperationException("Count of ion ranges unexpectedly does not match the number of PCA features");
        }

        var series = new ColumnSeries
        {
            Name = "",
            Title = "",
            DataLabels = true,
            LabelPoint = x => x.Y.ToString("F3"),
            Values = new ChartValues<float>(loadingData),
        };

        var ionBrushes = ions.Select(ionInfo => new SolidColorBrush(Resources.IonDisplayInfo.GetColor(ionInfo))).ToArray();
        var mapper = new CartesianMapper<float>()
            .X((_, i) => i)
            .Y(value => value)
            .Fill((_, i) => ionBrushes[i]);
        LoadingsSeries = new SeriesCollection(mapper)
        {
            series
        };
        LoadingsLabels = ions.Select(x => x.Name).ToList();

        // Scores Histogram
        int voxels = scores.Length;
        float binSize = 0.01f;
        float min = scores.Min();
        float max = scores.Max();
        int binCount = (int)Math.Ceiling((max - min) / binSize);
        var binnedScores = new int[binCount];
        for (int i = 0; i < scores.Length; i++)
        {
            int index = (int)((scores[i] - min) / binSize);
            binnedScores[index]++;
        }
        var scoreData = binnedScores.Select((y, i) => new Vector2(min + (i * binSize), y)).ToArray();
        var scoresHistogram = Resources.ChartObjects.CreateHistogram(
            scoreData,
            color: Colors.Blue);
        ScoresHistogramData = new IRenderData[] { scoresHistogram };
    }

    // Updates the noise eigenvalues tab plot when the computed eigenvalue data changes
    partial void OnEigenvalueResultsChanged(EigenvalueResults? value)
    {
        NoiseEigenValues = Array.Empty<IRenderData>();
        if (EigenvalueResults is not { Evals: { } evals })
        {
            return;
        }
        var positions = evals.Select((value, index) => new Vector3(index, 0f, value)).ToArray();
        var series = Resources.ChartObjects.CreateSeries();
        series.Positions = positions;
        series.Color = Colors.Blue;
        series.MarkerShape = MarkerShape.Circle;
        series.MarkerColor = Colors.Blue;
        NoiseEigenValues = new IRenderData[] { series };
    }

    // Updates the components 3D plots when the component data (derived from selected number of components) changes
    float[] GetPhaseIdScoresForVoxelIndices(PhaseIdResults phaseIdResults, int compIndex, int[] voxelIndices)
    {
        int numIndices = voxelIndices.Length;
        float[] scores = new float[numIndices];
        for (int i = 0; i < numIndices; ++i)
        {
            int voxelIndex = voxelIndices[i];
            scores[i] = phaseIdResults.PhaseForVoxelIntValue(voxelIndex) == compIndex ? 1.0f : 0.0f ;
        }
        return scores;
    }

    // Updates the components 3D plots when the component data (derived from selected number of components) changes
    partial void OnComponentsResultsChanged(ComponentsResults? value)
    {
        ComponentRenderData = Array.Empty<IRenderData>();
        SelectedGridRenderData = Array.Empty<IRenderData>();

        if (ComponentsResults is not { Grid3DData: { } gridData,
            Components: { } components,
            PhaseIDResults: { } phaseIdResults,
            VoxelIndices: { } voxelIndices }
         || Resources.GetValidIonData() is not { } ionData)
        {
            return;
        }

        int numComponents = 6; // was hardwired to numComponents  Properties.Components;
        // int selectedIndex = Properties.ComponentIndex;

        var newComponentsData = new IRenderData[numComponents];
        for (int compIndex = 0; compIndex < numComponents; compIndex++)
        {
            var phaseIdScores = GetPhaseIdScoresForVoxelIndices(phaseIdResults, compIndex, voxelIndices);

            // data fed into GetScoredPositions is an array of voxelIndices for which a dot should be generated,
            // and an array of scores -- scores[n] is the score for the voxel at voxelIndex[n]
            var positionsWithValues = PositionScores.GetScoredPositions(gridData, voxelIndices, phaseIdScores);

            var valuePoints = Resources.ChartObjects.CreateValuePoints();
            valuePoints.Name = $"Component {compIndex}";
            valuePoints.PositionsWithValues = positionsWithValues;
            valuePoints.ColorMap = Resources.ColorMap.GetPresetColorMap(ColorMapPreset.Plasma);

            newComponentsData[compIndex] = valuePoints;
        }
        ComponentRenderData = newComponentsData;


        var newSelectedGridData = new IRenderData[1];
        var projectionsDictionary = phaseIdResults.TwoDPeakProjections();
        var gridRepresentation = Resources.ChartObjects.CreateHistogram2D();
        gridRepresentation.ColorMap = Resources.ColorMap.GetPresetColorMap(ColorMapPreset.GreyScale);
        gridRepresentation.Height = 2.0;  // double
        gridRepresentation.Width = 2.0;  // double
        var gridIds = projectionsDictionary.Keys.ToList();
        if (gridIds.Count > 0)
        {
            string gridId = gridIds[0];
            TwoDPeakProjection projection = projectionsDictionary[gridId];
            DensityPlane dp = projection.densityPlane;
            (TwoDGridCoord minCoord, TwoDGridCoord maxCoord) = dp.MinMaxGridCoords();
            int spanX = 1 + maxCoord.x - minCoord.x;
            int spanY = 1 + maxCoord.y - minCoord.y;
            int span = Math.Max(spanX, spanY);
            int numCoords = span * span;
            float[] dat = new float[numCoords * 4];
            for (int q = 0; q < spanY;  q += 1)
            {
                int qOffset = q * span;
                int gridq = q + minCoord.y;
                for (int p = 0; p < spanX; p += 1)
                {
                    int arrayIndex = qOffset + p;
                    int gridp = p + minCoord.x; 
                    dat[arrayIndex] = dp.valueAtGridCoords(gridp, gridq);
                }

            }
            ReadOnlyMemory2D<float> rom = new ReadOnlyMemory2D<float>(dat, span, span);
            Vector2 binsize = new Vector2(0.5f, 0.5f); // Vector2(dp.binsize, dp.binsize);
            Vector2 origin = new Vector2(minCoord.x * dp.binsize, minCoord.y * dp.binsize);
            gridRepresentation.Update(rom, binsize, origin);
        }
        newSelectedGridData[0] = gridRepresentation;
        SelectedGridRenderData = newSelectedGridData;
    }

    // Updates readonly Min/Max properties so the bounds are displayed in the Properties panel 
    private void UpdateOptionsBounds()
    {
        var selectedResult = ComponentsResults?.Components.ElementAtOrDefault(Properties.ComponentIndex);
        Properties.Min = selectedResult?.Scores.Min();
        Properties.Max = selectedResult?.Scores.Max();
    }

    // Applies filter to the custom analysis: returns the ions in voxels for which the score of the selected component exceeds the specified threshold value
    // Olof Note -- this is where to change logic for viewing results -- redirect the algorithm here to look at the new 'identified phase' structure
  
    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(IIonData ionData, IProgress<double>? progress, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        DataStateIsError = false;
        if (ComponentsResults is null)
        {
            await UpdateComponents(cancellationToken);
        }

        // Extract necessary data out of ComponentsResults using some pattern matching for null checks and variable assignment
        if (ComponentsResults is not { Grid3DData: { } gridData, VoxelIndices: { } nonEmptyVoxels }
            || ComponentsResults.Components[Properties.ComponentIndex] is not { Scores: { } scores })
        {
            DataStateIsError = true;
            yield break;
        }
        var phaseIds = ComponentsResults.PhaseIDResults;
        int componentOfInterest = Properties.ComponentIndex;
        var minVector = gridData.GetMinVector();
        var voxelSize = gridData.GetVoxelSizeDimensions();
        int xBinStride = gridData.NumVoxels[0];
        int yBinStride = gridData.NumVoxels[1];
        var binner = new PositionToVoxels(minVector, voxelSize, xBinStride, yBinStride);

        // Create a map of voxel index to the associated score
        var scoredVoxels = nonEmptyVoxels
            .Zip(scores)
            .ToDictionary(x => x.First, x => x.Second);

        // Build buffers of filtered indices to return
        // Iterating through each point (to determine inclusion) is a bit of a complex chunked iterator code to support >Int32.MaxValue number of ions in a data set
        ulong index = 0ul;
        float threshold = Properties.Isovalue;
        foreach (var chunk in ionData.CreateSectionDataEnumerable(IonDataSectionName.Position))
        {
            int bufferIndex = 0;
            using var buffer = MemoryOwner<ulong>.Allocate(chunk.Length);
            var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
            for (int chunkIndex = 0; chunkIndex < chunk.Length; chunkIndex++)
            {
                int bin = binner.ToVoxel(positions.Span[chunkIndex]);

                // Properties.ComponentIndex is the selectedComponent
                if (phaseIds.PhaseForVoxelIntValue(bin) == componentOfInterest) 
                // if (scoredVoxels.TryGetValue(bin, out float score) && score >= threshold)
                {
                    buffer.Span[bufferIndex++] = index;
                }
                index += 1ul;
            }
            yield return buffer.Slice(0, bufferIndex).Memory;
        }

        DataStateIsValid = true;
    }

    /*
    protected override async IAsyncEnumerable<ReadOnlyMemory<ulong>> GetIndicesDelegateAsync(IIonData ionData, IProgress<double>? progress, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        DataStateIsError = false;
        if (ComponentsResults is null)
        {
            await UpdateComponents(cancellationToken);
        }

        // Extract necessary data out of ComponentsResults using some pattern matching for null checks and variable assignment
        if (ComponentsResults is not { Grid3DData: { } gridData, VoxelIndices: { } nonEmptyVoxels }
            || ComponentsResults.Components[Properties.ComponentIndex] is not { Scores: { } scores })
        {
            DataStateIsError = true;
            yield break;
        }

        var minVector = gridData.GetMinVector();
        var voxelSize = gridData.GetVoxelSizeDimensions();
        int xBinStride = gridData.NumVoxels[0];
        int yBinStride = gridData.NumVoxels[1];
        var binner = new PositionToVoxels(minVector, voxelSize, xBinStride, yBinStride);

        // Create a map of voxel index to the associated score
        var scoredVoxels = nonEmptyVoxels
            .Zip(scores)
            .ToDictionary(x => x.First, x => x.Second);

        // Build buffers of filtered indices to return
        // Iterating through each point (to determine inclusion) is a bit of a complex chunked iterator code to support >Int32.MaxValue number of ions in a data set
        ulong index = 0ul;
        float threshold = Properties.Isovalue;
        foreach (var chunk in ionData.CreateSectionDataEnumerable(IonDataSectionName.Position))
        {
            int bufferIndex = 0;
            using var buffer = MemoryOwner<ulong>.Allocate(chunk.Length);
            var positions = chunk.ReadSectionData<Vector3>(IonDataSectionName.Position);
            for (int chunkIndex = 0; chunkIndex < chunk.Length; chunkIndex++)
            {
                var bin = binner.ToVoxel(positions.Span[chunkIndex]);
                if (scoredVoxels.TryGetValue(bin, out float score) && score >= threshold)
                {
                    buffer.Span[bufferIndex++] = index;
                }
                index += 1ul;
            }
            yield return buffer.Slice(0, bufferIndex).Memory;
        }

        DataStateIsValid = true;
    }
    */

    // On Properties panel changes, some data must be invalidated to be recomputed with new values. Invalidations depend on the properties changed
    protected override void OnPropertiesChanged(PropertyChangedEventArgs e)
    {
        // Base call invalidates the returned filter - this correctly needs to be invalidated for any of the current editable properties
        base.OnPropertiesChanged(e);
        CanSave = true;
        switch (e.PropertyName)
        {
            case nameof(PcaProperties.Components):
                InvalidateComponents();
                break;
            case nameof(PcaProperties.ComponentIndex):
                UpdateOptionsBounds();
                InvalidateSelectedComponent();
                break;
            case nameof(PcaProperties.Invert):
                FilterIsInverted = Properties.Invert;
                InvalidateSelectedComponent();
                break;
            default:
                break;
        }
    }

    /* Data invalidation methods */
    private void InvalidateSelectedComponent()
    {
        LoadingsSeries = new SeriesCollection();
        LoadingsLabels = Array.Empty<string>();
        ScoresHistogramData = Array.Empty<IRenderData>();
    }

    private void InvalidateAll()
    {
        EigenvalueResults = null;
        InvalidateComponents();
    }

    private void InvalidateComponents()
    {
        ComponentsResults = null;
        InvalidateSelectedComponent();
    }

    // Should actually be implemented in the base class CoreNodeBase along with existing DataStateIsValid.
    // Remove after a Cameca.CustomAnalysis.Utilities updates adds this functionality
    protected bool DataStateIsError
    {
        get => DataState?.IsErrorState ?? false;
        set
        {
            if (DataState is not null)
            {
                DataState.IsErrorState = value;
            }
        }
    }
}
