using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.IO;
 
// CandidateRanker is used to identify the 'best' candidates from all the PixelSuggestion 
// objects it is asked to consider. Items added to the list of candidates are the 'best'
// until a better candidate is nominated.
//
// It would only be a small amount of work to generalize this to any IComparable instead
// instead of hard coding the reference to PixelSuggestion and PixelSuggestion.score
//
// It is used in the Peak separation algorithm -- each peak nominates the next pixel 
// for inclusion in a peak, so the number of items to be considered is the number of peaks
// There's not much 

public class CandidateRanker
{
    float bestScore;
    List<PixelSuggestion> candidates;  
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




