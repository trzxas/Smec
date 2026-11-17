using System.Windows;
using System.Windows.Input;
using UserControl = System.Windows.Controls.UserControl;

namespace Smec.CustomControls
{
    /// <summary>
    /// Interaction logic for ControlPanel.xaml
    /// </summary>
   
        public partial class ControlPanel : UserControl
        {
            public ControlPanel() => InitializeComponent();

            // TimeValue
            public static readonly DependencyProperty TimeValueProperty =
                DependencyProperty.Register(nameof(TimeValue), typeof(TimeSpan), typeof(ControlPanel), new PropertyMetadata(TimeSpan.Zero));

            public TimeSpan TimeValue
            {
                get => (TimeSpan)GetValue(TimeValueProperty);
                set => SetValue(TimeValueProperty, value);
            }

            // Команда Старт
            public static readonly DependencyProperty StartCommandProperty =
                DependencyProperty.Register(nameof(StartCommand), typeof(ICommand), typeof(ControlPanel));

            public ICommand StartCommand
            {
                get => (ICommand)GetValue(StartCommandProperty);
                set => SetValue(StartCommandProperty, value);
            }

            // Команда Стоп
            public static readonly DependencyProperty StopCommandProperty =
                DependencyProperty.Register(nameof(StopCommand), typeof(ICommand), typeof(ControlPanel));

            public ICommand StopCommand
            {
                get => (ICommand)GetValue(StopCommandProperty);
                set => SetValue(StopCommandProperty, value);
            }

            // Команда Пауза
            public static readonly DependencyProperty PauseCommandProperty =
                DependencyProperty.Register(nameof(PauseCommand), typeof(ICommand), typeof(ControlPanel));

            public ICommand PauseCommand
            {
                get => (ICommand)GetValue(PauseCommandProperty);
                set => SetValue(PauseCommandProperty, value);
            }
        }

    }



