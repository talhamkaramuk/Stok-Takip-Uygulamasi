using FluentValidation;
using STOKIO.Application.Dtos.Counts;

namespace STOKIO.Application.Validation;

public sealed class CreateInventoryCountRequestValidator : AbstractValidator<CreateInventoryCountRequest>
{
    public CreateInventoryCountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.WarehouseId).NotEmpty().When(x => x.WarehouseId.HasValue);
    }
}

public sealed class ScanCountItemRequestValidator : AbstractValidator<ScanCountItemRequest>
{
    public ScanCountItemRequestValidator()
    {
        RuleFor(x => x.Barcode).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100_000);
    }
}
