// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        return MapToDocument(await _client.LoadDataRawAsync(
                new FileInfo(filePath),
                resultType: ResultType.Json,
                cancellationToken: cancellationToken)
            .ToArrayAsync(cancellationToken));
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

        return MapToDocument(await _client.LoadDataRawAsync(
                inMemoryFile,
                resultType: ResultType.Json,
                cancellationToken: cancellationToken)
            .ToArrayAsync(cancellationToken));
    }

    public override async Task<Document> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        Memory<byte> content = new byte[checked(stream.Length - stream.Position)];
        await stream.ReadAtLeastAsync(content, content.Length, cancellationToken: cancellationToken).ConfigureAwait(false);

        string fileName = stream is FileStream fileStream
            ? Path.GetFileName(fileStream.Name)
            // TODO: adsitnik: this is either a design flaw of DocumentReader or LlamaParseClient limitation.
            // As InMemoryFile tries to obtain the MIME type from the file name, but we only have a stream here.
            : throw new NotImplementedException("Unable to get file name and MIME type from stream.");

        InMemoryFile inMemoryFile = new(content, fileName);

        return MapToDocument(await _client.LoadDataRawAsync(
                inMemoryFile,
                resultType: ResultType.Json,
                cancellationToken: cancellationToken)
            .ToArrayAsync(cancellationToken));
    }

    private Document MapToDocument(RawResult[] parsed)
    {
        Document result = new();

        // As of now, we assume that there is only one result.
        // But when there are any images, the first one is the document
        // and the rest are images. Same seems to be true for charts.
        foreach (var rawResult in parsed)
        {
            if (rawResult.Result.TryGetProperty("pages", out var property))
            {
                LlamaParseDocument document = JsonSerializer.Deserialize<LlamaParseDocument>(rawResult.Result, _jsonSerializerOptions)!;
                result.Sections.AddRange(Map(document));
            }
            else
            {
                throw new NotImplementedException("Image support is missing");
            }
        }

        return result;
    }

    private IEnumerable<Section> Map(LlamaParseDocument document)
    {
        foreach (var parsedPage in document.Pages)
        {
            Section page = new()
            {
                Text = parsedPage.Text,
                Markdown = parsedPage.Markdown,
                PageNumber = parsedPage.PageNumber
            };

            if (!string.IsNullOrEmpty(parsedPage.PageHeaderMarkdown))
            {
                page.Elements.Add(new Header()
                {
                    // It's weird: Page Header is exposed as Markdown, but not as Text.
                    Text = RemoveHashtags(parsedPage.PageHeaderMarkdown),
                    Markdown = parsedPage.PageHeaderMarkdown
                });
            }

            foreach (var item in parsedPage.Items)
            {
                Element element = item switch
                {
                    TextPageItem text => new Paragraph()
                    {
                        Text = text.Value,
                    },
                    HeadingPageItem heading => new Header()
                    {
                        Text = heading.Value,
                        Level = heading.Level,
                    },
                    TablePageItem table => new Table()
                    {
                        Markdown = table.Markdown
                    },
                    _ => throw new InvalidOperationException()
                };
                element.PageNumber = parsedPage.PageNumber;
                element.Markdown = item.Markdown;

                page.Elements.Add(element);
            }

            if (!string.IsNullOrEmpty(parsedPage.PageFooterMarkdown))
            {
                page.Elements.Add(new Footer()
                {
                    // It's weird: Page Footer is exposed as Markdown, but not as Text.
                    Text = RemoveHashtags(parsedPage.PageFooterMarkdown),
                    Markdown = parsedPage.PageFooterMarkdown
                });
            }

            yield return page;
        }
    }

    private string RemoveHashtags(string pageHeaderMarkdown)
    {
        for (int i = 0; i < pageHeaderMarkdown.Length; i++)
        {
            if (pageHeaderMarkdown[i] != '#')
            {
                return i == 0 ? pageHeaderMarkdown : pageHeaderMarkdown[i..];
            }
        }

        return pageHeaderMarkdown;
    }
}
