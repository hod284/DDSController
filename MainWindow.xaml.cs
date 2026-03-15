// RTI DDS Controller v3.0 — Connext DDS 7.6.0
// ImplicitUsings=disable → using 명시 필수

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using SysIO = System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// WPF — 충돌 없이 명시
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

using Microsoft.Win32;   // OpenFileDialog (WPF 전용)

// RTI Connext DDS 7.6.0
using Rti.Dds.Domain;
using Rti.Dds.Publication;
using Rti.Dds.Subscription;
using Rti.Dds.Topics;
using Rti.Types.Dynamic;

namespace DDSController
{
    // ─────────────────────────────────────────────────
    // THEME
    // ─────────────────────────────────────────────────
    public class DdsTheme
    {
        public string Name, Hex;
        public Color Chip, BgDeep, BgPanel, BgCard, Border,
                     Accent, Accent2, Warn, Purple, Text, TextDim;

        public static readonly List<DdsTheme> All = new List<DdsTheme>()
        {
            new DdsTheme(){ Name="Cyber Dark", Hex="#00E5FF", Chip=C("#00E5FF"),
                BgDeep=C("#0A0E1A"),BgPanel=C("#0F1628"),BgCard=C("#161D35"),Border=C("#1A2840"),
                Accent=C("#00E5FF"),Accent2=C("#00FF88"),Warn=C("#FF6B35"),
                Purple=C("#7B61FF"),Text=C("#E8EDF5"),TextDim=C("#6B7A99") },
            new DdsTheme(){ Name="Navy Blue", Hex="#4D9FFF", Chip=C("#4D9FFF"),
                BgDeep=C("#04080F"),BgPanel=C("#07111F"),BgCard=C("#0B1A30"),Border=C("#112244"),
                Accent=C("#4D9FFF"),Accent2=C("#3DFFA0"),Warn=C("#FF7043"),
                Purple=C("#9B79FF"),Text=C("#DCE8FF"),TextDim=C("#5070A0") },
            new DdsTheme(){ Name="Matrix", Hex="#00FF41", Chip=C("#00FF41"),
                BgDeep=C("#010A01"),BgPanel=C("#021002"),BgCard=C("#041804"),Border=C("#0A2A0A"),
                Accent=C("#00FF41"),Accent2=C("#39FF14"),Warn=C("#FFAA00"),
                Purple=C("#CC44FF"),Text=C("#CCFFCC"),TextDim=C("#336633") },
            new DdsTheme(){ Name="Amber", Hex="#FFB300", Chip=C("#FFB300"),
                BgDeep=C("#0D0800"),BgPanel=C("#150E00"),BgCard=C("#1E1500"),Border=C("#2A1E00"),
                Accent=C("#FFB300"),Accent2=C("#FFE066"),Warn=C("#FF5733"),
                Purple=C("#CC88FF"),Text=C("#FFF8DC"),TextDim=C("#806020") },
            new DdsTheme(){ Name="Crimson", Hex="#FF4466", Chip=C("#FF4466"),
                BgDeep=C("#0F0008"),BgPanel=C("#190010"),BgCard=C("#22001A"),Border=C("#380028"),
                Accent=C("#FF4466"),Accent2=C("#FF88AA"),Warn=C("#FFA000"),
                Purple=C("#BB66FF"),Text=C("#FFE8EE"),TextDim=C("#804060") },
            new DdsTheme(){ Name="Light", Hex="#0070CC", Chip=C("#0070CC"),
                BgDeep=C("#F0F4FA"),BgPanel=C("#FFFFFF"),BgCard=C("#E8EDF5"),Border=C("#CBD4E0"),
                Accent=C("#0070CC"),Accent2=C("#00AA55"),Warn=C("#E05000"),
                Purple=C("#6633CC"),Text=C("#1A2030"),TextDim=C("#7080A0") },
        };

        public static Color C(string h)
        {
            h = h.TrimStart('#');
            return Color.FromRgb(
                System.Convert.ToByte(h.Substring(0,2), 16),
                System.Convert.ToByte(h.Substring(2,2), 16),
                System.Convert.ToByte(h.Substring(4,2), 16));
        }
        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    // ─────────────────────────────────────────────────
    // LOG COLOR CONVERTER
    // ─────────────────────────────────────────────────
    public class LogColorConverter : IValueConverter
    {
        public static readonly LogColorConverter Instance = new LogColorConverter();
        static DdsTheme _t = DdsTheme.All[0];
        public static void SetTheme(DdsTheme t) => _t = t;

        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            var s = v?.ToString() ?? "";
            if (s.Contains("[TX]"))   return new SolidColorBrush(_t.Accent2);
            if (s.Contains("[RX]"))   return new SolidColorBrush(_t.Accent);
            if (s.Contains("[ERR]"))  return new SolidColorBrush(Color.FromRgb(255,80,80));
            if (s.Contains("[QoS]"))  return new SolidColorBrush(_t.Purple);
            if (s.Contains("[TYPE]")) return new SolidColorBrush(Color.FromRgb(255,200,60));
            if (s.Contains("[OK]"))   return new SolidColorBrush(_t.Accent2);
            if (s.Contains("[DDS]"))  return new SolidColorBrush(_t.Warn);
            if (s.Contains("[WARN]")) return new SolidColorBrush(Color.FromRgb(255,200,0));
            return new SolidColorBrush(_t.TextDim);
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => throw new NotImplementedException();
    }

    // ─────────────────────────────────────────────────
    // FIELD DESCRIPTOR
    // ─────────────────────────────────────────────────
    public class DdsField
    {
        public string Name     { get; set; }
        public string CsType   { get; set; }
        public bool   IsArray  { get; set; }
        public int    ArraySize{ get; set; }
        public object Value    { get; set; }

        public bool IsBool    => CsType == "bool";
        public bool IsNumeric => CsType == "int"  || CsType == "long"  || CsType == "float"  ||
                                 CsType == "double"|| CsType == "short" || CsType == "byte"   ||
                                 CsType == "uint"  || CsType == "ulong";
        public bool IsString  => CsType == "string";

