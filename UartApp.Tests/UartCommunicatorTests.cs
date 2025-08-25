// UartApp.Tests/UartCommunicatorTests.cs
using Xunit;
using Moq;
using UartApp;

namespace UartApp.Tests
{
    public class UartCommunicatorTests
    {
        private readonly Mock<ISerialPortWrapper> _mockSerialPort;
        private readonly UartCommunicator _communicator;

        public UartCommunicatorTests()
        {
            _mockSerialPort = new Mock<ISerialPortWrapper>();
            _communicator = new UartCommunicator(_mockSerialPort.Object);
        }

        [Fact]
        public void Connect_WithValidPort_OpensConnection()
        {
            // Arrange
            var portName = "COM3";
            var baudRate = 9600;

            // Act
            _communicator.Connect(portName, baudRate);

            // Assert
            _mockSerialPort.Verify(x => x.Open(), Times.Once);
            _mockSerialPort.VerifySet(x => x.PortName = portName, Times.Once);
            _mockSerialPort.VerifySet(x => x.BaudRate = baudRate, Times.Once);
            _mockSerialPort.Verify(x => x.DiscardInBuffer(), Times.Once);
            _mockSerialPort.Verify(x => x.DiscardOutBuffer(), Times.Once);
        }

        [Fact]
        public void IsConnected_WhenPortIsOpen_ReturnsTrue()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);

            // Act
            var result = _communicator.IsConnected;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Disconnect_WhenConnected_ClosesConnection()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);

            // Act
            _communicator.Disconnect();

            // Assert
            _mockSerialPort.Verify(x => x.Close(), Times.Once);
        }

        [Fact]
        public void TransmitData_WithHexString_SendsCorrectBytes()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);
            var hexData = "0xFF007C3A";
            var expectedBytes = new byte[] { 0xFF, 0x00, 0x7C, 0x3A };

            // Act
            _communicator.TransmitData(hexData);

            // Assert
            _mockSerialPort.Verify(x => x.Write(It.Is<byte[]>(b => 
                b.SequenceEqual(expectedBytes))), Times.Once);
        }

        [Fact]
        public void TransmitData_WithPlainString_SendsUtf8Bytes()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);
            var textData = "Hello";
            var expectedBytes = System.Text.Encoding.UTF8.GetBytes(textData);

            // Act
            _communicator.TransmitData(textData);

            // Assert
            _mockSerialPort.Verify(x => x.Write(It.Is<byte[]>(b => 
                b.SequenceEqual(expectedBytes))), Times.Once);
        }

        [Fact]
        public void SendMessage_WhenConnected_SendsDataToPort()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);
            var message = "Hello UART";

            // Act
            _communicator.SendMessage(message);

            // Assert
            _mockSerialPort.Verify(x => x.WriteLine(message), Times.Once);
        }

        [Fact]
        public void SendMessage_WhenDisconnected_ThrowsException()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                _communicator.SendMessage("test"));
        }

        [Fact]
        public void CheckEchoResponse_WithMatchingData_ReturnsSuccess()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);
            var sentData = "FF007C3A";
            
            // 模擬接收到相同的回音資料
            var responseBytes = new byte[] { 0xFF, 0x00, 0x7C, 0x3A };
            
            // Act
            _communicator.TransmitData($"0x{sentData}");
            
            // 模擬資料回音
            _mockSerialPort.Raise(x => x.RawDataReceived += null, _mockSerialPort.Object, responseBytes);
            
            var result = _communicator.CheckEchoResponse(sentData, timeoutMs: 1000);
            
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CheckEchoResponse_WithTimeout_ReturnsFalse()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);
            var sentData = "FF007C3A";
            
            // Act
            _communicator.TransmitData($"0x{sentData}");
            var result = _communicator.CheckEchoResponse(sentData, timeoutMs: 100);
            
            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TransmitDataWithEcho_SuccessfulTransmission_ReturnsTrue()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);
            var testData = "0xABCDEF";
            var expectedBytes = new byte[] { 0xAB, 0xCD, 0xEF };
            
            // Act
            var task = _communicator.TransmitDataWithEchoAsync(testData, timeoutMs: 1000);
            
            // 模擬回音響應
            await Task.Delay(50);
            _mockSerialPort.Raise(x => x.RawDataReceived += null, _mockSerialPort.Object, expectedBytes);
            
            var result = await task;
            
            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("0xFF007C3A", new byte[] { 0xFF, 0x00, 0x7C, 0x3A })]
        [InlineData("0xABCDEF12", new byte[] { 0xAB, 0xCD, 0xEF, 0x12 })]
        [InlineData("Hello", new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F })] // "Hello" in ASCII
        public void ConvertDataToBytes_VariousInputs_ReturnsCorrectBytes(string input, byte[] expected)
        {
            // Act
            var result = _communicator.ConvertDataToBytes(input);
            
            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void DataReceived_WhenDataArrives_RaisesMessageReceivedEvent()
        {
            // Arrange
            string? receivedMessage = null;
            _communicator.MessageReceived += (sender, message) => receivedMessage = message;

            // Act
            _mockSerialPort.Raise(x => x.DataReceived += null, _mockSerialPort.Object, "Test Data");

            // Assert
            Assert.Equal("Test Data", receivedMessage);
        }

        [Fact]
        public void RawDataReceived_WhenBytesArrive_RaisesRawDataReceivedEvent()
        {
            // Arrange
            byte[]? receivedData = null;
            _communicator.RawDataReceived += (sender, data) => receivedData = data;
            var testBytes = new byte[] { 0xFF, 0x00, 0x7C, 0x3A };

            // Act
            _mockSerialPort.Raise(x => x.RawDataReceived += null, _mockSerialPort.Object, testBytes);

            // Assert
            Assert.Equal(testBytes, receivedData);
        }

        [Fact]
        public void RawDataReceived_UpdatesResponseBuffer_WithHexStrings()
        {
            // Arrange
            var testBytes = new byte[] { 0xFF, 0x00, 0x7C, 0x3A };
            
            // Act
            _mockSerialPort.Raise(x => x.RawDataReceived += null, _mockSerialPort.Object, testBytes);
            
            // 等待一下讓事件處理完成
            Thread.Sleep(100);
            
            // 檢查是否正確添加到回應緩衝區 (透過檢查回音功能)
            var result = _communicator.CheckEchoResponse("FF007C3A", timeoutMs: 100);
            
            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SendCommandAsync_WithValidCommand_ReturnsResponse()
        {
            // Arrange
            _mockSerialPort.Setup(x => x.IsOpen).Returns(true);
            _mockSerialPort.Setup(x => x.ReadLine()).Returns("Response");

            // Act
            var response = await _communicator.SendCommandAsync("CMD");

            // Assert
            Assert.Equal("Response", response);
        }

        [Fact]
        public void ConvertHexStringToBytes_InvalidHexString_ThrowsException()
        {
            // Arrange
            var invalidHex = "0xZZZ";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _communicator.ConvertDataToBytes(invalidHex));
        }

        [Fact]
        public void ConvertHexStringToBytes_OddLengthHex_ThrowsException()
        {
            // Arrange
            var oddHex = "0xFFF";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _communicator.ConvertDataToBytes(oddHex));
        }
    }
}