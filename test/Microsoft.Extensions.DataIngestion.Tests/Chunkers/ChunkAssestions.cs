// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    public static class ChunkAssertions
    {
        public static void ContentEquals(string expected, DocumentChunk chunk)
        {
            Assert.Equal(expected.ReplaceLineEndings(), chunk.Content.Trim());
        }

        public static void ContextEquals(string expected, DocumentChunk chunk)
        {
            Assert.Equal(expected, chunk.Context?.Trim() ?? string.Empty);
        }
    }
}