        public string DdsType
        {
            get
            {
                switch(CsType)
                {
                    case "bool":   return "boolean";
                    case "int":    return "long";
                    case "long":   return "long long";
                    case "float":  return "float";
                    case "double": return "double";
                    case "string": return "string";
                    case "byte":   return "octet";
                    case "short":  return "short";
                    case "uint":   return "unsigned long";
                    case "ulong":  return "unsigned long long";
                    default:       return CsType;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────
    // TOPIC MODEL
    // ─────────────────────────────────────────────────
    public class DdsTopic
    {
        public string TopicName { get; set; }
        public string TypeName  { get; set; }
        public List<DdsField> Fields { get; set; } = new List<DdsField>();
        public long TxCount { get; set; }
        public long RxCount { get; set; }
    }

    // ─────────────────────────────────────────────────
    // CS PARSER
    // ─────────────────────────────────────────────────
    public static class CsParser
    {
        static readonly Dictionary<string,string> Map = new Dictionary<string,string>
        {
            {"System.Boolean","bool"},{"Boolean","bool"},{"System.Int32","int"},{"Int32","int"},
            {"System.Int64","long"},{"Int64","long"},{"System.Single","float"},{"Single","float"},
            {"System.Double","double"},{"Double","double"},{"System.String","string"},{"String","string"},
            {"System.Byte","byte"},{"Byte","byte"},{"System.Int16","short"},{"Int16","short"},
            {"System.UInt32","uint"},{"UInt32","uint"},{"System.UInt64","ulong"},{"UInt64","ulong"},
        };

        static bool Known(string t) =>
            t=="bool"||t=="int"||t=="long"||t=="float"||t=="double"||
            t=="string"||t=="byte"||t=="short"||t=="uint"||t=="ulong";

        static object Def(string t)
        {
            if(t=="bool")   
                return false;
           
            if(t=="float")  
                return 0.0f;
            
            if(t=="double") 
                return 0.0;
            
            if(t=="string") 
                return "";

            return 0;
        }

        public static (string typeName, List<DdsField> fields) Parse(string src)
        {
            var fields = new List<DdsField>();
            var cm = Regex.Match(src, @"public\s+class\s+(\w+)\s*(?::|{)");
            string tn = cm.Success ? cm.Groups[1].Value : "UnknownType";

            var fp = new Regex(@"public\s+([\w]+(?:\[\])?)\s+(\w+)\s*(?:;|=\s*[^;]+;)", RegexOptions.Multiline);
         
            foreach (Match m in fp.Matches(src))
            {
                var raw  = m.Groups[1].Value.Trim();
                var name = m.Groups[2].Value.Trim();
                if (name.ToUpper()==name && name.Length>2) continue;
                if (name.StartsWith("_") || name=="value__") continue;
                if (name.StartsWith("TYPE") || name.StartsWith("NDDS")) continue;
                bool arr  = raw.Contains("[]");
                string cl = raw.Replace("[]","").Replace("?","").Trim();
                string mp;
                if (Map.TryGetValue(cl, out mp)) cl = mp;
                cl = cl.ToLower();
                if (!Known(cl) || fields.Any(f=>f.Name==name)) continue;
                fields.Add(new DdsField{ Name=name, CsType=cl, IsArray=arr, Value=Def(cl) });
            }

            if (fields.Count==0)
            {
                var pp = new Regex(@"public\s+([\w]+)\s+(\w+)\s*\{\s*get\s*;\s*set\s*;\s*\}", RegexOptions.Multiline);
                foreach (Match m in pp.Matches(src))
                {
                    var raw  = m.Groups[1].Value.Trim();
                    var name = m.Groups[2].Value.Trim();
                    string mp2;
                    if (Map.TryGetValue(raw, out mp2)) raw = mp2;
                    raw = raw.ToLower();
                    if (!Known(raw)) continue;
                    fields.Add(new DdsField{ Name=name, CsType=raw, Value=Def(raw) });
                }
            }
            return (tn, fields);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MAIN WINDOW
    // ═══════════════════════════════════════════════════════════════════════
    public partial class MainWindow : Window
    {
        // ── 앱 상태 ──
        bool   _connected     = false;
        string _idlPath       = "";
        string _csPath        = "";
        string _qosPath       = "";
        string _qosApplied    = "";
        string _rtiddsgenPath = "";
        string _typeName      = "MyType";
        List<DdsField> _parsedFields = new List<DdsField>();
        DdsTheme _theme = DdsTheme.All[0];

        // ── RTI DDS 엔티티 ──
        DomainParticipant       _participant;
        Publisher               _publisher;
        Subscriber              _subscriber;
        Topic<DynamicData>      _ddsTopic;
        DataWriter<DynamicData> _writer;
        DataReader<DynamicData> _reader;
        DynamicType             _dynamicType;

        // ── 토픽/UI ──
        List<DdsTopic> _topics = new List<DdsTopic>();
        DdsTopic _active = null;
        Dictionary<string,Button> _tabBtns = new Dictionary<string,Button>();

        CancellationTokenSource _pubCts;
        bool _pubLoop = false, _subLoop = false;
        long _tx = 0, _rx = 0;

        readonly ObservableCollection<string> _log = new ObservableCollection<string>();

        // ════════════════════
        // INIT
        // ════════════════════
        public MainWindow()
        {
            InitializeComponent();
            LogListBox.ItemsSource = _log;
            BuildPalette();
            ApplyTheme(DdsTheme.All[0]);
            AddTopic("MyTopic");
            Log("[DDS] RTI DDS Controller v3.0 (Connext 7.6.0)");
            Log("[DDS] 순서: ① IDL 원스텝  ② QoS APPLY  ③ Domain ID  ④ CONNECT");
        }

        // ════════════════════
        // THEME 팔레트 칩
        // ════════════════════
        void BuildPalette()
        {
            ThemePalette.Children.Clear();
            foreach (var t in DdsTheme.All)
            {
                var th = t;
                var chip = new Button
                {
                    Width=22, Height=22, Margin=new Thickness(3,0,0,0),
                    Cursor=Cursors.Hand,
                    Background=new SolidColorBrush(th.Chip),
                    BorderBrush=new SolidColorBrush(Color.FromArgb(0,255,255,255)),
                    BorderThickness=new Thickness(2),
                    ToolTip=th.Name
                };
                chip.MouseEnter += (s,e) => chip.BorderBrush=new SolidColorBrush(Colors.White);
                chip.MouseLeave += (s,e) => chip.BorderBrush=new SolidColorBrush(Color.FromArgb(0,255,255,255));
                chip.Click += (s,e) => ApplyTheme(th);
                ThemePalette.Children.Add(chip);
            }
            var custom = new Button
            {
                Width=22, Height=22, Margin=new Thickness(3,0,0,0),
                Content="+", FontFamily=new FontFamily("Consolas"), FontSize=12,
                Cursor=Cursors.Hand,
                Background=new SolidColorBrush(Color.FromRgb(26,40,64)),
                Foreground=new SolidColorBrush(Color.FromRgb(107,122,153)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(42,58,85)),
                BorderThickness=new Thickness(1), ToolTip="커스텀 테마"
            };
            custom.Click += (s,e) => { var d=new CustomThemeDialog(_theme); if(d.ShowDialog()==true) ApplyTheme(d.Result); };
            ThemePalette.Children.Add(custom);
        }

        void ApplyTheme(DdsTheme t)
        {
            _theme = t;
            LogColorConverter.SetTheme(t);
            Resources["ThBgDeep"]  = new SolidColorBrush(t.BgDeep);
            Resources["ThBgPanel"] = new SolidColorBrush(t.BgPanel);
            Resources["ThBgCard"]  = new SolidColorBrush(t.BgCard);
            Resources["ThBorder"]  = new SolidColorBrush(t.Border);
            Resources["ThAccent"]  = new SolidColorBrush(t.Accent);
            Resources["ThAccent2"] = new SolidColorBrush(t.Accent2);
            Resources["ThWarn"]    = new SolidColorBrush(t.Warn);
            Resources["ThPurple"]  = new SolidColorBrush(t.Purple);
            Resources["ThText"]    = new SolidColorBrush(t.Text);
            Resources["ThTextDim"] = new SolidColorBrush(t.TextDim);
            LogListBox.Items.Refresh();
            Log("[OK] 테마: " + t.Name);
        }

        // ════════════════════
        // TITLE BAR
        // ════════════════════
        void TitleBar_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        { if(e.ClickCount==2) ToggleMax(); else DragMove(); }
        void BtnMinimize_Click(object s, RoutedEventArgs e) => WindowState=WindowState.Minimized;
        void BtnMaximize_Click(object s, RoutedEventArgs e) => ToggleMax();
        void BtnClose_Click(object s, RoutedEventArgs e) { CleanupDds(); Application.Current.Shutdown(); }
        void ToggleMax() => WindowState = WindowState==WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        void BtnShowIdlExample_Click(object s, RoutedEventArgs e) => new IdlExampleDialog().ShowDialog();

        // ════════════════════════════════════════════════
        // ① IDL → rtiddsgen → CS 파싱 (원스텝)
        // ════════════════════════════════════════════════
        void BtnBrowseIdl_Click(object s, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter="IDL Files (*.idl)|*.idl|All Files|*.*" };
            if(d.ShowDialog()!=true) return;
            _idlPath = d.FileName;
            TbIdlFilePath.Text = SysIO.Path.GetFileName(_idlPath);
            if(TbGenOutputDir.Text=="./gen")
                TbGenOutputDir.Text = SysIO.Path.Combine(SysIO.Path.GetDirectoryName(_idlPath),"gen");
            SetStep(1,true,true);
            Log("[DDS] IDL: "+_idlPath);
        }

        void BtnBrowseRtiddsgen_Click(object s, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter="rtiddsgen|rtiddsgen.bat;rtiddsgen|All|*.*" };
            if(d.ShowDialog()!=true) return;
            _rtiddsgenPath = d.FileName;
            TbRtiddsgenPath.Text = _rtiddsgenPath;
        }

        // 폴더 선택: WPF 방식 (System.Windows.Forms 불필요)
        void BtnBrowseOutputDir_Click(object s, RoutedEventArgs e)
        {
            var d = new OpenFileDialog
            {
                Title = "출력 디렉토리 선택 (아무 파일이나 선택 후 확인)",
                Filter = "All Files|*.*",
                FileName = "폴더선택",
                CheckFileExists = false,
                CheckPathExists = true
            };
            if(d.ShowDialog()==true)
                TbGenOutputDir.Text = SysIO.Path.GetDirectoryName(d.FileName);
        }

        async void BtnRunAll_Click(object s, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(_idlPath)||!SysIO.File.Exists(_idlPath))
            { Log("[ERR] IDL 파일을 먼저 선택하세요."); return; }

            BtnRunAll.IsEnabled = false;

            string rtiddsgen = ResolveRtiddsgen();
            if(string.IsNullOrEmpty(rtiddsgen))
            {
                Log("[ERR] rtiddsgen을 찾을 수 없습니다.");
                Log("[ERR] NDDSHOME 환경변수 또는 ② 경로를 직접 지정하세요.");
                SetStep(2,false,false); BtnRunAll.IsEnabled=true; return;
            }
            TbRtiddsgenPath.Text = rtiddsgen;
            SetStep(2,true,null);

            string outDir = TbGenOutputDir.Text.Trim();
            if(string.IsNullOrEmpty(outDir))
                outDir = SysIO.Path.Combine(SysIO.Path.GetDirectoryName(_idlPath),"gen");
            SysIO.Directory.CreateDirectory(outDir);

            Log("[DDS] rtiddsgen 실행 중...");
            Log(string.Format("[DDS]   rtiddsgen -language C# -d \"{0}\" \"{1}\"", outDir, SysIO.Path.GetFileName(_idlPath)));

            var result = await RunProcess(rtiddsgen, string.Format("-language C# -d \"{0}\" \"{1}\"", outDir, _idlPath));
            if(result.exit!=0)
            {
                Log("[ERR] rtiddsgen 실패 (exit="+result.exit+")");
                foreach (var l in result.stderr.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(l)) 
                        Log("[ERR]   " + l.TrimEnd());
                }
                SetStep(2,false,false); BtnRunAll.IsEnabled=true; return;
            }
            foreach (var l in result.stdout.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(l)) Log("[DDS]   " + l.TrimEnd());
            }
            SetStep(2,true,true);
            Log("[OK] rtiddsgen 완료.");

            var allCs = SysIO.Directory.GetFiles(outDir,"*.cs",SysIO.SearchOption.TopDirectoryOnly);
            string cs = allCs
                .Where(f=>!f.EndsWith("Plugin.cs")&&!f.EndsWith("Support.cs"))
                .OrderBy(f=>f.Length).FirstOrDefault();
            foreach(var f in allCs) Log("[DDS]   "+SysIO.Path.GetFileName(f));

            if(cs==null)
            { 
                Log("[ERR] 생성된 CS 없음."); 
                SetStep(3,false,false); 
                BtnRunAll.IsEnabled=true; 
                return; 
            }
            _csPath = cs;
            TbCsFilePath.Text = SysIO.Path.GetFileName(cs);
            SetStep(3,true,null);
            await ParseCsAsync(_csPath);
            SetStep(3,true,true);
            BtnRunAll.IsEnabled = true;
        }

        string ResolveRtiddsgen()
        {
            // 0. 사용자가 직접 지정한 경로 (② 버튼으로 선택한 경우)
            if (!string.IsNullOrEmpty(_rtiddsgenPath)&&SysIO.File.Exists(_rtiddsgenPath)) 
                return _rtiddsgenPath;

            // 1. NDDSHOME 환경변수
            var home =Environment.GetEnvironmentVariable("NDDSHOME");
            if (!string.IsNullOrEmpty(home))
            {
                foreach (var n in new[] { "rtiddsgen.bat", "rtiddsgen" })
                { 
                    var p = SysIO.Path.Combine(home, "bin", n); 
                    if (SysIO.File.Exists(p)) 
                    { 
                        Log("[DDS] NDDSHOME: " + p); 
                        return p; 
                    } 
                }
            }

            // 2. 시스템 PATH
            var envPath =Environment.GetEnvironmentVariable("PATH")??"";
            foreach (var dir in envPath.Split(SysIO.Path.PathSeparator))
            {
                foreach (var n in new[] { "rtiddsgen.bat", "rtiddsgen.exe", "rtiddsgen" })
                { 
                    var p = SysIO.Path.Combine(dir.Trim(), n); 
                    if (SysIO.File.Exists(p)) 
                    { 
                        Log("[DDS] PATH: " + p); 
                        return p; } 
                }
            }

            // 3. 드라이브에서 rti_connext_dds* 폴더 검색
            foreach (var root in SysIO.DriveInfo.GetDrives().Where(d => d.DriveType == SysIO.DriveType.Fixed).Select(d => d.RootDirectory.FullName))
            {
                try
                {
                    foreach (var d in SysIO.Directory.GetDirectories(root, "rti_connext_dds*").OrderByDescending(x => x))
                    {
                        var p = SysIO.Path.Combine(d, "bin", "rtiddsgen.bat");
                        if (SysIO.File.Exists(p))
                        {
                            Log("[DDS] 설치경로: " + p);
                            return p;
                        }
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        async Task<(int exit, string stdout, string stderr)> RunProcess(string exe, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName=exe, Arguments=args,
                RedirectStandardOutput=true, RedirectStandardError=true,
                UseShellExecute=false, CreateNoWindow=true,
                WorkingDirectory=SysIO.Path.GetDirectoryName(_idlPath)??"."
            };
            using (var proc = new Process{ StartInfo=psi })
            {
                var so=new StringBuilder(); var se=new StringBuilder();
                proc.OutputDataReceived += (s,ev) => { if(ev.Data!=null) so.AppendLine(ev.Data); };
                proc.ErrorDataReceived  += (s,ev) => { if(ev.Data!=null) se.AppendLine(ev.Data); };
                proc.Start(); proc.BeginOutputReadLine(); proc.BeginErrorReadLine();
                await Task.Run(()=>proc.WaitForExit());
                return (proc.ExitCode, so.ToString(), se.ToString());
            }
        }

        void SetStep(int step, bool active, bool? done)
        {
            Ellipse dot; TextBlock lbl;
            if(step==1){dot=EllStep1;lbl=TbStep1;}
            else if(step==2){dot=EllStep2;lbl=TbStep2;}
            else{dot=EllStep3;lbl=TbStep3;}

            Color c; string pfx;
            if(!active)         { c=_theme.Border;            pfx="○ "; }
            else if(done==null) { c=Color.FromRgb(255,200,0); pfx="● "; }
            else if(done==true) { c=_theme.Accent2;           pfx="✔ "; }
            else                { c=Color.FromRgb(255,80,80); pfx="✗ "; }
            dot.Fill = new SolidColorBrush(c);
            lbl.Foreground = new SolidColorBrush(c);
            lbl.Text = pfx + lbl.Text.TrimStart('○','●','✔','✗',' ');
        }

        // ════════════════════
        // CS 직접 로드
        // ════════════════════
        void BtnBrowseCs_Click(object s, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter="C# Files (*.cs)|*.cs|All|*.*" };
            if(d.ShowDialog()!=true) return;
            _csPath = d.FileName;
            TbCsFilePath.Text = SysIO.Path.GetFileName(_csPath);
            Log("[DDS] CS: "+_csPath);
        }

        async void BtnParseCsFile_Click(object s, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(_csPath)||!SysIO.File.Exists(_csPath)){Log("[ERR] CS 파일 선택 필요.");return;}
            await ParseCsAsync(_csPath);
        }

        async Task ParseCsAsync(string path)
        {
            string src;
            try 
            {
                src=SysIO.File.ReadAllText(path); 
            }
            catch(Exception ex)
            {
                Log("[ERR] "+ex.Message); 
                return; 
            }

            var parsed = CsParser.Parse(src);
            string tn = parsed.typeName;
            var fields = parsed.fields;
            if(fields.Count==0)
            {
                Log("[WARN] 필드 인식 실패. rtiddsgen -language C# 출력인지 확인.");
                return;
            }

            _typeName=tn; _parsedFields=fields;
            Log("[TYPE] ── '"+tn+"'  "+fields.Count+"개 필드 ──");
            foreach(var f in fields) Log(string.Format("[TYPE]   {0,-16} {1}", f.DdsType, f.Name));

            if(_connected)
            {
                Log("[TYPE] 연결 중 → Writer/Reader 재생성...");
                bool wp=_pubLoop, ws=_subLoop;
                StopPub(); StopSub();
                await Task.Delay(100);
                await RebuildEntities();
                Log("[OK] 재생성 완료.");
                if(wp||ws) Log("[WARN] 루프 중지됨. 수동 재시작.");
            }
            else 
                Log("[OK] Connect 시 DynamicType으로 등록됩니다.");

            if(_active!=null)
            {
                _active.TypeName = tn;
                _active.Fields = fields.Select(f=>new DdsField{Name=f.Name,CsType=f.CsType,IsArray=f.IsArray,Value=f.Value}).ToList();
                TbTopicType.Text = tn;
                RenderFields(_active);
            }
        }

        // ════════════════════
        // QoS
        // ════════════════════
        void BtnBrowseQos_Click(object s, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter="XML Files (*.xml)|*.xml|All|*.*" };
            if(d.ShowDialog()!=true) 
                return;
            _qosPath=d.FileName; TbQosFilePath.Text=SysIO.Path.GetFileName(_qosPath);
            Log("[QoS] 파일: "+_qosPath);
        }

        void BtnLoadQos_Click(object s, RoutedEventArgs e)
        {
            if(!SysIO.File.Exists(_qosPath))
            {
                Log("[ERR] QoS 파일 없음.");
                return;
            }
            try
            {
                var xml = SysIO.File.ReadAllText(_qosPath);
                TbQosPreview.Text = xml.Length>600 ? xml.Substring(0,600)+"..." : xml;
                Log("[QoS] 미리보기 완료.");
            }
            catch(Exception ex){Log("[ERR] "+ex.Message);}
        }

        void BtnApplyQos_Click(object s, RoutedEventArgs e)
        {
            if(!SysIO.File.Exists(_qosPath)){Log("[ERR] QoS 파일 선택 필요.");return;}
            if(_connected){Log("[WARN] 연결 중 QoS 교체 불가. DISCONNECT → APPLY → CONNECT");return;}
            var target = SysIO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"USER_QOS_PROFILES.xml");
            try
            {
                SysIO.File.Copy(_qosPath,target,true);
                _qosApplied = target;
                EllQosStatus.Fill = new SolidColorBrush(_theme.Accent2);
                TbQosStatus.Text = "✔ "+TbQosLibrary.Text+"::"+TbQosProfile.Text;
                TbQosStatus.Foreground = new SolidColorBrush(_theme.Accent2);
                Log("[QoS] USER_QOS_PROFILES.xml 교체 완료.");
                Log("[QoS] "+TbQosLibrary.Text+"::"+TbQosProfile.Text);
                Log("[OK] 다음 Connect 시 적용됩니다.");
            }
            catch(Exception ex)
            {
                Log("[ERR] "+ex.Message);
            }
        }

