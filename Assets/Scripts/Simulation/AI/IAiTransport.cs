namespace TinyTown.Simulation.AI
{
    /// <summary>
    /// One HTTP call in progress. Polled each frame rather than awaited, so the simulation stays single-threaded
    /// and a slow or missing Ollama service can never block it.
    /// </summary>
    public interface IAiCall
    {
        bool IsDone { get; }

        /// <summary>True when the call completed with an HTTP success status.</summary>
        bool Succeeded { get; }

        /// <summary>The response body, which may hold an error message even when <see cref="Succeeded"/> is false.</summary>
        string ResponseText { get; }

        /// <summary>A short description of what went wrong, when not <see cref="Succeeded"/>.</summary>
        string Error { get; }

        void Cancel();
    }

    /// <summary>Sends requests to the AI service. The game uses UnityWebRequest; tests use a scripted fake.</summary>
    public interface IAiTransport
    {
        IAiCall Get(string url, double timeoutSeconds);

        IAiCall Post(string url, string jsonBody, double timeoutSeconds);
    }
}
