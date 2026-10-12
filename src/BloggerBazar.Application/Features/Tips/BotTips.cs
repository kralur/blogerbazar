using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Tips;

// D54: three times a week the bot sends one tip about a feature: what it does, where it is and how to use it.
public enum TipAudience
{
    All = 0,
    // Bloggers and brand faces: both apply to campaigns and get offers.
    Creator = 1,
    Blogger = 2,
    BrandFace = 3,
    Business = 4
}

// Numbers are stable ids: the user's last number is stored, so a tip keeps its number and new tips get new ones.
public sealed record BotTip(int Number, TipAudience Audience, string Route, BotText Button, BotText Text)
{
    public bool IsFor(MarketplaceRole? role) => Audience switch
    {
        TipAudience.All => true,
        TipAudience.Creator => role is MarketplaceRole.Blogger or MarketplaceRole.BrandFace,
        TipAudience.Blogger => role == MarketplaceRole.Blogger,
        TipAudience.BrandFace => role == MarketplaceRole.BrandFace,
        TipAudience.Business => role == MarketplaceRole.Business,
        _ => false
    };
}

public static class BotTipCatalog
{
    private static readonly BotText OpenHome = new("Открыть главную", "Bosh sahifani ochish");
    private static readonly BotText FindBlogger = new("Найти блогера", "Bloger topish");
    private static readonly BotText OpenCampaigns = new("Смотреть кампании", "Kampaniyalarni ko‘rish");
    private static readonly BotText MyCampaigns = new("Мои кампании", "Kampaniyalarim");
    private static readonly BotText OpenRequests = new("Открыть заявки", "Arizalarni ochish");
    private static readonly BotText OpenProfile = new("Открыть профиль", "Profilni ochish");
    private static readonly BotText OpenSettings = new("Открыть настройки", "Sozlamalarni ochish");
    private static readonly BotText OpenApp = new("Открыть приложение", "Ilovani ochish");

    private static BotTip Tip(int number, TipAudience audience, string route, BotText button, string russian, string uzbek) =>
        new(number, audience, route, button, new BotText($"💡 {russian}", $"💡 {uzbek}"));

