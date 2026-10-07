using BloggerBazar.Application.Abstractions.Telegram;

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
        $"Ваш отклик на кампанию «{campaignTitle}» принят. Сделка создана — контакты партнёра открыты в сделке.",
        $"«{campaignTitle}» kampaniyasiga javobingiz qabul qilindi. Bitim yaratildi — hamkor kontaktlari bitim sahifasida.");

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

    public static BotText DealCompleted => Bilingual(
        "Сделка завершена. Теперь можно оставить отзыв.",
        "Bitim yakunlandi. Endi fikr qoldirishingiz mumkin.");

    public static BotText PartnerReviewed => Bilingual(
        "Партнёр оставил отзыв о сотрудничестве. Оцените и вы — отзывы откроются, когда оба оценят друг друга.",
        "Hamkor hamkorlik haqida fikr qoldirdi. Siz ham baholang — fikrlar ikkalangiz baholaganingizdan keyin ochiladi.");

    public static BotText ReviewsPublished => Bilingual(
        "Партнёр тоже оставил отзыв — оба отзыва опубликованы.",
        "Hamkor ham fikr qoldirdi — ikkala fikr e’lon qilindi.");

    public static BotText ReviewReminderLastWeek => Bilingual(
        "Осталась неделя, чтобы оценить завершённую сделку. Отзывы публикуются, когда обе стороны оценят друг друга.",
        "Yakunlangan bitimni baholash uchun bir hafta qoldi. Fikrlar ikki tomon ham baholaganidan keyin e’lon qilinadi.");

    public static BotText ReviewReminder => Bilingual(
        "Оцените завершённую сделку. Отзывы публикуются, когда обе стороны оценят друг друга.",
        "Yakunlangan bitimni baholang. Fikrlar ikki tomon ham baholaganidan keyin e’lon qilinadi.");

    public static BotText CompletionReminder => Bilingual(
        "Сделка всё ещё активна. Если сотрудничество завершено, отметьте это в приложении.",
        "Bitim hali faol. Hamkorlik yakunlangan bo‘lsa, buni ilovada belgilang.");

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
