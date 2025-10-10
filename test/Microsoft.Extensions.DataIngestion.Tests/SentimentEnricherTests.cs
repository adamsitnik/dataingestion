// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class SentimentEnricherTests : ChatClientTestBase
{
    private static readonly IngestionDocument document = new("test");

    [Fact]
    public async Task CanProvideSentiment()
    {
        SentimentEnricher sut = new(ChatClient);
        var input = CreateChunks().ToAsyncEnumerable();

        var chunks = await sut.ProcessAsync(input).ToListAsync();

        Assert.Equal(4, chunks.Count);

        Assert.Equal("Positive", chunks[0].Metadata[SentimentEnricher.MetadataKey]);
        Assert.Equal("Negative", chunks[1].Metadata[SentimentEnricher.MetadataKey]);
        Assert.Equal("Neutral", chunks[2].Metadata[SentimentEnricher.MetadataKey]);
        Assert.Equal("Unknown", chunks[3].Metadata[SentimentEnricher.MetadataKey]);
    }

    private static List<IngestionChunk> CreateChunks() =>
    [
        new("I love programming! It's so much fun and rewarding.", document),
        new("I hate bugs. They are so frustrating and time-consuming.", document),
        new("The weather is okay, not too bad but not great either.", document),
        new("I hate you. I am sorry, I actually don't. I am not sure myself what my feelings are.", document)
    ];
}
