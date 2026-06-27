using FluentValidation;
using STOKIO.Application.Dtos.Users;
using STOKIO.Domain.Enums;

namespace STOKIO.Application.Validation;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(180);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(10)
            .Must(value => value.Any(char.IsDigit) && value.Any(char.IsUpper) && value.Any(char.IsLower))
            .WithMessage("Password must contain upper-case, lower-case, and numeric characters.");
        RuleFor(x => x.Role)
            .Must(role => role is UserRole.Manager or UserRole.Staff)
            .WithMessage("Only Manager or Staff users can be created from tenant management.");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Role)
            .Must(role => role is UserRole.Manager or UserRole.Staff)
            .WithMessage("Only Manager or Staff roles can be assigned from tenant management.");
    }
}

