using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;
 
 
 

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

    // This strategy looks first at finding density peaks in PCA space
    // Then, draws N dimensional spheres around each peak and assigns voxels in those spheres to be the given component
    // On a first pass, the data is binned one dimensionally in each of the PCA dimensions
    // This allows A) ranges to be set and B) predictions to be made about what the expected population of 
    // an N dimensional bin at any N dimensional coordinate if the values where uncorrelated
    // then a 2D binning is made with the first two PCA dimensions, and deviations from the calculated populations are 
    // estimated

    public PhaseIdResults GetPhasesStrategyC()
    {
        List<int> voxelIndices = pcaVoxels.Keys.ToList();

        PhaseIdResults phaseIdResults = new PhaseIdResults(voxelIndices, nVoxels);

        // oneDDensities contains one list for each PCA dimension
        // each list is a smoothed histogram of populations 
        // first float is coordinate point, second float is the population
        // on first pass, the coordinate points are 0.5 apart
        // So, if there are points between 0 and 10, there may be 23 entries, from -0.5 to 10.5
        // at spacings of 0.5
        // float binSeparation = 0.5f;
        // List<DensityProfile> oneDDensities = CalculateOneDDensities(voxelIndices, binSeparation);
        // for (int i = 0; i < this.scoreDims; ++i)
        // {
        //     oneDDensities[i].ConsoleDump("density " + i);
        // }

        float binSeparation = 0.5f;
        int gridDims = Math.Min(2, this.scoreDims); // 2 is the simplest choice 
        Dictionary<int, DensityPlane> twoDGrids = new Dictionary<int, DensityPlane>();
        Dictionary<int, List<List<PixelID>>> partitions = new Dictionary<int, List<List<PixelID>>>();
        // now, make twoD grids using all pairs of dimensions
        for (int i = 0; i < (gridDims - 1); ++i)
        {
            for (int j = i + 1; j < gridDims; ++j)
            {
                int gridId = 10 * i + j;
                var twoDGrid = CalculateTwoDDensity(voxelIndices, i, j, binSeparation);
                twoDGrid.ConsoleDump("dgrid" + i + j);
                twoDGrids[gridId] = twoDGrid;
                List<List<PixelID>> partitionedIndices = IdentifyPartitions(twoDGrid, i, j, voxelIndices);
                partitions[gridId] = partitionedIndices;
            }
        }




        // now turn the partitions array into the data needed by phaseIDResults
        // phaseIDResults has a method  IdentifyVoxelAs(int voxelIds, int phase)
        // so, for each peak identified by the 2d grid partition, 
        // make a list of voxels corresponding to that peak.
        // a voxel corresponds to the peak if its PCA scores for the given two dimensions 
        // is in a pixel of a peak.
        // 
        // to avoid looking up whether o not the pixel from each voxel is in one of the peaks
        // first, group all the voxelIDs into leist for each pixelID
        // then do a single lookup for each pixelID

        if (gridDims > 1)
        {
            int firstGridId = 1;
            DensityPlane grid = twoDGrids[firstGridId];
            List<List<PixelID>> firstGridPartitions = partitions[firstGridId];

            // for debugging, dump the partitions:
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

        // using a twoDGrid, return lists of voxel indices that are in clusters or not
        // to identify clusters, first, look for maxima in the twoD grid
        // for each maximum, find connected pixels reachable in a gradient descent,
        // stopping at 5% of the first peak
        // pixels reachable from multiple peaks should not be classified as either
        // 5% of the first peak is also the threshhold for finding maxima






        // var twoDGridAB = calculateTwoDDensity(voxelIndices, 0, 1, binSeparation);
        // twoDGridAB.ConsoleDump("dgrid01");
        // var twoDGridAC = calculateTwoDDensity(voxelIndices, 0, 2, binSeparation);
        // twoDGridAC.ConsoleDump("dgrid02");
        // var twoDGridBC = calculateTwoDDensity(voxelIndices, 1, 2, binSeparation);
        // twoDGridBC.ConsoleDump("dgrid12");




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




