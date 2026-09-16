using System.Windows.Input;
using Finance.Application.Infrastructure;

namespace Finance.App.Controls;

/// <summary>
/// Клавиатура суммы: цифры, четыре действия, стирание и «=». Что делает нажатая
/// клавиша, решает модель представления — контрол только называет знак.
/// </summary>
public partial class AmountKeypad : ContentView
{
    /// <summary>Команда нажатия клавиши. Знак клавиши приходит параметром.</summary>
    public static readonly BindableProperty KeyCommandProperty =
        BindableProperty.Create(nameof(KeyCommand), typeof(ICommand), typeof(AmountKeypad));

    /// <summary>Команда стирания последнего знака.</summary>
    public static readonly BindableProperty BackspaceCommandProperty =
        BindableProperty.Create(nameof(BackspaceCommand), typeof(ICommand), typeof(AmountKeypad));

    /// <summary>Команда клавиши «=»: свернуть набранное выражение в итог.</summary>
    public static readonly BindableProperty EqualsCommandProperty =
        BindableProperty.Create(nameof(EqualsCommand), typeof(ICommand), typeof(AmountKeypad));

    /// <summary>Создаёт клавиатуру.</summary>
    public AmountKeypad()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Подпись клавиши разделителя. Берётся из тех же правил набора, что и разбор
    /// нажатия: разойдись подпись со знаком — клавиша молча перестала бы приниматься.
    /// </summary>
    public static string Separator { get; } = AmountInput.Separator.ToString();

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

    /// <summary>Команда клавиши «=».</summary>
    public ICommand? EqualsCommand
    {
        get => (ICommand?)GetValue(EqualsCommandProperty);
        set => SetValue(EqualsCommandProperty, value);
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

    private void OnBackspace(object? sender, TappedEventArgs e) => Run(BackspaceCommand);

    private void OnEquals(object? sender, EventArgs e) => Run(EqualsCommand);

    private static void Run(ICommand? command)
    {
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
