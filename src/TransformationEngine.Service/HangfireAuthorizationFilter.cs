using Hangfire.Dashboard;

namespace TransformationEngine;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // For development, allow all access
        // In production, implement proper authentication
        return true;
    }
}
