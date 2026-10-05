using System.ComponentModel;
using Finance.Application.Infrastructure.AppLock;

namespace Finance.App.Controls;

/// <summary>
/// Набор ПИН-кода: точки, ответ и клавиатура цифр. Модель — <see cref="PinPadViewModel"/>
/// в <see cref="BindableObject.BindingContext"/>.
/// </summary>
/// <remarks>
/// Проверку набранного запускает сам контрол, когда набрана последняя цифра,
/// и сам ведёт отсчёт паузы: и заслонке, и экрану ПИН-кода это нужно одинаково.
/// </remarks>
public sealed partial class PinPad : ContentView
{
    private PinPadViewModel? _model;
    private bool _ticking;

    /// <summary>
    /// Создаёт набор ПИН-кода.
    /// </summary>
    public PinPad()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Запускает отсчёт паузы, если она идёт. Зовётся при показе: пауза могла начаться
    /// в прошлый показ и продолжаться после перезапуска.
    /// </summary>
    public void Resume()
    {
        if (_model?.Tick() is true)
        {
            StartTicking();
        }
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_model is not null)
        {
            _model.Rejected -= OnRejected;
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = BindingContext as PinPadViewModel;

        if (_model is not null)
        {
            _model.Rejected += OnRejected;
            _model.PropertyChanged += OnModelChanged;
        }
    }

    private void OnDigit(object? sender, EventArgs e)
    {
        if (_model is not { } model || sender is not Button { Text: [char digit] })
        {
            return;
        }

        model.Type(digit);

        if (model.IsFull)
        {
            Guarded.Run(() => model.SubmitAsync());
        }
    }

    private void OnErase(object? sender, TappedEventArgs e) => _model?.Erase();

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PinPadViewModel.IsPaused) && _model is { IsPaused: true })
        {
            StartTicking();
        }
    }

    private void StartTicking()
    {
        if (_ticking)
        {
            return;
        }

        _ticking = true;

        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            _ticking = _model?.Tick() is true;

            return _ticking;
        });
    }

    /// <summary>
    /// Неверный код — точки качаются из стороны в сторону, как у ПИН-кода телефона.
    /// </summary>
    private void OnRejected(object? sender, EventArgs e) => Guarded.Run(ShakeAsync);

    private async Task ShakeAsync()
    {
        foreach (double offset in (double[])[-12, 12, -8, 8, -4, 0])
        {
            await Dots.TranslateToAsync(offset, 0, 40);
        }
    }
}
