using IpcRpcSharedMemory.Models;
using IpcRpcSharedMemory.Utilities;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IpcRpcSharedMemory
{
    /// <summary>
    /// RpcServer (listener / host) part of communication
    /// </summary>
    public class RpcServer
    {
        /// <summary>
        /// Indicates whether the RpcListener is currently listening.
        /// </summary>
        public bool Listening { get; private set; } = false;

        /// <summary>
        /// RcpServer disposed and objects cleared indicator.
        /// </summary>
        public bool Disposed { get; private set; } = false;

        /// <summary>
        ///  A unique name to this process - For the mapped memory file and mutex descriptor.
        /// </summary>
        public string ListenerName { get; private set; }

        /// <summary>
        /// Maximum memory file capacity (smaller = faster but can store less data). Default is 1024 bytes. Maximum should be around 1 MB.
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        ///  How often the listener should access the shared memory and read the data - Default is every 5 miliseconds.
        /// </summary>
        public int PollFrequency { get; set; }

        private Action<RpcPayload> RemoteCallHandler = null;
        private Func<RpcPayload, RpcResult> RemoteCallHandlerWithResult = null;

        private byte[] _lastMessageBytes = null;
        private Guid _lastMessageGuid = Guid.NewGuid();
        private Thread _listenerThread = null;
        private BindingList<RpcUnprocessItem> _incomingMessages = new BindingList<RpcUnprocessItem>();
        private BindingList<RpcUnprocessItem> _outgoingMessages = new BindingList<RpcUnprocessItem>();
        private MemoryMappedFile _responseMappedFile = null;
        private MemoryMappedViewAccessor _responseMappedFileAccessor = null;

        /// <summary>
        /// Create new RpcListener with an action callback (just 'do something' and don't pass any data back) apart
        /// from a 'success' response.
        /// </summary>
        /// <param name="uniqueInstanceListenerName">This RpcListener name - Must be unique</param>
        /// <param name="actionCallback">Action to be performed callback</param>
        /// <param name="capacity">Maximum memory file capacity (smaller = faster but can store less data). Default is 1024 bytes. Maximum should be around 1 MB.</param>
        /// <param name="pollFrequencyMs">How often the listener should access the shared memory and read the data - Default is every 3 miliseconds.</param>
        public RpcServer(
            string uniqueInstanceListenerName,
            Action<RpcPayload> actionCallback,
            int capacity = 1024,
            int pollFrequencyMs = 3)
        {
            ValidateInit(uniqueInstanceListenerName, capacity, ref pollFrequencyMs);
            ResetSemaphores();

            ListenerName = uniqueInstanceListenerName;
            PollFrequency = pollFrequencyMs;
            RemoteCallHandler = actionCallback;
            Capacity = capacity;

            _incomingMessages.ListChanged += ProcessIncomingMessage;
            _outgoingMessages.ListChanged += ProcessOutgoingMessage;

            _responseMappedFile = MemoryMappedFile.CreateOrOpen(
                ListenerName + "_Response_MappedFile", Capacity, MemoryMappedFileAccess.ReadWrite);

            _responseMappedFileAccessor = _responseMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        }

        /// <summary>
        /// Create new RpcListener with an function callback (do something and return data).
        /// </summary>
        /// <param name="uniqueInstanceListenerName">This RpcListener name - Must be unique</param>
        /// <param name="functionCallback">Function to be executed and the result returned</param>
        /// <param name="capacity">Maximum memory file capacity (smaller = faster but can store less data). Default is 1024 bytes. Maximum should be around 1 MB.</param>
        /// <param name="pollFrequencyMs">How often the listener should access the shared memory and read the data - Default is every 3 miliseconds.</param>
        public RpcServer(
            string uniqueInstanceListenerName,
            Func<RpcPayload, RpcResult> functionCallback,
            int capacity = 1024,
            int pollFrequencyMs = 3)
        {
            ValidateInit(uniqueInstanceListenerName, capacity, ref pollFrequencyMs);
            ResetSemaphores();

            ListenerName = uniqueInstanceListenerName;
            PollFrequency = pollFrequencyMs;
            RemoteCallHandlerWithResult = functionCallback;
            Capacity = capacity;

            _incomingMessages.ListChanged += ProcessIncomingMessage;
            _outgoingMessages.ListChanged += ProcessOutgoingMessage;

            _responseMappedFile = MemoryMappedFile.CreateOrOpen(
                ListenerName + "_Response_MappedFile", Capacity, MemoryMappedFileAccess.ReadWrite);

            _responseMappedFileAccessor = _responseMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        }

        /// <summary>
        /// Set the RpcServer to start listening
        /// </summary>
        public RpcServer Start()
        {
            if (Disposed)
                throw new ApplicationException("This RpcServer has been disposed. You need to create a new one.");

            if (Listening) // Already running
                return this;

            Listening = true;
            if (_listenerThread != null)
            {
                try
                {
                    _listenerThread.Abort();
                }
                catch { }
            }

            _listenerThread = null;
            _listenerThread = new Thread((ThreadStart)delegate
            {
                while (Listening)
                {
                    ReadRequestData();
                    SafeThread.Sleep(PollFrequency);
                }
            })
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };

            _listenerThread.Start();

            return this;
        }

        /// <summary>
        /// Stop the RpcServer from Listening
        /// </summary>
        public void Stop()
        {
            Listening = false;
            SafeThread.Sleep(50);

            try
            {
                _listenerThread.Abort();
            }
            catch { }

            try
            {
                _listenerThread = null;
            }
            catch { }

            ResetSemaphores();
        }

        private void ProcessIncomingMessage(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                RpcUnprocessItem currentMessage = null;
                lock (_incomingMessages)
                    currentMessage = _incomingMessages[e.NewIndex];

                if (currentMessage.Bytes != null)
                {
                    try
                    {
                        if (currentMessage.Bytes.Any(b => b != 0))
                        {
                            Task.Factory.StartNew(delegate
                            {
                                var payload = Serialization.DeserializeFromXml<RpcPayload>(
                                    currentMessage.Bytes.ConvertByteArrayToString());

                                if (payload != null &&
                                    _lastMessageGuid != payload.MessageGuid &&
                                    payload.MessageGuid != Guid.Empty)
                                {
                                    _lastMessageGuid = payload.MessageGuid;

                                    if (RemoteCallHandler != null)
                                    {
                                        RemoteCallHandler(payload);
                                        var response = new RpcResponse
                                        {
                                            MessageGuid = payload.MessageGuid,     
                                            MethodName = payload.MethodName,
                                            Content = "OK",
                                            Success = true
                                        };

                                        AddToResponseQueue(response);
                                    }

                                    else if (RemoteCallHandlerWithResult != null)
                                    {
                                        var result = RemoteCallHandlerWithResult(payload);
                                        var response = new RpcResponse
                                        {
                                            MessageGuid = payload.MessageGuid,
                                            MethodName = payload.MethodName,
                                            Content = result.Content,
                                            Success = true
                                        };

                                        AddToResponseQueue(response);
                                    }
                                }
                            });
                        }
                    }
                    catch { }
                }

                try
                {
                    lock (_incomingMessages)
                    {
                        _incomingMessages.Remove(
                            _incomingMessages.Where(m => m.Id == currentMessage.Id)
                                             .FirstOrDefault());
                    }
                }
                catch { }
            }
        }

        private void AddToResponseQueue(RpcResponse response)
        {
            string xmlData = response.SerializeToXml();
            byte[] buffer = Serialization.ConvertStringToByteArray(xmlData);

            lock (_outgoingMessages)
            {
                _outgoingMessages.Add(new RpcUnprocessItem
                {
                    Id = Guid.NewGuid(),
                    Bytes = buffer
                });
            }
        }

        private void ReadRequestData()
        {
            try
            {
                SafeThread.Sleep(1); // Give some time for other threads to have a go.

                using (MemoryMappedFile file = MemoryMappedFile.OpenExisting(
                    ListenerName + "_Listener_MappedFile", MemoryMappedFileRights.Read))
                {
                    using (MemoryMappedViewAccessor accessor = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                    {
                        var listenerSemaphore = new Semaphore(0, 1, ListenerName + "_Listener_Semaphore", out bool semaphoreCreatedNew);
                        if (semaphoreCreatedNew)
                            listenerSemaphore.Release();

                        if (listenerSemaphore.WaitOne(100))
                        {
                            try
                            {
                                byte[] buffer = new byte[accessor.Capacity];
                                accessor.ReadArray<byte>(0, buffer, 0, buffer.Length);

                                if (!Serialization.ByteArrayCompare(buffer, _lastMessageBytes))
                                {
                                    _lastMessageBytes = buffer;
                                    Task.Factory.StartNew(delegate
                                    {
                                        // Add to a queue for another thread to deserialise for performance
                                        lock (_incomingMessages)
                                        {
                                            _incomingMessages.Add(new RpcUnprocessItem
                                            {
                                                Id = Guid.NewGuid(),
                                                Bytes = buffer
                                            });
                                        }
                                    });
                                }
                            }
                            finally
                            {
                                listenerSemaphore.Release();
                            }
                        }
                    }
                }
            }
            catch (FileNotFoundException)
            { }
            catch (WaitHandleCannotBeOpenedException)
            { }
            catch (AbandonedMutexException)
            { }
        }

        private void ProcessOutgoingMessage(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded)
            {
                RpcUnprocessItem currentMessage = null;
                lock (_outgoingMessages)
                    currentMessage = _outgoingMessages[e.NewIndex];

                SendResponseMessage(currentMessage.Bytes);

                try
                {
                    lock (_outgoingMessages)
                    {
                        _outgoingMessages.Remove(
                            _outgoingMessages.Where(m => m.Id == currentMessage.Id)
                                             .FirstOrDefault());
                    }
                }
                catch { }
            }
        }

        private void SendResponseMessage(byte[] responseBuffer)
        {
            SafeThread.Sleep(1);

            var responseSemaphore = new Semaphore(0, 1, ListenerName + "_Response_Semaphore", out bool semaphoreCreatedNew);
            if (semaphoreCreatedNew)
                responseSemaphore.Release();

            if (responseSemaphore.WaitOne(200))
            {
                try
                {
                    _responseMappedFileAccessor.WriteArray<byte>(0, responseBuffer, 0, responseBuffer.Length);
                }
                finally
                {
                    responseSemaphore.Release();
                }
            }
        }

        private void ResetSemaphores()
        {
            var requestSemaphore = new Semaphore(0, 1, "_Request_Semaphore", out bool requestSemaphoreCreatedNew);
            if (!requestSemaphoreCreatedNew)
            {
                try
                {
                    requestSemaphore.Release(10);
                    requestSemaphore.Dispose();
                }
                catch { }
            }
            else
            {
                try
                {
                    requestSemaphore.Dispose();
                }
                catch { }
            }

            var listenerSemaphore = new Semaphore(0, 1, "_Request_Semaphore", out bool listenerSemaphoreCreatedNew);
            if (!listenerSemaphoreCreatedNew)
            {
                try
                {
                    listenerSemaphore.Release(10);
                    listenerSemaphore.Dispose();
                }
                catch { }
            }
            else
            {
                try
                {
                    listenerSemaphore.Dispose();
                }
                catch { }
            }

            var responseSemaphore = new Semaphore(0, 1, "_Request_Semaphore", out bool responseSemaphoreCreatedNew);
            if (!responseSemaphoreCreatedNew)
            {
                try
                {
                    responseSemaphore.Release(10);
                    responseSemaphore.Dispose();
                }
                catch { }
            }
            else
            {
                try
                {
                    responseSemaphore.Dispose();
                }
                catch { }
            }
        }

        private void ValidateInit(string uniqueInstanceListenerName, int capacity, ref int pollFrequencyMs)
        {
            if (string.IsNullOrEmpty(uniqueInstanceListenerName))
                throw new ApplicationException("Listener name cannot be null or empty.");

            if (capacity < 256 || capacity > (1024 * 1024))
                throw new ApplicationException("Capacity needs to be between 256kb and 1MB");

            if (pollFrequencyMs <= 0)
                pollFrequencyMs = 5;
        }

        /// <summary>
        /// Stop the RpcServer from listening and dispose / clear objects
        /// </summary>
        public void Dispose()
        {
            Listening = false;
            SafeThread.Sleep(50);

            try
            {
                _listenerThread.Abort();
            }
            catch { }

            try
            {
                _listenerThread = null;
            }
            catch { }

            _incomingMessages.Clear();
            _outgoingMessages.Clear();

            RemoteCallHandler = null;
            RemoteCallHandlerWithResult = null;

            _responseMappedFileAccessor.Dispose();
            _responseMappedFile.Dispose();

            _lastMessageBytes = null;

            ResetSemaphores();

            Disposed = true;
        }
    }
}
