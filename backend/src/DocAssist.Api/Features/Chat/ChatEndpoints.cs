using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace DocAssist.Api.Features.Chat;

public static class ChatEndpoints
{
    private const string LogCategory = "DocAssist.Api.Chat";

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat").WithTags("Chat");

        group.MapPost("/", Ask)
            .WithSummary("Responde a una pregunta con los documentos de la tienda (respuesta completa)");

        group.MapPost("/stream", Stream)
            .WithSummary("Igual que /api/chat, pero envía la respuesta poco a poco con Server-Sent Events");

        return app;
    }

    private static async Task<Ok<AskResponse>> Ask(
        AskRequest request,
        RagChatService chatService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await chatService.AskAsync(request, cancellationToken));

    // Server-Sent Events: la respuesta HTTP se queda abierta y el servidor va escribiendo
    // eventos de texto ("event: delta\ndata: {...}\n\n"). Con un modelo en CPU, el usuario
    // ve las primeras palabras en segundos en vez de esperar a la respuesta entera.
    private static ServerSentEventsResult<string> Stream(
        AskRequest request,
        RagChatService chatService,
        IOptions<JsonOptions> jsonOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var events = chatService.StreamAsync(request, cancellationToken);
        return TypedResults.ServerSentEvents(
            ToServerSentEvents(events, jsonOptions.Value.SerializerOptions, loggerFactory.CreateLogger(LogCategory), cancellationToken));
    }

    // Convierte cada evento en "event: <tipo>" + "data: <JSON>". Si algo falla a mitad,
    // ya no se puede cambiar el código HTTP (200 ya se envió): se manda un evento "error".
    private static async IAsyncEnumerable<SseItem<string>> ToServerSentEvents(
        IAsyncEnumerable<ChatStreamEvent> events,
        JsonSerializerOptions jsonOptions,
        ILogger logger,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = events.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            // C# no permite "yield return" dentro de un try con catch: se separa en dos pasos.
            ChatStreamEvent current;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    yield break;
                }
                current = enumerator.Current;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Chat streaming failed");
                current = new ErrorEvent("No se pudo generar la respuesta. ¿Está arrancado Ollama?");
            }

            // GetType(): se serializa el tipo real (DeltaEvent...), no el abstracto.
            yield return new SseItem<string>(
                JsonSerializer.Serialize(current, current.GetType(), jsonOptions), current.Type);

            if (current is ErrorEvent)
            {
                yield break;
            }
        }
    }
}
