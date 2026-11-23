using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace PinShot
{
    internal sealed class TrayIconController : IDisposable
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _activeItem;
        private readonly ToolStripMenuItem _ignoreTextItem;
        private static Icon? _logoIcon;

        public TrayIconController(
            string version,
            Action openNewNote,
            Action openLibrary,
            Action openSettings,
            Action closeAllCaptures,
            Action exit,
            Func<bool> isEnabled,
            Action<bool> setEnabled,
            Func<bool> ignoresTextCopies,
            Action<bool> setIgnoreTextCopies)
        {
            _icon = new NotifyIcon
            {
                Icon = GetTrayIcon(true),
                Text = "PinShot - Activo",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("PinShot v" + version) { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());

            var newNote = new ToolStripMenuItem("Nueva nota");
            newNote.Click += (s, e) => openNewNote();
            menu.Items.Add(newNote);

            var noteList = new ToolStripMenuItem("Lista de notas");
            noteList.Click += (s, e) => openLibrary();
            menu.Items.Add(noteList);

            var settings = new ToolStripMenuItem("Configuración");
            settings.Click += (s, e) => openSettings();
            menu.Items.Add(settings);
            menu.Items.Add(new ToolStripSeparator());

            _activeItem = new ToolStripMenuItem("Activo") { Checked = isEnabled() };
            _activeItem.Click += (s, e) => setEnabled(!isEnabled());
            menu.Items.Add(_activeItem);

            var closeAll = new ToolStripMenuItem("Cerrar todas las capturas");
            closeAll.Click += (s, e) => closeAllCaptures();
            menu.Items.Add(closeAll);
            menu.Items.Add(new ToolStripSeparator());

            _ignoreTextItem = new ToolStripMenuItem("Ignorar copias con texto (Word/Excel)")
            {
                Checked = ignoresTextCopies()
            };
            _ignoreTextItem.Click += (s, e) =>
            {
                bool ignore = !ignoresTextCopies();
                setIgnoreTextCopies(ignore);
                _ignoreTextItem.Checked = ignore;
            };
            menu.Items.Add(_ignoreTextItem);

            var startupItem = new ToolStripMenuItem("Iniciar con Windows")
            {
                Checked = WindowsStartupService.IsEnabled()
            };
            startupItem.Click += (s, e) =>
            {
                bool enabled = !WindowsStartupService.IsEnabled();
                if (WindowsStartupService.SetEnabled(enabled))
                    startupItem.Checked = enabled;
                else
                    ShowBalloon("PinShot", "No se pudo cambiar el inicio con Windows.", ToolTipIcon.Error);
            };
            menu.Items.Add(startupItem);

            var openLog = new ToolStripMenuItem("Abrir registro (log)");
            openLog.Click += (s, e) =>
            {
                try
                {
                    if (!File.Exists(AppLog.Path)) AppLog.Write("(registro creado)");
                    Process.Start(new ProcessStartInfo(AppLog.Path) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    AppLog.Write("No se pudo abrir el registro: " + ex.Message);
                    ShowBalloon("PinShot", "No se pudo abrir el registro.", ToolTipIcon.Error);
                }
            };
            menu.Items.Add(openLog);
            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Salir");
            exitItem.Click += (s, e) => exit();
            menu.Items.Add(exitItem);

            _icon.ContextMenuStrip = menu;
            _icon.DoubleClick += (s, e) => openLibrary();
        }

        public void SetEnabled(bool enabled)
        {
            _activeItem.Checked = enabled;
            var oldIcon = _icon.Icon;
            _icon.Icon = GetTrayIcon(enabled);
            oldIcon?.Dispose();
            _icon.Text = enabled ? "PinShot - Activo" : "PinShot - Apagado";
        }

        public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _icon.ShowBalloonTip(4000, title, text, icon);
        }

        private static Icon CreatePinIcon(bool enabled)
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);
                var bg = enabled
                    ? System.Drawing.Color.FromArgb(37, 99, 235)
                    : System.Drawing.Color.FromArgb(107, 114, 128);
                using var bgBrush = new SolidBrush(bg);
                g.FillEllipse(bgBrush, 2, 2, 28, 28);
                using var pen = new Pen(System.Drawing.Color.White, 2.5f);
                g.DrawEllipse(pen, 11, 8, 10, 10);
                g.DrawLine(pen, 16, 18, 16, 25);
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var temp = Icon.FromHandle(hIcon);
                return (Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        private static Icon GetLogoIcon()
        {
            if (_logoIcon != null) return _logoIcon;
            try
            {
                var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/PinShot.ico"));
                if (info != null)
                {
                    using var stream = info.Stream;
                    _logoIcon = new Icon(stream);
                    return _logoIcon;
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo cargar PinShot.ico: " + ex.Message);
            }

            return CreatePinIcon(true);
        }

        private static Icon GetTrayIcon(bool enabled)
        {
            if (enabled) return (Icon)GetLogoIcon().Clone();
            try
            {
                using var bmp = GetLogoIcon().ToBitmap();
                using var gray = new Bitmap(bmp.Width, bmp.Height);
                using var g = Graphics.FromImage(gray);
                var matrix = new ColorMatrix(new[]
                {
                    new[] { 0.299f, 0.587f, 0.114f, 0f, 0f },
                    new[] { 0.299f, 0.587f, 0.114f, 0f, 0f },
                    new[] { 0.299f, 0.587f, 0.114f, 0f, 0f },
                    new[] { 0f, 0f, 0f, 0.65f, 0f },
                    new[] { 0f, 0f, 0f, 0f, 1f }
                });
                using var attrs = new ImageAttributes();
                attrs.SetColorMatrix(matrix);
                g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
                    0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attrs);

                IntPtr handle = gray.GetHicon();
                try
                {
                    using var temp = Icon.FromHandle(handle);
                    return (Icon)temp.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo crear el icono inactivo: " + ex.Message);
                return CreatePinIcon(false);
            }
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            _logoIcon?.Dispose();
            _logoIcon = null;
        }
    }
}
