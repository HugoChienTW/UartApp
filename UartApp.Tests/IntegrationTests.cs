// UartApp.Tests/IntegrationTests.cs
using Xunit;
using UartApp;

namespace UartApp.Tests
{
    [Trait("Category", "Integration")]
    public class IntegrationTests
    {
        [Fact(Skip = "Requires physical hardware")]
        public void RealSerialPort_Connection_WorksCorrectly()
        {
            // 這個測試需要實際的硬體設備
            var wrapper = new SerialPortWrapper();
            var communicator = new UartCommunicator(wrapper);

            // 實際測試會需要真實的串列埠
            // communicator.Connect("COM3", 9600);
            // Assert.True(communicator.IsConnected);
        }

        [Fact(Skip = "Requires physical hardware")]
        public async Task RealSerialPort_EchoTest_WorksCorrectly()
        {
            // 這個測試需要硬體設備支援回音功能
            var wrapper = new SerialPortWrapper();
            var communicator = new UartCommunicator(wrapper);

            try
            {
                // communicator.Connect("COM3", 9600);
                // var success = await communicator.TransmitDataWithEchoAsync("0xFF007C3A", 2000);
                // Assert.True(success);
                await Task.CompletedTask; // 避免 async 警告
            }
            finally
            {
                communicator.Dispose();
            }
        }

        [Fact]
        public void PythonFunctionality_DataConversion_WorksCorrectly()
        {
            // 測試 Python 程式的資料轉換功能
            var wrapper = new SerialPortWrapper();
            var communicator = new UartCommunicator(wrapper);

            // 測試十六進制轉換
            var hexResult = communicator.ConvertDataToBytes("0xFF007C3A");
            var expectedHex = new byte[] { 0xFF, 0x00, 0x7C, 0x3A };
            Assert.Equal(expectedHex, hexResult);

            // 測試文字轉換
            var textResult = communicator.ConvertDataToBytes("Hello");
            var expectedText = System.Text.Encoding.UTF8.GetBytes("Hello");
            Assert.Equal(expectedText, textResult);
        }
    }

    [Trait("Category", "PythonEquivalent")]
    public class PythonEquivalentTests
    {
        [Theory]
        [InlineData("0xFF007C3A", new byte[] { 0xFF, 0x00, 0x7C, 0x3A })]
        [InlineData("0xABCDEF12", new byte[] { 0xAB, 0xCD, 0xEF, 0x12 })]
        [InlineData("0x1234", new byte[] { 0x12, 0x34 })]
        public void ConvertHexData_LikePythonFromhex_ProducesCorrectBytes(string hexInput, byte[] expected)
        {
            // 模擬 Python 的 bytes.fromhex(data) 功能
            var wrapper = new SerialPortWrapper();
            var communicator = new UartCommunicator(wrapper);

            var result = communicator.ConvertDataToBytes(hexInput);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Hello", "48656C6C6F")]
        [InlineData("World", "576F726C64")]
        [InlineData("Test", "54657374")]
        public void ConvertTextData_LikePythonEncode_ProducesCorrectHex(string textInput, string expectedHex)
        {
            // 模擬 Python 的 data.encode('utf-8') 功能
            var wrapper = new SerialPortWrapper();
            var communicator = new UartCommunicator(wrapper);

            var result = communicator.ConvertDataToBytes(textInput);
            var resultHex = Convert.ToHexString(result);
            Assert.Equal(expectedHex, resultHex);
        }

        [Fact]
        public void ByteToHexConversion_LikePythonFormat_ProducesUppercaseHex()
        {
            // 模擬 Python 的 "%0.2X"%byte 功能
            byte testByte = 0xFF;
            string result = testByte.ToString("X2");
            Assert.Equal("FF", result);

            testByte = 0x0A;
            result = testByte.ToString("X2");
            Assert.Equal("0A", result);
        }
    }
}