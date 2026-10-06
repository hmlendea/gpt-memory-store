# Operations Guide

This document covers day-to-day operational tasks for running GPT Memory Store in production.

## Service Management

### systemd (Linux)

```bash
# Start
sudo systemctl start gpt-memory-store

# Stop
sudo systemctl stop gpt-memory-store

# Restart
sudo systemctl restart gpt-memory-store

# Status
sudo systemctl status gpt-memory-store

# Follow logs
sudo journalctl -u gpt-memory-store -f

# Enable at boot
sudo systemctl enable gpt-memory-store

# Disable at boot
sudo systemctl disable gpt-memory-store
```

### Docker

```bash
# Start
docker start gpt-memory-store

# Stop
docker stop gpt-memory-store

# Restart
docker restart gpt-memory-store

# Logs
docker logs -f gpt-memory-store

# Remove container (data persists in volumes)
docker rm gpt-memory-store
```

### Kubernetes

```bash
# Scale
kubectl scale deployment gpt-memory-store --replicas=1

# Restart rollout
kubectl rollout restart deployment/gpt-memory-store

# Check status
kubectl rollout status deployment/gpt-memory-store

# View logs
kubectl logs -l app=gpt-memory-store -f

# Describe pod
kubectl describe pod -l app=gpt-memory-store
```

---

## Configuration Changes

### Applying Changes

**All configuration changes require application restart.**

```bash
# systemd
sudo systemctl restart gpt-memory-store

# Docker
docker restart gpt-memory-store

# Kubernetes
kubectl rollout restart deployment/gpt-memory-store
```

### Common Configuration Updates

| Change | Environment Variable | Restart Required |
|--------|---------------------|------------------|
| API Key rotation | `SecuritySettings__ApiKey` | Yes |
| Store path | `DataStoreSettings__MemoryStorePath` | Yes |
| Log path | `NuciLoggerSettings__LogFilePath` | Yes |
| Enable/disable file logging | `NuciLoggerSettings__IsFileOutputEnabled` | Yes |

### API Key Rotation

```bash
# 1. Generate new key
NEW_KEY=$(openssl rand -base64 32)

# 2. Update secret in your platform
# (Kubernetes secret, Docker env, systemd Environment, etc.)

# 3. Restart service
sudo systemctl restart gpt-memory-store

# 4. Update clients to use new key
# 5. Verify access works
curl -H "Authorization: Bearer $NEW_KEY" https://memory.example.com/Memories
```

---

## Monitoring

### Health Checks

No built-in health endpoint. Implement infrastructure-level checks:

```bash
# Liveness (process running)
systemctl is-active gpt-memory-store

# Readiness (API responding)
curl -f -s -H "Authorization: Bearer $API_KEY" https://memory.example.com/Memories > /dev/null && echo "ready" || echo "not ready"
```

### Key Metrics to Monitor

| Metric | Source | Alert Threshold |
|--------|--------|-----------------|
| Process CPU | node_exporter / cAdvisor | > 80% sustained |
| Process Memory | node_exporter / cAdvisor | > 80% of limit |
| Disk usage (store) | node_exporter | > 80% |
| Disk usage (logs) | node_exporter | > 80% |
| Request latency (p99) | Reverse proxy (nginx/Caddy) | > 1s |
| Error rate (5xx) | Reverse proxy | > 1% |
| Request rate | Reverse proxy | Baseline + 3σ |

### Log Monitoring

Structured logs (when enabled) contain:

```json
{
  "Operation": "CreateMemory",
  "Status": "Success",
  "Id": "a1b2c3d4...",
  "Content": "User prefers...",
  "Source": "profile",
  "Confidence": 0.95
}
```

**Useful queries:**

```bash
# Error rate
grep "Failure" /var/log/gpt-memory-store/logfile.log | wc -l

# Operations per minute
grep "Started" /var/log/gpt-memory-store/logfile.log | \
  jq -r '.Timestamp' | \
  date -f - +%s | \
  uniq -c

# Failed operations
grep "Failure" /var/log/gpt-memory-store/logfile.log | jq '.Operation'

# Specific memory operations
grep "a1b2c3d4" /var/log/gpt-memory-store/logfile.log | jq .
```

---

## Backup Operations

### Manual Backup

