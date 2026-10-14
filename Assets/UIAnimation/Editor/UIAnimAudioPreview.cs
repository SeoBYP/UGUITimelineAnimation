using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>
    /// Plays clips in edit mode through Unity's internal AudioUtil (what the Inspector's clip preview uses);
    /// AudioSource does not play outside Play mode. Silently does nothing if the internal API changes.
    /// </summary>
    internal static class UIAnimAudioPreview
    {
        static readonly Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        static readonly MethodInfo PlayMethod = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
            null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        static readonly MethodInfo StopMethod = AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);

        public static void Play(AudioClip clip)
        {
            if (clip == null || PlayMethod == null) return;
            try { PlayMethod.Invoke(null, new object[] { clip, 0, false }); }
            catch (Exception) { /* preview audio is a convenience; never break the editor over it */ }
        }

        public static void StopAll()
        {
            if (StopMethod == null) return;
            try { StopMethod.Invoke(null, null); }
            catch (Exception) { }
        }
    }
}
