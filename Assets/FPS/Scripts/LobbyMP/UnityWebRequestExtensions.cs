using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;

public static class UnityWebRequestExtensions
{
    public static async Task<UnityWebRequestResult> SendWebRequestAsync(this UnityWebRequest request)
    {
        var tcs = new TaskCompletionSource<UnityWebRequestResult>();

        async void Completed(AsyncOperation operation)
        {
            var webRequest = operation as UnityWebRequestAsyncOperation;
            
            if (webRequest != null) {
                //Debug.Log(webRequest.webRequest.result);
                if (webRequest.webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.webRequest.result == UnityWebRequest.Result.ProtocolError) {
                    tcs.SetResult(new UnityWebRequestResult(webRequest.webRequest.error));
                }
                else {
                    tcs.SetResult(new UnityWebRequestResult(webRequest.webRequest.downloadHandler.text, false));
                }
            }
            else {
                tcs.SetException(new InvalidOperationException("Invalid operation"));
            }
        }

        var asyncOperation = request.SendWebRequest();
        asyncOperation.completed += Completed;

        return await tcs.Task;
    }
}

public struct UnityWebRequestResult
{
    public bool IsError { get; }
    public string Error { get; }
    public string Result { get; }

    public UnityWebRequestResult(string error)
    {
        IsError = true;
        Error = error;
        Result = null;
    }

    public UnityWebRequestResult(string result, bool isError = false)
    {
        IsError = isError;
        Error = null;
        Result = result;
    }
}