```bash
#!/bin/bash
# backup-memories.sh

set -euo pipefail

STORE_PATH="/var/lib/gpt-memory-store/memories.json"
BACKUP_DIR="/backups/gpt-memory-store"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="$BACKUP_DIR/memories-$TIMESTAMP.json"

mkdir -p "$BACKUP_DIR"

# Optional: suspend writes (scale to 0, or use read-only mode if available)
# systemctl stop gpt-memory-store

cp "$STORE_PATH" "$BACKUP_FILE"

# Verify
COUNT=$(jq length "$BACKUP_FILE")
echo "Backed up $COUNT memories to $BACKUP_FILE"

# Optional: resume writes
# systemctl start gpt-memory-store
```

### Automated Backup (cron)

```bash
# /etc/cron.d/gpt-memory-store-backup
# Run daily at 2:30 AM
30 2 * * * gptmemory /usr/local/bin/backup-memories.sh >> /var/log/gpt-memory-store-backup.log 2>&1
```

### Backup Retention

```bash
# Keep last 30 daily backups
find /backups/gpt-memory-store -name "memories-*.json" -mtime +30 -delete

# Keep last 12 monthly backups (first of month)
find /backups/gpt-memory-store -name "memories-*-01-*.json" -mtime +365 -delete
```

### Restore Procedure

```bash
#!/bin/bash
# restore-memories.sh

set -euo pipefail

BACKUP_FILE="$1"
STORE_PATH="/var/lib/gpt-memory-store/memories.json"

if [[ ! -f "$BACKUP_FILE" ]]; then
    echo "Backup file not found: $BACKUP_FILE"
    exit 1
fi

# Verify backup is valid JSON
jq empty "$BACKUP_FILE" || { echo "Invalid JSON in backup"; exit 1; }

COUNT=$(jq length "$BACKUP_FILE")
echo "Restoring $COUNT memories from $BACKUP_FILE"

# Stop service
systemctl stop gpt-memory-store

# Backup current store (just in case)
cp "$STORE_PATH" "$STORE_PATH.pre-restore-$(date +%s)"

# Restore
cp "$BACKUP_FILE" "$STORE_PATH"

# Verify
RESTORED_COUNT=$(jq length "$STORE_PATH")
echo "Restored $RESTORED_COUNT memories"

# Start service
systemctl start gpt-memory-store

# Verify service responds
sleep 2
curl -f -s -H "Authorization: Bearer $API_KEY" https://memory.example.com/Memories > /dev/null \
    && echo "Restore verified successfully" \
    || echo "WARNING: Service not responding after restore"
```

---

## Log Management

### Log Rotation (logrotate)

```bash
# /etc/logrotate.d/gpt-memory-store
/var/log/gpt-memory-store/logfile.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    notifempty
    create 640 gptmemory gptmemory
    sharedscripts
    postrotate
        systemctl reload gpt-memory-store > /dev/null 2>&1 || true
    endscript
}
```

### Manual Log Cleanup

```bash
# Compress old logs
gzip /var/log/gpt-memory-store/logfile.log.1

# Remove logs older than 90 days
find /var/log/gpt-memory-store -name "logfile.log.*.gz" -mtime +90 -delete
```

---

## Store Maintenance

### Inspect Store

```bash
# Count records
jq length /var/lib/gpt-memory-store/memories.json

# View recent records
jq 'sort_by(.CreatedTimestamp) | reverse | .[0:10]' /var/lib/gpt-memory-store/memories.json

# Find by source
jq '.[] | select(.Source == "profile-sync")' /var/lib/gpt-memory-store/memories.json

# Check for duplicates
jq 'group_by(.Id) | map(select(length > 1))' /var/lib/gpt-memory-store/memories.json
```

### Compact Store (if needed)

The JSON file is rewritten on every `SaveChanges()`. No separate compaction needed.

### Repair Corrupted Store

```bash
# If JSON is malformed
jq empty /var/lib/gpt-memory-store/memories.json 2>/dev/null || {
    echo "Store corrupted, restoring from backup..."
    # Restore from latest backup
    LATEST=$(ls -t /backups/gpt-memory-store/memories-*.json | head -1)
    cp "$LATEST" /var/lib/gpt-memory-store/memories.json
    systemctl restart gpt-memory-store
}
```

