// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Microsoft.Extensions.DataIngestion;

/// <summary>
/// Reads documents by converting them to Markdown using the <see href="https://github.com/microsoft/markitdown">MarkItDown</see> MCP server.
/// </summary>
public class MarkItDownMcpReader : IngestionDocumentReader
{
    private readonly Uri _mcpServerUri;
    private readonly McpClientOptions? _mcpOptions;
    private readonly MarkdownReader _markdownReader = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkItDownMcpReader"/> class.
    /// </summary>
    /// <param name="mcpServerUri">The URI of the MarkItDown MCP server (e.g., http://localhost:3001/mcp).</param>
    public MarkItDownMcpReader(Uri mcpServerUri, McpClientOptions? mcpOptions = null)
    {
        _mcpServerUri = mcpServerUri ?? throw new ArgumentNullException(nameof(mcpServerUri));
        _mcpOptions = mcpOptions;
    }

    /// <inheritdoc/>
    public override async Task<IngestionDocument> ReadAsync(FileInfo source, string identifier, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        else if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }
        else if (!source.Exists)
        {
            throw new FileNotFoundException("The specified file does not exist.", source.FullName);
        }

#if NET
        byte[] fileBytes = await File.ReadAllBytesAsync(source.FullName, cancellationToken).ConfigureAwait(false);
#else
        byte[] fileBytes;
        using (FileStream fs = new(source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous))
        {
            using MemoryStream ms = new();
            await fs.CopyToAsync(ms).ConfigureAwait(false);
            fileBytes = ms.ToArray();
        }
#endif
        string dataUri = BuildDataUri(mediaType, fileBytes);
        string markdown = await ConvertToMarkdownAsync(dataUri, cancellationToken).ConfigureAwait(false);

        return _markdownReader.Read(markdown, identifier);
    }

    /// <inheritdoc/>
    public override async Task<IngestionDocument> ReadAsync(Stream source, string identifier, string mediaType, CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        else if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        // Read stream content as base64 data URI
        using MemoryStream ms = new();
#if NET
        await source.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
#else
        await source.CopyToAsync(ms).ConfigureAwait(false);
#endif
        string dataUri = BuildDataUri(mediaType, ms);
        string markdown = await ConvertToMarkdownAsync(dataUri, cancellationToken).ConfigureAwait(false);

        return _markdownReader.Read(markdown, identifier);
    }

    private static string BuildDataUri(string? mediaType, MemoryStream memoryStream)
    {
        string base64Content = memoryStream.TryGetBuffer(out var buffer)
            ? Convert.ToBase64String(buffer.Array!, buffer.Offset, buffer.Count)
            : Convert.ToBase64String(memoryStream.ToArray());

        string mimeType = string.IsNullOrEmpty(mediaType) ? "application/octet-stream" : mediaType!;
        return $"data:{mimeType};base64,{base64Content}";
    }

    private static string BuildDataUri(string? mediaType, byte[] fileBytes)
    {
        string base64Content = Convert.ToBase64String(fileBytes);
        string mimeType = string.IsNullOrEmpty(mediaType) ? "application/octet-stream" : mediaType!;
        string dataUri = $"data:{mimeType};base64,{base64Content}";
        return dataUri;
    }

    private async Task<string> ConvertToMarkdownAsync(string dataUri, CancellationToken cancellationToken)
    {
        await using HttpClientTransport transport = new(new HttpClientTransportOptions
        {
            Endpoint = _mcpServerUri
        });

        await using McpClient client = await McpClient.CreateAsync(transport, _mcpOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

        Dictionary<string, object?> parameters = new()
        {
            ["uri"] = dataUri
        };

        CallToolResult result = await client.CallToolAsync("convert_to_markdown", parameters, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Extract markdown content from result
        // The result is expected to be in the format: { "content": [{ "type": "text", "text": "markdown content" }] }
        if (result.Content != null && result.Content.Count > 0)
        {
            foreach (var content in result.Content)
            {
                if (content.Type == "text" && content is TextContentBlock textBlock)
                {
                    return textBlock.Text;
                }
            }
        }

        throw new InvalidOperationException("Failed to convert document to markdown: unexpected response format from MCP server.");
    }
}
