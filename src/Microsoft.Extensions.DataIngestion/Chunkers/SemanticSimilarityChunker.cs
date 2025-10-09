// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.DataIngestion.Chunkers;

/// <summary>
/// Splits a <see cref="IngestionDocument"/> into chunks based on semantic similarity between its elements.
/// </summary>
public sealed class SemanticSimilarityChunker : IngestionChunker
{
    private readonly ElementsChunker _elementsChunker;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly float _thresholdPercentile;

    public SemanticSimilarityChunker(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        Tokenizer tokenizer, IngestionChunkerOptions? options = default,
        float thresholdPercentile = 95.0f)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _elementsChunker = new(tokenizer, options ?? new());
        _thresholdPercentile = thresholdPercentile < 0f || thresholdPercentile > 100f
            ? throw new ArgumentOutOfRangeException(nameof(thresholdPercentile))
            : thresholdPercentile ;
    }

    public override async IAsyncEnumerable<IngestionChunk> ProcessAsync(IngestionDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (document.Sections.Count == 0)
        {
            yield break;
        }

        List<(IngestionDocumentElement, float)> distances = await CalculateDistances(document, cancellationToken);
        foreach (var chunk in MakeChunks(document, distances))
        {
            yield return chunk;
        }
    }

    private async Task<List<(IngestionDocumentElement element, float distance)>> CalculateDistances(IngestionDocument documents, CancellationToken cancellationToken)
    {
        List<(IngestionDocumentElement element, float distance)> elementDistance = [];
        List<string> semanticContents = [];

        foreach (IngestionDocumentElement element in documents.EnumerateContent())
        {
            string? semanticContent = element is IngestionDocumentImage img
                ? img.AlternativeText ?? img.Text
                : element.GetMarkdown();

            if (!string.IsNullOrEmpty(semanticContent))
            {
                elementDistance.Add((element, default));
                semanticContents.Add(semanticContent!);
            }
        }

        var embeddings = await _embeddingGenerator.GenerateAsync(semanticContents, cancellationToken: cancellationToken).ConfigureAwait(false);

        for (int i = 0; i < elementDistance.Count - 1; i++)
        {
            float distance = 1 - TensorPrimitives.CosineSimilarity(embeddings[i].Vector.Span, embeddings[i + 1].Vector.Span);
            elementDistance[i] = (elementDistance[i].element, distance);
        }

        return elementDistance;
    }

    private IEnumerable<IngestionChunk> MakeChunks(IngestionDocument document, List<(IngestionDocumentElement element, float distance)> elementDistances)
    {
        float distanceThreshold = Percentile(elementDistances);

        List<IngestionDocumentElement> elementAccumulator = [];
        string context = string.Empty; // we could implement some simple heuristic
        foreach (var (element, distance) in elementDistances)
        {
            elementAccumulator.Add(element);
            if (distance > distanceThreshold)
            {
                foreach (var chunk in _elementsChunker.Process(document, context, elementAccumulator))
                {
                    yield return chunk;
                }
                elementAccumulator.Clear();
            }
        }

        if (elementAccumulator.Count > 0)
        {
            foreach (var chunk in _elementsChunker.Process(document, context, elementAccumulator))
            {
                yield return chunk;
            }
        }
    }

    private float Percentile(List<(IngestionDocumentElement element, float distance)> elementDistances)
    {
        if (elementDistances.Count == 0)
        {
            return 0f;
        }
        else if (elementDistances.Count == 1)
        {
            return elementDistances[0].distance;
        }

        float[] sorted = new float[elementDistances.Count];
        for (int elementIndex = 0; elementIndex < elementDistances.Count; elementIndex++)
        {
            sorted[elementIndex] = elementDistances[elementIndex].distance;
        }
        Array.Sort(sorted);

        float i = (_thresholdPercentile / 100f) * (sorted.Length - 1);
        int i0 = (int)i;
        int i1 = Math.Min(i0 + 1, sorted.Length - 1);
        return sorted[i0] + (i - i0) * (sorted[i1] - sorted[i0]);
    }
}
