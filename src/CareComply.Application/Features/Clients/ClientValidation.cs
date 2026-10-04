using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using FluentValidation;

namespace CareComply.Application.Features.Clients;

internal static class ClientValidation
{
    public const int MaximumServices = 25;
    public const int MaximumLanguages = 20;
    public const int MaximumSearchLength = 120;
    public const int MaximumPageSize = 100;

    public static bool IsValidEditorVersion(string? value) =>
        value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    public static bool HasUniqueServiceCodes(IReadOnlyList<ClientServiceInput> services) =>
        services.Select(service => service.Code.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == services.Count;

    public static bool HasUniqueLanguageCodes(IReadOnlyList<ClientLanguageInput> languages) =>
        languages.Select(language => language.LanguageCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == languages.Count;

    public static bool HasExactlyOnePrimaryLanguage(IReadOnlyList<ClientLanguageInput> languages) =>
        languages.Count > 0 && languages.Count(language => language.IsPrimary) == 1;

    public static bool IsValidUsPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Any(character =>
                !char.IsAsciiDigit(character)
                && character is not ' ' and not '(' and not ')' and not '-' and not '.'))
        {
            return false;
        }
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 10;
    }

    public static bool IsValidUsPostalCode(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), @"^\d{5}(?:-\d{4})?$");
}

