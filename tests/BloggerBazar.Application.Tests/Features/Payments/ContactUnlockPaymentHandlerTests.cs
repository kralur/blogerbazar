using BloggerBazar.Application.Abstractions.Payments;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Payments;

public sealed class ContactUnlockPaymentHandlerTests
{
    [Fact]
    public async Task Creates_pending_order_for_another_profile()
    {
        var target = BloggerProfile.Create(202, "Madina", "Tashkent", ["Lifestyle"]);
        var orders = new InMemoryPaymentOrderRepository();
        var handler = new CreateContactUnlockOrderHandler(
            new InMemoryBloggerRepository(target),
            new InMemoryBusinessRepository(),
            new InMemoryContactUnlockRepository(),
            orders,
            new FixedPricing(),
            new SpyUnitOfWork());

        var result = await handler.Handle(new CreateContactUnlockOrderCommand(101, ContactTargetType.Blogger, target.Id), CancellationToken.None);

        Assert.Equal(PaymentOrderStatus.Pending, result.Status);
        Assert.Equal(29000, result.AmountUzs);
        Assert.Single(orders.Orders);
    }

    [Fact]
    public async Task Marks_order_paid_and_unlocks_contact_once()
    {
        var order = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, Guid.NewGuid(), 29000);
        var orders = new InMemoryPaymentOrderRepository(order);
        var unlocks = new InMemoryContactUnlockRepository();
        var handler = new ConfirmContactUnlockPaymentHandler(orders, unlocks, new SpyUnitOfWork());

        var result = await handler.Handle(new ConfirmContactUnlockPaymentCommand(order.Reference, "click-1", 29000), CancellationToken.None);
        await handler.Handle(new ConfirmContactUnlockPaymentCommand(order.Reference, "click-1", 29000), CancellationToken.None);

        Assert.True(result.IsUnlocked);
        Assert.Equal(PaymentOrderStatus.Paid, order.Status);
        Assert.Single(unlocks.Unlocks);
    }

    [Fact]
    public async Task Reuses_an_existing_pending_order_for_the_same_contact()
    {
        var target = BloggerProfile.Create(202, "Madina", "Tashkent", ["Lifestyle"]);
        var pendingOrder = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, target.Id, 29000);
        var orders = new InMemoryPaymentOrderRepository(pendingOrder);
        var handler = new CreateContactUnlockOrderHandler(
            new InMemoryBloggerRepository(target),
            new InMemoryBusinessRepository(),
            new InMemoryContactUnlockRepository(),
            orders,
            new FixedPricing(),
            new SpyUnitOfWork());

        var result = await handler.Handle(new CreateContactUnlockOrderCommand(101, ContactTargetType.Blogger, target.Id), CancellationToken.None);

        Assert.Equal(pendingOrder.Reference, result.Reference);
        Assert.Single(orders.Orders);
    }

    [Fact]
    public async Task Rejects_confirmation_with_another_amount()
    {
        var order = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, Guid.NewGuid(), 29000);
        var handler = new ConfirmContactUnlockPaymentHandler(new InMemoryPaymentOrderRepository(order), new InMemoryContactUnlockRepository(), new SpyUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ConfirmContactUnlockPaymentCommand(order.Reference, "click-1", 1), CancellationToken.None));
    }

    [Fact]
    public async Task Creates_invoice_only_for_pending_order_owner()
    {
        var order = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, Guid.NewGuid(), 29000);
        var gateway = new FakeTelegramPaymentGateway();
        var handler = new CreateContactUnlockTelegramInvoiceHandler(new InMemoryPaymentOrderRepository(order), gateway);

        var invoice = await handler.Handle(new CreateContactUnlockTelegramInvoiceCommand(order.Reference, 101), CancellationToken.None);

        Assert.Equal(order.Reference, invoice.Reference);
        Assert.Equal("https://t.me/invoice", invoice.InvoiceLink);
        Assert.Equal(order.Reference, gateway.Request?.Payload);
        Assert.Equal(29000, gateway.Request?.AmountUzs);
    }

    [Fact]
    public async Task Rejects_checkout_for_a_different_payer()
    {
        var order = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, Guid.NewGuid(), 29000);
        var handler = new ValidateContactUnlockCheckoutHandler(new InMemoryPaymentOrderRepository(order));

        var result = await handler.Handle(new ValidateContactUnlockCheckoutCommand(order.Reference, 202, 29000), CancellationToken.None);

        Assert.False(result.IsApproved);
    }

    private sealed class FixedPricing : IContactUnlockPricing
    {
        public int AmountUzs => 29000;
    }

    private sealed class FakeTelegramPaymentGateway : ITelegramPaymentGateway
    {
        public TelegramInvoiceRequest? Request { get; private set; }

        public Task<string> CreateInvoiceLinkAsync(TelegramInvoiceRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult("https://t.me/invoice");
        }

        public Task AnswerPreCheckoutQueryAsync(string queryId, bool isApproved, string? errorMessage, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile target) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == target.Id ? target : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class InMemoryBusinessRepository : IBusinessProfileRepository
    {
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(null);
    }

    private sealed class InMemoryPaymentOrderRepository(params PaymentOrder[] paymentOrders) : IPaymentOrderRepository
    {
        public List<PaymentOrder> Orders { get; } = [.. paymentOrders];

        public Task AddAsync(PaymentOrder paymentOrder, CancellationToken cancellationToken)
        {
            Orders.Add(paymentOrder);
            return Task.CompletedTask;
        }

        public Task<PaymentOrder?> GetByReferenceAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult<PaymentOrder?>(Orders.SingleOrDefault(order => order.Reference == reference));

        public Task<PaymentOrder?> GetPendingContactUnlockAsync(long payerTelegramUserId, ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
            Task.FromResult<PaymentOrder?>(Orders.FirstOrDefault(order =>
                order.PayerTelegramUserId == payerTelegramUserId
                && order.TargetType == targetType
                && order.TargetId == targetId
                && order.Status == PaymentOrderStatus.Pending));
    }

    private sealed class InMemoryContactUnlockRepository : IContactUnlockRepository
    {
        public List<ContactUnlock> Unlocks { get; } = [];

        public Task AddAsync(ContactUnlock contactUnlock, CancellationToken cancellationToken)
        {
            Unlocks.Add(contactUnlock);
            return Task.CompletedTask;
        }

        public Task<ContactUnlock?> GetAsync(long viewerTelegramUserId, ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
            Task.FromResult<ContactUnlock?>(Unlocks.SingleOrDefault(unlock =>
                unlock.ViewerTelegramUserId == viewerTelegramUserId && unlock.TargetType == targetType && unlock.TargetId == targetId));
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
