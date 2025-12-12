using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TransformationEngine.Pages;

/// <summary>
/// Page model for the Transformation Management Dashboard
/// Provides a comprehensive view of job management, transformation projects, and rule versioning
/// </summary>
public class TransformationDashboardModel : PageModel
{
    /// <summary>
    /// Initializes the dashboard page
    /// </summary>
    public void OnGet()
    {
        // Dashboard loads all data via JavaScript API calls
        // This provides a responsive, real-time user experience
    }
}

