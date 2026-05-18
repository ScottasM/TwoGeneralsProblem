using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JetBrains.Annotations;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Threading;

[Serializable]
public class PlayerGameStateUpdate
{

    public string UID = null;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore), DefaultValue(false)]
    public bool Alive { get; set; }

    public List<StateSyncObject> StateSyncObjects { get; set; } = new List<StateSyncObject>();

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string JoinData { get; set; } = null;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore), DefaultValue(0)]
    public float joinedAt { get; set; } = 0;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore),DefaultValue(0)]
    public float DiedAt { get; set; } = 0;

    [JsonIgnore]
    public float lastUpdate;

}

public class GamePlayer : MonoBehaviour
{
    public static GamePlayer instance;

    public List<LobbyPlayer> lobbyPlayers = new List<LobbyPlayer>();

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerObject;

    public Connection hostConn;
    private JoinData jd;
    private LobbyPlayer myPlayer;

    private bool Alive = false;
    public bool GameRunning = false;

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        
    }


    private Action<Connection, string> _GameStateHandler;

    public void StartPlayer(Connection hostConn)
    {
        if (senderCoroutine != null)
            StopCoroutine(senderCoroutine);


        playersJoinProcessed = false;
        MultiplayerManager.isHost = false;
        PlayerBehaviour.instance.inMultiplayer = true;
        stateUpdater = new PlayerGameStateUpdate();
        stateUpdater.lastUpdate = Time.realtimeSinceStartup;
        processedSSO.Clear();
        lobbyPlayers.Clear();
        MultiplayerManager.Channels["PlayerJoined"] += PlayerJoined;
        MultiplayerManager.Channels["PlayerLeft"] += PlayerLeft;
        MultiplayerManager.Channels["UpdatePosition"] += ReceiveUpdatePosition;
        MultiplayerManager.Channels["Shot"] += ShotReceive;
        _GameStateHandler = (Connection conn, string data) => UpdateGameState(conn, data, null);

        MultiplayerManager.Channels["GameStateUpdate"] += _GameStateHandler;
        MultiplayerManager.Channels["PlayerDied"] += PlayerDied;
        MultiplayerManager.Channels["PlayerAction"] += PlayerActionUpdate;
        MultiplayerManager.Channels["StateSync"] += StateSyncReceive;
        //MultiplayerManager.ConnectionEstablished += SendPlayerJoined;


        LobbyPlayer lPlayer = PlaceSelf();

        JoinData joinData = new JoinData();
        joinData.name = PlayerDefs.nickname;
        joinData.UID = lPlayer.UID;
        joinData.host = false;


        jd = joinData;

        this.hostConn = hostConn;



        StartGame();
        UILobbies.instance.ShowHideLobbyUI(false);
        PlayerBehaviour.instance.UpdateMovement = true;
        PlayerDefs.MovementEnabled = true;
    }


    private int countChannel = 0;
    public void SendPlayerJoined(Connection conn)
    {
        countChannel++;
        if(countChannel == 1) {
            //Debug.LogWarning("One part of channels fully loaded");
            return;
        }
        else if (countChannel == 2) {
            //Debug.LogWarning("All channels successfully lodaded");
            countChannel = 0;
        }
        

        MultiplayerManager.ConnectionStateChange(ConnectState.AllChannesLoaded);


        StartCoroutine(delay(0.5f));
    }


    private Coroutine senderCoroutine = null;

    private IEnumerator delay(float delay)
    {
        yield return new WaitForSeconds(delay);
        hostConn.SendMessage(JsonUtility.ToJson(jd), "PlayerJoined");


        senderCoroutine = StartCoroutine(GameStateUpdateSender());
        stateUpdater.JoinData = JsonUtility.ToJson(jd);
        stateUpdater.joinedAt = Time.realtimeSinceStartup;
        stateUpdater.lastUpdate = Time.realtimeSinceStartup + 2f;
    }

    LobbyPlayer PlaceSelf()
    {
        LobbyPlayer lPlayer = new LobbyPlayer();
        lPlayer.name = PlayerDefs.nickname;
        lPlayer.playerObject = playerObject.gameObject;

        lPlayer.self = true;
        lPlayer.UID = Guid.NewGuid().ToString();
        lPlayer.host = false;
        lPlayer.conn = null;
        lobbyPlayers.Add(lPlayer);
        myPlayer = lPlayer;

        return lPlayer;
    }

    public void PlayerJoined(Connection conn,string data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {



            JoinData joinData = JsonUtility.FromJson<JoinData>(data);

            if (lobbyPlayers.Find(u => u.UID == joinData.UID) != null)
                return;

            Debug.LogWarning("Received UID on join  :" + joinData.UID);

            LobbyPlayer lPlayer = CreatePlayer(joinData.name, joinData.clothes);

            lPlayer.self = false;
            lPlayer.UID = joinData.UID;
            lPlayer.host = joinData.host;
            lPlayer.Alive = true;
            lobbyPlayers.Add(lPlayer);
        });
    }

    LobbyPlayer CreatePlayer(string name, int[] clothing)
    {
        GameObject pObject = Instantiate(playerPrefab);
        pObject.GetComponent<MpPlayer>().InitNickname(name);

        int platformId = lobbyPlayers.Count;
        pObject.transform.localPosition = new Vector3(0, 0.2f, 0);


        LobbyPlayer lPlayer = new LobbyPlayer();
        lPlayer.mpPlayer = pObject.GetComponent<MpPlayer>();
        lPlayer.mpPlayer.Init();
        lPlayer.name = name;
        lPlayer.clothes = clothing;
        lPlayer.playerObject = pObject;
        return lPlayer;
    }

    public void PlayerLeft(Connection conn,string data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            LobbyPlayer player = lobbyPlayers.Find(lb => lb.UID == data);
            if (player == null) {
                Debug.LogError("player that left doesnt exist");
                return;
            }
            Destroy(player.playerObject);
            lobbyPlayers.Remove(player);
        });
    }

    public void ClosePlayer(bool stopRTC = false)
    {
        if (MultiplayerManager.isHost) return;

        try {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {

                //PlayerBehaviour.instance.UpdateMovement = false;
                //PlayerBehaviour.instance.ResetCharacter();
                
                if(senderCoroutine != null)
                    StopCoroutine(senderCoroutine);
                senderCoroutine = null;


                WebSocketHandler.Instance.socket.Disconnect();
                MultiplayerManager.Channels["PlayerJoined"] -= PlayerJoined;
                MultiplayerManager.Channels["PlayerLeft"] -= PlayerLeft;
                MultiplayerManager.Channels["UpdatePosition"] -= ReceiveUpdatePosition;
                MultiplayerManager.Channels["Shot"] -= ShotReceive;
                MultiplayerManager.Channels["GameStateUpdate"] -= _GameStateHandler;
                MultiplayerManager.Channels["PlayerDied"] -= PlayerDied;
                MultiplayerManager.Channels["PlayerAction"] -= PlayerActionUpdate;
                MultiplayerManager.Channels["StateSync"] -= StateSyncReceive;
                foreach (LobbyPlayer player in lobbyPlayers) {
                    if (player.self) continue;
                    Destroy(player.playerObject);
                }
                lobbyPlayers.Clear();
                //UIController.ShowPage(typeof(UIMainMenu), true);
                PlayerBehaviour.instance.transform.SetParent(null);
                jd = null;
                if (stopRTC) {
                    WebSocketHandler.CloseAllConnections("");
                }
                hostConn = null;

                PlayerBehaviour.instance.inMultiplayer = false;
                stateUpdater = null;
                GameRunning = false;
                UILobbies.instance.ShowHideLobbyUI(true);
                PlayerBehaviour.instance.UpdateMovement = false ;
                PlayerDefs.MovementEnabled = false;
            });
        }
        catch (Exception e) {
            Debug.LogError(e);
        }
        
        
    }

    public void StartGame()
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            Alive = true;
            myPlayer.Alive = true;
            //ActivateSpectate(false);

            PlayerBehaviour.instance.StartMpUpdate();
        });
    }


    int counter = 999;

   


    private Vector3 eulers = Vector3.zero;
    public void ReceiveUpdatePosition(Connection conn, string data)
    {
        PooledPositionData pooledData = JsonUtility.FromJson<PooledPositionData>(data);
        foreach (PositionUpdateData posData in pooledData.data) {
            foreach(LobbyPlayer player in lobbyPlayers) {
                if (player.UID == posData.UID) {
                    if (player.self || !player.Alive)
                        break;
                    player.positionUpdateData = posData;

                    eulers.y = posData.rotation;
                    //player.mpPlayer.playerRigidbody.linearVelocity = posData.velocity;

                    player.mpPlayer.ReceivedPosition(posData.position, eulers,posData.velocity);
                    break;
                }
            }
        }
    }

    public void PlayerDied(Connection conn, string uid)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            LobbyPlayer diedPlayer = lobbyPlayers.Find(u => u.UID == uid);
            if (diedPlayer != null) {
                Debug.LogError("Died player not found");
                return;
            }



            PlaceInStart(diedPlayer);
        });
    }

    public void SendSelfUpdate(PositionUpdateData data)
    {
        data.UID = jd.UID;
        if (hostConn != null)
            hostConn.SendMessage(JsonUtility.ToJson(data), "UpdatePosition");
        else Debug.LogError("Trying to send position update when host conn is null");
    }


    public void SendDeath()
    {
        int aliveCount = 0;

        foreach (LobbyPlayer lb in lobbyPlayers) {
            if (lb.Alive && lb != myPlayer)
                aliveCount++;
        }

        //if (aliveCount > 1)
            //ActivateSpectate(true);

        hostConn.SendMessage("", "PlayerDied");
        resetPlayerToWaiting();
    }

    public void UpdateGameState(Connection conn,string data,GameStateUpdate forceObj = null)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            //Debug.Log("received game state update " + data);



            GameStateUpdate state = null;
            if (forceObj == null)
                state = JsonConvert.DeserializeObject<GameStateUpdate>(data);
            else state = forceObj;

            if (state.winner != null) {
                //ActivateSpectate(false);
                GameRunning = false;


                foreach (LobbyPlayer pl in lobbyPlayers) {
                    if (!pl.self) PlaceInStart(pl);
                }

                resetPlayerToWaiting();
            }
            else {
            }
        });
    }


    public void PlaceInStart(LobbyPlayer lb)
    {
        
    }


    public void SendActionUpdate(StateSyncObject SSObject = null)
    {

        if (SSObject.UID == null)
            SSObject.UID = jd.UID;

        SSObject.addedAt = Time.realtimeSinceStartup;
        SSObject.SSOID = Guid.NewGuid().ToString();

        stateUpdater.StateSyncObjects.Add(SSObject);
        
        hostConn.SendMessage(JsonConvert.SerializeObject(SSObject), "PlayerAction");
    }



    public void PlayerActionUpdate(Connection conn, string data)
    {
        //Debug.LogWarning("Received action update:");
        //Debug.LogWarning(data);
        //Debug.LogWarning("passed thorugh udpate : " + data);
        StateSyncObject dt = JsonConvert.DeserializeObject<StateSyncObject>(data);

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (UnityEngine.Random.Range(0, 100) < 20)
                return;
            processSSO(conn, data, dt);
        });


        
       
    }

    private void processSSO(Connection conn,string data = null, StateSyncObject dt = null)
    {
        if (dt == null)
            return;

        try {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {

               

                if (dt.SSOID != null) {
                    if (processedSSO.Contains(dt.SSOID))
                        return;
                    processedSSO.Add(dt.SSOID);
                }

                switch (dt.type) {
                    case SSOType.HealthTest:
                        LobbyPlayer player1 = lobbyPlayers.Find(x => x.UID == dt.UID);
                        if (player1 == null)
                        {
                            Debug.LogError("player that spawned the health test not found");
                            return;
                        }

                        

                        Thread thr = new Thread(() => {

                            Thread.Sleep(100);

                            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                                player1.mpPlayer.healthLogger.FakeHealth -= 1;
                            });
                        });
                        thr.Start();
                        
                        break;
                    case SSOType.HealthUpdate:
                        int health = int.Parse(dt.SSOData);
                        LobbyPlayer player = lobbyPlayers.Find(x => x.UID == dt.UID);
                        if (player == null)
                        {
                            Debug.LogError("player that spawned the shot not found");
                            return;
                        }
                        player.mpPlayer.UpdateHealth(health);
                        break;

                }
            });
        }
        catch (Exception e) { Debug.LogError("Caught error in action update : " + e.Message); }
    }

    public Vector2Int DeserializeVector2Int(string vector2IntString)
    {
        string[] values = vector2IntString.Split(',');
        if (values.Length == 2) {
            int x = int.Parse(values[0]);
            int y = int.Parse(values[1]);
            return new Vector2Int(x, y);
        }
        else {
            // Handle the error or provide a default value
            Debug.LogError("Invalid format for Vector2Int string");
            
        }
        return new Vector2Int(-1, -1);
    }

    private void resetPlayerToWaiting()
    {
        //playerObject.transform.localPosition = new Vector3(0, 0.2f, 0);
        
        Alive = false;
        stateUpdater.DiedAt = Time.realtimeSinceStartup;
        //playerObject.transform.GetChild(0).GetChild(0).eulerAngles = Vector3.zero;
        //playerObject.transform.eulerAngles = new Vector3(0, 180, 0);
        //Camera.main.transform.parent.position = new Vector3(18.5f, 216.764f, 0f);
        //Camera.main.transform.parent.eulerAngles = new Vector3(0, 180, 0);
    }

    public PlayerGameStateUpdate stateUpdater = new PlayerGameStateUpdate();
    private List<string> processedSSO = new List<string>();

    public IEnumerator GameStateUpdateSender()
    {



        if (stateUpdater.UID == null)
            stateUpdater.UID = jd.UID;

        List<StateSyncObject> removeSSOList = new List<StateSyncObject>();

        while (true) {

            yield return new WaitForSeconds(0.5f);
            if (lobbyPlayers.Count == 0)
                continue;
            if (!PlayerBehaviour.instance.inMultiplayer)
                yield break;
            if (stateUpdater == null)
                yield break;


            stateUpdater.Alive = Alive;

            if(Time.realtimeSinceStartup >  stateUpdater.lastUpdate + 3f) {
                LobbyManager.OnDisconnection(message:"timed out", conn: hostConn);
            }

            foreach (StateSyncObject SSO in stateUpdater.StateSyncObjects) {
                if (Time.realtimeSinceStartup > SSO.addedAt + 3f) {
                    removeSSOList.Add(SSO);
                    continue;
                }
            }

            for (int i = 0; i < removeSSOList.Count; i++) {
                stateUpdater.StateSyncObjects.Remove(removeSSOList[i]);
            }

            if(stateUpdater.JoinData != null) {
                if (Time.realtimeSinceStartup > stateUpdater.joinedAt + GameHost.StateSyncTimeout) {
                    stateUpdater.JoinData = null;
                    stateUpdater.joinedAt = 0;
                }
            }

            if(stateUpdater.DiedAt > 0) {
                if(Time.realtimeSinceStartup > stateUpdater.DiedAt + GameHost.StateSyncTimeout) {
                    stateUpdater.DiedAt = 0;
                }
            }

            hostConn.SendMessage(JsonConvert.SerializeObject(stateUpdater), "StateSync");
        }
    }

    private bool playersJoinProcessed = false;
     
    public void StateSyncReceive(Connection conn, string data)
    {
        //Debug.Log("received state sync : " + data);
        UnityMainThreadDispatcher.Instance().Enqueue(() => {

            if (UnityEngine.Random.Range(0, 100) < 20)
                return;
            GameStateUpdate PGSupdate = JsonConvert.DeserializeObject<GameStateUpdate>(data);



            if (conn != hostConn) {
                Debug.LogError("State sync received from not host");
                return;
            }

            stateUpdater.lastUpdate = Time.realtimeSinceStartup;

            if(PGSupdate.winner != null && GameRunning) {
                UpdateGameState(conn, null,PGSupdate);
            }

            Debug.Log("Received state sync with" + PGSupdate.StateSyncObjects.Count);
            foreach (StateSyncObject SSObject in PGSupdate.StateSyncObjects) {


                try {
                    processSSO(conn, dt: SSObject);
                }
                catch (Exception e) {
                    Debug.LogError(e);
                }
            }


            foreach(GameStatePlayer gsp in PGSupdate.playerStates) {

                if(gsp.UID == jd.UID) {
                    if(gsp.pjdData != null && !playersJoinProcessed) {
                        pooledJoinData pjd = JsonConvert.DeserializeObject<pooledJoinData>(gsp.pjdData);

                        foreach(JoinData pj in pjd.joinDatas) {
                            if (lobbyPlayers.Find(u => u.UID == jd.UID) == null)
                                PlayerJoined(hostConn, JsonUtility.ToJson(jd));
                        }
                        playersJoinProcessed = true;
                    }
                    continue;
                }

                LobbyPlayer statePlayer = lobbyPlayers.Find(u => u.UID == gsp.UID);
                if (statePlayer == null) {
                    if(gsp.joinData !=  null) {
                        PlayerJoined(null, gsp.joinData);
                    }
                    return;
                }
                    
                if (gsp.DCed) {
                    PlayerLeft(null, statePlayer.UID);
                    continue;
                }
                if(statePlayer.Alive && !gsp.Alive) {
                    PlayerDied(null, gsp.UID);
                }
            }


            for (int i = processedSSO.Count - 1; i >= 0; i--) {
                StateSyncObject sso = PGSupdate.StateSyncObjects.Find(u => u.SSOID == processedSSO[i]);
                if (sso == null) {
                    processedSSO.Remove(processedSSO[i]);
                }
            }
        });
    }

    public void ShotReceive(Connection conn, string data)
    {
        ShotData shotdata = JsonConvert.DeserializeObject<ShotData>(data);

        LobbyPlayer player = lobbyPlayers.Find(x=>x.UID == shotdata.playerUID);
        if(player == null)
        {
            Debug.LogError("player that spawned the shot not found");
            return;
        }

        player.mpPlayer.ReceivedShot(shotdata);
    }

    public void SendShot(ShotData data)
    {
        data.playerUID = myPlayer.UID;
        hostConn.SendMessage(JsonUtility.ToJson(data), "Shot");
    }

}
