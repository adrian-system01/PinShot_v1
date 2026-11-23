using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PinShot
{
    public static class UiHelpers
    {
        public static System.Windows.Media.Color ParseColor(string? hex, string fallback = "#22C55E")
        {
            try
            {
                return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex ?? fallback);
            }
            catch
            {
                return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fallback);
            }
        }

        public static SolidColorBrush BrushOf(string? hex)
        {
            return new SolidColorBrush(ParseColor(hex));
        }

        public static string FormatDate(DateTime d)
        {
            if (d.Date == DateTime.Today) return d.ToString("HH:mm");
            try
            {
                var es = CultureInfo.GetCultureInfo("es-PE");
                string s = d.ToString("d 'de' MMM", es);
                if (d.Year != DateTime.Today.Year) s += " " + d.Year;
                return s;
            }
            catch
            {
                return d.ToString("dd/MM/yyyy");
            }
        }

        /// <summary>Dibuja la fila de circulos de colores. onPick se llama al elegir uno.</summary>
        public static void BuildPalette(System.Windows.Controls.Panel host, string selected, Func<string, string> onPick)
        {
            host.Children.Clear();

            foreach (string hex in NoteStore.Palette)
            {
                bool isSelected = string.Equals(hex, selected, StringComparison.OrdinalIgnoreCase);

                var dot = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = BrushOf(hex),
                    BorderThickness = new Thickness(2),
                    BorderBrush = isSelected
                        ? System.Windows.Media.Brushes.White
                        : System.Windows.Media.Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                string picked = hex;
                dot.MouseLeftButtonUp += (s, e) =>
                {
                    string current = onPick(picked);
                    BuildPalette(host, current, onPick);
                };

                host.Children.Add(dot);
            }
        }
    }
}
