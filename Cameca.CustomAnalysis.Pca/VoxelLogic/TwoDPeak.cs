using System.Collections.Generic;
using System.Diagnostics;
using System;
using PcaExtensionMethods;


// PixelSuggestion represents a pixel that could be added to a TwoDPeak as part of the PartitionFinder
// peak partitioning algorithm
public struct PixelSuggestion : IComparable<PixelSuggestion>
{
    public float score;
    public PixelID pixelId;
    public PeakID peakId;

    public PixelSuggestion(PeakID peak, PixelID pixel, float score)
    {
        this.score = score;
        this.pixelId = pixel;
        this.peakId = peak;
    }

    public int CompareTo(PixelSuggestion other)
    {
        return other.score < score ? -1 : other.score > score ? 1 : 0;
    }

    public string DebugStr()
    {
        return "PixelID: " + pixelId.DebugStr() + ", score: " + score + ", peak: " + peakId.DebugStr();

    }
}


// TwoDPeak represents a maximum in a DensityPlane grid and its surrounding pixels, as part of
// the Partitioning algorithm implemented in TwoDGridPartitionFinder
// Initially it is created with a single pixel location, and it grows to surrounding pixels
// governed by the TwoDGridPartitionFinder.
//
// It maintains an ordered list of "pixels to add", and during the algorithm, repeated provides
// to PartitionFinder the next 'best candidate' for adding to itself.  PartitionFinder evaluates all the 
// candidates provided by all the peaks it knows about, and selects them.  When a candidate is selected
// the SuggestionAccepted method is called, and new potential candidates are added to its list of "pixels to add"
//
// As an example, if the densities look like this:
//
//   50     70    50     40    30
//   80    101   100     97    70
//   70     99    98     80    60
//   30     40    50     70    50   
//
// The peak would grow like this:
//
//  Cycle       values of ids in the Peak list   Values of Pixels in the candidates list 
//  0           {}                               {101}
//  1           {101}                            {100, 99, 80, 70}
//  2           {101, 100}                       {99, 98, 97, 80, 70, 50}
//  3           {101, 100, 99}                   {98, 97, 80, 70, 70, 50, 40}
//  4           {101, 100, 99, 98}               {97, 80, 80, 70, 70, 50, 50, 40}
//  5           {101, 100, 99, 98, 97}           {80, 80, 70, 70, 70, 50, 50, 40, 40}
//  6           {101, 100, 99, 98, 97, 80, 80}   {70, 70, 70, 70, 60, 50, 50, 50, 40, 40}
//
// After step 5, both of the '80' pixels would be returned as part of the best candidates.
//
// A future logical addition:  right now, the algorithm won't handle the case very well where what should really 
// be a single peak is actually two maxima very close together. Some logic should be added so that when a 
// "increase" is found above a certain fraction of the peak maximum, it is not considered as evidence of a second peak.
//
// As an example, if the densities look like this:
//
//   50     70    50     40    30
//   80    101   100     97    70
//   70    100    98     99    80
//   30     40    50     70    50    
//
// The algorthim will find two peaks but it should probably only find one
// The logic could be that if a pixel at higher than some threshhold of
// the peak maximum, say 75%, finds an increase, it shouldn't warn of an 'increase found'
// result, but just accumulate that pixel as well
// in the example above, the peak would start at 101, then grow to the two 100 pixels.
// the next candidate offered/accepted would be the 98 pixel.  At that point, the 99
// pixel would be considered for adding to the candudates list, but as it is an increase from 
// 98, algorithm would flag it as the case where a second peak must exist.
public class TwoDPeak
{
    public PeakID peakId;
    PixelID peakMaxPixelId; // this is also the id for this object in container's dictionary
    List<PixelID> borderPixelIds;
    List<PixelID> inPeakPixelIds;
    List<PixelSuggestion> nextCandidates;
    List<PixelSuggestion> topCandidates; // this list is recycled as the return vehicle for getting next suggestions
    DensityPlane referenceGrid;
    List<PixelID> foundIncreaseIds; // list of pixelIds at edge which increase relative to neighbor
    TwoDGridPartitionFinder partitionFinder; // will supply available neighbors

