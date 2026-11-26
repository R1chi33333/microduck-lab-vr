using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.Audio
{
    /// <summary>
    /// Procedural duck sounds (in the spirit of voice.js / sfx.js): each duck gets its own chirp timbre from its
    /// seed (pitch, sweep, vibrato, buzz), synthesised once into AudioClips; footsteps are short noise ticks whose
    /// colour depends on the surface. Everything plays from 3D sources on the duck.
    /// </summary>
    [RequireComponent(typeof(DuckSetup))]
    public class DuckAudio : MonoBehaviour
    {
        public int seed = 1;
        public float volume = 0.6f;
        public bool idleChirps = true;

        AudioSource voice, steps;
        AudioClip[] chirps; AudioClip hello, boing, splash;
        AudioClip stepWood, stepTile, stepSoft, stepGrass;
        DuckBody body; DuckBrain brain;
        float nextIdle;
        System.Random rng;

        const int SR = 44100;

        void Start()
        {
            rng = new System.Random(seed * 7919 + 13);
            var s = GetComponent<DuckSetup>(); body = s.Body; brain = GetComponent<DuckBrain>();
            voice = MakeSource("voice", 1f); steps = MakeSource("steps", 0.5f);
            BuildVoice();
            boing = Chirp(260f, 2.2f, 0.18f, 0.05f, 0f);
            splash = Noise(0.22f, 0.6f, 0.9f);
            stepWood = Noise(0.035f, 0.35f, 0.5f); stepTile = Noise(0.03f, 0.85f, 0.55f);
            stepSoft = Noise(0.045f, 0.12f, 0.3f); stepGrass = Noise(0.05f, 0.5f, 0.35f);
            body.OnSound += OnBodySound;
            if (brain != null) brain.OnEvent += (d, kind, ctx) =>
            {
                if (kind == "got" || kind == "drop") Chirp();
                else if (kind == "bed") voice.PlayOneShot(boing, volume * 0.7f);
                else if (kind == "water") voice.PlayOneShot(splash, volume * 0.8f);
            };
            nextIdle = Time.time + 3f + (float)rng.NextDouble() * 10f;
        }

        /// <summary>Give the duck another voice (customising): rebuilds the chirps from a new seed.</summary>
        public void SetVoice(int newSeed)
        {
            seed = newSeed;
            if (voice == null) return;          // not started yet: Start builds it from the seed
            BuildVoice(); Hello();
        }

        void BuildVoice()
        {
            var vr = new System.Random(seed * 7919 + 13);
            // timbre from the seed
            float basePitch = 700f + (float)vr.NextDouble() * 500f;      // Hz
            float buzz = 0.15f + (float)vr.NextDouble() * 0.35f;          // second harmonic
            float vib = 6f + (float)vr.NextDouble() * 10f;
            chirps = new[]
            {
                Chirp(basePitch, 1.35f, 0.12f, buzz, vib),
                Chirp(basePitch * 1.1f, 0.8f, 0.10f, buzz, vib),
                Chirp(basePitch * 0.95f, 1.6f, 0.16f, buzz, vib),
            };
            hello = Concat(Chirp(basePitch, 1.4f, 0.09f, buzz, vib), Silence(0.05f), Chirp(basePitch * 1.2f, 1.5f, 0.11f, buzz, vib));
        }

        AudioSource MakeSource(string n, float vol)
        {
            var go = new GameObject(n); go.transform.SetParent(body != null ? body.transform : transform, false);
            go.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            var a = go.AddComponent<AudioSource>();
            a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = 0.15f; a.maxDistance = 8f;
            a.dopplerLevel = 0f; a.playOnAwake = false; a.volume = vol;
            return a;
        }

        void OnBodySound(BodySound kind, object data)
        {
            if (kind != BodySound.Plant) return;
            var surf = SurfaceTag.At(body.Pos);
            string t = surf != null ? surf.type : "wood";
            var clip = t == "tile" || t == "table" ? stepTile : t == "rug" || t == "bed" ? stepSoft : t == "grass" ? stepGrass : stepWood;
            steps.pitch = 0.9f + (float)rng.NextDouble() * 0.25f;
            steps.PlayOneShot(clip, volume * 0.45f);
        }

        public void Chirp() => voice.PlayOneShot(chirps[rng.Next(chirps.Length)], volume);
        public void Hello() => voice.PlayOneShot(hello, volume);

        void Update()
        {
            if (!idleChirps || brain == null) return;
            if (Time.time > nextIdle) { Chirp(); nextIdle = Time.time + 6f + (float)rng.NextDouble() * 12f; }
        }

        // ------------------------------------------------------------------ synthesis
        AudioClip Chirp(float f0, float sweep, float dur, float buzz, float vibHz)
        {
            int n = Mathf.CeilToInt(dur * SR); var d = new float[n]; double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, u = t / dur;
                float f = f0 * Mathf.Lerp(1f, sweep, Mathf.Sin(u * Mathf.PI * 0.5f)) * (1f + 0.03f * Mathf.Sin(t * vibHz * Mathf.PI * 2f));
                ph += 2 * Mathf.PI * f / SR;
                float env = Mathf.Min(1f, u * 18f) * Mathf.Pow(1f - u, 1.6f);
                d[i] = env * (0.75f * (float)System.Math.Sin(ph) + buzz * (float)System.Math.Sin(ph * 2) + 0.08f * (float)System.Math.Sin(ph * 3)) * 0.6f;
            }
            var c = AudioClip.Create("chirp", n, 1, SR, false); c.SetData(d, 0); return c;
        }

        AudioClip Noise(float dur, float bright, float vol)
        {
            int n = Mathf.CeilToInt(dur * SR); var d = new float[n]; float lp = 0f;
            var r = new System.Random(rng.Next());
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n, x = (float)(r.NextDouble() * 2 - 1);
                lp += (x - lp) * Mathf.Lerp(0.05f, 0.9f, bright);         // brighter = less low-pass
                d[i] = lp * vol * Mathf.Pow(1f - u, 3f);
            }
            var c = AudioClip.Create("noise", n, 1, SR, false); c.SetData(d, 0); return c;
        }

        static AudioClip Silence(float dur) { int n = Mathf.CeilToInt(dur * SR); var c = AudioClip.Create("sil", n, 1, SR, false); c.SetData(new float[n], 0); return c; }

        static AudioClip Concat(params AudioClip[] clips)
        {
            int n = 0; foreach (var c in clips) n += c.samples;
            var d = new float[n]; int o = 0;
            foreach (var c in clips) { var b = new float[c.samples]; c.GetData(b, 0); b.CopyTo(d, o); o += b.Length; }
            var r = AudioClip.Create("concat", n, 1, SR, false); r.SetData(d, 0); return r;
        }
    }
}
