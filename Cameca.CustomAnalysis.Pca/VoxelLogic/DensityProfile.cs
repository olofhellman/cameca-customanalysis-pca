using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;


// DensityProfile represents a histogram-ish population along one axis
// Bins are regularly spaced from the minimum to the maximum
// Bins have an integer id according to their distance from 0
// So, if the axis coordinate is 2, and the bins are 0.5 apart, the bin is ID=4
// The data is collected in a Dictionary<int, float>
// if a bin is unpopulated, there is no Dictionary entry for the corresponding ID
// smoothing is applied at population time: when a point is added,  it is automatically split 
// between bins to preserve a constant smoothing for all added points
// There is always a bin at zero
// bins at negative values have negative indices
// "out of bounds" limits at -1023 and 1023
//  points outside of the bounds are counted but not binned
// Points are added to the profile using a splat transfer function, in a way that the 
// delocalization for every point added to the profile is constant.  That is,
// if a point is added at the center of a bin, it contributes .75 to that bin and .125 to each neighbor bin
// If a point is added exactly at the border between two bins, it contributes 0.5 to each one

public class DensityProfile
{
    float maxval;
    float halfBinsize;
    float binsize;
    float oneOverBinsize;
    int oobPoints;
    Dictionary<int, float> data;
    public DensityProfile(float binsize)
    {
        this.maxval = binsize * 1023.0f;
        this.halfBinsize = binsize * 0.5f;
        this.binsize = binsize;
        this.oneOverBinsize = 1.0f / binsize;
        this.data = new Dictionary<int, float>();
        this.oobPoints = 0;
    }

    public void ConsoleDump(string prefix)
    {
        Debug.WriteLine("DensityProfile Dump " + prefix);
        var keys = data.Keys.ToList();
        keys.Sort();
        foreach (var key in keys)
        {
            float binx = key * binsize;
            Debug.WriteLine("x = " + binx + ", population = " + data[key]);
        }
    }
    internal float populationAtBin(int bin)
    {
        float binValue = 0.0f;
        if (data.ContainsKey(bin))
        {
            binValue = data[bin];
        }
        return binValue;
    }

    public void AddPointAt(float x)
    {
        if (Math.Abs(x) > maxval)
        {
            oobPoints += 1;
            return;
        }

        float Neg1Func(float x)
        {
            return (.5f * MathF.Pow(x - 0.5f, 2));
        }

        float ZeroFunc(float x)
        {
            return (.75f - MathF.Pow(x, 2));
        }

        float Pos1Func(float x)
        {
            return (.5f * MathF.Pow(x + 0.5f, 2));
        }

        int binIndex = (int)MathF.Floor((x + halfBinsize) * oneOverBinsize);
        float xRem = (x - (binIndex * binsize)) * oneOverBinsize;

        float xm1 = (float)Neg1Func(xRem);
        float xp1 = (float)Pos1Func(xRem);    // these are the spline transfer functions
        float x0 = (float)ZeroFunc(xRem);
        data[binIndex] = populationAtBin(binIndex) + x0;
        data[binIndex - 1] = populationAtBin(binIndex - 1) + xm1;
        data[binIndex + 1] = populationAtBin(binIndex + 1) + xp1;
    }

}








