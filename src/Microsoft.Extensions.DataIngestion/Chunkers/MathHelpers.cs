// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    /// <summary>
    /// Math library for chunker implementations.
    /// Provides Savitzky-Golay filtering and similarity operations.
    /// </summary>
    internal static class MathHelpers
    {
        private const double SingularityThreshold = 1e-10;

        /// <summary>
        /// Apply Savitzky-Golay filter to smooth data or compute derivatives.
        /// </summary>
        /// <param name="data">Input data array.</param>
        /// <param name="windowLength">Length of the filter window (must be odd and > polyOrder).</param>
        /// <param name="polyOrder">Order of the polynomial used for fitting.</param>
        /// <param name="derivative">Derivative order (0=smoothing, 1=first derivative, 2=second derivative).</param>
        /// <returns>Filtered data array.</returns>
        public static double[] SavitzkyGolayFilter(ReadOnlySpan<double> data, int windowLength = 5, int polyOrder = 3, int derivative = 0)
        {
            if (windowLength % 2 == 0 || windowLength <= polyOrder)
            {
                throw new ArgumentException("Window length must be odd and greater than polynomial order.");
            }

            var coeffs = ComputeSavitzkyGolayCoefficients(windowLength, polyOrder, derivative);
            var result = new double[data.Length];
            ApplyConvolution(data, coeffs, result);
            return result;
        }

        /// <summary>
        /// Find local minima using Savitzky-Golay derivatives.
        /// </summary>
        /// <param name="data">Input data array.</param>
        /// <param name="windowSize">Savitzky-Golay window size.</param>
        /// <param name="polyOrder">Polynomial order for fitting.</param>
        /// <param name="tolerance">Tolerance for considering derivative as zero.</param>
        /// <returns>Tuple of indices and values of local minima.</returns>
        public static (int[] Indices, double[] Values) FindLocalMinimaInterpolated(
            ReadOnlySpan<double> data,
            int windowSize = 11,
            int polyOrder = 2,
            double tolerance = 0.2)
        {
            var firstDerivative = SavitzkyGolayFilter(data, windowSize, polyOrder, 1);
            var secondDerivative = SavitzkyGolayFilter(data, windowSize, polyOrder, 2);

            // Count minima
            int count = 0;
            for (int i = 0; i < data.Length; i++)
            {
                if (System.Math.Abs(firstDerivative[i]) < tolerance && secondDerivative[i] > 0)
                {
                    count++;
                }
            }

            // Collect minima
            var indices = new int[count];
            var values = new double[count];
            int index = 0;

            for (int i = 0; i < data.Length; i++)
            {
                if (System.Math.Abs(firstDerivative[i]) < tolerance && secondDerivative[i] > 0)
                {
                    indices[index] = i;
                    values[index] = data[i];
                    index++;
                }
            }

            return (indices, values);
        }

        /// <summary>
        /// Compute windowed cross-similarity for semantic chunking.
        /// </summary>
        /// <param name="embeddings">2D array of embeddings (n_sentences x embedding_dim).</param>
        /// <param name="windowSize">Size of sliding window (must be odd and >= 3).</param>
        /// <returns>Average similarities for each position.</returns>
        public static double[] WindowedCrossSimilarity(ReadOnlySpan<double> embeddings, int embeddingDim, int windowSize = 3)
        {
            if (windowSize % 2 == 0 || windowSize < 3)
            {
                throw new ArgumentException("Window size must be odd and >= 3.");
            }

            int n = embeddings.Length / embeddingDim;
            var result = new double[n - 1];
            int halfWindow = windowSize / 2;

            for (int i = 0; i < n - 1; i++)
            {
                int start = System.Math.Max(0, i - halfWindow);
                int end = System.Math.Min(n, i + halfWindow + 2);

                double similaritySum = 0;
                int count = 0;

                for (int j = start; j < end - 1; j++)
                {
                    double similarity = CosineSimilarity(
                        embeddings.Slice(j * embeddingDim, embeddingDim),
                        embeddings.Slice((j + 1) * embeddingDim, embeddingDim));

                    if (!double.IsNaN(similarity))
                    {
                        similaritySum += similarity;
                        count++;
                    }
                }

                result[i] = count > 0 ? similaritySum / count : 0;
            }

            return result;
        }

        /// <summary>
        /// Filter split indices by percentile threshold and minimum distance.
        /// </summary>
        public static (int[] Indices, double[] Values) FilterSplitIndices(
            ReadOnlySpan<int> indices,
            ReadOnlySpan<double> values,
            double threshold = 0.5,
            int minDistance = 2)
        {
            if (indices.Length == 0)
            {
                return (Array.Empty<int>(), Array.Empty<double>());
            }

            double thresholdValue = Percentile(values, threshold);

            // Count valid splits
            int count = 0;
            int lastIndex = -minDistance - 1;

            for (int i = 0; i < indices.Length; i++)
            {
                if (values[i] <= thresholdValue && indices[i] - lastIndex >= minDistance)
                {
                    count++;
                    lastIndex = indices[i];
                }
            }

            // Collect valid splits
            var filteredIndices = new int[count];
            var filteredValues = new double[count];
            int resultIndex = 0;
            lastIndex = -minDistance - 1;

            for (int i = 0; i < indices.Length; i++)
            {
                if (values[i] <= thresholdValue && indices[i] - lastIndex >= minDistance)
                {
                    filteredIndices[resultIndex] = indices[i];
                    filteredValues[resultIndex] = values[i];
                    resultIndex++;
                    lastIndex = indices[i];
                }
            }

            return (filteredIndices, filteredValues);
        }

        #region Private Helper Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CosineSimilarity(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
        {
            double dot = 0, norm1 = 0, norm2 = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                norm1 += a[i] * a[i];
                norm2 += b[i] * b[i];
            }

            return norm1 > 0 && norm2 > 0 ? dot / (System.Math.Sqrt(norm1) * System.Math.Sqrt(norm2)) : 0;
        }

        private static double[] ComputeSavitzkyGolayCoefficients(int windowSize, int polyOrder, int derivative)
        {
            int halfWindow = (windowSize - 1) / 2;
            int matrixCols = polyOrder + 1;

            // Build Vandermonde matrix
            var A = new double[windowSize * matrixCols];
            for (int i = 0; i < windowSize; i++)
            {
                double val = i - halfWindow;
                for (int j = 0; j < matrixCols; j++)
                {
                    A[i * matrixCols + j] = System.Math.Pow(val, j);
                }
            }

            // Compute A^T
            var AT = new double[matrixCols * windowSize];
            MatrixTranspose(A, AT, windowSize, matrixCols);

            // Compute A^T * A
            var ATA = new double[matrixCols * matrixCols];
            MatrixMultiply(AT, A, ATA, matrixCols, windowSize, matrixCols);

            // Invert (A^T * A)
            var ATAInv = new double[matrixCols * matrixCols];
            if (!MatrixInverse(ATA, ATAInv, matrixCols))
            {
                throw new InvalidOperationException("Matrix inversion failed - singular matrix.");
            }

            // Compute coefficients for the requested derivative
            double factorial = 1.0;
            for (int i = 1; i <= derivative; i++)
            {
                factorial *= i;
            }

            var coeffs = new double[windowSize];
            for (int i = 0; i < windowSize; i++)
            {
                double sum = 0;
                for (int k = 0; k < matrixCols; k++)
                {
                    sum += ATAInv[derivative * matrixCols + k] * A[i * matrixCols + k];
                }
                coeffs[i] = factorial * sum;
            }

            return coeffs;
        }

        private static void ApplyConvolution(ReadOnlySpan<double> data, double[] kernel, Span<double> output)
        {
            int half = kernel.Length / 2;
            int n = data.Length;

            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int j = 0; j < kernel.Length; j++)
                {
                    int idx = i - half + j;

                    // Handle boundaries with reflection
                    if (idx < 0)
                    {
                        idx = -idx;
                    }
                    else if (idx >= n)
                    {
                        idx = 2 * n - idx - 2;
                    }

                    sum += data[idx] * kernel[j];
                }
                output[i] = sum;
            }
        }

        private static void MatrixMultiply(double[] A, double[] B, double[] C, int m, int n, int p)
        {
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < p; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < n; k++)
                    {
                        sum += A[i * n + k] * B[k * p + j];
                    }
                    C[i * p + j] = sum;
                }
            }
        }

        private static void MatrixTranspose(double[] A, double[] AT, int m, int n)
        {
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    AT[j * m + i] = A[i * n + j];
                }
            }
        }

        private static bool MatrixInverse(double[] A, double[] AInv, int n)
        {
            // Create identity matrix in AInv
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    AInv[i * n + j] = i == j ? 1.0 : 0.0;
                }
            }

            // Make a copy of A to work with
            var work = new double[n * n];
            Array.Copy(A, work, n * n);

            // Gaussian elimination with partial pivoting
            for (int i = 0; i < n; i++)
            {
                // Find pivot
                int maxRow = i;
                double maxVal = System.Math.Abs(work[i * n + i]);

                for (int k = i + 1; k < n; k++)
                {
                    double absVal = System.Math.Abs(work[k * n + i]);
                    if (absVal > maxVal)
                    {
                        maxVal = absVal;
                        maxRow = k;
                    }
                }

                // Swap rows if needed
                if (maxRow != i)
                {
                    for (int j = 0; j < n; j++)
                    {
                        (work[i * n + j], work[maxRow * n + j]) = (work[maxRow * n + j], work[i * n + j]);
                        (AInv[i * n + j], AInv[maxRow * n + j]) = (AInv[maxRow * n + j], AInv[i * n + j]);
                    }
                }

                // Check for singular matrix
                double pivot = work[i * n + i];
                if (System.Math.Abs(pivot) < SingularityThreshold)
                {
                    return false;
                }

                // Normalize pivot row
                for (int j = 0; j < n; j++)
                {
                    work[i * n + j] /= pivot;
                    AInv[i * n + j] /= pivot;
                }

                // Eliminate column
                for (int k = 0; k < n; k++)
                {
                    if (k != i)
                    {
                        double factor = work[k * n + i];
                        for (int j = 0; j < n; j++)
                        {
                            work[k * n + j] -= factor * work[i * n + j];
                            AInv[k * n + j] -= factor * AInv[i * n + j];
                        }
                    }
                }
            }

            return true;
        }

        private static double Percentile(ReadOnlySpan<double> data, double p)
        {
            if (data.Length == 0)
            {
                return 0;
            }

            // Create sorted copy
            var sorted = data.ToArray();
            Array.Sort(sorted);

            double idx = p * (sorted.Length - 1);
            int lower = (int)idx;
            int upper = lower < sorted.Length - 1 ? lower + 1 : lower;
            double weight = idx - lower;

            return sorted[lower] * (1 - weight) + sorted[upper] * weight;
        }

        #endregion
    }
}
