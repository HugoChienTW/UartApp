// UartStressTester.cs - UART 壓力測試程式 (支援 JSON 設定檔)
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UartStressTester
{
    public class UartTestConfig
    {
        [JsonPropertyName("targetProgram")]
        public string TargetProgram { get; set; } = "UartApp.exe";

        [JsonPropertyName("testCommands")]
        public List<string> TestCommands { get; set; } = new List<string>
        {
            "trans 0x01050003FF007C3A COM3",
            "trans 0x0105000300003DCA COM3"
        };

        [JsonPropertyName("testCount")]
        public int TestCount { get; set; } = 50;

        [JsonPropertyName("delayMs")]
        public int DelayMs { get; set; } = 100;

        [JsonPropertyName("commandTimeoutMs")]
        public int CommandTimeoutMs { get; set; } = 10000;

        [JsonPropertyName("enableDetailedLog")]
        public bool EnableDetailedLog { get; set; } = true;

        [JsonPropertyName("reportFormat")]
        public string ReportFormat { get; set; } = "txt";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "UART 串列通訊測試";

        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public class TestResult
    {
        public int TestNumber { get; set; }
        public string Command { get; set; } = "";
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TestStatistics
    {
        public int TotalTests { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double SuccessRate => TotalTests > 0 ? (double)SuccessCount / TotalTests * 100 : 0;
        public double FailureRate => TotalTests > 0 ? (double)FailureCount / TotalTests * 100 : 0;
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration => TotalTests > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalTests) : TimeSpan.Zero;
    }

    class Program
    {
        private static UartTestConfig Config = new UartTestConfig();
        private static List<TestResult> TestResults = new List<TestResult>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("🧪 UART 串列通訊壓力測試程式 v2.0");
            Console.WriteLine("=====================================");

            // 解析命令列參數
            string configFile = "uart-test-config.json";
            
            if (args.Length > 0)
            {
                if (args[0] == "--help" || args[0] == "-h")
                {
                    ShowHelp();
                    return;
                }
                
                if (args[0] == "--generate-config" || args[0] == "-g")
                {
                    await GenerateDefaultConfig(args.Length > 1 ? args[1] : configFile);
                    return;
                }

                if (args[0].EndsWith(".json"))
                {
                    configFile = args[0];
                }
                else
                {
                    // 向後相容：支援舊的命令列參數
                    ParseLegacyArguments(args);
                }
            }

            // 讀取或生成設定檔
            await LoadOrCreateConfig(configFile);

            // 驗證設定
            if (!ValidateConfig())
            {
                return;
            }

            // 顯示測試設定
            ShowTestConfiguration();

            // 開始測試
            Console.WriteLine("🚀 開始測試...");
            var totalStopwatch = Stopwatch.StartNew();

            for (int i = 0; i < Config.TestCount; i++)
            {
                var command = Config.TestCommands[i % Config.TestCommands.Count];
                var testNumber = i + 1;

                Console.Write($"📝 測試 {testNumber:D3}/{Config.TestCount:D3} - {GetCommandDisplayName(command)} ... ");

                var result = await ExecuteUartCommand(testNumber, command);
                TestResults.Add(result);

                var status = result.Success ? "✅" : "❌";
                var duration = $"{result.Duration.TotalMilliseconds:F0}ms";
                Console.WriteLine($"{status} ({duration})");

                // 如果失敗且啟用詳細記錄，顯示錯誤訊息
                if (!result.Success && Config.EnableDetailedLog)
                {
                    Console.WriteLine($"   錯誤: {GetFailureReason(result)}");
                }

                // 每10次測試顯示一次統計
                if (testNumber % 10 == 0)
                {
                    ShowProgressStats();
                }

                // 測試間隔
                if (i < Config.TestCount - 1)
                {
                    await Task.Delay(Config.DelayMs);
                }
            }

            totalStopwatch.Stop();

            // 顯示最終結果
            Console.WriteLine();
            ShowFinalResults(totalStopwatch.Elapsed);

            // 儲存詳細報告
            await SaveDetailedReport();

            Console.WriteLine("\n🎯 測試完成！按任意鍵結束...");
            Console.ReadKey();
        }

        static async Task LoadOrCreateConfig(string configFile)
        {
            try
            {
                if (File.Exists(configFile))
                {
                    Console.WriteLine($"📄 讀取設定檔: {configFile}");
                    var jsonContent = await File.ReadAllTextAsync(configFile);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    };
                    
                    Config = JsonSerializer.Deserialize<UartTestConfig>(jsonContent, options) ?? new UartTestConfig();
                    Console.WriteLine("✅ 設定檔載入成功");
                }
                else
                {
                    Console.WriteLine($"⚠️  設定檔 {configFile} 不存在，使用預設設定");
                    Console.WriteLine("💡 可以使用 -g 參數產生預設設定檔");
                    
                    // 使用預設設定
                    Config = new UartTestConfig();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 讀取設定檔失敗: {ex.Message}");
                Console.WriteLine("使用預設設定繼續執行...");
                Config = new UartTestConfig();
            }
        }

        static void ParseLegacyArguments(string[] args)
        {
            // 向後相容舊的命令列參數格式
            if (args.Length >= 1 && int.TryParse(args[0], out int testCount))
                Config.TestCount = testCount;
            if (args.Length >= 2 && int.TryParse(args[1], out int delayMs))
                Config.DelayMs = delayMs;
            if (args.Length >= 3)
                Config.TargetProgram = args[2];
            
            Console.WriteLine("⚠️  使用舊版命令列參數格式，建議使用 JSON 設定檔");
        }

        static bool ValidateConfig()
        {
            if (!File.Exists(Config.TargetProgram))
            {
                Console.WriteLine($"❌ 找不到目標程式: {Config.TargetProgram}");
                Console.WriteLine("請確認檔案路徑或將此程式放在與目標程式相同的目錄中");
                return false;
            }

            if (Config.TestCommands.Count == 0)
            {
                Console.WriteLine("❌ 沒有設定測試命令");
                return false;
            }

            if (Config.TestCount <= 0)
            {
                Console.WriteLine("❌ 測試次數必須大於 0");
                return false;
            }

            return true;
        }

        static void ShowTestConfiguration()
        {
            Console.WriteLine($"📋 測試設定:");
            Console.WriteLine($"   描述: {Config.Description}");
            Console.WriteLine($"   目標程式: {Config.TargetProgram}");
            Console.WriteLine($"   測試次數: {Config.TestCount}");
            Console.WriteLine($"   測試間隔: {Config.DelayMs}ms");
            Console.WriteLine($"   命令逾時: {Config.CommandTimeoutMs}ms");
            Console.WriteLine($"   詳細記錄: {(Config.EnableDetailedLog ? "啟用" : "停用")}");
            Console.WriteLine($"   報告格式: {Config.ReportFormat.ToUpper()}");
            Console.WriteLine($"   測試命令:");
            for (int i = 0; i < Config.TestCommands.Count; i++)
            {
                Console.WriteLine($"     {i + 1}. {Config.TestCommands[i]}");
            }

            if (Config.Metadata.Any())
            {
                Console.WriteLine($"   額外資訊:");
                foreach (var meta in Config.Metadata)
                {
                    Console.WriteLine($"     {meta.Key}: {meta.Value}");
                }
            }
            Console.WriteLine();
        }

        static string GetCommandDisplayName(string command)
        {
            var parts = command.Split(' ');
            return parts.Length >= 2 ? parts[1] : command;
        }

        static async Task<TestResult> ExecuteUartCommand(int testNumber, string command)
        {
            var result = new TestResult
            {
                TestNumber = testNumber,
                Command = command,
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Config.TargetProgram,
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var outputTask = process.StandardOutput.ReadToEndAsync();
                        var errorTask = process.StandardError.ReadToEndAsync();

                        // 使用設定檔中的逾時時間
                        var completed = await Task.Run(() => process.WaitForExit(Config.CommandTimeoutMs));

                        if (!completed)
                        {
                            process.Kill();
                            result.Success = false;
                            result.Error = $"程式執行超時 ({Config.CommandTimeoutMs / 1000}秒)";
                        }
                        else
                        {
                            result.Output = await outputTask;
                            result.Error = await errorTask;
                            result.Success = AnalyzeOutput(result.Output, result.Error);
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Error = "無法啟動程式";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return result;
        }

        static bool AnalyzeOutput(string output, string error)
        {
            // 檢查是否有錯誤
            if (!string.IsNullOrEmpty(error))
                return false;

            // 檢查輸出中的成功標記
            if (output.Contains("Echo response received, transmission successful") ||
                output.Contains("✅ Echo response received, transmission successful"))
                return true;

            // 檢查是否有明顯的失敗標記
            if (output.Contains("❌") || 
                output.Contains("Error:") || 
                output.Contains("failed") ||
                output.Contains("timeout"))
                return false;

            // 如果輸出為空或無法判斷，視為失敗
            return false;
        }

        static string GetFailureReason(TestResult result)
        {
            if (!string.IsNullOrEmpty(result.Error))
                return result.Error;

            if (result.Output.Contains("Error:"))
            {
                var lines = result.Output.Split('\n');
                var errorLine = lines.FirstOrDefault(line => line.Contains("Error:"));
                return errorLine?.Trim() ?? "未知錯誤";
            }

            if (result.Output.Contains("❌"))
            {
                var lines = result.Output.Split('\n');
                var failLine = lines.FirstOrDefault(line => line.Contains("❌"));
                return failLine?.Trim() ?? "傳輸失敗";
            }

            return "未知原因";
        }

        static void ShowProgressStats()
        {
            var stats = CalculateStatistics();
            Console.WriteLine($"   📊 目前統計: 成功 {stats.SuccessCount}/{stats.TotalTests} ({stats.SuccessRate:F1}%), " +
                            $"失敗 {stats.FailureCount} ({stats.FailureRate:F1}%), " +
                            $"平均耗時 {stats.AverageDuration.TotalMilliseconds:F0}ms");
        }

        static TestStatistics CalculateStatistics()
        {
            var stats = new TestStatistics
            {
                TotalTests = TestResults.Count,
                SuccessCount = TestResults.Count(r => r.Success),
                FailureCount = TestResults.Count(r => !r.Success),
                TotalDuration = TimeSpan.FromTicks(TestResults.Sum(r => r.Duration.Ticks))
            };

            return stats;
        }

        static void ShowFinalResults(TimeSpan totalDuration)
        {
            var stats = CalculateStatistics();

            Console.WriteLine("📈 最終測試結果");
            Console.WriteLine("=====================================");
            Console.WriteLine($"📝 測試描述: {Config.Description}");
            Console.WriteLine($"📊 總計測試次數: {stats.TotalTests}");
            Console.WriteLine($"✅ 成功次數: {stats.SuccessCount} ({stats.SuccessRate:F2}%)");
            Console.WriteLine($"❌ 失敗次數: {stats.FailureCount} ({stats.FailureRate:F2}%)");
            Console.WriteLine($"⏱️  總測試時間: {totalDuration.TotalSeconds:F1} 秒");
            Console.WriteLine($"⚡ 平均每次測試: {stats.AverageDuration.TotalMilliseconds:F0}ms");
            Console.WriteLine($"🔥 測試頻率: {stats.TotalTests / totalDuration.TotalSeconds:F1} 次/秒");

            // 分析不同命令的成功率
            Console.WriteLine("\n📋 各命令成功率分析:");
            foreach (var command in Config.TestCommands)
            {
                var commandResults = TestResults.Where(r => r.Command == command).ToList();
                var successCount = commandResults.Count(r => r.Success);
                var successRate = commandResults.Count > 0 ? (double)successCount / commandResults.Count * 100 : 0;
                Console.WriteLine($"   {GetCommandDisplayName(command)}: {successCount}/{commandResults.Count} ({successRate:F1}%)");
            }

            // 失敗原因分析
            var failures = TestResults.Where(r => !r.Success).ToList();
            if (failures.Any())
            {
                Console.WriteLine("\n🔍 失敗原因分析:");
                var failureGroups = failures.GroupBy(f => GetFailureReason(f))
                                          .OrderByDescending(g => g.Count());

                foreach (var group in failureGroups)
                {
                    Console.WriteLine($"   {group.Key}: {group.Count()} 次");
                }
            }

            // 效能評估
            Console.WriteLine($"\n⚡ 效能評估:");
            if (stats.SuccessRate >= 95)
                Console.WriteLine("   🟢 優秀 - 通訊非常穩定");
            else if (stats.SuccessRate >= 90)
                Console.WriteLine("   🟡 良好 - 通訊基本穩定，偶有問題");
            else if (stats.SuccessRate >= 80)
                Console.WriteLine("   🟠 一般 - 通訊不夠穩定，需要檢查");
            else
                Console.WriteLine("   🔴 差 - 通訊很不穩定，需要修正");
        }

        static async Task SaveDetailedReport()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var reportFile = $"UartTestReport_{timestamp}.{Config.ReportFormat}";

            try
            {
                switch (Config.ReportFormat.ToLower())
                {
                    case "json":
                        await SaveJsonReport(reportFile);
                        break;
                    case "csv":
                        await SaveCsvReport(reportFile);
                        break;
                    default:
                        await SaveTextReport(reportFile);
                        break;
                }

                Console.WriteLine($"📄 詳細報告已儲存至: {reportFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 儲存報告失敗: {ex.Message}");
            }
        }

        static async Task SaveTextReport(string fileName)
        {
            using (var writer = new StreamWriter(fileName))
            {
                await writer.WriteLineAsync("UART 串列通訊測試詳細報告");
                await writer.WriteLineAsync("=====================================");
                await writer.WriteLineAsync($"測試時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                await writer.WriteLineAsync($"測試描述: {Config.Description}");
                await writer.WriteLineAsync($"目標程式: {Config.TargetProgram}");
                await writer.WriteLineAsync();

                var stats = CalculateStatistics();
                await writer.WriteLineAsync("測試統計:");
                await writer.WriteLineAsync($"  總計: {stats.TotalTests}");
                await writer.WriteLineAsync($"  成功: {stats.SuccessCount} ({stats.SuccessRate:F2}%)");
                await writer.WriteLineAsync($"  失敗: {stats.FailureCount} ({stats.FailureRate:F2}%)");
                await writer.WriteLineAsync($"  平均耗時: {stats.AverageDuration.TotalMilliseconds:F0}ms");
                await writer.WriteLineAsync();

                await writer.WriteLineAsync("測試設定:");
                await writer.WriteLineAsync($"  測試次數: {Config.TestCount}");
                await writer.WriteLineAsync($"  測試間隔: {Config.DelayMs}ms");
                await writer.WriteLineAsync($"  命令逾時: {Config.CommandTimeoutMs}ms");
                await writer.WriteLineAsync();

                await writer.WriteLineAsync("詳細測試記錄:");
                await writer.WriteLineAsync("時間\t\t測試#\t命令\t\t結果\t耗時\t說明");
                await writer.WriteLineAsync("".PadRight(80, '-'));

                foreach (var result in TestResults)
                {
                    var status = result.Success ? "✅" : "❌";
                    var reason = result.Success ? "成功" : GetFailureReason(result);
                    await writer.WriteLineAsync($"{result.Timestamp:HH:mm:ss}\t{result.TestNumber:D3}\t{GetCommandDisplayName(result.Command)}\t{status}\t{result.Duration.TotalMilliseconds:F0}ms\t{reason}");
                }
            }
        }

        static async Task SaveJsonReport(string fileName)
        {
            var report = new
            {
                TestInfo = new
                {
                    Timestamp = DateTime.Now,
                    Description = Config.Description,
                    TargetProgram = Config.TargetProgram,
                    Configuration = Config
                },
                Statistics = CalculateStatistics(),
                Results = TestResults
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonString = JsonSerializer.Serialize(report, options);
            await File.WriteAllTextAsync(fileName, jsonString);
        }

        static async Task SaveCsvReport(string fileName)
        {
            using (var writer = new StreamWriter(fileName))
            {
                // CSV 標頭
                await writer.WriteLineAsync("TestNumber,Timestamp,Command,Success,Duration_ms,Error");

                // CSV 資料
                foreach (var result in TestResults)
                {
                    var csvLine = $"{result.TestNumber}," +
                                 $"{result.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                                 $"\"{result.Command}\"," +
                                 $"{result.Success}," +
                                 $"{result.Duration.TotalMilliseconds:F0}," +
                                 $"\"{GetFailureReason(result)}\"";
                    await writer.WriteLineAsync(csvLine);
                }
            }
        }

        static async Task GenerateDefaultConfig(string fileName)
        {
            var defaultConfig = new UartTestConfig
            {
                TargetProgram = "UartApp.exe",
                TestCommands = new List<string>
                {
                    "trans 0x01050003FF007C3A COM3",
                    "trans 0x0105000300003DCA COM3"
                },
                TestCount = 50,
                DelayMs = 100,
                CommandTimeoutMs = 10000,
                EnableDetailedLog = true,
                ReportFormat = "txt",
                Description = "UART 串列通訊壓力測試",
                Metadata = new Dictionary<string, string>
                {
                    ["version"] = "2.0",
                    ["author"] = "UART Test Tool",
                    ["created"] = DateTime.Now.ToString("yyyy-MM-dd")
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonString = JsonSerializer.Serialize(defaultConfig, options);
            await File.WriteAllTextAsync(fileName, jsonString);

            Console.WriteLine($"✅ 預設設定檔已產生: {fileName}");
            Console.WriteLine("💡 您可以編輯此檔案來自訂測試設定");
        }

        static void ShowHelp()
        {
            Console.WriteLine("UART 串列通訊壓力測試程式 v2.0");
            Console.WriteLine("=====================================");
            Console.WriteLine();
            Console.WriteLine("使用方式:");
            Console.WriteLine("  UartStressTester.exe [選項]");
            Console.WriteLine();
            Console.WriteLine("選項:");
            Console.WriteLine("  <config.json>           使用指定的 JSON 設定檔");
            Console.WriteLine("  -g, --generate-config   產生預設設定檔");
            Console.WriteLine("  -h, --help             顯示此說明");
            Console.WriteLine();
            Console.WriteLine("向後相容 (舊版格式):");
            Console.WriteLine("  UartStressTester.exe <測試次數> <間隔ms> [程式路徑]");
            Console.WriteLine();
            Console.WriteLine("範例:");
            Console.WriteLine("  UartStressTester.exe                        # 使用預設設定");
            Console.WriteLine("  UartStressTester.exe my-config.json         # 使用自訂設定檔");
            Console.WriteLine("  UartStressTester.exe -g                     # 產生預設設定檔");
            Console.WriteLine("  UartStressTester.exe -g my-config.json      # 產生指定名稱的設定檔");
            Console.WriteLine("  UartStressTester.exe 100 200                # 舊版格式：100次測試，間隔200ms");
        }
    }
}