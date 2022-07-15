using IpcRpcSharedMemory.Models;
using IpcRpcSharedMemory.Utilities;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace IpcRpcSharedMemory
{
    /// <summary>
    /// RpcClient (requestor / slave) part of communication.
    /// </summary>
    public class RpcClient
    {
        /// <summary>
        ///  The unique name for the RpcServer - For the mapped memory file and mutex descriptor.
        /// </summary>
        public string ListenerName { get; set; }

        /// <summary>
        /// Maximum memory file capacity (smaller = faster but can store less data). Default is 1024 bytes. Maximum should be around 1 MB.
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// RpcClient disposed and objects cleared indicator.
        /// </summary>
        public bool Disposed { get; private set; }

        private MemoryMappedFile _listenerMappedFile = null;
        private MemoryMappedViewAccessor _listenerMappedFileAccessor = null;

        /// <summary>
        /// RpcClient (requestor / slave) part of communication.
        /// </summary>
        /// <param name="listenerName">The RpcListener name - Will be unique to that host / process / server</param>
        /// <param name="capacity">The RpcListener name - Will be unique to that host / process / server</param>

        public RpcClient(string listenerName, int capacity = 1024)
        {
            ValidateInit(listenerName, capacity);

            ListenerName = listenerName;
            Capacity = capacity;

            _listenerMappedFile = MemoryMappedFile.CreateOrOpen(
                ListenerName + "_Listener_MappedFile", Capacity, MemoryMappedFileAccess.ReadWrite);

            _listenerMappedFileAccessor = _listenerMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        }

        /// <summary>
        /// Send the requst to the RpcServer
        /// </summaryc>
        /// <param name="requestMessage">RpcRequestMessage object to send - Containing data</param>
        /// <param name="timeout">Fail when timed out after this many miliseconds</param>
        /// <returns>RpcResponse object, possible containing data the server sends back</returns>
        public RpcResponse RemoteRequest(RpcRequestMessage requestMessage, int timeout = 5000)
        {
            ValidateRequest(requestMessage, ref timeout);

            var requestSemaphore = new Semaphore(0, 1, ListenerName + "_Request_Semaphore", out bool semaphoreCreatedNew);
            if (semaphoreCreatedNew)
                requestSemaphore.Release();

            var messageId = Guid.NewGuid();
            var response = new RpcResponse
            {
                MessageGuid = messageId,
                MethodName = requestMessage.MethodName,
                Success = false,
                Content = "Timed out"
            };

            if (requestSemaphore.WaitOne(timeout))
            {
                try
                {
                    int counter = 0;
                    while (true)
                    {
                        try
                        {
                            SendMessage(messageId, requestMessage);
                            response = WaitForResponse(messageId, requestMessage.MethodName, 200);

                            if (response.Success || counter > timeout)
                                break;
                            else
                            {
                                if (response.Content is string &&
                                    ((string)response.Content == "Duplicate memory file read - Ignore." ||
                                    (string)response.Content == "No response from server - Check you've started it." ||
                                    ((string)response.Content).StartsWith("Exception occurred: ")))
                                {
                                    counter += 35;
                                }
                                else
                                    counter += 200;

                                SafeThread.Sleep(1);
                            }
                        }
                        catch
                        {
                            counter += 30;
                            Thread.Sleep(1);
                        }

                        if (counter > timeout)
                            break;
                    }
                }
                finally
                {
                    requestSemaphore.Release();
                }
            }

            return response;
        }

        /// <summary>
        /// Send the requst to the RpcServer
        /// </summaryc>
        /// <param name="requestMessage">RpcRequestMessage object to send - Containing data</param>
        /// <param name="timeout">Fail when timed out after this many miliseconds</param>
        /// <returns>RpcResponse object, possible containing data the server sends back</returns>
        public async Task<RpcResponse> RemoteRequestAsync(RpcRequestMessage requestMessage, int timeout = 5000)
        {
            return await Task.Run(() => RemoteRequest(requestMessage, timeout));
        }

        private void SendMessage(Guid messageId, RpcRequestMessage requestMessage)
        {
            RpcPayload data = new RpcPayload
            {
                MessageGuid = messageId,
                Content = requestMessage.Content,
                MethodName = requestMessage.MethodName
            };

            string xmlData = data.SerializeToXml();
            byte[] buffer = Serialization.ConvertStringToByteArray(xmlData);

            var listenerSemaphore = new Semaphore(0, 1, ListenerName + "_Listener_Semaphore", out bool semaphoreCreatedNew);
            if (semaphoreCreatedNew)
                listenerSemaphore.Release();

            if (listenerSemaphore.WaitOne(100))
            {
                try
                {
                    _listenerMappedFileAccessor.WriteArray<byte>(0, buffer, 0, buffer.Length);
                }
                finally
                {
                    listenerSemaphore.Release();
                }
            }
        }

        private RpcResponse WaitForResponse(Guid messageId, string methodName, int timeout)
        {
            RpcResponse successResponse = null;

            var timeoutResponse = new RpcResponse
            {
                MessageGuid = messageId,
                MethodName = methodName,
                Success = false,
                Content = "Timed out."
            };

            var duplicateResponse = new RpcResponse
            {
                MessageGuid = messageId,
                MethodName = methodName,
                Success = false,
                Content = "Duplicate memory file read - Ignore."
            };

            var exceptionResponse = new RpcResponse
            {
                MessageGuid = messageId,
                MethodName = methodName,
                Success = false,
                Content = "Exception occurred: "
            };

            var noServerResponse = new RpcResponse
            {
                MessageGuid = messageId,
                MethodName = methodName,
                Success = false,
                Content = "No response from server - Check you've started it."
            };

            try
            {
                SafeThread.Sleep(1); // Give some time for other threads to have a go.

                using (MemoryMappedFile file = MemoryMappedFile.OpenExisting(
                    ListenerName + "_Response_MappedFile", MemoryMappedFileRights.Read))
                {
                    using (MemoryMappedViewAccessor accessor = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                    {
                        var responseSemaphore = new Semaphore(0, 1, ListenerName + "_Response_Semaphore", out bool semaphoreCreatedNew);
                        if (semaphoreCreatedNew)
                            responseSemaphore.Release();

                        if (responseSemaphore.WaitOne(timeout))
                        {
                            try
                            {
                                byte[] buffer = new byte[accessor.Capacity];
                                accessor.ReadArray<byte>(0, buffer, 0, buffer.Length);
                                successResponse = Serialization.DeserializeFromXml<RpcResponse>(
                                    buffer.ConvertByteArrayToString());

                                if (successResponse.MessageGuid == messageId)
                                    return successResponse;
                                else
                                    return duplicateResponse;
                            }
                            catch
                            {   
                                return noServerResponse;
                            }
                            finally
                            {
                                responseSemaphore.Release();
                            }
                        }
                    }
                }
            }
            catch (FileNotFoundException e)
            {
                exceptionResponse.Content += e.Message;
                return exceptionResponse;
            }
            catch (WaitHandleCannotBeOpenedException e)
            {
                exceptionResponse.Content += e.Message;
                return exceptionResponse;
            }
            catch (AbandonedMutexException e)
            {
                exceptionResponse.Content += e.Message;
                return exceptionResponse;
            }

            if (successResponse == null)
                return timeoutResponse;

            return successResponse;
        }

        private void ValidateInit(string listenerName, int capacity)
        {
            if (string.IsNullOrEmpty(listenerName))
                throw new ApplicationException("Listener name cannot be null or empty.");

            if (capacity < 256 || capacity > (1024 * 1024))
                throw new ApplicationException("Capacity needs to be between 256kb and 1MB");
        }

        private void ValidateRequest(RpcRequestMessage requestMessage, ref int timeout)
        {
            if (Disposed)
                throw new ApplicationException("This RpcClient has been disposed. You need to create a new one.");

            if (requestMessage == null)
                throw new ApplicationException("Request message cannot be null.");

            if (timeout <= 0)
                timeout = 5000;

            // Try not to bombard thread safety all at once
            Thread.Sleep(new Random().Next(1, 10));
        }

        /// <summary>
        /// Dipose and clear the RpcClient objects
        /// </summary>
        public void Dispose()
        {
            Disposed = true;

            _listenerMappedFileAccessor.Dispose();
            _listenerMappedFile.Dispose();
        }
    }
}
