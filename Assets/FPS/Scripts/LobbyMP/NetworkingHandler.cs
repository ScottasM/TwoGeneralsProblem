using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;

using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System;
using Newtonsoft.Json;
using System.Security.Cryptography;
using Unity.VisualScripting;
using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;

public class CertificateWhore : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true;
    }
}

[Serializable]
public class SerializableRTCIceCandidate
{

    public string Candidate;
    public string SdpMid;
    public int SdpMLineIndex;

    // Constructor to initialize from RTCIceCandidate object
    public SerializableRTCIceCandidate(RTCIceCandidate candidate)
    {
        Candidate = candidate.Candidate;
        SdpMid = candidate.SdpMid;
        SdpMLineIndex = candidate.SdpMLineIndex ?? -1;
    }
}


public class NetworkingHandler : MonoBehaviour
{

    //public static NetworkingHandler instance;
    //public static Lobby joiningLobby;


    public RTCPeerConnection peerConnection;
    //public static List<RTCIceCandidate> iceCandidates = new List<RTCIceCandidate>();


    private Connection passedConnection;
    //public static RTCSessionDescription sdp;

    public bool Host;

  


    public void Awake()
    {
        //if(instance == null) {
        //    instance =this;
        //}

    }

    private ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();

    void Update()
    {
        while (actions.Count > 0) {
            if (actions.TryDequeue(out Action action)) {
                action.Invoke();
            }
        }
    }

    public void OnApplicationQuit()
    {
        LobbyManager.DeleteOwnedLobby();
    }

    public Dictionary<string,RTCDataChannel> receiveChannels = new Dictionary<string,RTCDataChannel>();
    public Dictionary<string,RTCDataChannel> sendChannels = new Dictionary<string,RTCDataChannel>();


    void ReceiveChannelCallback(RTCDataChannel channel,Connection conn)
    {
        receiveChannels.Add(channel.Label, channel);
        receiveChannels[channel.Label].OnMessage = (byte[] data) => HandleReceiveMessage(data,channel.Label);

        if (receiveChannels.Count == MultiplayerManager.Channels.Count) {

            MultiplayerManager.AllChannelsLoaded?.Invoke(conn);
        }
    }

