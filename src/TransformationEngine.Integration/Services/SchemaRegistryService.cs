using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Configuration;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Implementation of schema registry service for Confluent Schema Registry
/// </summary>
public class SchemaRegistryService : ISchemaRegistryService
{
    private readonly ILogger<SchemaRegistryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _registryUrl;
    private readonly bool _enabled;

    public SchemaRegistryService(
        ILogger<SchemaRegistryService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        
        // Try to get from SchemaRegistry section first, then fall back to root
        _registryUrl = configuration["SchemaRegistry:Url"] ?? configuration["SchemaRegistryUrl"];
        _enabled = !string.IsNullOrEmpty(_registryUrl);

        if (!_enabled)
        {
            _logger.LogWarning("Schema Registry is not configured. Schema validation will be skipped.");
        }
        else
        {
            _logger.LogInformation("Schema Registry configured at: {Url}", _registryUrl);
        }
    }

    public async Task<string> GetSchemaAsync(string entityType, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            _logger.LogWarning("Schema Registry not configured, returning empty schema");
            return "{}";
        }

        try
        {
            var subject = GetSubjectName(entityType);
            var client = _httpClientFactory.CreateClient("SchemaRegistry");
            
            var response = await client.GetAsync(
                $"{_registryUrl}/subjects/{subject}/versions/latest",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch schema for {Subject}: {StatusCode}", subject, response.StatusCode);
                return "{}";
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var schemaResponse = JsonSerializer.Deserialize<SchemaResponse>(content);
            
            return schemaResponse?.Schema ?? "{}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schema for entity type {EntityType}", entityType);
            return "{}";
        }
    }

    public async Task<int> RegisterSchemaAsync(string subject, string schemaJson, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            _logger.LogWarning("Schema Registry not configured, skipping registration");
            return -1;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SchemaRegistry");
            
            var requestBody = new
            {
                schema = schemaJson,
                schemaType = "AVRO"
            };

            var response = await client.PostAsJsonAsync(
                $"{_registryUrl}/subjects/{subject}/versions",
                requestBody,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to register schema for {Subject}: {StatusCode} - {Error}", 
                    subject, response.StatusCode, error);
                return -1;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var registrationResponse = JsonSerializer.Deserialize<SchemaRegistrationResponse>(content);
            
            _logger.LogInformation("Successfully registered schema for {Subject} with ID {SchemaId}", 
                subject, registrationResponse?.Id ?? -1);
            
            return registrationResponse?.Id ?? -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering schema for subject {Subject}", subject);
            return -1;
        }
    }

    public async Task<bool> ValidateDataAgainstSchemaAsync(string entityType, string dataJson, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug("Schema Registry not configured, skipping validation");
            return true; // Skip validation if not configured
        }

        try
        {
            // Fetch the schema
            var schemaJson = await GetSchemaAsync(entityType, cancellationToken);
            
            if (schemaJson == "{}")
            {
                _logger.LogWarning("No schema found for {EntityType}, skipping validation", entityType);
                return true;
            }

            // Basic validation - check if data is valid JSON
            try
            {
                JsonDocument.Parse(dataJson);
            }
            catch (JsonException)
            {
                _logger.LogError("Invalid JSON data for {EntityType}", entityType);
                return false;
            }

            // TODO: Implement full Avro schema validation
            // For now, we'll just verify the JSON is valid
            _logger.LogDebug("Data validation passed for {EntityType}", entityType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating data for entity type {EntityType}", entityType);
            return false;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SchemaRegistry");
            var response = await client.GetAsync($"{_registryUrl}/subjects", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking schema registry availability");
            return false;
        }
    }

    public async Task<List<string>> GetSubjectsAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            return new List<string>();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SchemaRegistry");
            var response = await client.GetAsync($"{_registryUrl}/subjects", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch subjects: {StatusCode}", response.StatusCode);
                return new List<string>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var subjects = JsonSerializer.Deserialize<List<string>>(content);
            
            return subjects ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching subjects from schema registry");
            return new List<string>();
        }
    }

    private string GetSubjectName(string entityType)
    {
        // Convert entity type to subject name format
        // e.g., "User" -> "user-changes-value"
        return $"{entityType.ToLower()}-changes-value";
    }

    // Response models for Confluent Schema Registry API
    private class SchemaResponse
    {
        public string? Subject { get; set; }
        public int Version { get; set; }
        public int Id { get; set; }
        public string? Schema { get; set; }
    }

    private class SchemaRegistrationResponse
    {
        public int Id { get; set; }
    }
}

