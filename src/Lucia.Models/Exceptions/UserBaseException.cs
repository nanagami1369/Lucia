namespace Lucia.Models.Exceptions;

/// <summary>
/// ユーザーに送る例外
/// </summary>
public abstract class UserBaseException : Exception {

    protected UserBaseException(string message) : base(message) { }

}
