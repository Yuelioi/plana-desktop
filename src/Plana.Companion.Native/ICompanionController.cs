using Plana.Core.Companion;

namespace Plana.Companion.Native;

internal interface ICompanionController : ICompanionSurface
{
    void Close();
    void RunMessageLoop();
}
