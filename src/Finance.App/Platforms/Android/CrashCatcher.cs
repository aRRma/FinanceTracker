using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Finance.Application.Infrastructure.Diagnostics;
using DeviceInfo = Finance.Application.Infrastructure.Diagnostics.DeviceInfo;

namespace Finance.App;

/// <summary>
/// Перехват сбоев: падения .NET и Java и забытые задачи пишутся в отчёты о сбоях. Подключается в
/// <see cref="MainApplication"/> раньше сборки служб — иначе сбой при старте не попал бы никуда.
/// </summary>
/// <remarks>
/// Отчёты создаются здесь и уходят в контейнер служб готовыми: запись из обработчиков и экран отчётов
/// делят один файл и одну блокировку. Нативное падение и зависание отсюда не ловятся — о них при
/// следующем запуске рассказывает система.
/// </remarks>
internal static class CrashCatcher
{
    /// <summary>
    /// Отправка сводки уже стоит в очереди.
    /// </summary>
    private static int _summaryQueued;

    /// <summary>
    /// Отчёты о сбоях. Пусто только до подключения перехвата.
    /// </summary>
    internal static CrashReports? Reports { get; private set; }

    /// <summary>
    /// Создаёт отчёты и подключает перехват. Повторный вызов ничего не делает.
    /// </summary>
    /// <param name="context">Приложение.</param>
    internal static void Install(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Reports is not null)
        {
            return;
        }

        // Та же папка, что FileSystem.AppDataDirectory у MAUI, — но MAUI здесь ещё не поднят.
        // Папки нет — перехвата не будет, но и запуск он не уронит
        if (context.FilesDir?.AbsolutePath is not { } folder)
        {
            return;
        }

        Reports = new CrashReports(folder, TimeProvider.System, Describe(context));

        // Сводка — экран и последнее действие — уходит системе заранее: после нативного падения
        // .NET уже мёртв, а система отдаст её назад в записи о завершении при следующем запуске
        if (context.GetSystemService(Context.ActivityService) is ActivityManager activities)
        {
            ActionTrail trail = Reports.Trail;
            trail.Changed += () => Summarize(activities, trail);
        }

        Reports.Trail.Lifecycle(LifecycleEvent.Started);

        // Исключение .NET, уходящее в Java: главный путь падения в MAUI на Android. Признак
        // «обработано» не ставится — процесс закрывается, как и закрылся бы без перехвата
        AndroidEnvironment.UnhandledExceptionRaiser += static (_, e) => Reports?.WriteFatal(CrashKind.Unhandled, e.Exception);

        // Исключение в потоке и в таймере: кроме этого обработчика, его не видит никто
        AppDomain.CurrentDomain.UnhandledException += static (_, e) =>
        {
            if (e.ExceptionObject is Exception error)
            {
                Reports?.WriteFatal(CrashKind.Unhandled, error);
            }
        };

        // Забытая задача процесс не роняет и приходит после сборки мусора
        TaskScheduler.UnobservedTaskException += static (_, e) => Reports?.Write(CrashKind.Warning, e.Exception);

        // Исключение внутри Java. Прежний обработчик — среды .NET — получает его дальше
        // и закрывает процесс как обычно
        Java.Lang.Thread.DefaultUncaughtExceptionHandler = new JavaCrashHandler(Java.Lang.Thread.DefaultUncaughtExceptionHandler);
    }

    /// <summary>
    /// Разбирает записи системы о прошлых завершениях в фоне: запуск их не ждёт.
    /// </summary>
    /// <param name="exits">Разбор записей.</param>
    internal static void RecordPastExits(PastExits exits)
    {
        ArgumentNullException.ThrowIfNull(exits);

        _ = Task.Run(async () =>
        {
            try
            {
                await exits.RecordAsync(CancellationToken.None);
            }
            catch (Exception failure) when (failure is not System.OperationCanceledException)
            {
                // Разбор не удался — это предупреждение, а не повод ронять только что поднятое приложение
                Android.Util.Log.Warn("Finance", failure.ToString());
                Warn(failure);
            }
        });
    }

    /// <summary>
    /// Сбой, пойманный приложением: пользователю показано общее сообщение.
    /// </summary>
    /// <param name="error">Сбой.</param>
    internal static void Caught(Exception error) => Reports?.Write(CrashKind.Caught, error);

    /// <summary>
    /// Сбой, после которого приложение работает дальше, но что-то пошло не так.
    /// </summary>
    /// <param name="error">Сбой.</param>
    internal static void Warn(Exception error) => Reports?.Write(CrashKind.Warning, error);

    /// <summary>
    /// След действий. Пусто только до подключения перехвата.
    /// </summary>
    internal static ActionTrail? Trail => Reports?.Trail;

    /// <summary>
    /// Ставит отправку сводки системе в очередь пула потоков. Отправка — вызов в системный процесс, и на главном
    /// потоке она шла бы перед каждым обработчиком; частые события сливаются в одну отправку свежей сводки.
    /// </summary>
    private static void Summarize(ActivityManager activities, ActionTrail trail)
    {
        if (Interlocked.Exchange(ref _summaryQueued, 1) is not 0)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            // Признак снимается до чтения сводки: событие, пришедшее во время отправки, поставит новую
            Volatile.Write(ref _summaryQueued, 0);

            try
            {
                activities.SetProcessStateSummary(trail.Summary());
            }
            // Поток пула: исключение отсюда уронило бы процесс, а сводка того не стоит
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                Android.Util.Log.Warn("Finance", failure.ToString());
            }
        });
    }

    /// <summary>
    /// Сборка и устройство. Не узнали — отчёты пишутся без них: перехват, который сам роняет запуск,
    /// хуже перехвата без версии.
    /// </summary>
    private static DeviceInfo Describe(Context context)
    {
        try
        {
            return DescribeOrThrow(context);
        }
        catch (Java.Lang.Exception failure)
        {
            Android.Util.Log.Warn("Finance", failure.ToString());

            return DeviceInfo.Unknown;
        }
    }

    private static DeviceInfo DescribeOrThrow(Context context)
    {
        PackageInfo? package = context.PackageManager?.GetPackageInfo(context.PackageName!, PackageManager.PackageInfoFlags.Of(0L));

        return new DeviceInfo
        {
            AppVersion = package?.VersionName ?? "?",
            AppBuild = (package?.LongVersionCode ?? 0).ToString(CultureInfo.InvariantCulture),
            Android = Build.VERSION.Release ?? "?",
            Model = string.Create(CultureInfo.InvariantCulture, $"{Build.Manufacturer} {Build.Model}"),
        };
    }
}
