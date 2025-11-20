using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TransformationEngine.Storage;

/// <summary>
/// File-based implementation of transformation rule repository
/// Reads from and writes to a DataTransformers directory
/// </summary>
public class FileTransformationRuleRepository : ITransformationRuleRepository
{
    private readonly string _baseDirectory;
    private readonly ILogger<FileTransformationRuleRepository>? _logger;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly Dictionary<string, DateTime> _fileTimestamps = new();

    public event EventHandler<TransformationRuleChangedEventArgs>? RuleChanged;

    public FileTransformationRuleRepository(
        string? baseDirectory = null,
        ILogger<FileTransformationRuleRepository>? logger = null,
        bool enableHotReload = true)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataTransformers");
        _logger = logger;

        // Ensure directory exists
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
            _logger?.LogInformation("Created DataTransformers directory: {Directory}", _baseDirectory);
        }

        // Set up file watcher for hot-reload
        if (enableHotReload)
        {
            _fileWatcher = new FileSystemWatcher(_baseDirectory, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Renamed += OnFileRenamed;

            _logger?.LogInformation("File watcher enabled for hot-reload: {Directory}", _baseDirectory);
        }
    }

    private string GetEventTypeDirectory(string eventType)
    {
        return Path.Combine(_baseDirectory, eventType);
    }

    private string GetRuleFilePath(string eventType, string ruleId)
    {
        var eventDir = GetEventTypeDirectory(eventType);
        return Path.Combine(eventDir, $"{ruleId}.json");
    }

    public async Task<List<TransformationRule>> GetRulesAsync(string eventType)
    {
        var eventDir = GetEventTypeDirectory(eventType);
        var rules = new List<TransformationRule>();

        if (!Directory.Exists(eventDir))
        {
            _logger?.LogDebug("Event type directory does not exist: {Directory}", eventDir);
            return rules;
        }

        var jsonFiles = Directory.GetFiles(eventDir, "*.json");

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var rule = JsonSerializer.Deserialize<TransformationRule>(json);
                
                if (rule != null)
                {
                    rule.Id = Path.GetFileNameWithoutExtension(file);
                    rule.EventType = eventType;
                    rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading rule from file: {File}", file);
            }
        }

        // Sort by order
        rules.Sort((a, b) => a.Order.CompareTo(b.Order));

        _logger?.LogDebug("Loaded {Count} rules for event type {EventType}", rules.Count, eventType);
        return rules;
    }

    public async Task<TransformationRule?> GetRuleAsync(string eventType, string ruleId)
    {
        var filePath = GetRuleFilePath(eventType, ruleId);

        if (!File.Exists(filePath))
        {
            _logger?.LogDebug("Rule file does not exist: {File}", filePath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var rule = JsonSerializer.Deserialize<TransformationRule>(json);
            
            if (rule != null)
            {
                rule.Id = ruleId;
                rule.EventType = eventType;
            }

            return rule;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading rule from file: {File}", filePath);
            return null;
        }
    }

    public async Task<TransformationRule> CreateRuleAsync(string eventType, TransformationRule rule)
    {
        if (string.IsNullOrEmpty(rule.Id))
        {
            rule.Id = Guid.NewGuid().ToString();
        }

        rule.EventType = eventType;
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;

        var eventDir = GetEventTypeDirectory(eventType);
        if (!Directory.Exists(eventDir))
        {
            Directory.CreateDirectory(eventDir);
        }

        var filePath = GetRuleFilePath(eventType, rule.Id);
        var json = JsonSerializer.Serialize(rule, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(filePath, json);

        _logger?.LogInformation("Created transformation rule: {EventType}/{RuleId}", eventType, rule.Id);

        // Track file timestamp to avoid duplicate events
        _fileTimestamps[filePath] = File.GetLastWriteTime(filePath);

        RuleChanged?.Invoke(this, new TransformationRuleChangedEventArgs
        {
            EventType = eventType,
            RuleId = rule.Id,
            ChangeType = RuleChangeType.Created
        });

        return rule;
    }

    public async Task<TransformationRule> UpdateRuleAsync(string eventType, string ruleId, TransformationRule rule)
    {
        var existingRule = await GetRuleAsync(eventType, ruleId);
        if (existingRule == null)
        {
            throw new FileNotFoundException($"Rule not found: {eventType}/{ruleId}");
        }

        rule.Id = ruleId;
        rule.EventType = eventType;
        rule.CreatedAt = existingRule.CreatedAt;
        rule.UpdatedAt = DateTime.UtcNow;

        var filePath = GetRuleFilePath(eventType, ruleId);
        var json = JsonSerializer.Serialize(rule, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(filePath, json);

        _logger?.LogInformation("Updated transformation rule: {EventType}/{RuleId}", eventType, ruleId);

        // Track file timestamp
        _fileTimestamps[filePath] = File.GetLastWriteTime(filePath);

        RuleChanged?.Invoke(this, new TransformationRuleChangedEventArgs
        {
            EventType = eventType,
            RuleId = ruleId,
            ChangeType = RuleChangeType.Updated
        });

        return rule;
    }

    public async Task<bool> DeleteRuleAsync(string eventType, string ruleId)
    {
        var filePath = GetRuleFilePath(eventType, ruleId);

        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Rule file does not exist for deletion: {File}", filePath);
            return false;
        }

        File.Delete(filePath);
        _fileTimestamps.Remove(filePath);

        _logger?.LogInformation("Deleted transformation rule: {EventType}/{RuleId}", eventType, ruleId);

        RuleChanged?.Invoke(this, new TransformationRuleChangedEventArgs
        {
            EventType = eventType,
            RuleId = ruleId,
            ChangeType = RuleChangeType.Deleted
        });

        return await Task.FromResult(true);
    }

    public Task<bool> RuleExistsAsync(string eventType, string ruleId)
    {
        var filePath = GetRuleFilePath(eventType, ruleId);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task<List<string>> GetEventTypesAsync()
    {
        if (!Directory.Exists(_baseDirectory))
        {
            return Task.FromResult(new List<string>());
        }

        var eventTypes = Directory.GetDirectories(_baseDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .ToList();

        return Task.FromResult(eventTypes);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Avoid duplicate events from file system
        var currentTimestamp = File.GetLastWriteTime(e.FullPath);
        if (_fileTimestamps.TryGetValue(e.FullPath, out var lastTimestamp) && 
            currentTimestamp == lastTimestamp)
        {
            return;
        }

        _fileTimestamps[e.FullPath] = currentTimestamp;

        var (eventType, ruleId) = ParseFilePath(e.FullPath);
        if (eventType != null && ruleId != null)
        {
            _logger?.LogInformation("File changed detected: {EventType}/{RuleId}", eventType, ruleId);
            
            Task.Delay(100).ContinueWith(_ =>
            {
                RuleChanged?.Invoke(this, new TransformationRuleChangedEventArgs
                {
                    EventType = eventType,
                    RuleId = ruleId,
                    ChangeType = RuleChangeType.Updated
                });
            });
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        var (eventType, ruleId) = ParseFilePath(e.FullPath);
        if (eventType != null && ruleId != null)
        {
            _logger?.LogInformation("File created detected: {EventType}/{RuleId}", eventType, ruleId);
            
            Task.Delay(100).ContinueWith(_ =>
            {
                RuleChanged?.Invoke(this, new TransformationRuleChangedEventArgs
                {
                    EventType = eventType,
                    RuleId = ruleId,
                    ChangeType = RuleChangeType.Created
                });
            });
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        var (eventType, ruleId) = ParseFilePath(e.FullPath);
        if (eventType != null && ruleId != null)
        {
            _logger?.LogInformation("File deleted detected: {EventType}/{RuleId}", eventType, ruleId);
            _fileTimestamps.Remove(e.FullPath);
            
            RuleChanged?.Invoke(this, new TransformationRuleChangedEventArgs
            {
                EventType = eventType,
                RuleId = ruleId,
                ChangeType = RuleChangeType.Deleted
            });
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Treat rename as delete + create
        OnFileDeleted(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, 
            Path.GetDirectoryName(e.OldFullPath)!, Path.GetFileName(e.OldFullPath)!));
        OnFileCreated(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, 
            Path.GetDirectoryName(e.FullPath)!, Path.GetFileName(e.FullPath)!));
    }

    private (string? eventType, string? ruleId) ParseFilePath(string filePath)
    {
        try
        {
            var relativePath = Path.GetRelativePath(_baseDirectory, filePath);
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            if (parts.Length >= 2)
            {
                var eventType = parts[0];
                var ruleId = Path.GetFileNameWithoutExtension(parts[^1]);
                return (eventType, ruleId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing file path: {Path}", filePath);
        }

        return (null, null);
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}

