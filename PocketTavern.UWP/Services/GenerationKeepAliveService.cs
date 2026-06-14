using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.ExtendedExecution;

namespace PocketTavern.UWP.Services
{
    public class GenerationKeepAliveService : IDisposable
    {
        private ExtendedExecutionSession _session;
        private bool _disposed;

        public async Task<bool> RequestAsync()
        {
            Release();

            _session = new ExtendedExecutionSession
            {
                Reason = ExtendedExecutionReason.Unspecified,
                Description = "AI response generation in progress"
            };
            _session.Revoked += OnSessionRevoked;

            var result = await _session.RequestExtensionAsync();
            if (result == ExtendedExecutionResult.Denied)
            {
                _session.Dispose();
                _session = null;
                return false;
            }
            return true;
        }

        public void Release()
        {
            if (_session != null)
            {
                _session.Revoked -= OnSessionRevoked;
                _session.Dispose();
                _session = null;
            }
        }

        private void OnSessionRevoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            if (_session != null)
            {
                _session.Revoked -= OnSessionRevoked;
                _session.Dispose();
                _session = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Release();
        }
    }
}
