# StreamConnect
Test utility to create a layer 4-type bridge from a pair of RS232, named pipe or TCP/IP streams, on Windows OS.

RS232 functionality is a bit limited - no error detection or flow control.

# Building

* Install Visual Studio code
* Install .Net SDK (https://dotnet.microsoft.com/learn/dotnet/hello-world-tutorial#install)
* Install the .Net 4.5 runtime (e.g. if building on Windows 7 or older)
* Open the project folder in VS code
* In VS code, start a terminal and run "dotnet build" to build the app

The build should output:

```PS c:\git\IpToSerial\StreamConnect\ip_to_serial_emulator\StreamConnect> dotnet build
Microsoft (R) Build Engine version 15.9.20+g88f5fadfbe for .NET Core
Copyright (C) Microsoft Corporation. All rights reserved.

  Nothing to do. None of the projects specified contain packages to restore.
  StreamConnect -> c:\git\IpToSerial\StreamConnect\bin\Debug\StreamConnect.exe

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.02
```

# Running the app
 * Run the app with a pair of stream endpoint specifications: `bin\debug\StreamConnect.exe <SourceStream> <DestinationStream>`
 * RS232 comms uses 115200 baud, with DTR raised while a client is connected and is specified in the form "COMxx" where x is the COM port
 * Named pipe comms is specified as `\\.\pipe\PipeNameHere`
 * A socket endpoint is specified as `IP:port` where IP is a dotted IPv4 address

For example, when started with `bin\debug\StreamConnect.exe 0.0.0.0:49153 COM1`, the app will accept incoming TCP/IP connections and when connected, redirect data to/from the local COM port.

Output should be like this:
```PS C:\git\StreamConnect> .\bin\debug\StreamConnect.exe 0.0.0.0:1234 COM1
Source Socket 0.0.0.0:1234 -> Dest Serial COM1
Connection from Socket 0.0.0.0:1234
Connection made
............................Input stream disconnected
```

NB the tool echoes dots to the console as data is forwarded.

# Why?

I use it for redirecting / monitoring serial port usage from VMs when talking to fixed-function RS232 devices, and for emulating IPToSerial devices.
# Running with virtual serial ports

The app can also be used to map VMWare virtual serial ports to local COM ports, see Program.cs for details.

# Mapping a COM port within a hyper V Gen 2 VM to a host com port
 * Shut down the VM
 * Disable UEFI secure boot in the vm
   - `Set-VMFirmware -VMName my_gen2_vm -EnableSecureBoot Off`
 * On the host, associate a pipe with the VM COM port  
   - `Set-VMComPort -VMName my_gen2_vm -Number 1 -Path \\.\pipe\com1`
 * Run a named pipe server on the host
   - `.\bin\debug\StreamConnect.exe \\.\pipe\com1 COM1`
 * Start the VM
 * Named pipe server should show:
   - `Connection from NamedPipe \\.\pipe\com1`
   - `Connection made`

