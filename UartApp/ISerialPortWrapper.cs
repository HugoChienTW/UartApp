// UartApp/ISerialPortWrapper.cs
namespace UartApp
{
    public interface ISerialPortWrapper
    {
        bool IsOpen { get; }
        string PortName { get; set; }
        int BaudRate { get; set; }
        int DataBits { get; set; }
        string NewLine { get; set; }
        
        void Open();
        void Close();
        void WriteLine(string text);
        void Write(byte[] data);
        string ReadLine();
        string ReadExisting();
        byte[] ReadBytes(int count);
        void DiscardInBuffer();
        void DiscardOutBuffer();
        
        event EventHandler<string> DataReceived;
        event EventHandler<byte[]> RawDataReceived;
    }
}