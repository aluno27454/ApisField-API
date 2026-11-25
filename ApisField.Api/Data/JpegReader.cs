using Microsoft.Net.Http.Headers;

namespace ApisField.Api.Data;

/// <summary>Lê e valida uma fotografia enviada como corpo binário de um pedido.</summary>
public static class JpegReader
{
    /// <summary>Tamanho máximo de uma fotografia: 2 MB (contrato).</summary>
    private const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>Lê o corpo do pedido.</summary>
    /// <returns>Os bytes da imagem, ou a mensagem de erro a devolver no 400.</returns>
    public static async Task<(byte[]? Jpeg, string? Error)> ReadAsync(HttpRequest request)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var type) ||
            !type.MediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            return (null, "A fotografia tem de ser JPEG.");

        // Lê no máximo um byte além do limite: chega para saber que passou, sem guardar o resto em memória.
        var buffer = new byte[MaxBytes + 1];
        var length = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false);
        if (length > MaxBytes) return (null, "A fotografia tem no máximo 2 MB.");

        var jpeg = buffer.AsSpan(0, length);
        // Um JPEG começa sempre por FF D8 FF. O Content-Type, só por si, não garante nada.
        return jpeg.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])
            ? (jpeg.ToArray(), null)
            : (null, "A fotografia tem de ser JPEG.");
    }
}
