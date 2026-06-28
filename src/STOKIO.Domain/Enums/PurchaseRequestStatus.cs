namespace STOKIO.Domain.Enums;

public enum PurchaseRequestStatus
{
    PendingApproval = 1,
    Approved = 2,
    PartiallyReceived = 3,
    Received = 4,
    Cancelled = 5
}
