using FluentValidation;
using ITPlotter.Application.DTOs.Printers;

namespace ITPlotter.Application.Validators;

public class CreatePrinterRequestValidator : AbstractValidator<CreatePrinterRequest>
{
    public CreatePrinterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Название принтера обязательно.")
            .MaximumLength(200).WithMessage("Название не должно превышать 200 символов.");

        RuleFor(x => x.CupsName)
            .NotEmpty().WithMessage("CUPS имя обязательно.")
            .MaximumLength(200).WithMessage("CUPS имя не должно превышать 200 символов.")
            .Matches(@"^[a-zA-Z0-9_\-]+$").WithMessage("CUPS имя может содержать только латинские буквы, цифры, дефис и подчёркивание.");

        RuleFor(x => x.DeviceUri)
            .NotEmpty().WithMessage("URI устройства обязателен.");

        RuleFor(x => x.DriverUri)
            .NotEmpty().WithMessage("URI драйвера обязателен.");

        RuleFor(x => x.Location)
            .MaximumLength(300).WithMessage("Местоположение не должно превышать 300 символов.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Некорректный тип устройства.");

        RuleFor(x => x.MaxPaperFormat)
            .IsInEnum().WithMessage("Некорректный формат бумаги.");
    }
}
