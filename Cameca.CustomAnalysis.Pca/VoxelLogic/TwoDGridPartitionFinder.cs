using System.Collections.Generic;
using System.Linq;



public class TwoDGridPartitionFinder
{
    DensityPlane grid;
    List<PixelID> unplacedPixelIds;
    List<PixelID> rejectedPixelIds;
    Dictionary<PeakID, TwoDPeak> peaks;

    public TwoDGridPartitionFinder(DensityPlane twoDGrid)
    {
        this.grid = twoDGrid;
        this.peaks = new Dictionary<PeakID, TwoDPeak> ();
        this.rejectedPixelIds = new List<PixelID>();
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

    public void IterateIdentifyingPeaks(float noiseFloor)
    {
        bool keepGoing = true;

        while (keepGoing)
        {
            CandidateRanker candidateRanker = new CandidateRanker();

            //step 1
            foreach (PeakID peakKey in peaks.Keys.ToList())
            {
                TwoDPeak nthPeak = peaks[peakKey];
                if (nthPeak.HasFoundIncrease()) { return; }
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
                    keepGoing = false;
                }
                else
                {
                    peaks[suggestion.peakId].SuggestionAccepted(suggestion);
                    unplacedPixelIds.Remove(suggestion.pixelId);
                }
            }
        }
 
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

    public void FindPartitions(float noiseFloor)
    {
        PixelID? maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
        while (maybeMaximumPixelId.HasValue)
        {
            PixelID maximumPixelId = maybeMaximumPixelId.Value;
            float populationAtNextMax = grid.valueAtPixel(maximumPixelId);
            if (populationAtNextMax > noiseFloor)
            {
                TwoDPeak nextPeak = new TwoDPeak(this, grid, maximumPixelId);
                peaks[nextPeak.peakId] = nextPeak;
                IterateIdentifyingPeaks(noiseFloor);

                maybeMaximumPixelId = grid.FindMaximum(unplacedPixelIds);
            }
            else
            {
                // if we get here, the next available peak has a maximum
                // less than the noise floor of the search, so there will be
                // nothing to find
                maybeMaximumPixelId = null;
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