        // ════════════════════
        // DOMAIN ID
        // ════════════════════
        void BtnDomainDec_Click(object s, RoutedEventArgs e)
        { 
            int d; 
            if(int.TryParse(TbDomainId.Text,out d)&&d>0) 
                TbDomainId.Text=(d-1).ToString(); 
        }
        void BtnDomainInc_Click(object s, RoutedEventArgs e)
        { 
            int d; 
            if(int.TryParse(TbDomainId.Text,out d)&&d<232) 
                TbDomainId.Text=(d+1).ToString(); 
        }

        // ════════════════════════════════════════════════
        // CONNECT — RTI SDK
        // ════════════════════════════════════════════════
        async void BtnConnect_Click(object s, RoutedEventArgs e)
        {
            if(_connected)
                return;

            int domId;

            if(!int.TryParse(TbDomainId.Text,out domId)||domId<0||domId>232)
            { 
                Log("[ERR] Domain ID: 0~232"); return; 
            }

            Log("[DDS] Domain "+domId+" 연결 중...");
            BtnConnect.IsEnabled=BtnDisconnect.IsEnabled=false;
            TbStatusIndicator.Text="● 연결 중...";
            TbStatusIndicator.Foreground=new SolidColorBrush(Color.FromRgb(255,200,0));

            try
            {
                await Task.Run(()=>
                {
                    // 1. Participant 생성 (USER_QOS_PROFILES.xml 자동 로드)
                    _participant = DomainParticipantFactory.Instance.CreateParticipant(domId);

                    // 2. DynamicType 구성
                    if(_parsedFields.Count>0)
                    {
                        var tf = DynamicTypeFactory.Instance;
                        var sb = tf.BuildStruct().WithName(_typeName);
                        foreach(var f in _parsedFields)
                        {
                            DynamicType mt = f.IsArray
                                ? tf.CreateArray(GetPrimType(tf,f.CsType), (uint)(f.ArraySize>0?f.ArraySize:128))
                                : GetPrimType(tf,f.CsType);
                            sb = sb.AddMember(new StructMember(f.Name, mt));
                        }
                        _dynamicType = sb.Create();
                    }

                    // 3. Publisher / Subscriber
                    _publisher  = _participant.CreatePublisher();
                    _subscriber = _participant.CreateSubscriber();
                });

                _connected = true;

                if(_parsedFields.Count>0) 
                    await RebuildEntities();

                TbStatusIndicator.Text       = "● DOMAIN "+domId;
                TbStatusIndicator.Foreground = new SolidColorBrush(_theme.Accent2);
                TbStatusMsg.Text             = "연결됨 — Domain "+domId;
                BtnConnect.IsEnabled         = false;
                BtnDisconnect.IsEnabled      = true;
                SetInd(EllPublisher,TbPubStatus,false);
                SetInd(EllSubscriber,TbSubStatus,false);

                Log("[OK] DomainParticipant 생성 완료 (Domain "+domId+")");
                Log("[DDS] Participant: "+TbParticipantName.Text);
                Log(string.IsNullOrEmpty(_qosApplied)
                    ?"[QoS] QoS 파일 없음 → RTI 기본 QoS"
                    :"[QoS] 적용: "+SysIO.Path.GetFileName(_qosApplied)+" ("+TbQosLibrary.Text+"::"+TbQosProfile.Text+")");
                Log(_parsedFields.Count>0
                    ?"[TYPE] DynamicType 등록: "+_typeName+" ("+_parsedFields.Count+"개 필드)"
                    :"[WARN] CS 미파싱 → 타입 없이 연결");
            }
            catch(Exception ex)
            {
                Log("[ERR] 연결 실패: "+ex.Message);
                if (ex.Message.ToLower().Contains("license"))
                {
                    Log("[ERR] rti_license.dat을 실행 디렉토리에 복사하거나 RTI_LICENSE_FILE 환경변수 설정.");
                }
                BtnConnect.IsEnabled=true; 
                BtnDisconnect.IsEnabled=false;
                TbStatusIndicator.Text="● OFFLINE";
                TbStatusIndicator.Foreground=new SolidColorBrush(Color.FromRgb(255,68,68));
                CleanupDds();
            }
        }

