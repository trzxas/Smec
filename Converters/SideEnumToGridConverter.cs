using Smec.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Data;
using FlowDirection = System.Windows.FlowDirection;

namespace Smec.Converters
{
    public class SideEnumToGridConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SidePosition side)
            {
                string param = parameter?.ToString()?.ToLower() ?? "";

                switch (param)
                {
                    case "row":
                        return GetRowForSide(side); // 0, 1 или 2

                    case "column":
                        return GetColumnForSide(side); // 0, 1 или 2

                    case "horizontalalignment":
                        // Левые порты прижимаем влево, правые — вправо, верх/низ центр
                        switch (side)
                        {
                            case SidePosition.Left:
                            case SidePosition.LeftTopCorner:
                            case SidePosition.LeftBottomCorner:
                                //case SidePosition.LeftTop:
                                //case SidePosition.TopLeft:
                                //case SidePosition.LeftBottom:
                                //case SidePosition.BottomLeft:
                                return System.Windows.HorizontalAlignment.Left;
                            case SidePosition.Right:
                            case SidePosition.RightTopCorner:
                            case SidePosition.RightBottomCorner:
                                //case SidePosition.RightTop:
                                //case SidePosition.TopRight:
                                //case SidePosition.RightBottom:
                                //case SidePosition.BottomRight:
                                return System.Windows.HorizontalAlignment.Right;
                            default:
                                return System.Windows.HorizontalAlignment.Center;
                        }

                    case "verticalalignment":
                        // Верхние порты — вверх, нижние — вниз, боковые — по центру ячейки
                        switch (side)
                        {
                            case SidePosition.Top:
                            case SidePosition.LeftTopCorner:
                            case SidePosition.RightTopCorner:
                                //case SidePosition.TopLeft:
                                //case SidePosition.TopRight:
                                //case SidePosition.LeftTop:
                                //case SidePosition.RightTop:
                                return VerticalAlignment.Top;
                            case SidePosition.Bottom:
                            case SidePosition.LeftBottomCorner:
                            case SidePosition.RightBottomCorner:
                                //case SidePosition.BottomRight:
                                //case SidePosition.BottomLeft:
                                //case SidePosition.LeftBottom:
                                //case SidePosition.RightBottom:
                                return VerticalAlignment.Bottom;
                            default:
                                return VerticalAlignment.Center;
                        }

                    case "orientation":
                        {
                            switch (side)
                            {
                                // Если сторона верхняя или нижняя — порты выстраиваются горизонтально в ряд
                                case SidePosition.Top:
                                case SidePosition.Bottom:
                                    //case SidePosition.TopLeft:
                                    //case SidePosition.TopRight:
                                    //case SidePosition.BottomLeft:
                                    //case SidePosition.BottomRight:
                                    return System.Windows.Controls.Orientation.Horizontal;
                                default:
                                    return System.Windows.Controls.Orientation.Vertical;
                            }

                        }
                        //case "flowdirection":
                        //    {
                        //        switch (side)
                        //        {
                        //            case SidePosition.Right:
                        //                return FlowDirection.RightToLeft;

                        //            default:
                        //                return FlowDirection.LeftToRight;
                        //        }
                        //    }
                }
            }
            return DependencyProperty.UnsetValue;
        }



        private int GetRowForSide(SidePosition side) => side switch
        {
            SidePosition.Top or SidePosition.LeftTopCorner or SidePosition.RightTopCorner
            /*or SidePosition.LeftTop or SidePosition.RightTop or SidePosition.TopLeft or SidePosition.TopRight*/  => 0,
            SidePosition.Bottom or SidePosition.LeftBottomCorner or SidePosition.RightBottomCorner
            /*or SidePosition.LeftBottom or SidePosition.RightBottom or SidePosition.BottomLeft or SidePosition.BottomRight*/=> 2,
            _ => 1
        };

        private int GetColumnForSide(SidePosition side) => side switch
        {
            SidePosition.Left or  SidePosition.LeftTopCorner or SidePosition.LeftBottomCorner
           /* or SidePosition.LeftTop or SidePosition.LeftBottom or SidePosition.TopLeft or SidePosition.BottomLeft */ => 0,
            SidePosition.Right or SidePosition.RightTopCorner or SidePosition.RightBottomCorner
            /*or SidePosition.RightTop or SidePosition.RightBottom or SidePosition.TopRight or SidePosition.BottomRight */=> 2,
            _ => 1
        };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //throw new NotImplementedException();
            return DependencyProperty.UnsetValue;
        }
    }

}