---

## Security Operations

### Audit API Key Access

```bash
# Check who has access to the secret
# (Platform-specific: Kubernetes RBAC, Docker secrets, systemd credentials, etc.)

# Rotate key periodically (e.g., quarterly)
# See "API Key Rotation" above
```

### File Permissions Audit

```bash
# Verify store permissions
ls -la /var/lib/gpt-memory-store/
# Should be: drwxr-x--- gptmemory gptmemory
#            -rw-r----- gptmemory gptmemory memories.json

# Verify log permissions
ls -la /var/log/gpt-memory-store/
# Should be: drwxr-x--- gptmemory gptmemory
#            -rw-r----- gptmemory gptmemory logfile.log
```

### Dependency Vulnerability Scan

```bash
# Check for vulnerable packages
dotnet list GptMemoryStore/GptMemoryStore.csproj package --vulnerable --include-transitive

# Update if needed
dotnet add GptMemoryStore/GptMemoryStore.csproj package NuciAPI --version <latest>
# ... test thoroughly ...
```

---

## Incident Response

### Service Down

```bash
# 1. Check process
systemctl status gpt-memory-store

# 2. Check logs
journalctl -u gpt-memory-store -n 50

# 3. Check disk space
df -h /var/lib/gpt-memory-store

# 4. Check port binding
ss -tlnp | grep 5081

# 5. Restart
systemctl restart gpt-memory-store
```

### High Error Rate

```bash
# 1. Check recent errors
journalctl -u gpt-memory-store -n 100 | grep -i error

# 2. Check structured logs
grep "Failure" /var/log/gpt-memory-store/logfile.log | tail -20 | jq .

# 3. Common causes:
#    - Invalid API key from clients
#    - Disk full
#    - Permission denied on store/log
#    - NuciDAL repository exceptions
```

### Data Corruption

```bash
# 1. Stop service immediately
systemctl stop gpt-memory-store

# 2. Assess damage
jq empty /var/lib/gpt-memory-store/memories.json

# 3. Restore from backup
./restore-memories.sh /backups/gpt-memory-store/memories-20240115-023000.json

# 4. Verify and restart
```

---

## Capacity Planning

### Storage Growth

| Records | Est. Size | RAM (loaded) |
|---------|-----------|--------------|
| 1,000 | ~500 KB | ~5 MB |
| 10,000 | ~5 MB | ~50 MB |
| 100,000 | ~50 MB | ~500 MB |
| 1,000,000 | ~500 MB | ~5 GB |

**Recommendation:** Plan for alternative storage (SQLite, PostgreSQL) beyond 50K records.

### Performance Baseline

Typical latencies (local SSD, single instance):
- `GET /Memories` (100 records): ~5-10 ms
- `GET /Memories` (10,000 records): ~50-100 ms
- `POST /Memories`: ~10-20 ms
- `PUT /Memories`: ~10-20 ms
- `DELETE /Memories/{id}`: ~5-10 ms

---

## Upgrade Checklist

### Pre-Upgrade

- [ ] Review release notes for breaking changes
- [ ] Backup current store and configuration
- [ ] Test upgrade in staging environment
- [ ] Verify integration tests pass
- [ ] Schedule maintenance window (if required)

### Upgrade

- [ ] Download new release
- [ ] Stop service
- [ ] Replace binary / update container image
- [ ] Verify configuration compatibility
- [ ] Start service
- [ ] Run smoke tests

### Post-Upgrade

- [ ] Verify API responds correctly
- [ ] Check logs for errors
- [ ] Monitor metrics for anomalies
- [ ] Confirm client compatibility
- [ ] Clean up old version after validation period

---

## Runbook Templates

### Daily Checks

- [ ] Service running (`systemctl is-active`)
- [ ] API responding (health check)
- [ ] Disk space > 20% free
- [ ] No error spikes in logs
- [ ] Backup completed successfully

### Weekly Checks

- [ ] Review error rates and latency trends
- [ ] Verify backup integrity (test restore)
- [ ] Check for dependency updates
- [ ] Review access logs for anomalies

### Monthly Checks

- [ ] Rotate API key
- [ ] Review and update dependencies
- [ ] Capacity planning review
- [ ] Security scan
- [ ] Documentation updates