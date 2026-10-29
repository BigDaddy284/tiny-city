using System.Text;
using TinyCity.Simulation.AI;
using UnityEngine.Networking;

namespace TinyCity.Runtime
{
    /// <summary>Sends AI requests with UnityWebRequest. Calls are polled, never awaited, so the game never blocks.</summary>
    public sealed class UnityAiTransport : IAiTransport
    {
        public IAiCall Get(string url, double timeoutSeconds) => Start(UnityWebRequest.Get(url), timeoutSeconds);

        public IAiCall Post(string url, string jsonBody, double timeoutSeconds)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody ?? string.Empty)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Content-Type", "application/json");
            return Start(request, timeoutSeconds);
        }

        private static IAiCall Start(UnityWebRequest request, double timeoutSeconds)
        {
            // The queue enforces its own timeout too; this one stops Unity holding the connection open longer.
            request.timeout = (int)System.Math.Ceiling(timeoutSeconds) + 1;
            try
            {
                request.SendWebRequest();
            }
            catch
            {
                // For example "Insecure connection not allowed"; the AI service reports it and carries on.
                request.Dispose();
                throw;
            }

            return new Call(request);
        }

        private sealed class Call : IAiCall
        {
            private UnityWebRequest request;
            private string text;
            private string error;
            private bool succeeded;
            private bool done;

            public Call(UnityWebRequest request)
            {
                this.request = request;
            }

            public bool IsDone
            {
                get
                {
                    Poll();
                    return done;
                }
            }

            public bool Succeeded => IsDone && succeeded;

            public string ResponseText => IsDone ? text : null;

            public string Error => IsDone ? error : null;

            public void Cancel()
            {
                if (request != null)
                {
                    request.Abort();
                    request.Dispose();
                    request = null;
                }

                done = true;
                succeeded = false;
                error = "cancelled";
            }

            private void Poll()
            {
                if (done || request == null || !request.isDone)
                {
                    return;
                }

                text = request.downloadHandler?.text;
                succeeded = request.result == UnityWebRequest.Result.Success;
                error = succeeded ? null : $"{request.error} (HTTP {request.responseCode})";
                done = true;
                request.Dispose();
                request = null;
            }
        }
    }
}
