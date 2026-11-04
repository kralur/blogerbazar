namespace BloggerBazar.Domain.Enums;

public enum DealReminderKind
{
    ReviewDay1 = 0,
    ReviewDay3 = 1,
    ReviewDay7 = 2,
    CompleteDay7 = 3,
    CompleteDay14 = 4,
    ResultsDay2 = 5,
    PriceDay2 = 6,
    // Not a reminder: the business's "a link was added" message, claimed once per deal so add/delete loops cannot spam.
    PublicationNotice = 7
}
