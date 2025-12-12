using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransformationEngine.Integration.Models;
using TransformationEngine.Integration.Data;

namespace TransformationEngine.Service.Services;

/// <summary>
/// Background service that consumes Kafka events for job status updates
/// Listens for job completion events from Kafka enrichment pipelines
/// </summary>
public class KafkaJobStatusConsumerService : BackgroundService
{
    private readonly ILogger<KafkaJobStatusConsumerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConsumer<string, string>? _consumer;
    private readonly string? _kafkaBootstrapServers;
    private readonly string? _topicName;

    public KafkaJobStatusConsumerService(
        ILogger<KafkaJobStatusConsumerService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        try
        {
            _kafkaBootstrapServers = configuration["Kafka:BootstrapServers"];
            _topicName = configuration["Kafka:JobStatusTopic"] ?? "transformation-job-status";

            if (string.IsNullOrEmpty(_kafkaBootstrapServers))
            {
                _logger.LogWarning("Kafka BootstrapServers not configured, Kafka job monitoring disabled");
                return;
            }

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _kafkaBootstrapServers,
                GroupId = "transformation-engine-job-monitor",
                AutoOffsetReset = AutoOffsetReset.Latest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            _logger.LogInformation("Kafka Job Status Consumer initialized for topic {Topic}", _topicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Kafka consumer");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_consumer == null)
        {
            _logger.LogInformation("Kafka consumer not available, job status monitoring disabled");
            return;
        }

        _logger.LogInformation("Kafka Job Status Consumer Service starting");

        try
        {
            _consumer.Subscribe(_topicName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(10));

                    if (consumeResult == null)
                        continue;

                    await ProcessJobStatusMessageAsync(consumeResult.Message, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    // Only log non-timeout and non-EOF errors
                    if (!ex.Error.IsLocalError || (ex.Error.Code != ErrorCode.Local_TimedOut))
                    {
                        _logger.LogError(ex, "Consume error: {Error}", ex.Error.Reason);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consuming Kafka message");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka consumer fatal error");
        }
        finally
        {
            _consumer.Close();
            _logger.LogInformation("Kafka Job Status Consumer Service stopped");
        }
    }

    private async Task ProcessJobStatusMessageAsync(Message<string, string> message, CancellationToken stoppingToken)
    {
        try
        {
            var jobStatus = JsonSerializer.Deserialize<KafkaJobStatusMessage>(message.Value);

            if (jobStatus == null)
            {
                _logger.LogWarning("Failed to deserialize Kafka job status message");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TransformationIntegrationDbContext>();

            // Update job queue status
            var job = await dbContext.TransformationJobQueue
                .FirstOrDefaultAsync(j => j.Id == jobStatus.JobId, stoppingToken);

            if (job == null)
            {
                _logger.LogWarning("Kafka job status received for unknown job {JobId}", jobStatus.JobId);
                return;
            }

            // Update job status based on message
            job.Status = jobStatus.Status switch
            {
                "Completed" => JobStatus.Completed,
                "Failed" => JobStatus.Failed,
                "Processing" => JobStatus.Processing,
                "Cancelled" => JobStatus.Cancelled,
                _ => job.Status
            };

            if (!string.IsNullOrEmpty(jobStatus.ErrorMessage))
            {
                job.ErrorMessage = jobStatus.ErrorMessage;
            }

            if (jobStatus.GeneratedFields != null)
            {
                job.GeneratedFields = jobStatus.GeneratedFields;
            }

            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Updated Kafka job {JobId} to status {Status}",
                jobStatus.JobId, job.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Kafka job status message");
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Job status message from Kafka
/// </summary>
public class KafkaJobStatusMessage
{
    public int JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? GeneratedFields { get; set; }
    public long? RowsProcessed { get; set; }
    public DateTime? CompletedAt { get; set; }
}

