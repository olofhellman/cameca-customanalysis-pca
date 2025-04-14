using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;
 
 
// PcaScoresGrid represents a three dimensional grid containing the PCA scores for a collection of voxels
// PcaScoresGrid is initialized with the size of the grid in x y and z, so that it can then map 
// a particular [x,y,z] grid coordinate to an index (and back) 
//
// Of particular interest to the PCA data processing logic, this class implements the logic for 
// calculating PhaseIdResults -- using the PCA score data to identify which voxels belong to which 
// 'phases'

public interface IScoresProvider
{
    public float[] GetScores(int voxelIndex);
}
public class PcaScoresGrid
{
    Dictionary<int, PcaVoxel> pcaVoxels; // the key is the 'id' for the voxel, from which you can
                                         // calculate the x,y,z position of the voxel on the grid if
                                         // the grid dimensions are known

    int scoreDims;
    int xDim;
    int yDim;
    int zDim;
    int nVoxels;

    int VoxelIndexFor(GridCoord gridCoord)
    {
        return gridCoord.x + (gridCoord.y * xDim) + (gridCoord.z * xDim * yDim);
    }

    GridCoord GridCoordFor(int voxelIndex)
    {
        int zOffset = xDim * yDim;
        int yOffset = xDim;
        int zCoord = voxelIndex / zOffset;
        int rem = voxelIndex % zOffset;
        int yCoord = rem / yOffset;
        rem = rem % yOffset;
        int xCoord = rem;
        return new GridCoord(xCoord, yCoord, zCoord);
    }

    // returns a list of 27 indices, unless the voxel is near the edge
    List<int> NeighborIndicesFor(GridCoord gridCoord)
    {
        List<int> neighborIndices = new List<int>();
        //List<int> xIndices = new List<int>();
        //List<int> yIndices = new List<int>();
        //List<int> zIndices = new List<int>();
        int minx = Math.Max(0, gridCoord.x - 1);
        int maxx = Math.Min(xDim - 1, gridCoord.x + 1);
        int miny = Math.Max(0, gridCoord.y - 1);
        int maxy = Math.Min(yDim - 1, gridCoord.y + 1);
        int minz = Math.Max(0, gridCoord.z - 1);
        int maxz = Math.Min(zDim - 1, gridCoord.z + 1);
        for (int z = minz; z <= maxz; ++z)
        {
            for (int y = miny; y <= maxy; ++y)
            {
                for (int x = minx; x <= maxx; ++x)
                {
                    neighborIndices.Add(x + (y * xDim) + (z * xDim * yDim));
                }
            }
        }
        return neighborIndices;
    }
    public PcaScoresGrid(IScoresProvider scoresProvider, List<int> voxelIndices, int scoreDimensions, int[] gridDimensions)
    {
        scoreDims = scoreDimensions;

        pcaVoxels = pcaVoxelsInit(scoresProvider, voxelIndices, gridDimensions);

        xDim = gridDimensions[0];
        yDim = gridDimensions[1];
        zDim = gridDimensions[2];

        nVoxels = xDim * yDim * zDim;
    }

    static Dictionary<int, PcaVoxel> pcaVoxelsInit(IScoresProvider scoresProvider, List<int> voxelIndices, int[] gridDimensions)
    {
        var voxels = new Dictionary<int, PcaVoxel>();
        int nIndices = voxelIndices.Count;

        for (int i = 0; i < nIndices; ++i)
        {
            float[] nthVector = scoresProvider.GetScores(i);

            var pcaVoxel = new PcaVoxel(voxelIndices[i], nthVector);
            voxels[voxelIndices[i]] = pcaVoxel;
        }

        return voxels;
    }

    // GetPhasesStrategyC is produces PhaseIdResults based on the first two 
    // PCA dimensions only (later strategies will incorporate more dimensions)
    //
    // The strategy goes like this:
    //
    // A) make a 2D grid representing the density of voxels with PCA scores in 
    //    the first two PCA dimensions at x,y
    // B) identify peaks in that 2D grid -- each peak corresponds to a 'phase'
    // C) assign voxels with PCA scores in each peak to the corresponding phase
    public PhaseIdResults GetPhasesStrategyC()
    {
        List<int> voxelIndices = pcaVoxels.Keys.ToList();

        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIndices, nVoxels);

