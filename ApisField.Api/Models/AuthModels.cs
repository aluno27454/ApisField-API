using System.ComponentModel.DataAnnotations;
using ApisField.Api.Data;

namespace ApisField.Api.Models;

// Modelos de entrada e saída da autenticação (docs/CONTRATO.md).
// As anotações de validação (nos parâmetros do construtor, como o MVC exige nos records) fazem com que o [ApiController] responda 400 com ValidationProblemDetails
// (os erros por campo em "errors") antes de o controller ser executado.

/// <summary>Corpo de POST /api/auth/register.</summary>
public record RegisterRequest(
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "O nome tem de ter entre 2 e 60 caracteres.")]
    string Name,

    [Required(ErrorMessage = "O email é obrigatório.")]
    [EmailAddress(ErrorMessage = "O email não é válido.")]
    [StringLength(120, ErrorMessage = "O email tem no máximo 120 caracteres.")]
    string Email,

    [Required(ErrorMessage = "A palavra-passe é obrigatória.")]
    // Pelo menos 8 caracteres, com pelo menos uma letra e um dígito (contrato).
    [RegularExpression(@"^(?=.*\p{L})(?=.*\d).{8,}$",
        ErrorMessage = "A palavra-passe tem de ter pelo menos 8 caracteres, com letras e números.")]
    string Password);

/// <summary>Corpo de POST /api/auth/login. Só se exige que os campos venham preenchidos.</summary>
public record LoginRequest(
    [Required(ErrorMessage = "O email é obrigatório.")] string Email,
    [Required(ErrorMessage = "A palavra-passe é obrigatória.")] string Password);

/// <summary>Utilizador no formato do contrato. Nunca inclui o hash da palavra-passe.</summary>
public record UserResponse(int Id, string Name, string Email, string Role)
{
    public static UserResponse From(User u) => new(u.Id, u.Name, u.Email, u.Role);
}

/// <summary>Resposta do registo e do login.</summary>
public record AuthResult(string Token, UserResponse User);
