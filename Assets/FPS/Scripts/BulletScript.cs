using UnityEngine;

public class BulletScript : MonoBehaviour
{
    public float MoveSpeed = 500;
    public float raycastDistance = 5;
    public float DestroyAfter = 5;
    public LayerMask ignoreLayer;

    private float elapsedTime = 0;


    public void Update()
    {
        elapsedTime += Time.deltaTime;
        if (elapsedTime > DestroyAfter)
        {
            Destroy(this.gameObject);
            return;
        }

        float traveled = MoveSpeed * Time.deltaTime;
        if (Physics.Raycast(transform.position, transform.forward, out var hit, traveled, ~ignoreLayer))
        {
            ObjectHealth objectHealth = hit.collider.GetComponent<ObjectHealth>();
            if (objectHealth != null)
                objectHealth.TakeDamage(15);

            Destroy(gameObject);


        }

        
        transform.position += transform.forward * traveled;


        

    }

}