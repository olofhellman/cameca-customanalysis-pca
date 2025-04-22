
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Windows.Controls;
using System.Text.RegularExpressions;


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
    public string DebugStr()
    {
        int x;
        int y;
        (x, y) = this.xyCoords();
        return pixelId.ToString() + ":{" + x + "," + y + "}";
    }
}


public struct PeakID
{
    PixelID peakMax;
    public PeakID(PixelID pixelId)
    {
        peakMax = pixelId;
    }

    public string DebugStr()
    {
        return peakMax.DebugStr();
    }
}

// DensityPlane represents a 2D density plot, and the bins exist on a 2D grid
// Bins are regularly spaced from the minimum to the maximum
// a binsize maps a floating point coordinate to a grid coordinate.
// if the binsize is 1, then the grid coordinate at p-1, q=1 represents the floating point coordinate 1.0, 1.0
// if the binsize is 0.5, then the grid coordinate at p-1, q=1 represents the floating point coordinate 0.5, 0.5
// Bins have an integer id derived from their p and q coordinates :  1024q + p 
// So, for a binsize of 1.0, the xy coordinate 3.0,4.0 would correspond to 
// the bin at p = 3, q = 4 and the ID would be 4099
// if the binsize is 0.5, a point at x=3, y=4 would be the gridpoint p=6, q=8, and the ID would be 8198
//
// The data is collected in a Dictionary<int, float>
// if a bin is unpopulated, there is no Dictionary entry for the corresponding ID
// smoothing is applied at population time: when a point is added,  it is automatically split 
// between bins to preserve a constant smoothing for all added points
// There is always a bin at zero
// bins at negative values have negative indices
// "out of bounds" limits at -510 and 510
//  points outside of the bounds are counted but not binned
// 
// Points are added to the Plane using a splat transfer function, in a way that the 
// delocalization for every point added to the profile is almost constant.  That is,
// if a point is added at a corner of the grid, equidistant from four different grid points, 
// it contributes .25 to each of the four bins The 1 dimensional transfer function is used in both 
// dimensions, so that if a point is added exactly at a grid point, it only contributes 9/16 to that point
// and 7/16 to the surrounding points following this matrix of contributions:
//
//   1/64   3/32   1/64
//   3/32   9/16   3/32 
//   1/64   3/32   1/64
public class DensityPlane
{
    float halfBinsize;
   public float binsize;
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

    internal void AddIfPossible(int x, int y, List<PixelID> list)
    {
        PixelID possibleBin = new PixelID(x, y);
        if (data.ContainsKey(possibleBin))
        {
            list.Add(possibleBin);
        }
    }

    // check for the 8 neighboring pixels and add them if they have a non-zero value
    public List<PixelID> PixelIdsNeighboring(PixelID pixelId)
    {
        //int x;
        //int y;
        (int x, int y) = pixelId.xyCoords();
        List<PixelID> neighbors = new List<PixelID>();
        AddIfPossible(x - 1, y - 1, neighbors);
        AddIfPossible(x - 1, y, neighbors);
        AddIfPossible(x - 1, y + 1, neighbors);
        AddIfPossible(x, y-1, neighbors);
        AddIfPossible(x, y+1, neighbors);
        AddIfPossible(x + 1, y-1, neighbors);
        AddIfPossible(x + 1, y, neighbors);
        AddIfPossible(x + 1, y + 1, neighbors);

		return neighbors;
	}
	
    public float MaximumValue(List<PixelID> candidates)
    {
        float maxScore = float.MinValue;

        foreach (PixelID candidate in candidates)
        {
            float nthScore = valueAtPixel(candidate);
            if (nthScore > maxScore)
            {
                maxScore = nthScore;
            }
        }
        return maxScore;
    }
    
    public PixelID? FindMaximum(List<PixelID> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }
        PixelID bestCandidate = candidates[0];
        float currMax = valueAtPixel(bestCandidate);
        foreach (PixelID candidate in candidates)
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
    
    public (TwoDGridCoord, TwoDGridCoord) MinMaxGridCoords()
    {
        int miny = 0;
        int minx = 0;
        int maxy = 0;
        int maxx = 0;
        bool first = true;
      
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
        return (new TwoDGridCoord(minx, miny), new TwoDGridCoord(maxx, maxy));
    }
    
    public void WriteToStream(StreamWriter stream)
    {
        TwoDGridCoord min;
        TwoDGridCoord max;
        (min, max) = MinMaxGridCoords();

        int xSize = 1 + max.x - min.x;
		int ySize = 1 + max.y - min.y;
		stream.WriteLine("x size= " + xSize);
		stream.WriteLine("y size= " + ySize);

		stream.Write("{ " );
		// now csv data for the grid from min to max
		for (int y = min.y; y <= max.y; ++y)
		{
			string l = "{";
			for (int x = min.x; x <= max.x; ++x)
			{
				PixelID xthKey = new PixelID(x, y);
				float xthValue = 0;
				if (data.ContainsKey(xthKey))
				{
					xthValue = data[xthKey];
				}
				l = l + xthValue;
				if (x != max.x) {
					l = l + ",";
				}  
			}
			l = l + "}";
			stream.Write(l);
			if (y != max.y)
			{
				stream.Write(",");
			}
		}
		stream.Write("}");
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
            Debug.WriteLine("x = " + binx + ",y = " + biny + ", population = " + data[pixelId]);
        }
    }
    
    public void writeToFile(string outputFilename)
    {   
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        using (StreamWriter outputFile = new StreamWriter(outputFilename))
        {
            this.WriteToStream(outputFile);
        }
    }

    public float valueAtGridCoords(int x, int y)
    {
        PixelID pixelId = this.BinFor(x, y);
        return this.valueAtPixel(pixelId);
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

    public PixelID BinFor(int x, int y)
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



