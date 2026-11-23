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

        public static string DisplayColor(string? hex)
        {
            if (!NoteStore.Settings.ColorBlindMode || string.IsNullOrWhiteSpace(hex))
                return hex ?? "#22C55E";

            return hex.ToUpperInvariant() switch
            {
                "#22C55E" => "#009E73",
                "#3B82F6" => "#0072B2",
                "#F59E0B" => "#E69F00",
                "#EF4444" => "#D55E00",
                "#A855F7" => "#CC79A7",
                "#EC4899" => "#56B4E9",
                "#14B8A6" => "#F0E442",
                "#9CA3AF" => "#999999",
                _ => hex
            };
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

            string[] palette = NoteStore.Settings.ColorBlindMode
                ? NoteStore.AccessiblePalette
                : NoteStore.Palette;
            string displayedSelected = DisplayColor(selected);

            foreach (string hex in palette)
            {
                bool isSelected = string.Equals(hex, displayedSelected, StringComparison.OrdinalIgnoreCase);

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
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = ColorName(hex)
                };
                System.Windows.Automation.AutomationProperties.SetName(dot, ColorName(hex));

                string picked = hex;
                dot.MouseLeftButtonUp += (s, e) =>
                {
                    string current = onPick(picked);
                    BuildPalette(host, current, onPick);
                };

                host.Children.Add(dot);
            }
        }

        private static string ColorName(string hex)
        {
            return hex.ToUpperInvariant() switch
            {
                "#E69F00" => "Naranja",
                "#56B4E9" => "Celeste",
                "#009E73" => "Verde azulado",
                "#F0E442" => "Amarillo",
                "#0072B2" => "Azul",
                "#D55E00" => "Bermellón",
                "#CC79A7" => "Malva",
                "#999999" => "Gris",
                "#22C55E" => "Verde",
                "#3B82F6" => "Azul",
                "#F59E0B" => "Ámbar",
                "#EF4444" => "Rojo",
                "#A855F7" => "Morado",
                "#EC4899" => "Rosa",
                "#14B8A6" => "Turquesa",
                "#9CA3AF" => "Gris",
                _ => hex
            };
        }
    }
}
