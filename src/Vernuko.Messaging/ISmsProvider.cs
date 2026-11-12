using Vernuko.Core;

namespace Vernuko.Messaging;

/// <summary>
/// Contract for SMS provider adapters (Netgsm, İleti Merkezi, Verimor ... — planned for Phase 5).
/// No adapter is implemented yet; the application must not present SMS as available until one is.
/// Costs are always billed to the business' own provider account.
/// </summary>
public interface ISmsProvider
{
    string ProviderKey { get; }

    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken);

    Task<SmsDeliveryStatus> GetDeliveryStatusAsync(string providerMessageId, CancellationToken cancellationToken);
}

public sealed record SmsMessage(PhoneNumber To, string Text, string Originator, string IdempotencyKey, bool IsCommercial);

public sealed record SmsSendResult(bool Accepted, string? ProviderMessageId, string? ErrorCode, string? ErrorMessage, bool IsRetryable);

public enum SmsDeliveryStatus
{
    Unknown,
    Queued,
    Sent,
    Delivered,
    Failed,
    Expired,
}
