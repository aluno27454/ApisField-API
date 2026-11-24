using System.ComponentModel.DataAnnotations;
using ApisField.Api.Data;

namespace ApisField.Api.Models;

// Modelos de entrada e saída das visitas (docs/CONTRATO.md).

/// <summary>Corpo de POST /api/hives/{hiveId}/visits e de PUT /api/visits/{id}.</summary>
/// <remarks>
/// A data e <c>queenSeen</c> são anuláveis só para se distinguir um campo em falta do valor por omissão
/// (0001-01-01 ou <c>false</c>), que passaria como válido.
/// </remarks>
public record VisitRequest(
    [Required(ErrorMessage = "A data é obrigatória.")]
    DateTime? Date,

    [Required(ErrorMessage = "Indica se a rainha foi vista.")]
    bool? QueenSeen,

    [AllowedValues("fraca", "media", "forte", ErrorMessage = "A criação tem de ser fraca, media ou forte.")]
    string? BroodLevel,

    [AllowedValues("calma", "nervosa", "agressiva", ErrorMessage = "O temperamento tem de ser calma, nervosa ou agressiva.")]
    string? Temperament,

    [StringLength(200, ErrorMessage = "O tratamento tem no máximo 200 caracteres.")]
    string? Treatment,

    [StringLength(500, ErrorMessage = "As notas têm no máximo 500 caracteres.")]
    string? Notes,

    [Range(-90d, 90d, ErrorMessage = "A latitude tem de estar entre -90 e 90.")]
    double? Latitude,

    [Range(-180d, 180d, ErrorMessage = "A longitude tem de estar entre -180 e 180.")]
    double? Longitude) : IValidatableObject
{
    /// <summary>Tolerância para relógios adiantados: a app pode ter a hora uns minutos à frente da do servidor.</summary>
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    /// <summary>A regra da data depende da hora atual, por isso não cabe numa anotação.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DateUtc() > DateTime.UtcNow + FutureTolerance)
            yield return new ValidationResult("A data não pode ser no futuro.", [nameof(Date)]);
    }

    /// <summary>Passa os dados do pedido, já validados, para a entidade.</summary>
    public void ApplyTo(Visit visit)
    {
        visit.Date = DateUtc();
        visit.QueenSeen = QueenSeen!.Value;
        visit.BroodLevel = BroodLevel!;
        visit.Temperament = Temperament!;
        visit.Treatment = Clean(Treatment);
        visit.Notes = Clean(Notes);
        visit.Latitude = Latitude;
        visit.Longitude = Longitude;
    }

    /// <summary>A data em UTC. Sem fuso no pedido, assume-se UTC (contrato), e não a hora local do servidor.</summary>
    private DateTime DateUtc() => Date!.Value.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(Date.Value, DateTimeKind.Utc)
        : Date.Value.ToUniversalTime();

    /// <summary>Um texto só com espaços conta como vazio.</summary>
    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Visita no formato do contrato.</summary>
public record VisitResponse(int Id, int HiveId, string HiveName, DateTime Date, bool QueenSeen, string BroodLevel,
    string Temperament, string? Treatment, string? Notes, double? Latitude, double? Longitude,
    int[] PhotoIds, int AuthorId)
{
    /// <summary>Exige a colmeia e as fotografias carregadas (<c>Include</c>).</summary>
    public static VisitResponse From(Visit v) => new(v.Id, v.HiveId, v.Hive!.Name, v.Date, v.QueenSeen, v.BroodLevel,
        v.Temperament, v.Treatment, v.Notes, v.Latitude, v.Longitude,
        [.. v.Photos.Select(p => p.Id).Order()], v.AuthorId);
}
