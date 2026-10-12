using Sales.Bll.Dto;
using Sales.Dal.Interfaces;
using Sales.Domain.Constants;
using Sales.Domain.Exceptions;
using Sales.Domain.Models;

namespace Sales.Bll.Validation;

public interface IDealValidator
{
    Task ValidateCreateAsync(CreateDealDto dto, CancellationToken ct = default);
    Task ValidateAddItemAsync(long dealId, AddDealItemDto dto, CancellationToken ct = default);
    void ValidateDelete(Deal deal);
}

public class DealValidator : IDealValidator
{
    private readonly IUnitOfWork _uow;

    public DealValidator(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task ValidateCreateAsync(CreateDealDto dto, CancellationToken ct = default)
    {
        // 1. Перевірка клієнта
        var client = await _uow.Clients.GetByIdAsync(dto.ClientId, ct);
        if (client == null || client.IsDeleted)
        {
            throw new NotFoundException("Client", dto.ClientId);
        }

        // 2. Перевірка відповідального співробітника
        var owner = await _uow.TeamMembers.GetByIdAsync(dto.OwnerId, ct);
        if (owner == null || owner.IsDeleted)
        {
            throw new NotFoundException("TeamMember", dto.OwnerId);
        }

        if (!owner.IsActive)
        {
            throw new BusinessConflictException($"Responsible team member '{owner.FullName}' is inactive.");
        }

        // 3. Валідація позицій та дублікатів ProductId
        var errors = new Dictionary<string, string[]>();

        if (dto.Items == null || dto.Items.Count == 0)
        {
            errors["Items"] = new[] { "Список позицій угоди не може бути порожнім." };
            throw new ValidationException(errors);
        }

        var duplicates = dto.Items
            .GroupBy(i => i.ProductId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            errors["Items"] = new[] { $"Список позицій містить дубльовані ProductId: {string.Join(", ", duplicates)}." };
            throw new ValidationException(errors);
        }

        // 4. Перевірка активності товарів та ліміту знижки
        foreach (var item in dto.Items)
        {
            var product = await _uow.Products.GetByIdAsync(item.ProductId, ct);
            if (product == null || !product.IsActive)
            {
                throw new BusinessConflictException(
                    $"Product with ID '{item.ProductId}' not found or inactive in catalog copy.");
            }

            var maxDiscount = item.Quantity * product.Price;
            if (item.Discount > maxDiscount)
            {
                throw new ValidationException(
                    $"Знижка {item.Discount} грн перевищує повну вартість позиції {maxDiscount} грн для послуги '{product.Name}'.");
            }
        }
    }

    public async Task ValidateAddItemAsync(long dealId, AddDealItemDto dto, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(dto.ProductId, ct);
        if (product == null || !product.IsActive)
        {
            throw new BusinessConflictException(
                $"Product with ID '{dto.ProductId}' not found or inactive in catalog copy.");
        }

        var maxDiscount = dto.Quantity * product.Price;
        if (dto.Discount > maxDiscount)
        {
            throw new ValidationException(
                $"Знижка {dto.Discount} грн перевищує повну вартість позиції {maxDiscount} грн для послуги '{product.Name}'.");
        }
    }

    public void ValidateDelete(Deal deal)
    {
        if (string.Equals(deal.Stage, DealStatuses.Won, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessConflictException("Неможливо видалити успішно завершену (Won) угоду.");
        }
    }
}
