using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GenchiPhotoTool
{
    static class Program
    {
        internal const string Title = "現地調査写真整理ツール";
        // 作業用のフォルダは各自のパソコンに作る（共有フォルダに書き込まないように）
        internal static readonly string LocalDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenchiPhotoTool");
        internal static string LibDir;

        [STAThread]
        static void Main()
        {
            try { LibDir = ExtractLibraries(); }
            catch (Exception ex)
            {
                MessageBox.Show("起動の準備ができませんでした。\n\n" + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // EXE の横に WebView2 の DLL がないときは、取り出した DLL を使う
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                string path = Path.Combine(LibDir, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            Run();
        }

        // WebView2 の型を使うのは、DLL の読み込み先を決めたあと（このメソッドの中）から
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Run()
        {
            CoreWebView2Environment.SetLoaderDllFolderPath(LibDir);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        // EXE の中に入れてある WebView2 の DLL を、作業用のフォルダに取り出す（版と CPU の種類ごとのフォルダ）
        static string ExtractLibraries()
        {
            string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64"
                : Environment.Is64BitProcess ? "x64" : "x86";
            string dir = Path.Combine(LocalDir, "lib", Assembly.GetExecutingAssembly().GetName().Version + "-" + arch);
            Directory.CreateDirectory(dir);
            ExtractResource("lib.Microsoft.Web.WebView2.Core.dll", Path.Combine(dir, "Microsoft.Web.WebView2.Core.dll"));
            ExtractResource("lib.Microsoft.Web.WebView2.WinForms.dll", Path.Combine(dir, "Microsoft.Web.WebView2.WinForms.dll"));
            ExtractResource("native." + arch + ".WebView2Loader.dll", Path.Combine(dir, "WebView2Loader.dll"));
            return dir;
        }

        internal static void ExtractResource(string name, string path)
        {
            using (Stream src = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (src == null) throw new FileNotFoundException("アプリの中に " + name + " が見つかりません。");
                if (File.Exists(path) && new FileInfo(path).Length == src.Length) return; // 取り出し済み
                string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (Stream dst = File.Create(tmp)) src.CopyTo(dst);
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    File.Delete(tmp); // ほかに起動中のアプリが使っているときは、そのファイルをそのまま使う
                }
            }
        }
    }

    // 現地調査写真整理ツール（HTML）を、Edge の部品（WebView2）で表示するだけのアプリ。
    // ツールを「アプリの中だけの https のアドレス」で開くので、共有フォルダ（\\newjec\dfs\…）に
    // 置いたままでも、フォルダを選んで写真を読み書きする機能が使える。
    sealed class MainForm : Form
    {
        const string Host = "genchi-photo-tool.local"; // アプリの中だけで使うアドレス
        const string ToolName = "現地調査写真整理ツール.html";
        const string RuntimeUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

        readonly WebView2 web = new WebView2 { Dock = DockStyle.Fill };

        public MainForm()
        {
            Text = Program.Title;
            Size = new Size(1400, 900);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* アイコンがなくても動く */ }
            Controls.Add(web);
            Load += async (s, e) => await StartAsync();
        }

        async Task StartAsync()
        {
            try
            {
                string appDir = Path.Combine(Program.LocalDir, "app");
                Directory.CreateDirectory(appDir);
                // ツール本体は EXE の中に入っている。毎回取り出し直して、EXE と同じ版を使う
                string target = Path.Combine(appDir, ToolName);
                if (File.Exists(target)) File.Delete(target);
                Program.ExtractResource("tool.html", target);

                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Program.LocalDir, "WebView2"));
                await web.EnsureCoreWebView2Async(env);
                CoreWebView2 core = web.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(Host, appDir, CoreWebView2HostResourceAccessKind.Allow);

                // 選んだフォルダへの書き込み（名前の変更・データの保存・Word の保存）は、確認を出さずに許可する。
                // フォルダを選ぶこと自体は、これまでどおり使う人がフォルダ選択の画面で行う。
                core.PermissionRequested += (s, e) =>
                {
                    if (e.PermissionKind == CoreWebView2PermissionKind.FileReadWrite && IsTool(e.Uri))
                        e.State = CoreWebView2PermissionState.Allow;
                };
                // 説明の中のリンク（国土地理院のページなど）は、ふだんのブラウザで開く
                core.NewWindowRequested += (s, e) => { e.Handled = true; OpenInBrowser(e.Uri); };
                core.NavigationStarting += (s, e) =>
                {
                    if (!IsTool(e.Uri)) { e.Cancel = true; OpenInBrowser(e.Uri); }
                };
                core.DocumentTitleChanged += (s, e) => Text = string.IsNullOrEmpty(core.DocumentTitle) ? Program.Title : core.DocumentTitle;

                core.Navigate("https://" + Host + "/" + Uri.EscapeDataString(ToolName));
            }
            catch (WebView2RuntimeNotFoundException)
            {
                if (MessageBox.Show(this,
                        "このパソコンには、アプリが使う「Microsoft Edge WebView2 ランタイム」が入っていません。\n\n" +
                        "ダウンロードのページを開きますか？（入れるには、パソコン管理担当の許可が必要なことがあります）",
                        Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    OpenInBrowser(RuntimeUrl);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "起動できませんでした。\n\n" + ex.Message, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        static bool IsTool(string uri) =>
            uri != null && uri.StartsWith("https://" + Host + "/", StringComparison.OrdinalIgnoreCase);

        static void OpenInBrowser(string uri)
        {
            if (uri == null || !(uri.StartsWith("https://") || uri.StartsWith("http://"))) return;
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { /* 開けなくても続ける */ }
        }
    }
}
