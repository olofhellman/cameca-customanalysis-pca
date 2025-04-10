using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;
 
 

public class CandidateRanker
{
    float bestScore;
    List<PixelSuggestion> candidates; // first value is peakID, second value is pixelId
    public CandidateRanker()
    {
        bestScore = -1.0f;
        candidates = new List<PixelSuggestion> ();
    }

    public void Clear()
    {
        bestScore = -1.0f;
        candidates.Clear();
    }
    public void Consider(PixelSuggestion suggestion)
    {
        float suggestionScore = suggestion.score;
        if (suggestionScore >= bestScore)  
        {
            if (suggestionScore > bestScore)
            {
                bestScore = suggestionScore;
                candidates.Clear();
            }
            candidates.Add(suggestion);
        } 
    }

    public List<PixelSuggestion>  bestCandidates()
    {
        return candidates;
    }
}




