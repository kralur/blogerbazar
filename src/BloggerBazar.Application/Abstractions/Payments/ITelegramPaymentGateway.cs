namespace BloggerBazar.Application.Abstractions.Payments;

public interface ITelegramPaymentGateway
{
    Task<string> CreateInvoiceLinkAsync(TelegramInvoiceRequest request, CancellationToken cancellationToken);
    Task AnswerPreCheckoutQueryAsync(string queryId, bool isApproved, string? errorMessage, CancellationToken cancellationToken);
}

public sealed record TelegramInvoiceRequest(string Title, string Description, string Payload, int AmountUzs);
