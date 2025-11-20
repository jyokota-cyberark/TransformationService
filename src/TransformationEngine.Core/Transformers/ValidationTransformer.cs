using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core;

namespace TransformationEngine.Transformers;

/// <summary>
/// Transformer that validates data according to rules
/// </summary>
public class ValidationTransformer<T> : ITransformer<T, T>
{
    private readonly List<IValidationRule<T>> _rules;
    private readonly ILogger<ValidationTransformer<T>>? _logger;

    public int Order => 30; // After normalization
    public string Name => "Validation";

    public ValidationTransformer(
        IEnumerable<IValidationRule<T>>? rules = null,
        ILogger<ValidationTransformer<T>>? logger = null)
    {
        _rules = rules?.ToList() ?? new List<IValidationRule<T>>();
        _logger = logger;
    }

    public bool CanTransform(Type inputType, Type outputType)
    {
        return inputType == typeof(T) && outputType == typeof(T);
    }

    public async Task<T> TransformAsync(T input, TransformationContext context)
    {
        if (input == null)
            return input!;

        if (_rules.Count == 0)
        {
            _logger?.LogDebug("No validation rules configured, skipping validation");
            return input;
        }

        _logger?.LogDebug("Validating data with {Count} rules", _rules.Count);

        var errors = new List<ValidationError>();

        foreach (var rule in _rules)
        {
            try
            {
                var result = await rule.ValidateAsync(input);
                if (!result.IsValid)
                {
                    errors.AddRange(result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing validation rule {RuleName}", rule.Name);
                errors.Add(new ValidationError
                {
                    Field = "Unknown",
                    Message = $"Validation rule '{rule.Name}' failed: {ex.Message}"
                });
            }
        }

        if (errors.Any())
        {
            var errorMessages = string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"));
            _logger?.LogWarning("Validation failed with {Count} errors: {Errors}", 
                errors.Count, errorMessages);
            
            throw new TransformationValidationException("Validation failed", errors);
        }

        _logger?.LogDebug("Validation passed successfully");
        return input;
    }
}

/// <summary>
/// Interface for validation rules
/// </summary>
public interface IValidationRule<T>
{
    string Name { get; }
    Task<ValidationResult> ValidateAsync(T input);
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
}

/// <summary>
/// Validation error
/// </summary>
public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public class TransformationValidationException : TransformationException
{
    public List<ValidationError> Errors { get; }

    public TransformationValidationException(string message, List<ValidationError> errors)
        : base(message)
    {
        Errors = errors;
    }
}

/// <summary>
/// Simple field validation rule
/// </summary>
public class FieldValidationRule<T> : IValidationRule<T>
{
    private readonly string _fieldName;
    private readonly Func<T, object?> _fieldAccessor;
    private readonly List<Func<object?, bool>> _validators;
    private readonly List<string> _errorMessages;

    public string Name => $"FieldValidation_{_fieldName}";

    public FieldValidationRule(
        string fieldName,
        Func<T, object?> fieldAccessor)
    {
        _fieldName = fieldName;
        _fieldAccessor = fieldAccessor;
        _validators = new List<Func<object?, bool>>();
        _errorMessages = new List<string>();
    }

    public FieldValidationRule<T> Required(string? errorMessage = null)
    {
        _validators.Add(value => value != null && !string.IsNullOrWhiteSpace(value.ToString()));
        _errorMessages.Add(errorMessage ?? $"{_fieldName} is required");
        return this;
    }

    public FieldValidationRule<T> Email(string? errorMessage = null)
    {
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        _validators.Add(value => value == null || emailRegex.IsMatch(value.ToString() ?? ""));
        _errorMessages.Add(errorMessage ?? $"{_fieldName} must be a valid email address");
        return this;
    }

    public FieldValidationRule<T> Regex(string pattern, string? errorMessage = null)
    {
        var regex = new Regex(pattern);
        _validators.Add(value => value == null || regex.IsMatch(value.ToString() ?? ""));
        _errorMessages.Add(errorMessage ?? $"{_fieldName} does not match required pattern");
        return this;
    }

    public async Task<ValidationResult> ValidateAsync(T input)
    {
        var result = new ValidationResult { IsValid = true };
        var fieldValue = _fieldAccessor(input);

        for (int i = 0; i < _validators.Count; i++)
        {
            if (!_validators[i](fieldValue))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    Field = _fieldName,
                    Message = _errorMessages[i]
                });
            }
        }

        return await Task.FromResult(result);
    }
}

