using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Cameca.Extensions.Controls;
using CommunityToolkit.HighPerformance;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Interaction logic for ProjectionGridsView.xaml
/// </summary>
public partial class ProjectionGridsView : UserControl
{

    int whichGrid = 0;
    public ProjectionGridsView()
    {
        InitializeComponent();
        var histogram = ProjectionGrid2dHistogram;
    }

    public static readonly DependencyProperty GridsSourceProperty = DependencyProperty.Register(
        nameof(GridsSource),
        typeof(ICollection<IRenderData>),
        typeof(ProjectionGridsView),
        new FrameworkPropertyMetadata(Array.Empty<IRenderData>(), GridsSourcePropertyChanged));

    private static void GridsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ProjectionGridsView projectionGridsView) { return; }
        ICollection<IRenderData> renderData = projectionGridsView.GridsSource;
        int rdc = renderData.Count;
        Debug.WriteLine("GridsSourcePropertyChanged called -- render data count is " + rdc);
    }

    private void GridsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshGridData();
    }

    private void RefreshGridData()
    { 
        ICollection<IRenderData> renderDataCollection = this.GridsSource;
        int rdc = renderDataCollection.Count;
        if (rdc > 0)
        {
            List<IRenderData> renderList = renderDataCollection.ToList();
            if (whichGrid >= rdc)
            {
                whichGrid = 0;
            }

            var renderData = renderList[whichGrid];
            List<IRenderData> singleList = new List<IRenderData> { renderData };
            Histogram2D histogram = ProjectionGrid2dHistogram;
            histogram.DataSource = singleList;
        }
    }

    public void FillRenderDataWithGridData(IHistogram2DRenderData renderData, TwoDPeakProjection projection)
    {
       // renderData.ColorMap = Resources.ColorMap.GetPresetColorMap(ColorMapPreset.GreyScale);
        DensityPlane dp = projection.densityPlane;
        (TwoDGridCoord minCoord, TwoDGridCoord maxCoord) = dp.MinMaxGridCoords();
        int spanX = 1 + maxCoord.x - minCoord.x;
        int spanY = 1 + maxCoord.y - minCoord.y;
        int span = Math.Max(spanX, spanY);
        int numCoords = span * span;
        float[] dat = new float[numCoords * 4];
        for (int q = 0; q < spanY; q += 1)
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
        renderData.Update(rom, binsize, origin);
    }

    public ICollection<IRenderData> GridsSource
    {
        get { return (ICollection<IRenderData>)GetValue(GridsSourceProperty); }
        set { SetValue(GridsSourceProperty, value); }
    }

    private void AdvanceGridButton_Click(object sender, RoutedEventArgs e)
    {
        whichGrid += 1;
        RefreshGridData();
    }

    private static IEnumerable<T> GetChildren<T>(DependencyObject root) where T: DependencyObject
    {
        int childrenCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T)
            {
                yield return (T)child;
            }
            foreach (var recursiveChild in GetChildren<T>(child))
            {
                yield return recursiveChild;
            }
        }
    }
}
