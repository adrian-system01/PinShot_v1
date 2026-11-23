using System;
using System.IO;
using Microsoft.Win32;

namespace PinShot
{
    internal static class WindowsStartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "PinShot";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(RunValueName) != null;
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo consultar el inicio con Windows: " + ex.Message);
                return false;
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null)
                {
                    AppLog.Write("No se pudo abrir la clave de inicio de Windows.");
                    return false;
                }

                if (enabled)
                {
                    string? exe = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exe))
                    {
                        AppLog.Write("No se pudo determinar la ruta del ejecutable para el inicio.");
                        return false;
                    }

                    key.SetValue(RunValueName, "\"" + exe + "\"");
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }

                return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("Error al cambiar inicio con Windows: " + ex.Message);
                return false;
            }
        }
    }
}
