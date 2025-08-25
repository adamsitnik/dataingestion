// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests.Chunkers
{
    /// <summary>
    /// Extension methods for making assertions on <see cref="Chunk"/> objects in tests.
    /// </summary>
    public static class ChunkAssertions
    {
        /// <summary>
        /// Asserts that the chunk's content (trimmed) equals the expected string.
        /// </summary>
        /// <param name="assert">The Assert instance (not used but required for extension method syntax)</param>
        /// <param name="expected">The expected string content</param>
        /// <param name="chunk">The chunk to test</param>
        public static void Equal(this Assert assert, string expected, Chunk chunk)
        {
            Assert.Equal(expected, chunk.Content.Trim());
        }
    }
}
