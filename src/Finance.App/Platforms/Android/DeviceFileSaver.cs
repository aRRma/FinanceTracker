using Android.App;
using Android.Content;

namespace Finance.App;

/// <summary>
/// Сохранение файла в папку на телефоне через системное окно выбора места
/// (<c>ACTION_CREATE_DOCUMENT</c>). Готового средства в MAUI нет: «Поделиться»
/// отдаёт файл приложениям, а положить его в «Загрузки» не умеет.
/// </summary>
internal static class DeviceFileSaver
{
    // Номер запроса, по которому ответ окна выбора отличают от чужих
    private const int RequestCode = 4207;

    private static TaskCompletionSource<Android.Net.Uri?>? _pending;

    /// <summary>
    /// Предлагает место и имя и копирует туда файл.
    /// </summary>
    /// <param name="source">Полный путь к сохраняемому файлу.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><see langword="true"/>, если сохранено; <see langword="false"/>, если пользователь передумал.</returns>
    internal static async Task<bool> SaveAsync(string source, CancellationToken cancellationToken = default)
    {
        Activity activity = Platform.CurrentActivity ?? throw new InvalidOperationException(AppFaults.NoActivity());

        Intent intent = new(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/octet-stream");
        intent.PutExtra(Intent.ExtraTitle, Path.GetFileName(source));

        TaskCompletionSource<Android.Net.Uri?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending;

        try
        {
            // Через тип Activity, а не активности AndroidX: там тот же вызов помечен
            // устаревшим, и предупреждение уронило бы сборку
            activity.StartActivityForResult(intent, RequestCode);
        }
        catch (ActivityNotFoundException)
        {
            // Окно не открылось — ответа не будет, и ожидание осталось бы висеть
            _pending = null;
            throw;
        }

        if (await pending.Task.WaitAsync(cancellationToken) is not { } target)
        {
            return false;
        }

        // «wt» — с усечением: при перезаписи существующего файла хвост прежнего не остаётся
        await using Stream output = activity.ContentResolver?.OpenOutputStream(target, "wt")
            ?? throw new IOException(AppFaults.FileNotWritable());
        await using FileStream input = File.OpenRead(source);

        await input.CopyToAsync(output, cancellationToken);

        return true;
    }

    /// <summary>
    /// Принимает ответ окна выбора. Зовётся из событий жизненного цикла
    /// активности для всех ответов; чужие пропускает.
    /// </summary>
    /// <param name="requestCode">Номер запроса.</param>
    /// <param name="resultCode">Итог окна.</param>
    /// <param name="data">Выбранное место.</param>
    internal static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode)
        {
            return;
        }

        TaskCompletionSource<Android.Net.Uri?>? pending = _pending;
        _pending = null;

        pending?.TrySetResult(resultCode is Result.Ok ? data?.Data : null);
    }
}
