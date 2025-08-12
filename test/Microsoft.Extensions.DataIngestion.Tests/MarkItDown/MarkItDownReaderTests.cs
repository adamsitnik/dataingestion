// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class MarkItDownReaderTests : DocumentReaderConformanceTests
{
    protected override DocumentReader CreateDocumentReader(bool extractImages = false) => new MarkItDownReader();

    protected override void SimpleAsserts(Document document, string source)
    {
        Assert.NotNull(document);
        Assert.NotEmpty(document.Sections);
        Assert.NotEmpty(document.Markdown);

        var elements = Flatten(document).ToArray();
        
        bool isPdf = source.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        if (!isPdf)
        {
            // MarkItDown does a bad job of recognizing Headers and Tables even for simple PDF files.
            Assert.Contains(elements, element => element is DocumentHeader);
            Assert.Contains(elements, element => element is DocumentTable);
        }

        Assert.Contains(elements, element => element is DocumentParagraph);
        Assert.All(elements, element => Assert.NotEmpty(element.Markdown));
    }

    public override Task SupportsImages(string filePath)
    {
        // MarkItDown currently does not support images (the original purpose of the library was to support text-only LLMs).
        // Source: https://github.com/microsoft/markitdown/issues/56#issuecomment-2546357264

        return Task.CompletedTask;
    }
}
