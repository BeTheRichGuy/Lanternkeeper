using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Lanternkeeper.Sim;
using UnityEngine;

namespace Lanternkeeper.View
{
    /// <summary>
    /// Writes <see cref="SaveData"/> as JSON inside a checksummed envelope (GDD §19 Save/).
    /// Writes go to a temp file first and then replace the main file, so a crash mid-write never leaves a half file.
    /// Up to three rotating backups; loading falls back to the newest backup that passes its checksum.
    /// </summary>
    public sealed class SaveSystem
    {
        public const string MainFile = "save.json";
        public const int Backups = 3;

        [Serializable]
        sealed class Envelope
        {
            public int version;
            public string checksum;
            public string payload;
        }

        public readonly string Dir;

        public SaveSystem(string dir)
        {
            Dir = dir;
            Directory.CreateDirectory(dir);
        }

        /// <summary>The Editor uses its own folder so fast-forwarded play sessions never touch the player build's save.</summary>
        public static string DefaultDir => Path.Combine(Application.persistentDataPath, Application.isEditor ? "saves-editor" : "saves");

        public enum LoadResult { NoSave, Loaded, AllUnreadable }

        string PathOf(string name) => Path.Combine(Dir, name);
        string BackupName(int i) => $"save.bak{i}.json";

        public static string Encode(SaveData data)
        {
            string payload = JsonUtility.ToJson(data);
            var env = new Envelope { version = data.Version, checksum = Hash(payload), payload = payload };
            return JsonUtility.ToJson(env, true);
        }

        public static bool TryDecode(string text, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            try
            {
                var env = JsonUtility.FromJson<Envelope>(text);
                if (env == null || string.IsNullOrEmpty(env.payload) || env.checksum != Hash(env.payload)) return false;
                data = JsonUtility.FromJson<SaveData>(env.payload);
                if (data == null) return false;
                if (IsFromNewerBuild(data)) { data = null; return false; } // keep it intact; quarantine instead of downgrading
                Migrate(data);
                return true;
            }
            catch (ArgumentException) { return false; } // malformed JSON
        }

        /// <summary>Version upgrades go here. v1 is the first format; JsonUtility already fills new fields with defaults.</summary>
        static void Migrate(SaveData d)
        {
            if (d.Version < SaveData.CurrentVersion) d.Version = SaveData.CurrentVersion;
        }

        /// <summary>A save written by a newer build is refused rather than rewritten in the old format (losing fields).</summary>
        public static bool IsFromNewerBuild(SaveData d) => d.Version > SaveData.CurrentVersion;

        static string Hash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Saves; with <paramref name="rotateBackup"/> the previous main file becomes backup 1 first.</summary>
        public void Save(SaveData data, DateTime utcNow, bool rotateBackup)
        {
            data.SavedAtUnix = new DateTimeOffset(utcNow).ToUnixTimeSeconds();
            string text = Encode(data);
            string main = PathOf(MainFile), tmp = main + ".tmp";
            if (rotateBackup && File.Exists(main))
            {
                for (int i = Backups; i >= 2; i--)
                {
                    var from = PathOf(BackupName(i - 1));
                    if (File.Exists(from)) File.Copy(from, PathOf(BackupName(i)), true);
                }
                File.Copy(main, PathOf(BackupName(1)), true);
            }
            // Flush to disk before the swap so a power cut can't leave a renamed-but-empty main file.
            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
            if (!File.Exists(main)) { File.Move(tmp, main); return; }
            // ReplaceFile fails (1175/1176) while antivirus or sync tools hold the target: retry, then fall back to a copy.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try { File.Replace(tmp, main, null); return; }
                catch (IOException) { System.Threading.Thread.Sleep(60); }
                catch (UnauthorizedAccessException) { System.Threading.Thread.Sleep(60); }
            }
            File.Copy(tmp, main, true);
            File.Delete(tmp);
        }

        /// <summary>The newest valid save (main, then backups 1..3), or null. <paramref name="source"/> names the file used.</summary>
        public SaveData Load(out string source) => Load(out source, out _);

        public SaveData Load(out string source, out LoadResult result)
        {
            source = null;
            bool anyFile = false;
            foreach (var name in Candidates())
            {
                var path = PathOf(name);
                if (!File.Exists(path)) continue;
                anyFile = true;
                string text;
                try { text = File.ReadAllText(path, Encoding.UTF8); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                if (TryDecode(text, out var data)) { source = name; result = LoadResult.Loaded; return data; }
                Debug.LogWarning($"[Lanternkeeper] save '{name}' failed its checksum or is unreadable; trying the next backup");
            }
            result = anyFile ? LoadResult.AllUnreadable : LoadResult.NoSave;
            return null;
        }

        /// <summary>
        /// Moves every save file into <c>corrupt-&lt;time&gt;/</c> so a fresh game can never overwrite progress
        /// that only failed to load (locked file, bad sector, newer format). Returns the folder used.
        /// </summary>
        public string Quarantine(DateTime utcNow)
        {
            var dir = Path.Combine(Dir, "corrupt-" + utcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            foreach (var name in Candidates())
            {
                var path = PathOf(name);
                if (!File.Exists(path)) continue;
                try { File.Copy(path, Path.Combine(dir, name), true); File.Delete(path); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return dir;
        }

        System.Collections.Generic.IEnumerable<string> Candidates()
        {
            yield return MainFile;
            for (int i = 1; i <= Backups; i++) yield return BackupName(i);
        }
    }
}
