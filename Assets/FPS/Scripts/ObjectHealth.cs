using UnityEngine;

public class ObjectHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
        NormalUI.instance.UpdateHealth(currentHealth);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        NormalUI.instance.UpdateHealth(currentHealth);
        if (currentHealth <= 0)
        {
            Debug.LogError("died");
        }

        StateSyncObject sso = new StateSyncObject();
        sso.type = SSOType.HealthUpdate;
        sso.SSOData = currentHealth.ToString();


        if (MultiplayerManager.isHost)
            GameHost.instance.SendActionUpdate(sso);
        else GamePlayer.instance.SendActionUpdate(sso); 
    }
}
