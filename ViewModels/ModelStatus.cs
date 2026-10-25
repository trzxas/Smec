//using CommunityToolkit.Mvvm.Input;

namespace Emulation.ViewModels
{
    [Flags]
    public enum ModelStatus
    {
        UnChanged = 0,
        VisualChanged = 1 << 0,
        LogicalChanged = 1 << 1,

    }

    // Вспомогательный метод для проверки AnyFlag (расширение)
    public static class EnumExtensions
    {
        public static bool HasChanged(this ModelStatus value)
        {
            return (value != 0);
        }
    }
}
