// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.DataIngestion.Chunkers
{
    public class SlidingWindowNeuralSplittingStrategy : TextSplittingStrategy
    {
        private readonly InferenceSession _inferenceSession;
        private readonly Tokenizer _tokenizer;
        private readonly double _logitsThreshold;
        private const int WindowSlideContextLength = 255; // Could be dynamic relative to max tokens per chunk

        public SlidingWindowNeuralSplittingStrategy(string tokenizerVocalbularyPath, string modelPath, double probabilityThreshold = 0.5)
        {
            _logitsThreshold = Math.Log(1.0 / probabilityThreshold - 1.0);
            _tokenizer = BertTokenizer.Create(tokenizerVocalbularyPath);
            _inferenceSession = new InferenceSession(modelPath);
        }

        public override List<int> GetSplitIndices(ReadOnlySpan<char> text, int maxTokenCount)
        {
            TokenData tokenData = PrepareTokens(text, out IReadOnlyList<EncodedToken>? encodedTokens);
            ChunkingContext context = new()
            {
                SplitCharPositions = new List<int>(),
                TokenPositions = new List<int>(),
                WindowStart = 0,
                UnchunkTokens = 0,
                BackupPos = null,
                BestLogit = float.NegativeInfinity,
                MaxTokensPerChunk = maxTokenCount
            };
            while (context.WindowStart < tokenData.CoreLength)
            {
                ProcessWindow(tokenData, encodedTokens, context);
            }
            return context.TokenPositions;
        }

        private TokenData PrepareTokens(ReadOnlySpan<char> text, out IReadOnlyList<EncodedToken> encodedTokens)
        {
            encodedTokens = _tokenizer.EncodeToTokens(text, out string? _);
            long[] allIds = encodedTokens.Select(t => (long)t.Id).ToArray();
            long clsId = allIds[0];
            long sepId = allIds[allIds.Length - 1];

            ReadOnlySpan<long> coreIds = allIds.AsSpan(1, allIds.Length - 2);
            int coreLength = coreIds.Length;
            int step = (int)Math.Round(Math.Floor((WindowSlideContextLength - 2) / 2.0) * 1.75);

            return new TokenData(clsId, sepId, coreIds.ToArray(), coreLength, step);
        }

        private void ProcessWindow(
            TokenData tokenData,
            IReadOnlyList<EncodedToken> encodedTokens,
            ChunkingContext context)
        {
            int winEndExclusive = Math.Min(tokenData.CoreLength, context.WindowStart + WindowSlideContextLength - 2);
            int innerLen = winEndExclusive - context.WindowStart;

            ReadOnlySpan<long> coreSpan = tokenData.CoreIds.AsSpan();
            long[] winIdsArray = new long[innerLen + 2];
            winIdsArray[0] = tokenData.ClsId;
            coreSpan.Slice(context.WindowStart, innerLen).CopyTo(winIdsArray.AsSpan(1, innerLen));
            winIdsArray[innerLen + 1] = tokenData.SepId;
            ReadOnlySpan<long> winIds = winIdsArray.AsSpan();

            float[] diffs = RunInferenceAndComputeDiffs(winIds);
            int[] aboveIndicesThreshold = FilterIndicesAboveThreshold(diffs, _logitsThreshold);
            bool hasUsefulSeparator = HasUsefulSeparator(aboveIndicesThreshold);

            if (hasUsefulSeparator)
            {
                ProcessWindowWithSeparators(
                    aboveIndicesThreshold,
                    diffs,
                    encodedTokens,
                    context);
            }
            else
            {
                ProcessWindowWithoutSeparators(
                    diffs,
                    encodedTokens,
                    tokenData.CoreLength,
                    tokenData.Step,
                    context);
            }
        }

        private float[] RunInferenceAndComputeDiffs(ReadOnlySpan<long> windowIds)
        {
            List<NamedOnnxValue> inputs = CreateModelInputs(windowIds);

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _inferenceSession.Run(inputs);
            Tensor<float> logits = results[0].AsTensor<float>();

            return ComputeLogitDifferences(logits);
        }

        private static List<NamedOnnxValue> CreateModelInputs(ReadOnlySpan<long> winIds)
        {
            DenseTensor<long> idsTensor = new DenseTensor<long>(winIds.ToArray(), [1, winIds.Length]);
            DenseTensor<long> attMask = new DenseTensor<long>(Enumerable.Repeat(1L, winIds.Length).ToArray(), new[] { 1, winIds.Length });
            DenseTensor<long> typeIds = new DenseTensor<long>(new long[winIds.Length], [1, winIds.Length]);

            return new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", idsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attMask),
                NamedOnnxValue.CreateFromTensor("token_type_ids", typeIds)
            };
        }

        private static float[] ComputeLogitDifferences(Tensor<float> logits)
        {
            int winSequenceLength = logits.Dimensions[1];
            int innerTokenCount = Math.Max(0, winSequenceLength - 2);

            return Enumerable.Range(0, innerTokenCount)
                .Select(i => logits[0, i + 1, 1] - logits[0, i + 1, 0])
                .ToArray();
        }

        private static int[] FilterIndicesAboveThreshold(float[] diffs, double logitsThreshold)
        {
            return diffs
                .Select((diff, index) => (diff, index))
                .Where(x => x.diff > -logitsThreshold)
                .Select(x => x.index)
                .ToArray();
        }

        private static bool HasUsefulSeparator(int[] aboveIndicesThreshold)
        {
            return aboveIndicesThreshold.Length > 0 &&
                   !(aboveIndicesThreshold.Length == 1 && aboveIndicesThreshold[0] == 0);
        }

        private void ProcessWindowWithSeparators(
            int[] aboveIndicesThreshold,
            float[] diffs,
            IReadOnlyList<EncodedToken> encodedTokens,
            ChunkingContext context)
        {
            int innerTokenCount = diffs.Length;
            int unchunkThisWindow = CalculateUnchunkThisWindow(aboveIndicesThreshold, innerTokenCount);

            if (context.MaxTokensPerChunk > 0 && (context.UnchunkTokens + unchunkThisWindow) > context.MaxTokensPerChunk)
            {
                UpdateBackupPositionInRange(diffs, context.WindowStart, 0, context.MaxTokensPerChunk - context.UnchunkTokens, context);
                ForceSplitAtBackup(encodedTokens, context);
                return;
            }

            int[] filteredIndices = FilterIndicesByMaxTokens(aboveIndicesThreshold, context);
            RecordSplitPositions(filteredIndices, context.WindowStart, encodedTokens, context);

            int lastIndex = filteredIndices[filteredIndices.Length - 1];
            context.WindowStart = lastIndex + context.WindowStart;
            ResetBackupPosition(context);
        }

        private static int CalculateUnchunkThisWindow(int[] aboveIndicesThreshold, int innerTokenCount)
        {
            return (aboveIndicesThreshold[0] != 0)
                ? aboveIndicesThreshold[0]
                : (aboveIndicesThreshold.Length > 1 ? aboveIndicesThreshold[1] : innerTokenCount);
        }

        private int[] FilterIndicesByMaxTokens(int[] aboveIndicesThreshold, ChunkingContext context)
        {
            if (aboveIndicesThreshold.Length < 2 || context.MaxTokensPerChunk <= 0)
                return aboveIndicesThreshold;

            for (int i = 0; i < aboveIndicesThreshold.Length - 1; i++)
            {
                if (aboveIndicesThreshold[i + 1] - aboveIndicesThreshold[i] > context.MaxTokensPerChunk)
                {
                    return aboveIndicesThreshold.Take(i + 1).ToArray();
                }
            }

            return aboveIndicesThreshold;
        }

        private static void RecordSplitPositions(
            int[] indices,
            int windowStart,
            IReadOnlyList<EncodedToken> encodedTokens,
            ChunkingContext context)
        {
            foreach (var index in indices.Skip(1))
            {
                int globalPos = index + windowStart;
                int charStart = encodedTokens[globalPos + 1].Offset.Start.Value; // +1 for CLS

                context.SplitCharPositions.Add(charStart);
                context.TokenPositions.Add(globalPos);
            }
        }

        private void ProcessWindowWithoutSeparators(
            float[] diffs,
            IReadOnlyList<EncodedToken> encodedTokens,
            int coreLength,
            int step,
            ChunkingContext context)
        {
            int stepThis = Math.Min(context.WindowStart + step, coreLength) - context.WindowStart;

            if (context.MaxTokensPerChunk > 0 && (context.UnchunkTokens + stepThis) > context.MaxTokensPerChunk)
            {
                UpdateBackupPositionInRange(diffs, context.WindowStart, 0, context.MaxTokensPerChunk - context.UnchunkTokens, context);
                ForceSplitAtBackup(encodedTokens, context);
            }
            else
            {
                UpdateBackupPositionInRange(diffs, context.WindowStart, 0, diffs.Length, context);
                context.UnchunkTokens += stepThis;
                context.WindowStart += stepThis;
            }
        }

        private static void UpdateBackupPositionInRange(
            float[] diffs,
            int windowStart,
            int startIndex,
            int rangeLength,
            ChunkingContext context)
        {
            if (diffs.Length <= 1)
                return;

            int searchEnd = Math.Min(diffs.Length - 1, startIndex + rangeLength - 1);
            if (searchEnd < 1)
                return;

            int argMax = Math.Max(1, startIndex);
            float maxVal = diffs[argMax];

            for (int i = argMax + 1; i <= searchEnd; i++)
            {
                if (diffs[i] > maxVal)
                {
                    maxVal = diffs[i];
                    argMax = i;
                }
            }

            if (maxVal > context.BestLogit)
            {
                context.BestLogit = maxVal;
                context.BackupPos = windowStart + argMax;
            }
        }

        private static void ForceSplitAtBackup(IReadOnlyList<EncodedToken> encodedTokens, ChunkingContext context)
        {
            int splitPos = context.BackupPos ?? context.WindowStart;
            int encIdx = splitPos + 1;
            int charStart = encodedTokens[encIdx].Offset.Start.Value;

            context.SplitCharPositions.Add(charStart);
            context.TokenPositions.Add(splitPos);

            ResetBackupPosition(context);
            context.WindowStart = splitPos;
        }

        private static void ResetBackupPosition(ChunkingContext context)
        {
            context.BestLogit = float.NegativeInfinity;
            context.BackupPos = null;
            context.UnchunkTokens = 0;
        }

        private static List<string> BuildChunksFromPositions(string text, List<int> splitCharPositions)
        {
            List<string> chunks = new List<string>();
            int[] starts = [0, .. splitCharPositions];
            int[] ends = [.. splitCharPositions, text.Length];

            for (int i = 0; i < starts.Length; i++)
            {
                int s = starts[i];
                int e = ends[i];
                if (e > s)
                {
                    chunks.Add(text.Substring(s, e - s));
                }
            }

            return chunks;
        }

        private struct ChunkingContext
        {
            public ChunkingContext(List<int> splitCharPositions, List<int> tokenPositions, int windowStart, int unchunkTokens, int? backupPos, float bestLogit, int maxTokensPerChunk)
            {
                SplitCharPositions = splitCharPositions;
                TokenPositions = tokenPositions;
                WindowStart = windowStart;
                UnchunkTokens = unchunkTokens;
                BackupPos = backupPos;
                BestLogit = bestLogit;
                MaxTokensPerChunk = maxTokensPerChunk;
            }

            public List<int> SplitCharPositions { get; set; }
            public List<int> TokenPositions { get; set; }
            public int WindowStart { get; set; }
            public int UnchunkTokens { get; set; }
            public int? BackupPos { get; set; }
            public float BestLogit { get; set; }
            public int MaxTokensPerChunk { get; set; }
        }

        private readonly struct TokenData
        {
            public TokenData(long clsId, long sepId, long[] coreIds, int coreLength, int step)
            {
                ClsId = clsId;
                SepId = sepId;
                CoreIds = coreIds;
                CoreLength = coreLength;
                Step = step;
            }

            public long ClsId { get; }
            public long SepId { get; }
            public long[] CoreIds { get; }
            public int CoreLength { get; }
            public int Step { get; }
        }
    }
}
