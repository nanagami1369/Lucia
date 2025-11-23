namespace Lucia.Models.Exceptions;

/// <summary>
/// セッション例外
/// </summary>
public class SessionException : UserBaseException {

    public SessionException(string message) : base(message) { }

}
