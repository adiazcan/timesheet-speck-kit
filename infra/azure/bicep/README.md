# Azure Bicep Infrastructure as Code

This directory contains Bicep templates for deploying Azure infrastructure for the HR Chat Agent.

## Files

### blob-lifecycle-policy.bicep

Configures Azure Blob Storage lifecycle management for audit logs with automatic tier transitions and 7-year retention.

**Purpose**: Implements FR-014d compliance requirement - retain audit logs for 7 years regardless of conversation deletion requests.

**Lifecycle Policy:**
- **Hot Tier (0-90 days)**: Immediate access for recent audit logs
- **Cool Tier (91-365 days)**: Lower cost for infrequent access
- **Archive Tier (365+ days)**: Lowest cost for rare access
- **Automatic Deletion (7 years)**: Compliant with retention requirements

**Parameters:**
- `storageAccountName` (required): Name of the existing storage account
- `coolTierTransitionDays` (optional): Days before moving to cool tier (default: 90)
- `archiveTierTransitionDays` (optional): Days before moving to archive tier (default: 365)
- `deletionDays` (optional): Days before deletion (default: 2555 = 7 years)

**Deployment:**

```bash
# Deploy to existing resource group with existing storage account
az deployment group create \
  --resource-group <resource-group-name> \
  --template-file blob-lifecycle-policy.bicep \
  --parameters storageAccountName=<storage-account-name>

# Deploy with custom retention periods
az deployment group create \
  --resource-group <resource-group-name> \
  --template-file blob-lifecycle-policy.bicep \
  --parameters \
    storageAccountName=<storage-account-name> \
    coolTierTransitionDays=60 \
    archiveTierTransitionDays=180 \
    deletionDays=3650
```

**Verification:**

```bash
# Check lifecycle policy status
az storage account management-policy show \
  --account-name <storage-account-name> \
  --resource-group <resource-group-name>
```

**Notes:**
- This policy applies ONLY to blobs in the `audit-logs/` container prefix
- Blob versioning is supported if enabled on the storage account
- Transition times are based on last modification date (baseBlob) or creation date (version)
- Archive tier access requires rehydration (priority standard: up to 15 hours)

## Prerequisites

- Azure CLI installed and authenticated
- Existing Azure Storage Account
- Storage account must have hierarchical namespace enabled (Azure Data Lake Storage Gen2)
- Storage account must be in a resource group

## Cost Optimization

The lifecycle policy automatically moves audit logs through progressively cheaper storage tiers:

| Tier | Days | Cost (per GB/month) | Use Case |
|------|------|---------------------|----------|
| Hot | 0-90 | ~$0.018 | Recent logs, frequent access |
| Cool | 91-365 | ~$0.010 | Occasional access (investigations) |
| Archive | 365+ | ~$0.002 | Compliance retention, rare access |

**Example**: 1TB of audit logs over 7 years
- Hot tier (3 months): 1TB × $0.018 × 3 = $54/year
- Cool tier (9 months): 1TB × $0.010 × 9 = $90/year
- Archive tier (6 years): 1TB × $0.002 × 72 = $144/year
- **Total: ~$288/year vs ~$216/year in hot tier only** (25% savings)

## Compliance

This lifecycle policy ensures compliance with:
- **FR-014d**: 7-year audit log retention requirement
- **GDPR**: Right to be forgotten (conversation data) while preserving audit trails
- **Data residency**: Logs remain in configured Azure region throughout lifecycle

## Related Files

- `/src/HRAgent.Api/Services/AuditLogger.cs` - Service that writes audit logs to blob storage
- `/src/HRAgent.Contracts/Models/AuditLogEntry.cs` - Audit log data model
- `/specs/001-hr-chat-agent/data-model.md` - Audit log schema and retention policy

## Troubleshooting

**Policy not applying to existing blobs:**
- Lifecycle policies apply to blob modifications/creations AFTER policy deployment
- Existing blobs are evaluated daily (can take 24-48 hours for first transition)

**Archive tier access errors:**
- Archive blobs must be rehydrated before access
- Use priority rehydration for faster access (higher cost)

**Policy conflicts:**
- Only one management policy per storage account
- Extend this template if multiple container policies are needed

## References

- [Azure Blob Storage Lifecycle Management](https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview)
- [Bicep Storage Account Reference](https://learn.microsoft.com/en-us/azure/templates/microsoft.storage/storageaccounts)
- [Access Tiers Best Practices](https://learn.microsoft.com/en-us/azure/storage/blobs/access-tiers-overview)
