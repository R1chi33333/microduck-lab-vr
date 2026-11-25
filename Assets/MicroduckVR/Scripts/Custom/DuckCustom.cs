using System;
using System.Collections.Generic;
using MicroduckVR.Audio;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using UnityEngine;

namespace MicroduckVR.Custom
{
    /// <summary>
    /// The player's duck as customised in the mirror (port of customise.js): a colourway, optional colourways per
    /// part, and one of four voices. Remembered between sessions.
    /// </summary>
    [Serializable]
    public class DuckCustom
    {
        public string skin = "cream";
        public string head = "", body = "", legs = "", feet = "";
        public int voice;

        public static readonly string[] SkinIds = { "cream", "graphite", "lavender", "sky" };
        public static readonly string[] PartIds = { "head", "body", "legs", "feet" };
        public static readonly (string name, int seed)[] Voices = { ("CHIRPY", 3), ("SQUEAKY", 41), ("WARBLY", 77), ("HONKY", 112) };
        const string Key = "microduck_custom_v1";

        public string Part(string p) => p switch { "head" => head, "body" => body, "legs" => legs, "feet" => feet, _ => "" };
        public void SetPart(string p, string v) { switch (p) { case "head": head = v; break; case "body": body = v; break; case "legs": legs = v; break; case "feet": feet = v; break; } }

        public static DuckCustom Load()
        {
            try { var j = PlayerPrefs.GetString(Key, ""); if (!string.IsNullOrEmpty(j)) return JsonUtility.FromJson<DuckCustom>(j) ?? new DuckCustom(); } catch { }
            return new DuckCustom();
        }
        public void Save() { try { PlayerPrefs.SetString(Key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); } catch { } }

        public void Randomise(System.Random r)
        {
            skin = SkinIds[r.Next(SkinIds.Length)];
            foreach (var p in PartIds) SetPart(p, r.Next(3) == 0 ? SkinIds[r.Next(SkinIds.Length)] : "");
            voice = r.Next(Voices.Length);
        }

        /// <summary>Paint the duck and give it the voice.</summary>
        public void ApplyTo(GameObject duck, bool withVoice = true)
        {
            var setup = duck.GetComponent<DuckSetup>();
            if (setup != null && setup.Body != null && setup.Body.Model.childCount > 0)
            {
                setup.skin = skin;
                var parts = new Dictionary<string, string>();
                foreach (var p in PartIds) parts[p] = Part(p);
                ComicLook.ApplyCustom(setup.Body.Model.GetChild(0), skin, parts);
            }
            var au = duck.GetComponent<DuckAudio>();
            if (withVoice && au != null) au.SetVoice(Voices[Mathf.Clamp(voice, 0, Voices.Length - 1)].seed);
        }
    }
}
