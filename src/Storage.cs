using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Saldo {
    public sealed class Preferences {
        public string Theme = "light", Mode = "orb", Organization = "";
        public double Left = 70, Top = 100;
        public int RefreshSeconds = 120;
    }
    public static class Storage {
        public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Saldo");
        static string TokenFile { get { return Path.Combine(Root, "session.dpapi"); } }
        public static Preferences Load() {
            try { return new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(Path.Combine(Root, "preferences.json"))) ?? new Preferences(); }
            catch { return new Preferences(); }
        }
        public static void Save(Preferences preferences) {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "preferences.json"), new JavaScriptSerializer().Serialize(preferences));
        }
        public static string ReadToken() {
            if (!File.Exists(TokenFile)) return "";
            byte[] plain = null;
            try { plain = ProtectedData.Unprotect(File.ReadAllBytes(TokenFile), null, DataProtectionScope.CurrentUser); return Encoding.UTF8.GetString(plain); }
            catch { return ""; }
            finally { if (plain != null) Array.Clear(plain, 0, plain.Length); }
        }
        public static void SaveToken(string token) {
            Directory.CreateDirectory(Root);
            byte[] plain = Encoding.UTF8.GetBytes(token);
            try { File.WriteAllBytes(TokenFile, ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser)); }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
        public static void ForgetToken() { if (File.Exists(TokenFile)) File.Delete(TokenFile); }
    }
}
