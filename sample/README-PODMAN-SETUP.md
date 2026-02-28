# FxMap Telemetry Demo - Podman Setup (MacBook ARM)

This guide is specifically for users running:
- **MacBook with Apple Silicon** (M1/M2/M3 - ARM64 architecture)
- **Podman** instead of Docker
- **Existing local infrastructure** (NATS, PostgreSQL, MongoDB already running)

## Quick Start

```bash
cd sample
./start-observability-podman.sh
```

This script will:
1. Check prerequisites (Podman, .NET SDK)
2. Verify your local NATS, PostgreSQL, MongoDB are running
3. Start only the observability stack (Jaeger, Prometheus, Grafana)
4. Provide URLs and next steps

## What Gets Started

Using `docker-compose-observability.yml`, only the observability services are started:

| Service    | Image                          | ARM64 Support | Port  |
|------------|--------------------------------|---------------|-------|
| Jaeger     | jaegertracing/all-in-one:1.53  | ✅ Native     | 16686 |
| Prometheus | prom/prometheus:v2.48.1        | ✅ Native     | 9090  |
| Grafana    | grafana/grafana:10.2.3         | ✅ Native     | 3000  |

All images are multi-architecture and will run natively on Apple Silicon without emulation.

## Prerequisites Check

### 1. Verify Podman is installed
```bash
podman --version
# Should show: podman version 4.x.x or higher
```

If not installed:
```bash
brew install podman
podman machine init
podman machine start
```

### 2. Install podman-compose (recommended)
```bash
pip3 install podman-compose

# Verify
podman-compose --version
```

Alternatively, use `docker-compose` with Podman:
```bash
# docker-compose can work with Podman via socket
export DOCKER_HOST=unix://$(podman info --format '{{.Host.RemoteSocket.Path}}')
docker-compose --version
```

### 3. Verify your local infrastructure

**NATS**
```bash
# Check if NATS is running
nc -z localhost 4222 && echo "NATS is running" || echo "NATS is NOT running"

# Check NATS monitoring endpoint
curl http://localhost:8222/varz
```

**PostgreSQL**
```bash
# Check if PostgreSQL is running
nc -z localhost 5432 && echo "PostgreSQL is running" || echo "PostgreSQL is NOT running"

# List databases (using podman)
podman exec -it <your-postgres-container> psql -U postgres -c "\l"

# Create required databases if they don't exist
podman exec -it <your-postgres-container> psql -U postgres <<EOF
CREATE DATABASE "FxMapTestService1";
CREATE DATABASE "FxMapTestOtherService1";
CREATE DATABASE "FxMapTestService2";
CREATE DATABASE "FxMapTestService3";
EOF
```

**MongoDB**
```bash
# Check if MongoDB is running
nc -z localhost 27017 && echo "MongoDB is running" || echo "MongoDB is NOT running"

# Test connection (using podman)
podman exec -it <your-mongo-container> mongosh --eval "db.adminCommand('ping')"
```

## Starting the Observability Stack

### Option 1: Using the script (easiest)
```bash
cd sample
./start-observability-podman.sh
```

### Option 2: Manual with podman-compose
```bash
cd sample
podman-compose -f docker-compose-observability.yml up -d

# View logs
podman-compose -f docker-compose-observability.yml logs -f

# Check status
podman-compose -f docker-compose-observability.yml ps
```

### Option 3: Manual with docker-compose + Podman
```bash
cd sample
export DOCKER_HOST=unix://$(podman info --format '{{.Host.RemoteSocket.Path}}')
docker-compose -f docker-compose-observability.yml up -d
```