    public static readonly IReadOnlyList<BotTip> All =
    [
        Tip(1, TipAudience.All, "/", OpenHome,
            "На главной есть ряд «Подходят вам»: кампании или авторы под ваши категории и город. Загляните, там могут быть новые возможности.",
            "Bosh sahifada «Sizga mos» qatori bor: toifalaringiz va shahringizga mos kampaniyalar yoki mualliflar. Kirib ko‘ring, yangi imkoniyatlar bo‘lishi mumkin."),
        Tip(2, TipAudience.Business, "/search", FindBlogger,
            "В «Поиске» есть фильтры по платформе, подписчикам, ER и цене. Нажмите «Фильтры», чтобы за минуту найти блогера под ваш бюджет.",
            "«Qidiruv»da platforma, obunachilar, ER va narx bo‘yicha filtrlar bor. Byudjetingizga mos blogerni bir daqiqada topish uchun «Filtrlar»ni bosing."),
        Tip(3, TipAudience.Creator, "/campaigns", OpenCampaigns,
            "Во вкладке «Кампании» бизнесы ищут авторов прямо сейчас. Откройте кампанию и нажмите «Откликнуться на кампанию». Пара слов о себе повышает шанс, что выберут вас.",
            "«Kampaniyalar» bo‘limida bizneslar hozir mualliflar qidirmoqda. Kampaniyani oching va «Kampaniyaga javob berish»ni bosing. O‘zingiz haqingizda bir-ikki so‘z tanlanish ehtimolini oshiradi."),
        Tip(4, TipAudience.Business, "/my-campaigns", MyCampaigns,
            "Не хотите искать сами? Создайте кампанию: Профиль → «Мои кампании» → «Создать кампанию». Блогеры откликнутся сами, а вы выберете лучших.",
            "O‘zingiz qidirishni xohlamaysizmi? Kampaniya yarating: Profil → «Kampaniyalarim» → «Kampaniya yaratish». Blogerlar o‘zlari javob yuborishadi, siz esa eng yaxshilarini tanlaysiz."),
        Tip(5, TipAudience.All, "/requests", OpenRequests,
            "Все отклики, предложения и сделки собраны во вкладке «Заявки». Красный кружок значит, что там ждут вашего ответа.",
            "Barcha javoblar, takliflar va bitimlar «Arizalar» bo‘limida. Qizil doiracha u yerda javobingiz kutilayotganini bildiradi."),
        Tip(6, TipAudience.Business, "/search", FindBlogger,
            "Понравился блогер? Откройте его профиль и нажмите «Предложить сотрудничество». Блогер получит предложение в боте и ответит в течение 48 часов.",
            "Bloger yoqdimi? Uning profilini oching va «Hamkorlik taklif qilish»ni bosing. Bloger taklifni botda oladi va 48 soat ichida javob beradi."),
        Tip(7, TipAudience.Creator, "/profile", OpenProfile,
            "Профиль с фото, категориями, городом и ценами выбирают чаще. Откройте «Профиль» и проверьте, всё ли заполнено.",
            "Surati, toifalari, shahri va narxlari bor profil ko‘proq tanlanadi. «Profil»ni ochib, hammasi to‘ldirilganini tekshiring."),
        Tip(8, TipAudience.Business, "/search", FindBlogger,
            "Нажмите значок закладки на карточке блогера, чтобы сохранить его. Сохранённые авторы ждут вас в Профиле, раздел «Сохранённые блогеры».",
            "Blogerni saqlash uchun kartasidagi xatcho‘p belgisini bosing. Saqlangan mualliflar Profilda, «Saqlangan blogerlar» bo‘limida turadi."),
        Tip(9, TipAudience.Creator, "/requests", OpenRequests,
            "После публикации добавьте в сделку ссылку на пост и просмотры. Так бизнес сразу увидит результат вашей работы.",
            "E’lon chiqqanidan keyin bitimga post havolasi va ko‘rishlar sonini qo‘shing. Shunda biznes ishingiz natijasini darhol ko‘radi."),
        Tip(10, TipAudience.Business, "/requests", OpenRequests,
            "Когда автор добавит ссылку на публикацию, проверьте её и нажмите «Подтвердить». В сделке появится отметка «Подтверждено бизнесом».",
            "Muallif e’lon havolasini qo‘shganda, uni tekshirib «Tasdiqlash»ni bosing. Bitimda «Biznes tasdiqlagan» belgisi paydo bo‘ladi."),
        Tip(11, TipAudience.All, "/requests", OpenRequests,
            "После завершения сделки оставьте отзыв. Отзывы открываются, когда обе стороны оценят друг друга, поэтому оценки получаются честными.",
            "Bitim yakunlangach fikr qoldiring. Fikrlar ikki tomon ham bir-birini baholaganda ochiladi, shuning uchun baholar xolis bo‘ladi."),
        Tip(12, TipAudience.All, "/settings", OpenSettings,
            "В Профиль → «Настройки» можно выбрать язык (русский или узбекский) и тему: светлую, тёмную или как в Telegram.",
            "Profil → «Sozlamalar»da tilni (rus yoki o‘zbek) va mavzuni tanlash mumkin: yorug‘, qorong‘i yoki Telegram’dagidek."),
        Tip(13, TipAudience.All, "/profile", OpenProfile,
            "У одного аккаунта может быть несколько ролей: блогер, бизнес и бренд-фейс. Переключаться можно в «Профиле», регистрироваться заново не нужно.",
            "Bitta akkauntda bir nechta rol bo‘lishi mumkin: bloger, biznes va brend-yuz. Rolni «Profil»da almashtirish mumkin, qayta ro‘yxatdan o‘tish shart emas."),
        Tip(14, TipAudience.All, "/profile", OpenProfile,
            "Уезжаете или сейчас нет времени? В «Профиле» нажмите «Скрыть профиль», и вас временно не будет в каталоге. Вернуться можно в любой момент.",
            "Safarga ketyapsizmi yoki hozir vaqtingiz yo‘qmi? «Profil»da «Profilni yashirish»ni bosing, siz katalogda vaqtincha ko‘rinmaysiz. Istalgan payt qaytishingiz mumkin."),
        Tip(15, TipAudience.All, "/", OpenApp,
            "Потяните экран вниз, чтобы обновить список. Новые отклики, кампании и сделки появятся сразу.",
            "Ro‘yxatni yangilash uchun ekranni pastga torting. Yangi javoblar, kampaniyalar va bitimlar darhol paydo bo‘ladi."),
        Tip(16, TipAudience.All, "/", OpenApp,
            "Если профиль или кампания похожи на обман или спам, нажмите красную кнопку «Пожаловаться» вверху страницы. Мы проверяем каждую жалобу.",
            "Profil yoki kampaniya firibgarlik yoki spamga o‘xshasa, sahifa tepasidagi qizil «Shikoyat qilish» tugmasini bosing. Har bir shikoyatni tekshiramiz."),
        Tip(17, TipAudience.All, "/profile", OpenProfile,
            "Номер телефона подтверждается через Telegram, и его видят только партнёры по сделке. Сменить номер можно командой /phone в этом чате.",
            "Telefon raqami Telegram orqali tasdiqlanadi va uni faqat bitim bo‘yicha hamkorlar ko‘radi. Raqamni shu chatda /phone buyrug‘i bilan o‘zgartirish mumkin."),
        Tip(18, TipAudience.Business, "/search", FindBlogger,
            "Кроме блогеров есть бренд-фейсы: люди, которые снимутся в рекламе вашего бренда. В «Поиске» переключите тип автора на «Бренд-фейсы».",
            "Blogerlardan tashqari brend-yuzlar ham bor: brendingiz reklamasida suratga tushadigan insonlar. «Qidiruv»da muallif turini «Brend-yuzlar»ga almashtiring."),
        Tip(19, TipAudience.BrandFace, "/brand-face", OpenProfile,
            "Добавьте в анкету несколько хороших фото. Бизнес выбирает бренд-фейса по ним, поэтому анкеты с фото смотрят чаще.",
            "Anketangizga bir nechta yaxshi surat qo‘shing. Biznes brend-yuzni ular bo‘yicha tanlaydi, shuning uchun suratli anketalar ko‘proq ko‘riladi."),
        Tip(20, TipAudience.Business, "/my-campaigns", MyCampaigns,
            "В кампании укажите бюджет, срок и что именно нужно сделать. Чем понятнее задача, тем больше подходящих откликов.",
            "Kampaniyada byudjet, muddat va aynan nima qilish kerakligini yozing. Vazifa qanchalik aniq bo‘lsa, mos javoblar shuncha ko‘p bo‘ladi."),
        Tip(21, TipAudience.Business, "/requests", OpenRequests,
            "Если цены не было в предложении, впишите её в сделке. Так у вас останется история: с кем работали и сколько потратили.",
            "Narx taklifda bo‘lmagan bo‘lsa, uni bitimga yozing. Shunda kim bilan ishlaganingiz va qancha sarflaganingiz tarixi saqlanadi."),
        Tip(22, TipAudience.Creator, "/requests", OpenRequests,
            "Бизнес может сам прислать вам предложение. Оно придёт в этот чат и во вкладку «Заявки». Ответьте в течение 48 часов, иначе оно истечёт.",
            "Biznes sizga o‘zi taklif yuborishi mumkin. U shu chatga va «Arizalar» bo‘limiga keladi. 48 soat ichida javob bering, aks holda muddati tugaydi."),
        Tip(23, TipAudience.Creator, "/requests", OpenRequests,
            "Когда работа готова, откройте сделку и нажмите «Завершить сделку». Там же можно сразу добавить ссылку на публикацию.",
            "Ish tayyor bo‘lgach, bitimni oching va «Bitimni yakunlash»ni bosing. U yerda darhol e’lon havolasini ham qo‘shish mumkin."),
        Tip(24, TipAudience.Blogger, "/blogger-form", OpenProfile,
            "Укажите в анкете статистику по каждой площадке: подписчиков и охваты. Бизнесу проще выбрать, когда видны цифры.",
            "Anketada har bir platforma bo‘yicha statistikani ko‘rsating: obunachilar va qamrov. Raqamlar ko‘rinsa, biznesga tanlash osonroq."),
        Tip(25, TipAudience.Business, "/business", OpenProfile,
            "Добавьте логотип и описание компании. Авторы охотнее откликаются, когда понимают, с кем будут работать.",
            "Kompaniya logotipi va tavsifini qo‘shing. Mualliflar kim bilan ishlashini tushunsa, ko‘proq javob yuborishadi."),
        Tip(26, TipAudience.Blogger, "/blogger-form", OpenProfile,
            "Добавьте в анкету ссылку на свою лучшую рекламу в разделе «Моя работа». Пример работы убеждает лучше любых слов.",
            "Anketaning «Mening ishim» bo‘limiga eng yaxshi reklamangiz havolasini qo‘shing. Ish namunasi har qanday so‘zdan yaxshiroq ishontiradi."),
        Tip(27, TipAudience.Business, "/my-campaigns", MyCampaigns,
            "Откройте свою кампанию, чтобы увидеть все отклики. Примите подходящий, и сделка с контактами автора создастся сама.",
            "Barcha javoblarni ko‘rish uchun kampaniyangizni oching. Mosini qabul qiling, muallif kontaktlari bilan bitim o‘zi yaratiladi."),
        Tip(28, TipAudience.All, "/requests", OpenRequests,
            "Во вкладке «Заявки» есть фильтр по дате: сегодня, неделя, месяц или свои даты. Удобно, когда заявок много.",
            "«Arizalar» bo‘limida sana bo‘yicha filtr bor: bugun, hafta, oy yoki o‘z sanalaringiz. Arizalar ko‘p bo‘lganda qulay."),
        Tip(29, TipAudience.All, "/requests", OpenRequests,
            "Контакты партнёра открываются в сделке, как только отклик или предложение приняты. Детали можно обсудить напрямую.",
            "Hamkor kontaktlari javob yoki taklif qabul qilingan zahoti bitimda ochiladi. Tafsilotlarni to‘g‘ridan-to‘g‘ri kelishib olish mumkin."),
        Tip(30, TipAudience.Creator, "/campaigns", OpenCampaigns,
            "Откликайтесь на кампании, которые подходят вашей аудитории. Так выше шанс, что вас выберут, и лучше отзывы после сделки.",
            "Auditoriyangizga mos kampaniyalarga javob yuboring. Shunda tanlanish ehtimoli yuqori, bitimdan keyingi fikrlar esa yaxshiroq bo‘ladi.")
    ];

