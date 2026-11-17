using Smec.Models;

namespace Smec.ViewModels
{
    public class JunctionVm(double x, double y, PortState state) : ObservableObject
    {
        public double X { get; } = x;
        public double Y { get; } = y;

        
        public PortState State
        {
            get;
            set => SetProperty(ref field, value);
        } = state;
    }
}
