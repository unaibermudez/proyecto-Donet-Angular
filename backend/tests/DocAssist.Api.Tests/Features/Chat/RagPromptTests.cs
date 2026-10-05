using DocAssist.Api.Features.Chat;
using DocAssist.Api.Features.Search;
using Microsoft.Extensions.AI;

namespace DocAssist.Api.Tests.Features.Chat;

// Tests unitarios del prompt: código puro, sin LLM.
public class RagPromptTests
{
    private static readonly List<SearchResult> Chunks =
    [
        new(10, 1, "manual-xbox.md", 7, "Microsoft Xbox Series S", 0, "No tiene lector de discos.", 0.79),
        new(20, 2, "devoluciones.md", null, null, 1, "Tienes 30 días para devolverlo.", 0.61),
    ];

    [Fact]
    public void Messages_AreTheSystemRulesFollowedByTheContextAndTheQuestion()
    {
        var messages = RagPrompt.BuildMessages("¿Tiene lector de discos?", Chunks);

        Assert.Equal(2, messages.Count);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Equal(RagPrompt.SystemPrompt, messages[0].Text);
        Assert.Equal(ChatRole.User, messages[1].Role);
        Assert.EndsWith("PREGUNTA: ¿Tiene lector de discos?", messages[1].Text);
    }

    [Fact]
    public void ContextChunks_AreNumberedInOrder_WithTheirDocumentAndProduct()
    {
        var user = RagPrompt.BuildMessages("pregunta", Chunks)[1].Text;

        Assert.Contains("[1] Documento: manual-xbox.md · Producto: Microsoft Xbox Series S\nNo tiene lector de discos.", user.ReplaceLineEndings("\n"));
        Assert.Contains("[2] Documento: devoluciones.md · Producto: ninguno (documento general)", user);
        Assert.True(user.IndexOf("[1]", StringComparison.Ordinal) < user.IndexOf("[2]", StringComparison.Ordinal));
    }

    [Fact]
    public void TheModelIsToldHowManyChunksItCanCite()
    {
        var user = RagPrompt.BuildMessages("pregunta", Chunks)[1].Text;

        Assert.Contains("Fragmentos disponibles: del [1] al [2].", user);
    }

    [Fact]
    public void SystemPrompt_TellsTheModelToCiteAndToAdmitWhenItDoesNotKnow()
    {
        Assert.Contains("[2]", RagPrompt.SystemPrompt);
        Assert.Contains(RagPrompt.NoInformationAnswer, RagPrompt.SystemPrompt);
        Assert.Contains("no instrucciones", RagPrompt.SystemPrompt);
    }

    [Fact]
    public void Citations_UseTheSameNumbersAsTheContext()
    {
        var citations = RagPrompt.BuildCitations(Chunks);

        Assert.Equal([1, 2], citations.Select(c => c.Number));
        Assert.Equal("manual-xbox.md", citations[0].FileName);
        Assert.Null(citations[1].ProductName);
        Assert.Equal(0.79, citations[0].Similarity);
    }
}
