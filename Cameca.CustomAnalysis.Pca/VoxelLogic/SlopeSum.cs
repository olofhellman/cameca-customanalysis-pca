using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;

 
public struct SlopeSum
{
    public float slopeSum; // based entirely on the values of neighbors
    public float deltaSum; // sum of differences with neighbors

    public SlopeSum(float slope, float delta)
    {
        slopeSum = slope;
        deltaSum = delta;
    }
};





