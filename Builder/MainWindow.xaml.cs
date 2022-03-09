using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace Builder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged = null;
        
        ///===========================================================================
        ///   Properties
        ///===========================================================================
        public string UnityExePath
        {
            get => _UnityExePath;
            protected set
            {
                _UnityExePath = value;
                OnPropertyChanged();
            }
        }
        string _UnityExePath = string.Empty;

        public string UnityProjPath
        {
            get => _UnityProjPath;
            protected set
            {
                _UnityProjPath = value;
                OnPropertyChanged();
            }
        }
        string _UnityProjPath = string.Empty;

        public string UnityOutputPath
        {
            get => _UnityOutputPath;
            protected set
            {
                _UnityOutputPath = value;
                OnPropertyChanged();
            }
        }
        string _UnityOutputPath = string.Empty;

        public string AdditionalBatFile
        {
            get => _AdditionalBatFile;
            protected set
            {
                _AdditionalBatFile = value;
                OnPropertyChanged();
            }
        }
        string _AdditionalBatFile = string.Empty;

        public bool IsRunning
        {
            get => _IsRunning;
            protected set
            {
                _IsRunning = value;
                IsNotRunning = !IsRunning;
                OnPropertyChanged();
            }
        }
        bool _IsRunning = false;

        public bool IsNotRunning
        {
            get => !IsRunning;
            set
            {
                _IsNotRunning = value;
                OnPropertyChanged();
            }
        }
        bool _IsNotRunning = true;

        public List<string> UnitySceneFiles
        {
            get => _UnitySceneFiles;
            protected set
            {
                _UnitySceneFiles = value;
                OnPropertyChanged();
            }
        }
        List<string> _UnitySceneFiles = new List<string>();

        public string? VdfFilePath
        {
            get => _VdfFilePath;
            set
            {
                _VdfFilePath = value;
                OnPropertyChanged();
            }
        }
        string? _VdfFilePath = null;

        ///<summary> 不能在非 UI 线程中访问原始的 UI 数据，所以要复制一份 </summary>
        public string VdfDesc { get; set; }

        public string ProjectName => System.IO.Path.GetFileName(UnityProjPath);

        ///===========================================================================
        ///   Fields
        ///===========================================================================
        CancellationTokenSource m_CancelToken = new CancellationTokenSource();

        ///===========================================================================
        ///   DefineMethods
        ///===========================================================================
        public MainWindow()
        {
            this.DataContext = this;
            LoadSetting();
            InitializeComponent();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void LoadSetting()
        {
            var setting = Properties.Settings.Default;
            setting.Reload();

            UnityExePath = setting.UnityExePath;
            UnityProjPath = setting.UnityProjPath;

            if (setting.UnitySeneFiles is { } savedSceneFiles)
            {
                var sceneFiles = new List<string>();
                foreach (var file in savedSceneFiles)
                {
                    sceneFiles.Add(file);
                }
                UnitySceneFiles = sceneFiles;
            }

            UnityOutputPath = setting.UnityOutputPath;
            AdditionalBatFile = setting.AdditionalBatFile;
            VdfFilePath = setting.VdfFilePath;
        }

        private void SaveSetting()
        {
            var setting = Properties.Settings.Default;
            setting.UnityProjPath = UnityProjPath;
            setting.UnityExePath = UnityExePath;

            var sceneFileCollection = new StringCollection();
            foreach (var file in UnitySceneFiles)
            {
                sceneFileCollection.Add(file);
            }
            setting.UnitySeneFiles = sceneFileCollection;
            setting.UnityOutputPath = UnityOutputPath;
            setting.AdditionalBatFile = AdditionalBatFile;
            setting.VdfFilePath = VdfFilePath;

            setting.Save();
        }

        private void BtnSelectUnityExe_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 Unity 执行文件",
                Filter = "Unity执行文件（*.exe）|*.exe",
                Multiselect = false,
                InitialDirectory = string.IsNullOrEmpty(UnityExePath) ? string.Empty : UnityExePath,
            };

            if (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK
                && !string.IsNullOrEmpty(dialog.FileName))
            {
                /// exe 文件名称判断
                var match = Regex.Match(dialog.SafeFileName, ".*[Uu]nity.*.exe$");
                if (!match.Success)
                {
                    MessageBox.Show("非 Unity 执行文件!");
                    return;
                }

                UnityExePath = dialog.FileName;
                SaveSetting();
            }
        }

        private void BtnSelectProjPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = false,
                InitialDirectory = string.IsNullOrEmpty(UnityProjPath) ? string.Empty : UnityProjPath,
            };

            if (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK
                && !string.IsNullOrEmpty(dialog.SelectedPath))
            {
                UnityProjPath = dialog.SelectedPath;
                SaveSetting();
            }
        }

        private void BtnBuild_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UnityExePath))
            {
                Logout("未指定 Unity 执行文件路径");
                return; 
            }

            if (string.IsNullOrEmpty(UnityProjPath))
            {
                Logout("未指定游戏工程文件路径");
                return;
            }

            if (string.IsNullOrEmpty(UnityOutputPath))
            {
                Logout("未指定输出路径");
                return;
            }

            BuildWin64();
        }

        ///<returns> 脚本文件最终的路径 </returns>
        private string PrepareScriptFileDest()
        {
            /// 确认脚本复制终点
            string scriptPath = $@"{UnityProjPath}\Assets\Editor\Temp";
            if (!Directory.Exists(scriptPath))
            {
                Directory.CreateDirectory(scriptPath);
            }

            /// 确认是否有重名脚本
            return $@"{scriptPath}\tempBuilder.cs";
        }

        ///<returns> 输出路径的最终完全路径（.exe文件路径） </returns>
        private string PrepareOutputDest(string _suffix)
        {
            string output;
            if (IfAutoCreateSubfolder.IsChecked is true)
            {
                /// 追加时间戳
                DateTime now = DateTime.Now;
                string stamp = $"{now:yyyyMMdd}_{now:HHmm}";
                output = $"{UnityOutputPath}\\{_suffix}_{stamp}";
            }
            else
            {
                output = $"{UnityOutputPath}";
            }

            Directory.CreateDirectory(output);
            return output + $@"\{ProjectName}.exe";
        }

        private void BuildWin64()
        {
            /// 如果不构建到子文件夹，先清除残留的上次构建文件
            if (IfAutoCreateSubfolder.IsChecked is false)
            {

                string[] unityFileNames = new string[]
                {
                    $"{ProjectName}.exe",
                    "UnityPlayer.dll",
                    "UnityCrashHandler64.exe",
                };
                var files = Directory.GetFiles(UnityOutputPath);
                foreach (var file in files)
                {
                    if (unityFileNames.Contains(Path.GetFileName(file)))
                    {
                        File.Delete(file);
                    }
                }

                string[] unityDirectoryNames = new string[]
                {
                    "MonoBleedingEdge",
                    $"{ProjectName}_Data",
                };
                var directories = Directory.GetDirectories(UnityOutputPath);
                foreach (var directory in directories)
                {
                    if (unityDirectoryNames.Contains(Path.GetFileName(directory)))
                    {
                        Directory.Delete(directory, true);
                    }
                }
            }

            Logout("构建开始");
            string cmd = $@"/c ""{UnityExePath}"" -quit "
                            + $"-batchmode "
                            + $"-projectPath {UnityProjPath} "
                            + $"-buildWindows64Player {UnityOutputPath}\\{ProjectName}.exe";

            var info = new ProcessStartInfo()
            {
                FileName = "cmd",
                Arguments = cmd,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = UnityProjPath
            };
            RunProcess(info, OnCompeletedBuild);
        }

        private void OnCompeletedBuild(object? _sender, EventArgs _args)
        {
            if (_sender is not Process p) return;
            if (p.ExitCode is 0)
            {
                var time = Math.Round((p.ExitTime - p.StartTime).TotalMilliseconds);
                Dispatcher.Invoke(() => Logout($"构建成功。耗时 {time} 毫秒"));
                RunAdditionBatFile();
            }
            else
            {
                Dispatcher.Invoke(() => Logout($"构建失败。退出码 {p.ExitCode}"));
            }
        }

        private void RunProcess(ProcessStartInfo _info, Action<object?, EventArgs> _onCompleted)
        {
            try
            {
                IsRunning = true;
                var task = Task.Run(_Run);

                ///-----------------
                ///> 本地函数
                void _Run()
                {
                    if (_info is null) return;

                    var p = Process.Start(_info);
                    if (p is null) return;
                    p.EnableRaisingEvents = true;
                    if (_onCompleted is not null)
                    {
                        p.Exited += (s, args) =>
                        {
                            IsRunning = false;
                            _onCompleted(s, args); 
                        };
                    }    
                    
                    if (_info.RedirectStandardOutput)
                    {
                        p.OutputDataReceived += LogoutCMDOutput;
                        p.BeginOutputReadLine();
                    }

                    if (_info.RedirectStandardError)
                    {
                        p.ErrorDataReceived += LogoutCMDOutput;
                        p.BeginErrorReadLine();
                    }
                }

                ///---------------------------------------------------------------
                void LogoutCMDOutput(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Dispatcher.Invoke(() => Logout(e.Data));
                    }
                };
                ///< 本地函数
                ///-----------------
            }
            catch (Exception e)
            {
                WriteCrashLog(e);
            }
        }

        private void WriteCrashLog(Exception e)
        {
            Dispatcher.Invoke(() => Logout(e.Message));
            var now = DateTime.Now;
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory
                              + $"/crash_{now:yyyyMMdd}_{now:HHmm}.txt", tbxInfo.Text);
        }

        private void RunAdditionBatFile()
        {
            /// 修改 VDF 文本描述
            try
            {
                if (!string.IsNullOrWhiteSpace(VdfDesc)
                    && File.Exists(VdfFilePath))
                {
                    var lines = File.ReadAllLines(VdfFilePath);
                    /// 找出里面开头为 "desc" 的一行，替换后方文本
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Trim().StartsWith("\"desc\""))
                        {
                            lines[i] = $@"    ""desc"" ""{VdfDesc}""";
                            break;
                        }
                    }
                    /// 回写 VDF 文件
                    File.WriteAllLines(VdfFilePath, lines);
                    Dispatcher.Invoke(() => Logout($"修改 VDF 文件描述为 {VdfDesc}"));
                }

            }
            catch (Exception e)
            {
                WriteCrashLog(e);
            }

            /// 运行脚本
            if (!string.IsNullOrEmpty(AdditionalBatFile)
                && File.Exists(AdditionalBatFile))
            {
                Dispatcher.Invoke(() => Logout("运行附加脚本"));
                var info = new ProcessStartInfo()
                {
                    FileName = "cmd",
                    Arguments = $"/k {AdditionalBatFile}",
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(AdditionalBatFile)
                };

                RunProcess(info, (e, a) => 
                { 
                    Dispatcher.Invoke(() => Logout("附加脚本运行完成"));
                });
            }
        }

        private void Logout(string str)
        {
            tbxInfo.Text += $"{DateTime.Now} {str}\n";
        }

        private void BtnSelectUnityScenes_Click(object sender, RoutedEventArgs e)
        {
            string initDir = string.Empty;
            if (UnitySceneFiles.Count is 0)
            {
                if (string.IsNullOrEmpty(UnityProjPath) is false)
                {
                    initDir = UnityProjPath; 
                }
            }
            else
            {
                initDir = Path.GetDirectoryName(UnitySceneFiles[0]);
            }
            var dialog = new OpenFileDialog
            {
                Title = "选择 Unity 场景文件",
                Filter = "场景文件（*.unity）|*.unity",
                Multiselect = true,
                InitialDirectory = initDir,
            };

            if (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK
                && dialog.FileNames.Length != 0)
            {
                UnitySceneFiles = dialog.FileNames.ToList();
            }
        }

        private void BtnSelectOutputPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = true,
                InitialDirectory = string.IsNullOrEmpty(UnityOutputPath) ? string.Empty : UnityOutputPath,
            };

            if (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK
                && !string.IsNullOrEmpty(dialog.SelectedPath))
            {
                UnityOutputPath = dialog.SelectedPath;
                SaveSetting();
            }
        }

        private void BtnOpenOutputDir_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(UnityOutputPath)) return;
            Process.Start("explorer.exe", UnityOutputPath);
        }

        private string GetRanomdString(uint _length)
        {
            Random res = new();
            string str = "abcdefghijklmnopqrstuvwxyz0123456789";
            string randomstring = "";
            for (int i = 0; i < _length; i++)
            {
                int x = res.Next(str.Length);
                randomstring += str[x];
            }
            return randomstring;
        }

        private void BtnGitPull_Click(object sender, RoutedEventArgs e)
        {
            Logout("开始执行 Git pull");
            var info = new ProcessStartInfo()
            {
                FileName = "git",
                Arguments = "pull",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = UnityProjPath
            };

            RunProcess(info, OnCompleted); 

            /// 本地函数
            void OnCompleted(object? sender, EventArgs arg)
            {
               Dispatcher.Invoke(() => Logout("完成 Git pull"));
            }
        }

        private void BtnAdditionalBat_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                Title = "选择构建后自动执行的 bat 文件",
                Filter = "批处理文件（*.bat）|*.bat",
                Multiselect = false
            };

            if (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK
                && dialog.FileName.Length != 0)
            {
                AdditionalBatFile = dialog.FileName;
                SaveSetting();
            }
        }

        private void BtnClearAdditionBat_Click(object sender, RoutedEventArgs e)
        {
            AdditionalBatFile = null;
        }

        private void CbxWatchGit_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void CbxWatchGit_Unchecked(object sender, RoutedEventArgs e)
        {

            
        }

        private void BtnSelectVdfFile_Click(object sender, RoutedEventArgs e)
        {
            string initPath = string.Empty;
            if (string.IsNullOrEmpty(VdfFilePath))
            {
                if (!string.IsNullOrEmpty(AdditionalBatFile))
                {
                    initPath = AdditionalBatFile; 
                }
            }
            else
            {
                initPath = VdfFilePath;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "VDF文件（*.vdf）|*.vdf",
                Title = "选择使用的 VDF 文件",
                Multiselect = false,
                InitialDirectory = initPath
            };

            if (dialog.ShowDialog() is System.Windows.Forms.DialogResult.OK
                && !string.IsNullOrEmpty(dialog.FileName))
            {
                VdfFilePath = dialog.FileName;
                SaveSetting();
            }
        }

        private void BtnClearVdfFile_Click(object sender, RoutedEventArgs e)
        {
            VdfFilePath = null;
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            /// 修改 VDF 文本描述
            if (!string.IsNullOrWhiteSpace(VdfDesc)
                && File.Exists(VdfFilePath))
            {
                var lines = File.ReadAllLines(VdfFilePath);
                /// 找出里面开头为 "desc" 的一行，替换后方文本
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim().StartsWith("\"desc\""))
                    {
                        lines[i] = $"\t\"desc\" \"{VdfDesc}\"";
                        break;
                    }
                }
                /// 回写 VDF 文件
                File.WriteAllLines(VdfFilePath, lines);
                Logout($"修改 VDF 文件描述为 {VdfDesc}");
            }
        }

        private void BtnRunAdditionBat_Click(object sender, RoutedEventArgs e)
        {
            RunAdditionBatFile();
        }
    }
}
