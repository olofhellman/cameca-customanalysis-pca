using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using Prism.Regions;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using System.Security.Cryptography;
using System.Windows.Input;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Xml.Schema;
using Cameca.CustomAnalysis.Interface;

// PcaVoxel represents a voxel and its PCA scores returned from the PCA algorithm
public struct PcaVoxel
{
    public int voxelIndex; // the voxelIndex from compResults.VoxelIndices;  (between 0 and nTotalVoxels - 1)
    public float[] scoreVector;

    public PcaVoxel(int vIndex, float[] vec)
    {
        voxelIndex = vIndex;
        scoreVector = vec;
    }
}

 





