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

/// <summary>Colmeia com sensores (Hive Box). Guarda a localização e a última leitura de cada sensor.</summary>
/// <remarks>
/// O estado (<c>healthy</c>, <c>warning</c>, <c>danger</c>, <c>offline</c>) não é guardado:
/// é calculado a partir destas leituras, para nunca ficar desatualizado em relação a elas.
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
    public double TempInterior { get; set; }

    /// <summary>Temperatura exterior, em °C.</summary>
    public double TempExterior { get; set; }

    /// <summary>Humidade relativa, em %.</summary>
    public double Humidity { get; set; }

    /// <summary>Peso da colmeia, em kg.</summary>
    public double Weight { get; set; }

    /// <summary>CO₂ em ppm. <c>null</c> no modelo starter, que não tem este sensor.</summary>
    public double? Co2 { get; set; }

    /// <summary>Bateria do módulo, em % (0–100).</summary>
    public int Battery { get; set; }

    /// <summary>Qualidade do sinal de comunicação, em % (0–100).</summary>
    public int Signal { get; set; }

    /// <summary>Indica se o sensor detetou vespa asiática junto à entrada.</summary>
    public bool WaspDetected { get; set; }

    /// <summary>Instante (UTC) da última leitura. Sem leituras há mais de 2 horas, a colmeia fica <c>offline</c>.</summary>
    public DateTime LastUpdate { get; set; }

    public int OwnerId { get; set; }
    public User? Owner { get; set; }
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