### Option 4: Direct podman commands
```bash
# Create network
podman network create fxmap-network

# Start Jaeger
podman run -d \
  --name fxmap-jaeger \
  --network fxmap-network \
  -e COLLECTOR_OTLP_ENABLED=true \
  -p 16686:16686 \
  -p 4317:4317 \
  -p 4318:4318 \
  -p 14268:14268 \
  jaegertracing/all-in-one:1.53

# Start Prometheus
podman run -d \
  --name fxmap-prometheus \
  --network fxmap-network \
  -p 9090:9090 \
  -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml:ro \
  -v prometheus-data:/prometheus \
  prom/prometheus:v2.48.1 \
  --config.file=/etc/prometheus/prometheus.yml \
  --storage.tsdb.path=/prometheus

# Start Grafana
podman run -d \
  --name fxmap-grafana \
  --network fxmap-network \
  -p 3000:3000 \
  -e GF_SECURITY_ADMIN_USER=admin \
  -e GF_SECURITY_ADMIN_PASSWORD=admin \
  -v grafana-data:/var/lib/grafana \
  -v $(pwd)/grafana/provisioning:/etc/grafana/provisioning:ro \
  grafana/grafana:10.2.3
```

## Running the Demo

Once the observability stack is running:

### 1. Start FxMap Services

**Terminal 1 - Service1 (GraphQL API)**
```bash
cd sample/Service1
dotnet run
```
Wait for: `Now listening on: https://localhost:5001`

**Terminal 2 - Service2 (Worker)**
```bash
cd sample/Service2
dotnet run
```

**Terminal 3 - Service3 (Worker)**
```bash
cd sample/Service3
dotnet run
```

### 2. Make GraphQL Request

Open https://localhost:5001/graphql and execute:

```graphql
query {
  members {
    id
    name
    addresses {
      id
      provinceId
      province {
        id
        name
        country {
          id
          name
        }
      }
    }
    additionalData {
      id
      name
    }
  }
}
```

### 3. View Distributed Traces in Jaeger

1. Open http://localhost:16686
2. Select service: **Service1**
3. Click **Find Traces**
4. Click on a trace to see:
   - Complete request flow across services
   - Parent-child span relationships
   - Timing breakdown
   - Tags: `fxmap.attribute`, `fxmap.transport`, `messaging.system`

### 4. View Metrics in Grafana

1. Open http://localhost:3000 (login: admin/admin)
2. Navigate to: **Dashboards → FxMap → FxMap Framework Overview**
3. Observe panels:
   - Request rate (requests/sec)
   - Request duration (P50, P95 latency)
   - Active requests (current in-flight)
   - Error rate
   - Items returned

### 5. Query Metrics in Prometheus

Open http://localhost:9090 and try these queries:

```promql
# Total request count
fxmap_request_count_total

# Request rate per second
rate(fxmap_request_count_total[1m])

# P95 request duration
histogram_quantile(0.95, rate(fxmap_request_duration_milliseconds_bucket[1m]))

# Active requests
fxmap_request_active

# Error rate
rate(fxmap_request_errors_total[1m])
```

## Troubleshooting

### Podman-specific issues

**Issue: Containers can't access host services (NATS, PostgreSQL, MongoDB)**

Solution: Use `host.containers.internal` instead of `localhost` in connection strings:

```csharp
// Instead of:
cfg.AddNats(c => c.Url("nats://localhost:4222"));

// Use:
cfg.AddNats(c => c.Url("nats://host.containers.internal:4222"));
```

Or run containers with `--network host`:
```bash
podman run --network host ...
```

**Issue: Permission denied when accessing volumes**

Solution: Add `:Z` or `:z` to volume mounts for SELinux:
```bash
-v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml:ro,Z
```

**Issue: Port conflicts**

Check what's using the port:
```bash
lsof -i :16686  # Jaeger
lsof -i :9090   # Prometheus
lsof -i :3000   # Grafana
```

Stop conflicting services or change ports in `docker-compose-observability.yml`.

### ARM64-specific issues

**Issue: Image doesn't support ARM64**

All images in `docker-compose-observability.yml` are verified ARM64-compatible:
- ✅ jaegertracing/all-in-one:1.53
- ✅ prom/prometheus:v2.48.1
- ✅ grafana/grafana:10.2.3

If you see platform warnings, explicitly specify:
```bash
podman pull --platform linux/arm64 jaegertracing/all-in-one:1.53
```

**Issue: Slow performance**

