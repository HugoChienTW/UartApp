// UartApp/SerialPortWrapper.cs
using System.IO.Ports;

namespace UartApp
{
    public class SerialPortWrapper : ISerialPortWrapper, IDisposable
    {
        private readonly SerialPort _serialPort;

        public SerialPortWrapper()
        {
            _serialPort = new SerialPort();
            _serialPort.DataReceived += OnDataReceived;
        }

        public bool IsOpen => _serialPort.IsOpen;
        public string PortName 
        { 
            get => _serialPort.PortName; 
            set => _serialPort.PortName = value; 
        }
        public int BaudRate 
        { 
            get => _serialPort.BaudRate; 
            set => _serialPort.BaudRate = value; 
        }
        public int DataBits 
        { 
            get => _serialPort.DataBits; 
            set => _serialPort.DataBits = value; 
        }
        public string NewLine 
        { 
            get => _serialPort.NewLine; 
            set => _serialPort.NewLine = value; 
        }

        public event EventHandler<string>? DataReceived;
        public event EventHandler<byte[]>? RawDataReceived;

        public void Open()
        {
            _serialPort.Open();
        }

        public void Close()
        {
            _serialPort.Close();
        }

        public void WriteLine(string text)
        {
            _serialPort.WriteLine(text);
        }

        public void Write(byte[] data)
        {
            _serialPort.Write(data, 0, data.Length);
        }

        public string ReadLine()
        {
            return _serialPort.ReadLine();
        }

        public string ReadExisting()
        {
            return _serialPort.ReadExisting();
        }

        public byte[] ReadBytes(int count)
        {
            byte[] buffer = new byte[count];
            int bytesRead = _serialPort.Read(buffer, 0, count);
            
            if (bytesRead < count)
            {
                Array.Resize(ref buffer, bytesRead);
            }
            
            return buffer;
        }

        public void DiscardInBuffer()
        {
            _serialPort.DiscardInBuffer();
        }

        public void DiscardOutBuffer()
        {
            _serialPort.DiscardOutBuffer();
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // 讀取原始 byte 資料
                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead > 0)
                {
                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);
                    
                    if (bytesRead > 0)
                    {
                        // 調整陣列大小以符合實際讀取的位元組數
                        if (bytesRead < buffer.Length)
                        {
                            Array.Resize(ref buffer, bytesRead);
                        }
                        
                        // 觸發原始資料事件
                        RawDataReceived?.Invoke(this, buffer);
                        
                        // 也觸發文字資料事件 (向後相容)
                        string textData = System.Text.Encoding.UTF8.GetString(buffer);
                        DataReceived?.Invoke(this, textData);
                    }
                }
            }
            catch (Exception ex)
            {
                // 處理讀取錯誤
                Console.WriteLine($"Error reading data: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _serialPort?.Dispose();
        }
    }
}