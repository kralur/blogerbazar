using BloggerBazar.Application.Abstractions.Payments;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
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
    public async Task Ignores_a_repeated_payment_delivery_without_sending_a_second_notification()
    {
        var order = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, Guid.NewGuid(), 29000);
        var orders = new InMemoryPaymentOrderRepository(order);
        var unlocks = new InMemoryContactUnlockRepository();
        var bot = new SpyTelegramBotClient();
        var handler = new ConfirmContactUnlockPaymentHandler(orders, unlocks, new SpyUnitOfWork(), bot);

        await handler.Handle(new ConfirmContactUnlockPaymentCommand(order.Reference, "telegram-charge-1", 29000, 101), CancellationToken.None);
        await handler.Handle(new ConfirmContactUnlockPaymentCommand(order.Reference, "telegram-charge-1", 29000, 101), CancellationToken.None);

        Assert.Single(unlocks.Unlocks);
        Assert.Equal(1, bot.NotificationCount);
    }

    [Fact]
    public async Task Treats_a_concurrent_unique_conflict_as_an_already_completed_payment()
    {
        var order = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, Guid.NewGuid(), 29000);
        order.MarkPaid("telegram-charge-1");
        var completedUnlock = ContactUnlock.Create(order);
        var orders = new ConflictPaymentOrderRepository(order);
        var unlocks = new ConflictContactUnlockRepository(completedUnlock);
        var bot = new SpyTelegramBotClient();
        var handler = new ConfirmContactUnlockPaymentHandler(orders, unlocks, new ConflictUnitOfWork(), bot);

        var result = await handler.Handle(new ConfirmContactUnlockPaymentCommand(order.Reference, "telegram-charge-1", 29000, 101), CancellationToken.None);

        Assert.True(result.IsUnlocked);
        Assert.Equal(1, unlocks.AddAttempts);
        Assert.Equal(0, bot.NotificationCount);
    }

    [Fact]
    public async Task Confirms_payment_and_unlock_inside_a_unit_of_work_transaction()
    {
        var order = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, Guid.NewGuid(), 29000);
        var unitOfWork = new TransactionSpyUnitOfWork();
        var handler = new ConfirmContactUnlockPaymentHandler(
            new InMemoryPaymentOrderRepository(order),
            new InMemoryContactUnlockRepository(),
            unitOfWork);

        await handler.Handle(new ConfirmContactUnlockPaymentCommand(order.Reference, "telegram-charge-1", 29000, 101), CancellationToken.None);

        Assert.Equal(1, unitOfWork.TransactionCount);
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
        var handler = new CreateContactUnlockTelegramInvoiceHandler(new InMemoryPaymentOrderRepository(order), gateway, new SpyUnitOfWork());

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
        var handler = new ValidateContactUnlockCheckoutHandler(new InMemoryPaymentOrderRepository(order), new SpyUnitOfWork());

        var result = await handler.Handle(new ValidateContactUnlockCheckoutCommand(order.Reference, 202, 29000), CancellationToken.None);

        Assert.False(result.IsApproved);
    }

    [Fact]
    public async Task Expires_an_old_order_and_creates_a_new_one()
    {
        var target = BloggerProfile.Create(202, "Madina", "Tashkent", ["Lifestyle"]);
        var expired = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, target.Id, 29000, DateTime.UtcNow.AddMinutes(-1));
        var orders = new InMemoryPaymentOrderRepository(expired);
        var handler = new CreateContactUnlockOrderHandler(
            new InMemoryBloggerRepository(target), new InMemoryBusinessRepository(), new InMemoryContactUnlockRepository(), orders, new FixedPricing(), new SpyUnitOfWork());

        var result = await handler.Handle(new CreateContactUnlockOrderCommand(101, ContactTargetType.Blogger, target.Id), CancellationToken.None);

        Assert.Equal(PaymentOrderStatus.Expired, expired.Status);
        Assert.NotEqual(expired.Reference, result.Reference);
        Assert.Equal(PaymentOrderStatus.Pending, result.Status);
    }

    [Fact]
    public async Task Rejects_checkout_for_an_expired_order()
    {
        var order = PaymentOrder.CreateContactUnlock(101, ContactTargetType.Blogger, Guid.NewGuid(), 29000, DateTime.UtcNow.AddMinutes(-1));
        var handler = new ValidateContactUnlockCheckoutHandler(new InMemoryPaymentOrderRepository(order), new SpyUnitOfWork());

        var result = await handler.Handle(new ValidateContactUnlockCheckoutCommand(order.Reference, 101, 29000), CancellationToken.None);

        Assert.False(result.IsApproved);
        Assert.Equal(PaymentOrderStatus.Expired, order.Status);
    }

    private sealed class FixedPricing : IContactUnlockPricing
    {
        public int AmountUzs => 29000;
        public TimeSpan PendingOrderLifetime => TimeSpan.FromMinutes(30);
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

        public Task<PaymentOrder?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken cancellationToken) =>
            Task.FromResult<PaymentOrder?>(Orders.SingleOrDefault(order => order.ProviderTransactionId == providerTransactionId));

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

    private sealed class SpyTelegramBotClient : ITelegramBotClient
    {
        public int NotificationCount { get; private set; }

        public Task SendStartMessageAsync(long chatId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendNotificationAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            NotificationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ConflictPaymentOrderRepository(PaymentOrder order) : IPaymentOrderRepository
    {
        private int providerTransactionLookups;

        public Task<PaymentOrder?> GetByReferenceAsync(string reference, CancellationToken cancellationToken) => Task.FromResult<PaymentOrder?>(order);

        public Task<PaymentOrder?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken cancellationToken) =>
            Task.FromResult<PaymentOrder?>(++providerTransactionLookups == 1 ? null : order);

        public Task AddAsync(PaymentOrder paymentOrder, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ConflictContactUnlockRepository(ContactUnlock completedUnlock) : IContactUnlockRepository
    {
        private int lookups;

        public int AddAttempts { get; private set; }

        public Task AddAsync(ContactUnlock contactUnlock, CancellationToken cancellationToken)
        {
            AddAttempts++;
            return Task.CompletedTask;
        }

        public Task<ContactUnlock?> GetAsync(long viewerTelegramUserId, ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
            Task.FromResult<ContactUnlock?>(++lookups == 1 ? null : completedUnlock);
    }

    private sealed class ConflictUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class TransactionSpyUnitOfWork : IUnitOfWork
    {
        public int TransactionCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        {
            TransactionCount++;
            return await operation(cancellationToken);
        }
    }
}
