using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading;
using System.Collections;



#if UNITY_EDITOR
using UnityEditor;

#endif

public class MpPlayer : MonoBehaviour
{
    [SerializeField] public Rigidbody playerRigidbody;
    [SerializeField] public Transform playerGraphics;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private TextMeshPro nicknameText;
    [SerializeField] private TextMeshPro healthText;

    protected Animator playerAnimatorController;

    protected readonly int ANIMATOR_PARAM_RUN_HASH = Animator.StringToHash("Run");
    protected readonly int ANIMATOR_PARAM_MOVEMENT_HASH = Animator.StringToHash("Movement");
    protected readonly int ANIMATOR_PARAM_IS_GROUNDED_HASH = Animator.StringToHash("isGrounded");
    protected readonly int ANIMATOR_PARAM_JUMP_HASH = Animator.StringToHash("Jump");
    protected readonly int ANIMATOR_PARAM_FALL_HASH = Animator.StringToHash("Fall");
    protected readonly int ANIMATOR_PARAM_DISABLE = Animator.StringToHash("disabled");

    protected bool IsKinematic
    {
        get => playerRigidbody.isKinematic;

        set {

            playerRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;// : CollisionDetectionMode.Continuous;
            playerRigidbody.isKinematic = value;

        }

    }

    protected void Awake()
    {
        //nicknameTransform = nicknameText.gameObject.transform;
        playerRigidbody = GetComponent<Rigidbody>();
        
    }


    private Vector3 startPos;



    private PositionLogger positionLogger;
    public HealthLogger healthLogger;
    public void Init()
    {
        IsKinematic = true;
        if(UILobbies.instance.toggle.isOn)
            positionLogger = new PositionLogger(transform, "mpplayer_pos" + Random.Range(0, 50000) + ".csv", 20);

        if (!MultiplayerManager.isHost)
        {
            healthLogger = new HealthLogger("player_health" + UnityEngine.Random.Range(0, 50000) + ".csv", 20);
        }

    }

    public void PlaceInStart()
    {
        transform.position = startPos;
    }

    public void InitNickname(string nick)
    {
        nicknameText.text = nick;
    }

    public void PlayWinAnimation()
    {
        IsKinematic = true;
        transform.eulerAngles = new Vector3(0f, 180f, 0f);
        playerAnimatorController.SetBool(ANIMATOR_PARAM_MOVEMENT_HASH, false);

    }

    private Vector3 targetRot;
    private Vector3 targetPos;
    private Vector3 targetVelocity;
    private bool posSet = false;

    [SerializeField] private float lerpSpeed;
    public void ReceivedPosition(Vector3 pos,Vector3 eulers,Vector3 velocity)
    {
        if(positionLogger != null && !positionLogger.isRunning)
            positionLogger.Start();
        if(healthLogger != null && !healthLogger.isRunning)
            healthLogger.Start();

        //if (Random.Range(0, 100) < 20) @@ faketest
        //    return;

        //Thread thread = new Thread(() => {

            //Thread.Sleep(120);
            targetRot = eulers;
            targetPos = pos;
            targetVelocity = velocity;
            posSet = false;
        //});
        //thread.Start();
    }


    public void ReceivedShot(ShotData data)
    {
        Instantiate(bulletPrefab, data.position, Quaternion.Euler(data.rotation));
    }


    public void Update()
    {
        if (!posSet)
        {
            transform.position = targetPos;
            playerGraphics.eulerAngles = targetRot;
            posSet = true;
        }

        
        
        //transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * lerpSpeed);
        //playerGraphics.eulerAngles = Vector3.Lerp(playerGraphics.eulerAngles,targetRot, Time.deltaTime * lerpSpeed);
        positionLogger?.UpdatePosition();
    }

    public void FixedUpdate()
    {
        if (targetVelocity != Vector3.zero)
            Debug.Log(targetVelocity);
        playerRigidbody.MovePosition(transform.position + targetVelocity * Time.fixedDeltaTime);
        positionLogger?.UpdatePosition();
    }



    public void UpdateHealth(int health)
    {
        healthText.text = health.ToString();
    }
}
