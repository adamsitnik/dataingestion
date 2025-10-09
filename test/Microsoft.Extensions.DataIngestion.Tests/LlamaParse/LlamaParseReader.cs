// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using LlamaIndex.Core.Schema;
using LlamaParse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests;

public class LlamaParseReader : IngestionDocumentReader
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
    private readonly LlamaParseClient _client;

    public LlamaParseReader(LlamaParseClient client) => _client = client;

    public override async Task<IngestionDocument> ReadAsync(FileInfo source, string identifier, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        else if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        (List<RawResult> rawResults, List<ImageDocument> imageDocuments) = await LoadDataAsync(
            _client.LoadDataRawAsync(
                source,
                resultType: ResultType.Json,
                cancellationToken: cancellationToken),
            cancellationToken);

        return MapToDocument(rawResults, imageDocuments, identifier);
    }

    public override async Task<IngestionDocument> ReadAsync(Stream stream, string identifier, string mediaType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }
        else if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }
        else if (string.IsNullOrEmpty(mediaType))
        {
            throw new ArgumentNullException(nameof(mediaType));
        }

        using MemoryStream copy = stream.CanSeek
            ? new((int)(stream.Length - stream.Position))
            : new MemoryStream();

        await stream.CopyToAsync(copy, cancellationToken);

        Memory<byte> content = copy.GetBuffer().AsMemory(0, (int)copy.Length);
        // When mimeType is not provided, the LlamaParseClient will try to guess it based on the file extension.
        // Since we are dealing with a Stream (does not expose Media Type information),
        // and we use the identifier as the file name (which might not have an extension at all),
        // we require the caller to provide the mediaType in explicit way.
        InMemoryFile inMemoryFile = new(content, fileName: identifier, mimeType: mediaType);

        (List<RawResult> rawResults, List<ImageDocument> imageDocuments) = await LoadDataAsync(
            _client.LoadDataRawAsync(
                inMemoryFile,
                resultType: ResultType.Json,
                cancellationToken: cancellationToken),
            cancellationToken);

        return MapToDocument(rawResults, imageDocuments, identifier);
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

    private static IngestionDocument MapToDocument(List<RawResult> parsed, List<ImageDocument> images, string identifier)
    {
        IngestionDocument result = new(identifier);

        foreach (var rawResult in parsed)
        {
            LlamaParseDocument document = JsonSerializer.Deserialize<LlamaParseDocument>(rawResult.Result, _jsonSerializerOptions)!;
            result.Sections.AddRange(Map(document, images));
        }

        return result;
    }

    private static IEnumerable<IngestionDocumentSection> Map(LlamaParseDocument document, List<ImageDocument> images)
    {
        foreach (var parsedPage in document.Pages)
        {
            IngestionDocumentSection page = new(parsedPage.Markdown)
            {
                Text = parsedPage.Text,
                PageNumber = parsedPage.PageNumber,
                Metadata =
                {
                    { nameof(Page.Width), parsedPage.Width },
                    { nameof(Page.Height), parsedPage.Height },
                    { nameof(Page.Confidence), parsedPage.Confidence },
                }
            };

            if (!string.IsNullOrEmpty(parsedPage.PageHeaderMarkdown))
            {
                page.Elements.Add(new IngestionDocumentHeader(parsedPage.PageHeaderMarkdown)
                {
                    // It's weird: Page Header is exposed as Markdown, but not as Text.
                    Text = parsedPage.PageHeaderMarkdown.TrimStart('#'),
                });
            }

            foreach (var item in parsedPage.Items)
            {
                if (item is TablePageItem { Rows.Count: 0 })
                {
                    continue; // Workaround a LlamaParse bug
                }

                IngestionDocumentElement element = item switch
                {
                    TextPageItem text => new IngestionDocumentParagraph(item.Markdown)
                    {
                        Text = text.Value,
                    },
                    HeadingPageItem heading => new IngestionDocumentHeader(item.Markdown)
                    {
                        Text = heading.Value,
                        Level = heading.Level,
                    },
                    TablePageItem table => new IngestionDocumentTable(table.Markdown, GetCells(table.Rows)),
                    _ => throw new InvalidOperationException()
                };
                element.PageNumber = parsedPage.PageNumber;
                element.Metadata[nameof(PageItem.BoundingBox)] = item.BoundingBox;

                page.Elements.Add(element);
            }

            // All we know about the location of images is the page number, so we add them at the end of the page.
            object boxOnce = parsedPage.PageNumber;
            foreach (var image in images.Where(image => image.Image is not null &&
                image.Metadata.TryGetValue("page_number", out object? pageNumber) && pageNumber.Equals(boxOnce)))
            {
                // Based on what we learn from the further steps like using the image with IChatClient,
                // the Base64 string might become the standard instead of BinaryData.
                ReadOnlyMemory<byte> binaryData = Convert.FromBase64String(image.Image!);

                IngestionDocumentImage documentImage = new($"![]({image.ImageUrl})")
                {
                    Content = binaryData,
                    MediaType = image.ImageMimetype,
                    PageNumber = parsedPage.PageNumber,
                    Text = image.Text,
                };

                foreach (var kvp in image.Metadata)
                {
                    documentImage.Metadata[kvp.Key] = kvp.Value;
                }

                page.Elements.Add(documentImage);
            }

            if (!string.IsNullOrEmpty(parsedPage.PageFooterMarkdown))
            {
                page.Elements.Add(new IngestionDocumentFooter(parsedPage.PageFooterMarkdown)
                {
                    // It's weird: Page Footer is exposed as Markdown, but not as Text.
                    Text = parsedPage.PageFooterMarkdown.TrimStart('#'),
                });
            }

            yield return page;
        }
    }

    private static IngestionDocumentElement?[,] GetCells(List<List<string>> rows)
    {
        var cells = new IngestionDocumentElement?[rows.Count, rows[0].Count];
        for (int i = 0; i < rows.Count; i++)
        {
            for (int j = 0; j < rows[i].Count; j++)
            {
                cells[i,j] = string.IsNullOrEmpty(rows[i][j])
                    ? null // IngestionDocumentParagraph does not accept empty strings
                    : new IngestionDocumentParagraph(rows[i][j]);
            }
        }
        return cells;
    }
}
