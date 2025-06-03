using UnityEngine;
using UnityEngine.SceneManagement;

public class HeadHoverSelector : MonoBehaviour
{
    public float requiredHoverTime = 2.0f;
    private float hoverTimer = 0f;

    [Header("Cursor")]
    public GameObject cursor;

    void Update()
    {
        if (cursor == null) return;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, cursor.transform.position);

        RectTransform rt = GetComponent<RectTransform>();

        if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null))
        {
            hoverTimer += Time.deltaTime;

            if (hoverTimer > requiredHoverTime)
            {
                onHoverSelect();
                hoverTimer = 0f;
            }
        } 
        else 
        {
            hoverTimer = 0f;
        }
    }

    void onHoverSelect()
    {
        switch (gameObject.name)
        {
            case "Player1Button":
                SceneManager.LoadScene("SampleScene");
                break;
            case "Player2Button":
                SceneManager.LoadScene("SecondPlayer");
                break;
            case "ExitButton":
                Application.Quit();
                break;
        }
    }
}
