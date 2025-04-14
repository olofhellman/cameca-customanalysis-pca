#include "pch.h"
#include "RMT_rank.h"

#include <array>
#include <cmath>
#include <Eigen/Dense>

using namespace Eigen;

template <typename T>
TWscale<T>::TWscale(int nIn, int pIn) : n(nIn), p(pIn) {
    T nm = (T)n - 0.5;
    T pm = (T)p - 0.5;
    mu = pow(sqrt(nm) + sqrt(pm), 2) / (T)n;
    sigma = cbrt(1.0 / sqrt(nm) + 1 / sqrt(pm)) * (sqrt(nm) + sqrt(pm)) / (T)n;
}

template <typename T>
void TWscale<T>::TWupdate(int nIn, int pIn) {
    n = nIn;
    p = pIn;
    T nm = (T)n - 0.5;
    T pm = (T)p - 0.5;
    mu = pow(sqrt(nm) + sqrt(pm), 2) / (T)n;
    sigma = cbrt(1.0 / sqrt(nm) + 1 / sqrt(pm)) * (sqrt(nm) + sqrt(pm)) / (T)n;
}

// The eigenvalue ajdustment procedure accounts for slight offsets or slope
// variation of the eigenvalue plot from theory.
template <typename derived>
std::pair<double, double> EigenvalueAdjustment(MatrixBase<derived>& evals, int nObs, int rank, double frac2fit) {
    using T = typename MatrixBase<derived>::Scalar;
    int nEvals = evals.size();

    // Only fit the leading fraction of noise values
    int number2fit = std::lround(frac2fit * (nEvals - rank));

    // Compute the MP noise-eigenvalude distribution given the rank
    MarchenkoPasturDist<T> MPdist(nObs, nEvals - rank);
    Vector<T, Dynamic> RandEvals(nEvals - rank);
    Map<Vector<T, Dynamic>> RandEvalsMap(RandEvals.data(), nEvals - rank);
    MPdist.NoiseEvals(0, RandEvalsMap);  // all of the eigenvalues

    // Perform least squares fit of actual eigenvalues vs predicted noise eigenvalues
    Matrix<T, Dynamic, Dynamic> A(number2fit, 2);
    A.col(0) = RandEvals.head(number2fit);
    A.col(1).fill((T)1.0);
    Vector<T, Dynamic> b = evals.segment(rank, number2fit);
    Vector<T, Dynamic> x = A.colPivHouseholderQr().solve(b);

    std::pair<double, double> lineParams;
    lineParams.first = (double)x(0); // slope
    lineParams.second = (double)x(1); // intercept
    return lineParams;
}


template <typename T>
MarchenkoPasturDist<T>::MarchenkoPasturDist(int inN, int inP) : N(inN), p(inP) {
    c = (T)p / (T)N;
    fractionNZ = c > 1.0 ? 1 / c : 1;
    a = pow(1 - sqrt(c), 2); // minimum eigenvalue
    b = pow(1 + sqrt(c), 2); // maximum eigenvalue
    // constants for CDF calculation
    ab = a * b; sqrtab = sqrt(a * b);
    aSqr = a * a; bSqr = b * b;
    b_plus_a = b + a; b_minus_a = b - a;
    k1 = (2.0 * sqrtab - b_plus_a) * HALFPI;
    k2 = 2 * TWOPI * c;
};

template <typename T>
T MarchenkoPasturDist<T>::CDF(const T x) {
    T T1, T2, T3, y;
    if (x <= a)
        y = 1.0 - fractionNZ;
    else if (x >= b)
        y = 1.0;
    else {
        T1 = std::min<T>(1.0, std::max<T>(-1.0, (b_plus_a * x - 2.0 * ab) / (b_minus_a * x)));
        T2 = std::min<T>(1.0, std::max<T>(-1.0, (2.0 * x - b_plus_a) / b_minus_a));
        T3 = std::max<T>(0.0, (x - a) * (b - x));
        y = -(2.0 * sqrtab * std::asin(T1) - b_plus_a * std::asin(T2)
            - 2.0 * sqrt(T3) + k1) / k2;
    }
    return y;
};

// Bisection algorithm from Numerical Recipes 3rd ed, p449
template <typename T>
T MarchenkoPasturDist<T>::ICDF(const T targetP) {
    const int maxIter = 50;
    const T tol = b * std::numeric_limits<T>::epsilon();
    T dx = b_minus_a, xmid = a, fmid = 1.0, rtb = a;
    for (auto j = 0; j < maxIter; j++) {
        dx *= 0.5;
        xmid = rtb + dx;
        fmid = CDF(xmid) - targetP;
        if (fmid <= 0.0) rtb = xmid;
        if ((dx < tol) || fmid == 0.0) break;
    }
    return rtb;
};

