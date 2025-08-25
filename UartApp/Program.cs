// UartApp/Program.cs
using UartApp;
using System.IO.Ports;

namespace UartApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 檢查測試模式
            if (args.Length > 0 && args[0] == "test")
            {
                await RunTestMode();
                return;
            }

            // 檢查埠查詢模式
            if (args.Length > 0 && args[0] == "ports")
            {
                ShowAvailablePorts();
                return;
            }

            // 檢查命令列參數 (模擬 Python 的參數檢查)
            if (args.Length == 3 && args[0] == "trans")
            {
                await RunTransmissionMode(args[1], args[2]);
                return;
            }

            // 互動模式
            await RunInteractiveMode(args);
        }

        static async Task RunTestMode()
        {
            Console.WriteLine("=== UART Communicator Test Mode ===");
            
            // 測試資料轉換功能 (不需要實際硬體)
            var wrapper = new SerialPortWrapper();
            var communicator = new UartCommunicator(wrapper);
            
            Console.WriteLine("\n1. Testing data conversion...");
            
            // 測試十六進制轉換
            var testData = "0x01050003FF007C3A";
            var bytes = communicator.ConvertDataToBytes(testData);
            Console.WriteLine($"Input: {testData}");
            Console.WriteLine($"Output: {Convert.ToHexString(bytes)}");
            Console.WriteLine($"Bytes: [{string.Join(", ", bytes.Select(b => $"0x{b:X2}"))}]");
            Console.WriteLine($"Length: {bytes.Length} bytes");
            
            // 測試文字轉換
            var textData = "Hello World";
            var textBytes = communicator.ConvertDataToBytes(textData);
            Console.WriteLine($"\nInput: {textData}");
            Console.WriteLine($"Output: {Convert.ToHexString(textBytes)}");
            Console.WriteLine($"ASCII: [{string.Join(", ", textBytes)}]");
            
            Console.WriteLine("\n2. Testing available ports...");
            ShowAvailablePorts();
            
            Console.WriteLine("\n3. Data conversion validation...");
            
            // 驗證 Python 等效性
            var pythonTestCases = new[]
            {
                ("0xFF007C3A", new byte[] { 0xFF, 0x00, 0x7C, 0x3A }),
                ("0x010203", new byte[] { 0x01, 0x02, 0x03 }),
                ("Hello", new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F })
            };
            
            foreach (var (input, expected) in pythonTestCases)
            {
                var result = communicator.ConvertDataToBytes(input);
                var match = result.SequenceEqual(expected);
                Console.WriteLine($"  {input} → {(match ? "✅ PASS" : "❌ FAIL")}");
                if (!match)
                {
                    Console.WriteLine($"    Expected: [{string.Join(", ", expected.Select(b => $"0x{b:X2}"))}]");
                    Console.WriteLine($"    Got:      [{string.Join(", ", result.Select(b => $"0x{b:X2}"))}]");
                }
            }
            
            Console.WriteLine("\n✨ Test mode completed successfully!");
            Console.WriteLine("💡 To test with real hardware:");
            Console.WriteLine("   1. Connect a serial device or create virtual COM ports");
            Console.WriteLine("   2. Use: UARTApp.exe trans <data> <available_port>");
            Console.WriteLine("   3. Or try interactive mode: UARTApp.exe <port> <baud>");
            
            // 避免 async 警告
            await Task.CompletedTask;
        }

        static async Task RunTransmissionMode(string data, string portName)
        {
            // 檢查是否為自動埠選擇
            if (portName.ToUpper() == "AUTO")
            {
                var functionalPorts = GetFunctionalPorts();
                if (!functionalPorts.Any())
                {
                    Console.WriteLine("❌ No functional COM ports found.");
                    Console.WriteLine("💡 Try 'UartApp.exe ports' to see all available ports.");
                    return;
                }
                
                portName = functionalPorts.First();
                Console.WriteLine($"🔍 Auto-selected port: {portName}");
            }

            // 執行詳細的埠檢查
            Console.WriteLine($"🔍 Checking port {portName}...");
            await DiagnosePort(portName);

            var serialPortWrapper = new SerialPortWrapper();
            var communicator = new UartCommunicator(serialPortWrapper);

            try
            {
                Console.WriteLine($"📡 Attempting to connect to {portName} at 9600 baud...");
                
                // 嘗試連接前先暫停一下
                await Task.Delay(100);
                
                communicator.Connect(portName, 9600);
                Console.WriteLine("✅ Connected successfully!");

                // 設置原始資料接收事件 (對應 Python 的 data_received)
                communicator.RawDataReceived += (sender, receivedData) =>
                {
                    Console.WriteLine($"📥 Received {receivedData.Length} byte(s): {string.Join(" ", receivedData.Select(b => b.ToString("X2")))}");
                };

                // 發送資料並檢查回音 (對應 Python 的 funname 函數)
                Console.WriteLine($"📤 Transmitting data: {data}");
                
                bool isHex = data.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"📋 Data type: {(isHex ? "Hexadecimal" : "String")}");
                
                // 解析並顯示資料內容
                var bytes = communicator.ConvertDataToBytes(data);
                Console.WriteLine($"📊 Parsed bytes: [{string.Join(", ", bytes.Select(b => $"0x{b:X2}"))}]");
                Console.WriteLine($"📏 Data length: {bytes.Length} bytes");

                // 發送資料並等待回音確認
                Console.WriteLine("⏳ Waiting for echo response...");
                bool success = await communicator.TransmitDataWithEchoAsync(data, timeoutMs: 2000);
                
                Console.WriteLine();
                if (success)
                {
                    Console.WriteLine("✅ Echo response received, transmission successful");
                }
                else
                {
                    // 提供詳細的失敗分析
                    var expectedBytes = communicator.ConvertDataToBytes(data);
                    var expectedHex = string.Join("", expectedBytes.Select(b => b.ToString("X2")));
                    var receivedHex = communicator.GetReceivedDataAsHex();
                    
                    Console.WriteLine("❌ Echo response failed");
                    Console.WriteLine($"   Expected: {expectedHex} ({expectedBytes.Length} bytes)");
                    Console.WriteLine($"   Received: {receivedHex} ({receivedHex.Length/2} bytes)");
                    
                    if (receivedHex.Length == 0)
                    {
                        Console.WriteLine("💡 No data received - device doesn't echo data back (normal for some devices)");
                    }
                    else if (receivedHex.Length < expectedHex.Length)
                    {
                        Console.WriteLine("💡 Partial data received - device might be slow or have transmission issues");
                    }
                    else if (receivedHex.StartsWith(expectedHex))
                    {
                        Console.WriteLine("💡 Expected data received with extra data - device might add protocol headers/footers");
                    }
                    else
                    {
                        Console.WriteLine("💡 Different data received - device might use different communication protocol");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"❌ Error: Access denied to {portName}");
                Console.WriteLine($"   Details: {ex.Message}");
                Console.WriteLine("💡 Possible solutions:");
                Console.WriteLine("   - Close other applications using this port (Putty, Arduino IDE, etc.)");
                Console.WriteLine("   - Run this program as Administrator");
                Console.WriteLine("   - Check if Windows Terminal or other terminal apps are connected");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"❌ Error: Invalid port configuration for '{portName}'");
                Console.WriteLine($"   Details: {ex.Message}");
                Console.WriteLine("💡 The port exists but has configuration issues");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"❌ Error: Port operation failed for '{portName}'");
                Console.WriteLine($"   Details: {ex.Message}");
                Console.WriteLine("💡 This often means 'The port is closed' - the port is not accessible");
                Console.WriteLine("💡 Try:");
                Console.WriteLine("   - Unplug and replug the USB device");
                Console.WriteLine("   - Close other applications using serial ports");
                Console.WriteLine("   - Run as Administrator");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"❌ Error: Hardware communication failed");
                Console.WriteLine($"   Details: {ex.Message}");
                Console.WriteLine("💡 Check hardware connection and drivers");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Unexpected error: {ex.Message}");
                Console.WriteLine($"   Type: {ex.GetType().Name}");
                Console.WriteLine($"   Details: {ex}");
            }
            finally
            {
                communicator.Disconnect();
                Console.WriteLine("🔌 Disconnected.");
            }
        }

        static async Task DiagnosePort(string portName)
        {
            Console.WriteLine($"🔬 Diagnosing port {portName}...");
            
            // 檢查埠是否在系統清單中
            var availablePorts = SerialPort.GetPortNames();
            bool portExists = availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
            
            Console.WriteLine($"   Port in system list: {(portExists ? "✅ Yes" : "❌ No")}");
            
            if (portExists)
            {
                // 嘗試快速連接測試
                try
                {
                    using (var testPort = new SerialPort(portName))
                    {
                        testPort.BaudRate = 9600;
                        testPort.DataBits = 8;
                        testPort.Parity = Parity.None;
                        testPort.StopBits = StopBits.One;
                        
                        Console.WriteLine("   Attempting quick connection test...");
                        testPort.Open();
                        await Task.Delay(50); // 短暫等待
                        testPort.Close();
                        Console.WriteLine("   Quick test: ✅ Port is accessible");
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("   Quick test: ⚠️  Port is in use by another application");
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"   Quick test: ❌ Port is closed ({ex.Message})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Quick test: ❌ Error ({ex.GetType().Name}: {ex.Message})");
                }
            }
            
            Console.WriteLine();
        }

        static async Task RunInteractiveMode(string[] args)
        {
            string portName = "COM3";
            int baudRate = 9600;

            // 解析命令列參數
            if (args.Length >= 1) portName = args[0];
            if (args.Length >= 2) int.TryParse(args[1], out baudRate);

            var serialPortWrapper = new SerialPortWrapper();
            var communicator = new UartCommunicator(serialPortWrapper);

            // 設置事件處理
            communicator.MessageReceived += (sender, message) =>
            {
                Console.WriteLine($"Text Received: {message}");
            };

            communicator.RawDataReceived += (sender, data) =>
            {
                Console.WriteLine($"📥 Received {data.Length} byte(s): {string.Join(" ", data.Select(b => b.ToString("X2")))}");
            };

            try
            {
                Console.WriteLine($"Connecting to {portName} at {baudRate} baud...");
                communicator.Connect(portName, baudRate);
                Console.WriteLine("Connected! Available commands:");
                ShowCommands();

                while (true)
                {
                    Console.Write("\nUART> ");
                    var input = Console.ReadLine();

                    if (string.IsNullOrEmpty(input))
                        continue;

                    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var command = parts[0].ToLower();

                    try
                    {
                        switch (command)
                        {
                            case "quit":
                            case "exit":
                                return;

                            case "send":
                                if (parts.Length < 2)
                                {
                                    Console.WriteLine("Usage: send <text>");
                                    break;
                                }
                                var message = string.Join(" ", parts[1..]);
                                communicator.SendMessage(message);
                                Console.WriteLine($"Sent: {message}");
                                break;

                            case "trans":
                                if (parts.Length < 2)
                                {
                                    Console.WriteLine("Usage: trans <data>");
                                    break;
                                }
                                var data = parts[1];
                                bool success = await communicator.TransmitDataWithEchoAsync(data, timeoutMs: 2000);
                                if (success)
                                {
                                    Console.WriteLine("✅ Transmission successful");
                                }
                                else
                                {
                                    var expectedBytes = communicator.ConvertDataToBytes(data);
                                    var expectedHex = string.Join("", expectedBytes.Select(b => b.ToString("X2")));
                                    var receivedHex = communicator.GetReceivedDataAsHex();
                                    Console.WriteLine("❌ Transmission failed");
                                    Console.WriteLine($"   Expected: {expectedHex}, Received: {receivedHex}");
                                }
                                break;

                            case "hex":
                                if (parts.Length < 2)
                                {
                                    Console.WriteLine("Usage: hex <hexdata>");
                                    break;
                                }
                                var hexData = parts[1];
                                if (!hexData.StartsWith("0x"))
                                    hexData = "0x" + hexData;
                                communicator.TransmitData(hexData);
                                Console.WriteLine($"Sent hex: {hexData}");
                                break;

                            case "at":
                                if (parts.Length < 2)
                                {
                                    Console.WriteLine("Usage: at <command>");
                                    break;
                                }
                                var atCommand = string.Join(" ", parts[1..]);
                                var response = await communicator.SendCommandAsync(atCommand, 3000);
                                Console.WriteLine($"AT Response: {response}");
                                break;

                            case "help":
                                ShowCommands();
                                break;

                            case "ports":
                                ShowAvailablePorts();
                                break;

                            default:
                                Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                                break;
                        }
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("Timeout: No response received");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
                ShowUsage();
            }
            finally
            {
                communicator.Disconnect();
                Console.WriteLine("Disconnected.");
            }
        }

        static void ShowCommands()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  send <text>     - Send text message");
            Console.WriteLine("  trans <data>    - Transmit data with echo check (supports 0x prefix for hex)");
            Console.WriteLine("  hex <hexdata>   - Send hexadecimal data (without 0x prefix)");
            Console.WriteLine("  at <command>    - Send AT command and wait for response");
            Console.WriteLine("  ports           - Show available COM ports");
            Console.WriteLine("  help            - Show this help");
            Console.WriteLine("  quit/exit       - Exit program");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  send Hello World");
            Console.WriteLine("  trans 0xFF007C3A");
            Console.WriteLine("  trans HelloWorld");
            Console.WriteLine("  hex FF007C3A");
            Console.WriteLine("  at AT+VERSION");
        }

        static void ShowUsage()
        {
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  Test mode:         UartApp.exe test");
            Console.WriteLine("  Transmission mode: UartApp.exe trans <data> <port|auto>");
            Console.WriteLine("  Interactive mode:  UartApp.exe [port] [baudrate]");
            Console.WriteLine("  Port check:        UartApp.exe ports");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  UartApp.exe test                           # Test without hardware");
            Console.WriteLine("  UartApp.exe ports                          # Check available COM ports");
            Console.WriteLine("  UartApp.exe trans 0xFF007C3A auto          # Auto-select working port");
            Console.WriteLine("  UartApp.exe trans 0xFF007C3A COM12         # Use specific port");
            Console.WriteLine("  UartApp.exe trans \"Hello World\" COM8       # Text transmission");
            Console.WriteLine("  UartApp.exe COM12 9600                     # Interactive mode");
            Console.WriteLine("  UartApp.exe                                # Interactive mode (auto-detect)");
            Console.WriteLine("\n💡 Tips:");
            Console.WriteLine("  - Use 'auto' to automatically select the first working COM port");
            Console.WriteLine("  - Check 'ports' command to see which ports are functional");
            Console.WriteLine("  - COM3 might need driver reinstallation if showing errors");
        }

        static void ShowAvailablePorts()
        {
            Console.WriteLine("Available COM ports:");
            var ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Console.WriteLine("  No COM ports found");
                Console.WriteLine("\nTroubleshooting tips:");
                Console.WriteLine("  1. Check Device Manager for serial ports");
                Console.WriteLine("  2. Ensure drivers are properly installed");
                Console.WriteLine("  3. Try running as Administrator");
                Console.WriteLine("  4. Check if ports are being used by other applications");
            }
            else
            {
                Console.WriteLine($"\nFound {ports.Length} COM port(s):");
                
                foreach (var port in ports.OrderBy(p => p))
                {
                    Console.Write($"  {port,-8}");
                    
                    // 嘗試獲取更多埠資訊
                    try
                    {
                        using (var serialPort = new SerialPort(port))
                        {
                            serialPort.Open();
                            serialPort.Close();
                            Console.WriteLine("✅ Available and functional");
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine("⚠️  In use by another application");
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine("❌ Invalid port name");
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("❌ Hardware error or disconnected");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error - {ex.GetType().Name}");
                    }
                }
                
                // 推薦最佳埠選擇
                var functionalPorts = GetFunctionalPorts();
                if (functionalPorts.Any())
                {
                    var recommendedPort = functionalPorts.First();
                    Console.WriteLine($"\n💡 Recommended port: {recommendedPort}");
                    Console.WriteLine($"   Test command: UartApp.exe trans 0x01050003FF007C3A {recommendedPort}");
                }
            }
            
            Console.WriteLine("\n🔧 Virtual port options for development:");
            Console.WriteLine("  - com0com (free virtual serial port pairs)");
            Console.WriteLine("  - VSPE (Virtual Serial Port Emulator)");
            Console.WriteLine("  - Terminal emulators with loopback capability");
            
            Console.WriteLine("\n🔍 Hardware debugging tips:");
            Console.WriteLine("  - Check Device Manager for yellow warning icons");
            Console.WriteLine("  - Verify USB cable connection");
            Console.WriteLine("  - Try different USB ports");
            Console.WriteLine("  - Update or reinstall device drivers");
        }

        static List<string> GetFunctionalPorts()
        {
            var functionalPorts = new List<string>();
            var ports = SerialPort.GetPortNames();
            
            foreach (var port in ports)
            {
                try
                {
                    using (var serialPort = new SerialPort(port))
                    {
                        serialPort.Open();
                        serialPort.Close();
                        functionalPorts.Add(port);
                    }
                }
                catch
                {
                    // 埠不可用，跳過
                }
            }
            
            return functionalPorts.OrderBy(p => p).ToList();
        }
    }
}