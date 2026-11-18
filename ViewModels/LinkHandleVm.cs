using Smec.Models;

namespace Smec.ViewModels
{
    public class LinkHandleVm(int index, double x, double y, bool isHorizontal) : ObservableObject
    {
       
        public int Index { get; } = index;

        public double X
        {
            get;
            set => SetProperty(ref field, value);
        } = x;

        public double Y
        {
            get;
            set => SetProperty(ref field, value);
        } = y;

        public bool IsHorizontal
        {
            get;
            set => SetProperty(ref field, value);
        } = isHorizontal;

    }
}
