using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace UndefinedSS.ServicesPrechecker
{
    internal static class ClipboardWriter
    {
        private static readonly int[] RetryDelaysMilliseconds =
            { 0, 60, 140, 280, 520 };

        public static async Task<bool> TrySetUnicodeTextWithRetryAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (int delay in RetryDelaysMilliseconds)
            {
                if (delay > 0)
                {
                    await Task.Delay(delay);
                }

                if (TrySetUnicodeText(text))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TrySetUnicodeText(string text)
        {
            try
            {
                int initializeResult = OleInitialize(IntPtr.Zero);
                if (initializeResult < 0)
                {
                    return false;
                }

                try
                {
                    DataObject dataObject = new DataObject();
                    dataObject.SetData(DataFormats.UnicodeText, text);
                    int setResult = OleSetClipboard(dataObject);
                    return setResult >= 0 && OleFlushClipboard() >= 0;
                }
                finally
                {
                    OleUninitialize();
                }
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException ||
                    exception is StackOverflowException ||
                    exception is ThreadAbortException ||
                    exception is AccessViolationException)
                {
                    throw;
                }
                return false;
            }
        }

        [DllImport("ole32.dll")]
        private static extern int OleInitialize(IntPtr reserved);

        [DllImport("ole32.dll")]
        private static extern void OleUninitialize();

        [DllImport("ole32.dll")]
        private static extern int OleSetClipboard(
            System.Runtime.InteropServices.ComTypes.IDataObject dataObject);

        [DllImport("ole32.dll")]
        private static extern int OleFlushClipboard();
    }
}
