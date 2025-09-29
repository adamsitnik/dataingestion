// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class RemovalProcessorTests
{
    [Fact]
    public async Task WhenFooterIsDeletedTheMarkdownIsUpdatedAndMetadataIsPreserved()
    {
        const string ExpectedMarkdown = "This is a paragraph.";
        DocumentParagraph paragraph = new(ExpectedMarkdown)
        {
            Metadata = { ["key"] = "value" }
        };

        Document document = new("some");
        DocumentSection section = new();
        section.Elements.Add(paragraph);
        section.Elements.Add(new DocumentFooter("This is a footer that should be removed."));
        document.Sections.Add(section);

        Document updated = await RemovalProcessor.Footers.ProcessAsync(document);

        Assert.Same(document.Identifier, updated.Identifier);
        Assert.Single(updated.Sections);
        Assert.Single(updated.Sections[0].Elements);
        DocumentParagraph updatedParagraph = Assert.IsType<DocumentParagraph>(updated.Sections[0].Elements[0]);
        Assert.Equal(paragraph.Markdown, updatedParagraph.Markdown);
        Assert.Equal(paragraph.Metadata, updatedParagraph.Metadata);
        Assert.Equal(ExpectedMarkdown, updated.Sections[0].Markdown);
        Assert.Equal(ExpectedMarkdown, updated.Markdown);
    }

    [Fact]
    public async Task EmptySectionsCanBeRemovedRecursively()
    {
        const string ExpectedMarkdown = "This is a paragraph.";

        Document document = new("some")
        {
            Sections =
            {
                new DocumentSection()
                {
                    Elements =
                    {
                        new DocumentSection()
                        {
                            Elements =
                            {
                                new DocumentSection()
                            }
                        }
                    }
                },
                new DocumentSection()
                {
                    Elements =
                    {
                        new DocumentParagraph(ExpectedMarkdown)
                    }
                }
            }
        };

        Document updated = await RemovalProcessor.EmptySections.ProcessAsync(document);

        Assert.Same(document.Identifier, updated.Identifier);
        Assert.Single(updated.Sections);
        Assert.Single(updated.Sections[0].Elements);
        DocumentParagraph updatedParagraph = Assert.IsType<DocumentParagraph>(updated.Sections[0].Elements[0]);
        Assert.Equal(ExpectedMarkdown, updatedParagraph.Markdown);
        Assert.Equal(ExpectedMarkdown, updated.Sections[0].Markdown);
        Assert.Equal(ExpectedMarkdown, updated.Markdown);
    }
}
