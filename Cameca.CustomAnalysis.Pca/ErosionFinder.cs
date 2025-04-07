using System.Collections.Generic;
using System.Linq;

public class ErosionFinder
{
    List<int> innerVoxels = new List<int>();
    List<int> edgeVoxels = new List<int>();
    int dimA;
    int dimB;
    int dimC; 
    int nVoxels;


    public ErosionFinder(List<int> startingVoxels, int[] dimensions)
    {
        dimA = dimensions[0];
        dimB = dimensions[1];
        dimC = dimensions[2];

        nVoxels = dimA * dimB * dimC;
        (innerVoxels, edgeVoxels) = FindFirstEdges(startingVoxels);
    }

    public void SetVoxelIndices(List<int> startingVoxels)
    {
        (innerVoxels, edgeVoxels) = FindFirstEdges(startingVoxels);
    }

    public List<int> FindInnerVoxels()
    {
        List<int> shell = edgeVoxels;
        List<int> core = innerVoxels;
        (shell, core) = FindShell(innerVoxels, edgeVoxels);
        while ( core.Count > 0 )
        {
            (shell, core) = FindShell(core, shell);
        }

        // now the indices in shell are the innermost core of the first erosion algorithm
        // we can winnow further by removing voxels with fewer neighbors
        shell = WinnowShell(shell);

        return (shell);
    }

    List<int> keysWithLowestValue(Dictionary<int, int> dict)
    {
        var keys = new List<int>();
        if (dict.Count < 2)
        {
            foreach (var key in dict.Keys) {       
                keys.Add(key);
            }    
        }
        else
        {
            var list = sortByValue(dict);
            var firstVal = list[0].Value;
            int listLen = list.Count;
            int i = 1;
            keys.Add(list[0].Key);
            while ((i< listLen) && (list[i].Value == firstVal))
            {
                keys.Add(list[i].Key);
                ++i;
            }
        }
        return keys;
    }

    int? keyWithHighestValue(Dictionary<int, int> dict)
    {
        if (dict.Count < 2)
        {
            if (dict.Count == 1)
            {
                return dict.First().Key;
            }
            else
            {
                return null;
            }
        }
        else
        {
            var list = sortByValue(dict);
            var last = list[list.Count - 1];
            var second = list[list.Count - 2];
            if (last.Value > second.Value)
            {
                return last.Key;
            }
            else
            {
                return null;
            }
        }
    }

    List<KeyValuePair<int, int>> sortByValue(Dictionary<int, int> dict)
    {
        List<KeyValuePair<int, int>> myList = dict.ToList();

        myList.Sort(
            delegate (KeyValuePair<int, int> pair1,
            KeyValuePair<int, int> pair2)
            {
                return pair1.Value.CompareTo(pair2.Value);
            }
        );

        return myList;
    }

    List<int> WinnowShell(List<int> voxelsIn)
    {
        // for each voxel, rank according to how many neighbors it has,
        // secondary rank according to sum of how many neighbors each neighbor has
        // remove lowest tier and retry until a single voxel has highest rank, or until all voxels ae lowest rank
        // if all voxels lowest rank, choose the one closest to origin
        Dictionary<int, int> ranking = new Dictionary<int, int>();
        List<int> neighborOffsets = new List<int>();
        int zOffset = dimA * dimB;
        int yOffset = dimA;
        neighborOffsets.Add(-zOffset);
        neighborOffsets.Add(-yOffset);
        neighborOffsets.Add(-1);
        neighborOffsets.Add(1);
        neighborOffsets.Add(yOffset);
        neighborOffsets.Add(zOffset);
        List<int> edgyOffsets = new List<int>();
        edgyOffsets.Add(-zOffset - yOffset);
        edgyOffsets.Add(-zOffset - 1);
        edgyOffsets.Add(-zOffset + yOffset);
        edgyOffsets.Add(-zOffset + 1);
        edgyOffsets.Add(-yOffset - 1);
        edgyOffsets.Add(-yOffset + 1);
        edgyOffsets.Add(yOffset - 1);
        edgyOffsets.Add(yOffset + 1);
        edgyOffsets.Add(zOffset - yOffset);
        edgyOffsets.Add(zOffset - 1);
        edgyOffsets.Add(zOffset + yOffset);
        edgyOffsets.Add(zOffset + 1);

        foreach (int v in voxelsIn)
        {
            ranking[v] = 0;
        }

        int previousNumberOfVoxels;
        do
        {
            previousNumberOfVoxels = ranking.Count;
            foreach (int v in ranking.Keys)
            {
                int vScore = 0;
                foreach (int offset in neighborOffsets)
                {
                    if (ranking.Keys.Contains(v + offset))
                    {
                        vScore += 100;
                    }
                }
                foreach (int offset in edgyOffsets)
                {
                    if (ranking.Keys.Contains(v + offset))
                    {
                        vScore += 1;
                    }
                }
                ranking[v] = ranking[v] + vScore;
            }

            // see if single highest value voxel exists
            var chosen = keyWithHighestValue(ranking);
            if (chosen.HasValue)
            {
                var resultList = new List<int>();
                resultList.Add(chosen.Value);
                return resultList;
            }
            // prune keys with lowest Value
            var lowestRankers = keysWithLowestValue(ranking);
            if (lowestRankers.Count < ranking.Count) {
                foreach (int v in lowestRankers) { 
                    ranking.Remove(v);
                }
           }

        } while (ranking.Count < previousNumberOfVoxels);

        // if we get here, winnowing the voxels has not resulted in a single winner
        // the results are either symmetric (a 2x2x2 cube) or disjoint
        // (two single voxels which are not neighbors)
        // in this case, pick the one with the lower index

                var resultList2 = new List<int>();
                foreach (int key in ranking.Keys) {
                     resultList2.Add(key);
                }
                return resultList2;

    }

