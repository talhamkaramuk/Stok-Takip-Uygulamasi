using FluentValidation;
using STOKIO.Application.Dtos.Products;

namespace STOKIO.Application.Validation;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.CategoryName).MaximumLength(120);
        RuleFor(x => x.CriticalStockLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Barcodes).NotNull();
        RuleForEach(x => x.Barcodes).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Barcodes)
            .Must(values => values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count)
            .When(x => x.Barcodes is not null)
            .WithMessage("Barcodes must be unique in the request.");
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.CategoryName).MaximumLength(120);
        RuleFor(x => x.CriticalStockLevel).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddBarcodeRequestValidator : AbstractValidator<AddBarcodeRequest>
{
    public AddBarcodeRequestValidator()
    {
        RuleFor(x => x.Barcode).NotEmpty().MaximumLength(128);
    }
}

