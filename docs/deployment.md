# Deployment Guide

This document covers deployment considerations for GPT Memory Store.

## Deployment Model

GPT Memory Store is a **self-hosted ASP.NET Core application**. There is no hosted/SaaS offering. The operator is responsible for:

- Provisioning compute (VM, container, bare metal)
- Configuring networking and TLS
- Managing secrets
- Providing durable storage
- Monitoring and alerting
- Backup and disaster recovery

## Runtime Requirements

| Requirement | Specification |
|-------------|---------------|
| .NET Runtime | .NET 10.0 (included in self-contained releases) |
| Architecture | linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64, win-arm64 |
| Memory | Minimum 128 MB (typical ~50-100 MB) |
| Disk | Space for JSON store + logs + application |
| Network | Inbound HTTP/HTTPS on configured port |

## Deployment Options

### 1. Release Archives (Recommended)

Download pre-built self-contained executables from [GitHub Releases](https://github.com/hmlendea/gpt-memory-store/releases/latest).

```bash
# Linux x64 example
wget https://github.com/hmlendea/gpt-memory-store/releases/download/v1.0.4/gpt-memory-store-linux-x64.tar.gz
tar -xzf gpt-memory-store-linux-x64.tar.gz
cd gpt-memory-store-linux-x64

# Configure
export SecuritySettings__ApiKey="your-secure-key"
export DataStoreSettings__MemoryStorePath="/data/memories.json"

# Run
./GptMemoryStore --urls "http://0.0.0.0:5081"
```

### 2. Docker

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["GptMemoryStore/GptMemoryStore.csproj", "GptMemoryStore/"]
RUN dotnet restore "GptMemoryStore/GptMemoryStore.csproj"
COPY . .
WORKDIR "/src/GptMemoryStore"
RUN dotnet publish -c Release -o /app/publish --self-contained false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GptMemoryStore.dll"]
```

Build and run:
```bash
docker build -t gpt-memory-store .
docker run -d \
  -p 5081:5081 \
  -e SecuritySettings__ApiKey="your-secure-key" \
  -e DataStoreSettings__MemoryStorePath="/data/memories.json" \
  -v gpt-memory-data:/data \
  --name gpt-memory-store \
  gpt-memory-store
```

### 3. systemd Service (Linux)

Create `/etc/systemd/system/gpt-memory-store.service`:

```ini
[Unit]
Description=GPT Memory Store
After=network.target

[Service]
Type=notify
User=gptmemory
WorkingDirectory=/opt/gpt-memory-store
ExecStart=/opt/gpt-memory-store/GptMemoryStore --urls "http://0.0.0.0:5081"
Environment=SecuritySettings__ApiKey=your-secure-key
Environment=DataStoreSettings__MemoryStorePath=/var/lib/gpt-memory-store/memories.json
Environment=NuciLoggerSettings__LogFilePath=/var/log/gpt-memory-store/logfile.log
Restart=on-failure
RestartSec=5
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now gpt-memory-store
sudo journalctl -u gpt-memory-store -f
```

### 4. Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: gpt-memory-store
spec:
  replicas: 1
  selector:
    matchLabels:
      app: gpt-memory-store
  template:
    metadata:
      labels:
        app: gpt-memory-store
    spec:
      containers:
      - name: gpt-memory-store
        image: gpt-memory-store:latest
        ports:
        - containerPort: 5081
        env:
        - name: SecuritySettings__ApiKey
          valueFrom:
            secretKeyRef:
              name: gpt-memory-store-secrets
              key: api-key
        - name: DataStoreSettings__MemoryStorePath
          value: /data/memories.json
        - name: NuciLoggerSettings__LogFilePath
          value: /var/log/gpt-memory-store/logfile.log
        volumeMounts:
        - name: data
          mountPath: /data
        - name: logs
          mountPath: /var/log/gpt-memory-store
      volumes:
      - name: data
        persistentVolumeClaim:
          claimName: gpt-memory-store-data
      - name: logs
        persistentVolumeClaim:
          claimName: gpt-memory-store-logs
---
apiVersion: v1
kind: Service
metadata:
  name: gpt-memory-store
spec:
  selector:
    app: gpt-memory-store
  ports:
  - port: 80
    targetPort: 5081
```

---

## TLS/HTTPS Configuration

The application uses `app.UseHttpsRedirection()` but **does not terminate TLS itself**. Configure TLS at the reverse proxy:

### Nginx Example

```nginx
server {
    listen 443 ssl http2;
    server_name memory.example.com;

    ssl_certificate /etc/letsencrypt/live/memory.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/memory.example.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5081;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name memory.example.com;
    return 301 https://$server_name$request_uri;
}
```

### Caddy Example

```caddy
memory.example.com {
    reverse_proxy 127.0.0.1:5081
}
```

---

## Storage Considerations

### JSON Store

- **Single file**: All memories in one JSON array
- **No database**: No ACID transactions, no concurrent write coordination
- **File locking**: Relies on NuciDAL's file handling; verify behavior under concurrent writes
- **Durability**: Operator must ensure filesystem durability (RAID, backups, etc.)

### Recommended Storage Layout

```
/var/lib/gpt-memory-store/
├── memories.json          # Primary store (must persist across deployments)
└── backups/               # Optional: automated backups
    ├── memories-2024-01-15.json
    └── ...

/var/log/gpt-memory-store/
└── logfile.log            # Structured logs (if file output enabled)
```

### Persistence Across Deployments

**Critical**: Store the JSON file **outside** the application directory.

```
❌ Bad: /opt/gpt-memory-store-v1.0.4/Data/memories.json  (lost on upgrade)
✅ Good: /var/lib/gpt-memory-store/memories.json         (persists)
```

Use bind mounts, persistent volumes, or symlinks to achieve this.

---

## Backup and Restore

### Backup

```bash
# Suspend writes (optional: scale to 0 replicas, or use read-only mode if implemented)
# Copy the store file
cp /var/lib/gpt-memory-store/memories.json /backups/memories-$(date +%F).json

# Verify backup
jq length /backups/memories-$(date +%F).json
```

### Restore

```bash
# Stop service
systemctl stop gpt-memory-store

# Restore
cp /backups/memories-2024-01-15.json /var/lib/gpt-memory-store/memories.json

# Verify
jq length /var/lib/gpt-memory-store/memories.json

# Start service
systemctl start gpt-memory-store
```

### Automated Backup (cron example)

```bash
# /etc/cron.daily/gpt-memory-store-backup
#!/bin/bash
set -euo pipefail
BACKUP_DIR="/backups/gpt-memory-store"
STORE_PATH="/var/lib/gpt-memory-store/memories.json"
mkdir -p "$BACKUP_DIR"
cp "$STORE_PATH" "$BACK_DIR/memories-$(date +%F).json"
# Keep last 30 days
find "$BACKUP_DIR" -name "memories-*.json" -mtime +30 -delete
```

---

## Monitoring

### Health Checks

No built-in health endpoint. Implement at infrastructure level:

```bash
# Simple liveness check
curl -f http://127.0.0.1:5081/Memories -H "Authorization: Bearer $API_KEY" > /dev/null
```

### Logs

Structured logs (when enabled) include:
- Operation: CreateMemory, GetMemory, GetMemories, UpdateMemory, DeleteMemory
- Status: Started, Success, Failure
- Context: IDs, content, source, confidence, counts

Forward to log aggregation (ELK, Loki, Datadog, etc.) via:
- File tailing (Fluent Bit, Vector, Promtail)
- stdout/stderr (if file output disabled, logs go to console)

### Metrics

No built-in metrics. Consider:
- Reverse proxy metrics (nginx: requests, latency, errors)
- Process metrics (Prometheus node_exporter + custom exporter)
- Application-level metrics would require code changes

---

## Scaling Considerations

### Horizontal Scaling

**Not supported out of the box.** The JSON file store is not designed for concurrent multi-instance access.

Options:
1. **Single instance only** (recommended for simplicity)
2. **Shared network filesystem** (NFS, EFS, Azure Files) with file locking - verify NuciDAL behavior
3. **Externalize storage** - implement alternative `IFileRepository` (e.g., SQLite, PostgreSQL, Redis)

### Vertical Scaling

Increase resources for:
- Larger memory collections (more RAM for deserialization)
- Higher request throughput (more CPU)

---

## Security Hardening

### Network
- Bind to localhost only (`--urls "http://127.0.0.1:5081"`) behind reverse proxy
- Use firewall to restrict direct access to application port
- Enforce HTTPS at reverse proxy

### File Permissions
```bash
# Store directory: readable/writable by app user only
chown gptmemory:gptmemory /var/lib/gpt-memory-store
chmod 750 /var/lib/gpt-memory-store
chmod 640 /var/lib/gpt-memory-store/memories.json

# Log directory
chown gptmemory:gptmemory /var/log/gpt-memory-store
chmod 750 /var/log/gpt-memory-store
```

### Secrets
- Never hardcode API key
- Use platform secret management
- Rotate periodically
- Audit access

### Updates
```bash
# Monitor for security updates
dotnet list package --vulnerable --include-transitive

# Update dependencies
dotnet add package NuciAPI --version latest
dotnet add package NuciDAL --version latest
# ... test thoroughly ...
```

---

## Troubleshooting

### Common Issues

| Symptom | Cause | Resolution |
|---------|-------|------------|
| 500 on all requests | Invalid/missing API key | Verify `SecuritySettings__ApiKey` matches client |
| 500 on GET /Memories/{id} | Memory not found | Verify ID exists; check store file |
| Store not created | Permission denied | Ensure app user can write to parent directory |
| Logs not appearing | File output disabled or path unwritable | Check `NuciLoggerSettings__IsFileOutputEnabled` and permissions |
| Configuration not picked up | Wrong env var format | Use double underscore: `Section__Key` |

### Debug Mode

```bash
# Enable developer exception page
export ASPNETCORE_ENVIRONMENT=Development
./GptMemoryStore
```

### Log Analysis

```bash
# View structured logs
tail -f /var/log/gpt-memory-store/logfile.log | jq .

# Filter by operation
grep "CreateMemory" /var/log/gpt-memory-store/logfile.log | jq .

# Find failures
grep "Failure" /var/log/gpt-memory-store/logfile.log | jq .
```

---

## Upgrade Procedure

1. **Backup** current store and configuration
2. **Download** new release archive
3. **Extract** to new directory (e.g., `/opt/gpt-memory-store-v1.0.5`)
4. **Copy** configuration or re-apply environment variables
5. **Stop** old service
6. **Start** new service (pointing to same store path)
7. **Verify** functionality
8. **Clean up** old version after confirmation

```bash
# Example zero-downtime upgrade with systemd
systemctl stop gpt-memory-store
# ... swap binary / update symlink ...
systemctl start gpt-memory-store
```

---

## Rollback Procedure

1. Stop current version
2. Restore previous binary/symlink
3. Start previous version (same store path)
4. Verify

The JSON store format is forward/backward compatible within the same major version.