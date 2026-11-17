using System;
using System.Collections.Generic;
using System.Text;

namespace Smec.Services
{

    public enum MessageResult
    {
        Yes,
        No,
        Cancel
    }

    public interface IMessageService
    {
        void ShowError(string message, string title = "Ошибка");
        void ShowInfo(string message, string title = "Информация");

        MessageResult ShowYesNo(string message, string title = "Подтверждение");

        MessageResult ShowYesNoCancel(string message, string title = "Предупреждение");
    }
}
