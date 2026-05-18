using System;
using System.Collections;
using UnityEngine;




public class PlayerBehaviour : MonoBehaviour
{
    public static PlayerBehaviour instance;


    [Header("Multiplayer setup")]
    [SerializeField] public GameHost GameHost;
    [SerializeField] public GamePlayer GamePlayer;
    [SerializeField] public Rigidbody playerRigidBody;



    [HideInInspector]public bool inMultiplayer = false;
    [HideInInspector]public bool UpdateMovement = false;
    private Coroutine MpUpdateCoroutine;



    public void Awake()
    {
        instance = this;
    }


    public static PositionLogger positionLogger;
    public static HealthLogger healthLogger;
    public void StartMpUpdate()
    {
        UpdateMovement = true;
        MpUpdateCoroutine = StartCoroutine(MpUpdate());

        if (!UILobbies.instance.toggle.isOn)
            positionLogger = new PositionLogger(this.transform, "host_pos" + UnityEngine.Random.Range(0, 50000) + ".csv", 20);

        if (MultiplayerManager.isHost)
        {
            healthLogger = new HealthLogger("host_health" + UnityEngine.Random.Range(0, 50000) + ".csv", 20);
            StartCoroutine(healthTest());
        }
    }

    IEnumerator healthTest()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (!inMultiplayer)
            {
                continue;
            }
            if (!UpdateMovement)
            {
                continue;
            }

            healthLogger.FakeHealth -= 1;

            StateSyncObject sso = new StateSyncObject();
            sso.type = SSOType.HealthTest;

            GameHost.SendActionUpdate(sso);
        }
    }

    IEnumerator MpUpdate()
    {
        PositionUpdateData posUpdate = new PositionUpdateData();
        while (true)
        {
            yield return new WaitForSecondsRealtime(0.05f);
            if (!inMultiplayer)
            {
                continue;
            }
            if (!UpdateMovement)
            {
                continue;
            }

            posUpdate.rotation = this.transform.eulerAngles.y;
            posUpdate.position = transform.position;
            posUpdate.velocity = playerRigidBody.linearVelocity;

            if (PlayerBehaviour.positionLogger != null && !PlayerBehaviour.positionLogger.isRunning)
                PlayerBehaviour.positionLogger.Start();

            if (PlayerBehaviour.healthLogger != null && !PlayerBehaviour.healthLogger.isRunning)
                PlayerBehaviour.healthLogger.Start();

            if (MultiplayerManager.isHost)
                GameHost.SendSelfUpdate(posUpdate);
            else GamePlayer.SendSelfUpdate(posUpdate);
        }
    }

    private void Update()
    {
        
        positionLogger?.UpdatePosition();
    }

}