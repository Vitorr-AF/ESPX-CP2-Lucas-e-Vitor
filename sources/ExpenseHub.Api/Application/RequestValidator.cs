using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Application;

/// <summary>Runs the declarative (DataAnnotations) validation of request DTOs.</summary>
internal static class RequestValidator
{
    public static void Validate(object request)
    {
        List<ValidationResult> results = [];
        ValidationContext context = new(request);

        if (Validator.TryValidateObject(request, context, results, validateAllProperties: true))
        {
            return;
        }

        Dictionary<string, List<string>> errors = [];

        foreach (ValidationResult result in results)
        {
            string message = result.ErrorMessage ?? "The value is invalid.";
            string[] members = result.MemberNames.ToArray();

            if (members.Length == 0)
            {
                members = [string.Empty];
            }

            foreach (string member in members)
            {
                string key = JsonNamingPolicy.CamelCase.ConvertName(member);

                if (!errors.TryGetValue(key, out List<string>? messages))
                {
                    messages = [];
                    errors[key] = messages;
                }

                messages.Add(message);
            }
        }

        throw new ValidationFailedException(errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
    }
}
