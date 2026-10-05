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
    /// <summary>
    /// Задержка прыжка каждой следующей точки в волне.
    /// </summary>
    private static readonly TimeSpan WaveStep = TimeSpan.FromMilliseconds(55);

    private PinPadViewModel? _model;
    private bool _ticking;
    private bool _waving;

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
        if (_waving || _model is not { } model || sender is not Button { Text: [char digit] })
        {
            return;
        }

        model.Type(digit);

        if (model.IsFull)
        {
            Guarded.Run(() => SubmitAsync(model));
        }
    }

    private void OnErase(object? sender, TappedEventArgs e)
    {
        if (!_waving)
        {
            _model?.Erase();
        }
    }

    /// <summary>
    /// Набрана последняя цифра: точки пробегают волной, и только потом код уходит
    /// на проверку. Не одновременно: верный код убирает заслонку, а ответ стирает
    /// набранное и пересоздаёт точки — волна обрывалась бы на середине.
    /// </summary>
    /// <remarks>
    /// С выключенными в системе анимациями волны нет: ждать проверку ради
    /// невидимых прыжков незачем. Проверку зовёт и набор, отключённый посреди
    /// волны, — пересобранная заслонка показывает те же четыре точки и ждёт ответа.
    /// </remarks>
    private async Task SubmitAsync(PinPadViewModel model)
    {
        if (Android.Animation.ValueAnimator.AreAnimatorsEnabled())
        {
            _waving = true;

            try
            {
                await WaveAsync();
            }
            finally
            {
                _waving = false;
            }
        }

        await model.SubmitAsync();
    }

    /// <summary>
    /// Точки по очереди подпрыгивают, подрастая, и опускаются обратно — волна слева направо.
    /// </summary>
    private async Task WaveAsync()
    {
        List<Task> jumps = [];

        foreach (IView child in Dots.Children)
        {
            if (child is VisualElement dot)
            {
                jumps.Add(JumpAsync(dot, WaveStep * jumps.Count));
            }
        }

        await Task.WhenAll(jumps);
    }

    private static async Task JumpAsync(VisualElement dot, TimeSpan delay)
    {
        await Task.Delay(delay);

        // Заслонку пересобрали посреди волны (сменилась тема): у отключённой
        // точки анимациям не найти часов, и запуск бросил бы исключение
        if (dot.Handler is null)
        {
            return;
        }

        await Task.WhenAll(
            dot.TranslateToAsync(0, -12, 110, Easing.CubicOut),
            dot.ScaleToAsync(1.35, 110, Easing.CubicOut));

        await Task.WhenAll(
            dot.TranslateToAsync(0, 0, 170, Easing.CubicInOut),
            dot.ScaleToAsync(1, 170, Easing.CubicInOut));
    }

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
