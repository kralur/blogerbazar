using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Notifications;

// Every bot message in Russian and Uzbek; the bot client picks the recipient's language (D39).
internal static class BotMessages
{
    private static BotText Bilingual(string russian, string uzbek) => new(russian, uzbek);

    public static BotText NewCampaignApplication(string bloggerName, string campaignTitle) => Bilingual(
        $"Новый отклик от {bloggerName} на кампанию «{campaignTitle}».",
        $"{bloggerName} «{campaignTitle}» kampaniyasiga javob yubordi.");

    public static BotText CampaignApplicationRejected(string campaignTitle) => Bilingual(
        $"Ваш отклик на кампанию «{campaignTitle}» отклонён.",
        $"«{campaignTitle}» kampaniyasiga javobingiz rad etildi.");

    public static BotText CampaignApplicationAccepted(string campaignTitle) => Bilingual(
        $"Ваш отклик на кампанию «{campaignTitle}» принят. Сделка создана, контакты партнёра открыты в сделке.",
        $"«{campaignTitle}» kampaniyasiga javobingiz qabul qilindi. Bitim yaratildi, hamkor kontaktlari bitim sahifasida.");

    public static BotText OfferReceived(string businessName) => Bilingual(
        $"{businessName} предлагает вам сотрудничество. Ответьте в течение 48 часов.",
        $"{businessName} sizga hamkorlik taklif qilmoqda. 48 soat ichida javob bering.");

    public static BotText OfferAccepted(string bloggerName) => Bilingual(
        $"{bloggerName} принял(а) ваше предложение. Сделка создана.",
        $"{bloggerName} taklifingizni qabul qildi. Bitim yaratildi.");

    public static BotText OfferDeclined(string bloggerName) => Bilingual(
        $"{bloggerName} отклонил(а) ваше предложение.",
        $"{bloggerName} taklifingizni rad etdi.");

    public static BotText CollaborationRequestReceived(string businessName) => Bilingual(
        $"{businessName} отправил(а) вам предложение о сотрудничестве.",
        $"{businessName} sizga hamkorlik taklifini yubordi.");

    // A deal is named after its campaign; an offer deal has no campaign, so its format names it.
    private static string? TopicRussian(DealTopic topic) => topic.CampaignTitle is { Length: > 0 } title ? $"«{title}»" : topic.Format switch
    {
        CollaborationFormat.Stories => "Stories",
        CollaborationFormat.Reels => "Reels",
        CollaborationFormat.Post => "«Пост»",
        CollaborationFormat.Integration => "«Интеграция»",
        _ => null
    };

    private static string? TopicUzbek(DealTopic topic) => topic.CampaignTitle is { Length: > 0 } title ? $"«{title}»" : topic.Format switch
    {
        CollaborationFormat.Stories => "Stories",
        CollaborationFormat.Reels => "Reels",
        CollaborationFormat.Post => "«Post»",
        CollaborationFormat.Integration => "«Integratsiya»",
        _ => null
    };

    private static string Spaced(string? value) => value is null ? "" : $" {value}";
    private static string WithPartnerRussian(string? partner) => string.IsNullOrWhiteSpace(partner) ? "" : $" с партнёром {partner}";
    private static string WithPartnerUzbek(string? partner) => string.IsNullOrWhiteSpace(partner) ? "" : $"{partner} bilan ";

    // Uzbek puts the deal name before "bitim" and adds the possessive suffix: "«X» bitimi", or plain "bitim".
    private static string DealUzbek(DealTopic topic, string suffix, string plainSuffix) =>
        TopicUzbek(topic) is { } name ? $"{name} bitim{suffix}" : $"bitim{plainSuffix}";

    public static BotText DealCompleted(string partner, DealTopic topic) => Bilingual(
        $"{partner} отметил(а) сделку{Spaced(TopicRussian(topic))} завершённой. Оставьте отзыв о сотрудничестве.",
        $"{partner} {DealUzbek(topic, "ini", "ni")} yakunlangan deb belgiladi. Hamkorlik haqida fikr qoldiring.");

