using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    public interface IImageGenBackend
    {
        ImageGenBackendType Type { get; }
        ImageGenCapabilities Capabilities { get; }

        Task<bool> TestConnectionAsync();
        Task<List<string>> GetSamplersAsync();
        Task<List<string>> GetSchedulersAsync();
        Task<List<string>> GetModelsAsync();
        Task GenerateAsync(ForgeGenerationParams parameters, IProgress<GenerationState> progress, CancellationToken ct = default);
        Task InterruptAsync();
    }
}
