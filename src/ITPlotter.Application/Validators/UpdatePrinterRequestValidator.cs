using FluentValidation;
using ITPlotter.Application.DTOs.Printers;

namespace ITPlotter.Application.Validators;

public class UpdatePrinterRequestValidator : AbstractValidator<UpdatePrinterRequest>
{
    public UpdatePrinterRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Название не должно превышать 200 символов.")
            .When(x => x.Name != null);

        RuleFor(x => x.Location)
            .MaximumLength(300).WithMessage("Местоположение не должно превышать 300 символов.")
            .When(x => x.Location != null);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Некорректный тип устройства.")
            .When(x => x.Type.HasValue);

        RuleFor(x => x.MaxPaperFormat)
            .IsInEnum().WithMessage("Некорректный формат бумаги.")
            .When(x => x.MaxPaperFormat.HasValue);
    }
}
