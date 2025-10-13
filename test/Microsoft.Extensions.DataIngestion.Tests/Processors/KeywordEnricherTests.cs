// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Processors.Tests;

public class KeywordEnricherTests : ChatClientTestBase
{
    private static readonly IngestionDocument document = new("test");

    [Fact]
    public async Task CanExtractKeywordsWithoutPredefinedList()
    {
        KeywordEnricher sut = new(ChatClient, predefinedKeywords: null, confidenceThreshold: 0.5);
        var chunks = CreateChunks().ToAsyncEnumerable();

        IReadOnlyList<IngestionChunk<string>> got = await sut.ProcessAsync(chunks).ToListAsync();

        IngestionChunk<string> chunk = Assert.Single(got);
        Assert.NotEmpty((string[])chunk.Metadata[KeywordEnricher.MetadataKey]!);
        Assert.Contains((string[])chunk.Metadata[KeywordEnricher.MetadataKey]!, keyword => keyword.Contains("artificial intelligence") || keyword.Contains("AI"));
    }

    [Fact]
    public async Task CanExtractKeywordsWithPredefinedList()
    {
        KeywordEnricher sut = new(ChatClient, predefinedKeywords: ["AI", ".NET", "Animals", "Rabbits"], confidenceThreshold: 0.6);
        var chunks = CreateChunks().ToAsyncEnumerable();

        IReadOnlyList<IngestionChunk<string>> got = await sut.ProcessAsync(chunks).ToListAsync();

        IngestionChunk<string> chunk = Assert.Single(got);
        Assert.NotEmpty((string[])chunk.Metadata[KeywordEnricher.MetadataKey]!);
        Assert.Contains("AI", (string[])chunk.Metadata[KeywordEnricher.MetadataKey]!);
        Assert.Contains(".NET", (string[])chunk.Metadata[KeywordEnricher.MetadataKey]!);
        Assert.DoesNotContain("Animals", (string[])chunk.Metadata[KeywordEnricher.MetadataKey]!);
        Assert.DoesNotContain("Rabbits", (string[])chunk.Metadata[KeywordEnricher.MetadataKey]!);
    }

    private static List<IngestionChunk<string>> CreateChunks() =>
    [
        new(".NET developers need to integrate and interact with a growing variety of artificial intelligence (AI) services in their apps. The Microsoft.Extensions.AI libraries provide a unified approach for representing generative AI components, and enable seamless integration and interoperability with various AI services.", document)
    ];
}