        async Task RebuildEntities()
        {
            if(_participant==null||_dynamicType==null) 
                return;
            
            await Task.Run(()=>
            {
                if(_reader!=null){ _reader.DataAvailable-=OnDataAvailable; _reader.Dispose(); _reader=null; }
                if(_writer!=null){ _writer.Dispose(); _writer=null; }
                if(_ddsTopic!=null){ _ddsTopic.Dispose(); _ddsTopic=null; }

                _ddsTopic = _participant.CreateTopic<DynamicData>(TbTopicName.Text, _typeName);
                _writer   = _publisher.CreateDataWriter(_ddsTopic);
                _reader   = _subscriber.CreateDataReader(_ddsTopic);
            });

            if(_reader!=null) 
                _reader.DataAvailable += OnDataAvailable;
        }

        // DataAvailable 핸들러 — 정확한 시그니처: (AnyDataReader)
        void OnDataAvailable(AnyDataReader anyReader)
        {
            if(_reader==null) 
                return;

            try
            {
                using (var samples = _reader.Take())
                {
                    foreach(var sample in samples)
                    {
                        if(!sample.Info.ValidData) continue;
                        var text = FormatSample(sample.Data, _parsedFields);
                        Dispatcher.Invoke(()=>
                        {
                            _rx++; if(_active!=null) _active.RxCount++;
                            TbPublishCount.Text="TX: "+_tx+"  RX: "+_rx;
                            TbReceivedData.Text=text;
                            Log("[RX] "+(_active!=null?_active.TopicName:"?")+" "+text);
                        });
                    }
                }
            }
            catch(Exception ex){ Dispatcher.Invoke(()=>Log("[ERR] RX: "+ex.Message)); }
        }

