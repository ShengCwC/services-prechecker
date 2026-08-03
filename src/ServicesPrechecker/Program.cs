using System;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace UndefinedSS.ServicesPrechecker
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool autoEnable = args.Any(
                delegate(string value)
                {
                    return string.Equals(value, "--enable-all", StringComparison.OrdinalIgnoreCase);
                });
            string targetUserSid = ReadTargetUserSid(args);
            if (!autoEnable)
            {
                targetUserSid = GetCurrentUserSid();
            }

            AppDomain.CurrentDomain.UnhandledException +=
                delegate(object sender, UnhandledExceptionEventArgs eventArgs)
                {
                    Exception exception = eventArgs.ExceptionObject as Exception;
                    if (exception != null)
                    {
                        MessageBox.Show(
                            "程序遇到未处理的错误：\n\n" + exception.Message,
                            "Services Prechecker",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                };

            Application application = new Application();
            application.ShutdownMode = ShutdownMode.OnMainWindowClose;
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            MainWindow window = new MainWindow(autoEnable, targetUserSid);
            application.Run(window);
        }

        private static string ReadTargetUserSid(string[] args)
        {
            const string prefix = "--target-user-sid=";
            string value = args.FirstOrDefault(
                delegate(string argument)
                {
                    return argument != null &&
                        argument.StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase);
                });
            return value == null ? null : value.Substring(prefix.Length);
        }

        private static string GetCurrentUserSid()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                return identity.User == null ? null : identity.User.Value;
            }
            catch
            {
                return null;
            }
        }
    }
}
