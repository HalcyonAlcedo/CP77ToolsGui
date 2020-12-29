using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AutoUpdaterDotNET;

namespace CP77ToolsGui
{
    public delegate void DelReadStdOutput(string result);
    public delegate void DelReadErrOutput(string result);

    public partial class MainWindow
    {
        [DllImport("user32.dll")]

        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref BlurBehind.WindowCompositionAttributeData data);
        private uint _blurOpacity;
        public double BlurOpacity
        {
            get { return _blurOpacity; }
            set { _blurOpacity = (uint)value; EnableBlur(); }
        }

        private uint _blurBackgroundColor = 0x990000; /* BGR color format */
        private string _currentLan = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            Init();
            _currentLan = "zh-cn";
        }
        private static Window GetActiveWindow() => Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            EnableBlur();
            string url = "https://api.github.com/repos/WolvenKit/CP77Tools/releases";
            string getJson = HttpUitls.Get(url);
            JArray ret = (JArray)JsonConvert.DeserializeObject(getJson);
            JObject githubInfo = (JObject)ret[0];

            string versionInfo = githubInfo["tag_name"].ToString();
            string downloadUrl = githubInfo["assets"][0]["browser_download_url"].ToString();

            AutoUpdater.Start("http://kit.alcedo.top/AutoUpdater.xml");

            try
            {
                if (File.Exists("ToolsVersion"))
                {
                    if (File.ReadAllText("ToolsVersion") != versionInfo)
                    {

                        HandyControl.Controls.Growl.Warning(Application.Current.FindResource("c_发现工具更新").ToString(), "InfoMessage");
                    }
                }
            }
            catch (Exception ex)
            {
                consoleBox.Inlines.Add(new Run(ex.Message + "\r\n"));
            }
        }
        private void EnableBlur()
        {
            var windowHelper = new WindowInteropHelper(this);

            var accent = new BlurBehind.AccentPolicy
            {
                AccentState = BlurBehind.AccentState.ACCENT_ENABLE_BLURBEHIND,
                //GradientColor = (int) ((_blurOpacity << 24) | (_blurBackgroundColor & 0xFFFFFF))
            };

            var accentStructSize = Marshal.SizeOf(accent);

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new BlurBehind.WindowCompositionAttributeData
            {
                Attribute = BlurBehind.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }

        public event DelReadStdOutput ReadStdOutput;
        public event DelReadErrOutput ReadErrOutput;

        private void Init()
        {
            //3.将相应函数注册到委托事件中
            ReadStdOutput += new DelReadStdOutput(ReadStdOutputAction);
            ReadErrOutput += new DelReadErrOutput(ReadErrOutputAction);
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                for (int path = 0; path < ((System.Array)e.Data.GetData(DataFormats.FileDrop)).Length; path++)
                {
                    string dropPath = "";
                    dropPath = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(path).ToString();
                    if (File.Exists(dropPath))
                    {
                        string filepath = dropPath;
                        DoArchive(filepath);
                    }
                    else if (Directory.Exists(dropPath))
                    {
                        List<string> fl = new List<string>() { };
                        fl = GetFiles(dropPath, fl);
                        for (int i = 0; i < fl.Count; i++)
                        {
                            string filepath = fl[i];
                            DoArchive(filepath);
                        }
                    }
                }
            }
        }

        private void ArchiveFile(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "档案文件(*.archive)|*.archive|所有文件(*.*)|*.*";
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string filepath = dlg.FileName;
                DoArchive(filepath);
            }
        }
        private void DumpFile(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "档案文件(*.archive)|*.archive|所有文件(*.*)|*.*";
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string filepath = dlg.FileName;
                //DoDump(filepath);
                DoGetPacketList(filepath);
            }
        }
        private void Cr2wFile(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "档案文件(*.cr2w)|*.archive|所有文件(*.*)|*.*";
            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                string filepath = dlg.FileName;
                DoCr2w(filepath);
            }
        }
        private void PackFile(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string filepath = dialog.FileName;
                DoPack(filepath);
            }
        }
        private void ExecuteClick(object sender, RoutedEventArgs e)
        {
            PerformUnpack(ArgumentsText.Text);
        }
        public List<string> GetFiles(string path, List<string> FileList)
        {
            string filename;
            DirectoryInfo dir = new DirectoryInfo(path);
            FileInfo[] fil = dir.GetFiles();
            DirectoryInfo[] dii = dir.GetDirectories();
            foreach (FileInfo f in fil)
            {
                filename = f.FullName;
                FileList.Add(filename);
            }
            foreach (DirectoryInfo d in dii)
            {
                GetFiles(d.FullName, FileList);
            }
            return FileList;
        }

        private void DoArchive(string filepath)
        {
            if(outFilePath.Text != "")
            {
                if (CreateDirectory.IsChecked == true)
                    ArgumentsText.Text = "archive -o \"" + outFilePath.Text + "\\" + Path.GetFileNameWithoutExtension(filepath)  + "\" -p \"" + filepath + "\" ";
                else
                    ArgumentsText.Text = "archive -o \"" + outFilePath.Text + "\" -p \"" + filepath + "\" ";
            }
            else
                ArgumentsText.Text = "archive -p \"" + filepath + "\" ";

            if (ScreenFile.Text != "")
            {
                ArgumentsText.Text += "-e -w " + ScreenFile.Text + " ";
            }
            else
                ArgumentsText.Text += "-e ";

            if (Texture.SelectedItem != null && Texture.SelectedIndex >= 1)
            {
                ArgumentsText.Text += "-u --uext " + Texture.SelectionBoxItem + " ";
            }

            if (ParametersOnly.IsChecked == false)
                PerformUnpack(ArgumentsText.Text);
        }

        private void DoDump(string filepath)
        {
            ArgumentsText.Text = "dump -i -p \"" + filepath + "\" ";
            consoleBox.Inlines.Add(new Run(Application.Current.FindResource("c_包信息保存目录提示").ToString() + "\r\n"));
            if (ParametersOnly.IsChecked == false)
                PerformUnpack(ArgumentsText.Text);
        }
        private void DoCr2w(string filepath)
        {
            if (outFilePath.Text != "")
                ArgumentsText.Text = "cr2w -i -o \"" + outFilePath.Text + "\" -p \"" + filepath + "\" ";
            else
                ArgumentsText.Text = "cr2w -i -p \"" + filepath + "\" ";

            if (ParametersOnly.IsChecked == false)
                PerformUnpack(ArgumentsText.Text);
        }
        private void DoPack(string filepath)
        {
            if (outFilePath.Text != "")
                ArgumentsText.Text = "pack -o \"" + outFilePath.Text + "\" -p \"" + filepath + "\" ";
            else
                ArgumentsText.Text = "pack -p \"" + filepath + "\" ";

            if (ParametersOnly.IsChecked == false)
                PerformUnpack(ArgumentsText.Text);
        }
        private void DoGetPacketList(string filepath)
        {
            PacketList PacketList = new PacketList(filepath);
            PacketList.Show();
            PacketList.SxEvent += PacketList_SxEvent;
        }
        private void PacketList_SxEvent(string leIn, string filepath)
        {
            if (outFilePath.Text != "")
            {
                if (CreateDirectory.IsChecked == true)
                    ArgumentsText.Text = "archive -o \"" + outFilePath.Text + "\\" + Path.GetFileNameWithoutExtension(filepath) + "\" -p \"" + filepath + "\" ";
                else
                    ArgumentsText.Text = "archive -o \"" + outFilePath.Text + "\" -p \"" + filepath + "\" ";
            }
            else
                ArgumentsText.Text = "archive -p \"" + filepath + "\" ";

            ArgumentsText.Text += "-e " + leIn + " ";


            if (Texture.SelectedItem != null && Texture.SelectedIndex >= 1)
            {
                ArgumentsText.Text += "-u --uext " + Texture.SelectionBoxItem + " ";
            }

            if (ParametersOnly.IsChecked == false)
                PerformUnpack(ArgumentsText.Text);
        }
        private void SavePathClick(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                outFilePath.Text = dialog.FileName;
            }
        }
        private void PerformUnpack(string StartFileArg)
        {
            consoleBox.Inlines.Clear();
            if (!File.Exists("CP77Tools/CP77Tools.exe"))
            {
                consoleBox.Inlines.Add(new Run() { Text = Application.Current.FindResource("c_未找到CP77Tools").ToString() + "\r\n", Foreground = new SolidColorBrush(Colors.Red) });
                return;
            }
            if (!File.Exists("CP77Tools/oo2ext_7_win64.dll"))
            {
                consoleBox.Inlines.Add(new Run() { Text = Application.Current.FindResource("c_未找到dll").ToString() + "\r\n", Foreground = new SolidColorBrush(Colors.Red)});
                return;
            }
            /*
            if (!File.Exists("CP77Tools/Resources/archivehashes.csv"))
            {
                consoleBox.Inlines.Add(new Run() { Text = "错误：未找到archivehashes.csv文件，可能是由于当前CP77tools版本问题，您可以从https://raw.githubusercontent.com/WolvenKit/CP77Tools/main/CP77Tools/Resources/archivehashes.zip手动下载后复制到CP77Tools/Resources/文件夹中。" + "\r\n", Foreground = new SolidColorBrush(Colors.Red) });
                return;
            }
            */
            if (!GetDotNetRelease(5))
            {
                consoleBox.Inlines.Add(new Run() { Text = Application.Current.FindResource("c_未安装net").ToString() + "\r\n", Foreground = new SolidColorBrush(Colors.Red)});
                return;
            }
            executionLoading.Visibility = Visibility.Visible;
            DoRun_Button.IsEnabled = false;
            DoArchive_Button.IsEnabled = false;
            DoUpdateTools_Button.IsEnabled = false;
            DoDump_Button.IsEnabled = false;
            DoPack_Button.IsEnabled = false;
            string Arguments = StartFileArg;
            consoleBox.Inlines.Add(new Run(Application.Current.FindResource("c_开始执行操作").ToString() + "\r\n"));
            RealAction("CP77Tools/CP77Tools.exe", Arguments);

        }
        private void RealAction(string StartFileName, string StartFileArg)
        {
            Process CmdProcess = new Process();
            CmdProcess.StartInfo.FileName = StartFileName;      // 命令
            CmdProcess.StartInfo.Arguments = StartFileArg;      // 参数

            if(ShowCMD.IsChecked != true)
            CmdProcess.StartInfo.CreateNoWindow = true;         // 不创建新窗口
            //CmdProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            CmdProcess.StartInfo.UseShellExecute = false;

            if (ShowCMD.IsChecked != true)
            {
                CmdProcess.StartInfo.RedirectStandardInput = true;  // 重定向输入
                CmdProcess.StartInfo.RedirectStandardOutput = true; // 重定向标准输出
                CmdProcess.StartInfo.RedirectStandardError = true;  // 重定向错误输出
                CmdProcess.OutputDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);
                CmdProcess.ErrorDataReceived += new DataReceivedEventHandler(p_ErrorDataReceived);
            }

            CmdProcess.EnableRaisingEvents = true;                      // 启用Exited事件
            CmdProcess.Exited += new EventHandler(CmdProcess_Exited);   // 注册进程结束事件

            CmdProcess.Start();
            if (ShowCMD.IsChecked != true)
            {
                CmdProcess.BeginOutputReadLine();
                CmdProcess.BeginErrorReadLine();
            }
            
            // 如果打开注释，则以同步方式执行命令，此例子中用Exited事件异步执行。
            // CmdProcess.WaitForExit();     
        }

        private void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                // 4. 异步调用，需要invoke
                Dispatcher.Invoke(ReadStdOutput, new object[] { e.Data });
            }
        }

        private void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Dispatcher.Invoke(ReadErrOutput, new object[] { e.Data });
            }
        }

        private void ReadStdOutputAction(string result)
        {
            consoleBox.Inlines.Add(new Run(result + "\r\n"));
        }

        private void ReadErrOutputAction(string result)
        {
            consoleBox.Inlines.Add(new Run() { Text = result + "\r\n", Foreground = new SolidColorBrush(Colors.Red) });
        }

        private void CmdProcess_Exited(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(delegate
            {
                consoleBox.Inlines.Add(new Run(){ Text = Application.Current.FindResource("c_操作完成").ToString()+" \r\n", Foreground = new SolidColorBrush(Colors.Blue) });
                executionLoading.Visibility = Visibility.Hidden;
                DoRun_Button.IsEnabled = true;
                DoArchive_Button.IsEnabled = true;
                DoUpdateTools_Button.IsEnabled = true;
                DoDump_Button.IsEnabled = true;
                DoPack_Button.IsEnabled = true;
            }));
            // 执行结束后触发
        }


        //提示消息
        private void StackPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ContextMenu menu = (sender as StackPanel).ContextMenu;
            MenuItem item = menu.Items[0] as MenuItem;
            item.Header = Resources["Clear"];
        }
        //NET版本检查
        private static bool GetDotNetRelease(int release)
        {
            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";
            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
            {
                if (ndpKey != null && ndpKey.GetValue("Release") != null)
                {
                    return (int)ndpKey.GetValue("Release") >= release ? true : false;
                }
                return false;
            }
        }
        //更新工具
        private void UpdateToolsClick(object sender, RoutedEventArgs e)
        {
            executionLoading.Visibility = Visibility.Visible;
            DoRun_Button.IsEnabled = false;
            DoArchive_Button.IsEnabled = false;
            DoUpdateTools_Button.IsEnabled = false;
            DoDump_Button.IsEnabled = false;
            DoPack_Button.IsEnabled = false;
            consoleBox.Inlines.Add(new Run(Application.Current.FindResource("c_获取更新").ToString() + " \r\n"));
            string url = "https://api.github.com/repos/WolvenKit/CP77Tools/releases";
            string getJson = HttpUitls.Get(url);
            JArray ret = (JArray)JsonConvert.DeserializeObject(getJson);
            JObject githubInfo = (JObject)ret[0];
            
            string versionInfo = githubInfo["tag_name"].ToString();
            string downloadUrl = githubInfo["assets"][0]["browser_download_url"].ToString();

            //文件路径
            string ToolsVersionPath = "ToolsVersion";

            try
            {
                if (File.Exists(ToolsVersionPath))
                {
                    if(File.ReadAllText(ToolsVersionPath) == versionInfo)
                    {
                        consoleBox.Inlines.Add(new Run(){ Text = Application.Current.FindResource("c_无更新上").ToString() + versionInfo + Application.Current.FindResource("c_无更新下").ToString() + " \r\n", Foreground = new SolidColorBrush(Colors.Blue) });
                        executionLoading.Visibility = Visibility.Hidden;
                        DoRun_Button.IsEnabled = true;
                        DoArchive_Button.IsEnabled = true;
                        DoUpdateTools_Button.IsEnabled = true;
                        DoDump_Button.IsEnabled = true;
                        DoPack_Button.IsEnabled = true;
                    }
                    else
                    {
                        consoleBox.Inlines.Add(new Run(downloadUrl + Application.Current.FindResource("c_下载中").ToString() + " \r\n"));
                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFileAsync(new Uri(downloadUrl.Trim()), "CP77Tools.zip");
                            client.DownloadProgressChanged += client_DownloadProgressChanged;
                            client.DownloadFileCompleted += client_DownloadFileCompleted;
                        }
                        File.WriteAllText(ToolsVersionPath, versionInfo);
                    }
                }
                else
                {
                    consoleBox.Inlines.Add(new Run(downloadUrl + Application.Current.FindResource("c_下载中").ToString() + " \r\n"));
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFileAsync(new Uri(downloadUrl.Trim()), "CP77Tools.zip");
                        client.DownloadProgressChanged += client_DownloadProgressChanged;
                        client.DownloadFileCompleted += client_DownloadFileCompleted;
                    }
                    File.WriteAllText(ToolsVersionPath, versionInfo);
                }
            }
            catch (Exception ex)
            {
                consoleBox.Inlines.Add(new Run(ex.Message + "\r\n"));
                executionLoading.Visibility = Visibility.Hidden;
                DoRun_Button.IsEnabled = true;
                DoArchive_Button.IsEnabled = true;
                DoUpdateTools_Button.IsEnabled = true;
                DoDump_Button.IsEnabled = true;
                DoPack_Button.IsEnabled = true;
            }
        }
        private void UpdateTools(object sender, RoutedEventArgs e)
        {
            AutoUpdater.Start("http://kit.alcedo.top/AutoUpdater.xml");
        }
        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            progress.Inlines.Clear();
            progress.Inlines.Add(new Run(Application.Current.FindResource("c_下载进度上").ToString() + e.BytesReceived + Application.Current.FindResource("c_下载进度中").ToString() + e.TotalBytesToReceive + Application.Current.FindResource("c_下载进度下").ToString()));
        }

        void client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                consoleBox.Inlines.Add(new Run(Application.Current.FindResource("c_下载取消").ToString() + "\r\n"));
                File.Delete("ToolsVersion");
            }
            else
            {
                consoleBox.Inlines.Add(new Run(Application.Current.FindResource("c_下载成功").ToString() +  " \r\n"));
                progress.Inlines.Clear();
                if (Directory.Exists(Directory.GetCurrentDirectory() + "/CP77Tools"))
                {
                    if (File.Exists("CP77Tools/oo2ext_7_win64.dll"))
                    {
                        File.Copy("CP77Tools/oo2ext_7_win64.dll", "oo2ext_7_win64.dll", true);
                    }
                    DelectDir(Directory.GetCurrentDirectory() + "/CP77Tools");
                }
                /*
                if (ZipHelper.UnZip("CP77Tools.zip", Directory.GetCurrentDirectory() + "/CP77Tools"))
                {
                    consoleBox.Inlines.Add(new Run() { Text = "更新完成。 \r\n", Foreground = new SolidColorBrush(Colors.Blue) });
                }
                else
                {
                    consoleBox.Inlines.Add(new Run() { Text = "更新失败！ \r\n", Foreground = new SolidColorBrush(Colors.Red) });
                    File.Delete("ToolsVersion");
                }
                */
                try
                {
                    ZipFile.ExtractToDirectory("CP77Tools.zip", "CP77Tools");
                    if (File.Exists("oo2ext_7_win64.dll"))
                    {
                        File.Move("oo2ext_7_win64.dll", "CP77Tools/oo2ext_7_win64.dll");
                    }
                    consoleBox.Inlines.Add(new Run() { Text = Application.Current.FindResource("c_更新完成").ToString() +  " \r\n", Foreground = new SolidColorBrush(Colors.Blue) });
                }
                catch (Exception ex)
                {
                    consoleBox.Inlines.Add(new Run() { Text = Application.Current.FindResource("c_更新失败").ToString() +  " \r\n", Foreground = new SolidColorBrush(Colors.Red) });
                    if (File.Exists("oo2ext_7_win64.dll"))
                    {
                        File.Delete("oo2ext_7_win64.dll");
                    }
                    File.Delete("ToolsVersion");
                }
            }
            
            executionLoading.Visibility = Visibility.Hidden;
            DoRun_Button.IsEnabled = true;
            DoArchive_Button.IsEnabled = true;
            DoUpdateTools_Button.IsEnabled = true;
            DoDump_Button.IsEnabled = true;
            DoPack_Button.IsEnabled = true;
        }
        public static void DelectDir(string srcPath)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(srcPath);
                FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();  //返回目录中所有文件和子目录
                foreach (FileSystemInfo i in fileinfo)
                {
                    if (i is DirectoryInfo)            //判断是否文件夹
                    {
                        DirectoryInfo subdir = new DirectoryInfo(i.FullName);
                        subdir.Delete(true);          //删除子目录和文件
                    }
                    else
                    {
                        File.Delete(i.FullName);      //删除指定文件
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        private void cp77_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.nexusmods.com/cyberpunk2077/mods/8");
        }
        private void my_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.caimogu.net/user/11301.html");
        }
        private void caimogu_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.caimogu.net/circle/293.html");
        }
        //切换语言
        private void LanguageSwitch(object sender, RoutedEventArgs e)
        {
            ResourceDictionary dict = new ResourceDictionary();
            if (_currentLan == "zh-cn")
            {
                dict.Source = new Uri(@"Resources/Language/en-us.xaml", UriKind.Relative);
                _currentLan = "en-us";
            }
            else
            {
                dict.Source = new Uri(@"Resources/Language/zh-cn.xaml", UriKind.Relative);
                _currentLan = "zh-cn";
            }
            Application.Current.Resources.MergedDictionaries[2] = dict;
        }

        private void OpenSetting(object sender, RoutedEventArgs e)
        {
            ToolsSetting.IsOpen = true;
        }
    }
}
