using System;
using System.Collections.Generic;
using System.Linq;
using R2API.Networking.Interfaces;
using UnityEngine;
using UnityEngine.Networking;

namespace Jukebox
{
    [Serializable]
    public class Song
    {
        public string id = "", title = "", artist = "", file = "";

        [NonSerialized] public string Path = "";
        public bool Downloaded => Path.Length > 0 && System.IO.File.Exists(Path);
        public string Label => string.IsNullOrEmpty(artist) ? (string.IsNullOrEmpty(title) ? id : title) : (title + " - " + artist);
    }

    [Serializable]
    public class Playlist
    {
        public Song[] songs = new Song[0];
    }

    /// <summary>
    /// The songs on this PC: %APPDATA%\BrokenCharacters\Jukebox\playlist.json (written by the launcher), plus any
    /// .mp3/.wav dropped into that folder by hand (named after the file).
    /// </summary>
    public static class SongLibrary
    {
        public static readonly string Folder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters", "Jukebox");

        public static List<Song> Songs = new List<Song>();
        private static DateTime _stamp;

        public static Song Find(string id) => string.IsNullOrEmpty(id) ? null : Songs.FirstOrDefault(s => s.id == id);

        public static void Reload()
        {
            try
            {
                if (!System.IO.Directory.Exists(Folder)) { Songs = new List<Song>(); return; }
                DateTime stamp = System.IO.Directory.GetLastWriteTimeUtc(Folder);
                string list = System.IO.Path.Combine(Folder, "playlist.json");
                if (System.IO.File.Exists(list)) { DateTime t = System.IO.File.GetLastWriteTimeUtc(list); if (t > stamp) stamp = t; }
                if (stamp == _stamp && Songs.Count > 0) return;
                _stamp = stamp;

                var songs = new List<Song>();
                if (System.IO.File.Exists(list))
                {
                    Playlist p = JsonUtility.FromJson<Playlist>(System.IO.File.ReadAllText(list));
                    foreach (Song s in p?.songs ?? new Song[0])
                    {
                        if (s == null || string.IsNullOrEmpty(s.id) || songs.Any(o => o.id == s.id)) continue;
                        string file = string.IsNullOrEmpty(s.file) ? s.id + ".mp3" : s.file;
                        s.Path = System.IO.Path.Combine(Folder, System.IO.Path.GetFileName(file));
                        songs.Add(s);
                    }
                }
                foreach (string f in System.IO.Directory.GetFiles(Folder))
                {
                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".mp3" && ext != ".wav") continue;
                    string id = System.IO.Path.GetFileNameWithoutExtension(f);
                    if (songs.Any(o => o.id == id || string.Equals(o.Path, f, StringComparison.OrdinalIgnoreCase))) continue;
                    songs.Add(new Song { id = id, title = id.Replace('_', ' '), Path = f });
                }
                Songs = songs;
            }
            catch (Exception e)
            {
                JukeboxPlugin.Log?.LogWarning("Jukebox couldn't read the song list: " + e.Message);
            }
        }
    }

    // ====================================================================== network messages

    /// <summary>Host → everyone: a vote opened with these songs.</summary>
    public class JbVoteStartMessage : INetMessage
    {
        public string[] Ids = new string[0], Titles = new string[0];
        public float Seconds;

        public void Serialize(NetworkWriter w)
        {
            w.Write((byte)Ids.Length);
            for (int i = 0; i < Ids.Length; i++) { w.Write(Ids[i] ?? ""); w.Write(Titles[i] ?? ""); }
            w.Write(Seconds);
        }

        public void Deserialize(NetworkReader r)
        {
            int n = r.ReadByte();
            Ids = new string[n]; Titles = new string[n];
            for (int i = 0; i < n; i++) { Ids[i] = r.ReadString(); Titles[i] = r.ReadString(); }
            Seconds = r.ReadSingle();
        }

        public void OnReceived() { if (!NetworkServer.active) Handle(); }
        public void Handle() => JukeboxPlugin.ApplyVoteStart(Ids, Titles, Seconds);
    }

    /// <summary>Player → host: my vote.</summary>
    public class JbBallotMessage : INetMessage
    {
        public uint Voter;
        public int Choice;
        public void Serialize(NetworkWriter w) { w.Write(Voter); w.Write(Choice); }
        public void Deserialize(NetworkReader r) { Voter = r.ReadUInt32(); Choice = r.ReadInt32(); }
        public void OnReceived() => JukeboxPlugin.HostReceiveBallot(Voter, Choice);
    }

    /// <summary>Host → everyone: the current vote counts.</summary>
    public class JbTallyMessage : INetMessage
    {
        public int[] Counts = new int[0];

        public void Serialize(NetworkWriter w)
        {
            w.Write((byte)Counts.Length);
            foreach (int c in Counts) w.Write(c);
        }

        public void Deserialize(NetworkReader r)
        {
            Counts = new int[r.ReadByte()];
            for (int i = 0; i < Counts.Length; i++) Counts[i] = r.ReadInt32();
        }

        public void OnReceived() { if (!NetworkServer.active) Handle(); }
        public void Handle() => JukeboxPlugin.VoteCounts = Counts;
    }

    /// <summary>Host → everyone: play this song (empty id = stop).</summary>
    public class JbPlayMessage : INetMessage
    {
        public string Id = "", Title = "";
        public void Serialize(NetworkWriter w) { w.Write(Id ?? ""); w.Write(Title ?? ""); }
        public void Deserialize(NetworkReader r) { Id = r.ReadString(); Title = r.ReadString(); }
        public void OnReceived() { if (!NetworkServer.active) Handle(); }
        public void Handle() => JukeboxPlugin.ApplyPlay(Id, Title);
    }

    /// <summary>Player → host: please open a vote.</summary>
    public class JbRequestMessage : INetMessage
    {
        public void Serialize(NetworkWriter w) { w.Write((byte)0); }
        public void Deserialize(NetworkReader r) { r.ReadByte(); }
        public void OnReceived() => JukeboxPlugin.HostStartVote();
    }
}
