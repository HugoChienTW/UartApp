# UartApp



## :feet: File list

\ (Project root directory)
├─README.md
├─UartCommunicator.sln
├─.gitignore
├─UartApp
│  ├─ UartApp.csproj
│  ├─ Program.cs
│  ├─ SerialPortWrapper.cs
│  ├─ UartCommunicator.cs
│  └─ ISerialPortWrapper.cs
├─UartApp.Tests
│  ├─ UartApp.Tests.csproj
│  ├─ IntegrationTests.cs
│  └─ UartCommunicatorTests.cs
├─UartStressTester
    └─UartStressTester
        ├─UartStressTester.csproj
        ├─Program.cs
        └─uart-test-config.json

## :feet: Build
:::success
List all the steps of your build
:::

```
cd .
dotnet build
```
```
cd .\UartApp\UartStressTester

dotnet build
or
dotnet publish --configuration Release --self-contained true --runtime win-x64 --output ./publish --property:PublishSingleFile=true
```
