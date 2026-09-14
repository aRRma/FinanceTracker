using System.Windows.Input;

namespace Finance.App.Controls;

/// <summary>
/// Клавиатура суммы: цифры, четыре действия, стирание и сохранение. Что делает
/// нажатая клавиша, решает модель представления — контрол только называет знак.
/// </summary>
public partial class AmountKeypad : ContentView
{
    /// <summary>Команда нажатия клавиши. Знак клавиши приходит параметром.</summary>
    public static readonly BindableProperty KeyCommandProperty =
        BindableProperty.Create(nameof(KeyCommand), typeof(ICommand), typeof(AmountKeypad));

    /// <summary>Команда стирания последнего знака.</summary>
    public static readonly BindableProperty BackspaceCommandProperty =
        BindableProperty.Create(nameof(BackspaceCommand), typeof(ICommand), typeof(AmountKeypad));

    /// <summary>Сохранять есть что: иначе клавиша сохранения гаснет.</summary>
    public static readonly BindableProperty CanSaveProperty =
        BindableProperty.Create(nameof(CanSave), typeof(bool), typeof(AmountKeypad), defaultValue: false);

    /// <summary>Клавиши на виду. Прячутся отдельно от клавиши сохранения.</summary>
    public static readonly BindableProperty AreKeysVisibleProperty =
        BindableProperty.Create(nameof(AreKeysVisible), typeof(bool), typeof(AmountKeypad), defaultValue: true);

    /// <summary>Создаёт клавиатуру.</summary>
    public AmountKeypad()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Нажата клавиша сохранения. Событием, а не командой: сохранение закрывает
    /// экран, а навигация живёт на странице, не в модели представления.
    /// </summary>
    public event EventHandler? Saved;

    /// <summary>Команда нажатия клавиши.</summary>
    public ICommand? KeyCommand
    {
        get => (ICommand?)GetValue(KeyCommandProperty);
        set => SetValue(KeyCommandProperty, value);
    }

    /// <summary>Команда стирания.</summary>
    public ICommand? BackspaceCommand
    {
        get => (ICommand?)GetValue(BackspaceCommandProperty);
        set => SetValue(BackspaceCommandProperty, value);
    }

    /// <summary>Сохранять есть что.</summary>
    public bool CanSave
    {
        get => (bool)GetValue(CanSaveProperty);
        set => SetValue(CanSaveProperty, value);
    }

    /// <summary>
    /// Клавиши на виду. Скрывается только сетка клавиш: спрятать контрол целиком
    /// значило бы унести с экрана и сохранение — записанную операцию нечем было бы закончить.
    /// </summary>
    public bool AreKeysVisible
    {
        get => (bool)GetValue(AreKeysVisibleProperty);
        set => SetValue(AreKeysVisibleProperty, value);
    }

    /// <summary>
    /// Знак клавиши берётся из её же подписи: второй список знаков в коде
    /// разошёлся бы с разметкой при первой же перестановке клавиш.
    /// </summary>
    private void OnKey(object? sender, EventArgs e)
    {
        if (sender is Button { Text: { Length: 1 } key } && KeyCommand is { } command && command.CanExecute(key))
        {
            command.Execute(key);
        }
    }

    private void OnBackspace(object? sender, TappedEventArgs e)
    {
        if (BackspaceCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnSave(object? sender, EventArgs e) => Saved?.Invoke(this, EventArgs.Empty);
}
