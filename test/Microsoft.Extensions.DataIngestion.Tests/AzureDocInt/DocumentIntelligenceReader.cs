// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.DocumentIntelligence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AdiParagraph = Azure.AI.DocumentIntelligence.DocumentParagraph;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    public sealed class DocumentIntelligenceReader : DocumentReader
    {
        private readonly DocumentIntelligenceClient _client;
        private readonly string _modelName;
        private readonly bool _extractImages;

        /// <param name="modelName">Unique document model name (<see cref="https://learn.microsoft.com/azure/ai-services/document-intelligence/overview#document-analysis-models"/>).</param>
        /// <param name="extractImages">Generate cropped images of detected figures. Disabled by default.</param>
        public DocumentIntelligenceReader(DocumentIntelligenceClient client, string modelName = "prebuilt-layout", bool extractImages = false)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _modelName = string.IsNullOrEmpty(modelName) ? throw new ArgumentNullException(nameof(modelName)) : modelName;
            _extractImages = extractImages;
        }

        public override async Task<Document> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            BinaryData binaryData = BinaryData.FromBytes(bytes);
            return await ReadAsync(new AnalyzeDocumentOptions(_modelName, binaryData), cancellationToken);
        }

        public override Task<Document> ReadAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ReadAsync(new AnalyzeDocumentOptions(_modelName, uri), cancellationToken);
        }

        public async Task<Document> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BinaryData binaryData = await BinaryData.FromStreamAsync(stream, cancellationToken);
            return await ReadAsync(new AnalyzeDocumentOptions(_modelName, binaryData), cancellationToken);
        }

        private async Task<Document> ReadAsync(AnalyzeDocumentOptions options, CancellationToken cancellationToken)
        {
            options.OutputContentFormat = DocumentContentFormat.Markdown;
            if (_extractImages)
            {
                options.Output.Add(AnalyzeOutputOption.Figures);
            }

            Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, options, cancellationToken);
            Dictionary<string, BinaryData> figures = await GetFigures(operation, cancellationToken);

            return MapToDocument(operation.Value, figures);
        }

        private async Task<Dictionary<string, BinaryData>> GetFigures(Operation<AnalyzeResult> result, CancellationToken cancellationToken)
        {
            Dictionary<string, BinaryData> figures = new(StringComparer.Ordinal);
            if (_extractImages)
            {
                foreach (var figure in result.Value.Figures)
                {
                    figures[figure.Id] = await _client.GetAnalyzeResultFigureAsync(result.Value.ModelId, result.Id, figure.Id, cancellationToken);
                }
            }
            return figures;
        }

        private static Document MapToDocument(AnalyzeResult parsed, Dictionary<string, BinaryData> figures)
        {
            Document document = new()
            {
                Markdown = parsed.Content,
            };

#if DEBUG
            HashSet<int> visitedSections = new();
#endif
            DocumentSection rootSection = new();
            HandleSection(sectionIndex: 0, rootSection, parsed.Content);

            // If the root section consists only of sections, add those sections directly to flatten the structure.
            if (rootSection.Elements.All(element => element is DocumentSection))
            {
                document.Sections.AddRange(rootSection.Elements.OfType<DocumentSection>());
            }
            else
            {
                document.Sections.Add(rootSection);
            }
#if DEBUG
            Debug.Assert(visitedSections.Count == parsed.Sections.Count, $"Visited {visitedSections.Count} out of {parsed.Sections.Count} sections.");
#endif

            return document;

            void HandleSection(int sectionIndex, DocumentSection section, string entireContent)
            {
#if DEBUG
                Debug.Assert(visitedSections.Add(sectionIndex), "Section should not be visited more than once.");
#endif
                var parsedSection = parsed.Sections[sectionIndex];
                foreach (var element in parsedSection.Elements)
                {
                    (string kind, int index) = Parse(element);

                    switch (kind)
                    {
                        case "section":
                            DocumentSection subSection = new();
                            section.Elements.Add(subSection);
                            HandleSection(index, subSection, entireContent);
                            break;
                        case "paragraph":
                            var parsedParagraph = parsed.Paragraphs[index];
                            var markdown = GetMarkdown(parsedParagraph.Spans, entireContent);
                            var paragraph = MapToElement(parsedParagraph, markdown);
                            paragraph.Markdown = markdown;
                            paragraph.PageNumber = GetPageNumber(parsedParagraph.BoundingRegions);
                            section.Elements.Add(paragraph);
                            break;
                        case "table":
                            var parsedTable = parsed.Tables[index];
                            // TODO adsitnik: handle tables and decide on design (list of rows vs list of cells).
                            section.Elements.Add(new DocumentTable()
                            {
                                PageNumber = GetPageNumber(parsedTable.BoundingRegions),
                                Markdown = GetMarkdown(parsedTable.Spans, entireContent),
                            });
                            break;
                        case "figure":
                            var figure = parsed.Figures[index];
                            BinaryData? content = figures.TryGetValue(figure.Id, out var data) ? data : null;
                            section.Elements.Add(new DocumentImage
                            {
                                Content = content,
                                MediaType = content?.MediaType ?? "image/png",
                                PageNumber = GetPageNumber(figure.BoundingRegions),
                                Text = figure.Caption?.Content ?? "",
                                Markdown = GetMarkdown(figure.Spans, entireContent),
                            });
                            break;
                        default:
                            throw new NotSupportedException($"Element kind '{kind}' is not supported.");
                    }
                }

                Debug.Assert(section.Elements.Count > 0, "Every section should contain at least one element.");
            }
        }

        private static (string kind, int index) Parse(string element)
        {
            if (element.StartsWith("/sections/", StringComparison.Ordinal))
            {
                return ("section", ParseIndex(element, "/sections/"));
            }
            else if (element.StartsWith("/paragraphs/", StringComparison.Ordinal))
            {
                return ("paragraph", ParseIndex(element, "/paragraphs/"));
            }
            else if (element.StartsWith("/tables/", StringComparison.Ordinal))
            {
                return ("table", ParseIndex(element, "/tables/"));
            }
            else if (element.StartsWith("/figures/", StringComparison.Ordinal))
            {
                return ("figure", ParseIndex(element, "/figures/"));
            }

            throw new NotSupportedException($"'{element}' is not a supported element type.");

            static int ParseIndex(string element, string prefix)
            {
#if NET8_0_OR_GREATER
                return int.Parse(element.AsSpan(prefix.Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
#else
                return int.Parse(element.Substring(prefix.Length), System.Globalization.CultureInfo.InvariantCulture);
#endif
            }
        }

        private static DocumentElement MapToElement(AdiParagraph parsedParagraph, string markdown)
        {
            if (parsedParagraph.Role is null)
            {
                return new DocumentParagraph
                {
                    Text = parsedParagraph.Content,
                };
            }
            else if (parsedParagraph.Role.Equals(ParagraphRole.PageHeader)
                || parsedParagraph.Role.Equals(ParagraphRole.SectionHeading) // If other parsers expose similar information, we could extend DocumentSection with DocumentHeader property.
                || parsedParagraph.Role.Equals(ParagraphRole.Title)) // Same as DocumentHeader, but for Presentations.
            {
                return new DocumentHeader()
                {
                    Text = parsedParagraph.Content,
                    Level = GetLevel(markdown)
                };
            }
            else if (parsedParagraph.Role.Equals(ParagraphRole.PageFooter)
                || parsedParagraph.Role.Equals(ParagraphRole.Footnote)) // Same as DocumentFooter, but for Presentations.
            {
                return new DocumentFooter()
                {
                    Text = parsedParagraph.Content,
                };
            }

            throw new NotSupportedException($"Paragraph role '{parsedParagraph.Role}' is not supported.");
        }

        private static int? GetPageNumber(IReadOnlyList<BoundingRegion> boundingRegions)
            => boundingRegions.Count != 1 ? null : boundingRegions[0].PageNumber;

        private static string GetMarkdown(IReadOnlyList<DocumentSpan> spans, string entireContent)
        {
            Debug.Assert(spans.Count > 0, "Paragraph should have at least one span.");

            return spans.Count switch
            {
                1 => entireContent.Substring(spans[0].Offset, spans[0].Length),
                _ => Concat(), // Multiple spans, concatenate them.
            };

            string Concat()
            {
                int length = 0;
                for (int i = 0; i < spans.Count; i++)
                {
                    length += spans[i].Length;
                }

                StringBuilder stringBuilder = new(length);
                foreach (var span in spans)
                {
                    stringBuilder.Append(entireContent.AsSpan(span.Offset, span.Length));
                }
                return stringBuilder.ToString();
            }
        }

        private static int? GetLevel(string markdown)
        {
            ReadOnlySpan<char> span = markdown.AsSpan().TrimStart();
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] != '#')
                {
                    return i == 0 ? null : i;
                }
            }
            return null;
        }
    }
}
