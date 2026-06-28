using FluentValidation;
using STOKIO.Application.Dtos.Operations;

namespace STOKIO.Application.Validation;

public sealed class OperationItemRequestValidator : AbstractValidator<OperationItemRequest>
{
    public OperationItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1_000_000);
    }
}

public sealed class CreateSalesOrderRequestValidator : AbstractValidator<CreateSalesOrderRequest>
{
    public CreateSalesOrderRequestValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new OperationItemRequestValidator());
    }
}

public sealed class CreatePurchaseRequestRequestValidator : AbstractValidator<CreatePurchaseRequestRequest>
{
    public CreatePurchaseRequestRequestValidator()
    {
        RuleFor(x => x.SupplierName).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new OperationItemRequestValidator());
    }
}

public sealed class ReceivePurchaseRequestRequestValidator : AbstractValidator<ReceivePurchaseRequestRequest>
{
    public ReceivePurchaseRequestRequestValidator()
    {
        When(x => x.Items is not null, () =>
        {
            RuleFor(x => x.Items).NotEmpty();
            RuleForEach(x => x.Items!).SetValidator(new OperationItemRequestValidator());
        });
    }
}

public sealed class CreateShipmentRequestValidator : AbstractValidator<CreateShipmentRequest>
{
    public CreateShipmentRequestValidator()
    {
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(180);
        RuleFor(x => x.TrackingNumber).MaximumLength(80);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new OperationItemRequestValidator());
    }
}

public sealed class CreateReturnRequestRequestValidator : AbstractValidator<CreateReturnRequestRequest>
{
    public CreateReturnRequestRequestValidator()
    {
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new OperationItemRequestValidator());
    }
}
