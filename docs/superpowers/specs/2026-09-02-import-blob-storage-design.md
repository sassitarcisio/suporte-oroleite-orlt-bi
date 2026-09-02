# Import Blob Storage Design

## Objective

Store every submitted import file in the existing private Azure Blob container so
that imports remain available for audit after Container App restarts or scaling.

## Scope

- Production uses Azure Blob Storage through the API managed identity.
- Development continues to use the existing local file store.
- Raw files remain private with no automatic retention deletion.
- Existing import parsing, batch status, SHA-256 audit field, and API contract
  remain unchanged.

## Components

| Component | Responsibility |
| --- | --- |
| `BlobImportFileStore` | Uploads an import stream to the `imports` container, calculates SHA-256, and returns its private blob URI. |
| Service registration | Selects Blob storage when production Blob configuration is present; otherwise selects local storage. |
| Bicep role assignment | Grants `Storage Blob Data Contributor` to the existing API managed identity at the storage-account scope. |
| Container App environment | Supplies Blob service URI and container name without account keys or SAS tokens. |

## Data Flow

1. An administrator sends a CSV to `POST /api/imports`.
2. `CsvImportWorkflow` writes the original stream through `IImportFileStore`.
3. In production, `BlobImportFileStore` writes to `imports/YYYY/MM/<guid>-<safe-file-name>.csv` with `DefaultAzureCredential`.
4. The workflow records the blob URI and SHA-256 on `ImportBatch`, then parses and persists the data rows as it does today.

## Error Handling

- Upload failures propagate as import failures and do not create a completed batch.
- Blob authorization failures remain visible in API logs; no storage credentials are exposed to the client or repository.
- The import endpoint continues to require the `Administrador` role.

## Testing

- Unit tests cover blob key generation, checksum calculation, and returned private URI.
- Registration tests cover production Blob selection and development local fallback.
- Existing import workflow and endpoint tests remain green.
- Post-deployment verification uploads the approved POWER CSV and checks the completed batch status and stored blob URI.

## Out Of Scope

- Public file access, download endpoints, lifecycle deletion rules, and retroactive migration of old local files.
