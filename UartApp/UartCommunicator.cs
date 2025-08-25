// UartApp/UartCommunicator.cs
using System.IO.Ports;
using System.Text;
using System.Diagnostics;

namespace UartApp
{
    public class UartCommunicator : IDisposable
    {
        private readonly ISerialPortWrapper _serialPort;
        private readonly List<string> _responseBuffer;
        private readonly object _bufferLock = new object();
        private DateTime _lastDataReceivedTime = DateTime.MinValue;
        
        public event EventHandler<string>? MessageReceived;
        public event EventHandler<byte[]>? RawDataReceived;

        public UartCommunicator(ISerialPortWrapper serialPort)
        {
            _serialPort = serialPort;
            _responseBuffer = new List<string>();
            _serialPort.DataReceived += OnDataReceived;
            _serialPort.RawDataReceived += OnRawDataReceived;
        }

        public bool IsConnected => _serialPort.IsOpen;

        public void Connect(string portName, int baudRate = 9600)
        {
            if (_serialPort.IsOpen)
                throw new InvalidOperationException("Already connected");

            _serialPort.PortName = portName;
            _serialPort.BaudRate = baudRate;
            _serialPort.DataBits = 8;
            _serialPort.NewLine = "\r\n";
            
            // 先開啟串列埠
            _serialPort.Open();
            
            // 然後清空緩衝區 (對應 Python 的 flushInput/flushOutput)
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
        }

        public void Disconnect()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        public void SendMessage(string message)
        {
            if (!_serialPort.IsOpen)
                throw new InvalidOperationException("Not connected");

            _serialPort.WriteLine(message);
        }

        public void TransmitData(string data)
        {
            if (!_serialPort.IsOpen)
                throw new InvalidOperationException("Not connected");

            byte[] dataToSend = ConvertDataToBytes(data);
            Console.WriteLine($"Sent data: {Convert.ToHexString(dataToSend)}");
            _serialPort.Write(dataToSend);
        }

        public byte[] ConvertDataToBytes(string data)
        {
            // 檢查是否為十六進制格式 (0x 開頭)
            if (data.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ConvertHexStringToBytes(data[2..]);
            }
            else
            {
                return Encoding.UTF8.GetBytes(data);
            }
        }

        public async Task<bool> TransmitDataWithEchoAsync(string data, int timeoutMs = 2000)
        {
            // 清空回應緩衝區並重置時間戳
            lock (_bufferLock)
            {
                _responseBuffer.Clear();
                _lastDataReceivedTime = DateTime.MinValue;
            }

            // 發送資料
            TransmitData(data);

            // 取得預期的回音資料
            byte[] expectedBytes = ConvertDataToBytes(data);
            string expectedHex = string.Join("", expectedBytes.Select(b => b.ToString("X2")));

            // 等待回音響應
            return await Task.Run(() => CheckEchoResponse(expectedHex, timeoutMs));
        }

        public string GetReceivedDataAsHex()
        {
            lock (_bufferLock)
            {
                return string.Join("", _responseBuffer);
            }
        }

        public bool CheckEchoResponse(string expectedHex, int timeoutMs = 2000, int intervalMs = 100)
        {
            var stopwatch = Stopwatch.StartNew();
            var noDataTimeoutMs = 300; // 300ms 內沒有新資料就認為接收結束

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                lock (_bufferLock)
                {
                    // 將回應緩衝區轉換為字串
                    string receivedHex = string.Join("", _responseBuffer);
                    var timeSinceLastData = (DateTime.Now - _lastDataReceivedTime).TotalMilliseconds;
                    
                    // 方法1: 完全匹配 (經典回音測試)
                    if (receivedHex == expectedHex)
                    {
                        return true; // ✅ 只返回結果，不打印
                    }
                    
                    // 方法2: 長度達到預期 + 一段時間沒有新資料 (實用判斷)
                    if (receivedHex.Length >= expectedHex.Length && 
                        _lastDataReceivedTime != DateTime.MinValue && 
                        timeSinceLastData >= noDataTimeoutMs)
                    {
                        if (receivedHex.StartsWith(expectedHex))
                        {
                            return true; // ✅ 只返回結果，不打印
                        }
                        else
                        {
                            return false; // ❌ 資料不匹配
                        }
                    }
                }

                Thread.Sleep(intervalMs);
            }

            return false; // ⏰ 超時
        }

        public string SendCommand(string command, int timeoutMs = 1000)
        {
            if (!_serialPort.IsOpen)
                throw new InvalidOperationException("Not connected");

            _serialPort.WriteLine(command);
            
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    return _serialPort.ReadLine();
                }
                catch
                {
                    Thread.Sleep(10);
                }
            }
            
            throw new TimeoutException("No response received within timeout period");
        }

        public async Task<string> SendCommandAsync(string command, int timeoutMs = 1000)
        {
            return await Task.Run(() => SendCommand(command, timeoutMs));
        }

        private byte[] ConvertHexStringToBytes(string hex)
        {
            // 移除空格和其他分隔符
            hex = hex.Replace(" ", "").Replace("-", "");
            
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have even length");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        private void OnDataReceived(object? sender, string data)
        {
            MessageReceived?.Invoke(this, data);
        }

        private void OnRawDataReceived(object? sender, byte[] data)
        {
            lock (_bufferLock)
            {
                // 記錄接收時間
                _lastDataReceivedTime = DateTime.Now;
                
                // 將每個 byte 轉換為十六進制字串 (對應 Python 的 %0.2X)
                // 只更新緩衝區，不在這裡打印 (避免重複顯示)
                foreach (byte b in data)
                {
                    string hexString = b.ToString("X2");
                    _responseBuffer.Add(hexString);
                }
            }
            
            // 觸發外部事件讓調用者決定如何顯示
            RawDataReceived?.Invoke(this, data);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}