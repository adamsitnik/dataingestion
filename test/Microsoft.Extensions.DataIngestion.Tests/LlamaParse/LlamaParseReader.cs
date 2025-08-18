// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using LlamaIndex.Core.Schema;
using LlamaParse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class LlamaParseReader : DocumentReader
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
    private readonly LlamaParseClient _client;

    public LlamaParseReader(LlamaParseClient client) => _client = client;

    public override async Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        FileInfo fileInfo = new(filePath);
        (List<RawResult> rawResults, List<ImageDocument> imageDocuments) = await LoadDataAsync(
            _client.LoadDataRawAsync(
                fileInfo,
                resultType: ResultType.Json,
                cancellationToken: cancellationToken),
            cancellationToken);

        return MapToDocument(rawResults, imageDocuments);
    }

    public override async Task<Document> ReadAsync(Uri source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.IsFile)
        {
            return await ReadAsync(source.LocalPath, cancellationToken);
        }

        HttpClient httpClient = new();
        using HttpResponseMessage response = await httpClient.GetAsync(source, cancellationToken);
        response.EnsureSuccessStatusCode();

        Memory<byte> content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        InMemoryFile inMemoryFile = new(content, source.Segments[^1]);

        (List<RawResult> rawResults, List<ImageDocument> imageDocuments) = await LoadDataAsync(
            _client.LoadDataRawAsync(
                inMemoryFile,
                resultType: ResultType.Json,
                cancellationToken: cancellationToken),
            cancellationToken);

        return MapToDocument(rawResults, imageDocuments);
    }

    private async Task<(List<RawResult> Parsed, List<ImageDocument> Images)> LoadDataAsync(
        IAsyncEnumerable<RawResult> rawResults,
        CancellationToken cancellationToken)
    {
        List<RawResult> parsed = [];
        List<ImageDocument> images = [];

        await foreach (var rawResult in rawResults)
        {
            parsed.Add(rawResult);

            await foreach (var imageDocument in _client.LoadImagesAsync(rawResult, cancellationToken))
            {
                images.Add(imageDocument);
            }
        }

        return (parsed, images);
    }

    private Document MapToDocument(List<RawResult> parsed, List<ImageDocument> images)
    {
        Document result = new();

        foreach (var rawResult in parsed)
        {
            LlamaParseDocument document = JsonSerializer.Deserialize<LlamaParseDocument>(rawResult.Result, _jsonSerializerOptions)!;
            result.Sections.AddRange(Map(document, images));
        }

        return result;
    }

    private IEnumerable<DocumentSection> Map(LlamaParseDocument document, List<ImageDocument> images)
    {
        foreach (var parsedPage in document.Pages)
        {
            DocumentSection page = new()
            {
                Text = parsedPage.Text,
                Markdown = parsedPage.Markdown,
                PageNumber = parsedPage.PageNumber
            };

            if (!string.IsNullOrEmpty(parsedPage.PageHeaderMarkdown))
            {
                page.Elements.Add(new DocumentHeader()
                {
                    // It's weird: Page Header is exposed as Markdown, but not as Text.
                    Text = parsedPage.PageHeaderMarkdown.TrimStart('#'),
                    Markdown = parsedPage.PageHeaderMarkdown
                });
            }

            foreach (var item in parsedPage.Items)
            {
                DocumentElement element = item switch
                {
                    TextPageItem text => new DocumentParagraph()
                    {
                        Text = text.Value,
                    },
                    HeadingPageItem heading => new DocumentHeader()
                    {
                        Text = heading.Value,
                        Level = heading.Level,
                    },
                    TablePageItem table => new DocumentTable()
                    {
                        Markdown = table.Markdown
                    },
                    _ => throw new InvalidOperationException()
                };
                element.PageNumber = parsedPage.PageNumber;
                element.Markdown = item.Markdown;

                page.Elements.Add(element);
            }

            // All we know about the location of images is the page number, so we add them at the end of the page.
            object boxOnce = parsedPage.PageNumber;
            foreach (var image in images.Where(image => image.Image is not null &&
                image.Metadata.TryGetValue("page_number", out object? pageNumber) && pageNumber.Equals(boxOnce)))
            {
                // Based on what we learn from the further steps like using the image with IChatClient,
                // the Base64 string might become the standard instead of BinaryData.
                BinaryData binaryData = BinaryData.FromBytes(Convert.FromBase64String(image.Image!));

                page.Elements.Add(new DocumentImage()
                {
                    Content = binaryData,
                    MediaType = image.ImageMimetype,
                    PageNumber = parsedPage.PageNumber,
                    Text = image.Text ?? string.Empty,
                });
            }

            if (!string.IsNullOrEmpty(parsedPage.PageFooterMarkdown))
            {
                page.Elements.Add(new DocumentFooter()
                {
                    // It's weird: Page Footer is exposed as Markdown, but not as Text.
                    Text = parsedPage.PageFooterMarkdown.TrimStart('#'),
                    Markdown = parsedPage.PageFooterMarkdown
                });
            }

            yield return page;
        }
    }
}
