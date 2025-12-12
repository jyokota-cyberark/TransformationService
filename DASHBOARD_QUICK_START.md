# Transformation Service - Quick Start Guide

## Accessing the Transformation Service

The Transformation Service provides two main interfaces for managing data transformations:

### 1. **Rule Management UI** (`/Transformations`)
Create, edit, and manage individual transformation rules.

### 2. **Management Dashboard** (`/TransformationDashboard`) ⭐ NEW
Monitor jobs, manage projects, and track rule versions in real-time.

---

## Quick Start: Using the Dashboard

### Step 1: Access the Dashboard
Navigate to `http://localhost:5020/TransformationDashboard` (or your service URL)

### Step 2: Monitor Jobs (Job Management Tab)
- **View Running Jobs**: See all transformation jobs with their status
- **Check Progress**: Watch real-time progress bars for executing jobs
- **View Details**: Click "View Details" on any job to see:
  - Detailed job information
  - Step-by-step execution timeline
  - Full JSON details for debugging
- **Search Jobs**: Use the search box to find jobs by name or ID
- **Refresh**: Auto-updates every 5 seconds, or click "Refresh" manually

### Step 3: Manage Projects (Projects & Pipelines Tab)
- **View Projects**: See all transformation projects
- **See Composed Rules**: Each project shows its associated transformation rules
- **Edit Project**: Click Edit to modify project configuration
- **Delete Project**: Remove a project (creates new one via API)

### Step 4: Track Rule Versions (Rule Versions Tab)
- **Select Rule**: Choose a rule from the dropdown
- **View History**: See all versions of that rule
- **Compare Versions**: Click "Compare" to see differences
- **Restore Version**: Click "Restore" to revert to a previous version

### Step 5: View Analytics (Analytics Tab)
- **Execution Modes**: See distribution of InMemory, Spark, and Kafka jobs
- **Success Rate**: View transformation completion vs. failure rates
- **Timeline**: Analyze job execution timeline over last 24 hours

---

## Job Status Meanings

| Status | Color | Meaning |
|--------|-------|---------|
| **Pending** | Yellow | Job submitted, waiting to start |
| **Running** | Blue | Job is currently executing |
| **Completed** | Green | Job finished successfully |
| **Failed** | Red | Job encountered an error |

---

## Key Features of the Dashboard

### Real-Time Monitoring
✅ Jobs update automatically every 5 seconds
✅ Progress bars show completion percentage
✅ Status badges update in real-time
✅ Animated indicators for running jobs

### Visual Timeline
✅ Step-by-step execution visualization
✅ Clear milestone markers
✅ Animation shows active steps
✅ Helps identify where jobs are in their lifecycle

### Job Details Modal
✅ Comprehensive job information
✅ Full execution timeline with visual steps
✅ JSON debugging details
✅ Cancel job capability
✅ Submitted timestamp

### Project Management
✅ View all transformation projects
✅ See which rules are in each project
✅ Visual rule tags for quick reference
✅ Edit/Delete actions

### Version Control
✅ Track all changes to transformation rules
✅ Version numbers and timestamps
✅ Easy rollback to previous versions
✅ Compare different versions

---

## Common Tasks

### Monitor a Running Job
1. Go to **Job Management** tab
2. Find your job in the list (search if needed)
3. Watch the progress bar fill (0% → 100%)
4. Click **View Details** for timeline information

### View Job Failure Details
1. Look for Red **"Failed"** status badge
2. Click **View Details**
3. Check the timeline to see where it failed
4. Review JSON details in the "Details" section at the bottom

### Cancel a Running Job
1. Find the running job in the list
2. Click **View Details**
3. Click **Cancel Job** button in the modal
4. Job status will change to "Cancelled"

### Check Rule Version History
1. Go to **Rule Versions** tab
2. Select a rule from dropdown
3. View all versions chronologically
4. Click **Restore** on any version to activate it
5. Click **Compare** to see differences

### Analyze Performance
1. Go to **Analytics** tab
2. View execution mode distribution
3. Check success rate
4. Examine job timeline for patterns

---

## API Endpoints Used

The dashboard uses these backend APIs:

```
GET  /api/transformation-jobs/list              - List all jobs
GET  /api/transformation-jobs/{id}/status       - Get job status
POST /api/transformation-jobs/{id}/cancel       - Cancel job
GET  /api/transformation-projects               - List projects
GET  /api/transformation-rules                  - List rules
GET  /api/rule-versions/{ruleId}               - List rule versions
```

---

## Dashboard Tabs Overview

### 📊 Job Management Tab
- Real-time job monitoring
- Status tracking and progress
- Job search and filter
- Detailed job information modal
- Job cancellation

### 📦 Projects & Pipelines Tab
- View all transformation projects
- See associated rules
- Project management (create/edit/delete)
- Visual rule composition

### 📜 Rule Versions Tab
- Select a transformation rule
- View complete version history
- Compare versions
- Restore to previous versions
- Track changes over time

### 📈 Analytics Tab
- Execution mode distribution
- Success rate visualization
- Job timeline analysis
- Performance insights

---

## Tips & Tricks

### 🎯 Auto-Refresh
- Dashboard auto-refreshes every 5 seconds
- Use Manual "Refresh" button for immediate update
- Auto-refresh continues even with modal open

### 🔍 Search
- Search by job name: `my_transformation_job`
- Search by job ID: `job_123456`
- Filters apply immediately to displayed list

### 📱 Mobile Friendly
- Dashboard is responsive on tablets and phones
- Touch-friendly buttons and cards
- Stat cards stack on small screens

### ⚡ Performance
- Dashboard loads initial data quickly
- Real-time updates without page refresh
- Efficient DOM rendering

---

## Troubleshooting

### Dashboard Not Showing Jobs?
1. Check if jobs were actually submitted
2. Verify API endpoint is responding: `/api/transformation-jobs/list`
3. Click "Refresh" button manually
4. Check browser console (F12) for errors

### Jobs Not Updating in Real-Time?
1. Auto-refresh runs every 5 seconds
2. Check if refresh interval is still active (should see clock icon)
3. Click "Refresh" manually to force update
4. Check if API is returning data

### Can't See Job Details?
1. Ensure job has been submitted successfully
2. Try clicking "View Details" again
3. Check browser console for errors
4. Verify API endpoint returns valid JSON

### Modal Won't Close?
1. Click X button in modal header
2. Click "Close" button at bottom
3. Press ESC key
4. Click outside modal area

---

## Next Steps

### Create a Transformation Rule
1. Go to `/Transformations` page
2. Select an entity type
3. Click "Add Rule"
4. Configure rule parameters
5. Save and monitor in dashboard

### Submit a Transformation Job
Use the API:
```bash
POST /api/transformation-jobs/submit
Content-Type: application/json

{
  "jobName": "My Transformation",
  "executionMode": "InMemory",
  "transformationRuleIds": [1, 2, 3],
  "inputData": "{...}"
}
```

### Create a Project
1. Go to Dashboard → Projects & Pipelines
2. Click "Create Project" 
3. Select transformation rules
4. Define project name and description
5. Save and manage in dashboard

---

## Support & Documentation

For more detailed information, see:
- **UI Guide**: `DASHBOARD_UI_GUIDE.md` - Comprehensive feature documentation
- **Architecture**: See service README for technical details
- **API Reference**: Check controller comments for endpoint documentation

---

## Service Information

- **Service Name**: Transformation Service
- **Default Port**: 5020
- **Dashboard URL**: `/TransformationDashboard`
- **API Base**: `/api/`
- **Rule Management**: `/Transformations`

---

Last Updated: November 29, 2025

