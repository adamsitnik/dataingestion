// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text.Json;
using Xunit;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class ModelsTests
{
    [Fact]
    public void CanDeserializeComplexLlamaParseResult_NrbfPdf()
    {
        // This is the result of parsing the NRBF spec in PDF format with LlamaParse.
        // https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-NRBF/%5bMS-NRBF%5d-190313.pdf
        string filePath = Path.Combine("TestFiles", "llamaParseNrbfPdf.json");
        LlamaParseDocument? document = ParseDocument(filePath);
        Assert.Equal(49, document.Pages.Count);
    }

    [Fact]
    public void CanDeserializeComplexLlamaParseResult_NrbfDocx()
    {
        // This is the result of parsing the NRBF spec in DOCX format with LlamaParse.
        // https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-NRBF/%5bMS-NRBF%5d-190313.docx
        string filePath = Path.Combine("TestFiles", "llamaParseNrbfDocx.json");
        LlamaParseDocument? document = ParseDocument(filePath);
        Assert.Equal(55, document.Pages.Count);
    }

    [Fact]
    public void CanDeserializeComplexLlamaParseResult_AITrendsTalk()
    {
        // This is the result of parsing the AI Trends presentation in PDF format with LlamaParse.
        // https://www.bondcap.com/report/pdf/Trends_Artificial_Intelligence.pdf
        string filePath = Path.Combine("TestFiles", "llamaParseAiTrendsTalk.json");
        LlamaParseDocument? document = ParseDocument(filePath);
        Assert.Equal(340, document.Pages.Count);
    }

    private static LlamaParseDocument ParseDocument(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        LlamaParseDocument? document = JsonSerializer.Deserialize<LlamaParseDocument>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        Assert.NotNull(document);
        return document;
    }
}
