namespace BloggerBazar.Application.Notifications;

// The backend does not store the user's interface language, so every bot message carries Russian and Uzbek.
internal static class BotMessages
{
    private static string Bilingual(string russian, string uzbek) => $"{russian}\n\n{uzbek}";

    public static string NewCampaignApplication(string bloggerName, string campaignTitle) => Bilingual(
        $"Новый отклик от {bloggerName} на кампанию «{campaignTitle}».",
        $"{bloggerName} «{campaignTitle}» kampaniyasiga javob yubordi.");

    public static string CampaignApplicationRejected(string campaignTitle) => Bilingual(
        $"Ваш отклик на кампанию «{campaignTitle}» отклонён.",
        $"«{campaignTitle}» kampaniyasiga javobingiz rad etildi.");

    public static string CampaignApplicationAccepted(string campaignTitle) => Bilingual(
        $"Ваш отклик на кампанию «{campaignTitle}» принят. Сделка создана — контакты партнёра открыты в сделке.",
        $"«{campaignTitle}» kampaniyasiga javobingiz qabul qilindi. Bitim yaratildi — hamkor kontaktlari bitim sahifasida.");

    public static string OfferReceived(string businessName) => Bilingual(
        $"{businessName} предлагает вам сотрудничество. Ответьте в течение 48 часов.",
        $"{businessName} sizga hamkorlik taklif qilmoqda. 48 soat ichida javob bering.");

    public static string OfferAccepted(string bloggerName) => Bilingual(
        $"{bloggerName} принял(а) ваше предложение. Сделка создана.",
        $"{bloggerName} taklifingizni qabul qildi. Bitim yaratildi.");

    public static string OfferDeclined(string bloggerName) => Bilingual(
        $"{bloggerName} отклонил(а) ваше предложение.",
        $"{bloggerName} taklifingizni rad etdi.");

    public static string CollaborationRequestReceived(string businessName) => Bilingual(
        $"{businessName} отправил(а) вам предложение о сотрудничестве.",
        $"{businessName} sizga hamkorlik taklifini yubordi.");

    public static string DealCompleted => Bilingual(
        "Сделка завершена. Теперь можно оставить отзыв.",
        "Bitim yakunlandi. Endi fikr qoldirishingiz mumkin.");

    public static string PartnerReviewed => Bilingual(
        "Партнёр оставил отзыв о сотрудничестве. Оцените и вы — отзывы откроются, когда оба оценят друг друга.",
        "Hamkor hamkorlik haqida fikr qoldirdi. Siz ham baholang — fikrlar ikkalangiz baholaganingizdan keyin ochiladi.");

    public static string ReviewsPublished => Bilingual(
        "Партнёр тоже оставил отзыв — оба отзыва опубликованы.",
        "Hamkor ham fikr qoldirdi — ikkala fikr e’lon qilindi.");

    public static string ReviewReminderLastWeek => Bilingual(
        "Осталась неделя, чтобы оценить завершённую сделку. Отзывы публикуются, когда обе стороны оценят друг друга.",
        "Yakunlangan bitimni baholash uchun bir hafta qoldi. Fikrlar ikki tomon ham baholaganidan keyin e’lon qilinadi.");

    public static string ReviewReminder => Bilingual(
        "Оцените завершённую сделку. Отзывы публикуются, когда обе стороны оценят друг друга.",
        "Yakunlangan bitimni baholang. Fikrlar ikki tomon ham baholaganidan keyin e’lon qilinadi.");

    public static string CompletionReminder => Bilingual(
        "Сделка всё ещё активна. Если сотрудничество завершено, отметьте это в приложении.",
        "Bitim hali faol. Hamkorlik yakunlangan bo‘lsa, buni ilovada belgilang.");

    public static string ContactUnlockPaid => Bilingual(
        "Оплата подтверждена, контакты разблокированы.",
        "To‘lov tasdiqlandi, kontaktlar ochildi.");

    public static string BloggerProfileApproved => Bilingual(
        "🎉 Ваш профиль прошёл проверку и доступен пользователям BloggerBazar.",
        "🎉 Profilingiz tekshiruvdan o‘tdi va BloggerBazar foydalanuvchilariga ko‘rinadi.");

    public static string BloggerProfileNeedsChanges => Bilingual(
        "Нужно исправить несколько пунктов профиля. После сохранения отправьте профиль ещё раз.",
        "Profildagi bir nechta bandni tuzatish kerak. Saqlagandan so‘ng profilni qayta yuboring.");

    public static string BloggerProfileRejected => Bilingual(
        "Профиль не прошёл проверку. Исправьте замечания и отправьте снова.",
        "Profil tekshiruvdan o‘tmadi. Kamchiliklarni tuzatib, qayta yuboring.");

    public static string BusinessProfileApproved => Bilingual(
        "🎉 Профиль компании одобрен и доступен в BloggerBazar.",
        "🎉 Kompaniya profili tasdiqlandi va BloggerBazar’da ko‘rinadi.");

    public static string BusinessProfileNeedsChanges => Bilingual(
        "Нужно исправить несколько пунктов профиля компании.",
        "Kompaniya profilidagi bir nechta bandni tuzatish kerak.");

    public static string BusinessProfileRejected => Bilingual(
        "Профиль компании не прошёл проверку.",
        "Kompaniya profili tekshiruvdan o‘tmadi.");
}
