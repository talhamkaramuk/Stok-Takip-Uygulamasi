using FluentValidation;
using STOKIO.Application.Dtos.Stock;
using STOKIO.Domain.Enums;

namespace STOKIO.Application.Validation;

public sealed class CreateStockMovementRequestValidator : AbstractValidator<CreateStockMovementRequest>
{
    public CreateStockMovementRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Type is StockMovementType.In or StockMovementType.Out);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).When(x => x.Type is StockMovementType.Adjustment or StockMovementType.CountCorrection);
        RuleFor(x => x.Type).Must(x => x is not StockMovementType.TransferIn and not StockMovementType.TransferOut)
            .WithMessage("Transfer hareketleri transfer endpoint'i üzerinden oluşturulmalıdır.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}
