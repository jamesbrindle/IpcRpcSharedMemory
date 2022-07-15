using System.Threading;

namespace IpcRpcSharedMemory.Utilities
{
    /// <summary>
    /// Don't throw on thread error methods
    /// </summary>
    internal class SafeThread
    {
        /// <summary>
        /// Sleep for given amount of milliseconds - Don't throw on erro
        /// </summary>
        /// <param name="milliseconds"></param>
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
