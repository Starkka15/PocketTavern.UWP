// Provides GetAwaiter / AsTask for WinRT async types without requiring
// System.Runtime.WindowsRuntime.dll (which NuGet 7 no longer injects for
// traditional UAP projects targeting .NETCore v5.0).
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;

namespace PocketTavern.UWP
{
    internal static class WinRTExtensions
    {
        public static TaskAwaiter<T> GetAwaiter<T>(this IAsyncOperation<T> op)
            => op.AsTask().GetAwaiter();

        public static TaskAwaiter GetAwaiter(this IAsyncAction op)
            => op.AsTask().GetAwaiter();

        public static Task<T> AsTask<T>(this IAsyncOperation<T> op)
        {
            var tcs = new TaskCompletionSource<T>();
            op.Completed = (asyncOp, status) =>
            {
                switch (status)
                {
                    case AsyncStatus.Completed:
                        tcs.TrySetResult(asyncOp.GetResults());
                        break;
                    case AsyncStatus.Error:
                        tcs.TrySetException(asyncOp.ErrorCode);
                        break;
                    case AsyncStatus.Canceled:
                        tcs.TrySetCanceled();
                        break;
                }
            };
            return tcs.Task;
        }

        public static Task AsTask(this IAsyncAction op)
        {
            var tcs = new TaskCompletionSource<bool>();
            op.Completed = (asyncOp, status) =>
            {
                switch (status)
                {
                    case AsyncStatus.Completed:
                        tcs.TrySetResult(true);
                        break;
                    case AsyncStatus.Error:
                        tcs.TrySetException(asyncOp.ErrorCode);
                        break;
                    case AsyncStatus.Canceled:
                        tcs.TrySetCanceled();
                        break;
                }
            };
            return tcs.Task;
        }
    }
}
