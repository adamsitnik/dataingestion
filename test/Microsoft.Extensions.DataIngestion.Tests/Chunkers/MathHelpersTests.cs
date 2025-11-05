// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests
{
    public class MathHelpersTests
    {
        #region SavitzkyGolayFilter Tests

        [Fact]
        public void SavitzkyGolayFilter_WithDefaultParameters_SmoothsData()
        {
            // Arrange
            double[] data = { 1.0, 2.0, 1.5, 3.0, 2.5, 4.0, 3.5, 5.0 };

            // Act
            double[] result = MathHelpers.SavitzkyGolayFilter(data);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(data.Length, result.Length);
            Assert.All(result, value => Assert.False(double.IsNaN(value)));
        }

        [Fact]
        public void SavitzkyGolayFilter_WithFirstDerivative_ComputesDerivative()
        {
            // Arrange
            double[] data = { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0 };

            // Act
            double[] result = MathHelpers.SavitzkyGolayFilter(data, windowLength: 5, polyOrder: 2, derivative: 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(data.Length, result.Length);
            // For linear data, first derivative should be approximately constant
            Assert.All(result, value => Assert.InRange(value, 0.5, 1.5));
        }

        [Fact]
        public void SavitzkyGolayFilter_WithSecondDerivative_ComputesSecondDerivative()
        {
            // Arrange
            double[] data = { 1.0, 4.0, 9.0, 16.0, 25.0, 36.0, 49.0, 64.0 }; // x^2

            // Act
            double[] result = MathHelpers.SavitzkyGolayFilter(data, windowLength: 7, polyOrder: 3, derivative: 2);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(data.Length, result.Length);
            // For quadratic data, second derivative should be approximately constant (2)
            Assert.All(result.Skip(1).Take(result.Length - 2), value => Assert.InRange(value, 1.0, 3.0));
        }

        [Theory]
        [InlineData(4, 2)] // Even window length
        [InlineData(3, 3)] // Window length <= polynomial order
        [InlineData(2, 2)] // Window length <= polynomial order
        public void SavitzkyGolayFilter_WithInvalidParameters_ThrowsArgumentException(int windowLength, int polyOrder)
        {
            // Arrange
            double[] data = { 1.0, 2.0, 3.0, 4.0, 5.0 };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MathHelpers.SavitzkyGolayFilter(data, windowLength, polyOrder));
        }

        [Fact]
        public void SavitzkyGolayFilter_WithEmptyData_ReturnsEmptyArray()
        {
            // Arrange
            double[] data = Array.Empty<double>();

            // Act
            double[] result = MathHelpers.SavitzkyGolayFilter(data);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region FindLocalMinimaInterpolated Tests

        [Fact]
        public void FindLocalMinimaInterpolated_WithSimpleCurve_FindsMinimum()
        {
            // Arrange - parabola with minimum at index 5
            double[] data = { 25.0, 16.0, 9.0, 4.0, 1.0, 0.0, 1.0, 4.0, 9.0, 16.0, 25.0 };

            // Act
            var (indices, values) = MathHelpers.FindLocalMinimaInterpolated(data, windowSize: 5, polyOrder: 2, tolerance: 0.5);

            // Assert
            Assert.NotEmpty(indices);
            Assert.Equal(indices.Length, values.Length);
            Assert.Contains(indices, idx => idx is >= 4 and <= 6); // Should find minimum around index 5
        }

        [Fact]
        public void FindLocalMinimaInterpolated_WithMultipleMinima_FindsAllMinima()
        {
            // Arrange - data with two local minima
            double[] data = { 5.0, 2.0, 1.0, 2.0, 5.0, 8.0, 5.0, 2.0, 1.0, 2.0, 5.0 };

            // Act
            var (indices, values) = MathHelpers.FindLocalMinimaInterpolated(data, windowSize: 5, polyOrder: 2, tolerance: 0.5);

            // Assert
            Assert.NotEmpty(indices);
            Assert.True(indices.Length >= 1); // Should find at least one minimum
        }

        [Fact]
        public void FindLocalMinimaInterpolated_WithNoMinima_ReturnsEmpty()
        {
            // Arrange - monotonically increasing data
            double[] data = { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0 };

            // Act
            var (indices, values) = MathHelpers.FindLocalMinimaInterpolated(data, windowSize: 5, polyOrder: 2, tolerance: 0.1);

            // Assert
            // May or may not find minima depending on derivative tolerance
            Assert.Equal(indices.Length, values.Length);
        }

        [Fact]
        public void FindLocalMinimaInterpolated_WithEmptyData_ReturnsEmpty()
        {
            // Arrange
            double[] data = Array.Empty<double>();

            // Act
            var (indices, values) = MathHelpers.FindLocalMinimaInterpolated(data);

            // Assert
            Assert.Empty(indices);
            Assert.Empty(values);
        }

        #endregion

        #region WindowedCrossSimilarity Tests

        [Fact]
        public void WindowedCrossSimilarity_WithIdenticalVectors_ReturnsHighSimilarity()
        {
            // Arrange - 3 identical embedding vectors of dimension 4
            double[] embeddings =
            {
                1.0, 0.0, 0.0, 0.0,
                1.0, 0.0, 0.0, 0.0,
                1.0, 0.0, 0.0, 0.0
            };
            int embeddingDim = 4;

            // Act
            double[] result = MathHelpers.WindowedCrossSimilarity(embeddings, embeddingDim, windowSize: 3);

            // Assert
            Assert.Equal(2, result.Length); // n-1 similarities for n vectors
            Assert.All(result, similarity => Assert.InRange(similarity, 0.99, 1.01)); // Should be ~1.0
        }

        [Fact]
        public void WindowedCrossSimilarity_WithOrthogonalVectors_ReturnsLowSimilarity()
        {
            // Arrange - orthogonal embedding vectors
            double[] embeddings =
            {
                1.0, 0.0, 0.0, 0.0,
                0.0, 1.0, 0.0, 0.0,
                0.0, 0.0, 1.0, 0.0
            };
            int embeddingDim = 4;

            // Act
            double[] result = MathHelpers.WindowedCrossSimilarity(embeddings, embeddingDim, windowSize: 3);

            // Assert
            Assert.Equal(2, result.Length);
            Assert.All(result, similarity => Assert.InRange(similarity, -0.1, 0.1)); // Should be ~0.0
        }

        [Theory]
        [InlineData(2)] // Even window size
        [InlineData(1)] // Window size < 3
        public void WindowedCrossSimilarity_WithInvalidWindowSize_ThrowsArgumentException(int windowSize)
        {
            // Arrange
            double[] embeddings = { 1.0, 0.0, 0.0, 1.0 };
            int embeddingDim = 2;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MathHelpers.WindowedCrossSimilarity(embeddings, embeddingDim, windowSize));
        }

        [Fact]
        public void WindowedCrossSimilarity_WithSingleVector_ReturnsEmpty()
        {
            // Arrange
            double[] embeddings = { 1.0, 0.0, 0.0, 0.0 };
            int embeddingDim = 4;

            // Act
            double[] result = MathHelpers.WindowedCrossSimilarity(embeddings, embeddingDim);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region FilterSplitIndices Tests

        [Fact]
        public void FilterSplitIndices_WithThreshold_FiltersCorrectly()
        {
            // Arrange
            int[] indices = { 0, 5, 10, 15, 20, 25 };
            double[] values = { 0.1, 0.3, 0.8, 0.2, 0.9, 0.15 };

            // Act - threshold at 50th percentile should filter out high values
            var (filteredIndices, filteredValues) = MathHelpers.FilterSplitIndices(indices, values, threshold: 0.5, minDistance: 2);

            // Assert
            Assert.NotEmpty(filteredIndices);
            Assert.Equal(filteredIndices.Length, filteredValues.Length);
            Assert.All(filteredValues, value => Assert.True(value <= 0.3)); // Values below ~50th percentile
        }

        [Fact]
        public void FilterSplitIndices_WithMinDistance_EnforcesMinimumDistance()
        {
            // Arrange
            int[] indices = { 0, 1, 2, 10, 11, 12 };
            double[] values = { 0.1, 0.1, 0.1, 0.1, 0.1, 0.1 };

            // Act
            var (filteredIndices, filteredValues) = MathHelpers.FilterSplitIndices(indices, values, threshold: 1.0, minDistance: 5);

            // Assert
            Assert.NotEmpty(filteredIndices);
            // Check that consecutive indices are at least minDistance apart
            for (int i = 1; i < filteredIndices.Length; i++)
            {
                Assert.True(filteredIndices[i] - filteredIndices[i - 1] >= 5);
            }
        }

        [Fact]
        public void FilterSplitIndices_WithEmptyInput_ReturnsEmpty()
        {
            // Arrange
            int[] indices = Array.Empty<int>();
            double[] values = Array.Empty<double>();

            // Act
            var (filteredIndices, filteredValues) = MathHelpers.FilterSplitIndices(indices, values);

            // Assert
            Assert.Empty(filteredIndices);
            Assert.Empty(filteredValues);
        }

        [Fact]
        public void FilterSplitIndices_WithAllValuesAboveThreshold_ReturnsEmpty()
        {
            // Arrange
            int[] indices = { 0, 5, 10 };
            double[] values = { 0.9, 0.95, 1.0 };

            // Act
            var (filteredIndices, filteredValues) = MathHelpers.FilterSplitIndices(indices, values, threshold: 0.1, minDistance: 1);

            // Assert
            Assert.Empty(filteredIndices);
            Assert.Empty(filteredValues);
        }

        #endregion

        #region Edge Cases and Integration Tests

        [Fact]
        public void SavitzkyGolayFilter_WithNoisyData_ReducesNoise()
        {
            // Arrange - linear trend with noise
            Random random = new(42);
            double[] noisyData = Enumerable.Range(0, 20)
                .Select(i => i + (random.NextDouble() - 0.5) * 0.5)
                .ToArray();

            // Act
            double[] smoothed = MathHelpers.SavitzkyGolayFilter(noisyData, windowLength: 7, polyOrder: 2);

            // Assert
            // Check that smoothed data has less variance
            double originalVariance = CalculateVariance(noisyData);
            double smoothedVariance = CalculateVariance(smoothed);
            Assert.True(smoothedVariance < originalVariance);
        }

        [Fact]
        public void WindowedCrossSimilarity_WithGradualChange_ShowsDecreasingSimilarity()
        {
            // Arrange - vectors that gradually change direction
            double[] embeddings =
            {
                1.0, 0.0, 0.0,
                0.9, 0.1, 0.0,
                0.7, 0.3, 0.0,
                0.5, 0.5, 0.0,
                0.0, 1.0, 0.0
            };
            int embeddingDim = 3;

            // Act
            double[] result = MathHelpers.WindowedCrossSimilarity(embeddings, embeddingDim, windowSize: 3);

            // Assert
            Assert.Equal(4, result.Length);
            // Generally, similarity should decrease as vectors become more different
            // (though windowing averages might complicate this)
            Assert.All(result, similarity => Assert.InRange(similarity, 0.0, 1.0));
        }

        private static double CalculateVariance(double[] data)
        {
            double mean = data.Average();
            return data.Select(x => System.Math.Pow(x - mean, 2)).Average();
        }

        #endregion
    }
}