Check if Rosetta emulation is being used:
```bash
podman inspect fxmap-jaeger | grep Architecture
# Should show: "Architecture": "arm64"
```

### Service connection issues

**NATS connection refused**

Verify NATS is accessible:
```bash
curl http://localhost:8222/varz
```

Check FxMap service logs for connection errors.

**PostgreSQL connection timeout**

Verify PostgreSQL is accessible:
```bash
psql -h localhost -U postgres -c "SELECT version();"
```

Ensure databases exist:
```bash
psql -h localhost -U postgres -c "\l" | grep FxMapTest
```

**No traces in Jaeger**

1. Check services have OTLP exporter configured
2. Verify Jaeger is receiving data:
   ```bash
   podman logs fxmap-jaeger | grep -i otlp
   ```
3. Check for errors in service console output

**No metrics in Prometheus**

1. Check Prometheus targets: http://localhost:9090/targets
2. Verify services expose `/metrics` endpoint (if using Prometheus scraping)
3. Check `prometheus.yml` configuration

## Managing the Observability Stack

### View logs
```bash
# All services
podman-compose -f docker-compose-observability.yml logs -f

# Specific service
podman logs -f fxmap-jaeger
podman logs -f fxmap-prometheus
podman logs -f fxmap-grafana
```

### Check status
```bash
podman-compose -f docker-compose-observability.yml ps

# Or individual containers
podman ps | grep fxmap
```

### Restart services
```bash
# All services
podman-compose -f docker-compose-observability.yml restart

# Specific service
podman restart fxmap-jaeger
```

### Stop services
```bash
# Stop all
podman-compose -f docker-compose-observability.yml down

# Stop but keep volumes (data persists)
podman-compose -f docker-compose-observability.yml stop
```

### Clean up
```bash
# Stop and remove containers, networks (keep volumes)
podman-compose -f docker-compose-observability.yml down

# Remove everything including volumes (clean slate)
podman-compose -f docker-compose-observability.yml down -v

# Or manually
podman stop fxmap-jaeger fxmap-prometheus fxmap-grafana
podman rm fxmap-jaeger fxmap-prometheus fxmap-grafana
podman volume rm prometheus-data grafana-data
podman network rm fxmap-network
```

## Resource Usage on Apple Silicon

Expected resource consumption (measured on M1 MacBook Pro):

| Service    | CPU (idle) | CPU (load) | Memory  |
|------------|------------|------------|---------|
| Jaeger     | ~1%        | ~5%        | ~150 MB |
| Prometheus | ~2%        | ~8%        | ~250 MB |
| Grafana    | ~1%        | ~3%        | ~180 MB |

Total: **~580 MB RAM, ~5-15% CPU**

Monitor with:
```bash
podman stats
```

## Next Steps

1. ✅ **Explore the demo**: Follow [README-TELEMETRY-DEMO.md](./README-TELEMETRY-DEMO.md)
2. 📖 **Understand the implementation**: Read [../docs/telemetry.md](../docs/telemetry.md)
3. 🎨 **Customize Grafana dashboards**: Modify [grafana/provisioning/dashboards/json/fxmap-overview.json](./grafana/provisioning/dashboards/json/fxmap-overview.json)
4. 🚨 **Set up alerting**: Configure Prometheus alert rules
5. 🔄 **Integrate with CI/CD**: Add telemetry checks to your pipeline

## Additional Resources

- **Podman Documentation**: https://docs.podman.io/
- **Jaeger on ARM**: https://www.jaegertracing.io/docs/latest/getting-started/
- **Prometheus on ARM**: https://prometheus.io/download/
- **Grafana on ARM**: https://grafana.com/grafana/download?platform=arm
- **OpenTelemetry .NET**: https://opentelemetry.io/docs/instrumentation/net/

## Support

If you encounter issues:

1. Check logs: `podman-compose -f docker-compose-observability.yml logs`
2. Verify prerequisites: Run `./start-observability-podman.sh` to check
3. Review troubleshooting section above
4. Check existing infrastructure is running (NATS, PostgreSQL, MongoDB)
