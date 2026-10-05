using CommunityToolkit.Mvvm.ComponentModel;
using Finance.Domain.Errors;

namespace Finance.Application.Infrastructure;

/// <summary>
/// Модель представления формы, которая сохраняет или удаляет запись.
/// Наследование здесь осознанное: защита от второго нажатия и показ нарушенного
/// правила иначе повторялись бы в каждой форме, а копии расходятся.
/// </summary>
public abstract partial class FormViewModel : ObservableObject, IFormModel
{
    /// <summary>
    /// Текст нарушенного правила. Пусто — сохранять можно.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; protected set; }

    /// <summary>
    /// Правило нарушено — сообщение показывается рядом с формой.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Идёт сохранение или удаление. Кнопки зовут методы напрямую, без команды
    /// с её защитой от повторного запуска: без флага второе нажатие до ухода
    /// экрана записало бы то же самое дважды. Форма, проверяющая ввод до записи,
    /// спрашивает флаг первым: нажатие во время записи — не новая попытка.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool IsSaving { get; private set; }

    /// <summary>
    /// Предыдущее действие закончилось — можно начинать следующее. Отдельно
    /// от <see cref="CanSave"/>: к нему привязана и кнопка удаления, а «можно
    /// сохранить» о ней ничего не говорит.
    /// </summary>
    public bool IsIdle => !IsSaving;

    /// <summary>
    /// Сохранять можно: предыдущее действие не идёт. Форма, которой для
    /// сохранения нужно ещё что-то набранное, дополняет условие.
    /// </summary>
    public virtual bool CanSave => IsIdle;

    /// <inheritdoc />
    public abstract bool IsDirty { get; }

    /// <summary>
    /// Записывает сохранение или удаление под флагом <see cref="IsSaving"/>.
    /// Нарушенное доменное правило показывается текстом рядом с формой: это ввод
    /// пользователя, а не сбой, и падать приложению не за что.
    /// </summary>
    /// <param name="write">Запись: зов обработчика и то, что меняется после удачи.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns><c>true</c>, если запись прошла и экран можно закрыть.</returns>
    protected async Task<bool> WriteAsync(Func<CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        if (IsSaving)
        {
            return false;
        }

        IsSaving = true;
        bool done = false;
        Error = null;

        try
        {
            // ConfigureAwait(false) здесь недопустим: следом меняются привязанные
            // свойства, а их правка вне потока интерфейса роняет разметку
            await write(cancellationToken);

            done = true;

            return true;
        }
        catch (DomainException error)
        {
            Error = error.Message;

            return false;
        }
        finally
        {
            // После удачи флаг остаётся: экран закрывается, и второе нажатие
            // в этот промежуток записало бы то же самое ещё раз. При неудаче
            // он снимается — нарушенное правило правят и сохраняют снова
            IsSaving = done;
        }
    }
}
