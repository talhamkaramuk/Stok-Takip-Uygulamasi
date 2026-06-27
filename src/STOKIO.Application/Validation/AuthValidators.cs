using FluentValidation;
using STOKIO.Application.Dtos.Auth;

namespace STOKIO.Application.Validation;

public sealed class RegisterTenantRequestValidator : AbstractValidator<RegisterTenantRequest>
{
    public RegisterTenantRequestValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.TaxNumber).MaximumLength(50);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.TenantSlug)
            .NotEmpty()
            .Matches("^[a-zA-Z0-9-]{3,64}$")
            .WithMessage("Tenant slug must contain only letters, numbers, and hyphens.");
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(180);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(10)
            .Must(value => value.Any(char.IsDigit) && value.Any(char.IsUpper) && value.Any(char.IsLower))
            .WithMessage("Password must contain upper-case, lower-case, and numeric characters.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(180);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
    }
}
