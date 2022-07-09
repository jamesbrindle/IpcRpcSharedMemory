using System.Threading;

namespace IpcRpcSharedMemory.Models.Utilities
{
    internal class SafeThread
    {
        internal static void Sleep(int milliseconds)
        {
            try
            {
                Thread.Sleep(milliseconds);
            }
            catch { }
        }
    }
}
