using System.Diagnostics;
using System.Text;
using DesktopPet.Core.Chat;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace DesktopPet.Chat;

/// <summary>
/// The local model behind every pet's chat: one instance, loaded once, answering one request at a time (so its memory
/// is spent only once). Runs llama.cpp through LLamaSharp on the graphics card (Vulkan) or the processor. Requests run
/// off the UI thread.
/// </summary>
public sealed class LlamaChatModel : IChatModel, IDisposable
{
    private static readonly object ConfigureLock = new();
    private static bool s_configured;

    private readonly LLamaWeights _weights;
    private readonly ModelParams _parameters;
    private readonly SemaphoreSlim _one = new(1, 1);

    private LlamaChatModel(LLamaWeights weights, ModelParams parameters, bool onGpu, string backend)
    {
        _weights = weights;
        _parameters = parameters;
        OnGpu = onGpu;
        Backend = backend;
    }

    /// <summary>Whether the model runs on the graphics card.</summary>
    public bool OnGpu { get; }

    /// <summary>Which native library was loaded (for notifications and debugging), such as "vulkan" or "avx2".</summary>
    public string Backend { get; }

    /// <summary>
    /// Loads the model (seconds; runs on a worker thread). Wanting the GPU but finding none usable falls back to the
    /// processor; check <see cref="OnGpu"/>. The backend choice is made once per process, at the first load.
    /// </summary>
    /// <param name="path">The model file.</param>
    /// <param name="useGpu">Run it on the graphics card if there is one.</param>
    /// <param name="contextTokens">Room for the prompt and the answer, in tokens.</param>
    /// <param name="cancellationToken">Cancels before the load starts.</param>
    /// <exception cref="FileNotFoundException">No model at <paramref name="path"/>.</exception>
    public static Task<LlamaChatModel> LoadAsync(string path, bool useGpu, int contextTokens, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The chat model wasn't found.", path);
        }

        return Task.Run(() => Load(path, useGpu, contextTokens), cancellationToken);
    }

    public Task<string> CompleteAsync(ChatPrompt prompt, ChatSampling sampling, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(sampling);
        return Task.Run(() => RunAsync(ChatMl.Format(prompt), sampling, cancellationToken), cancellationToken);
    }

    public void Dispose()
    {
        _one.Wait(); // let a request in flight finish with the weights
        _weights.Dispose();
        _one.Dispose();
    }

    private static LlamaChatModel Load(string path, bool useGpu, int contextTokens)
    {
        Configure(useGpu);
        var parameters = new ModelParams(path)
        {
            ContextSize = (uint)System.Math.Max(512, contextTokens),
            GpuLayerCount = useGpu ? 999 : 0, // every layer
            BatchSize = 512,
        };

        var weights = LLamaWeights.LoadFromFile(parameters);
        var backend = NativeLibraryName();
        var onGpu = useGpu && backend.Contains("vulkan", StringComparison.OrdinalIgnoreCase);
        var model = new LlamaChatModel(weights, parameters, onGpu, backend);
        model.WarmUp();
        return model;
    }

    /// <summary>
    /// One short throwaway answer to a prompt of the usual size. The GPU compiles its shaders on first use, which
    /// otherwise makes the first real line take ten seconds or more.
    /// </summary>
    private void WarmUp()
    {
        var prompt = new ChatPrompt(
            ChatPrompts.System("Pip", "little grey rabbit", "Pip is a little grey rabbit who loves carrots and climbing.", null, ["Hello!", "Carrots, please."]),
            ChatPrompts.Joke);
        RunAsync(ChatMl.Format(prompt), new ChatRules().Sampling with { MaxTokens = 8 }, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>Picks the native library before anything loads it: Vulkan when wanted (if the machine has it), else the CPU.</summary>
    private static void Configure(bool useGpu)
    {
        lock (ConfigureLock)
        {
            if (s_configured)
            {
                return;
            }

            s_configured = true;
            NativeLibraryConfig.All
                .WithVulkan(useGpu)
                .WithCuda(false)
                .WithAutoFallback(true)
                .WithLogCallback((level, message) =>
                {
                    if (level is LLamaLogLevel.Error or LLamaLogLevel.Warning)
                    {
                        Debug.Write("llama.cpp: " + message);
                    }
                });
        }
    }

    private static string NativeLibraryName()
    {
        var path = NativeLibraryConfig.LLama.LibraryHasLoaded
            ? Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .FirstOrDefault(m => string.Equals(m.ModuleName, "llama.dll", StringComparison.OrdinalIgnoreCase))?.FileName
            : null;
        return path is null ? "unknown" : new DirectoryInfo(Path.GetDirectoryName(path)!).Name;
    }

    private async Task<string> RunAsync(string prompt, ChatSampling sampling, CancellationToken cancellationToken)
    {
        await _one.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var executor = new StatelessExecutor(_weights, _parameters) { ApplyTemplate = false };
            var inference = new InferenceParams
            {
                MaxTokens = sampling.MaxTokens,
                AntiPrompts = [ChatMl.EndOfTurn, "<|endoftext|>", "<|im_start|>"],
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = (float)sampling.Temperature,
                    TopP = (float)sampling.TopP,
                    TopK = sampling.TopK,
                    MinP = (float)sampling.MinP,
                    RepeatPenalty = (float)sampling.RepeatPenalty,
                },
            };

            var reply = new StringBuilder();
            await foreach (var piece in executor.InferAsync(prompt, inference, cancellationToken).ConfigureAwait(false))
            {
                reply.Append(piece);
            }

            foreach (var stop in inference.AntiPrompts)
            {
                var at = reply.ToString().IndexOf(stop, StringComparison.Ordinal);
                if (at >= 0)
                {
                    reply.Length = at;
                }
            }

            return reply.ToString();
        }
        finally
        {
            _one.Release();
        }
    }
}
