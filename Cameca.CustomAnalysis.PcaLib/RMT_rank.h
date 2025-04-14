#pragma once
#include <array>
#include <cmath>
#include <Eigen/Dense>

#ifdef DOPCAFLOAT_EXPORTS
#define DOPCAFLOAT_API __declspec(dllexport)
#else
#define DOPCAFLOAT_API __declspec(dllimport)
#endif

#define TWOPI 6.283185307179586476925286766559005768e+00
#define HALFPI 1.570796326794896619231321691639751442e+00
#define ROOTHALFPI 1.253314137315500251207882642405522626e+00

using namespace Eigen;

template <typename Type, int Size>
using Vector = Matrix<Type, Size, 1>;

template <typename T>
class TWscale {
public:
	int n, p; // row and columns of data matrix
	T mu;
	T sigma;
	TWscale(int n, int p);
	void TWupdate(int n, int p);
};

template <typename T>
class MarchenkoPasturDist {
private:
    int N, p;     // input matrix is N samples x p variables,
    T c;          // ratio p/N
    T a, b;       // lower and upper edges of MP distribution support
    T fractionNZ; // For the case that N < p (will never happen for APT)
    T ab, sqrtab, aSqr, bSqr, b_plus_a, b_minus_a, k1, k2;
public:
    MarchenkoPasturDist(int N, int P);
    T CDF(const T x); // MP distribution function
    T ICDF(const T targetP); // inverse of MP distribution function
    // Compute eigenvalues of random noise linearly interpolated between a and b
    // The vector y will have p - ncomp elements
    void NoiseEvals(int ncomp, Map<Matrix<T, Dynamic, 1>>& y);
    // In addition, return index vector x that runs from ncomp+1 to p
    void NoiseEvals(int ncomp, Map<Matrix<T, Dynamic, 1>>& x, Map<Matrix<T, Dynamic, 1>>& y);
};


//****************** Precomputed statistics of Tracy-Widom distribution ******************************
// Bornemann: "On the numerical evaluation of distributions in Random Matrix Theory"
class BornemannTable {
private:
    // Mean, variance and skew of TW distribution for 6 largest eigenvalues
    constexpr static std::array<std::array<double, 3>, 6> TracyWidomStats{ {
        {-1.2065335745, 1.6077810345, 0.2934645240},
        {-3.2624279028, 1.0354474415, 0.1655094943},
        {-4.8216302757, 0.8223901151, 0.1176214761},
        {-6.1620399636, 0.7031581054, 0.0923283954},
        {-7.3701147042, 0.6242523679, 0.0765398210},
        {-8.4862183723, 0.5670071487, 0.0656707705},
    } };
    // Effective range of non-zero TW density 0.00001 tails
    // First 4 computed with Momar Dieng twdist function
    constexpr static std::array<std::pair<double, double>, 6> Support{
        std::make_pair(-5.75, 5.25),
        std::make_pair(-7.16, 1.58),
        std::make_pair(-8.39, -0.65),
        std::make_pair(-9.52, -2.37),
        std::make_pair(-10.5, -3.5),
        std::make_pair(-11.4, -4.5)
    };
    //Quantiles computed with Momar Dieng RMLab for largest eigenvalue
    // First element of pair is 1 - the tail probability
    constexpr static std::array<std::pair<double, double>, 6> Quantile{
        std::make_pair(0.999, 3.2712252822),
        std::make_pair(0.995, 2.4221107431),
        std::make_pair(0.990, 2.0233353027),
        std::make_pair(0.980, 1.5976951003),
        std::make_pair(0.950, 0.9792895441),
        std::make_pair(0.900, 0.4501290525)
    };
    // Effective range of non-zero Eigengap density 0.00001 tails
    constexpr static std::array<std::pair<double, double>, 5> GapSupport{
        std::make_pair(0.00733, 7.87),
        std::make_pair(0.476, 11.59),
        std::make_pair(1.24, 12.41),
        std::make_pair(2.12, 13.25),
        std::make_pair(3.00, 14.19),
    };
    // Eigenvalue Gap quantiles of gap size 1:5, from Silverstein simulations
    constexpr static std::array<std::array<double, 6>, 6> GapQuantile{ {
        {0.999, 6.5411, 8.2047, 9.5850, 10.8275, 11.9935},
        {0.995, 5.6487, 7.3208, 8.7027,  9.9479, 11.1063},
        {0.990, 5.2338, 6.9016, 8.2896,  9.5334, 10.6916},
        {0.980, 4.7843, 6.4574, 7.8496,  9.0970, 10.2506},
        {0.950, 4.1371, 5.8160, 7.2069,  8.4541,  9.6078},
        {0.900, 3.5916, 5.2690, 6.6601,  7.9046,  9.0579}
    } };
public:
    double getTWmean(int i) { return TracyWidomStats[i][0]; }
    double getTWvar(int i) { return TracyWidomStats[i][1]; }
    double getTWskew(int i) { return TracyWidomStats[i][2]; }
    double getTWmeanGap(int i, int j) {
        return TracyWidomStats[i][0] - TracyWidomStats[j][0];
    };
    double getTWvarGap(int i, int j) {
        return TracyWidomStats[i][1] - TracyWidomStats[j][1];
    };
    double getQuantile(int P = 1) { return Quantile[P].second; }
    double getTailP(int P) { return 1.0 - Quantile[P].second; }
    double getGapQuantile(int P = 1, int gap = 1) {
        return GapQuantile[P][gap];
    }
    double getGapTailP(int P) { return 1.0 - GapQuantile[1][0]; }
    std::pair<double, double> getSupport(int P = 0) { return Support[P]; }
    std::pair<double, double> getGapSupport(int P = 0) { return GapSupport[P]; }
};

template <typename derived>
std::pair<double, double> EigenvalueAdjustment(MatrixBase<derived>& evals, int nObs, int rank, double frac2fit = 0.5);

template <typename T>
void TWscale<T>::TWupdate(int nIn, int pIn);

template <typename derived>
int EstimateRank(MatrixBase<derived>& evals, int nObs, int nGaps = 1, int P = 1, bool refine = false);

extern "C" DOPCAFLOAT_API int EstimateRankF(
    float* evals,
    const int nEvals,
	const int nObs,
	const int nGaps,
	const int P,
	const bool refine);

extern "C" DOPCAFLOAT_API void NoiseEvals(
    const int gapRank,
    const int nEvals,
    const int nObs,
    float* yPtr);