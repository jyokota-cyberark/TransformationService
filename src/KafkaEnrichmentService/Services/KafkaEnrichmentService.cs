using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TransformationEngine.Core;
using TransformationEngine.Builders;
using TransformationEngine.Transformers;

namespace KafkaEnrichmentService.Services;

/// <summary>
/// Background service that consumes from Kafka, enriches data, and publishes to enriched topics
/// </summary>
public class KafkaEnrichmentService : BackgroundService
{
    private readonly ILogger<KafkaEnrichmentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private IConsumer<string, string>? _consumer;
    private IProducer<string, string>? _producer;
    private bool _isRunning = false;

    public KafkaEnrichmentService(
        ILogger<KafkaEnrichmentService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Kafka Enrichment Service starting...");

        var enabled = _configuration.GetValue<bool>("KafkaEnrichment:Enabled", true);
        if (!enabled)
        {
            _logger.LogWarning("Kafka Enrichment Service is disabled in configuration");
            return;
        }

        await StartConsumerWithRetriesAsync(stoppingToken);
    }

    private async Task StartConsumerWithRetriesAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var groupId = _configuration["KafkaEnrichment:GroupId"] ?? "kafka-enrichment-service-group";
        var sourceTopics = _configuration.GetSection("KafkaEnrichment:SourceTopics").Get<string[]>() 
            ?? new[] { "user-changes", "webapplication-changes" };
        var targetTopicSuffix = _configuration["KafkaEnrichment:TargetTopicSuffix"] ?? "-enriched";

        var baseDelayMs = 2000;
        var maxDelayMs = 30000;
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                attempt++;
                _logger.LogInformation("Attempting to connect to Kafka (attempt {Attempt})...", attempt);

                var consumerConfig = new ConsumerConfig
                {
                    BootstrapServers = bootstrapServers,
                    GroupId = groupId,
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = false,
                    EnablePartitionEof = true
                };

                _consumer = new ConsumerBuilder<string, string>(consumerConfig)
                    .SetErrorHandler((_, error) =>
                    {
                        _logger.LogError("Kafka consumer error: {Reason}", error.Reason);
                    })
                    .Build();

                var producerConfig = new ProducerConfig
                {
                    BootstrapServers = bootstrapServers,
                    ClientId = "kafka-enrichment-service-producer",
                    Acks = Acks.All,
                    EnableIdempotence = true
                };

                _producer = new ProducerBuilder<string, string>(producerConfig)
                    .SetErrorHandler((_, error) =>
                    {
                        _logger.LogError("Kafka producer error: {Reason}", error.Reason);
                    })
                    .Build();

                _consumer.Subscribe(sourceTopics);
                _isRunning = true;

                _logger.LogInformation("✅ Connected to Kafka. Subscribed to topics: {Topics}", 
                    string.Join(", ", sourceTopics));

