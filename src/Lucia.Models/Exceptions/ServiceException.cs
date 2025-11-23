using System;
using System.Collections.Generic;
using System.Text;

namespace Lucia.Models.Exceptions;

/// <summary>
/// サービス例外
/// </summary>
public class ServiceException : UserBaseException {

    public ServiceException(string message) : base(message) {
    
    }

}