    // The next tip for this role after the last one sent; after the last it starts over, so nothing repeats until all were sent.
    public static BotTip? Next(MarketplaceRole? role, int? lastNumber)
    {
        var suitable = All.Where(tip => tip.IsFor(role)).OrderBy(tip => tip.Number).ToArray();
        if (suitable.Length == 0)
        {
            return null;
        }

        return suitable.FirstOrDefault(tip => tip.Number > (lastNumber ?? 0)) ?? suitable[0];
    }

    // Every tip says how to switch them off.
    internal static BotText Message(BotTip tip) => new BotText(
        $"{tip.Text.Russian}\n\nОтключить советы: Профиль → Настройки.",
        $"{tip.Text.Uzbek}\n\nMaslahatlarni o‘chirish: Profil → Sozlamalar.") with { Button = tip.Button };
}

public static class BotTipSchedule
{
    // Tashkent has no daylight saving time.
    private static readonly TimeSpan TashkentOffset = TimeSpan.FromHours(5);
    private const int FirstHour = 11;
    private const int LastHour = 20;
    private static readonly DayOfWeek[] Days = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];

    public static bool IsTipTime(DateTime nowUtc)
    {
        var local = nowUtc + TashkentOffset;
        return Days.Contains(local.DayOfWeek) && local.Hour >= FirstHour && local.Hour < LastHour;
    }

    // Start of the current Tashkent day, in UTC: one tip per user per day at most.
    public static DateTime DayStartUtc(DateTime nowUtc) => (nowUtc + TashkentOffset).Date - TashkentOffset;
}

