using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC; 

public class Lobby 
{
    public string uniqueID;
    public bool Owned = false;
    public string name;
    public string owner;
    public int maxPlayers;
    public int currentPlayers;
    public int locked = 0;
    public string password = null;

    public GameObject menuObject;

    public RTCSessionDescription sdp;
    public List<RTCIceCandidate> iceCandidates;
    public Connection conn;
    public bool alreadyEstablished = false;

    public bool FoundInNewList = false;

    public Lobby(bool Owned = false)
    {
        this.Owned = Owned;
    }
}
