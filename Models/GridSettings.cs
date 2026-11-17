using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Smec.Models
{
    public class GridSettings : ObservableObject
    {
        private double _gridSize = 12.0;

        /// <summary>
        /// Размер 1.0 условной единицы сетки в пикселях (по умолчанию 4 px = 100 mil).
        /// </summary>
        public double GridSize
        {
            get => _gridSize;
            set
            {
                if (value > 0 && Math.Abs(_gridSize - value) > 0.001)
                {
                    _gridSize = value;
                    OnPropertyChanged();
                }
            }
        }

    }

}
