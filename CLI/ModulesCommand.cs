using Ripple.Services;

namespace Ripple.Cli;

public static class ModulesCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var modules = AppServices.Modules;
        var action = args.Length > 0 ? args[0].ToLowerInvariant() : "list";

        switch (action)
        {
            case "list":
                await modules.RefreshAsync();
                return List();

            case "install":
                return args.Length < 2 ? MissingId("install") : await Install(modules, args[1]);

            case "remove":
                return args.Length < 2 ? MissingId("remove") : Remove(modules, args[1]);

            default:
                CliHelper.Error($"Неизвестное действие «{action}». Доступно: list, install, remove.");
                return 1;
        }
    }

    private static int List()
    {
        CliHelper.Info($"{"ID",-16} {"Состояние",-12} Размер      Имя");
        CliHelper.Info(new string('-', 60));

        foreach (var m in AppServices.Modules.Modules)
        {
            CliHelper.Info($"{m.Id,-16} {StateText(m),-12} {m.SizeText,-11} {m.Name}");
        }

        CliHelper.Info("");
        CliHelper.Info("Установка: ripple modules install <id>    Удаление: ripple modules remove <id>");

        return 0;
    }

    private static async Task<int> Install(ModuleService modules, string id)
    {
        var info = modules.Modules.FirstOrDefault(m => m.Id == id);

        if (info is null)
        {
            return Unknown(id);
        }

        Console.CancelKeyPress += (_, e) => { e.Cancel = true; modules.Cancel(id); };

        void OnChanged() =>
            CliHelper.Progress($"[{StateText(info)}] {info.StatusDetail}" + ProgressSuffix(info));

        modules.Changed += OnChanged;
        var started = false;

        try
        {
            started = modules.Install(id);
        }

        finally
        {
            if (started) modules.Changed -= OnChanged;
        }

        if (!started)
        {
            CliHelper.Error("Модуль уже устанавливается.");
            return 1;
        }

        while (info.State == ModuleState.Installing)
        {
            await Task.Delay(200);
        }

        Console.CancelKeyPress -= null;
        CliHelper.ProgressDone();

        if (info.State == ModuleState.Ready)
        {
            CliHelper.Ok($"{info.Name}: установлено → {info.StatusDetail}");

            return 0;
        }

        CliHelper.Error($"{info.Name}: {info.StatusDetail}");

        return 1;
    }

    private static int Remove(ModuleService modules, string id)
    {
        var info = modules.Modules.FirstOrDefault(m => m.Id == id);
        if (info is null)
        {
            return Unknown(id);
        }

        if (!info.CanRemove)
        {
            CliHelper.Error($"{info.Name} не управляется приложением (установлен вручную или вне папки модулей).");

            return 1;
        }

        modules.Remove(id);
        CliHelper.Ok($"{info.Name}: удалено.");

        return 0;
    }

    private static string ProgressSuffix(ModuleInfo m) =>
        m.Progress is null ? "" : $" {m.Progress,6:P0}";

    private static string StateText(ModuleInfo m) => m.State switch
    {
        ModuleState.Ready => "готово",
        ModuleState.Installing => "установка",
        ModuleState.Failed => "ошибка",
        _ => "нет"
    };

    private static int MissingId(string action)
    {
        CliHelper.Error($"Нужно: ripple modules {action} <id>");

        return 1;
    }

    private static int Unknown(string id)
    {
        CliHelper.Error($"Неизвестный модуль «{id}». Список: ripple modules list");

        return 1;
    }
}