    (List<int>, List<int>) FindShell(List<int> voxelsIn, List<int> edgeIn)
    {
        List<int> core = new List<int>();
        List<int> shell = new List<int>();
    
        foreach (int v in voxelsIn)
        {
            int zOffset = dimA * dimB;
            int yOffset = dimA;
            List<int> neighborOffsets = new List<int>();
            neighborOffsets.Add(-zOffset);
            neighborOffsets.Add(-yOffset);
            neighborOffsets.Add(-1);
            neighborOffsets.Add(1);
            neighborOffsets.Add(yOffset);
            neighborOffsets.Add(zOffset);

            bool foundEdge = false;
            foreach (int offset in neighborOffsets)
            {
                if (edgeIn.Contains(v + offset))
                {
                    foundEdge = true;
                    break;
                }
            }

            if (foundEdge)
            {
                shell.Add(v);
            }
            else
            {
                core.Add(v);
            }

        }
        return (shell, core);
    }

    (List<int>, List<int>) FindFirstEdges(List<int> voxels)
    {
        // firstEdges are all the voxels in voxels adjacent to a voxel not in voxels
        // plus all voxels on the box edges
        // first, make a list of voxelIndices not in voxels
        List<int> notInVoxels = new List<int>(); 
        List<int> innerVoxels = new List<int>();
        List<int> edgeVoxels = new List<int>();
        for (int i = 0; i < nVoxels; ++i)
        {
            if (!voxels.Contains(i))
            {
                notInVoxels.Add(i);
            }
        }

        List<int> cellEdges = CellEdges(dimA, dimB, dimC);
        foreach (int v in voxels)
        {
            int zOffset = dimA * dimB;
            int yOffset = dimA;
            List<int> neighborOffsets = new List<int>();
            neighborOffsets.Add(-zOffset);
            neighborOffsets.Add(-yOffset);
            neighborOffsets.Add(-1);
            neighborOffsets.Add(1);
            neighborOffsets.Add(yOffset);
            neighborOffsets.Add(zOffset);

            if (cellEdges.Contains(v))
            {
                edgeVoxels.Add(v);
            } 
            else
            {
                // check if any of its neighbors are in notInVoxels
                // we know that v is not at the edge, so neighbor index calculation is easy
                bool foundEdge = false;
                foreach (int offset in neighborOffsets) {
                    if (notInVoxels.Contains(v+ offset))
                    {
                        foundEdge = true;
                        break;
                    }
                }

                if (foundEdge) {
                    edgeVoxels.Add(v);
                }
                else
                {
                    innerVoxels.Add(v);
                }
                // int[] neighbors = [v - zOffset, v - yOffset, v - 1, v + 1, v + yOffset, v + zOffset];

            }
        }
        return (innerVoxels, edgeVoxels);
    }

    // return a List of voxel indices corresponding to all the voxels on the edge of the cell
    List<int> CellEdges(int nx, int ny, int nz)
    {
        // add voxel indices for the top face, i.e. at z = 0
        List<int> cellEdges = new List<int>();
        for (int v = 0; v < nx * ny; ++v)
        {
            cellEdges.Add(v);
        }
        // add voxel indices for corners and edges for each plane of z not at the top or bottom

        for (int z = 1; z <(nz- 1); ++z)
        {
            int zOffset = z * (nx * ny);
            // add row of pixels at the y = 0 edge
            for (int x = 0; x < nx; ++x)
            {
                cellEdges.Add(x + zOffset);
            }

            // add the two cells at x = 0 and x = nx-1 for each y
            for (int y = 1; y < (ny - 1); ++y)
            {
                cellEdges.Add((y * nx) + zOffset);
                cellEdges.Add((y * nx) + (nx - 1) + zOffset);
            }

            int yOffset = (ny - 1) * nx;
            // add row of pixels at the y = ny - 1 edge
            for (int x = 0; x < nx; ++x)
            {
                cellEdges.Add(x + yOffset + zOffset);
            }
        }

        // add voxel indices for the bottom face, i.e. at z = nz - 1
        int bottomFaceOffset = (nz - 1) * (nx * ny);
        for (int v = 0; v < nx * ny; ++v)
        {
            cellEdges.Add(v + bottomFaceOffset);
        }

        return cellEdges;
    }

}