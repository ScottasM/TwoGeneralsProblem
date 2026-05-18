using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NormalUI : MonoBehaviour
{
    public static NormalUI instance;
    [SerializeField] private TextMeshProUGUI bulletText;
    [SerializeField] private TextMeshProUGUI healthText;


    private void Awake()
    {
        instance = this;
    }

    public void UpdateHealth(int health)
    {
        healthText.text = health.ToString();
    }

    public void UpdateBullets(int bullets)
    {
        bulletText.text = bullets.ToString();
    }

    

}
