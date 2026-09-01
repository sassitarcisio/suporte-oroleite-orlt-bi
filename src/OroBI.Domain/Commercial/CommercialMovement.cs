namespace OroBI.Domain.Commercial;

public sealed class CommercialMovement
{
    private CommercialMovement(Guid importBatchId, DateOnly movementDate, string seller, string movementType, decimal totalValue, decimal quantity)
    {
        Id = Guid.NewGuid();
        ImportBatchId = importBatchId;
        MovementDate = movementDate;
        Seller = seller;
        MovementType = movementType;
        TotalValue = totalValue;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid ImportBatchId { get; private set; }
    public DateOnly MovementDate { get; private set; }
    public string Seller { get; private set; } = string.Empty;
    public string Brand { get; private set; } = string.Empty;
    public string Group { get; private set; } = string.Empty;
    public string Family { get; private set; } = string.Empty;
    public string MovementType { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string CustomerCode { get; private set; } = string.Empty;
    public string DocumentNumber { get; private set; } = string.Empty;
    public string? SourceSystem { get; private set; }
    public string? SourceRecordKey { get; private set; }
    public decimal TotalValue { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }

    public static CommercialMovement Create(Guid importBatchId, DateOnly movementDate, string seller, string movementType, decimal totalValue, decimal quantity) =>
        new(importBatchId, movementDate, seller, movementType, totalValue, quantity);

    public static CommercialMovement CreateFromImport(
        Guid importBatchId,
        DateOnly movementDate,
        string seller,
        string brand,
        string group,
        string movementType,
        string city,
        string customerName,
        string productName,
        decimal totalValue,
        decimal quantity,
        decimal unitCost,
        string customerCode,
        string documentNumber)
    {
        var movement = new CommercialMovement(importBatchId, movementDate, seller, movementType, totalValue, quantity)
        {
            Brand = brand,
            Group = group,
            City = city,
            CustomerName = customerName,
            ProductName = productName,
            UnitCost = unitCost,
            CustomerCode = customerCode,
            DocumentNumber = documentNumber
        };

        return movement;
    }

    public void SetSourceIdentity(string sourceSystem, string sourceRecordKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRecordKey);
        SourceSystem = sourceSystem.Trim().ToUpperInvariant();
        SourceRecordKey = sourceRecordKey.Trim();
    }
}
