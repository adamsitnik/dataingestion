// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class ClassificationEnricherTests : ChatClientTestBase
{
    private static readonly IngestionDocument document = new("test");

    [Fact]
    public async Task CanClassify()
    {
        ClassificationEnricher sut = new(ChatClient, ["AI", "Animals", "Sports"], fallbackClass: "UFO");

        IReadOnlyList<IngestionChunk<string>> got = await sut.ProcessAsync(CreateChunks().ToAsyncEnumerable()).ToListAsync();

        Assert.Equal(3, got.Count);
        Assert.Equal("AI", got[0].Metadata[ClassificationEnricher.MetadataKey]);
        Assert.Equal("Animals", got[1].Metadata[ClassificationEnricher.MetadataKey]);
        Assert.Equal("UFO", got[2].Metadata[ClassificationEnricher.MetadataKey]);
    }

    private static List<IngestionChunk<string>> CreateChunks() =>
    [
        new(".NET developers need to integrate and interact with a growing variety of artificial intelligence (AI) services in their apps. The Microsoft.Extensions.AI libraries provide a unified approach for representing generative AI components, and enable seamless integration and interoperability with various AI services.", document),
        new ("Rabbits are small mammals in the family Leporidae of the order Lagomorpha (along with the hare and the pika). They are herbivorous animals and are known for their long ears, large hind legs, and short fluffy tails.", document),
        new("This text does not belong to any category.", document),
    ];
}
