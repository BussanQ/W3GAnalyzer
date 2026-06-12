using System.Runtime.InteropServices;
using W3GAnalyzer.Core;
using W3GAnalyzer.Forms;

namespace W3GAnalyzer;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    [STAThread]
    private static int Main(string[] args)
    {
        TextDecoder.EnsureProviderRegistered();

        // CLI 模式：--json / --text <input.w3g> [output]
        if (args.Length >= 1 &&
            (args[0].Equals("--json", StringComparison.OrdinalIgnoreCase) ||
             args[0].Equals("--text", StringComparison.OrdinalIgnoreCase)))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            return RunCli(args);
        }

        ApplicationConfiguration.Initialize();
        string? initial = args.Length >= 1 && File.Exists(args[0]) ? args[0] : null;
        Application.Run(new MainForm(initial));
        return 0;
    }

    private static int RunCli(string[] args)
    {
        bool json = args[0].Equals("--json", StringComparison.OrdinalIgnoreCase);
        if (args.Length < 2)
        {
            Console.Error.WriteLine("用法: W3GAnalyzer --json|--text <input.w3g> [output]");
            return 2;
        }

        string input = args[1];
        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"文件不存在: {input}");
            return 2;
        }

        try
        {
            var summary = ReplayParser.Parse(input);
            string content = json ? Exporter.ToJson(summary) : Exporter.ToText(summary);

            string output = args.Length >= 3
                ? args[2]
                : Path.ChangeExtension(input, json ? ".json" : ".txt");

            string? dir = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(output, content, new System.Text.UTF8Encoding(false));

            Console.WriteLine($"OK  {summary.Header.VersionDisplay}  " +
                              $"{summary.DurationText}  玩家 {summary.Players.Count}  " +
                              $"聊天 {summary.Chat.Count}  警告 {summary.Warnings.Count}  -> {output}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL  {input}  :  {ex.Message}");
            return 1;
        }
    }
}
