using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocAssist.Api.Domain;
using DocAssist.Api.Features.Chat;
using DocAssist.Api.Features.Documents;
using DocAssist.Api.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace DocAssist.Api.Tests.Features.Chat;

// Tests de integración de /api/chat y /api/chat/stream. La búsqueda es real (Postgres +
// pgvector con FakeEmbeddingGenerator) y el LLM es FakeChatClient.
public class ChatEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Ask_ReturnsTheAnswerWithNumberedCitations()
    {
        await UploadReadyAsync("guayaba.md", "# Guayaba\n\nLa guayaba tiene una garantia de siete años guayaba.");

        var response = await _client.PostAsJsonAsync("/api/chat",
            new AskRequest { Question = "¿Cuánta garantia tiene la guayaba?" }, Json);

        response.EnsureSuccessStatusCode();
        var answer = await response.Content.ReadFromJsonAsync<AskResponse>(Json);
        Assert.NotNull(answer);
        Assert.Equal(FakeChatClient.Answer, answer.Answer);
        Assert.InRange(answer.Citations.Count, 1, 4);
        Assert.Equal(Enumerable.Range(1, answer.Citations.Count), answer.Citations.Select(c => c.Number));
        Assert.Equal("guayaba.md", answer.Citations[0].FileName);
    }

    [Fact]
    public async Task Ask_SendsTheRulesTheContextAndTheQuestionToTheModel()
    {
        await UploadReadyAsync("chirimoya.md", "# Chirimoya\n\nLa chirimoya se envia en 24 horas chirimoya.");

        await _client.PostAsJsonAsync("/api/chat", new AskRequest { Question = "¿Cuándo llega la chirimoya?" }, Json);

        var messages = factory.ChatClient.LastMessages;
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Contains("Responde SOLO con la información", messages[0].Text);
        Assert.Contains("[1] Documento: chirimoya.md", messages[1].Text);
        Assert.Contains("La chirimoya se envia en 24 horas", messages[1].Text);
        Assert.EndsWith("PREGUNTA: ¿Cuándo llega la chirimoya?", messages[1].Text);
        Assert.Equal(0.1f, factory.ChatClient.LastOptions?.Temperature);
    }

    [Fact]
    public async Task Stream_SendsTheSourcesFirst_ThenTheTextInPieces_ThenDone()
    {
        await UploadReadyAsync("maracuya.md", "# Maracuya\n\nEl maracuya no tiene lector de discos maracuya.");

        var events = await StreamAsync("¿El maracuya tiene lector de discos?");

        Assert.Equal("sources", events[0].EventType);
        Assert.Equal("done", events[^1].EventType);
        var deltas = events.Where(e => e.EventType == "delta").ToList();
        Assert.True(deltas.Count > 1);
        Assert.Equal(FakeChatClient.Answer, string.Concat(deltas.Select(d => Deserialize<DeltaEvent>(d).Text)));

        var sources = Deserialize<SourcesEvent>(events[0]);
        Assert.Equal("maracuya.md", sources.Citations[0].FileName);
    }

    [Fact]
    public async Task Stream_WhenTheModelFails_SendsAnErrorEvent()
    {
        await UploadReadyAsync("papaya.md", "# Papaya\n\nLa papaya pesa dos kilos papaya.");

        var events = await StreamAsync($"¿Cuánto pesa la papaya? {FakeChatClient.FailureMarker}");

        Assert.Equal(["sources", "error"], events.Select(e => e.EventType));
        Assert.Contains("Ollama", Deserialize<ErrorEvent>(events[1]).Message);
    }

    [Theory]
    [InlineData("/api/chat")]
    [InlineData("/api/chat/stream")]
    public async Task TooShortQuestion_Returns400(string url)
    {
        var response = await _client.PostAsJsonAsync(url, new { question = "ab" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // Lee la respuesta SSE con el parser de .NET (System.Net.ServerSentEvents).
    private async Task<List<SseItem<string>>> StreamAsync(string question)
    {
        var response = await _client.PostAsJsonAsync("/api/chat/stream", new AskRequest { Question = question }, Json);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var events = new List<SseItem<string>>();
        await foreach (var item in SseParser.Create(await response.Content.ReadAsStreamAsync()).EnumerateAsync())
        {
            events.Add(item);
        }
        return events;
    }

    private static T Deserialize<T>(SseItem<string> item) =>
        JsonSerializer.Deserialize<T>(item.Data, Json) ?? throw new InvalidOperationException("Evento vacío.");

    private async Task UploadReadyAsync(string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName);
        var response = await _client.PostAsync("/api/documents", form);
        response.EnsureSuccessStatusCode();
        var document = (await response.Content.ReadFromJsonAsync<DocumentResponse>(Json))!;

        var timeout = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < timeout)
        {
            var current = await _client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{document.Id}", Json);
            if (current?.Status == DocumentStatus.Ready)
            {
                return;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException($"El documento {fileName} no se procesó a tiempo.");
    }
}
