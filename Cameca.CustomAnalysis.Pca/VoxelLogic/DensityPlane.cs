
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;

public struct PixelID : IComparable<PixelID>
{
    public int pixelId;

    public PixelID(int pixelId)
    {
        this.pixelId = pixelId;
    }

    public PixelID(int x, int y)
    {
        pixelId = y * 1024 + x;
    }
    public (int, int) xyCoords()
    {
        int y = (512 + pixelId) >> 10;
        int x = pixelId - (y * 1024);
        return (x, y);
    }

    public int CompareTo(PixelID other)
    {
        return other.pixelId > pixelId ? -1 : other.pixelId < pixelId ? 1 : 0;
    }
}


public struct PeakID
{
    PixelID peakMax;
    public PeakID(PixelID pixelId)
    {
        peakMax = pixelId;
    }
}
// DensityPlane represents a 2D density plot
// Bins are regularly spaced from the minimum to the maximum
// Bins have an integer id according to their distances from 0
// the ID of a point at x,y is 1024y + x
// So, the bin corresponding to x = 3, y = 4 if the bins are 0.5 apart, the ID=4051
// The data is collected in a Dictionary<int, float>
// if a bin is unpopulated, there is no Dictionary entry for the corresponding ID
// smoothing is applied at population time: when a point is added,  it is automatically split 
// between bins to preserve a constant smoothing for all added points
// There is always a bin at zero
// bins at negative values have negative indices
// "out of bounds" limits at -510 and 510
//  points outside of the bounds are counted but not binned
public class DensityPlane
{
    float halfBinsize;
    float binsize;
    float oneOverBinsize;
    int oobPoints;
    float maxval;
    Dictionary<PixelID, float> data;
    public DensityPlane(float binSep)
    {
        binsize = binSep;
        halfBinsize = binSep * 0.5f; 
        oobPoints = 0;
        oneOverBinsize = 1.0f / binSep;
        data = new Dictionary<PixelID, float>();
        maxval = 1022.0f * binSep;
    }
    public List<PixelID> GridPointIds()
    {
        return data.Keys.ToList();
    }
    public List<PixelID> PixelIdsNeighboring(PixelID pixelId)
    {
        int x;
        int y;
        (x, y) = pixelId.xyCoords();
        List<PixelID> neighbors = new List<PixelID>();
        PixelID neighborBin = new PixelID(x - 1, y);
        if (data.ContainsKey(neighborBin))
        {
            neighbors.Add(neighborBin);
        }
            neighborBin = new PixelID(x + 1, y);
            if (data.ContainsKey(neighborBin))
            {
                neighbors.Add(neighborBin);
            }
            neighborBin = new PixelID(x, y - 1);
            if (data.ContainsKey(neighborBin))
            {
                neighbors.Add(neighborBin);
            }
            neighborBin = new PixelID(x, y + 1);
            if (data.ContainsKey(neighborBin))
            {
                neighbors.Add(neighborBin);
            }
            return neighbors;
        }
    public PixelID? FindMaximum(List<PixelID> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }
        PixelID bestCandidate = candidates[0];
        float currMax = valueAtPixel(bestCandidate);
        foreach(PixelID candidate in candidates)
        {
            float nthPopulation = valueAtPixel(candidate);
            if (nthPopulation > currMax)
            {
                currMax = nthPopulation;
                bestCandidate = candidate;
            }
        }
        return bestCandidate;
    }
    public void ConsoleDump(string prefix)
    {
        int miny = 0; 
        int minx = 0;
        int maxy = 0;
        int maxx = 0;
        bool first = true;
        Debug.WriteLine("DensityProfile Dump " + prefix);
        var pixelIds = data.Keys.ToList();
        pixelIds.Sort();
        foreach (PixelID pixelId in pixelIds)
        {
            int x;
            int y;
            (x, y) = pixelId.xyCoords();
            if (!first)
            {
                miny = Math.Min(y, miny);
                maxy = Math.Max(y, maxy); 
                minx = Math.Min(x, minx);
                maxx = Math.Max(x, maxx);
            }
            else
            {
                miny = y;
                maxy = y;
                minx = x;
                maxx = x;
                first = false;
            }
            float binx = x * binsize;
            float biny = y * binsize;
            // Debug.WriteLine("x = " + binx + ",y = " + biny + ", population = " + data[key]);
        }

        Debug.WriteLine("csv data:");
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string outputFilename = System.IO.Path.Combine(docPath, prefix + ".csv");
        using (StreamWriter outputFile = new StreamWriter(outputFilename))
        {
            int xSize = 1 + maxx - minx;
            int ySize = 1 + maxy - miny;
            outputFile.WriteLine("x size= " + xSize);
            outputFile.WriteLine("y size= " + ySize);

            outputFile.Write("{ " );
            // now csv data for the grid from min to max
            for (int y = miny; y <= maxy; ++y)
            {
                string l = "{";
                for (int x = minx; x <= maxx; ++x)
                {
                    PixelID xthKey = new PixelID(x, y);
                    float xthValue = 0;
                    if (data.ContainsKey(xthKey))
                    {
                        xthValue = data[xthKey];
                    }
                    l = l + xthValue;
                    if (x != maxx) {
                        l = l + ",";
                    }  
                }
                l = l + "}";
                outputFile.Write(l);
                if (y != maxy)
                {
                    outputFile.Write(",");
                }
            }
            outputFile.Write("}");
        }

    }
    public float valueAtPixel(PixelID pixelId)
    {
        float binValue;
        if (data.TryGetValue(pixelId, out binValue))
        {
            return binValue;
        }
        return 0.0f;
    }
    PixelID BinFor(int x, int y)
    {
        return new PixelID(y * 1024 + x);
    }

    public (int, int) PixelIndicesFor(float x, float y)
    {
        int xBinIndex = (int)MathF.Floor((x + halfBinsize) * oneOverBinsize);
        int yBinIndex = (int)MathF.Floor((y + halfBinsize) * oneOverBinsize);
        return (xBinIndex, yBinIndex);
    }
    public PixelID PixelIDFor(float x, float y)
    {
        int binx;
        int biny;
        (binx, biny) = PixelIndicesFor(x, y);
        return new PixelID(binx, biny);
    }

    public void AddPointAt(float x, float y)
    {
        if ((Math.Abs(x) > maxval) || (Math.Abs(y) > maxval)) {
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

        int xBinIndex;
        int yBinIndex;
        (xBinIndex, yBinIndex) = PixelIndicesFor(x, y);

        float xRem = (x - (xBinIndex * binsize)) * oneOverBinsize;
        float xm1 = (float)Neg1Func(xRem);
        float xp1 = (float)Pos1Func(xRem);    // these are the spline transfer functions
        float x0 = (float)ZeroFunc(xRem); 
       
        float yRem = (y - (yBinIndex * binsize)) * oneOverBinsize;
        float ym1 = (float)Neg1Func(yRem);
        float yp1 = (float)Pos1Func(yRem);    // these are the spline transfer functions
        float y0 = (float)ZeroFunc(yRem);

        PixelID bin = new PixelID(xBinIndex - 1, yBinIndex - 1);
        data[bin] = valueAtPixel(bin) + xm1 * ym1;
        bin = new PixelID(xBinIndex, yBinIndex - 1);
        data[bin] = valueAtPixel(bin) + x0 * ym1;
        bin = new PixelID(xBinIndex + 1, yBinIndex - 1);
        data[bin] = valueAtPixel(bin) + xp1 * ym1;

        bin = new PixelID(xBinIndex - 1, yBinIndex);
        data[bin] = valueAtPixel(bin) + xm1 * y0;
        bin = new PixelID(xBinIndex, yBinIndex);
        data[bin] = valueAtPixel(bin) + x0 * y0;
        bin = new PixelID(xBinIndex + 1, yBinIndex);
        data[bin] = valueAtPixel(bin) + xp1 * y0;

        bin = new PixelID(xBinIndex - 1, yBinIndex + 1);
        data[bin] = valueAtPixel(bin) + xm1 * yp1;
        bin = new PixelID(xBinIndex, yBinIndex + 1);
        data[bin] = valueAtPixel(bin) + x0 * yp1;
        bin = new PixelID(xBinIndex + 1, yBinIndex + 1);
        data[bin] = valueAtPixel(bin) + xp1 * yp1;

    }

}



