// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class DocumentTests
{
    private readonly string[,] rows = { { "header" }, { "row1" }, { "row2" } };

    [Fact]
    public void EnumeratorFlattensTheStructureAndPreservesOrder()
    {
        Document doc = new("withSubSections")
        {
            Markdown = "same",
        };
        doc.Sections.Add(new DocumentSection("first section")
        {
            Elements =
            {
                new DocumentHeader("header"),
                new DocumentParagraph("paragraph"),
                new DocumentTable("table", rows),
                new DocumentSection("nested section")
                {
                    Elements =
                    {
                        new DocumentHeader("nested header"),
                        new DocumentParagraph("nested paragraph")
                    }
                }
            }
        });
        doc.Sections.Add(new DocumentSection("second section")
        {
            Elements =
            {
                new DocumentHeader("header 2"),
                new DocumentParagraph("paragraph 2")
            }
        });

        DocumentElement[] flatElements = doc.ToArray();

        Assert.IsType<DocumentHeader>(flatElements[0]);
        Assert.Equal("header", flatElements[0].Markdown);
        Assert.IsType<DocumentParagraph>(flatElements[1]);
        Assert.Equal("paragraph", flatElements[1].Markdown);
        Assert.IsType<DocumentTable>(flatElements[2]);
        Assert.Equal("table", flatElements[2].Markdown);
        Assert.IsType<DocumentHeader>(flatElements[3]);
        Assert.Equal("nested header", flatElements[3].Markdown);
        Assert.IsType<DocumentParagraph>(flatElements[4]);
        Assert.Equal("nested paragraph", flatElements[4].Markdown);
        Assert.IsType<DocumentHeader>(flatElements[5]);
        Assert.Equal("header 2", flatElements[5].Markdown);
        Assert.IsType<DocumentParagraph>(flatElements[6]);
        Assert.Equal("paragraph 2", flatElements[6].Markdown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyParagraphDocumentCantBeCreated(string? input)
        => Assert.Throws<ArgumentNullException>(() => new DocumentParagraph(input!));

    [Fact]
    public void EmptyMarkdownIsNotBeingCached()
    {
        Document doc = new("sut");
        Assert.Empty(doc.Markdown);

        doc.Sections.Add(new DocumentSection("section markdown"));
        Assert.Equal(doc.Markdown, doc.Sections[0].Markdown);
    }
}
