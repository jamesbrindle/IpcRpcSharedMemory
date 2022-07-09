# IpcRpcSharedMemory

Local inter-process communication and remote procedure call using memory mapped files (Shared Memory).

## Technology

* C#
* .Net Framework
* Memory Mapped files
* Semaphores

## Overview

Sometimes you need a process or program to speak to another process or program. There are many ways to achive this, including:

1. Wcf / Named pipes
2. Message Queues
3. .NET Remoting
4. Sockets
5. WmCopyData
6. Plain old files

My requirements are, however to:

a) Not require a need for enabling additional Windows features other what's enabled by default.
b) Not require the use of ports which can be potentially blocked by firewalls or the port be in use by another application.
c) Not require the use of a specific archetecture (x86 / x64 - e.g. .NET Remoting).
d) Be bi-directional for enabling RPC.
e) Be able to handle concurrent requests.

I found several solutions online, however they either didn't work, used solely pointers, could not handle more than 1 'RPC Client' instance before falling over and didn't handle concurrent requests very well (not thread safe).

I decided to write my own, using **Memory Mapped files** for sharing memory between processes and **semaphores** for thread safety / synchronisation... You create a function or action on the RpcServer and send a payload with the RcpClient:

## Basic Usage

### Creating Server with a Return (Function<> Callback)

```
var rpcServer = new RpcServer("TestRpcServer", payload =>
{
    string content = (string)payload.Content; // Content 'object' data type, anything you want.
    Console.WriteLine("\nMemory Mapped File read");
    Console.WriteLine("Message Guid = " + payload.MessageGuid.ToString());
    Console.WriteLine("Content = " + payload.Content.ToString());
    
    return new RpcResult
    {
        Content = "Hello client"
    };
}).Start();
        
// Dispose when finished
// rpcServer.Dispose();
```

### Creating Server That Just Performs an Action (Action<> Callback)

```
var rpcServer = new RpcServer("TestRpcServer", payload =>
{
    string content = (string)payload.Content; // Content 'object' data type, anything you want.
    Console.WriteLine("\nMemory Mapped File read");
    Console.WriteLine("Message Guid = " + payload.MessageGuid.ToString());
    Console.WriteLine("Content = " + payload.Content.ToString());

    // Do something
}).Start();
        
// Dispose when finished
// rpcServer.Dispose();
```

### Creating a Client

```
    var rpcClient = new RpcClient("TestRpcClient");
    
    var response = rpcClient.RemoteRequest(new RpcRequestMessage
    {
        Content = $"Hello server"
    });
    
    Console.Out.WriteLine("Response = " + response.Content);
```

## Additional Options

You can also define:

1. The memory file capacity (between 256KB - 1MB):

```
var rpcServer = new RpcServer("TestRpcServer", payload =>
{
    return new RpcResult
    {
        Content = "Hello client"
    };
}, capacity: 4096).Start();
```

2. The response timeout (in milliseconds) for the client:

```
var response = rpcClient.RemoteRequest(new RpcRequestMessage
{
    Content = $"Hello server"
}, timeout: 60000);
```

## Async Available:

```
var response = rpcClient.RemoteRequestAsync(new RpcRequestMessage
{
    Content = $"Hello server"
}, timeout: 60000);

Console.Out.WriteLine("Response = " + await response.Content);
```

## Testing with Parallel Requests and Multiple RpcClient Instances
```
int failCounter = 0;
Parallel.For(0, 5000,
      index =>
      {
          var rpcRequst = new RpcClient("TestRpc");
          var response = rpcRequst.RemoteRequest(new RpcRequestMessage
          {
              Content = "Hello server"
          });

          if (!response.Success)
              failCounter++;
          else
              Console.Out.WriteLine(response.Content);
      });
      
Console.ReadKey();
```
*(There are no failures FYI).*