public sealed record BotTipRecipient(long TelegramUserId, MarketplaceRole? Role, int? LastTipNumber);

public sealed record ProcessBotTipsCommand(DateTime NowUtc) : IRequest<int>;

public sealed class ProcessBotTipsHandler(
    IBotTipRepository recipients,
    ITelegramBotClient? botClient = null,
    ILogger<ProcessBotTipsHandler>? logger = null,
    TimeSpan? pauseBetweenMessages = null) : IRequestHandler<ProcessBotTipsCommand, int>
{
    private const int BatchSize = 200;
    // Telegram allows about 30 messages a second; 25 a second leaves room for deal notifications.
    private readonly TimeSpan pause = pauseBetweenMessages ?? TimeSpan.FromMilliseconds(40);

    public async Task<int> Handle(ProcessBotTipsCommand command, CancellationToken cancellationToken)
    {
        if (botClient is null || !BotTipSchedule.IsTipTime(command.NowUtc))
        {
            return 0;
        }

        var dayStartUtc = BotTipSchedule.DayStartUtc(command.NowUtc);
        var sent = 0;
        while (true)
        {
            var batch = await recipients.GetDueAsync(dayStartUtc, BatchSize, cancellationToken);
            if (batch.Count == 0)
            {
                return sent;
            }

            foreach (var recipient in batch)
            {
                var tip = BotTipCatalog.Next(recipient.Role, recipient.LastTipNumber);
                // The claim marks the day first, so another instance or a later run never sends a second tip today.
                if (!await recipients.TryClaimAsync(recipient.TelegramUserId, tip?.Number ?? recipient.LastTipNumber, dayStartUtc, command.NowUtc, cancellationToken) || tip is null)
                {
                    continue;
                }

                await BestEffortTelegramNotification.SendAsync(botClient, logger, recipient.TelegramUserId, BotTipCatalog.Message(tip), tip.Route, cancellationToken);
                sent++;
                if (pause > TimeSpan.Zero)
                {
                    await Task.Delay(pause, cancellationToken);
                }
            }
        }
    }
}

