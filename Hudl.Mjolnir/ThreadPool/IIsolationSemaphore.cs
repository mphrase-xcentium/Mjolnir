using System;
namespace Hudl.Mjolnir.ThreadPool
{
    internal interface IIsolationSemaphore : IDisposable
    {
        bool TryEnter();
        void Release();
    }
}