    public static BotText PartnerReviewed(string partner, DealTopic topic) => Bilingual(
        $"{partner} оставил(а) отзыв о сделке{Spaced(TopicRussian(topic))}. Оставьте и свой отзыв: вы увидите отзывы друг друга, когда оба оцените сделку.",
        $"{partner} {DealUzbek(topic, "i", "")} haqida fikr qoldirdi. Siz ham fikr qoldiring: ikkalangiz baholaganingizda bir-biringizning fikrlarini ko‘rasiz.");

    public static BotText ReviewsPublished(string partner, DealTopic topic) => Bilingual(
        $"{partner} тоже оставил(а) отзыв о сделке{Spaced(TopicRussian(topic))}. Оба отзыва опубликованы.",
        $"{partner} ham {DealUzbek(topic, "i", "")} haqida fikr qoldirdi. Ikkala fikr e’lon qilindi.");

    public static BotText ReviewReminderLastWeek(string? partner, DealTopic topic) => Bilingual(
        $"Осталась неделя, чтобы оценить сделку{Spaced(TopicRussian(topic))}{WithPartnerRussian(partner)}. Отзывы публикуются, когда обе стороны оценят друг друга.",
        $"{WithPartnerUzbek(partner)}{DealUzbek(topic, "ini", "ni")} baholash uchun bir hafta qoldi. Fikrlar ikki tomon ham baholaganidan keyin e’lon qilinadi.");

    public static BotText ReviewReminder(string? partner, DealTopic topic) => Bilingual(
        $"Оцените сделку{Spaced(TopicRussian(topic))}{WithPartnerRussian(partner)}. Отзывы публикуются, когда обе стороны оценят друг друга.",
        $"{WithPartnerUzbek(partner)}{DealUzbek(topic, "ini", "ni")} baholang. Fikrlar ikki tomon ham baholaganidan keyin e’lon qilinadi.");

    public static BotText CompletionReminder(string? partner, DealTopic topic) => Bilingual(
        $"Сделка{Spaced(TopicRussian(topic))}{WithPartnerRussian(partner)} всё ещё активна. Если сотрудничество завершено, отметьте это в приложении.",
        $"{WithPartnerUzbek(partner)}{DealUzbek(topic, "i", "")} hali faol. Hamkorlik yakunlangan bo‘lsa, buni ilovada belgilang.");

    public static BotText ContactUnlockPaid => Bilingual(
        "Оплата подтверждена, контакты разблокированы.",
        "To‘lov tasdiqlandi, kontaktlar ochildi.");

    public static BotText BloggerProfileApproved => Bilingual(
        "🎉 Ваш профиль прошёл проверку и доступен пользователям BloggerBazar.",
        "🎉 Profilingiz tekshiruvdan o‘tdi va BloggerBazar foydalanuvchilariga ko‘rinadi.");

    public static BotText BloggerProfileNeedsChanges => Bilingual(
        "Нужно исправить несколько пунктов профиля. После сохранения отправьте профиль ещё раз.",
        "Profildagi bir nechta bandni tuzatish kerak. Saqlagandan so‘ng profilni qayta yuboring.");

    public static BotText BloggerProfileRejected => Bilingual(
        "Профиль не прошёл проверку. Исправьте замечания и отправьте снова.",
        "Profil tekshiruvdan o‘tmadi. Kamchiliklarni tuzatib, qayta yuboring.");

    public static BotText BusinessProfileApproved => Bilingual(
        "🎉 Профиль компании одобрен и доступен в BloggerBazar.",
        "🎉 Kompaniya profili tasdiqlandi va BloggerBazar’da ko‘rinadi.");

    public static BotText BusinessProfileNeedsChanges => Bilingual(
        "Нужно исправить несколько пунктов профиля компании.",
        "Kompaniya profilidagi bir nechta bandni tuzatish kerak.");

    public static BotText BusinessProfileRejected => Bilingual(
        "Профиль компании не прошёл проверку.",
        "Kompaniya profili tekshiruvdan o‘tmadi.");
}