// Admin preview: the owner or an admin gets the next tip for their own role at once, to check texts and buttons
// without waiting for a tip day. Each press shows the next one; other users' rotation is not touched.
public sealed record SendBotTipPreviewCommand(long ActorTelegramUserId) : IRequest<BotTipPreviewDto>;

public sealed record BotTipPreviewDto(int Number, int Total, bool Sent);

public sealed class SendBotTipPreviewHandler(
    IPlatformUserRepository users,
    IAdminAccessPolicy access,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<SendBotTipPreviewHandler>? logger = null) : IRequestHandler<SendBotTipPreviewCommand, BotTipPreviewDto>
{
    public async Task<BotTipPreviewDto> Handle(SendBotTipPreviewCommand command, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(command.ActorTelegramUserId);
        var actor = await users.GetByTelegramUserIdAsync(command.ActorTelegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("An active account is required.");
        var tip = BotTipCatalog.Next(actor.SelectedMarketplaceRole, actor.LastTipNumber)
            ?? throw new InvalidOperationException("There is no tip for this role.");
        var total = BotTipCatalog.All.Count(item => item.IsFor(actor.SelectedMarketplaceRole));

        actor.SetLastTipNumber(tip.Number);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (botClient is null)
        {
            return new BotTipPreviewDto(tip.Number, total, false);
        }

        await BestEffortTelegramNotification.SendAsync(botClient, logger, actor.TelegramUserId, BotTipCatalog.Message(tip), tip.Route, cancellationToken);
        return new BotTipPreviewDto(tip.Number, total, true);
    }
}
