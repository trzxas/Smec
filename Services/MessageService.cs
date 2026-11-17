using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Smec.Services
{
    

    public class MessageService : IMessageService
    {
        public void ShowError(string message, string title = "Ошибка")
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ShowInfo(string message, string title = "Информация")
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public MessageResult ShowYesNo(string message, string title = "Подтверждение")
        {
            MessageBoxResult result = System.Windows.MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            return result switch
            {
                MessageBoxResult.Yes => MessageResult.Yes,
                MessageBoxResult.No => MessageResult.No,
                _ => MessageResult.Cancel
            };
        }

        // Для разветвленной логики сохранения перед закрытием/сбросом
        public MessageResult ShowYesNoCancel(string message, string title = "Предупреждение")
        {
            MessageBoxResult result = System.Windows.MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning
            );

            return result switch
            {
                MessageBoxResult.Yes => MessageResult.Yes,
                MessageBoxResult.No => MessageResult.No,
                _ => MessageResult.Cancel
            };
        }
    }

}