        string FormatSample(DynamicData d, List<DdsField> fields)
        {
            var sb=new StringBuilder("{ ");

            foreach(var f in fields)
            {
                string v;
                try
                {
                    switch(f.CsType)
                    {
                        case "bool":   v=d.GetValue<bool>(f.Name).ToString();           break;
                        case "int":    v=d.GetValue<int>(f.Name).ToString();            break;
                        case "long":   v=d.GetValue<long>(f.Name).ToString();           break;
                        case "float":  v=d.GetValue<float>(f.Name).ToString("F4");      break;
                        case "double": v=d.GetValue<double>(f.Name).ToString("F6");     break;
                        case "string": v=d.GetValue<string>(f.Name);                   break;
                        case "byte":   v=d.GetValue<byte>(f.Name).ToString();           break;
                        case "short":  v=d.GetValue<short>(f.Name).ToString();          break;
                        default:       v="?"; break;
                    }
                }
                catch { v="?"; }
                sb.Append(f.Name+"="+v+", ");
            }
            if(fields.Count>0)
                sb.Length-=2;

            return sb.Append(" }").ToString();
        }

        DynamicType GetPrimType(DynamicTypeFactory tf, string cs)
        {
            switch(cs)
            {
                case "bool":   return tf.GetPrimitiveType<bool>();
                case "int":    return tf.GetPrimitiveType<int>();
                case "long":   return tf.GetPrimitiveType<long>();
                case "float":  return tf.GetPrimitiveType<float>();
                case "double": return tf.GetPrimitiveType<double>();
                case "string": return tf.CreateString(256);
                case "byte":   return tf.GetPrimitiveType<byte>();
                case "short":  return tf.GetPrimitiveType<short>();
                case "uint":   return tf.GetPrimitiveType<uint>();
                case "ulong":  return tf.GetPrimitiveType<ulong>();
                default:       return tf.GetPrimitiveType<int>();
            }
        }

        // ════════════════════
        // DISCONNECT
        // ════════════════════
        async void BtnDisconnect_Click(object s, RoutedEventArgs e)
        {
            StopPub(); 
            StopSub();
            await Task.Delay(150);
            await Task.Run((Action)CleanupDds);
            _connected=false;
            TbStatusIndicator.Text="● OFFLINE";
            TbStatusIndicator.Foreground=new SolidColorBrush(Color.FromRgb(255,68,68));
            TbStatusMsg.Text="연결 해제됨";
            BtnConnect.IsEnabled=true; BtnDisconnect.IsEnabled=false;
            SetInd(EllPublisher,TbPubStatus,null);
            SetInd(EllSubscriber,TbSubStatus,null);
            Log("[DDS] 연결 해제.");
        }

        void CleanupDds()
        {
            try
            {
                if(_reader  !=null){_reader.DataAvailable-=OnDataAvailable;_reader.Dispose(); _reader  =null;}
                if(_writer  !=null){_writer.Dispose();  _writer  =null;}
                if(_ddsTopic!=null){_ddsTopic.Dispose(); _ddsTopic=null;}
                if(_dynamicType!=null){_dynamicType.Dispose(); _dynamicType=null;}
                if(_publisher  !=null){_publisher.Dispose();   _publisher  =null;}
                if(_subscriber !=null){_subscriber.Dispose();  _subscriber =null;}
                if(_participant!=null){_participant.Dispose();  _participant=null;}
            }
            catch(Exception ex)
            {
                Dispatcher.Invoke(()=>Log("[WARN] Cleanup: "+ex.Message));
            }
        }

        // ════════════════════
        // TOPIC
        // ════════════════════
        void AddTopic(string name)
        {
            var topic = new DdsTopic
            {
                TopicName=name, TypeName=_typeName,
                Fields=_parsedFields.Count>0
                    ?_parsedFields.Select(f=>new DdsField{Name=f.Name,CsType=f.CsType,IsArray=f.IsArray,Value=f.Value}).ToList()
                    :Demo()
            };
            _topics.Add(topic);
            var btn = new Button
            {
                Content=name, Style=FindResource("Btn") as Style,
                Margin=new Thickness(0,0,4,0), Padding=new Thickness(12,4,12,4)
            };
            btn.Click += (s2,e2) => Activate(topic);
            TopicTabBar.Children.Add(btn);
            _tabBtns[name]=btn;
            Activate(topic);
        }

        void Activate(DdsTopic t)
        {
            _active=t;
            foreach(Button b in TopicTabBar.Children)
            {
                b.Background=new SolidColorBrush(_theme.BgCard); 
                b.Foreground=new SolidColorBrush(_theme.Accent); 
            }
            Button ab;
            if (_tabBtns.TryGetValue(t.TopicName, out ab))
            {
                ab.Background = new SolidColorBrush(Color.FromArgb(80, _theme.Accent.R, _theme.Accent.G, _theme.Accent.B));
            }
            TbTopicName.Text=t.TopicName; TbTopicType.Text=t.TypeName;
            RenderFields(t);
        }

        void BtnAddTopic_Click(object s, RoutedEventArgs e)
        {
            var d=new InputDialog("토픽 추가","이름:","Topic"+(_topics.Count+1));
            if(d.ShowDialog()==true&&!string.IsNullOrEmpty(d.InputText))
                AddTopic(d.InputText);
        }

        void BtnRemoveTopic_Click(object s, RoutedEventArgs e)
        {
            if(_active==null||_topics.Count<=1) 
                return;
            
            var n=_active.TopicName; _topics.Remove(_active);
            Button b;
            if(_tabBtns.TryGetValue(n,out b))
            {
                TopicTabBar.Children.Remove(b);_tabBtns.Remove(n);
            }
            Activate(_topics.Last());
        }

        // ════════════════════
        // FIELD RENDERING
        // ════════════════════
        void RenderFields(DdsTopic t)
        {
            FieldsPanel.Children.Clear();
            if(t.Fields.Count==0)
            {
                FieldsPanel.Children.Add(new TextBlock
                {
                    Text="  IDL 파싱 후 필드가 표시됩니다.",
                    Foreground=new SolidColorBrush(_theme.Border),
                    FontFamily=new FontFamily("Consolas"),FontSize=12,
                    Margin=new Thickness(20,40,0,0)
                });
                return;
            }
            int i=0;
            foreach (var f in t.Fields)
            {
                FieldsPanel.Children.Add(MakeRow(f, i++));
            }
            var add = new Button
            {
                Content="+ 필드 추가", Style=FindResource("Btn") as Style,
                HorizontalAlignment=HorizontalAlignment.Left,
                Margin=new Thickness(0,8,0,0), Padding=new Thickness(12,5,12,5)
            };
            add.Click += (s2,e2) =>
            {
                var d=new AddFieldDialog();
                if(d.ShowDialog()==true&&_active!=null){_active.Fields.Add(d.CreatedField);RenderFields(_active);}
            };
            FieldsPanel.Children.Add(add);
        }

