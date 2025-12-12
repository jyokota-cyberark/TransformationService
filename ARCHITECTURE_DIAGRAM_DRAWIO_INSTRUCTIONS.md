# Architecture Diagram - Draw.io Instructions

## Complete Transformation System Architecture

### Setup
1. Open draw.io (https://app.diagrams.net/)
2. Create new blank diagram
3. Set canvas to A3 Landscape (File → Page Setup → Paper Size: A3, Orientation: Landscape)
4. Enable grid (View → Grid)

---

## Layer 1: External Systems (Top - Purple/Gray)

### Kafka Infrastructure (Top Left)
1. **Container**: Large rounded rectangle (600x200px)
   - Fill: Light purple (#E1D5E7)
   - Border: Purple (#9673A6), 2px
   - Label: "Kafka Infrastructure" (bold, 16pt)

2. **Inside Kafka Container**, add 3 components horizontally:
   - **Zookeeper**: Rectangle (150x80px)
     - Fill: Light gray (#F5F5F5)
     - Icon: Database icon
     - Label: "Zookeeper\nPort: 2181"
   
   - **Kafka Broker**: Rectangle (180x80px)
     - Fill: Light purple (#E1D5E7)
     - Icon: Server icon
     - Label: "Kafka Broker\nPort: 9092"
   
   - **Schema Registry**: Rectangle (180x80px)
     - Fill: Light purple (#E1D5E7)
     - Icon: Document icon
     - Label: "Schema Registry\nPort: 8085"

3. **Below Kafka Broker**, add topics (small cylinders):
   - "user-changes" (150x40px, light blue)
   - "inventory-user-items" (150x40px, light blue)
   - "transformation-job-status" (150x40px, light blue)

### PostgreSQL Database (Top Right)
1. **Container**: Rounded rectangle (300x200px)
   - Fill: Light blue (#DAE8FC)
   - Border: Blue (#6C8EBF), 2px
   - Label: "PostgreSQL" (bold, 16pt)

2. **Inside PostgreSQL**, add 3 databases (cylinders, 120x60px each):
   - **inventorypoc_users**
     - Fill: Light blue (#DAE8FC)
     - Label: "inventorypoc_users\nPort: 5432"
   
   - **transformationengine**
     - Fill: Light blue (#DAE8FC)
     - Label: "transformationengine\nPort: 5432"
   
   - **inventorypoc_inventory**
     - Fill: Light blue (#DAE8FC)
     - Label: "inventorypoc_inventory\nPort: 5432"

---

## Layer 2: Core Services (Middle - Blue/Green)

### User Management Service (Left)
1. **Container**: Rounded rectangle (400x500px)
   - Fill: Light blue (#D5E8D4)
   - Border: Green (#82B366), 3px
   - Label: "User Management Service\nPort: 5010" (bold, 16pt)

2. **Inside Container**, stack vertically:

   **API Layer** (Top section):
   - Rectangle (350x100px), fill: white
   - Label: "API Controllers" (bold)
   - Add 4 small boxes inside (80x30px each):
     - "UsersApiController"
     - "TransformationConfigController"
     - "TransformationJobsController"
     - "SyncApiController"

   **Service Layer** (Middle section):
   - Rectangle (350x120px), fill: light yellow (#FFF4E6)
   - Label: "Services" (bold)
   - Add 5 small boxes (70x30px each):
     - "UserService"
     - "UserSyncService"
     - "ConfigService"
     - "SyncHistoryService"
     - "KafkaEnrichmentService"

   **Integration Layer** (Bottom section):
   - Rectangle (350x100px), fill: light green (#E8F5E9)
   - Label: "Transformation Integration" (bold)
   - Add 3 boxes (110x30px each):
     - "IIntegratedTransformationService"
     - "TransformationModeRouter"
     - "JobQueueManagementService"

   **UI Layer** (Bottom):
   - Rectangle (350x80px), fill: light orange (#FFE6CC)
   - Label: "Razor Pages UI" (bold)
   - Add 3 boxes (110x25px):
     - "Users.cshtml"
     - "TransformationRules.cshtml"
     - "TransformationJobs.cshtml"

### Transformation Service (Center-Right)
1. **Container**: Rounded rectangle (450x500px)
   - Fill: Light orange (#FFE6CC)
   - Border: Orange (#D79B00), 3px
   - Label: "Transformation Service\nPort: 5020" (bold, 16pt)

2. **Inside Container**, stack vertically:

   **API Layer** (Top):
   - Rectangle (400x100px), fill: white
   - Label: "API Controllers" (bold)
   - Add 5 boxes (75x30px):
     - "TransformationRulesController"
     - "TransformationJobsController"
     - "TransformationProjectsController"
     - "SparkJobsController"
     - "RuleVersionsController"

   **Core Services** (Middle-Top):
   - Rectangle (400x100px), fill: light yellow (#FFF4E6)
   - Label: "Core Services" (bold)
   - Add 4 boxes (95x30px):
     - "TransformationJobService"
     - "TransformationProjectService"
     - "RuleVersioningService"
     - "SparkJobBuilderService"

   **Transformation Engines** (Middle):
   - Rectangle (400x120px), fill: light green (#E8F5E9)
   - Label: "Transformation Engines" (bold)
   - Add 3 boxes (125x35px):
     - "Sidecar Engine\n(In-Process)"
     - "Spark Engine\n(Distributed)"
     - "External API\n(Fallback)"

   **Background Services** (Bottom):
   - Rectangle (400x80px), fill: light purple (#E1D5E7)
   - Label: "Background Services" (bold)
   - Add 3 boxes (125x30px):
     - "TransformationQueueProcessor"
     - "KafkaJobStatusConsumer"
     - "SparkJobStatusPoller"

   **Dashboard UI** (Bottom):
   - Rectangle (400x60px), fill: light orange (#FFE6CC)
   - Label: "Dashboard UI" (bold)
   - Text: "TransformationDashboard.cshtml\n(Real-time job monitoring)"

### Inventory Service (Far Right)
1. **Container**: Rounded rectangle (300x300px)
   - Fill: Light cyan (#D5E8F5)
   - Border: Blue (#6C8EBF), 3px
   - Label: "Inventory Service\nPort: 5002" (bold, 16pt)

2. **Inside Container**:
   - Rectangle (250x80px): "API Controllers"
   - Rectangle (250x80px): "Services"
   - Rectangle (250x80px): "Kafka Consumer"
   - Small text: "Consumes transformed user data"

---

## Layer 3: Execution Engines (Bottom - Yellow/Orange)

### Spark Cluster (Bottom Left)
1. **Container**: Rounded rectangle (350x200px)
   - Fill: Light yellow (#FFF4E6)
   - Border: Orange (#D79B00), 2px
   - Label: "Apache Spark Cluster" (bold, 14pt)

2. **Inside Container**:
   - **Spark Master**: Rectangle (150x60px)
     - Fill: Yellow (#FFEB3B)
     - Label: "Spark Master\nPort: 7077, 8080"
   
   - **Spark Worker**: Rectangle (150x60px)
     - Fill: Light yellow (#FFF9C4)
     - Label: "Spark Worker\nPort: 8081"
   
   - **Job Execution**: Small box (150x40px)
     - Text: "ETL Jobs\nTransformation Jobs"

### Airflow (Bottom Center)
1. **Container**: Rounded rectangle (300x200px)
   - Fill: Light teal (#B2DFDB)
   - Border: Teal (#00897B), 2px
   - Label: "Apache Airflow" (bold, 14pt)

2. **Inside Container**:
   - Rectangle (250x60px): "Airflow Webserver\nPort: 8082"
   - Rectangle (250x60px): "Airflow Scheduler"
   - Rectangle (250x60px): "DAG Definitions"
   - Small text: "Orchestrates scheduled jobs"

### Hangfire (Bottom Right)
1. **Container**: Rounded rectangle (250x200px)
   - Fill: Light green (#C8E6C9)
   - Border: Green (#388E3C), 2px
   - Label: "Hangfire" (bold, 14pt)

2. **Inside Container**:
   - Rectangle (200x60px): "Hangfire Dashboard"
   - Rectangle (200x60px): "Job Queue"
   - Rectangle (200x60px): "Recurring Jobs"
   - Small text: "Background job processing"

---

## Connections and Data Flows

### User Creation Flow (Blue Arrows - Solid)
1. **Arrow 1**: User Management Service → PostgreSQL (inventorypoc_users)
   - Style: Blue, solid, 3px
   - Label: "1. Save User"

2. **Arrow 2**: User Management Service → Transformation Integration
   - Style: Blue, solid, 3px
   - Label: "2. Request Transform"

3. **Arrow 3**: Transformation Integration → TransformationJobQueue (in PostgreSQL)
   - Style: Blue, solid, 3px
   - Label: "3. Queue Job"

4. **Arrow 4**: User Management Service → Kafka (user-changes topic)
   - Style: Blue, solid, 3px
   - Label: "4. Publish Event"

### Transformation Processing Flow (Green Arrows - Solid)
1. **Arrow 5**: TransformationQueueProcessor → TransformationJobQueue
   - Style: Green, solid, 3px
   - Label: "5. Poll Jobs"

2. **Arrow 6**: TransformationQueueProcessor → Sidecar Engine
   - Style: Green, solid, 3px
   - Label: "6. Execute Transform"

3. **Arrow 7**: Sidecar Engine → TransformationRules (PostgreSQL)
   - Style: Green, solid, 3px
   - Label: "7. Load Rules"

4. **Arrow 8**: Sidecar Engine → PostgreSQL (inventorypoc_users)
   - Style: Green, solid, 3px
   - Label: "8. Update User\n(transformedData)"

### Spark Job Flow (Orange Arrows - Dashed)
1. **Arrow 9**: Transformation Service → Spark Master
   - Style: Orange, dashed, 2px
   - Label: "Submit Job"

2. **Arrow 10**: Spark Master → Spark Worker
   - Style: Orange, dashed, 2px
   - Label: "Distribute"

3. **Arrow 11**: Spark Worker → PostgreSQL
   - Style: Orange, dashed, 2px
   - Label: "Read/Write Data"

4. **Arrow 12**: Spark Worker → Kafka (transformation-job-status)
   - Style: Orange, dashed, 2px
   - Label: "Status Updates"

### Kafka Consumer Flow (Purple Arrows - Solid)
1. **Arrow 13**: Kafka (user-changes) → Inventory Service
   - Style: Purple, solid, 2px
   - Label: "Consume Events"

2. **Arrow 14**: Kafka (transformation-job-status) → KafkaJobStatusConsumer
   - Style: Purple, solid, 2px
   - Label: "Job Status Updates"

### Orchestration Flows (Teal Arrows - Dotted)
1. **Arrow 15**: Airflow → Transformation Service
   - Style: Teal, dotted, 2px
   - Label: "Trigger Scheduled Jobs"

2. **Arrow 16**: Hangfire → Transformation Service
   - Style: Teal, dotted, 2px
   - Label: "Background Jobs"

---

## Legend (Bottom Right Corner)

Create a small box (250x200px) with:

**Title**: "Legend" (bold)

**Components**:
- Green box: "User Management Service"
- Orange box: "Transformation Service"
- Blue box: "Inventory Service"
- Purple box: "Kafka Infrastructure"
- Yellow box: "Execution Engines"

**Arrows**:
- Blue solid: "User Creation Flow"
- Green solid: "Transformation Processing"
- Orange dashed: "Spark Job Execution"
- Purple solid: "Kafka Events"
- Teal dotted: "Orchestration"

**Data Stores**:
- Cylinder: "Database"
- Small cylinder: "Kafka Topic"

---

## Key Annotations

Add text boxes with arrows pointing to key areas:

1. **Near User Management Service**:
   - "Entry point for user operations"
   - "Integrates transformation via sidecar"

2. **Near Transformation Service**:
   - "Central transformation orchestration"
   - "Manages rules, jobs, and execution"

3. **Near TransformationJobQueue**:
   - "Async job queue for resilience"
   - "Jobs processed by background service"

4. **Near Sidecar Engine**:
   - "In-process transformation"
   - "JavaScript rule execution"

5. **Near Spark Cluster**:
   - "Distributed batch processing"
   - "Large-scale transformations"

6. **Near Kafka**:
   - "Event streaming backbone"
   - "Decouples services"

---

## Color Coding Summary

- **Light Green (#D5E8D4)**: User Management Service
- **Light Orange (#FFE6CC)**: Transformation Service
- **Light Cyan (#D5E8F5)**: Inventory Service
- **Light Purple (#E1D5E7)**: Kafka Infrastructure
- **Light Blue (#DAE8FC)**: PostgreSQL Databases
- **Light Yellow (#FFF4E6)**: Spark Cluster
- **Light Teal (#B2DFDB)**: Airflow
- **Light Green (#C8E6C9)**: Hangfire

---

## Final Touches

1. **Title** (Top center):
   - Large text box: "Transformation System Architecture"
   - Font: Arial, 24pt, bold
   - Subtitle: "Microservices-based Data Transformation Platform"

2. **Version Info** (Bottom left):
   - Small text: "Version: 1.0"
   - Date: "December 2025"
   - Author: "Transformation Team"

3. **Grouping**:
   - Use dotted rectangles to group related components
   - Label groups: "API Layer", "Service Layer", "Data Layer", "Execution Layer"

4. **Zoom and Export**:
   - Zoom to fit (View → Fit)
   - Export as PNG (File → Export as → PNG, Scale: 200%)
   - Export as PDF for documentation

---

## Tips for Draw.io

1. **Alignment**: Use Arrange → Align to align components
2. **Distribution**: Use Arrange → Distribute to space evenly
3. **Layers**: Use layers for different aspects (Edit → Edit Layers)
4. **Styles**: Save custom styles for reuse (Format → Copy Style)
5. **Connectors**: Use waypoints for cleaner arrow routing
6. **Labels**: Double-click arrows to add labels
7. **Groups**: Select multiple items and Ctrl+G to group

---

## Alternative: Simplified View

For presentations, create a simplified version:

1. **3 Main Boxes**:
   - User Management Service (left)
   - Transformation Service (center)
   - Inventory Service (right)

2. **Supporting Infrastructure** (top):
   - Kafka (one box)
   - PostgreSQL (one box)

3. **Execution Engines** (bottom):
   - Spark/Airflow/Hangfire (combined box)

4. **5 Main Arrows**:
   - User → Transform → Process → Store → Consume

This creates a high-level view that's easier to understand in presentations.

