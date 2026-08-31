namespace Plana.Core.Companion;

public sealed record CompanionSurfaceState(
    double? Left,
    double? Top,
    double Width,
    double Height,
    double Scale,
    bool AlwaysOnTop);

public interface ICompanionSurface : IDisposable
{
    nint WindowHandle { get; }
    void Show();
    void Hide();
    void Apply(CompanionSurfaceState state);
    CompanionSurfaceState Snapshot();
}