        UIElement MakeRow(DdsField f, int idx)
        {
            var row = new Border
            {
                Background      = new SolidColorBrush(idx%2==1?_theme.BgCard:_theme.BgPanel),
                BorderBrush     = new SolidColorBrush(_theme.Border),
                BorderThickness = new Thickness(0,0,0,1),
                Padding         = new Thickness(12,7,12,7)
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});
            g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(140)});
            g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(120)});
            g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(36)});

            var dot = new Ellipse{Width=6,Height=6,Fill=TB(f.CsType),VerticalAlignment=VerticalAlignment.Center};
            var nm  = new TextBlock
            {
                Text=f.Name, Foreground=new SolidColorBrush(_theme.Text),
                FontFamily=new FontFamily("Consolas"),FontSize=12,VerticalAlignment=VerticalAlignment.Center
            };

            var bsp = new StackPanel();
            bsp.Children.Add(new TextBlock{Text=f.CsType,Foreground=TB(f.CsType),FontFamily=new FontFamily("Consolas"),FontSize=9});
            bsp.Children.Add(new TextBlock{Text="→ "+f.DdsType,Foreground=new SolidColorBrush(_theme.TextDim),FontFamily=new FontFamily("Consolas"),FontSize=8});
            var badge = new Border
            {
                BorderBrush=TB(f.CsType),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(2),
                Padding=new Thickness(5,2,5,2),Background=new SolidColorBrush(Color.FromArgb(30,0,0,0)),
                HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,
                Child=bsp
            };

            var vc  = MakeCtrl(f);
            var del = new Button
            {
                Content="✕", Background=Brushes.Transparent, Foreground=new SolidColorBrush(_theme.TextDim),
                BorderThickness=new Thickness(0),FontSize=11,Cursor=Cursors.Hand,
                HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center
            };
            del.Click += (s2,e2) => {_active?.Fields.Remove(f);RenderFields(_active);};

            Grid.SetColumn(dot,0); Grid.SetColumn(nm,1); Grid.SetColumn(badge,2);
            Grid.SetColumn(vc,3);  Grid.SetColumn(del,4);
            g.Children.Add(dot);g.Children.Add(nm);g.Children.Add(badge);g.Children.Add(vc);g.Children.Add(del);
            row.Child=g;
            return row;
        }

        UIElement MakeCtrl(DdsField f)
        {
            var sp = new StackPanel
            {
                Orientation=Orientation.Horizontal,
                VerticalAlignment=VerticalAlignment.Center,
                Margin=new Thickness(8,0,0,0)
            };

            if(f.IsBool)
            {
                bool cur = f.Value is bool b && b;
                var btn = new Button{Style=FindResource("BoolBtn") as Style, Content=cur?"TRUE":"FALSE"};
                BoolStyle(btn,cur);
                btn.Click += (s2,e2) =>
                {
                    bool v = !(f.Value is bool bv && bv);
                    f.Value=v; btn.Content=v?"TRUE":"FALSE"; BoolStyle(btn,v);
                };
                sp.Children.Add(btn);
            }
            else if(f.IsNumeric)
            {
                var tb = new TextBox
                {
                    Style=FindResource("TB") as Style, Width=110,
                    Text=f.Value!=null?f.Value.ToString():"0",
                    VerticalAlignment=VerticalAlignment.Center
                };
                tb.TextChanged += (s2,e2) =>
                {
                    object v; bool ok=TryNum(f.CsType,tb.Text,out v);
                    if(ok) f.Value=v;
                    tb.Foreground=ok?new SolidColorBrush(_theme.Accent):new SolidColorBrush(Color.FromRgb(255,80,80));
                };
                var bm=SB("−"); bm.Click += (s2,e2) => Step(f,tb,-1);
                var bp=SB("+"); bp.Click += (s2,e2) => Step(f,tb,+1);
                sp.Children.Add(tb); sp.Children.Add(bm); sp.Children.Add(bp);
            }
            else
            {
                var tb = new TextBox
                {
                    Style=FindResource("TB") as Style, Width=190,
                    Text=f.Value!=null?f.Value.ToString():"",
                    VerticalAlignment=VerticalAlignment.Center
                };
                tb.TextChanged += (s2,e2) => f.Value=tb.Text;
                sp.Children.Add(tb);
            }
            return sp;
        }

        void BoolStyle(Button b, bool v)
        {
            b.Background  = v ? new SolidColorBrush(Color.FromArgb(60,_theme.Accent2.R,_theme.Accent2.G,_theme.Accent2.B))
                              : new SolidColorBrush(_theme.BgCard);
            b.Foreground  = v ? new SolidColorBrush(_theme.Accent2) : new SolidColorBrush(_theme.Warn);
            b.BorderBrush = b.Foreground;
        }

        Button SB(string l) => new Button
        {
            Content=l, Width=24, Height=24, FontSize=14, Cursor=Cursors.Hand,
            Margin=new Thickness(3,0,0,0),
            Background=new SolidColorBrush(_theme.BgCard),
            Foreground=new SolidColorBrush(_theme.Accent),
            BorderBrush=new SolidColorBrush(_theme.Border),
            BorderThickness=new Thickness(1)
        };

        void Step(DdsField f, TextBox tb, int d)
        {
            object c; TryNum(f.CsType,tb.Text,out c);
            double sv = System.Convert.ToDouble(c)+d; f.Value=sv;
            tb.Text = (f.CsType=="float"||f.CsType=="double") ? sv.ToString("G6") : ((long)sv).ToString();
        }

        bool TryNum(string t, string s, out object v)
        {
            v=0;

            int    i; 
            if(t=="int"    && int.TryParse(s,out i))   
            {
                v=i;  
                return true;
            }
            
            long   l;  
            if(t=="long"   && long.TryParse(s,out l))   
            {
                v=l; 
                return true;
            }

            float  f;  
            if(t=="float"  && float.TryParse(s,NumberStyles.Any,CultureInfo.InvariantCulture,out f)) 
            {
                v=f; 
                return true;
            }
            
            double d2;
            if(t=="double" && double.TryParse(s,NumberStyles.Any,CultureInfo.InvariantCulture,out d2))
            {
                v=d2;
                return true;
            }
            
            short  sh; 
            if(t=="short"  && short.TryParse(s,out sh)) 
            {
                v=sh; 
                return true;
            }
            
            byte   by;
            if(t=="byte"   && byte.TryParse(s,out by))  
            {
                v=by; 
                return true;
            }
            
            uint   ui; 
            if(t=="uint"   && uint.TryParse(s,out ui))  
            {
                v=ui;
                return true;
            }
            
            ulong  ul; 
            if(t=="ulong"  && ulong.TryParse(s,out ul)) 
            {
                v=ul;
                return true;
            }
            
            return false;
        }

        SolidColorBrush TB(string t)
        {
            if(t=="bool")             return new SolidColorBrush(_theme.Warn);
            if(t=="string")           return new SolidColorBrush(_theme.Purple);
            if(t=="float"||t=="double") return new SolidColorBrush(_theme.Accent);
            return new SolidColorBrush(_theme.Accent2);
        }

        // ════════════════════
        // PUBLISH
        // ════════════════════
        void BtnPublishOnce_Click(object s, RoutedEventArgs e)
        { if(!_connected){Log("[ERR] Connect 먼저.");return;} Pub(); }

        async void BtnPublishLoop_Click(object s, RoutedEventArgs e)
        {
            if(!_connected)
            {
                Log("[ERR] Connect 먼저.");
                return;
            }
            if(_pubLoop)
            {
                StopPub();
                return;
            }
            
            double hz; 
            
            if(!double.TryParse(TbPublishRate.Text,out hz)||hz<=0) 
                hz=1;
            
            int ms=(int)(1000.0/hz);

            _pubCts=new CancellationTokenSource(); 
            _pubLoop=true;
            BtnPublishLoop.Content="■ STOP";
            SetInd(EllPublisher,TbPubStatus,true);
            
            Log("[DDS] 퍼블리시 루프 시작 ("+hz.ToString("F1")+" Hz)");
            var tok=_pubCts.Token;
            await Task.Run(async()=>
            {
                while(!tok.IsCancellationRequested)
                {
                    Dispatcher.Invoke((Action)Pub);
                    try{await Task.Delay(ms,tok);}
                    catch(TaskCanceledException){break;}
                }
            });
            BtnPublishLoop.Content="▶ LOOP"; SetInd(EllPublisher,TbPubStatus,false);
        }

        void StopPub()
        { 
            _pubCts?.Cancel(); _pubLoop=false; BtnPublishLoop.Content="▶ LOOP"; SetInd(EllPublisher,TbPubStatus,false); 
        }

        void Pub()
        {
            if(_active==null||_writer==null||_dynamicType==null) 
                return;
            try
            {
                var sample = new DynamicData(_dynamicType);
                foreach(var f in _active.Fields)
                {
                    try
                    {
                        switch(f.CsType)
                        {
                            case "bool":   sample.SetValue(f.Name,(bool)f.Value);                       break;
                            case "int":    sample.SetValue(f.Name,System.Convert.ToInt32(f.Value));     break;
                            case "long":   sample.SetValue(f.Name,System.Convert.ToInt64(f.Value));     break;
                            case "float":  sample.SetValue(f.Name,System.Convert.ToSingle(f.Value));    break;
                            case "double": sample.SetValue(f.Name,System.Convert.ToDouble(f.Value));    break;
                            case "string": sample.SetValue(f.Name,(string)(f.Value??""));               break;
                            case "byte":   sample.SetValue(f.Name,System.Convert.ToByte(f.Value));      break;
                            case "short":  sample.SetValue(f.Name,System.Convert.ToInt16(f.Value));     break;
                        }
                    }
                    catch(Exception ex)
                    {
                        Log("[WARN] 필드 '"+f.Name+"': "+ex.Message);
                    }
                }
                _writer.Write(sample);
                _tx++; _active.TxCount++;
                TbPublishCount.Text="TX: "+_tx+"  RX: "+_rx;

                var sb=new StringBuilder("{ ");
                foreach(var f in _active.Fields)
                {
                    string fv;
                    if(f.CsType=="float")       fv=string.Format("{0:F3}",f.Value);
                    else if(f.CsType=="double")  fv=string.Format("{0:F5}",f.Value);
                    else if(f.CsType=="bool")    fv=(f.Value is bool bv&&bv)?"TRUE":"FALSE";
                    else                         fv=f.Value!=null?f.Value.ToString():"0";
                    sb.Append(f.Name+"="+fv+", ");
                }
                if(_active.Fields.Count>0)
                    sb.Length-=2;
               
                Log("[TX] "+_active.TopicName+" "+sb+" }");
            }
            catch(Exception ex)
            {
                Log("[ERR] Write: "+ex.Message);
            }
        }

        // ════════════════════
        // SUBSCRIBE
        // ════════════════════
        void BtnSubscribe_Click(object s, RoutedEventArgs e)
        {
            if(!_connected)
            {
                Log("[ERR] Connect 먼저.");
                return;
            }
            if(_reader==null)
            {
                Log("[ERR] Reader 없음. IDL 파싱 후 Connect 하세요.");
                return;
            }
            if(_subLoop)
                return;
            
            _subLoop=true; BtnSubscribe.IsEnabled=false; BtnUnsubscribe.IsEnabled=true;
            SetInd(EllSubscriber,TbSubStatus,true);
            Log("[DDS] '"+(_active!=null?_active.TopicName:"?")+"' 구독 활성화.");
        }

        void BtnUnsubscribe_Click(object s, RoutedEventArgs e) => StopSub();
        void StopSub()
        {
            _subLoop=false; BtnSubscribe.IsEnabled=true; BtnUnsubscribe.IsEnabled=false;
            SetInd(EllSubscriber,TbSubStatus,false); Log("[DDS] 구독 비활성화.");
        }

        // ════════════════════
        // HELPERS
        // ════════════════════
        void SetInd(Ellipse dot, TextBlock lbl, bool? on)
        {
            if(on==true)
            {
                Color c = lbl.Text=="PUB" ? _theme.Accent2 : _theme.Accent;
                dot.Fill=new SolidColorBrush(c); lbl.Foreground=new SolidColorBrush(c);
                dot.Effect=new DropShadowEffect{Color=c,BlurRadius=8,ShadowDepth=0};
            }
            else if(on==false)
            {
                dot.Fill=new SolidColorBrush(_theme.TextDim);
                lbl.Foreground=new SolidColorBrush(_theme.TextDim);
                dot.Effect=null;
            }
            else
            {
                dot.Fill=new SolidColorBrush(_theme.Border);
                lbl.Foreground=new SolidColorBrush(_theme.Border);
                dot.Effect=null;
            }
        }

        void Log(string msg)
        {
            _log.Add(DateTime.Now.ToString("HH:mm:ss.fff")+"  "+msg);
            
            if(_log.Count>600) 
                _log.RemoveAt(0);
            
            if(LogListBox.Items.Count>0)
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count-1]);
        }
        void BtnClearLog_Click(object s, RoutedEventArgs e) => _log.Clear();

        List<DdsField> Demo() => new List<DdsField>
        {
            new DdsField{Name="enabled",CsType="bool",  Value=false},
            new DdsField{Name="speed",  CsType="float", Value=0.0f},
            new DdsField{Name="altitude",CsType="double",Value=0.0},
            new DdsField{Name="status", CsType="int",   Value=0},
            new DdsField{Name="label",  CsType="string",Value=""},
        };
    }

    // ═══════════════════════════════════════════════════
    // IDL → CS 예제 다이얼로그
    // ═══════════════════════════════════════════════════
    public class IdlExampleDialog : Window
    {
        public IdlExampleDialog()
        {
            Title="IDL → CS 예제"; Width=820; Height=660;
            WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=new SolidColorBrush(Color.FromRgb(10,14,26));
            WindowStyle=WindowStyle.ToolWindow;
            var tabs=new TabControl
            {
                Background=new SolidColorBrush(Color.FromRgb(10,14,26)),
                FontFamily=new FontFamily("Consolas"),Margin=new Thickness(10)
            };
            tabs.Items.Add(T("① IDL",
@"// SensorData.idl
module MyApp {
    @topic
    struct SensorData {
        @key long   sensor_id;
        boolean     enabled;
        float       temperature;
        double      timestamp;
        string      location;
        short       error_code;
        octet       packet_type;
        long        sequence_num;
    };
};",Color.FromRgb(0,229,255)));
            tabs.Items.Add(T("② rtiddsgen",
@"rtiddsgen -language C# -d ./gen SensorData.idl

// 생성 파일:
//   ./gen/SensorData.cs     ← 앱에 로드!
//   ./gen/SensorDataPlugin.cs
//   ./gen/SensorDataSupport.cs",Color.FromRgb(255,107,53)));
            tabs.Items.Add(T("③ CS 출력",
@"namespace MyApp {
    public class SensorData {
        public int    sensor_id    = 0;   // @key
        public bool   enabled      = false;
        public float  temperature  = 0.0f;
        public double timestamp    = 0.0;
        public string location     = """";
        public short  error_code   = 0;
        public byte   packet_type  = 0;
        public int    sequence_num = 0;
    }
}",Color.FromRgb(0,255,136)));
            tabs.Items.Add(T("④ 타입 매핑",
@"// IDL ↔ C# ↔ DDS DynamicData
//  IDL           C#       API
//  boolean   →  bool   →  GetValue<bool>
//  short     →  short  →  GetValue<short>
//  long      →  int    →  GetValue<int>
//  long long →  long   →  GetValue<long>
//  float     →  float  →  GetValue<float>
//  double    →  double →  GetValue<double>
//  string    →  string →  GetValue<string>
//  octet     →  byte   →  GetValue<byte>",Color.FromRgb(123,97,255)));
            var close=new Button
            {
                Content="닫기",Width=80,Height=28,HorizontalAlignment=HorizontalAlignment.Right,
                Margin=new Thickness(0,6,0,4),Cursor=Cursors.Hand,FontFamily=new FontFamily("Consolas"),
                Background=new SolidColorBrush(Color.FromRgb(26,40,64)),
                Foreground=new SolidColorBrush(Color.FromRgb(0,229,255)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(0,229,255)),BorderThickness=new Thickness(1)
            };
            close.Click+=(s,e)=>Close();
            var dp=new DockPanel{Margin=new Thickness(10)};
            DockPanel.SetDock(close,Dock.Bottom);
            dp.Children.Add(close); 
            dp.Children.Add(tabs);
            Content=dp;
        }
        TabItem T(string h, string code, Color col)
        {
            return new TabItem
            {
                Header=h,
                FontFamily=new FontFamily("Consolas"), FontSize=11,
                Foreground=new SolidColorBrush(col),
                Content=new ScrollViewer
                {
                    VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                    Content=new TextBlock
                    {
                        Text=code, TextWrapping=TextWrapping.NoWrap,
                        FontFamily=new FontFamily("Consolas"), FontSize=12,
                        Foreground=new SolidColorBrush(col),
                        Background=new SolidColorBrush(Color.FromRgb(8,12,22)),
                        Padding=new Thickness(14,12,14,12)
                    }
                }
            };
        }
    }

    // ═══════════════════════════════════════════════════
    // 커스텀 테마 다이얼로그
    // ═══════════════════════════════════════════════════
    public class CustomThemeDialog : Window
    {
        public DdsTheme Result
        {
            get;
            private set;
        }

        readonly Dictionary<string,TextBox> _b=new Dictionary<string,TextBox>();

        public CustomThemeDialog(DdsTheme cur)
        {
            Title="커스텀 테마";Width=340;Height=480;
            WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=new SolidColorBrush(Color.FromRgb(10,14,26));
            WindowStyle=WindowStyle.ToolWindow;
            var sp=new StackPanel{Margin=new Thickness(14)};
            sp.Children.Add(new TextBlock
            {
                Text="색상 직접 입력 (#RRGGBB)",
                Foreground=new SolidColorBrush(Color.FromRgb(0,229,255)),
                FontFamily=new FontFamily("Consolas"),FontSize=11,
                FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,10)
            });
            R(sp,"배경 깊은","BgDeep",DdsTheme.ToHex(cur.BgDeep));
            R(sp,"배경 패널","BgPanel",DdsTheme.ToHex(cur.BgPanel));
            R(sp,"배경 카드","BgCard",DdsTheme.ToHex(cur.BgCard));
            R(sp,"테두리","Border",DdsTheme.ToHex(cur.Border));
            R(sp,"주 강조","Accent",DdsTheme.ToHex(cur.Accent));
            R(sp,"보조 강조","Accent2",DdsTheme.ToHex(cur.Accent2));
            R(sp,"경고","Warn",DdsTheme.ToHex(cur.Warn));
            R(sp,"보라","Purple",DdsTheme.ToHex(cur.Purple));
            R(sp,"텍스트","Text",DdsTheme.ToHex(cur.Text));
            R(sp,"흐린 텍스트","TextDim",DdsTheme.ToHex(cur.TextDim));
            var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
            var ok=new Button{Content="적용",Width=70,Height=28,Margin=new Thickness(0,0,6,0),FontFamily=new FontFamily("Consolas")};
            var cn=new Button{Content="취소",Width=70,Height=28,FontFamily=new FontFamily("Consolas")};
            ok.Click+=(s,e)=>
            {
                try
                {
                    Result=new DdsTheme
                    {
                        Name="Custom", Chip=P("Accent"),
                        BgDeep=P("BgDeep"),BgPanel=P("BgPanel"),BgCard=P("BgCard"),Border=P("Border"),
                        Accent=P("Accent"),Accent2=P("Accent2"),Warn=P("Warn"),Purple=P("Purple"),
                        Text=P("Text"),TextDim=P("TextDim")
                    };
                    DialogResult=true;
                }
                catch{MessageBox.Show("유효한 HEX (#RRGGBB) 입력 필요");}
            };
            cn.Click+=(s,e)=>DialogResult=false;
            row.Children.Add(ok);row.Children.Add(cn);sp.Children.Add(row);
            Content=new ScrollViewer{Content=sp,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        }

        void R(StackPanel sp, string lbl, string key, string val)
        {
            var g=new Grid{Margin=new Thickness(0,0,0,5)};
            g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(110)});
            g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            g.Children.Add(new TextBlock
            {
                Text=lbl, VerticalAlignment=VerticalAlignment.Center,
                Foreground=new SolidColorBrush(Color.FromRgb(107,122,153)),
                FontFamily=new FontFamily("Consolas"),FontSize=10
            });
            var tb=new TextBox
            {
                Text=val,
                Background=new SolidColorBrush(Color.FromRgb(10,14,26)),
                Foreground=new SolidColorBrush(Color.FromRgb(0,229,255)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(26,40,64)),
                FontFamily=new FontFamily("Consolas"),FontSize=11,Padding=new Thickness(6,3,6,3)
            };
            Grid.SetColumn(tb,1); _b[key]=tb; g.Children.Add(tb); sp.Children.Add(g);
        }

        Color P(string k)
        {
            var h=_b[k].Text.TrimStart('#');
            return Color.FromRgb(
                System.Convert.ToByte(h.Substring(0,2),16),
                System.Convert.ToByte(h.Substring(2,2),16),
                System.Convert.ToByte(h.Substring(4,2),16));
        }
    }

    // ═══════════════════════════════════════════════════
    // 공통 다이얼로그
    // ═══════════════════════════════════════════════════
    public class InputDialog : Window
    {
        TextBox _tb;
        public string InputText => _tb.Text;

        public InputDialog(string title, string prompt, string def="")
        {
            Title=title; Width=340; Height=140;
            WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=new SolidColorBrush(Color.FromRgb(10,14,26));
            WindowStyle=WindowStyle.ToolWindow;
            var sp=new StackPanel{Margin=new Thickness(16)};
            sp.Children.Add(new TextBlock
            {
                Text=prompt, Foreground=new SolidColorBrush(Color.FromRgb(107,122,153)),
                FontFamily=new FontFamily("Consolas"),FontSize=11,Margin=new Thickness(0,0,0,6)
            });
            _tb=new TextBox
            {
                Text=def, Background=new SolidColorBrush(Color.FromRgb(10,14,26)),
                Foreground=new SolidColorBrush(Color.FromRgb(0,229,255)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(26,40,64)),
                FontFamily=new FontFamily("Consolas"),FontSize=12,
                Padding=new Thickness(6,4,6,4),Margin=new Thickness(0,0,0,12)
            };
            sp.Children.Add(_tb);
            var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            var ok=new Button{Content="OK",Width=70,Height=28,Margin=new Thickness(0,0,6,0)};
            var cn=new Button{Content="취소",Width=70,Height=28};
            ok.Click+=(s,e)=>DialogResult=true;
            cn.Click+=(s,e)=>DialogResult=false;
            row.Children.Add(ok); row.Children.Add(cn); sp.Children.Add(row);
            Content=sp;
        }
    }

    public class AddFieldDialog : Window
    {
        public DdsField CreatedField{get;private set;}

        public AddFieldDialog()
        {
            Title="필드 추가";
            Width=360;
            Height=220;
            
            WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=new SolidColorBrush(Color.FromRgb(10,14,26));
            WindowStyle=WindowStyle.ToolWindow;
            
            var sp=new StackPanel{Margin=new Thickness(16)};
            sp.Children.Add(L("필드 이름:"));
            
            var tbn=new TextBox
            {
                Text="newField", Margin=new Thickness(0,0,0,8),
                Background=new SolidColorBrush(Color.FromRgb(10,14,26)),
                Foreground=new SolidColorBrush(Color.FromRgb(0,229,255)),
                BorderBrush=new SolidColorBrush(Color.FromRgb(26,40,64)),
                FontFamily=new FontFamily("Consolas"),Padding=new Thickness(6,4,6,4)
            };
            
            sp.Children.Add(tbn);
            sp.Children.Add(L("타입 (C# → DDS IDL):"));
            
            var cb=new ComboBox{Margin=new Thickness(0,0,0,14),FontFamily=new FontFamily("Consolas"),FontSize=11};
            
            var types=new[]{
                new[]{"bool","boolean"},new[]{"int","long"},new[]{"long","long long"},
                new[]{"float","float"},new[]{"double","double"},new[]{"string","string"},
                new[]{"byte","octet"},new[]{"short","short"}};
            
            foreach (var t in types)
            {
                cb.Items.Add(string.Format("{0,-8}  →  {1}", t[0], t[1]));
            }
            
            cb.SelectedIndex=0; 
            sp.Children.Add(cb);
            
            var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            var ok=new Button{Content="추가",Width=70,Height=28,Margin=new Thickness(0,0,6,0),FontFamily=new FontFamily("Consolas")};
            var cn=new Button{Content="취소",Width=70,Height=28,FontFamily=new FontFamily("Consolas")};
            
            ok.Click+=(s,e)=>
            {
                var item=cb.SelectedItem!=null?cb.SelectedItem.ToString():"int";
                var raw=item.Split(new[]{" →"},StringSplitOptions.None)[0].Trim();
                object defVal;
                if(raw=="bool")                   defVal=false;
                else if(raw=="float"||raw=="double") defVal=0.0;
                else if(raw=="string")             defVal="";
                else                               defVal=0;
                CreatedField=new DdsField{Name=tbn.Text,CsType=raw,Value=defVal};
                DialogResult=true;
            };
            
            cn.Click+=(s,e)=>DialogResult=false;
            row.Children.Add(ok); row.Children.Add(cn); sp.Children.Add(row);
            Content=sp;
        }
        TextBlock L(string t)=>new TextBlock
        {
            Text=t, Foreground=new SolidColorBrush(Color.FromRgb(107,122,153)),
            FontFamily=new FontFamily("Consolas"),FontSize=10,Margin=new Thickness(0,0,0,3)
        };
    }
}
