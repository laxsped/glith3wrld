using System.Collections;
using UnityEngine;

public class ЛифтСтарт : MonoBehaviour
{
    [Header("Объекты")]
    [SerializeField] private Transform леваяДверь;
    [SerializeField] private Transform праваяДверь;
    [SerializeField] private GameObject родительЗдания;

    [Header("Аудио")]
    [SerializeField] private AudioSource источникЗвука;
    [SerializeField] private AudioClip звукOneShot;
    [SerializeField] [Range(0f, 1f)] private float громкостьSfx = 1f;

    [Header("Тайминги")]
    [SerializeField] private float задержкаПередОткрытием = 7f;
    [SerializeField] private float длительностьОткрытия = 1f;

    [Header("Целевые Z (локальные)")]
    [SerializeField] private float zЛевойДвери = -1.046f;
    [SerializeField] private float zПравойДвери = 1.046f;

    private void Awake()
    {
        if (родительЗдания != null)
        {
            родительЗдания.SetActive(false);
        }
    }

    private void Start()
    {
        StartCoroutine(ЗапускСценыЛифта());
    }

    private IEnumerator ЗапускСценыЛифта()
    {
        yield return new WaitForSeconds(задержкаПередОткрытием);

        yield return StartCoroutine(ПлавноОткрытьДвери());

        if (родительЗдания != null)
        {
            родительЗдания.SetActive(true);
        }

        if (источникЗвука != null && звукOneShot != null)
        {
            float fx = Mathf.Clamp01(PlayerPrefs.GetInt("PauseSimple.volume_effects", 10) / 10f);
            источникЗвука.PlayOneShot(звукOneShot, громкостьSfx * fx);
        }
    }

    private IEnumerator ПлавноОткрытьДвери()
    {
        if (леваяДверь == null || праваяДверь == null)
        {
            yield break;
        }

        Vector3 стартЛевой = леваяДверь.localPosition;
        Vector3 стартПравой = праваяДверь.localPosition;

        Vector3 цельЛевой = new Vector3(стартЛевой.x, стартЛевой.y, zЛевойДвери);
        Vector3 цельПравой = new Vector3(стартПравой.x, стартПравой.y, zПравойДвери);

        float t = 0f;
        float длительность = Mathf.Max(0.01f, длительностьОткрытия);

        while (t < длительность)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / длительность);
            леваяДверь.localPosition = Vector3.Lerp(стартЛевой, цельЛевой, k);
            праваяДверь.localPosition = Vector3.Lerp(стартПравой, цельПравой, k);
            yield return null;
        }

        леваяДверь.localPosition = цельЛевой;
        праваяДверь.localPosition = цельПравой;
    }
}


