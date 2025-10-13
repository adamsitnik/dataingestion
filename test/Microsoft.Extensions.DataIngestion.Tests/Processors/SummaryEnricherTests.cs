// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Processors.Tests;

public class SummaryEnricherTests : ChatClientTestBase
{
    private static readonly IngestionDocument document = new("test");

    [Fact]
    public async Task CanProvideSummary()
    {
        SummaryEnricher sut = new(ChatClient);
        var input = CreateChunks().ToAsyncEnumerable();
        var chunks = await sut.ProcessAsync(input).ToListAsync();

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.NotEmpty((string)chunk.Metadata[SummaryEnricher.MetadataKey]!));
    }

    private static List<IngestionChunk<string>> CreateChunks() =>
    [
        new("I love programming! It's so much fun and rewarding.", document),
        new("I hate bugs. They are so frustrating and time-consuming.", document)
    ];
}
