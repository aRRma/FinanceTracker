namespace Finance.App;

/// <summary>
/// Движение интерфейса: волны, кольца, дрейф атмосферы. Одно место решает, есть ли оно вообще.
/// </summary>
/// <remarks>
/// Выключенные в системе анимации — просьба человека, а не помеха: без них состояние
/// сразу конечное, и ждать невидимого движения незачем.
/// </remarks>
internal static class Motion
{
    /// <summary>
    /// Анимации включены в системе.
    /// </summary>
    public static bool IsOn => Android.Animation.ValueAnimator.AreAnimatorsEnabled();
}
