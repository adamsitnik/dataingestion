// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics.Tensors;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    public class SemanticChunker : IDocumentChunker
    {
        private IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private float _tresholdPercentile;

        public SemanticChunker(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, float tresholdPercentile = 95.0f)
        {
            _embeddingGenerator = embeddingGenerator;
            _tresholdPercentile = tresholdPercentile;
        }

        public async Task<List<DocumentChunk>> ProcessAsync(Document document, CancellationToken cancellationToken = default)
        {
            IEnumerable<DocumentElement> elements = document.Where(element => element is not DocumentSection);
            IEnumerable<string> units = elements.Select(e => e.Markdown); // Needs extension to work with all element types
            Task<List<(string, float)>> sentenceDistances = CalculateDistances(units.ToArray());

            return MakeChunks(await sentenceDistances);
        }

        private async Task<List<(string, float)>> CalculateDistances(string[] elements)
        {
            List<(string, float)> sentenceDistance = new();
            if (!elements.Any(e => !String.IsNullOrEmpty(e)))
            {
                return sentenceDistance;
            }

            ReadOnlyMemory<float>[] embeddings = (await _embeddingGenerator.GenerateAsync(elements)).Select(e => e.Vector).ToArray();

            for (int i = 0; i < elements.Length - 1; i++)
            {
                string current = elements[i];
                string next = elements[i + 1];
                float distance = 1 - TensorPrimitives.CosineSimilarity(embeddings[i].Span, embeddings[i + 1].Span);
                sentenceDistance.Add((current, distance));
            }

            sentenceDistance.Add((elements.Last(), 0f));
            return sentenceDistance;
        }

        private List<DocumentChunk> MakeChunks(List<(string, float)> elementDistances)
        {
            List<DocumentChunk> chunks = [];
            float distanceThreshold = Percentile(elementDistances.Select(x => x.Item2));

            List<string> elementAccumulator = [];
            foreach (var (sentence, distance) in elementDistances)
            {
                elementAccumulator.Add(sentence);
                if (distance > distanceThreshold)
                {
                    DocumentChunk chunk = new(String.Join(" ", elementAccumulator));
                    chunks.Add(chunk);
                    elementAccumulator.Clear();
                }
            }

            if (elementAccumulator.Count > 0)
            {
                DocumentChunk chunk = new(String.Join(" ", elementAccumulator));
                chunks.Add(chunk);
            }

            return chunks;
        }


        private float Percentile(IEnumerable<float> sequence)
        {
            var sorted = sequence.OrderBy(x => x).ToArray();
            if (sorted.Length == 0)
            {
                return 0f;
            }
            else if (sorted.Length == 1)
            {
                return sorted[0];
            }

            float i = (_tresholdPercentile / 100f) * (sorted.Length - 1);
            int i0 = (int)i;
            int i1 = Math.Min(i0 + 1, sorted.Length - 1);
            return sorted[i0] + (i - i0) * (sorted[i1] - sorted[i0]);
        }
    }
}
