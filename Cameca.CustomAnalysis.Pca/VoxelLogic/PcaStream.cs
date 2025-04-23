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

    public void DumpVoxelSetStats(string prefix, Dictionary<string, HashSet<VoxelID>> voxelSets)
    {
        outputFile.WriteLine(prefix);
        foreach(KeyValuePair<string, HashSet<VoxelID>> kvp in voxelSets)
        {
            outputFile.WriteLine(kvp.Key + ": " + kvp.Value.Count());
        }
        outputFile.WriteLine("!");
    }

    // for use in filename, can't use : -- replace those with -
    // Using the format string "s" results in a string like this 2008-06-15T21:15:07
    static string DateAsString(DateTime dt)
    { 
        string s = dt.ToString("s");
        return s.Replace(":", "-");
    }

    public PcaStream(string fileNameStem)
    {
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        creationTime = DateTime.Now;
        string filename = fileNameStem + "-" + DateAsString(creationTime);
        string outputFilename = System.IO.Path.Combine(docPath, filename);

        outputFile = new StreamWriter(outputFilename);
    }

    public void WriteTimestamp(string prefix)
    {
        DateTime dtNow = DateTime.Now;
        TimeSpan diff = dtNow - creationTime;
        outputFile.WriteLine(prefix + " timestamp " +  diff.ToString());
    }

    public void Close()
    {
        outputFile.Close();
    }
} 