        float binSeparation = 0.5f;
        int gridDims = Math.Min(2, this.scoreDims); // 2 is the simplest choice 
        Dictionary<int, DensityPlane> twoDGrids = new Dictionary<int, DensityPlane>();
        Dictionary<int, List<List<PixelID>>> partitions = new Dictionary<int, List<List<PixelID>>>();
        
        
        // now, make twoD grids using all pairs of dimensions
        // yes, GetPhasesStrategyC only uses 2 dimensions, so these loops are useless, 
        // but they will be used in successive strategies when there are more dimensions used
        for (int i = 0; i < (gridDims - 1); ++i)
        {
            for (int j = i + 1; j < gridDims; ++j)
            {
                int gridId = 10 * i + j;
                
                // this makes the 2D grid  --  step A) above
                var twoDGrid = CalculateTwoDDensity(voxelIndices, i, j, binSeparation);
                twoDGrids[gridId] = twoDGrid;
                
                // this identifies the peaks --  step B) above
                List<List<PixelID>> partitionedIndices = IdentifyPartitions(twoDGrid, i, j, voxelIndices);
                partitions[gridId] = partitionedIndices;
            }
        }

        // now turn the partitions array into the data needed by phaseIDResults -- step C) above
        // 
        // phaseIDResults has a method  IdentifyVoxelAs(int voxelIds, int phase)
        //
        // a voxel corresponds to the peak if its PCA scores for the given two dimensions 
        // is in a pixel of a peak. So, for each voxel, we want to call IdentifyVoxelAs()
        // with the value for 'phase' corresponding to the peak it is in, if any
        //
        // to avoid looking up whether or not a pixel is in one of the peaks multiple times,
        // first, group all the voxelIDs into a list for each pixelID
        // then do a single lookup for each pixelID

        if (gridDims > 1)
        {
            // GetPhasesStrategyC only looks at one grid -- the first one
            int firstGridId = 1;
            DensityPlane grid = twoDGrids[firstGridId];
            List<List<PixelID>> firstGridPartitions = partitions[firstGridId];

            // for debugging, dump the partitions:
            /* 
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string outputFilename = System.IO.Path.Combine(docPath, "Partitions.txt");

            using (StreamWriter outputFile = new StreamWriter(outputFilename))
            {
                outputFile.WriteLine("partitions file");
                outputFile.WriteLine("grid Info:");

                TwoDGridCoord gridMinCoord;
                TwoDGridCoord gridMaxCoord;
                (gridMinCoord, gridMaxCoord) = grid.MinMaxGridCoords();
                outputFile.WriteLine("minx: " + gridMinCoord.x);
                outputFile.WriteLine("miny: " + gridMinCoord.y);
                outputFile.WriteLine("maxx: " + gridMaxCoord.x);
                outputFile.WriteLine("maxy: " + gridMaxCoord.y);
                int peakIndex = 0;
                outputFile.WriteLine("");
                foreach (List<PixelID> pixelIdList in firstGridPartitions)
                {
                    outputFile.WriteLine("partition: " + peakIndex);
                    outputFile.WriteLine("pixelCoords: " + peakIndex);
                    outputFile.Write("{");
                    foreach (PixelID pixelId in pixelIdList)
                    {
                        int x;
                        int y;
                        (x, y) = pixelId.xyCoords();
                        outputFile.Write("{" + x + "," + y + "}");
                    }
                    outputFile.Write("}\n");
                }
            }
            */
            // end of debug dump
            
            // For each voxel, assign the voxel to the List corresponding with its pixel
            Dictionary<PixelID, List<int>> voxelLists = new Dictionary<PixelID, List<int>>();
            foreach (int voxelId in voxelIndices)
            {
                PcaVoxel voxel = pcaVoxels[voxelId];
                PixelID nthPixelId = grid.PixelIDFor(voxel.scoreVector[0], voxel.scoreVector[1]);
                List<int>? voxelIDListForGridCoord;
                if (voxelLists.TryGetValue(nthPixelId, out voxelIDListForGridCoord))
                {
                    voxelIDListForGridCoord.Add(voxelId);
                }
                else
                {
                    voxelIDListForGridCoord = new List<int>();
                    voxelIDListForGridCoord.Add(voxelId);
                    voxelLists[nthPixelId] = voxelIDListForGridCoord;
                }
            }

            // now, each voxelIndex is a member of one of the lists in voxelLists
            // go through each list in voxelLists and see if its pixel is designated in a particular partiion
            // that is, for each pixel, see which peak it belongs to, if any
             
            int listN = 1;
            foreach (List<PixelID> pixelIdList in firstGridPartitions)
            {
                // the pixelIdList contains a list of pixelIds identified as being part of the Nth partition
                foreach (PixelID pixelID in pixelIdList)
                {
                    // Lookup for all the voxels bucketed under this pixelId
                    List<int> voxelIds = voxelLists[pixelID];
                    foreach (int voxelId in voxelIds)
                    {
                        phaseIdResults.IdentifyVoxelAs(voxelId, listN);
                    }
                    // remove that entry from voxelLists
                    voxelLists.Remove(pixelID);
                }

                ++listN;
            }

            // now, all the remaining entries in voxelLists are unassigned :  
            // assign these to component 0
            List<PixelID> unassignedPixels = voxelLists.Keys.ToList();
            foreach (PixelID pixelID in unassignedPixels)
            {
                // Lookup for all the voxels bucketed under this pixelId
                List<int> voxelIds = voxelLists[pixelID];
                foreach (int voxelId in voxelIds)
                {
                    phaseIdResults.IdentifyVoxelAs(voxelId, 0);
                }
            }
        }

