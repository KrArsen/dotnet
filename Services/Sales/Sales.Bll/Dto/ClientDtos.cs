using System.ComponentModel.DataAnnotations;

namespace Sales.Bll.Dto;

public class CreateClientDto
{
    [Required(ErrorMessage = "Назва компанії є обов'язковою.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Назва компанії має містити від 2 до 200 символів.")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Контактна особа є обов'язковою.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Ім'я контактної особи має містити від 2 до 150 символів.")]
    public string ContactPerson { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email є обов'язковим.")]
    [EmailAddress(ErrorMessage = "Некоректний формат електронної пошти.")]
    [StringLength(150, ErrorMessage = "Email не повинен перевищувати 150 символів.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Номер телефону є обов'язковим.")]
    [Phone(ErrorMessage = "Некоректний формат номера телефону.")]
    [StringLength(50, ErrorMessage = "Телефон не повинен перевищувати 50 символів.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Галузь не повинна перевищувати 100 символів.")]
    public string? Industry { get; set; }
}

public class UpdateClientDto
{
    [Required(ErrorMessage = "Назва компанії є обов'язковою.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Назва компанії має містити від 2 до 200 символів.")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Контактна особа є обов'язковою.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Ім'я контактної особи має містити від 2 до 150 символів.")]
    public string ContactPerson { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email є обов'язковим.")]
    [EmailAddress(ErrorMessage = "Некоректний формат електронної пошти.")]
    [StringLength(150, ErrorMessage = "Email не повинен перевищувати 150 символів.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Номер телефону є обов'язковим.")]
    [Phone(ErrorMessage = "Некоректний формат номера телефону.")]
    [StringLength(50, ErrorMessage = "Телефон не повинен перевищувати 50 символів.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Галузь не повинна перевищувати 100 символів.")]
    public string? Industry { get; set; }

    [Required(ErrorMessage = "RowVersion є обов'язковим для оптимістичного блокування.")]
    public string RowVersion { get; set; } = string.Empty;
}

public class ClientQueryDto
{
    public string? Search { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Номер сторінки має бути >= 1.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Розмір сторінки має бути від 1 до 100.")]
    public int PageSize { get; set; } = 10;
}

public record ClientDto(
    long Id,
    string CompanyName,
    string ContactPerson,
    string Email,
    string Phone,
    string? Industry,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    string RowVersion);
