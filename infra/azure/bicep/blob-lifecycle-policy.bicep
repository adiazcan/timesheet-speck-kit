// Blob Storage Lifecycle Management Policy for Audit Logs
// 7-Year Retention with Automated Tier Transitions
// FR-014d Compliance: Retain audit logs for 7 years regardless of conversation deletion

@description('Name of the storage account')
param storageAccountName string

@description('Resource group where storage account is deployed')
param resourceGroupName string = resourceGroup().name

@description('Location for the storage account')
param location string = resourceGroup().location

@description('Days before transitioning blobs to cool tier (default: 90 days)')
param coolTierTransitionDays int = 90

@description('Days before transitioning blobs to archive tier (default: 365 days)')
param archiveTierTransitionDays int = 365

@description('Days before deleting blobs (default: 2555 days = 7 years)')
param deletionDays int = 2555

// Reference existing storage account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

// Blob Service (lifecycle management is configured on blob service)
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' existing = {
  parent: storageAccount
  name: 'default'
}

// Management Policy for audit-logs container
resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          enabled: true
          name: 'audit-logs-lifecycle'
          type: 'Lifecycle'
          definition: {
            filters: {
              // Apply only to blobs in audit-logs container
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                'audit-logs/'
              ]
            }
            actions: {
              baseBlob: {
                // Transition to Cool tier after 90 days
                tierToCool: {
                  daysAfterModificationGreaterThan: coolTierTransitionDays
                }
                // Transition to Archive tier after 365 days (1 year)
                tierToArchive: {
                  daysAfterModificationGreaterThan: archiveTierTransitionDays
                }
                // Delete after 2555 days (7 years) - FR-014d compliance
                delete: {
                  daysAfterModificationGreaterThan: deletionDays
                }
              }
              // Also manage blob versions if versioning is enabled
              version: {
                tierToCool: {
                  daysAfterCreationGreaterThan: coolTierTransitionDays
                }
                tierToArchive: {
                  daysAfterCreationGreaterThan: archiveTierTransitionDays
                }
                delete: {
                  daysAfterCreationGreaterThan: deletionDays
                }
              }
            }
          }
        }
      ]
    }
  }
}

// Outputs for verification
output lifecyclePolicyId string = lifecyclePolicy.id
output lifecyclePolicyName string = lifecyclePolicy.name
output coolTierDays int = coolTierTransitionDays
output archiveTierDays int = archiveTierTransitionDays
output retentionDays int = deletionDays
output retentionYears string = '${deletionDays / 365} years'