        return phaseIdResults;
    }

    // identifyPartitions use the density map from the twoDGrid to separate 
    // voxels that belong to different peaks in the DensityPlane
    // first, identify the peaks and their associated pixels
    // then, for each voxel, see if it lands in on of the partitioned pixels.
    // If it does, add it to the appropriate list
    // return the list of lists
    // indices not identified are not returned in any list
    public List<List<PixelID>> IdentifyPartitions(DensityPlane twoDGrid, int dimX, int dimy, List<int> voxelIndices)
    {
        // to identify the first maximum, just find the pixel with the highest value
        // then, accumulate neighboring pixels, avoiding neighbors with higher values
        // accumulate neighbors in order of their density value.
        // stop accumulating when a slope increase is found:
        //   this indicates there must be another maximum to look for
        // also, stop at 10% of peak max
        // to look for another maximum, find the remaining pixel with the maximum value.
        // continue accumulating pixels into all peaks

        // partitionFinder operates on the grid, identifying pixels
        // associated with the different maxima
        TwoDGridPartitionFinder partitionFinder = new TwoDGridPartitionFinder(twoDGrid);

        partitionFinder.FindPartitions(0.1f);
        var pixelLists = partitionFinder.GetPixelLists();
        partitionFinder.Clear();
        return pixelLists;
    }

    public DensityPlane CalculateTwoDDensity(List<int> voxelIndices, int dimx, int dimy, float binsize)
    {
        DensityPlane densityPlane = new DensityPlane(binsize);
        for (int v = 0; v < voxelIndices.Count; ++v)
        {
            int voxelIndex = voxelIndices[v];
            var voxel = pcaVoxels[voxelIndex];
            var xVal = voxel.scoreVector[dimx];
            var yVal = voxel.scoreVector[dimy];
            densityPlane.AddPointAt(xVal, yVal);
        }
        return densityPlane;
    }

    // CalculateOneDDensities is not used, but here's what it does:
	// it creates a profile along each of the principal PCA dimensions 
	// of the density of voxels along that dimension 
	// That is, peaks in the density profile represent PCA score values with a high population of 
	// voxels. each Profile is a 'smoothed' histogram of populations 
	// The AP Suite already has code that produces a similar histogram, displayed
	// in one of the panes of the PCA Extension.
	// 
	// The number of points in the profile is determined by the binsize
	// If there are points between 0 and 10, there may be 23 entries, from -0.5 to 10.5
	// at spacings of 0.5
	//
	// It can be used like this:
	//
	// float binSeparation = 0.5f;
	// List<DensityProfile> oneDDensities = CalculateOneDDensities(voxelIndices, binSeparation);

    public List<DensityProfile> CalculateOneDDensities(List<int> voxelIndices, float binsize)
    {
        List<DensityProfile> densities = new List<DensityProfile>();
        for (int i = 0; i < this.scoreDims; ++i)
        {
            var nthDensityList = new DensityProfile(binsize);
            densities.Add(nthDensityList);
        }

        for (int v = 0; v < voxelIndices.Count; ++v)
        {
            int voxelIndex = voxelIndices[v];
            var voxel = pcaVoxels[voxelIndex];
            for (int i = 0; i < this.scoreDims; ++i)
            {
                densities[i].AddPointAt(voxel.scoreVector[i]);
            }
        }
        return densities;
    }
}




