// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.DataIngestion;

public class BoundingBox
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("w")]
    public double Width { get; set; }

    [JsonPropertyName("h")]
    public double Height { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextPageItem), "text")]
[JsonDerivedType(typeof(HeadingPageItem), "heading")]
[JsonDerivedType(typeof(TablePageItem), "table")]
public abstract class PageItem
{
    [JsonPropertyName("bBox")]
    public BoundingBox? BoundingBox { get; set; }

    [JsonPropertyName("md")]
    public string Markdown { get; set; } = string.Empty;
}

public class TextPageItem : PageItem
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class HeadingPageItem : PageItem
{
    [JsonPropertyName("lvl")]
    public int Level { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class TablePageItem : PageItem
{
    [JsonPropertyName("rows")]
    public List<List<string>> Rows { get; set; } = new();

    [JsonPropertyName("isPerfectTable")]
    public bool IsPerfectTable { get; set; }

    [JsonPropertyName("csv")]
    public string? Csv { get; set; }
}

public class Page
{
    [JsonPropertyName("page")]
    public int PageNumber { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("md")]
    public string Markdown { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<PageItem> Items { get; set; } = new();

    // Following fields were not included:
    // - `images`
    // - `charts`

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("originalOrientationAngle")]
    public int OriginalOrientationAngle { get; set; }

    [JsonPropertyName("links")]
    public List<Link> Links { get; set; } = new();

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    // Following fields were not included:
    // - `triggeredAutoMode`: boolean indicating if auto mode was triggered
    // - `parsingMode`: string indicating the parsing mode used (example: accurate)
    // - `structuredData`: object containing structured data extracted from the page
    // - `noStructuredContent`: boolean indicating if no structured content was found
    // - `noTextContent`: boolean indicating if no text content was found

    [JsonPropertyName("pageHeaderMarkdown")]
    public string PageHeaderMarkdown { get; set; } = string.Empty;

    [JsonPropertyName("pageFooterMarkdown")]
    public string PageFooterMarkdown { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public class Link
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("unsafeUrl")]
    public string? UnsafeUrl { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class LlamaParseDocument
{
    [JsonPropertyName("pages")]
    public List<Page> Pages { get; set; } = new();
}
