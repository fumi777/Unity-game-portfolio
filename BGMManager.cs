using UnityEngine;

public class BGMManager : MonoBehaviour
{
    [SerializeField] private AudioSource normalBGM;
    [SerializeField] private AudioSource eventBGM;
    [SerializeField] private AudioSource creditsBGM;

    private void Awake()
    {
        if ((normalBGM != null && normalBGM == eventBGM)
            || (creditsBGM != null && (creditsBGM == normalBGM || creditsBGM == eventBGM)))
        {
            Debug.LogError("BGMManager: use a different AudioSource for each BGM.", this);
            return;
        }
        Configure(normalBGM);
        Configure(eventBGM);
        Configure(creditsBGM);
        if (normalBGM != null) PlayNormalBGM();
    }

    private static void Configure(AudioSource source)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.Stop();
    }

    public void PlayNormalBGM() { SwitchTo(normalBGM); }
    public void PlayEventBGM() { SwitchTo(eventBGM); }
    public void PlayCreditsBGM() { SwitchTo(creditsBGM); }
    public void StopEventBGM() { if (eventBGM != null) eventBGM.Stop(); }
    public void StopCreditsBGM() { if (creditsBGM != null) creditsBGM.Stop(); }

    private void SwitchTo(AudioSource next)
    {
        if (next == null || next.clip == null)
        {
            Debug.LogError("BGMManager: check AudioSource and AudioClip assignments.", this);
            return;
        }
        if (normalBGM != null && normalBGM != next) normalBGM.Stop();
        if (eventBGM != null && eventBGM != next) eventBGM.Stop();
        if (creditsBGM != null && creditsBGM != next) creditsBGM.Stop();
        next.gameObject.SetActive(true);
        next.enabled = true;
        if (!next.isPlaying) next.Play();
    }

    private void OnDisable()
    {
        if (normalBGM != null) normalBGM.Stop();
        if (eventBGM != null) eventBGM.Stop();
        if (creditsBGM != null) creditsBGM.Stop();
    }
}
