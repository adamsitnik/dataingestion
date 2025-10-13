// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Processors.Tests;

public class RemovalProcessorTests
{
    [Fact]
    public async Task WhenFooterIsDeletedTheMarkdownIsUpdatedAndMetadataIsPreserved()
    {
        const string ExpectedMarkdown = "This is a paragraph.";
        IngestionDocumentParagraph paragraph = new(ExpectedMarkdown)
        {
            Metadata = { ["key"] = "value" }
        };

        IngestionDocument document = new("some");
        IngestionDocumentSection section = new();
        section.Elements.Add(paragraph);
        section.Elements.Add(new IngestionDocumentFooter("This is a footer that should be removed."));
        document.Sections.Add(section);

        IngestionDocument updated = await RemovalProcessor.Footers.ProcessAsync(document);

        Assert.Same(document.Identifier, updated.Identifier);
        Assert.Single(updated.Sections);
        Assert.Single(updated.Sections[0].Elements);
        IngestionDocumentParagraph updatedParagraph = Assert.IsType<IngestionDocumentParagraph>(updated.Sections[0].Elements[0]);
        Assert.Equal(paragraph.GetMarkdown(), updatedParagraph.GetMarkdown());
        Assert.Equal(paragraph.Metadata, updatedParagraph.Metadata);
        Assert.Equal(ExpectedMarkdown, updated.Sections[0].GetMarkdown());
    }

    [Fact]
    public async Task EmptySectionsCanBeRemovedRecursively()
    {
        const string ExpectedMarkdown = "This is a paragraph.";

        IngestionDocument document = new("some")
        {
            Sections =
            {
                new IngestionDocumentSection()
                {
                    Elements =
                    {
                        new IngestionDocumentSection()
                        {
                            Elements =
                            {
                                new IngestionDocumentSection()
                            }
                        }
                    }
                },
                new IngestionDocumentSection()
                {
                    Elements =
                    {
                        new IngestionDocumentParagraph(ExpectedMarkdown)
                    }
                }
            }
        };

        IngestionDocument updated = await RemovalProcessor.EmptySections.ProcessAsync(document);

        Assert.Same(document.Identifier, updated.Identifier);
        Assert.Single(updated.Sections);
        Assert.Single(updated.Sections[0].Elements);
        IngestionDocumentParagraph updatedParagraph = Assert.IsType<IngestionDocumentParagraph>(updated.Sections[0].Elements[0]);
        Assert.Equal(ExpectedMarkdown, updatedParagraph.GetMarkdown());
        Assert.Equal(ExpectedMarkdown, updated.Sections[0].GetMarkdown());
    }
}
