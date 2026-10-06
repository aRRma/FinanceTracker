using Android.App;
using Android.Runtime;

namespace Finance.App;

[Application]
public sealed class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	/// <summary>
	/// Перехват сбоев — до сборки приложения: сбой в ней самой иначе не попал бы в отчёты.
	/// </summary>
	public override void OnCreate()
	{
		CrashCatcher.Install(this);

		base.OnCreate();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
