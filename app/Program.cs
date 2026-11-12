using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GenchiPhotoTool
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // 現地調査写真整理ツール（HTML）を、Edge の部品（WebView2）で表示するだけのアプリ。
    // ツールを「アプリの中だけの https のアドレス」で開くので、共有フォルダ（\\newjec\dfs\…）に
    // 置いたままでも、フォルダを選んで写真を読み書きする機能が使える。
    sealed class MainForm : Form
    {
        const string Title = "現地調査写真整理ツール";
        const string Host = "genchi-photo-tool.local"; // アプリの中だけで使うアドレス
        // 外部の部品がいらない版を優先して開く
        static readonly string[] ToolFiles = { "現地調査写真整理ツール_部品入り.html", "現地調査写真整理ツール.html" };
        const string RuntimeUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

        readonly WebView2 web = new WebView2 { Dock = DockStyle.Fill };

        public MainForm()
        {
            Text = Title;
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
                // アプリと同じフォルダ（共有フォルダでもよい）にあるツールの HTML
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                // 決まった名前がなければ（ZIP の展開でファイル名が文字化けした場合など）、いちばん大きい HTML を使う
                string tool = ToolFiles.Select(f => Path.Combine(exeDir, f)).FirstOrDefault(File.Exists)
                    ?? Directory.GetFiles(exeDir, "*.html").OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
                if (tool == null)
                {
                    Fail("ツールの HTML ファイルが見つかりません。\n\n「" + ToolFiles[0] + "」を、このアプリ（GenchiPhotoTool.exe）と同じフォルダに置いてください。");
                    return;
                }

                // 作業用のフォルダは各自のパソコンに作る（共有フォルダに書き込まないように）
                string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenchiPhotoTool");
                string appDir = Path.Combine(local, "app");
                Directory.CreateDirectory(appDir);
                // 起動のたびに、共有フォルダの最新の HTML をコピーして使う
                foreach (string f in Directory.GetFiles(exeDir, "*.html"))
                    File.Copy(f, Path.Combine(appDir, Path.GetFileName(f)), true);

                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(local, "WebView2"));
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
                core.DocumentTitleChanged += (s, e) => Text = string.IsNullOrEmpty(core.DocumentTitle) ? Title : core.DocumentTitle;

                core.Navigate("https://" + Host + "/" + Uri.EscapeDataString(Path.GetFileName(tool)));
            }
            catch (WebView2RuntimeNotFoundException)
            {
                if (MessageBox.Show(this,
                        "このパソコンには、アプリが使う「Microsoft Edge WebView2 ランタイム」が入っていません。\n\n" +
                        "ダウンロードのページを開きますか？（入れるには、パソコン管理担当の許可が必要なことがあります）",
                        Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    OpenInBrowser(RuntimeUrl);
                Close();
            }
            catch (Exception ex)
            {
                Fail("起動できませんでした。\n\n" + ex.Message);
            }
        }

        static bool IsTool(string uri) =>
            uri != null && uri.StartsWith("https://" + Host + "/", StringComparison.OrdinalIgnoreCase);

        static void OpenInBrowser(string uri)
        {
            if (uri == null || !(uri.StartsWith("https://") || uri.StartsWith("http://"))) return;
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { /* 開けなくても続ける */ }
        }

        void Fail(string message)
        {
            MessageBox.Show(this, message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }
}
