using System.Windows.Forms;

namespace MailLoadTester.Gui;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // SEC-003/004: optional CLI acknowledgement for unrestricted live send.
        // Flag is read from Environment.GetCommandLineArgs() in MainForm.BuildOptions.
        // Usage: MailLoadTester.Gui.exe --unauthorized
        _ = args;
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