    public TwoDPeak(TwoDGridPartitionFinder partitionFinder, DensityPlane grid, PixelID peakMaxPixelId)
    {
        this.partitionFinder = partitionFinder;
        this.peakMaxPixelId = peakMaxPixelId;
        this.peakId = new PeakID(peakMaxPixelId);
        this.inPeakPixelIds = new List<PixelID>();
        this.borderPixelIds = new List<PixelID>();
        this.nextCandidates = new List<PixelSuggestion>();
        this.topCandidates = new List<PixelSuggestion>();
        this.referenceGrid = grid;
        this.foundIncreaseIds = new List<PixelID>();

        float firstSuggestionScore = grid.valueAtPixel(peakMaxPixelId);
        PixelSuggestion firstSuggestion = new PixelSuggestion(peakId, peakMaxPixelId, firstSuggestionScore);
        nextCandidates.Add(firstSuggestion);
    }

    public List<PixelID> PixelList()
    {
        return inPeakPixelIds;
    }

    public bool HasFoundIncrease()
    {
        return foundIncreaseIds.Count > 0;
    }
    // if suggestion accepted, remove from list and 
    // add adjacent pixels to list
    public void removeFromCandidates(PixelSuggestion suggestion)
    {
        nextCandidates.Remove(suggestion);
    }
    // if suggestion accepted, remove from list and 
    // add adjacent pixels to list
    public void SuggestionAccepted(PixelSuggestion suggestion)
    {
        Debug.WriteLine("SuggestionAccepted in peak " + this.peakId.DebugStr() + " : " + suggestion.DebugStr());
        removeFromCandidates(suggestion);
        bool isBorder = false;
        
        List<PixelID> newPossibilities = referenceGrid.PixelIdsNeighboring(suggestion.pixelId);
        List<PixelID> availableIds = partitionFinder.filterForAvailableIds(newPossibilities);
        foreach(PixelID availableId in availableIds)
        {
            // this might already be in our list of candidates -- if it is, skip

            if (!IsCandidate(availableId))
            {
                float neighborScore = referenceGrid.valueAtPixel(availableId);

                if (neighborScore > suggestion.score)
                {
                    // neighbor has higher score -- shouldn't add to current peak
                    // the currently 'accepted suggestion' is actually a pixel between peaks.
                    isBorder = true;
                    Debug.WriteLine("Found increase at pixel " + availableId.DebugStr() + " neighborScore: " + neighborScore + " suggestionScore: " + suggestion.score);
                    foundIncreaseIds.Add(availableId);
                }
                else
                {
                    PixelSuggestion newSuggestion = new PixelSuggestion(peakId, availableId, neighborScore);
                    this.InsertCandidate(newSuggestion);
                }
            }
        }
        if (isBorder)
        {
            borderPixelIds.Add(suggestion.pixelId);
        }
        else
        {
            inPeakPixelIds.Add(suggestion.pixelId);
        }
    }

    public bool IsCandidate(PixelID pixelId)
    {
        foreach ( PixelSuggestion candidate in  nextCandidates)
        {
            if (candidate.pixelId.pixelId == pixelId.pixelId)
            {
                return true;
            }
        }
        return false;
    }

    public List<PixelID> ClearFoundIncreases()
    {
        List<PixelID> returnList =  foundIncreaseIds;
        foundIncreaseIds = new List<PixelID>();
        return returnList;
    }


    // this peak maintains a list of the candidate neighbor pixels 
    // which could be added to the peak during the accumulation
    // the 
    public void InsertCandidate(PixelSuggestion candidate)
    {
        nextCandidates.AddSorted(candidate);
    }
    // if suggestion rejected, remove from list
    public void SuggestionRejected(PixelSuggestion suggestion)
    {
        removeFromCandidates(suggestion);
    }

    public List<PixelSuggestion> NextSuggestions()
    {
        topCandidates.Clear();
        if (nextCandidates.Count > 0)
        {
            var topCandidate = nextCandidates[0];
            topCandidates.Add(topCandidate);
            float bestScore = topCandidate.score;
            bool keepGoing = true;
            int nextIndex = 1;
            while (keepGoing)          
            {
                if (nextIndex < nextCandidates.Count)
                {
                    topCandidate = nextCandidates[nextIndex];
                    if (topCandidate.score < bestScore)
                    {
                        keepGoing = false;
                    }
                    else
                    {
                        topCandidates.Add(topCandidate);
                        nextIndex += 1;
                    }  
                }
                else { keepGoing = false; }
                
            }
        }
        return topCandidates;
    }
}





