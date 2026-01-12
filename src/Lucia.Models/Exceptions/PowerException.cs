namespace Lucia.Models.Exceptions;

/// <summary>
/// 電源管理例外
/// </summary>
public class PowerException : UserBaseException {
    public PowerException(string message) : base(message) {
    }
}
