using System.Text;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;

namespace AgentContext.Tests.Fakes;

/// <summary>
/// Deterministic fake <see cref="ILlmClient"/> for the pipeline seam tests
/// (spec §Testing Decisions: the LLM is the mocked collaborator — the database
/// is never mocked). Embeddings derive from the input text hash, so identical
/// content yields the identical vector (cosine distance 0) and different
/// content yields a pseudo-random 1536-dim vector — dedup behaviour is
/// controllable without a real model.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Queue<IReadOnlyList<KnowledgeExtraction>> _extractions = new();
    private int _extractionFailuresRemaining;

    /// <summary>Per-input embedding; default is a deterministic hash-based vector.</summary>
    public Func<string, float[]> EmbeddingFor { get; set; } = VectorFor;

    /// <summary>Seed the next ExtractKnowledgeAsync call(s) with canned items.</summary>
    public void EnqueueExtractions(params KnowledgeExtraction[] items) => _extractions.Enqueue(items);

    /// <summary>Make the next N extraction calls throw (retry-path tests).</summary>
    public void FailNextExtractions(int count) => _extractionFailuresRemaining = count;

    public Task<IReadOnlyList<KnowledgeExtraction>> ExtractKnowledgeAsync(
        string sessionSummaryJson, CancellationToken cancellationToken = default)
    {
        if (_extractionFailuresRemaining > 0)
        {
            _extractionFailuresRemaining--;
            throw new InvalidOperationException("LLM extraction failed (fake).");
        }

        return Task.FromResult(_extractions.Count > 0 ? _extractions.Dequeue() : []);
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(EmbeddingFor(text));

    /// <summary>Deterministic 1536-dim vector derived from the text (same text → same vector).</summary>
    public static float[] VectorFor(string text)
    {
        var seed = 17;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            seed = seed * 31 + b;
        }

        var rng = new Random(seed);
        return Enumerable.Range(0, LearningPipelineDefaults.EmbeddingDimensions)
            .Select(_ => (float)(rng.NextDouble() * 2 - 1))
            .ToArray();
    }
}
