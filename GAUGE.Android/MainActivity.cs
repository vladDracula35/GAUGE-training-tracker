using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using Com.Airbnb.Lottie;

namespace GAUGE.Android;

[Activity(
    Label = "GAUGE",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private FrameLayout? _splashOverlay;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        RequestWindowFeature(WindowFeatures.NoTitle);
        SupportActionBar?.Hide();
        base.OnCreate(savedInstanceState);
    }

    public override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        ShowSplashSafely();
    }

    private void ShowSplashSafely()
    {
        if (_splashOverlay != null) return;

        var decorView = Window?.DecorView as ViewGroup;
        if (decorView == null) return;

        try
        {
            _splashOverlay = new FrameLayout(this)
            {
                LayoutParameters = new ViewGroup.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.MatchParent)
            };
            _splashOverlay.SetBackgroundColor(global::Android.Graphics.Color.ParseColor("#222222"));

            var lottieView = new LottieAnimationView(this)
            {
                LayoutParameters = new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.MatchParent)
                {
                    Gravity = GravityFlags.Center
                }
            };

            lottieView.ImageAssetsFolder = "images/";
            lottieView.SetImageAssetDelegate(new SafeImageDelegate());
            lottieView.SetAnimation("gauge_splash.json");
            lottieView.RepeatCount = 0;

            // Запасний таймаут на 3.5 сек, якщо анімація зависне
            var handler = new Handler(Looper.MainLooper!);
            handler.PostDelayed(() => DismissSplash(decorView), 3500);

            // Коли векторна анімація добігає кінця — плавно гасимо оверлей
            lottieView.AddAnimatorListener(new SplashEndListener(() =>
            {
                RunOnUiThread(() => DismissSplash(decorView));
            }));

            _splashOverlay.AddView(lottieView);
            decorView.AddView(_splashOverlay);

            lottieView.PlayAnimation();
        }
        catch (System.Exception)
        {
            DismissSplash(decorView);
        }
    }

    private void DismissSplash(ViewGroup decorView)
    {
        if (_splashOverlay == null) return;

        _splashOverlay.Animate()
            ?.Alpha(0f)
            ?.SetDuration(300)
            ?.WithEndAction(new Java.Lang.Runnable(() =>
            {
                try
                {
                    decorView.RemoveView(_splashOverlay);
                    _splashOverlay = null;
                }
                catch { }
            }));
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder);
    }
}

public class SafeImageDelegate : Java.Lang.Object, IImageAssetDelegate
{
    public Bitmap? FetchBitmap(LottieImageAsset? asset)
    {
        return Bitmap.CreateBitmap(1, 1, Bitmap.Config.Argb8888!);
    }
}

public class SplashEndListener : Java.Lang.Object, global::Android.Animation.Animator.IAnimatorListener
{
    private readonly System.Action _onEnd;
    public SplashEndListener(System.Action onEnd) => _onEnd = onEnd;

    public void OnAnimationEnd(global::Android.Animation.Animator? animation) => _onEnd();
    public void OnAnimationCancel(global::Android.Animation.Animator? animation) => _onEnd();
    public void OnAnimationRepeat(global::Android.Animation.Animator? animation) { }
    public void OnAnimationStart(global::Android.Animation.Animator? animation) { }
}