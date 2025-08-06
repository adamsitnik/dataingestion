// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure;
using Azure.AI.DocumentIntelligence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Tests
{
    public sealed class DocumentIntelligenceReader : DocumentReader
    {
        private readonly DocumentIntelligenceClient _client;
        private readonly string _modelName;

        /// <param name="modelName">Unique document model name (<see cref="https://learn.microsoft.com/azure/ai-services/document-intelligence/overview#document-analysis-models"/>).</param>
        public DocumentIntelligenceReader(DocumentIntelligenceClient client, string modelName = "prebuilt-layout")
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _modelName = string.IsNullOrEmpty(modelName) ? throw new ArgumentNullException(nameof(modelName)) : modelName;
        }

        public override async Task<Document> ReadAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AnalyzeDocumentOptions options = new(_modelName, uri)
            {
                // In the future, we could consider using DocumentContentFormat.Markdown.
                OutputContentFormat = DocumentContentFormat.Text
            };

            Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, options, cancellationToken);

            return MapToDocument(operation.Value);
        }

        public override async Task<Document> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BinaryData binaryData = await BinaryData.FromStreamAsync(stream, cancellationToken);
            Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, _modelName, binaryData, cancellationToken);

            return MapToDocument(operation.Value);
        }

        private static Document MapToDocument(AnalyzeResult parsed)
        {
            Document document = new();

#if DEBUG
            HashSet<int> visitedSections = new();
#endif
            Section rootSection = new();
            HandleSection(sectionIndex: 0, rootSection);

            // If the root section consists only of sections, add those sections directly to flatten the structure.
            if (rootSection.Elements.All(element => element is Section))
            {
                document.Sections.AddRange(rootSection.Elements.OfType<Section>());
            }
            else
            {
                document.Sections.Add(rootSection);
            }
#if DEBUG
            Debug.Assert(visitedSections.Count == parsed.Sections.Count, $"Visited {visitedSections.Count} out of {parsed.Sections.Count} sections.");
#endif

            return document;

            void HandleSection(int sectionIndex, Section section)
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
                            Section subSection = new();
                            section.Elements.Add(subSection);
                            HandleSection(index, subSection);
                            break;
                        case "paragraph":
                            var parsedParagraph = parsed.Paragraphs[index];
                            var paragraph = MapToElement(parsedParagraph);
                            paragraph.PageNumber = GetPageNumber(parsedParagraph.BoundingRegions);
                            section.Elements.Add(paragraph);
                            break;
                        case "table":
                            var parsedTable = parsed.Tables[index];
                            // TODO adsitnik: handle tables and decide on design (list of rows vs list of cells).
                            section.Elements.Add(new Table()
                            {
                                PageNumber = GetPageNumber(parsedTable.BoundingRegions)
                            });
                            break;
                        case "figure":
                            // TODO adsitnik: handle images and charts.
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

        private static Element MapToElement(DocumentParagraph parsedParagraph)
        {
            if (parsedParagraph.Role is null)
            {
                return new Paragraph
                {
                    Text = parsedParagraph.Content,
                };
            }
            else if (parsedParagraph.Role.Equals(ParagraphRole.PageHeader)
                || parsedParagraph.Role.Equals(ParagraphRole.SectionHeading) // If other parsers expose similar information, we could extend Section with Header property.
                || parsedParagraph.Role.Equals(ParagraphRole.Title)) // Same as Header, but for Presentations.
            {
                return new Header()
                {
                    Text = parsedParagraph.Content,
                };
            }
            else if (parsedParagraph.Role.Equals(ParagraphRole.PageFooter)
                || parsedParagraph.Role.Equals(ParagraphRole.Footnote)) // Same as Footer, but for Presentations.
            {
                return new Footer()
                {
                    Text = parsedParagraph.Content,
                };
            }

            throw new NotSupportedException($"Paragraph role '{parsedParagraph.Role}' is not supported.");
        }

        private static int? GetPageNumber(IReadOnlyList<BoundingRegion> boundingRegions)
            => boundingRegions.Count != 1 ? null : boundingRegions[0].PageNumber;
    }
}
