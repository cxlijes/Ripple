using Ripple.Cli;
using Ripple.Services;

namespace Ripple;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0)
        {
            CliHelper.PrintHelp();

            return 0;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "import":
                    return await ImportCommand.RunAsync(args[1..]);
                case "list":
                    return await LibraryCommand.ListAsync(args[1..]);
                case "show":
                    return await LibraryCommand.ShowAsync(args[1..]);
                case "retry":
                    return await LibraryCommand.RetryAsync(args[1..]);
                case "delete":
                    return await LibraryCommand.DeleteAsync(args[1..]);
                case "record":
                    return await RecordCommand.RunAsync(args[1..]);
                case "modules":
                    return await ModulesCommand.RunAsync(args[1..]);
                case "config":
                    return await ConfigCommand.RunAsync(args[1..]);
                case "export":
                    return await ExportCommand.RunAsync(args[1..]);
                case "status":
                    return await CliHelper.StatusAsync();
                case "help":
                case "--help":
                case "-h":
                case "/?":
                    CliHelper.PrintHelp();

                    return 0;

                default:
                    CliHelper.Error($"Неизвестная команда «{args[0]}».");
                    CliHelper.PrintHelp();

                    return 1;
            }
        }
        catch (OperationCanceledException)
        {
            CliHelper.Error("Отменено.");

            return 130;
        }
        catch (Exception ex)
        {
            AppServices.Log($"CLI FAIL: {args[0]}: {ex}");
            CliHelper.Error(ex.Message);

            return 1;
        }
    }
}