    public void SendMessage(string label,string text)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            byte[] data = System.Text.Encoding.ASCII.GetBytes(text);
            try {
                if (sendChannels.ContainsKey(label)) {
                    sendChannels[label].Send(text);
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else Debug.LogError("No such channel registered : " + label);
#endif
            }
            catch (Exception e) { Debug.LogError("Send exception : " + e.Message); }
        });

    }

    private void HandleReceiveMessage(byte[] bytes,string label)
    {

        string message = System.Text.Encoding.UTF8.GetString(bytes);
        //Debug.Log("received message from : " + label + " " + message);
        if (MultiplayerManager.Channels.ContainsKey(label)) {
            //if (MultiplayerManager.Channels[label] == null) Debug.Log("is null");

                MultiplayerManager.Channels[label]?.Invoke(passedConnection, message);

            /*catch (Exception e) {
                Debug.LogError(e);
            }*/
            
            //Debug.LogWarning("Invoking : " + label + message);
        }
        else Debug.LogError("Channels doesnt exist with label : " + label);
    }

    public void crcn(bool hosted, string peerGUID, RTCSessionDescription offer,Connection conn)
    {

        try {
            actions.Enqueue(() =>
            {
                StartCoroutine(CreateConnection(hosted, peerGUID, offer,conn));
            });
        }
        catch (Exception e) {

        }
    }

    public void CloseConnection()
    {
        peerConnection.Close();
    }


    private Coroutine timeoutForHost;

    public IEnumerator CreateConnection(bool hosted,string peerGUID,RTCSessionDescription offer, Connection conn)
    {
        passedConnection = conn;
        Host = hosted;
        RTCConfiguration config = new RTCConfiguration {
            iceServers = new RTCIceServer[] {
            new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } },
            new RTCIceServer { urls = new string[] { "turn:mrozenas.lt:3478" }, username = "test", credential = "test123" }
        },
            iceTransportPolicy = RTCIceTransportPolicy.All
        };
        peerConnection = new RTCPeerConnection(ref config);


        foreach(var entry in MultiplayerManager.Channels) {
            RTCDataChannel sendChannel = peerConnection.CreateDataChannel(entry.Key);
            sendChannel.OnOpen = () => {
                /*if(!sendChannels.ContainsKey(entry.Key)) {
                    if (MultiplayerManager.isHost)
                        return;
                    sendChannels.Clear();
                }*/
                sendChannels.Add(entry.Key, sendChannel);

                if(sendChannels.Count == MultiplayerManager.Channels.Count) {

                    MultiplayerManager.AllChannelsLoaded?.Invoke(conn);
                }
            };
        }

        peerConnection.OnDataChannel = (channel) => ReceiveChannelCallback(channel,conn);


        peerConnection.OnIceCandidate = e => {
            if (!string.IsNullOrEmpty(e.Candidate)) {
                WebSocketHandler.Instance.SendIce(peerGUID, e,conn);
            }
                    
        };
        peerConnection.OnIceConnectionChange += newState => {

            if (timeoutForHost != null && newState != RTCIceConnectionState.Checking) {
                StopCoroutine(timeoutForHost);
                timeoutForHost = null;
            }
                

            if (newState == RTCIceConnectionState.Connected) {

                MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.SuccessToPlayer);
                MultiplayerManager.ConnectionEstablished?.Invoke(conn);
            }
            else if (newState == RTCIceConnectionState.Disconnected) {
                LobbyManager.OnDisconnection(message:"Disconnected",conn:passedConnection);
            }
            else if(newState == RTCIceConnectionState.Failed) {
                LobbyManager.OnDisconnection(message: "Disconnected",conn:passedConnection);
                MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.FailToPlayer);

            }

        };


        if (Host) {
            StartCoroutine(CreateOffer(peerGUID));
            
        }
            
        else StartCoroutine(CreateAnswer(offer,peerGUID));
        yield return null;


    }
    IEnumerator CreateOffer(string peerGUID)
    {
        var op = peerConnection.CreateOffer();
        
        yield return op;
        
        if (!op.IsError) {
            RTCSessionDescription sdp = op.Desc;

            var setDescOp = peerConnection.SetLocalDescription(ref sdp);
            yield return setDescOp;

            if (setDescOp.IsError) {

                LobbyManager.OnDisconnection(message: "Error while establishing connection : #0002", conn:passedConnection);
            }


            timeoutForHost = StartCoroutine(timeout());

            WebSocketHandler.Instance.SendOffer(peerGUID, sdp,passedConnection);
        }
        else {
            LobbyManager.OnDisconnection(message: "Error while establishing connection : #0003", conn:passedConnection);


        }

    }

    IEnumerator timeout()
    {
        yield return new WaitForSeconds(3f);
        LobbyManager.OnDisconnection(message: "Timed out", conn: passedConnection);
        timeoutForHost = null;
    }

    IEnumerator CreateAnswer(RTCSessionDescription offer,string peerGUID)
    {
        RTCSetSessionDescriptionAsyncOperation op9;
        try {
            op9 = peerConnection.SetRemoteDescription(ref offer);
        }
        catch (Exception e) {

            MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.FailToPlayer);
            yield break;
        }

       
        yield return op9;
        if (op9.IsError) {

            LobbyManager.OnDisconnection(message: "Error while establishing connection : #0001", conn:passedConnection);
            yield break;
        }

        var op = peerConnection.CreateAnswer();
        yield return op;

        if (!op.IsError) {
            RTCSessionDescription sdp = op.Desc;
            var setDescOp = peerConnection.SetLocalDescription(ref sdp);
            yield return setDescOp;
            

            if(setDescOp.IsError) {

                LobbyManager.OnDisconnection(message: "Error while establishing connection : #0004", conn:passedConnection);
            }
            else {
                WebSocketHandler.Instance.SendAnswer(passedConnection, JsonConvert.SerializeObject(sdp),peerGUID);
            }
        }
        else {

            LobbyManager.OnDisconnection(message: "Error while establishing connection : #0005", conn:passedConnection);
        }

    }

    public void GotAnswer(RTCSessionDescription answer)
    {
        try {
            actions.Enqueue(() => {
                StartCoroutine(GotAnswerCoroutine(answer));
            });
        }
        catch (Exception e) {

        }
    }

    IEnumerator GotAnswerCoroutine(RTCSessionDescription answer)
    {


        var op1 = peerConnection.SetRemoteDescription(ref answer);
        yield return op1;

        if (op1.IsError) {
            LobbyManager.OnDisconnection(message: "Error while establishing connection : #0006", conn: passedConnection);
        }
    }

    public void GotIce(RTCIceCandidate candidate)
    {
        peerConnection.AddIceCandidate(candidate);
    }

}