internal sealed class ClientIdentityInputValidator : AbstractValidator<ClientIdentityInput>
{
    public ClientIdentityInputValidator(TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var earliestReasonableBirthDate = today.AddYears(-120);

        RuleFor(input => input.FirstName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("First name is required.")
            .MaximumLength(Client.MaximumNameLength);
        RuleFor(input => input.MiddleName).MaximumLength(Client.MaximumNameLength);
        RuleFor(input => input.LastName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Last name is required.")
            .MaximumLength(Client.MaximumNameLength);
        RuleFor(input => input.PreferredName).MaximumLength(Client.MaximumPreferredNameLength);
        RuleFor(input => input.DateOfBirth)
            .NotEmpty()
            .LessThanOrEqualTo(today)
            .WithMessage("Date of birth cannot be in the future.")
            .GreaterThanOrEqualTo(earliestReasonableBirthDate)
            .WithMessage("Date of birth is outside the supported range.");
        RuleFor(input => input.Gender)
            .IsInEnum()
            .NotEqual(ClientGender.Unknown)
            .WithMessage("Gender is required.");
        RuleFor(input => input.PmiNumber)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("PMI number is required.")
            .Matches("^[0-9]{8}$")
            .WithMessage("PMI number must contain exactly 8 digits.")
            .MaximumLength(Client.MaximumPmiLength);
        RuleFor(input => input.Ssn)
            .Matches(@"^(?!000|666|9\d\d)\d{3}-(?!00)\d{2}-(?!0000)\d{4}$")
            .WithMessage("SSN must use a valid 123-45-6789 format.")
            .When(input => !string.IsNullOrWhiteSpace(input.Ssn));
    }
}

internal sealed class ClientContactInputValidator : AbstractValidator<ClientContactInput>
{
    public ClientContactInputValidator()
    {
        RuleFor(input => input.InsuranceProvider).MaximumLength(Client.MaximumInsuranceLength);
        RuleFor(input => input.Email)
            .MaximumLength(Client.MaximumEmailLength)
            .EmailAddress()
            .When(input => !string.IsNullOrWhiteSpace(input.Email));
        RuleFor(input => input.PrimaryPhone)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Primary phone is required.")
            .MaximumLength(Client.MaximumPhoneLength)
            .Must(ClientValidation.IsValidUsPhone)
            .WithMessage("Primary phone must contain exactly 10 US digits.");
        RuleFor(input => input.SecondaryPhone)
            .MaximumLength(Client.MaximumPhoneLength)
            .Must(ClientValidation.IsValidUsPhone)
            .WithMessage("Secondary phone must contain exactly 10 US digits.")
            .When(input => !string.IsNullOrWhiteSpace(input.SecondaryPhone));
        RuleFor(input => input.AddressLine1)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Street address is required.")
            .MaximumLength(Client.MaximumAddressLineLength);
        RuleFor(input => input.AddressLine2).MaximumLength(Client.MaximumAddressLineLength);
        RuleFor(input => input.City).MaximumLength(Client.MaximumCityLength);
        RuleFor(input => input.State).MaximumLength(Client.MaximumStateLength);
        RuleFor(input => input.PostalCode)
            .MaximumLength(Client.MaximumPostalCodeLength)
            .Must(ClientValidation.IsValidUsPostalCode)
            .WithMessage("Postal code must use 12345 or 12345-6789 format.");
        RuleFor(input => input.County).MaximumLength(Client.MaximumCountyLength);
        RuleFor(input => input.Country)
            .Must(country => string.Equals(country, "United States", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Country must be United States.");
    }
}

internal sealed class ClientCareSetupInputValidator : AbstractValidator<ClientCareSetupInput>
{
    public ClientCareSetupInputValidator()
    {
        RuleFor(input => input.SocDate).NotEmpty();
        RuleFor(input => input.IntakeDate)
            .Must((input, intakeDate) =>
                !intakeDate.HasValue || intakeDate.Value <= input.SocDate)
            .WithMessage("Intake date cannot be after the SOC date.");
        RuleFor(input => input.ReferralSource).MaximumLength(Client.MaximumReferralSourceLength);
        RuleFor(input => input.Location).MaximumLength(Client.MaximumLocationLength);
        RuleFor(input => input.EvvAuthorizedHoursPerWeek)
            .InclusiveBetween(0, Client.MaximumEvvAuthorizedHoursPerWeek)
            .When(input => input.EvvAuthorizedHoursPerWeek.HasValue);
    }
}

internal sealed class ClientSafetyInputValidator : AbstractValidator<ClientSafetyInput>
{
    public ClientSafetyInputValidator()
    {
        RuleFor(input => input.SafetyMeasures).MaximumLength(Client.MaximumTextLength);
        RuleFor(input => input.SpecialEquipment).MaximumLength(Client.MaximumTextLength);
        RuleFor(input => input.Allergies).MaximumLength(Client.MaximumTextLength);
        RuleFor(input => input.OtherServices).MaximumLength(Client.MaximumTextLength);
    }
}

internal sealed class ClientAdditionalProfileInputValidator : AbstractValidator<ClientAdditionalProfileInput>
{
    public ClientAdditionalProfileInputValidator()
    {
        RuleFor(input => input.ProgramType).IsInEnum();
        RuleFor(input => input.ReferralDetails).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.HomeCareRating).MaximumLength(ClientAdditionalProfile.MaximumShortTextLength);
        RuleFor(input => input.MedicalEquipment).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.PcaComplexity).MaximumLength(ClientAdditionalProfile.MaximumShortTextLength);
        RuleFor(input => input.Demographics).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.LivingArrangement).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.PreferredClinic).MaximumLength(ClientAdditionalProfile.MaximumShortTextLength);
        RuleFor(input => input.PreferredHospital).MaximumLength(ClientAdditionalProfile.MaximumShortTextLength);
        RuleFor(input => input.PreferredAmbulance).MaximumLength(ClientAdditionalProfile.MaximumShortTextLength);
        RuleFor(input => input.CaregiverPreference).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.NutritionalRequirements).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.Diet).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.MealSchedule).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.Notes).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
        RuleFor(input => input.AdditionalInformation).MaximumLength(ClientAdditionalProfile.MaximumLongTextLength);
    }
}

internal sealed class ClientServiceInputValidator : AbstractValidator<ClientServiceInput>
{
    public ClientServiceInputValidator()
    {
        RuleFor(input => input.Code)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Service code is required.")
            .MaximumLength(ClientService.MaximumCodeLength);
        RuleFor(input => input.Name)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Service name is required.")
            .MaximumLength(ClientService.MaximumNameLength);
    }
}

internal sealed class ClientLanguageInputValidator : AbstractValidator<ClientLanguageInput>
{
    public ClientLanguageInputValidator()
    {
        RuleFor(input => input.LanguageCode)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Language code is required.")
            .MaximumLength(ClientLanguagePreference.MaximumLanguageCodeLength);
        RuleFor(input => input.LanguageName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Language name is required.")
            .MaximumLength(ClientLanguagePreference.MaximumLanguageNameLength);
    }
}
