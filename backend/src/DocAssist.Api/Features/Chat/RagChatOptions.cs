using System.ComponentModel.DataAnnotations;

namespace DocAssist.Api.Features.Chat;

// Sección "Chat" de appsettings.json.
public sealed class RagChatOptions
{
    public const string SectionName = "Chat";

    // Cuántos fragmentos se le pasan al modelo. Con fragmentos de ~1.000 caracteres,
    // 4 son unos 1.200 tokens: caben de sobra en los 4.096 de contexto por defecto de
    // Ollama junto con las instrucciones y la respuesta.
    [Range(1, 10)]
    public int ContextChunks { get; set; } = 4;

    // Baja: queremos respuestas fieles al contexto, no creativas.
    [Range(0.0, 2.0)]
    public float Temperature { get; set; } = 0.1f;

    // Límite de la respuesta. En CPU, a ~6 tokens/s, 400 tokens son más de un minuto.
    [Range(50, 2000)]
    public int MaxOutputTokens { get; set; } = 400;
}
