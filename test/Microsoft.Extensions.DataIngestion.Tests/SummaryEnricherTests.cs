// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class SummaryEnricherTests : ChatClientTestBase
{
    private static readonly IngestionDocument document = new("test");

    [Fact]
    public async Task CanProvideSummary()
    {
        SummaryEnricher sut = new(ChatClient);

        var chunks = await sut.ProcessAsync(CreateChunks()).ToListAsync();

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.NotEmpty((string)chunk.Metadata[SummaryEnricher.MetadataKey]!));
    }

    private static async IAsyncEnumerable<IngestionChunk> CreateChunks()
    {
        yield return new("I love programming! It's so much fun and rewarding.", document);
        yield return new("I hate bugs. They are so frustrating and time-consuming.", document);
    }
}
