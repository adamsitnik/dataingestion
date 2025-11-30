// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    public class LumberChunker : IngestionChunker<string>
    {
        private const string system_prompt = """
        You will receive as input an english document with paragraphs identified by 'ID XXXX: <text>'.

        Task: Find the first paragraph (not the first one) where the content clearly changes compared to the previous paragraphs.

        Output: Return the ID of the paragraph with the content shift as in the exemplified format: 'Answer: ID XXXX'.

        Additional Considerations: Avoid very long groups of paragraphs. Aim for a good balance between 
        identifying content shifts and keeping groups manageable.

        If there is no clear content shift, return 'Answer: ID -1'.
        """;
        private readonly ChatMessage _systemMessage = new ChatMessage(ChatRole.System, system_prompt);
        private readonly ChatOptions _chatOptions = new ChatOptions
        {
            Temperature = 0.1f
        };


        private const string IdRegex = """Answer: ID \w+""";
        private readonly IChatClient _chatClient;
        private readonly Tokenizer _tokenizer;
        private readonly ElementsChunker _elementsChunker;
        private readonly int _maxTokensPerChunk;
        private readonly bool _considerNormalization;


        public LumberChunker(IngestionChunkerOptions options, IChatClient chat)
        {
            _tokenizer = options.Tokenizer;
            _maxTokensPerChunk = options.MaxTokensPerChunk;
            _considerNormalization = options.ConsiderNormalization;
            _chatClient = chat;
            _elementsChunker = new ElementsChunker(options);
        }

        public override IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(IngestionDocument document, CancellationToken cancellationToken = default)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            List<IngestionChunk<string>> chunks = [];
            int totalTokens = 0;
            List<PreChunk> currentPreChunkElements = [];
            foreach (IngestionDocumentElement element in document.EnumerateContent())
            {
                string? semanticContent = element.SemanticContent;

                if (string.IsNullOrEmpty(semanticContent))
                {
                    continue; // An image can come with Markdown, but no AlternativeText or Text.
                }
                int elementTokenCount = CountTokens(semanticContent.AsSpan());

                if (totalTokens + elementTokenCount > _maxTokensPerChunk)
                {
                    ProcessPrechunks();
                }
                if (totalTokens + elementTokenCount > _maxTokensPerChunk) // Still too big even after split
                {
                    IEnumerable<IngestionChunk<string>> processedChunks = CreateChunks(element, document);
                    chunks.AddRange(processedChunks);
                }

                if (elementTokenCount > _maxTokensPerChunk)
                {
                    var split_chunks = _elementsChunker.Process(document, String.Empty, [element]);
                    chunks.AddRange(split_chunks);
                }
                else
                {
                    totalTokens += elementTokenCount;
                    currentPreChunkElements.Add(new PreChunk(element, elementTokenCount));
                }
            }

            while (currentPreChunkElements.Any())
            {
                ProcessPrechunks();
            }
            return chunks.ToAsyncEnumerable();

            void ProcessPrechunks()
            {
                if (currentPreChunkElements.Count == 0)
                {
                    return;
                }

                int splitIndex = GetSplitPoint(cancellationToken, currentPreChunkElements);
                splitIndex = splitIndex == -1 ? currentPreChunkElements.Count : splitIndex;

                IEnumerable<IngestionChunk<string>> processedChunks = CreateChunks(currentPreChunkElements.Take(splitIndex), document);
                chunks.AddRange(processedChunks);

                currentPreChunkElements = currentPreChunkElements.Skip(splitIndex).ToList();
                totalTokens = currentPreChunkElements.Sum(pc => pc.TokenCount);
            }
        }

        private IEnumerable<IngestionChunk<string>> CreateChunks(IEnumerable<PreChunk> preChunks, IngestionDocument document)
        {
            List<IngestionDocumentElement> elements = preChunks.Select(pc => pc.Element).ToList();
            return _elementsChunker.Process(document, String.Empty, elements);
        }

        private IEnumerable<IngestionChunk<string>> CreateChunks(IngestionDocumentElement element, IngestionDocument document)
        {
            return _elementsChunker.Process(document, String.Empty, [element]);
        }

        private int GetSplitPoint(CancellationToken cancellationToken,  List<PreChunk> currentChunkElements)
        {
            string query = BuildQuery(currentChunkElements);
            IEnumerable<ChatMessage> messages = [
                _systemMessage,
                new ChatMessage(ChatRole.User, query)
            ];

            string response = _chatClient.GetResponseAsync(messages, _chatOptions, cancellationToken).Result.Text;
            int match = ParseResponse(response);
            return match;
        }

        private string BuildQuery(List<PreChunk> currentChunkElements)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < currentChunkElements.Count; i++)
            {
                sb.AppendLine($"ID {i}: {currentChunkElements[i].Element.SemanticContent}");
            }
            return sb.ToString();
        }

        private static int ParseResponse(string response)
        {
            if (response.Equals("Answer: ID -1", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            var idMatch = System.Text.RegularExpressions.Regex.Match(response, IdRegex);
            int match = 0;
            if (idMatch.Success)
            {
                var digits = System.Text.RegularExpressions.Regex.Match(idMatch.Value, @"\d+");
                if (digits.Success)
                {
                    _ = int.TryParse(digits.Value, out match);
                }
            }

            return match;
        }

        private int CountTokens(ReadOnlySpan<char> input)
        => _tokenizer.CountTokens(input, considerNormalization: _considerNormalization);

        internal readonly struct PreChunk
        {
            public IngestionDocumentElement Element { get; }
            public int TokenCount { get; }

            public PreChunk(IngestionDocumentElement element, int tokenCount)
            {
                if (element is null)
                {
                    throw new ArgumentNullException(nameof(element));
                }
                if (tokenCount < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(tokenCount), "TokenCount must be non-negative.");
                }

                Element = element;
                TokenCount = tokenCount;
            }

            public void Deconstruct(out IngestionDocumentElement element, out int tokenCount)
            {
                element = Element;
                tokenCount = TokenCount;
            }
        }
    }
}
