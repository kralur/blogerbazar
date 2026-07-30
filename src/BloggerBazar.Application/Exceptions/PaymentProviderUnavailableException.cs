namespace BloggerBazar.Application.Exceptions;

public sealed class PaymentProviderUnavailableException(string message) : Exception(message);
