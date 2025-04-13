using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using PcaExtensionMethods;


public class TwoDGridPartitionFinder
{
    DensityPlane grid;
    List<PixelID> unplacedPixelIds;
    List<PixelID> rejectedPixelIds;
    List<PixelID> foundIncreasePixelIds; // when a peak finds an increase, remember it here
    Dictionary<PeakID, TwoDPeak> peaks;

    public enum ReturnCode {
        noStatus,
        foundIncrease,
        bestSuggestionBelowFloor,
        exhaustedSuggestions
    }

    public TwoDGridPartitionFinder(DensityPlane twoDGrid)
    {
        this.grid = twoDGrid;
        this.peaks = new Dictionary<PeakID, TwoDPeak> ();
        this.rejectedPixelIds = new List<PixelID>();
        this.foundIncreasePixelIds = new List<PixelID>();
        this.unplacedPixelIds = twoDGrid.GridPointIds();
    }

    // for each iteration step,
    // 1) ask the different peaks if they have found an increase
    //    if yes -- make new peak, otherwise
    // 2) ask the different peaks to suggest 
    //    their next target pixel(s)
    // 3) find highest valued pixel(s)
    // 4) reject pixels that collide
    //    -- notify each peak of rejection
    //    -- place rejected pixels in rejected bucket
    //    -- remove from unplacedPixelIds
    // 5) stop if highest valued pixel is below floor
    // 6) accept all highest valued pixels
    //    -- notify each peak of acceptance and
    //    -- remove from unplacedPixelIds

    public ReturnCode IterateIdentifyingPeaks(float noiseFloor)
    {
        bool keepGoing = true;
        ReturnCode returnCode = ReturnCode.noStatus;
        while (keepGoing)
        {
            CandidateRanker candidateRanker = new CandidateRanker();

            //step 1
            foreach (PeakID peakKey in peaks.Keys.ToList())
            {
                TwoDPeak nthPeak = peaks[peakKey];
                if (nthPeak.HasFoundIncrease()) {
                    // need to clean out the 'HasFoundIncrease' flag from the peaks
                    // this will get done by the caller if we return the 'found increase' flag
                    return ReturnCode.foundIncrease; 
                }
            }

            // step 2
            candidateRanker.Clear();
            foreach (PeakID peakKey in peaks.Keys.ToList())
            {
                TwoDPeak nthPeak = peaks[peakKey];
                var nthSuggestions = nthPeak.NextSuggestions();
                foreach (PixelSuggestion nthSuggestion in nthSuggestions)
                {
                    candidateRanker.Consider(nthSuggestion);
                }
            }

            // step 3
            List<PixelSuggestion> bestCandidates = candidateRanker.bestCandidates();
            if (bestCandidates.Count == 0)
            {
                return ReturnCode.exhaustedSuggestions;
            }

            // step 4
            // count all the instances of each ID
            Dictionary<PixelID, int> pixelIdCounts = new Dictionary<PixelID, int>();
            foreach (PixelSuggestion suggestion in bestCandidates)
            {
                if (pixelIdCounts.ContainsKey(suggestion.pixelId))
                {
                    pixelIdCounts[suggestion.pixelId] = pixelIdCounts[suggestion.pixelId] + 1;
                }
                else
                {
                    pixelIdCounts[suggestion.pixelId] = 1;
                }
            }
            // if there are any elements of the pixelIdCounts dictionary with value more than 1,
            // that's a collision

            List<PixelID> collisions = new List<PixelID>();
            List<PixelID> pixelIdCountsKeys = pixelIdCounts.Keys.ToList();
            foreach (PixelID pixelId in pixelIdCountsKeys)
            {
                if (pixelIdCounts[pixelId] > 1)
                {
                    collisions.Add(pixelId);
                }
            }

            // foreach (PixelID collidingPixelId in collisions)
            if (collisions.Count > 0)
            {
                List<PixelSuggestion> tempCandidates = bestCandidates;
                bestCandidates = new List<PixelSuggestion>();
                // remove the collisions from bestCandidates
                // add the ids to rejectedPixelIds
                // notify peaks of rejection

                foreach (PixelSuggestion suggestion in tempCandidates)
                {
                    if (collisions.Contains(suggestion.pixelId))
                    {
                        peaks[suggestion.peakId].SuggestionRejected(suggestion);
                    }
                    else
                    {
                        bestCandidates.Add(suggestion);
                    }
                }

                foreach (PixelID pixelId in collisions)
                {
                    rejectedPixelIds.Add(pixelId);
                    unplacedPixelIds.Remove(pixelId);
                }
            }


            // step 6
            // for each accepted suggestion, notify the peak of Acceptance,
            // remove the id from unplaced IDs

            foreach (PixelSuggestion suggestion in bestCandidates)
            {
                // step 5 -- make sure the score is above the floor
                if (suggestion.score < noiseFloor)
                {
                    returnCode = ReturnCode.bestSuggestionBelowFloor;
                    keepGoing = false;
                }
                else
                {
                    peaks[suggestion.peakId].SuggestionAccepted(suggestion);
                    unplacedPixelIds.Remove(suggestion.pixelId);
                }
            }
        }
        return returnCode;
    }


