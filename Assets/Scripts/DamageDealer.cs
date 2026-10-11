using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    public float damageAmount = 10f; // Ne kadar can götürecek?
    public float damageInterval = 1f; // Ne kadar sürede bir (saniye)?
    private float nextDamageTime;

    private void OnTriggerStay(Collider other)
    {
        // Eğer çarpan objenin üzerinde PlayerMovement scripti varsa
        if (other.CompareTag("Player") && Time.time >= nextDamageTime)
        {
            PlayerMovement player = other.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.TakeHit(damageAmount); // Canı azalt
                nextDamageTime = Time.time + damageInterval; // Bekleme süresini güncelle
                //Debug.Log("Oyuncu hasar aldı! Kalan Can: " + player.HP);
            }
        }
    }
}