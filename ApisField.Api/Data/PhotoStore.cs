namespace ApisField.Api.Data;

/// <summary>Guarda os ficheiros das fotografias em disco, na pasta <c>Photos:Path</c>.</summary>
/// <remarks>
/// Os nomes são sempre gerados aqui e nunca vêm do pedido, para ninguém conseguir ler ou escrever
/// fora da pasta. Um caminho relativo conta a partir da pasta da API.
/// </remarks>
public class PhotoStore(IConfiguration configuration, IWebHostEnvironment environment)
{
    private readonly string _folder = Path.Combine(environment.ContentRootPath, configuration["Photos:Path"] ?? "fotos");

    /// <summary>Grava a imagem num ficheiro novo.</summary>
    /// <returns>O nome do ficheiro, a guardar em <see cref="Photo.FileName"/>.</returns>
    public async Task<string> SaveAsync(byte[] jpeg)
    {
        Directory.CreateDirectory(_folder);
        var fileName = $"{Guid.NewGuid():N}.jpg";
        await File.WriteAllBytesAsync(PathOf(fileName), jpeg);
        return fileName;
    }

    /// <summary>Caminho completo do ficheiro, ou <c>null</c> se já não existir em disco.</summary>
    public string? Find(string fileName) => File.Exists(PathOf(fileName)) ? PathOf(fileName) : null;

    /// <summary>Apaga os ficheiros indicados. Os que já não existirem são ignorados.</summary>
    /// <remarks>
    /// Chamar só depois de gravar na base de dados: se algo falhar a meio, sobra um ficheiro sem uso,
    /// o que é melhor do que um registo a apontar para uma fotografia que não existe.
    /// </remarks>
    public void Delete(IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
            if (Find(fileName) is { } path)
                File.Delete(path);
    }

    private string PathOf(string fileName) => Path.Combine(_folder, fileName);
}