    public void Clear()
    {
        peaks.Clear();
        rejectedPixelIds.Clear();
        unplacedPixelIds.Clear();
        unplacedPixelIds = grid.GridPointIds();
    }

    public List<List<PixelID>> GetPixelLists()
    {
        List<List<PixelID>> pixelLists = new List<List<PixelID>>();
        foreach (PeakID peakKey in peaks.Keys.ToList())
        {
            TwoDPeak nthPeak = peaks[peakKey];
            List<PixelID> peakPixelList = nthPeak.PixelList();
            pixelLists.Add(peakPixelList);
        }
        return pixelLists;
    }

    public void FindPartitions(float noiseFloorFraction)
    {
        float noiseFloor = grid.MaximumValue(unplacedPixelIds) * noiseFloorFraction;
        PixelID? maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
        bool shouldContinueWithLowerSearchFloor = false;
        while (maybeMaximumPixelId.HasValue || shouldContinueWithLowerSearchFloor)
        {
            if (!shouldContinueWithLowerSearchFloor && maybeMaximumPixelId.HasValue)
            {
                PixelID maximumPixelId = maybeMaximumPixelId.Value;
                TwoDPeak nextPeak = new TwoDPeak(this, grid, maximumPixelId);
                peaks[nextPeak.peakId] = nextPeak;
            }

            shouldContinueWithLowerSearchFloor = false;
            float higherPixelVal = 0.0f;
            if (foundIncreasePixelIds.Count > 0)
            {
                higherPixelVal = grid.valueAtPixel(foundIncreasePixelIds[0]);
            }
            float searchFloor = Math.Max(higherPixelVal, noiseFloor);
            ReturnCode returnCode = IterateIdentifyingPeaks(searchFloor);

            switch (returnCode)
            {
                case ReturnCode.noStatus:
                    {
                        Debug.Write("returnCode noStatus -- must be error");
                        break;
                    }
                case ReturnCode.foundIncrease:
                    {
                        Debug.Write("returnCode found increase");
                            // need to remove the 'found increase' buckets from the peaks;
                            // also, collect these so that we can remember to look for new peaks
                        foreach (PeakID peakId in peaks.Keys.ToList())
                        {
                            List<PixelID> higherPixels = peaks[peakId].ClearFoundIncreases();
                            foreach (PixelID pixelId in higherPixels)
                            {
                                foundIncreasePixelIds.AddSorted(pixelId);
                            }
                        }
                        maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
                        break;
                    }
                case ReturnCode.bestSuggestionBelowFloor:
                    {
                        // its possible we have another peak to find
                        // first. filter any previous identified higherPixels to see if they are now part of a new peak:
                        foundIncreasePixelIds = filterForAvailableIds(foundIncreasePixelIds);
                        if (foundIncreasePixelIds.Count > 0)
                        {
                            // it is a certainty we still have a peak to find
                            maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
                        }
                        else
                        {
                            // if a new peak was identified, it has absorbed the pixels we found
                            // we should continue with a lower search floor
                            maybeMaximumPixelId = null;
                            shouldContinueWithLowerSearchFloor = (noiseFloor < searchFloor);
                        }
                        break;
                    }
                case ReturnCode.exhaustedSuggestions:
                    {
                        maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
                        break;
                    }
            }
      
        }
    }

    public List<PixelID> filterForAvailableIds(List<PixelID> pixelIds)
    {
        // return a list containing all the items in the
        // input list which are in the unplacedPixelIds list
        return pixelIds.Where(pixelId => unplacedPixelIds.Contains(pixelId)).ToList();
    }
}   





