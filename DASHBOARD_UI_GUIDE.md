# Transformation Dashboard UI Guide

## Overview

The Transformation Management Dashboard provides a comprehensive, real-time interface for managing transformation jobs, pipelines, and rule versioning. The dashboard is built with a modern, intuitive design that helps users visualize and control complex transformation workflows.

## Dashboard Features

### 1. Job Management Tab

The Job Management section provides real-time monitoring of all transformation jobs with an intuitive status tracking system.

#### Key Components:

**Statistics Cards:**
- **Completed Jobs**: Successfully executed transformations
- **Running Jobs**: Currently processing transformations (with animated progress bars)
- **Failed Jobs**: Jobs that encountered errors
- **Pending Jobs**: Jobs awaiting execution

**Job List Display:**
- Each job shows:
  - Job name and unique ID
  - Current status (color-coded badge)
  - Execution mode (InMemory, Spark, Kafka)
  - Submission timestamp
  - Progress bar (0-100%)
  - "View Details" button

**Interactive Features:**
- **Real-time Auto-Refresh**: Jobs list updates every 5 seconds
- **Search Functionality**: Filter jobs by name or ID
- **Manual Refresh**: Force refresh button to fetch latest data
- **Status Colors**:
  - Yellow: Pending
  - Blue: Running
  - Green: Completed
  - Red: Failed

#### Job Details Modal

Click "View Details" to open a comprehensive job information modal featuring:

**Step-by-Step Execution Timeline:**
- Visual timeline showing job progression through stages:
  1. Submitted ✓
  2. Validating
  3. Processing (animated indicator if running)
  4. Completed

- Each step shows:
  - Completion status
  - Current progress indicator (animated for active steps)
  - Time status (Completed/In Progress/Pending)

**Detailed Information:**
- Job ID and name
- Current status with badge
- Execution mode
- Overall progress percentage
- Full JSON details for debugging

**Job Control:**
- Cancel job button (only available for running jobs)
- Modal auto-closes after cancellation

### 2. Projects & Pipelines Tab

Manage transformation projects and execution pipelines.

#### Features:

**Project Cards Display:**
- Project name and description
- Associated transformation rules (tagged)
- Edit and Delete actions
- Visual rule composition display

**Rule Tags:**
- Color-coded tags showing all rules in a project
- Easy identification of transformation pipeline composition

**Planned Features:**
- Create new projects
- Edit project configurations
- Delete projects
- Reorder rules within a project

### 3. Rule Versions Tab

Track and manage transformation rule versions with full version history.

#### Features:

**Version Selection:**
- Dropdown to select any transformation rule
- Automatically loads version history when selected

**Version History Display:**
Each version shows:
- Version number badge (v1, v2, etc.)
- Creation timestamp
- Rule type (Replace, Regex, Lookup, etc.)
- Source pattern
- Action buttons:
  - **Compare**: See differences between versions
  - **Restore**: Revert to a previous version

**Version Timeline:**
- Chronological display of all versions
- Visual separation with cards
- Quick access to version details

### 4. Analytics Tab

Visual insights into transformation job execution and performance.

#### Components:

**Execution Mode Distribution Chart:**
- Pie or bar chart showing job distribution:
  - InMemory executions
  - Spark executions
  - Kafka enrichment streams
  - Airflow scheduled jobs

**Transformation Success Rate:**
- Success/failure ratio visualization
- Percentage indicators
- Trend analysis over time

**Job Execution Timeline (Gantt Chart):**
- Last 24 hours of job execution
- Visual timeline showing:
  - Job duration and overlap
  - Execution start/end times
  - Parallel job identification
  - Resource utilization patterns

## UI Design Principles

### Color Scheme
- **Primary**: #667eea (Purple-blue)
- **Secondary**: #764ba2 (Purple)
- **Success**: #28a745 (Green)
- **Warning**: #ffc107 (Yellow)
- **Danger**: #dc3545 (Red)
- **Info**: #17a2b8 (Teal)

### Visual Elements

**Stat Cards:**
- Left border accent color matching status
- Large number display with supporting label
- Immediate visual impact for KPIs

**Job Cards:**
- Hover effect with shadow elevation
- Status badge for quick identification
- Progress bar for job completion percentage
- Compact layout for easy scanning