template <typename T>
void MarchenkoPasturDist<T>::NoiseEvals(int ncomp, Map<Matrix<T, Dynamic, 1>>& x, Map<Matrix<T, Dynamic, 1>>& y) {
    int n2compute = p - ncomp <= x.size() ? p - ncomp : x.size(); // Assumes N > p
    T targetP;
    // mexPrintf("p: %d, ncomp: %d, n2compute: %d\n\n",p,ncomp,n2compute);
    if (ncomp == 0) {
        y(0) = b; y(n2compute - 1) = a;
        for (auto i = 1; i < n2compute - 1; i++) {
            targetP = 1.0 - (T)i / (T)(n2compute - 1);
            y(i) = ICDF(targetP);
        }
        x.setLinSpaced(n2compute, ncomp + 1, ncomp + n2compute);
    }
    else {
        MarchenkoPasturDist<T> noiseDist(N, n2compute);
        noiseDist.NoiseEvals(0, x, y);
        x.array() += (T)ncomp;
    }
};

template <typename T>
void MarchenkoPasturDist<T>::NoiseEvals(int ncomp, Map<Matrix<T, Dynamic, 1>>& y) {
    int n2compute = p - ncomp <= y.size() ? p - ncomp : y.size(); // Assumes N > p
    T targetP;
    if (ncomp == 0) {
        y(0) = b; y(n2compute - 1) = a;
        for (auto i = 1; i < n2compute - 1; i++) {
            targetP = 1.0 - (T)i / (T)(n2compute - 1);
            y(i) = ICDF(targetP);
        }
    }
    else {
        MarchenkoPasturDist<T> noiseDist(N, n2compute);
        noiseDist.NoiseEvals(0, y);
    }
};


// Automatic rank-estimation based on either the largest noise eigenvalue (nGaps = 0)
// or the gap between the largest and nth largest noise eigenvalues (nGaps > 0)
template <typename derived>
int EstimateRank(MatrixBase<derived>& evals, int nObs, int nGaps, int P, bool refine) {
    using T = typename MatrixBase<derived>::Scalar;
    int nEvals = evals.size();
    Vector<T, Dynamic> gaps;
    if (nGaps > 0)
        gaps = evals.head(nEvals - nGaps) - evals.tail(nEvals - nGaps);
    else
        gaps = evals;
    TWscale<T> params(nObs, nEvals);

    BornemannTable BT;
    T q = nGaps > 0 ? (T)BT.getGapQuantile(P, nGaps) : (T)BT.getQuantile(P);

    // Compute the initial rank estimate
    int rank = 0;
    T threshold;
    for (rank = 0; rank < nEvals - nGaps - 1; rank++) {
        threshold = nGaps > 0 ? params.sigma * q : params.mu + params.sigma * q;
        if ((gaps(rank) < threshold) && (gaps(rank + 1) < threshold))
            break;
        else
            params.TWupdate(nObs, nEvals - rank - 1);
    }

    if (refine) {
        int oldrank;
        int iter = 0;
        std::pair<double, double> linfit;
        Vector<T, Dynamic> adjustedEvals(nEvals);
        do {
            oldrank = rank;
            // Adjust the eigenvalues based on the rank
            linfit = EigenvalueAdjustment(evals, nObs, rank);
            adjustedEvals.array() = (evals.array() - linfit.second) / linfit.first;
            rank = EstimateRank(adjustedEvals, nObs, nGaps, P, false);
            iter++;
        } while (rank > 0 && rank < oldrank && iter < 3);
    }

    return rank;
};

DOPCAFLOAT_API int EstimateRankF(
	float* evals,
	const int nEvals,
	const int nObs,
	const int nGaps,
	const int P,
	const bool refine) {
    Map<VectorXf> mEvals(evals, nEvals);
    return EstimateRank(mEvals, nObs, nGaps, P, refine);
}

DOPCAFLOAT_API void NoiseEvals(
    const int gapRank,
    const int nEvals,
    const int nObs,
    float* yPtr) {
    Map<VectorXf> y(yPtr, nEvals - gapRank, 1);
    MarchenkoPasturDist<float> MPdist(nObs, nEvals);
    MPdist.NoiseEvals(gapRank, y);
}