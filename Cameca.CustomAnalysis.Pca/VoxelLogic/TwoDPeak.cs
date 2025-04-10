using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using PcaExtensionMethods;
using static System.Formats.Asn1.AsnWriter;
using System.Windows.Controls;

public struct PixelSuggestion: IComparable<PixelSuggestion> 
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
}


public class TwoDPeak
{
    public PeakID peakId;
    PixelID peakMaxPixelId; // this is also the id for this object in container's dictionary
    List<PixelID> pixelIds;
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
        this.pixelIds = new List<PixelID>();
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
        return pixelIds;
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
        removeFromCandidates(suggestion);
        pixelIds.Add(suggestion.pixelId);
        List<PixelID> newPossibilities = referenceGrid.PixelIdsNeighboring(suggestion.pixelId);
        List<PixelID> availableIds = partitionFinder.filterForAvailableIds(newPossibilities);
        foreach(PixelID availableId in availableIds)
        {
            float neighborScore = referenceGrid.valueAtPixel(availableId);

            if (neighborScore > suggestion.score)
            {
                // neighbor has higher score -- shouldn't add to current peak
                foundIncreaseIds.Add(availableId);
            }
            else
            {
                PixelSuggestion newSuggestion = new PixelSuggestion(peakId, availableId, neighborScore);
                this.InsertCandidate(newSuggestion);
            }
        }
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





