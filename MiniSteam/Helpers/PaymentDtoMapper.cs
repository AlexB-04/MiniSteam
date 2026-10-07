using MiniSteam.Models.DTOs;
using MiniSteam.Models.Entities;

namespace MiniSteam.Helpers;

public static class PaymentDtoMapper
{
    public static PaymentDto ToDto(this Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            Status = payment.Status.ToString(),
            Provider = payment.Provider,
            ProviderReference = payment.ProviderReference,
            Currency = payment.Currency,
            Subtotal = payment.Subtotal,
            TaxRate = payment.TaxRate,
            TaxAmount = payment.TaxAmount,
            Total = payment.Total,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt,
            ConfirmedAt = payment.ConfirmedAt,
            FailedAt = payment.FailedAt,
            RefundedAt = payment.RefundedAt,
            FailureReason = payment.FailureReason,
            RefundReference = payment.RefundReference,
            RefundReason = payment.RefundReason,
            RefundAmount = payment.RefundAmount,
            PurchaseId = payment.PurchaseId,
            ReceiptNumber = payment.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded
                ? $"MS-SBX-{payment.Id:00000000}"
                : null,
            Items = payment.Items
                .Select(item => new PaymentItemDto
                {
                    GameId = item.GameId,
                    GameName = item.GameName,
                    OriginalPrice = item.OriginalPrice,
                    DiscountPercent = item.DiscountPercent,
                    Price = item.Price
                })
                .ToList()
        };
    }
}