**Timeline Visualization:**
- Circular step indicators
- Connecting lines between steps
- Color-coded states (pending/active/completed/failed)
- Animated pulse for active steps

**Modal Layouts:**
- Clean, organized sections
- Clear visual hierarchy
- Responsive grid layout
- Pre-formatted JSON for technical details

## API Integration

### Endpoints Used

**Job Management:**
- `GET /api/transformation-jobs/list` - List all jobs
- `GET /api/transformation-jobs/{jobId}/status` - Get job status
- `GET /api/transformation-jobs/{jobId}/result` - Get job results
- `POST /api/transformation-jobs/{jobId}/cancel` - Cancel a job

**Projects:**
- `GET /api/transformation-projects` - List projects
- `POST /api/transformation-projects` - Create project
- `PUT /api/transformation-projects/{id}` - Update project
- `DELETE /api/transformation-projects/{id}` - Delete project

**Rules:**
- `GET /api/transformation-rules` - List all rules
- `GET /api/rule-versions/{ruleId}` - Get rule versions
- `POST /api/rule-versions/{ruleId}/restore/{version}` - Restore version

## Real-Time Updates

The dashboard implements several auto-refresh strategies:

1. **Automatic Job Polling**: Jobs list updates every 5 seconds
2. **Manual Refresh**: Users can manually trigger updates
3. **Modal Real-Time Updates**: Job details can auto-refresh while modal is open
4. **Search Persistence**: Filter criteria maintained during refreshes

## Error Handling

The dashboard provides user-friendly error messages:

- **API Errors**: Display alert dialogs with error details
- **Network Issues**: Graceful fallback with retry prompts
- **Data Load Failures**: Clear error messages with optional retry buttons
- **Console Logging**: Detailed error logs for debugging

## Responsive Design

- **Desktop**: Full-width layout with side-by-side cards
- **Tablet**: Single-column layout with optimized spacing
- **Mobile**: Stacked layouts with touch-friendly buttons
- **Stat Cards**: Auto-responsive grid (4-column on desktop, 2 on tablet, 1 on mobile)

## Performance Optimizations

1. **Efficient DOM Updates**: Only re-render when data changes
2. **Pagination Ready**: Dashboard can scale to handle thousands of jobs
3. **Lazy Loading**: Charts and analytics load on-demand
4. **Debounced Search**: Search filters to avoid excessive API calls
5. **Memory Management**: Cleanup intervals on page unload

## Future Enhancements

- [ ] Advanced filtering (date range, execution mode, status)
- [ ] Export job history as CSV/JSON
- [ ] Scheduled job calendar view
- [ ] Real-time WebSocket updates instead of polling
- [ ] Custom dashboard widgets
- [ ] Alert notifications for job failures
- [ ] Historical analytics and trend analysis
- [ ] Bulk job operations (retry, cancel multiple)
- [ ] Job scheduling interface
- [ ] Integration with external monitoring tools

## Troubleshooting

### Dashboard Not Loading
1. Check browser console for JavaScript errors
2. Verify API endpoints are responding
3. Check network tab for failed requests
4. Ensure user has proper permissions

### Jobs Not Updating
1. Check if auto-refresh is running (5-second intervals)
2. Verify API endpoint `/api/transformation-jobs/list` is accessible
3. Check browser console for fetch errors
4. Manual refresh button as temporary workaround

### Missing Project/Rule Data
1. Verify data exists in the database
2. Check API responses using browser network tab
3. Ensure proper API endpoint registration
4. Check user permissions for resource access

## Code Examples

### Fetching Job Status
```javascript
const response = await fetch(`/api/transformation-jobs/${jobId}/status`);
const status = await response.json();
console.log(`Job ${jobId} is ${status.status}`);
```

### Filtering Jobs Locally
```javascript
const filtered = allJobs.filter(job => 
    job.jobName.toLowerCase().includes(searchTerm) ||
    job.jobId.toLowerCase().includes(searchTerm)
);
```

### Creating Stat Cards
```javascript
const stats = {
    completed: allJobs.filter(j => j.status === 'Completed').length,
    running: allJobs.filter(j => j.status === 'Running').length,
    failed: allJobs.filter(j => j.status === 'Failed').length,
    pending: allJobs.filter(j => j.status === 'Pending').length
};
```

