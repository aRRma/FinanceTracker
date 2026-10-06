using System.ComponentModel;
using Finance.Application.Infrastructure.AppLock;
using Microsoft.Maui.Controls.Shapes;

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
    /// Задержка появления каждой следующей диагонали клавиш, мс.
    /// </summary>
    private const int EntranceStep = 45;

    /// <summary>
    /// Расстояние между центрами соседних точек: точка 16 и промежуток 20 из разметки.
    /// </summary>
    private const double DotPitch = 36;

    /// <summary>
    /// Задержка прыжка каждой следующей точки в волне.
    /// </summary>
    private static readonly TimeSpan WaveStep = TimeSpan.FromMilliseconds(55);

    /// <summary>
    /// Качание ряда на неверный код — затухающее, как у ПИН-кода телефона.
    /// </summary>
    private static readonly double[] ShakeOffsets = [-12, 10, -7, 4, -2, 0];

    private readonly VisualElement[] _keys;
    private readonly Dictionary<VisualElement, Ellipse> _rings = [];

    private PinPadViewModel? _model;
    private bool _ticking;
    private bool _waving;
    private bool _entering;

    /// <summary>
    /// Создаёт набор ПИН-кода.
    /// </summary>
    public PinPad()
    {
        InitializeComponent();

        _keys = [.. Keys.Children.OfType<VisualElement>()];
        AddRings();

        Loaded += OnLoaded;
    }

    /// <summary>
    /// Набор появится с анимацией: замок опускается, клавиши выпрыгивают волной
    /// по диагонали. Зовётся до показа: прячет набор и начинает волну, как только
    /// он окажется в окне.
    /// </summary>
    public void Enter()
    {
        if (!Motion.IsOn)
        {
            return;
        }

        if (IsLoaded)
        {
            PrepareEntrance();
            Guarded.Run(EnterAsync);
        }
        else
        {
            _entering = true;
        }
    }

    /// <summary>
    /// Код принят: дужка замка откидывается, точки сходятся в одну цветом дохода.
    /// Владелец ждёт этого и только потом убирает набор.
    /// </summary>
    public async Task OpenAsync()
    {
        LockIcon.Key = "lock-open";

        // Ряд цвета дохода проступает поверх точек и сходится к середине
        Dots.Opacity = 0;
        Right.Opacity = 1;

        List<Task> moves = [Pulse(Lock)];
        int count = Right.Children.Count;

        for (int index = 0; index < count; index++)
        {
            if (Right.Children[index] is VisualElement dot)
            {
                // К середине ряда: крайние проходят полтора шага, внутренние — половину
                double offset = (((count - 1) / 2.0) - index) * DotPitch;

                moves.Add(dot.TranslateToAsync(offset, 0, 280, Easing.CubicInOut));
                moves.Add(dot.ScaleToAsync(1.4, 280, Easing.CubicInOut));
            }
        }

        await Task.WhenAll(moves);

        static async Task Pulse(VisualElement view)
        {
            await view.ScaleToAsync(1.15, 120, Easing.CubicOut);

            if (Attached(view))
            {
                await view.ScaleToAsync(1, 260, Easing.SpringOut);
            }
        }
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

    /// <summary>
    /// Вид ещё в окне. Заслонку пересобирают посреди анимации (сменилась тема), а у
    /// отключённого вида анимациям не найти часов: следующий шаг бросил бы исключение,
    /// и пользователь увидел бы сообщение о сбое вместо новой заслонки.
    /// </summary>
    private static bool Attached(VisualElement view) => view.Handler is not null;

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_entering)
        {
            _entering = false;
            PrepareEntrance();
            Guarded.Run(EnterAsync);
        }
    }

    /// <summary>
    /// Исходное положение волны появления. Ставится только у вида, уже попавшего
    /// в окно: у клавиш с тенью MAUI оборачивает вид в контейнер, и масштаб,
    /// заданный до обёртки, оставался на самой клавише навсегда — анимация двигала
    /// уже контейнер, и клавиши застывали в половину размера. Попадание в окно
    /// приходит раньше первого кадра, и набор не мелькает.
    /// </summary>
    private void PrepareEntrance()
    {
        Lock.Opacity = 0;
        Lock.TranslationY = -14;
        Lock.Scale = 0.8;
        Title.Opacity = 0;
        DotRow.Opacity = 0;

        foreach (VisualElement key in _keys)
        {
            key.Opacity = 0;
            key.Scale = 0.55;
        }
    }

    private async Task EnterAsync()
    {
        List<Task> steps =
        [
            Lock.FadeToAsync(1, 250),
            Lock.TranslateToAsync(0, 0, 450, Easing.SpringOut),
            Lock.ScaleToAsync(1, 450, Easing.SpringOut),
            Appear(Title, 80),
            Appear(DotRow, 80)
        ];

        foreach (VisualElement key in _keys)
        {
            steps.Add(PopIn(key, ((Grid.GetRow(key) + Grid.GetColumn(key)) * EntranceStep) + 90));
        }

        await Task.WhenAll(steps);

        static async Task Appear(VisualElement view, int delay)
        {
            await Task.Delay(delay);

            if (Attached(view))
            {
                await view.FadeToAsync(1, 400);
            }
        }

        static async Task PopIn(VisualElement key, int delay)
        {
            await Task.Delay(delay);

            if (Attached(key))
            {
                await Task.WhenAll(key.FadeToAsync(1, 200), key.ScaleToAsync(1, 420, Easing.SpringOut));
            }
        }
    }

    /// <summary>
    /// Кольца нажатия — по одному под каждой клавишей, невидимые до касания.
    /// Заводятся заранее, а не на каждое касание: масштаб вида, добавленного
    /// и сразу увеличенного, MAUI считает от левого верхнего угла — размера
    /// у вида ещё нет, и кольцо расходилось из угла клавиши, а не из её середины.
    /// </summary>
    private void AddRings()
    {
        foreach (VisualElement key in _keys)
        {
            // Первым ребёнком сетки: кольцо рисуется под клавишами
            Ellipse ring = new() { Style = PinRing, InputTransparent = true, Opacity = 0 };

            Grid.SetRow(ring, Grid.GetRow(key));
            Grid.SetColumn(ring, Grid.GetColumn(key));
            Keys.Children.Insert(0, ring);

            _rings[key] = ring;
        }
    }

    /// <summary>
    /// Палец лёг на клавишу: она проседает, и от неё расходится кольцо.
    /// </summary>
    private void OnKeyDown(object? sender, EventArgs e)
    {
        // Во время волны цифра не принимается: проседание и кольцо обещали бы обратное
        if (!_waving && sender is VisualElement key && Attached(key))
        {
            Guarded.Run(() => key.ScaleToAsync(0.9, 70, Easing.CubicOut));
            Ripple(key);
        }
    }

    /// <summary>
    /// Палец ушёл: клавиша возвращается с пружиной.
    /// </summary>
    private void OnKeyUp(object? sender, EventArgs e)
    {
        if (sender is VisualElement key && Attached(key))
        {
            Guarded.Run(() => key.ScaleToAsync(1, 320, Easing.SpringOut));
        }
    }

    /// <summary>
    /// Кольцо под клавишей расходится за её край и тает. Под ней, а не поверх:
    /// поверх оно закрыло бы цифру, а снаружи видно и под пальцем.
    /// </summary>
    private void Ripple(VisualElement key)
    {
        if (!Motion.IsOn || !_rings.TryGetValue(key, out Ellipse? ring))
        {
            return;
        }

        ring.AbortAnimation("ScaleTo");
        ring.AbortAnimation("FadeTo");
        ring.Scale = 0.3;
        ring.Opacity = 1;

        Guarded.Run(() => Task.WhenAll(
            ring.ScaleToAsync(1.65, 500, Easing.CubicOut),
            ring.FadeToAsync(0, 500, Easing.CubicOut)));
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
        if (_waving)
        {
            return;
        }

        // Касание стирания приходит целиком, без отдельного «палец лёг»: кольцо и
        // короткое проседание вместе
        if (sender is VisualElement key && Attached(key))
        {
            Ripple(key);
            Guarded.Run(async () =>
            {
                await key.ScaleToAsync(0.9, 70, Easing.CubicOut);
                await key.ScaleToAsync(1, 320, Easing.SpringOut);
            });
        }

        _model?.Erase();
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
        if (Motion.IsOn)
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

        if (!Attached(dot))
        {
            return;
        }

        await Task.WhenAll(
            dot.TranslateToAsync(0, -12, 110, Easing.CubicOut),
            dot.ScaleToAsync(1.35, 110, Easing.CubicOut));

        if (Attached(dot))
        {
            await Task.WhenAll(
                dot.TranslateToAsync(0, 0, 170, Easing.CubicInOut),
                dot.ScaleToAsync(1, 170, Easing.CubicInOut));
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PinPadViewModel.IsPaused) && _model is { IsPaused: true })
        {
            StartTicking();
        }

        // Ответ пришёл заново, а не отсчёт паузы сменил секунды
        if (e.PropertyName is nameof(PinPadViewModel.Message)
            && _model is { HasMessage: true, IsPaused: false }
            && Attached(Answer)
            && Motion.IsOn)
        {
            Guarded.Run(ShowAnswerAsync);
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
    /// Неверный код — точки краснеют, качаются из стороны в сторону, затухая, и гаснут.
    /// </summary>
    /// <remarks>
    /// Модель к этому моменту уже стёрла набранное, и точки под красным рядом пустые:
    /// он гаснет над ними после качания.
    /// </remarks>
    private void OnRejected(object? sender, EventArgs e) => Guarded.Run(ShakeAsync);

    private async Task ShakeAsync()
    {
        if (!Motion.IsOn || !Attached(DotRow))
        {
            return;
        }

        // Неверный код, набранный быстрее прошлого качания, начинает его заново:
        // два качания дрались бы за сдвиг ряда, и хвост первого гасил бы красный второго
        DotRow.AbortAnimation("TranslateTo");
        Wrong.AbortAnimation("FadeTo");
        Wrong.Opacity = 1;

        foreach (double offset in ShakeOffsets)
        {
            if (!Attached(DotRow))
            {
                return;
            }

            await DotRow.TranslateToAsync(offset, 0, 60, Easing.CubicOut);
        }

        await Wrong.FadeToAsync(0, 250);
    }

    /// <summary>
    /// Ответ появляется, выплывая сверху: заметно, что пришёл новый, даже если текст
    /// тот же, что был.
    /// </summary>
    private async Task ShowAnswerAsync()
    {
        Answer.Opacity = 0;
        Answer.TranslationY = -6;

        await Task.WhenAll(Answer.FadeToAsync(1, 220), Answer.TranslateToAsync(0, 0, 220, Easing.CubicOut));
    }

    /// <summary>
    /// Стиль кольца нажатия из ресурсов приложения: цвет по теме задаёт разметка стиля.
    /// </summary>
    private static Style? PinRing =>
        ControlsApplication.Current?.Resources.TryGetValue("PinRing", out object? value) is true ? value as Style : null;
}
