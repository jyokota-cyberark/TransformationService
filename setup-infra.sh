#!/bin/bash

# Transformation Service Infrastructure Setup Script
# This script sets up the required infrastructure for the Transformation Service

set -e

echo "🚀 Transformation Service Infrastructure Setup"
echo "=============================================="

# Function to check if Docker is running
check_docker() {
    if ! docker info > /dev/null 2>&1; then
        echo "❌ Error: Docker is not running. Please start Docker and try again."
        exit 1
    fi
    echo "✅ Docker is running"
}

# Function to check if Docker Compose is available
check_docker_compose() {
    if ! command -v docker &> /dev/null; then
        echo "❌ Error: Docker Compose is not available. Please install Docker Compose."
        exit 1
    fi
    echo "✅ Docker Compose is available"
}

# Function to start PostgreSQL
start_postgres() {
    echo ""
    echo "📦 Starting PostgreSQL..."
    docker compose -f docker-compose.postgres.yml up -d
    
    echo "⏳ Waiting for PostgreSQL to be ready..."
    sleep 5
    
    if docker ps | grep -q "transformation-postgres"; then
        echo "✅ PostgreSQL is running on port 5432"
    else
        echo "❌ Failed to start PostgreSQL"
        exit 1
    fi
}

# Function to start Spark cluster
start_spark() {
    echo ""
    echo "⚡ Starting Spark cluster..."
    docker compose -f docker-compose.spark.yml up -d
    
    echo "⏳ Waiting for Spark cluster to be ready..."
    sleep 10
    
    if docker ps | grep -q "transformation-spark-master"; then
        echo "✅ Spark Master is running"
        echo "   - Master Web UI: http://localhost:8080"
        echo "   - Master URL: spark://localhost:7077"
    else
        echo "❌ Failed to start Spark Master"
        exit 1
    fi
    
    if docker ps | grep -q "transformation-spark-worker"; then
        echo "✅ Spark Workers are running"
        echo "   - Worker 1 Web UI: http://localhost:8081"
        echo "   - Worker 2 Web UI: http://localhost:8082"
    else
        echo "⚠️  Warning: Some Spark workers may not have started"
    fi
}

# Function to stop all services
stop_services() {
    echo ""
    echo "🛑 Stopping all services..."
    docker compose -f docker-compose.spark.yml down
    docker compose -f docker-compose.postgres.yml down
    echo "✅ All services stopped"
}

# Function to show service status
show_status() {
    echo ""
    echo "📊 Service Status:"
    echo "==================="
    docker compose -f docker-compose.postgres.yml ps
    docker compose -f docker-compose.spark.yml ps
}

# Main script logic
case "${1:-start}" in
    start)
        check_docker
        check_docker_compose
        start_postgres
        start_spark
        echo ""
        echo "🎉 Infrastructure setup complete!"
        echo ""
        echo "📌 Access Points:"
        echo "   - PostgreSQL: localhost:5432 (user: postgres, password: postgres)"
        echo "   - Spark Master UI: http://localhost:8080"
        echo "   - Spark Master: spark://localhost:7077"
        echo "   - Spark Worker 1 UI: http://localhost:8081"
        echo "   - Spark Worker 2 UI: http://localhost:8082"
        echo ""
        echo "💡 To stop all services: ./setup-infra.sh stop"
        echo "💡 To check status: ./setup-infra.sh status"
        ;;
    stop)
        stop_services
        ;;
    status)
        show_status
        ;;
    restart)
        stop_services
        sleep 2
        check_docker
        check_docker_compose
        start_postgres
        start_spark
        echo "✅ Services restarted"
        ;;
    *)
        echo "Usage: $0 {start|stop|status|restart}"
        echo ""
        echo "Commands:"
        echo "  start    - Start all infrastructure services (default)"
        echo "  stop     - Stop all infrastructure services"
        echo "  status   - Show status of all services"
        echo "  restart  - Restart all services"
        exit 1
        ;;
esac
