namespace ApisField.Api.Data;

// Entidades persistidas pelo EF Core. Não são expostas diretamente na API:
// as respostas JSON seguem os modelos de docs/CONTRATO.md e são construídas a partir destas classes.
// Os valores enumerados (role, model, type...) ficam como string para coincidirem com o contrato
// sem conversões; a validação dos valores permitidos é feita à entrada, nos controllers.

/// <summary>Utilizador da aplicação (apicultor ou administrador).</summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Nome apresentado na app (2–60 caracteres).</summary>
    public required string Name { get; set; }

    /// <summary>Email de login. Único na base de dados (ver <see cref="AppDbContext"/>).</summary>
    public required string Email { get; set; }

    /// <summary>Hash da palavra-passe no formato de <see cref="PasswordHasher"/>. Nunca a palavra-passe em claro.</summary>
    public required string PasswordHash { get; set; }

    /// <summary><c>"user"</c> ou <c>"admin"</c>. O admin vê e gere os dados de todos os utilizadores.</summary>
    public string Role { get; set; } = "user";
}

/// <summary>Colmeia com sensores (Hive Box). Guarda a localização e os valores de referência de cada sensor.</summary>
/// <remarks>
/// Nenhuma colmeia tem hardware real, por isso as leituras são simuladas (<see cref="HiveSimulator"/>):
/// partem dos valores guardados aqui e variam com a hora do dia. O estado (<c>healthy</c>, <c>warning</c>,
/// <c>danger</c>, <c>offline</c>) também não é guardado: é calculado a partir da leitura (<see cref="HiveRules"/>).
/// </remarks>
public class Hive
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary><c>"starter"</c> ou <c>"pro"</c>. Só o modelo Pro tem sensor de CO₂.</summary>
    public string Model { get; set; } = "starter";

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Descrição curta do local (ex.: "Asseiceira"), até 100 caracteres.</summary>
    public string Label { get; set; } = "";

    /// <summary>Temperatura no interior da colmeia, em °C.</summary>
    public double TempInterior { get; set; } = 34.8;

    /// <summary>Temperatura exterior, em °C.</summary>
    public double TempExterior { get; set; } = 14;

    /// <summary>Humidade relativa, em %.</summary>
    public double Humidity { get; set; } = 60;

    /// <summary>Peso da colmeia, em kg.</summary>
    public double Weight { get; set; } = 30;

    /// <summary>CO₂ em ppm. Sem valor, o modelo Pro usa <see cref="HiveSimulator.DefaultCo2"/>; o starter não tem este sensor.</summary>
    public double? Co2 { get; set; }

    /// <summary>Bateria do módulo, em % (0–100).</summary>
    public int Battery { get; set; } = 100;

    /// <summary>Qualidade do sinal de comunicação, em % (0–100).</summary>
    public int Signal { get; set; } = 80;

    /// <summary>Indica se o sensor detetou vespa asiática junto à entrada.</summary>
    public bool WaspDetected { get; set; }

    /// <summary>Instante (UTC) em que a colmeia foi registada. O histórico não tem dados antes deste dia.</summary>
    public DateTime CreatedAt { get; set; }

    public int OwnerId { get; set; }
    public User? Owner { get; set; }
}

/// <summary>Alerta gerado quando uma leitura ultrapassa um limiar (ver <see cref="HiveRules"/>).</summary>
public class Alert
{
    public int Id { get; set; }

    public int HiveId { get; set; }
    public Hive? Hive { get; set; }

    /// <summary><c>"wasp"</c>, <c>"temperature"</c>, <c>"co2"</c>, <c>"battery"</c> ou <c>"signal"</c>.</summary>
    public required string Type { get; set; }

    /// <summary><c>"warning"</c> ou <c>"danger"</c>.</summary>
    public required string Severity { get; set; }

    /// <summary>Texto para o utilizador, com o valor lido no momento em que o alerta foi gerado.</summary>
    public required string Message { get; set; }

    /// <summary>Instante (UTC) em que a condição foi detetada.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Indica se o utilizador já viu o alerta.</summary>
    public bool Read { get; set; }

    /// <summary>
    /// <c>true</c> enquanto a condição se mantiver. Impede alertas repetidos para a mesma condição;
    /// quando a condição passa, fica <c>false</c> e uma nova ocorrência gera um alerta novo. Não sai na API.
    /// </summary>
    public bool Active { get; set; }
}

/// <summary>Registo de uma inspeção feita pelo apicultor a uma colmeia.</summary>
public class Visit
{
    public int Id { get; set; }

    public int HiveId { get; set; }
    public Hive? Hive { get; set; }

    /// <summary>Data e hora (UTC) da inspeção. Não pode estar mais de 5 minutos no futuro.</summary>
    public DateTime Date { get; set; }

    /// <summary>Indica se a rainha foi vista durante a inspeção.</summary>
    public bool QueenSeen { get; set; }

    /// <summary>Quantidade de criação: <c>"fraca"</c>, <c>"media"</c> ou <c>"forte"</c>.</summary>
    public required string BroodLevel { get; set; }

    /// <summary>Comportamento da colónia: <c>"calma"</c>, <c>"nervosa"</c> ou <c>"agressiva"</c>.</summary>
    public required string Temperament { get; set; }

    /// <summary>Tratamento aplicado (ex.: contra a varroa), até 200 caracteres.</summary>
    public string? Treatment { get; set; }

    /// <summary>Observações livres, até 500 caracteres.</summary>
    public string? Notes { get; set; }

    /// <summary>Posição GPS de onde a visita foi registada, se a app a enviou.</summary>
    public double? Latitude { get; set; }

    /// <inheritdoc cref="Latitude"/>
    public double? Longitude { get; set; }

    public int AuthorId { get; set; }
    public User? Author { get; set; }

    public List<Photo> Photos { get; set; } = [];
}

/// <summary>Fotografia de uma visita. A base de dados guarda só o nome do ficheiro; a imagem fica em disco (<see cref="PhotoStore"/>).</summary>
public class Photo
{
    public int Id { get; set; }

    public int VisitId { get; set; }
    public Visit? Visit { get; set; }

    /// <summary>Nome do ficheiro na pasta das fotografias, gerado pela API.</summary>
    public required string FileName { get; set; }
}

/// <summary>Avistamento de vespa asiática (<i>Vespa velutina</i>) ou de um ninho, partilhado entre todos os utilizadores.</summary>
public class WaspSighting
{
    public int Id { get; set; }

    /// <summary><c>"wasp"</c> (vespa) ou <c>"nest"</c> (ninho).</summary>
    public required string Type { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Precisão do GPS no momento do registo, em metros.</summary>
    public double? Accuracy { get; set; }

    public string? Notes { get; set; }

    /// <summary>Data e hora (UTC) do avistamento.</summary>
    public DateTime Date { get; set; }

    /// <summary>Autor do registo. Só ele (ou o admin) pode editar ou apagar o avistamento.</summary>
    public int AuthorId { get; set; }
    public User? Author { get; set; }
}
