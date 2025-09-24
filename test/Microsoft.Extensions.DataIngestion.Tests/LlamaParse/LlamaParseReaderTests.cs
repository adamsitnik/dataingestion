// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using LlamaParse;
using System;
using System.Net.Http;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class LlamaParseReaderTests : DocumentReaderConformanceTests
{
    protected override DocumentReader CreateDocumentReader(bool extractImages = false)
    {
        string key = Environment.GetEnvironmentVariable("LLAMACLOUD_API_KEY")!;

        LlamaParse.Configuration configuration = new()
        {
            ApiKey = key ?? throw new InvalidOperationException("LLAMACLOUD_API_KEY environment variable is not set."),
            ItemsToExtract = ItemType.Table,
        };

        if (extractImages)
        {
            configuration.ItemsToExtract |= ItemType.Image;
        }

        return new LlamaParseReader(new LlamaParseClient(new HttpClient(), configuration));
    }
}
