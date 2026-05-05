using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class MixerVolumeController : MonoBehaviour
{
    [System.Serializable]
    public class MixerGroup
    {
        public string name;
        public AudioMixer mixer;
        public string exposedParam;
        public Slider slider;
        [Range(0.001f, 1f)]
        public float defaultVolume = 1f;
    }

    public MixerGroup[] groups;

    void Start()
    {
        foreach (var group in groups)
        {
            if (group.slider == null || group.mixer == null) continue;

            float saved = PlayerPrefs.GetFloat(group.exposedParam, group.defaultVolume);
            group.slider.value = saved;
            ApplyVolume(group.exposedParam, group.mixer, saved);

            var g = group;
            group.slider.onValueChanged.AddListener(value =>
            {
                ApplyVolume(g.exposedParam, g.mixer, value);
                PlayerPrefs.SetFloat(g.exposedParam, value);
                PlayerPrefs.Save();
            });
        }
    }

    void ApplyVolume(string param, AudioMixer mixer, float value)
    {
        float dB = Mathf.Log10(Mathf.Max(value, 0.001f)) * 20f;
        mixer.SetFloat(param, dB);
    }

    public void ResetAll()
    {
    foreach (var group in groups)
        {
            if (group.slider == null || group.mixer == null) continue;
            group.slider.value = 1f;
            ApplyVolume(group.exposedParam, group.mixer, 1f);
            PlayerPrefs.SetFloat(group.exposedParam, 1f);
        }
        PlayerPrefs.Save();
    }
}