using FluentValidation;
using ITPlotter.Application.DTOs.PrintJobs;

namespace ITPlotter.Application.Validators;

public class CreatePrintJobRequestValidator : AbstractValidator<CreatePrintJobRequest>
{
    public CreatePrintJobRequestValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("ID документа обязателен.");

        RuleFor(x => x.PrinterId)
            .NotEmpty().WithMessage("ID принтера обязателен.");

        RuleFor(x => x.Copies)
            .InclusiveBetween(1, 100).WithMessage("Количество копий должно быть от 1 до 100.");

        RuleFor(x => x.PaperFormat)
            .IsInEnum().WithMessage("Некорректный формат бумаги.");
    }
}
