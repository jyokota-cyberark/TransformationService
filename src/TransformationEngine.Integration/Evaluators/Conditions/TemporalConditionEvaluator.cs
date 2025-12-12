using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Evaluators.Conditions;

/// <summary>
/// Evaluates temporal conditions (time-based access control)
/// </summary>
public class TemporalConditionEvaluator : ConditionEvaluatorBase
{
    private readonly ILogger<TemporalConditionEvaluator> _logger;

    public override string ConditionType => "Temporal";

    public TemporalConditionEvaluator(ILogger<TemporalConditionEvaluator> logger)
    {
        _logger = logger;
    }

    public override Task<ConditionEvaluationResult> EvaluateAsync(
        ConditionConfig condition,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var timestamp = context.Timestamp;

        // Handle business hours shorthand
        if (condition.BusinessHours != null)
        {
            return Task.FromResult(EvaluateBusinessHours(condition.BusinessHours, timestamp));
        }

        // Handle full temporal config
        if (condition.Temporal != null)
        {
            return Task.FromResult(EvaluateTemporal(condition.Temporal, timestamp));
        }

        return Task.FromResult(Failed("No temporal configuration provided"));
    }

    private ConditionEvaluationResult EvaluateBusinessHours(BusinessHoursConfig config, DateTime timestamp)
    {
        try
        {
            // Convert to target timezone
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(config.Timezone));
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(timestamp, timezone);

            var hour = localTime.Hour;
            var dayOfWeek = (int)localTime.DayOfWeek;

            // Check if within business hours (Mon-Fri by default)
            var isWeekday = dayOfWeek >= 1 && dayOfWeek <= 5;
            var isWithinHours = hour >= config.Start && hour < config.End;

            if (!isWeekday)
            {
                return Failed($"Access denied: Not a business day (current: {localTime.DayOfWeek})",
                    new Dictionary<string, object>
                    {
                        ["currentDay"] = localTime.DayOfWeek.ToString(),
                        ["localTime"] = localTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
            }

            if (!isWithinHours)
            {
                return Failed($"Access denied: Outside business hours ({config.Start}:00 - {config.End}:00)",
                    new Dictionary<string, object>
                    {
                        ["currentHour"] = hour,
                        ["allowedStart"] = config.Start,
                        ["allowedEnd"] = config.End,
                        ["localTime"] = localTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
            }

            return Passed($"Within business hours ({config.Start}:00 - {config.End}:00 {config.Timezone})",
                new Dictionary<string, object>
                {
                    ["currentHour"] = hour,
                    ["localTime"] = localTime.ToString("yyyy-MM-dd HH:mm:ss")
                });
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Unknown timezone: {Timezone}, falling back to UTC", config.Timezone);
            return EvaluateBusinessHoursUtc(config, timestamp);
        }
    }

    private ConditionEvaluationResult EvaluateBusinessHoursUtc(BusinessHoursConfig config, DateTime timestamp)
    {
        var hour = timestamp.Hour;
        var dayOfWeek = (int)timestamp.DayOfWeek;

        var isWeekday = dayOfWeek >= 1 && dayOfWeek <= 5;
        var isWithinHours = hour >= config.Start && hour < config.End;

        if (!isWeekday || !isWithinHours)
        {
            return Failed($"Outside business hours (UTC)");
        }

        return Passed($"Within business hours (UTC)");
    }

    private ConditionEvaluationResult EvaluateTemporal(TemporalConditionConfig config, DateTime timestamp)
    {
        try
        {
            // Convert to target timezone
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(ConvertIanaToWindows(config.Timezone));
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(timestamp, timezone);

            // Check date range
            if (config.StartDate.HasValue && localTime.Date < config.StartDate.Value.Date)
            {
                return Failed($"Access not yet active (starts: {config.StartDate.Value:yyyy-MM-dd})");
            }

            if (config.EndDate.HasValue && localTime.Date > config.EndDate.Value.Date)
            {
                return Failed($"Access expired (ended: {config.EndDate.Value:yyyy-MM-dd})");
            }

            // Check day of week
            var dayOfWeek = (int)localTime.DayOfWeek;
            if (config.DaysOfWeek.Count > 0 && !config.DaysOfWeek.Contains(dayOfWeek))
            {
                return Failed($"Access denied: Not an allowed day (current: {localTime.DayOfWeek})",
                    new Dictionary<string, object>
                    {
                        ["currentDay"] = dayOfWeek,
                        ["allowedDays"] = config.DaysOfWeek
                    });
            }

            // Check time window
            var hour = localTime.Hour;
            if (hour < config.StartHour || hour >= config.EndHour)
            {
                return Failed($"Outside allowed hours ({config.StartHour}:00 - {config.EndHour}:00)",
                    new Dictionary<string, object>
                    {
                        ["currentHour"] = hour,
                        ["allowedStart"] = config.StartHour,
                        ["allowedEnd"] = config.EndHour
                    });
            }

            return Passed($"Within allowed time window",
                new Dictionary<string, object>
                {
                    ["localTime"] = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["timezone"] = config.Timezone
                });
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Unknown timezone: {Timezone}", config.Timezone);
            return Failed($"Unknown timezone: {config.Timezone}");
        }
    }

    public override IEnumerable<string> Validate(ConditionConfig condition)
    {
        var errors = new List<string>();

        if (condition.BusinessHours != null)
        {
            if (condition.BusinessHours.Start < 0 || condition.BusinessHours.Start > 23)
                errors.Add("BusinessHours.Start must be between 0 and 23");
            if (condition.BusinessHours.End < 0 || condition.BusinessHours.End > 24)
                errors.Add("BusinessHours.End must be between 0 and 24");
            if (condition.BusinessHours.Start >= condition.BusinessHours.End)
                errors.Add("BusinessHours.Start must be less than End");
        }

        if (condition.Temporal != null)
        {
            if (condition.Temporal.StartHour < 0 || condition.Temporal.StartHour > 23)
                errors.Add("Temporal.StartHour must be between 0 and 23");
            if (condition.Temporal.EndHour < 0 || condition.Temporal.EndHour > 24)
                errors.Add("Temporal.EndHour must be between 0 and 24");
            if (condition.Temporal.StartDate.HasValue && condition.Temporal.EndDate.HasValue &&
                condition.Temporal.StartDate > condition.Temporal.EndDate)
                errors.Add("Temporal.StartDate must be before EndDate");
        }

        return errors;
    }

    /// <summary>
    /// Convert IANA timezone to Windows timezone ID
    /// </summary>
    private static string ConvertIanaToWindows(string ianaTimezone)
    {
        // Common mappings - in production, use NodaTime or TimeZoneConverter
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["America/Los_Angeles"] = "Pacific Standard Time",
            ["America/New_York"] = "Eastern Standard Time",
            ["America/Chicago"] = "Central Standard Time",
            ["America/Denver"] = "Mountain Standard Time",
            ["Europe/London"] = "GMT Standard Time",
            ["Europe/Paris"] = "Romance Standard Time",
            ["Asia/Tokyo"] = "Tokyo Standard Time",
            ["Asia/Shanghai"] = "China Standard Time",
            ["Australia/Sydney"] = "AUS Eastern Standard Time",
            ["UTC"] = "UTC"
        };

        return mappings.TryGetValue(ianaTimezone, out var windowsId) ? windowsId : ianaTimezone;
    }
}
