// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class DocumentTests
{
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
                new DocumentTable("table"),
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

        Assert.IsType<DocumentSection>(flatElements[0]);
        Assert.Equal("first section", flatElements[0].Markdown);
        Assert.IsType<DocumentHeader>(flatElements[1]);
        Assert.Equal("header", flatElements[1].Markdown);
        Assert.IsType<DocumentParagraph>(flatElements[2]);
        Assert.Equal("paragraph", flatElements[2].Markdown);
        Assert.IsType<DocumentTable>(flatElements[3]);
        Assert.Equal("table", flatElements[3].Markdown);
        Assert.IsType<DocumentSection>(flatElements[4]);
        Assert.Equal("nested section", flatElements[4].Markdown);
        Assert.IsType<DocumentHeader>(flatElements[5]);
        Assert.Equal("nested header", flatElements[5].Markdown);
        Assert.IsType<DocumentParagraph>(flatElements[6]);
        Assert.Equal("nested paragraph", flatElements[6].Markdown);
        Assert.IsType<DocumentSection>(flatElements[7]);
        Assert.Equal("second section", flatElements[7].Markdown);
        Assert.IsType<DocumentHeader>(flatElements[8]);
        Assert.Equal("header 2", flatElements[8].Markdown);
        Assert.IsType<DocumentParagraph>(flatElements[9]);
        Assert.Equal("paragraph 2", flatElements[9].Markdown);
    }
}
