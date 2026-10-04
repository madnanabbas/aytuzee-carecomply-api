using CareComply.Application.Abstractions.Employees;
using FluentValidation;

namespace CareComply.Application.Features.Employees;

public sealed class EmployeeRequestValidation
{
    private readonly EmployeeSaveRequestValidator createValidator = new(requireSsn: true);
    private readonly EmployeeSaveRequestValidator updateValidator = new(requireSsn: false);

    public IReadOnlyDictionary<string, string[]> Validate(
        SaveEmployeeRequest request,
        bool creating)
    {
        var result = (creating ? createValidator : updateValidator).Validate(request);
        return result.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(failure => failure.ErrorMessage)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private sealed class EmployeeSaveRequestValidator : AbstractValidator<SaveEmployeeRequest>
    {
        public EmployeeSaveRequestValidator(bool requireSsn)
        {
            RuleFor(request => request).Custom((request, context) =>
                ValidateRequest(request, context, requireSsn));
        }

        private static void ValidateRequest(
            SaveEmployeeRequest request,
            ValidationContext<SaveEmployeeRequest> context,
            bool requireSsn)
        {
            AddRequiredErrors(request, context, requireSsn);
            if (!requireSsn && (string.IsNullOrWhiteSpace(request.EditorVersion)
                || request.EditorVersion.Length != 64))
            {
                context.AddFailure(nameof(request.EditorVersion), "Editor version is required and must contain 64 characters.");
            }
            AddAdditionalInformationErrors(request, context);
            AddPayerErrors(request, context);
            AddBackgroundErrors(request, context);
            AddTrainingErrors(request, context);
            AddContactErrors(request, context);
        }

        private static void AddRequiredErrors(
            SaveEmployeeRequest request,
            ValidationContext<SaveEmployeeRequest> context,
            bool requireSsn)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName))
            {
                context.AddFailure(nameof(request.FirstName), "First name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.LastName))
            {
                context.AddFailure(nameof(request.LastName), "Last name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Email)
                || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email))
            {
                context.AddFailure(nameof(request.Email), "A valid email is required.");
            }

            if (string.IsNullOrWhiteSpace(request.JobTitle))
            {
                context.AddFailure(nameof(request.JobTitle), "Title is required.");
            }

            if (request.HireDate is null)
            {
                context.AddFailure(nameof(request.HireDate), "Hire date is required.");
            }

            if (request.DateOfBirth is null)
            {
                context.AddFailure(nameof(request.DateOfBirth), "Date of birth is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Gender))
            {
                context.AddFailure(nameof(request.Gender), "Gender is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Count(char.IsDigit) != 10)
            {
                context.AddFailure(nameof(request.Phone), "Enter a 10-digit phone number.");
            }

            if (string.IsNullOrWhiteSpace(request.StreetAddress))
            {
                context.AddFailure(nameof(request.StreetAddress), "Street address is required.");
            }

            if (requireSsn && string.IsNullOrWhiteSpace(request.Ssn))
            {
                context.AddFailure(nameof(request.Ssn), "SSN is required when creating an employee.");
            }

            if (!string.IsNullOrWhiteSpace(request.Ssn) && request.Ssn.Count(char.IsDigit) != 9)
            {
                context.AddFailure(nameof(request.Ssn), "SSN must contain 9 digits.");
            }

            if (!string.IsNullOrWhiteSpace(request.EvvEmail)
                && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.EvvEmail))
            {
                context.AddFailure(nameof(request.EvvEmail), "Enter a valid EVV email.");
            }

            if (!string.IsNullOrWhiteSpace(request.Npi)
                && (!request.Npi.All(char.IsDigit) || request.Npi.Length != 10))
            {
                context.AddFailure(nameof(request.Npi), "NPI must contain 10 digits.");
            }
        }

        private static void AddAdditionalInformationErrors(
            SaveEmployeeRequest request,
            ValidationContext<SaveEmployeeRequest> context)
        {
            if (request.HasUsedDifferentName && string.IsNullOrWhiteSpace(request.PreviousName))
            {
                context.AddFailure(nameof(request.PreviousName), "Previous name is required when a different name was used.");
            }

            if (request.HeightInches is <= 0 or > 120)
            {
                context.AddFailure(nameof(request.HeightInches), "Height must be between 1 and 120 inches.");
            }

            if (request.WeightPounds is <= 0 or > 1500)
            {
                context.AddFailure(nameof(request.WeightPounds), "Weight must be between 1 and 1,500 pounds.");
            }

            if (request.HasWorkingHourRestrictions
                && (request.AvailableStartTime is null || request.AvailableEndTime is null))
            {
                context.AddFailure(nameof(request.AvailableStartTime), "Start and end times are required when working hours are restricted.");
            }

            if (request.AvailableStartTime is not null
                && request.AvailableEndTime is not null
                && request.AvailableStartTime >= request.AvailableEndTime)
            {
                context.AddFailure(nameof(request.AvailableEndTime), "End time must be after start time.");
            }

            if (!request.CanWorkWithSmokers && string.IsNullOrWhiteSpace(request.SmokerRestrictionExplanation))
            {
                context.AddFailure(nameof(request.SmokerRestrictionExplanation), "Explain the smoking restriction.");
            }

            if (request.IsMilitaryVeteran && string.IsNullOrWhiteSpace(request.MilitaryServiceExplanation))
            {
                context.AddFailure(nameof(request.MilitaryServiceExplanation), "Provide military service details.");
            }

            if (!request.CanWorkWithPets && string.IsNullOrWhiteSpace(request.PetRestrictionExplanation))
            {
                context.AddFailure(nameof(request.PetRestrictionExplanation), "Explain the pet restriction.");
            }

            if (request.HasOtherJob && string.IsNullOrWhiteSpace(request.OtherEmployerName))
            {
                context.AddFailure(nameof(request.OtherEmployerName), "Employer name is required when the employee has another job.");
            }

            if (request.PreviousAddresses?.Count > 10)
            {
                context.AddFailure(nameof(request.PreviousAddresses), "No more than 10 previous addresses may be recorded.");
            }
        }

        private static void AddPayerErrors(
            SaveEmployeeRequest request,
            ValidationContext<SaveEmployeeRequest> context)
        {
            if (request.PayerAffiliations?.Count > 20)
            {
                context.AddFailure(nameof(request.PayerAffiliations), "No more than 20 payer affiliations may be recorded.");
            }

            if (request.InsuranceAffiliationRequired && request.PayerAffiliations is not { Count: > 0 })
            {
                context.AddFailure(nameof(request.PayerAffiliations), "Add at least one payer affiliation when insurance affiliation is required.");
            }

            foreach (var affiliation in request.PayerAffiliations ?? [])
            {
                if (string.IsNullOrWhiteSpace(affiliation.Company)
                    || string.IsNullOrWhiteSpace(affiliation.Payer))
                {
                    context.AddFailure(nameof(request.PayerAffiliations), "Company and payer are required for every affiliation.");
                }

                if (affiliation.AffiliationDate is not null
                    && affiliation.AffiliationEndDate is not null
                    && affiliation.AffiliationEndDate < affiliation.AffiliationDate)
                {
                    context.AddFailure(nameof(request.PayerAffiliations), "Affiliation end date cannot be before the affiliation date.");
                }
            }
        }

        private static void AddBackgroundErrors(
            SaveEmployeeRequest request,
            ValidationContext<SaveEmployeeRequest> context)
        {
            var background = request.BackgroundInformation;
            if (background?.Checks?.Count > 20)
            {
                context.AddFailure(nameof(request.BackgroundInformation), "No more than 20 background checks may be recorded.");
            }

            foreach (var check in background?.Checks ?? [])
            {
                if (string.IsNullOrWhiteSpace(check.LicenseType)
                    || string.IsNullOrWhiteSpace(check.StudyType))
                {
                    context.AddFailure(nameof(request.BackgroundInformation), "License type and study type are required for every background check.");
                }

                if (check.LicenseType?.Length > 100 || check.StudyType?.Length > 100)
                {
                    context.AddFailure(nameof(request.BackgroundInformation), "Background check license and study types cannot exceed 100 characters.");
                }

                if (check.Received && check.ReceivedDate is null)
                {
                    context.AddFailure(nameof(request.BackgroundInformation), "Received date is required when a background check is marked received.");
                }

                if (check.CheckDate is not null
                    && check.ReceivedDate is not null
                    && check.ReceivedDate < check.CheckDate)
                {
                    context.AddFailure(nameof(request.BackgroundInformation), "Background check received date cannot be before its check date.");
                }
            }

            if (background?.MantouxGivenDate is not null
                && background.MantouxCheckedDate is not null
                && background.MantouxCheckedDate < background.MantouxGivenDate)
            {
                context.AddFailure(nameof(request.BackgroundInformation), "Mantoux checked date cannot be before the given date.");
            }

            if (background?.MantouxReceived == true
                && (background.MantouxGivenDate is null || background.MantouxCheckedDate is null))
            {
                context.AddFailure(nameof(request.BackgroundInformation), "Mantoux given and checked dates are required when the result is received.");
            }

            if (background?.MedicalTestingReceived == true && background.MedicalTestingDate is null)
            {
                context.AddFailure(nameof(request.BackgroundInformation), "Medical testing date is required when the result is received.");
            }
        }

        private static void AddTrainingErrors(
            SaveEmployeeRequest request,
            ValidationContext<SaveEmployeeRequest> context)
        {
            var training = request.TrainingInformation;
            if (training?.Courses?.Count > 50 || training?.Trainings?.Count > 50)
            {
                context.AddFailure(nameof(request.TrainingInformation), "No more than 50 courses and 50 training records may be recorded.");
            }

            foreach (var course in training?.Courses ?? [])
            {
                if (string.IsNullOrWhiteSpace(course.CourseName))
                {
                    context.AddFailure(nameof(request.TrainingInformation), "Course name is required for every course.");
                }

                if (course.CourseName?.Length > 200 || course.Notes?.Length > 1000)
                {
                    context.AddFailure(nameof(request.TrainingInformation), "Course name or notes exceed the allowed length.");
                }

                if (course.QualifyingPoints is < 0 or > 10000 || course.ObtainedPoints is < 0 or > 10000)
                {
                    context.AddFailure(nameof(request.TrainingInformation), "Course points must be between 0 and 10,000.");
                }
            }

            foreach (var trainingItem in training?.Trainings ?? [])
            {
                if (string.IsNullOrWhiteSpace(trainingItem.TrainingName))
                {
                    context.AddFailure(nameof(request.TrainingInformation), "Training name is required for every training record.");
                }

                if (trainingItem.TrainingName?.Length > 200
                    || trainingItem.JobTitle?.Length > 200
                    || trainingItem.Notes?.Length > 1000)
                {
                    context.AddFailure(nameof(request.TrainingInformation), "Training name, job title or notes exceed the allowed length.");
                }

                if (trainingItem.QualifyingPoints is < 0 or > 10000
                    || trainingItem.ObtainedPoints is < 0 or > 10000)
                {
                    context.AddFailure(nameof(request.TrainingInformation), "Training points must be between 0 and 10,000.");
                }

                if (trainingItem.CompletedOn is not null
                    && trainingItem.ExpiresOn is not null
                    && trainingItem.ExpiresOn < trainingItem.CompletedOn)
                {
                    context.AddFailure(nameof(request.TrainingInformation), "Training expiration date cannot be before its completion date.");
                }
            }
        }

        private static void AddContactErrors(
            SaveEmployeeRequest request,
            ValidationContext<SaveEmployeeRequest> context)
        {
            if (request.Contacts?.Count > 20)
            {
                context.AddFailure(nameof(request.Contacts), "No more than 20 employee contacts may be recorded.");
            }

            foreach (var contact in request.Contacts ?? [])
            {
                if (string.IsNullOrWhiteSpace(contact.Name))
                {
                    context.AddFailure(nameof(request.Contacts), "Contact name is required for every employee contact.");
                }

                if (contact.Name?.Length > 200 || contact.Relationship?.Length > 100)
                {
                    context.AddFailure(nameof(request.Contacts), "Contact name or relationship exceeds the allowed length.");
                }

                if (!string.IsNullOrWhiteSpace(contact.Phone) && contact.Phone.Count(char.IsDigit) != 10)
                {
                    context.AddFailure(nameof(request.Contacts), "Contact phone must contain 10 digits.");
                }
            }
        }
    }
}
