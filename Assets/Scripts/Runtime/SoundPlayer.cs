using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TinyCity.Art;
using TinyCity.Simulation;
using TinyCity.Simulation.Configuration;
using TinyCity.Sound;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Plays the music, the ambience of the place the active panel shows, and the sounds of residents near its
    /// camera. Every sound is generated in the background at start-up (or read from a WAV in
    /// <c>StreamingAssets/Audio</c>), and volumes follow the Settings window.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        public const float FadeSeconds = 3f;
        private const int EffectVoices = 10;
        private const int MaxEffectsPerFrame = 4;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly SoundDirector director = new SoundDirector();
        private readonly List<SoundCue> pending = new List<SoundCue>();
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly AudioSource[] ambience = new AudioSource[2];
        private readonly AudioSource[] effects = new AudioSource[EffectVoices];
        private Task<List<SoundBuffer>> loading;
        private int nextVoice;
        private float musicFade = 1f;
        private float ambienceFade = 1f;

        public static string OverrideFolder => Path.Combine(Application.streamingAssetsPath, "Audio");

        /// <summary>Whether every sound has been made and handed to Unity.</summary>
        public bool Ready { get; private set; }

        public string MusicPlaying => music[0].clip != null ? music[0].clip.name : null;

        public string AmbiencePlaying => ambience[0].clip != null ? ambience[0].clip.name : null;

        public float MusicVolume => music[0].volume;

        public int EffectsPlayed { get; private set; }

        private void Awake()
        {
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }

            for (var i = 0; i < 2; i++)
            {
                music[i] = NewSource(loop: true);
                ambience[i] = NewSource(loop: true);
            }

            for (var i = 0; i < EffectVoices; i++)
            {
                effects[i] = NewSource(loop: false);
            }

            var folder = OverrideFolder;
            loading = Task.Run(() => SoundLibrary.Keys
                .Select(key => SoundLibrary.Load(key, k => ReadOverride(folder, k), problem => Debug.LogWarning($"[TinyCity] Sound: {problem}")))
                .ToList());
        }

        private AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private static byte[] ReadOverride(string folder, string key)
        {
            var path = Path.Combine(folder, key + ".wav");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>Called after each simulated minute: works out what was heard, to play next frame.</summary>
        public void Hear(TownSimulation simulation)
        {
            pending.AddRange(director.Update(simulation));
            if (pending.Count > 200)
            {
                pending.RemoveRange(0, pending.Count - 200);
            }
        }

        /// <summary>A new or loaded town: nothing it does before the next minute makes a sound.</summary>
        public void Forget()
        {
            director.Reset();
            pending.Clear();
        }

        public void PlayChime() => Play(SoundEffects.KeyOf(SoundEffect.Chime, 0), 1f, 1f);

        /// <summary>Called every frame with the game's state.</summary>
        public void Steer(TownSimulation simulation, CameraPanel active, TownView view, PlayerPreferences prefs)
        {
            if (!Ready)
            {
                if (loading == null || !loading.IsCompleted)
                {
                    return;
                }

                if (loading.IsFaulted)
                {
                    Debug.LogWarning($"[TinyCity] Sound could not be made: {loading.Exception?.GetBaseException().Message}");
                    loading = null;
                    return;
                }

                foreach (var buffer in loading.Result.Where(b => b != null))
                {
                    var clip = AudioClip.Create(buffer.Key, buffer.Samples.Length, 1, buffer.SampleRate, false);
                    clip.SetData(buffer.Samples, 0);
                    clips[buffer.Key] = clip;
                }

                loading = null;
                Ready = true;
            }

            var night = Lighting.At(simulation.Time, outdoors: true).WindowGlow > 0.5;
            var mapId = active?.MapId;
            var indoors = mapId != null && simulation.World.TryGetMap(mapId, out var map) && !map.IsExterior;
            var place = indoors ? AmbienceKind.Indoors : night ? AmbienceKind.CityNight : AmbienceKind.CityDay;

            musicFade = Crossfade(music, Music.KeyOf(night ? MusicPiece.Night : MusicPiece.Day), musicFade, (float)prefs.Volume(prefs.MusicVolume));
            ambienceFade = Crossfade(ambience, Ambience.KeyOf(place), ambienceFade, (float)prefs.Volume(prefs.AmbienceVolume));

            var effectsVolume = (float)prefs.Volume(prefs.EffectsVolume);
            var played = 0;
            foreach (var cue in pending)
            {
                if (played >= MaxEffectsPerFrame || effectsVolume <= 0f || active == null || cue.Location.MapId != mapId)
                {
                    continue;
                }

                // Louder the nearer they are to the middle of the panel; silent once well off screen.
                var size = Mathf.Max(1f, active.Camera.orthographicSize);
                var distance = Vector2.Distance(view.ToWorld(cue.Location), active.Camera.transform.position);
                var nearness = Mathf.Clamp01(1.4f - (distance / (size * 1.5f)));
                if (nearness <= 0.01f)
                {
                    continue;
                }

                var pitch = 0.94f + (0.12f * (float)Synth.Pick(cue.Variant + 7, (int)(simulation.Time.TotalMinutes % 100000)));
                Play(cue.Key, effectsVolume * Mathf.Min(1f, nearness) * (cue.Effect == SoundEffect.Footstep ? 0.6f : 1f), pitch);
                played++;
            }

            pending.Clear();
        }

        /// <summary>
        /// Keeps <paramref name="pair"/>[0] playing <paramref name="key"/>: when it changes, the old clip moves to
        /// [1] and fades out while the new one fades in. Returns the fade's progress.
        /// </summary>
        private float Crossfade(AudioSource[] pair, string key, float fade, float volume)
        {
            if (clips.TryGetValue(key, out var clip) && pair[0].clip != clip)
            {
                (pair[0], pair[1]) = (pair[1], pair[0]);
                pair[0].clip = clip;
                pair[0].time = 0f;
                pair[0].Play();
                fade = 0f;
            }

            fade = Mathf.Min(1f, fade + (Time.unscaledDeltaTime / FadeSeconds));
            pair[0].volume = volume * fade;
            pair[1].volume = volume * (1f - fade);
            if (fade >= 1f && pair[1].isPlaying)
            {
                pair[1].Stop();
            }

            return fade;
        }

        private void Play(string key, float volume, float pitch)
        {
            if (!clips.TryGetValue(key, out var clip) || volume <= 0f)
            {
                return;
            }

            var voice = effects[nextVoice];
            nextVoice = (nextVoice + 1) % effects.Length;
            voice.Stop();
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.Play();
            EffectsPlayed++;
        }
    }
}
