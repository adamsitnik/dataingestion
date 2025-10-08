// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Chunkers.Tests;

public class ChunkerOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeSetCorrectly()
    {
        IngestionChunkerOptions options = new();

        Assert.Equal(2000, options.MaxTokensPerChunk);
        Assert.Equal(500, options.OverlapTokens);
    }

    [Fact]
    public void DefaultOverlapTokensIsZeroForSmallMaxTokensPerChunk()
    {
        IngestionChunkerOptions options = new() { MaxTokensPerChunk = 100 };

        Assert.Equal(100, options.MaxTokensPerChunk);
        Assert.Equal(0, options.OverlapTokens);
    }

    [Fact]
    public void Properties_ShouldThrow_OnZeroOrNegative()
    {
        IngestionChunkerOptions options = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxTokensPerChunk = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxTokensPerChunk = -1);

        // 0 is allowed for OverlapTokens
        Assert.Throws<ArgumentOutOfRangeException>(() => options.OverlapTokens = -1);
    }

    [Fact]
    public void OverlapTokens_ShouldThrow_WhenGreaterOrEqualThanMaxTokens()
    {
        IngestionChunkerOptions options = new() { MaxTokensPerChunk = 1000 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.OverlapTokens = 1000);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.OverlapTokens = 1500);
    }

    [Fact]
    public void MaxTokensPerChunk_ShouldThrow_WhenLessOrEqualThanOverlapTokens()
    {
        IngestionChunkerOptions options = new() { OverlapTokens = 10 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxTokensPerChunk = 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxTokensPerChunk = 5);
    }
}