                await ProcessMessagesAsync(stoppingToken, targetTopicSuffix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in Kafka enrichment service");
                _isRunning = false;

                if (_consumer != null)
                {
                    try
                    {
                        _consumer.Close();
                        _consumer.Dispose();
                    }
                    catch { }
                    _consumer = null;
                }

                if (_producer != null)
                {
                    try
                    {
                        _producer.Flush(TimeSpan.FromSeconds(5));
                        _producer.Dispose();
                    }
                    catch { }
                    _producer = null;
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    var delay = Math.Min(baseDelayMs * attempt, maxDelayMs);
                    _logger.LogInformation("Retrying connection in {Delay}ms...", delay);
                    await Task.Delay(delay, stoppingToken);
                }
            }
        }
    }

    private async Task ProcessMessagesAsync(CancellationToken stoppingToken, string targetTopicSuffix)
    {
        while (!stoppingToken.IsCancellationRequested && _consumer != null && _producer != null)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromSeconds(1));

                if (result == null)
                    continue;

                if (result.IsPartitionEOF)
                {
                    _logger.LogDebug("Reached end of partition {Partition}", result.Partition);
                    continue;
                }

                _logger.LogDebug("Received message from topic {Topic}, partition {Partition}, offset {Offset}",
                    result.Topic, result.Partition, result.Offset);

                try
                {
                    // Determine event type from topic or message
                    var eventType = DetermineEventType(result.Topic, result.Message.Value);
                    var targetTopic = result.Topic + targetTopicSuffix;

                    // Transform and enrich the message
                    var enrichedMessage = await EnrichMessageAsync(
                        result.Message.Value,
                        eventType,
                        result.Message.Headers);

                    if (enrichedMessage != null)
                    {
                        // Publish enriched message
                        var enrichedHeaders = new Headers
                        {
                            { "enriched-at", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")) },
                            { "enrichment-service", System.Text.Encoding.UTF8.GetBytes("KafkaEnrichmentService") }
                        };
                        
                        // Copy original headers
                        if (result.Message.Headers != null)
                        {
                            foreach (var header in result.Message.Headers)
                            {
                                enrichedHeaders.Add(header.Key, header.GetValueBytes());
                            }
                        }

                        var kafkaMessage = new Message<string, string>
                        {
                            Key = result.Message.Key,
                            Value = enrichedMessage,
                            Headers = enrichedHeaders
                        };

                        var produceResult = await _producer.ProduceAsync(targetTopic, kafkaMessage, stoppingToken);

                        _logger.LogInformation(
                            "✅ Enriched and published message: Topic={Topic}, Partition={Partition}, Offset={Offset}",
                            produceResult.Topic, produceResult.Partition, produceResult.Offset);

                        // Commit offset
                        _consumer.Commit(result);
                    }
                    else
                    {
                        _logger.LogWarning("Enrichment returned null, skipping message");
                        _consumer.Commit(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from topic {Topic}", result.Topic);
                    // Commit anyway to avoid reprocessing the same message
                    _consumer.Commit(result);
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error");
            }
        }
    }

    private string DetermineEventType(string topic, string messageValue)
    {
        // Try to determine event type from topic name
        if (topic.Contains("user", StringComparison.OrdinalIgnoreCase))
            return "UserChangeEvent";
        if (topic.Contains("application", StringComparison.OrdinalIgnoreCase) || 
            topic.Contains("webapp", StringComparison.OrdinalIgnoreCase))
            return "WebApplicationChangeEvent";

        // Try to parse from message
        try
        {
            var jsonDoc = JsonDocument.Parse(messageValue);
            if (jsonDoc.RootElement.TryGetProperty("EventType", out var eventTypeProp))
            {
                return eventTypeProp.GetString() ?? "Unknown";
            }
            if (jsonDoc.RootElement.TryGetProperty("entityType", out var entityTypeProp))
            {
                return entityTypeProp.GetString() ?? "Unknown";
            }
        }
        catch { }

        return "Unknown";
    }

    private async Task<string?> EnrichMessageAsync(
        string messageValue,
        string eventType,
        Headers headers)
    {
        try
        {
            // Parse the message as JSON
            var jsonDoc = JsonDocument.Parse(messageValue);
            var root = jsonDoc.RootElement;

            // Create a dynamic dictionary from the JSON
            var data = new Dictionary<string, object?>();
            foreach (var prop in root.EnumerateObject())
            {
                data[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetInt32(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }

            // Apply transformations based on event type
            // For now, we'll do basic enrichment (add metadata, normalize)
            var enrichedData = new Dictionary<string, object?>(data);

            // Add enrichment metadata
            enrichedData["EnrichedAt"] = DateTime.UtcNow.ToString("O");
            enrichedData["EnrichmentService"] = "KafkaEnrichmentService";
            enrichedData["OriginalEventType"] = eventType;

            // Normalize field names (camelCase to PascalCase)
            var normalizedData = new Dictionary<string, object?>();
            foreach (var kvp in enrichedData)
            {
                var normalizedKey = char.ToUpperInvariant(kvp.Key[0]) + kvp.Key.Substring(1);
                normalizedData[normalizedKey] = kvp.Value;
            }

            // Convert back to JSON
            var enrichedJson = JsonSerializer.Serialize(normalizedData, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            return enrichedJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching message");
            return null;
        }
    }

    public override void Dispose()
    {
        _isRunning = false;
        _consumer?.Close();
        _consumer?.Dispose();
        _producer?.Flush(TimeSpan.FromSeconds(5));
        _producer?.Dispose();
        base.Dispose();
    }
}

