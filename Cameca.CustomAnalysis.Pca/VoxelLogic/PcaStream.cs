using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;
using Cameca.CustomAnalysis.Interface;
using System.DirectoryServices.ActiveDirectory;

// PcaStream is a utility class for writing information to a file during a run of the 
// Pca code in APSuite
// On creation of the object, a unique name is chosen based on timestamp.
// 

public class PcaStream  
{
    StreamWriter outputFile;
    DateTime creationTime;

    ("pcaCodeVoxelSets", pcaCodeVoxelSets);
    public void DumpVoxelSetStats(string prefix, Dictionary<string, HashSet<VoxelID>> voxelSets)
    {
        outputFile.WriteLine(prefix);
    }

    static string dateAsString(DateTime dt)
    {
        return dt.ToString("x", cultures);
    }

    public PcaStream(string fileNameStem)
    {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        creationTime = DateTime.Now;
        string filename = fileNameStem + "-" + DateAsString(creationTime);
        outputFile = new StreamWriter(filename);
    }

    public WriteTimestamp(string prefix)
    {
        DateTime dtNow = DateTime.Now;
        TimeSpan diff = dtNow - creationTime;
        outputFile.WriteLine(prefix + " timestamp " +  diff.ToString);
    }

    public Close()
    {
        outputFile.Close();
    }

} 




