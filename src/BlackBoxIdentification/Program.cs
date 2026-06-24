using System;
using System.Windows.Forms;

namespace BlackBoxIdentification;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.ToString(),
                "Ошибка при запуске приложения",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
