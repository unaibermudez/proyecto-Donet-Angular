using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace DocAssist.Api.Tests.Infrastructure;

// Sustituye al LLM en los tests: siempre responde lo mismo y guarda los mensajes que
// recibió, para poder comprobar qué prompt se le envió.
public sealed class FakeChatClient : IChatClient
{
    public const string Answer = "Respuesta de prueba sacada del contexto [1].";

    // Una pregunta con esta marca hace fallar al modelo, para probar los errores.
    public const string FailureMarker = "FAIL-CHAT";

    public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

    public ChatOptions? LastOptions { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Record(messages, options);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer)));
    }

    // Devuelve la respuesta palabra a palabra, como haría un modelo real.
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Record(messages, options);
        var words = Answer.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            await Task.Yield();
            var isLast = i == words.Length - 1;
            yield return new ChatResponseUpdate(ChatRole.Assistant, isLast ? words[i] : words[i] + " ");
        }
    }

    private void Record(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        LastMessages = messages.ToList();
        LastOptions = options;
        if (LastMessages.Any(m => m.Text.Contains(FailureMarker)))
        {
            throw new InvalidOperationException("Fake chat failure.");
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
