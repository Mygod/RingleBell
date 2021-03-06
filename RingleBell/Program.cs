﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Mygod.Xml.Linq;

namespace Mygod.RingleBell
{
    static class Program
    {
        /// <summary>
        /// 用于将错误转化为可读的字符串。
        /// </summary>
        /// <param name="e">错误。</param>
        /// <returns>错误字符串。</returns>
        public static string GetMessage(this Exception e)
        {
            var result = new StringBuilder();
            GetMessage(e, result);
            return result.ToString();
        }

        private static void GetMessage(Exception e, StringBuilder result)
        {
            while (e != null && !(e is AggregateException))
            {
                result.AppendFormat("({0}) {1}{2}{3}{2}", e.GetType(), e.Message, Environment.NewLine, e.StackTrace);
                e = e.InnerException;
            }
            var ae = e as AggregateException;
            if (ae != null) foreach (var ex in ae.InnerExceptions) GetMessage(ex, result);
        }

        [STAThread]
        public static void Main()
        {
			AppDomain.CurrentDomain.UnhandledException += (sender, e) => MessageBox.Show(((Exception) e.ExceptionObject).GetMessage());
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var schedules = XHelper.Load("Schedule.xml").ElementCaseInsensitive("Schedule").Elements()
                                   .Select(Schedule.GetSchedule).ToArray();
            Notify.MouseUp += (sender, e) =>
            {
                //if (e.Button == MouseButtons.Left) Engine.StopAllSounds();
                if (e.Button == MouseButtons.Right) Application.Exit();
            };
            //Notify.BalloonTipClicked += (sender, e) => Engine.StopAllSounds();
            //Notify.BalloonTipClosed += (sender, e) => Engine.StopAllSounds();
            Notify.Visible = true;
            timer = new Timer { Interval = 1000 };
            timer.Tick += (sender, e) =>
            {
                var pending = new List<PlaySchedule>();
                var now = DateTime.Now;
                foreach (var schedule in schedules.Where(schedule => schedule.DateTime == now))
                {
                    var play = schedule as PlaySchedule;
                    var mute = schedule as MuteSchedule;
                    if (play != null) pending.Add(play);
                    if (mute != null) pending.Clear();
                }
                foreach (var play in pending)
                {
                    var path = play.Sound[Random.Next(play.Sound.Length)];
                    if (!Players.ContainsKey(path)) Players[path] = new SoundPlayer(path);
                    Players[path].Play();
                    Notify.ShowBalloonTip(10000, play.Name, now.ToString("yyyy.M.d H:mm:ss"), ToolTipIcon.Info);
                }
            };
            timer.Enabled = true;
            Notify.ShowBalloonTip(5000, Title + " 已成功启动。", "左键点击可使它闭嘴，右键点击可以杀死它。\r\n" +
                                  "提示：可通过修改 Schedule.xml 自定义响铃时间。", ToolTipIcon.Info);
            Application.Run();
        }

        private static Timer timer;
        private static readonly Random Random = new Random();
        private static readonly NotifyIcon Notify = new NotifyIcon
            { Text = Title, Icon = IconExtractor.GetIcon(Assembly.GetEntryAssembly().Location) };
        private static readonly Dictionary<string, SoundPlayer> Players = new Dictionary<string, SoundPlayer>();

        private static AssemblyName NowAssemblyName { get { return Assembly.GetEntryAssembly().GetName(); } }
        public static string Title { get { return NowAssemblyName.Name + " V" + NowAssemblyName.Version; } }
    }

    public struct ExtendedDateTime
    {
        public ExtendedDateTime(string value)
        {
            var values = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Second = int.Parse(values[values.Length - 1]);
            if (values.Length >= 2 && !string.IsNullOrWhiteSpace(values[values.Length - 2]) && values[values.Length - 2] != "*")
                Minutes = new HashSet<int>(values[values.Length - 2].Split(',').Select(int.Parse));
            else Minutes = null;
            if (values.Length >= 3 && !string.IsNullOrWhiteSpace(values[values.Length - 3]) && values[values.Length - 3] != "*")
                Hours = new HashSet<int>(values[values.Length - 3].Split(',').Select(int.Parse));
            else Hours = null;
            if (values.Length >= 4 && !string.IsNullOrWhiteSpace(values[values.Length - 4]) && values[values.Length - 4] != "*")
                WeekDays = new HashSet<int>(values[values.Length - 4].Split(',').Select(str => WeekDaysLookup[str.ToLowerInvariant()]));
            else WeekDays = null;
            if (values.Length >= 5 && !string.IsNullOrWhiteSpace(values[values.Length - 5]) && values[values.Length - 5] != "*")
                Days = new HashSet<int>(values[values.Length - 5].Split(',').Select(int.Parse));
            else Days = null;
            if (values.Length >= 6 && !string.IsNullOrWhiteSpace(values[values.Length - 6]) && values[values.Length - 6] != "*")
                Months = new HashSet<int>(values[values.Length - 6].Split(',').Select(int.Parse));
            else Months = null;
            if (values.Length >= 7 && !string.IsNullOrWhiteSpace(values[values.Length - 7]) && values[values.Length - 7] != "*")
                Years = new HashSet<int>(values[values.Length - 7].Split(',').Select(int.Parse));
            else Years = null;
        }

        public static Dictionary<string, int> WeekDaysLookup = new Dictionary<string, int>
            { { "sun", 0 }, { "mon", 1 }, { "tue", 2 }, { "wed", 3 }, { "thu", 4 }, { "fri", 5 }, { "sat", 6 }, };
        public HashSet<int> Years, Months, Days, WeekDays, Hours, Minutes;
        public int Second;

        public static bool operator ==(ExtendedDateTime ex, DateTime value)
        {
            if (ex.Second != value.Second) return false;
            if (ex.Minutes != null && !ex.Minutes.Contains(value.Minute)) return false;
            if (ex.Hours != null && !ex.Hours.Contains(value.Hour)) return false;
            if (ex.WeekDays != null && !ex.WeekDays.Contains((int)value.DayOfWeek)) return false;
            if (ex.Days != null && !ex.Days.Contains(value.Day)) return false;
            if (ex.Months != null && !ex.Months.Contains(value.Month)) return false;
            return ex.Years == null || ex.Years.Contains(value.Year);
        }

        public static bool operator !=(ExtendedDateTime ex, DateTime value)
        {
            return !(ex == value);
        }
    }

    public abstract class Schedule
    {
        protected Schedule(XElement element)
        {
            DateTime = new ExtendedDateTime(element.GetAttributeValue("DateTime"));
        }

        public ExtendedDateTime DateTime;

        public static Schedule GetSchedule(XElement element)
        {
            switch (element.Name.LocalName.ToLowerInvariant())
            {
                case "play":    return new PlaySchedule(element);
                case "mute":    return new MuteSchedule(element);
                default:        throw new FormatException();
            }
        }
    }
    public sealed class PlaySchedule : Schedule
    {
        public PlaySchedule(XElement element) : base(element)
        {
            Name = element.GetAttributeValue("Name");
            Sound = element.GetAttributeValue("Sound").Split('|');
        }

        public string Name;
        public string[] Sound;
    }
    public sealed class MuteSchedule : Schedule
    {
        public MuteSchedule(XElement element) : base(element)
        {
        }
    }

    public sealed class IconExtractor : IDisposable
    {
        #region Win32 interop.

        #region Unmanaged Types

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Auto)]
        private delegate bool EnumResNameProc(IntPtr hModule, int lpszType, IntPtr lpszName, IconResInfo lParam);

        #endregion

        #region Consts.

        private const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

        private const int RT_ICON = 3;
        private const int RT_GROUP_ICON = 14;

        private const int MAX_PATH = 260;

        private const int ERROR_FILE_NOT_FOUND = 2;
        private const int ERROR_BAD_EXE_FORMAT = 193;

        private const int sICONDIR = 6; // sizeof(ICONDIR) 
        private const int sICONDIRENTRY = 16; // sizeof(ICONDIRENTRY)
        private const int sGRPICONDIRENTRY = 14; // sizeof(GRPICONDIRENTRY)

        #endregion

        #region API Functions

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool EnumResourceNames(
            IntPtr hModule, int lpszType, EnumResNameProc lpEnumFunc, IconResInfo lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, int lpType);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern int SizeofResource(IntPtr hModule, IntPtr hResInfo);

        #endregion

        #endregion

        #region Managed Types

        private class IconResInfo
        {
            public readonly List<ResourceName> IconNames = new List<ResourceName>();
        }

        private class ResourceName
        {
            public IntPtr Id { get; private set; }
            public string Name { get; private set; }

            private IntPtr _bufPtr = IntPtr.Zero;

            public ResourceName(IntPtr lpName)
            {
                if (((uint)lpName >> 16) == 0) // #define IS_INTRESOURCE(_r) ((((ULONG_PTR)(_r)) >> 16) == 0)
                {
                    Id = lpName;
                    Name = null;
                }
                else
                {
                    Id = IntPtr.Zero;
                    Name = Marshal.PtrToStringAuto(lpName);
                }
            }

            public IntPtr GetValue()
            {
                if (Name == null)
                {
                    return Id;
                }
                else
                {
                    _bufPtr = Marshal.StringToHGlobalAuto(Name);
                    return _bufPtr;
                }
            }

            public void Free()
            {
                if (_bufPtr != IntPtr.Zero)
                {
                    try
                    {
                        Marshal.FreeHGlobal(_bufPtr);
                    }
                    catch
                    {
                    }

                    _bufPtr = IntPtr.Zero;
                }
            }
        }

        #endregion

        #region Private Fields

        private IntPtr _hModule = IntPtr.Zero;
        private readonly IconResInfo _resInfo;

        private Icon[] _iconCache;

        #endregion

        #region Public Properties

        private readonly string _filename;

        // Full path 
        public string Filename
        {
            get { return _filename; }
        }

        public int IconCount
        {
            get { return _resInfo.IconNames.Count; }
        }

        #endregion

        #region Contructor/Destructor and relatives

        /// <summary>
        /// Load the specified executable file or DLL, and get ready to extract the icons.
        /// </summary>
        /// <param name="filename">The name of a file from which icons will be extracted.</param>
        public IconExtractor(string filename)
        {
            if (filename == null)
            {
                throw new ArgumentNullException("filename");
            }

            _hModule = LoadLibrary(filename);
            if (_hModule == IntPtr.Zero)
            {
                _hModule = LoadLibraryEx(filename, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
                if (_hModule == IntPtr.Zero)
                {
                    switch (Marshal.GetLastWin32Error())
                    {
                        case ERROR_FILE_NOT_FOUND:
                            throw new FileNotFoundException("Specified file '" + filename + "' not found.");

                        case ERROR_BAD_EXE_FORMAT:
                            throw new ArgumentException("Specified file '" + filename +
                                                        "' is not an executable file or DLL.");

                        default:
                            throw new Win32Exception();
                    }
                }
            }

            var buf = new StringBuilder(MAX_PATH);
            GetModuleFileName(_hModule, buf, buf.Capacity + 1);
            _filename = filename;

            _resInfo = new IconResInfo();
            bool success = EnumResourceNames(_hModule, RT_GROUP_ICON, EnumResNameCallBack, _resInfo);
            if (!success)
            {
                throw new Win32Exception();
            }

            _iconCache = new Icon[IconCount];
        }

        ~IconExtractor()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_hModule != IntPtr.Zero)
            {
                try
                {
                    FreeLibrary(_hModule);
                }
                catch
                {
                }

                _hModule = IntPtr.Zero;
            }

            if (_iconCache != null)
            {
                foreach (Icon i in _iconCache)
                {
                    if (i != null)
                    {
                        try
                        {
                            i.Dispose();
                        }
                        catch
                        {
                        }
                    }
                }

                _iconCache = null;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Extract an icon from the loaded executable file or DLL. 
        /// </summary>
        /// <param name="iconIndex">The zero-based index of the icon to be extracted.</param>
        /// <returns>A System.Drawing.Icon object which may consists of multiple icons.</returns>
        /// <remarks>Always returns new copy of the Icon. It should be disposed by the user.</remarks>
        public Icon GetIcon(int iconIndex)
        {
            if (_hModule == IntPtr.Zero)
            {
                throw new ObjectDisposedException("IconExtractor");
            }

            if (iconIndex < 0 || IconCount <= iconIndex)
            {
                throw new ArgumentException(
                    "iconIndex is out of range. It should be between 0 and " + (IconCount - 1).ToString() + ".");
            }

            if (_iconCache[iconIndex] == null)
            {
                _iconCache[iconIndex] = CreateIcon(iconIndex);
            }

            return (Icon)_iconCache[iconIndex].Clone();
        }

        /// <summary>
        /// Split an Icon consists of multiple icons into an array of Icon each consist of single icons.
        /// </summary>
        /// <param name="icon">The System.Drawing.Icon to be split.</param>
        /// <returns>An array of System.Drawing.Icon each consist of single icons.</returns>
        public static IEnumerable<Icon> SplitIcon(Icon icon)
        {
            if (icon == null)
            {
                throw new ArgumentNullException("icon");
            }

            // Get multiple .ico file image.
            byte[] srcBuf;
            using (var stream = new MemoryStream())
            {
                icon.Save(stream);
                srcBuf = stream.ToArray();
            }

            var splitIcons = new List<Icon>();
            {
                int count = BitConverter.ToInt16(srcBuf, 4); // ICONDIR.idCount

                for (int i = 0; i < count; i++)
                {
                    using (var destStream = new MemoryStream())
                    using (var writer = new BinaryWriter(destStream))
                    {
                        // Copy ICONDIR and ICONDIRENTRY.
                        writer.Write(srcBuf, 0, sICONDIR - 2);
                        writer.Write((short)1); // ICONDIR.idCount == 1;

                        writer.Write(srcBuf, sICONDIR + sICONDIRENTRY * i, sICONDIRENTRY - 4);
                        writer.Write(sICONDIR + sICONDIRENTRY);
                        // ICONDIRENTRY.dwImageOffset = sizeof(ICONDIR) + sizeof(ICONDIRENTRY)

                        // Copy picture and mask data.
                        int imgSize = BitConverter.ToInt32(srcBuf, sICONDIR + sICONDIRENTRY * i + 8);
                        // ICONDIRENTRY.dwBytesInRes
                        int imgOffset = BitConverter.ToInt32(srcBuf, sICONDIR + sICONDIRENTRY * i + 12);
                        // ICONDIRENTRY.dwImageOffset
                        writer.Write(srcBuf, imgOffset, imgSize);

                        // Create new icon.
                        destStream.Seek(0, SeekOrigin.Begin);
                        try
                        {
                            splitIcons.Add(new Icon(destStream));
                        }
                        catch (Win32Exception)  // creating 256x256 icon on XP will fail
                        {
                        }
                    }
                }
            }

            return splitIcons;
        }

        public static Icon GetIcon(string executionPath, int index = 0)
        {
            return SplitIcon(new IconExtractor(executionPath).GetIcon(index)).OrderByDescending(i => i.Height).First();
        }

        public override string ToString()
        {
            string text = String.Format("IconExtractor (Filename: '{0}', IconCount: {1})", Filename, IconCount);
            return text;
        }

        #endregion

        #region Private Methods

        private bool EnumResNameCallBack(IntPtr hModule, int lpszType, IntPtr lpszName, IconResInfo lParam)
        {
            // Callback function for EnumResourceNames().

            if (lpszType == RT_GROUP_ICON)
            {
                lParam.IconNames.Add(new ResourceName(lpszName));
            }

            return true;
        }

        private Icon CreateIcon(int iconIndex)
        {
            // Get group icon resource.
            byte[] srcBuf = GetResourceData(_hModule, _resInfo.IconNames[iconIndex], RT_GROUP_ICON);

            // Convert the resouce into an .ico file image.
            using (var destStream = new MemoryStream())
            using (var writer = new BinaryWriter(destStream))
            {
                int count = BitConverter.ToUInt16(srcBuf, 4); // ICONDIR.idCount
                int imgOffset = sICONDIR + sICONDIRENTRY * count;

                // Copy ICONDIR.
                writer.Write(srcBuf, 0, sICONDIR);

                for (int i = 0; i < count; i++)
                {
                    // Copy GRPICONDIRENTRY converting into ICONDIRENTRY.
                    writer.BaseStream.Seek(sICONDIR + sICONDIRENTRY * i, SeekOrigin.Begin);
                    writer.Write(srcBuf, sICONDIR + sGRPICONDIRENTRY * i, sICONDIRENTRY - 4);
                    // Common fields of structures
                    writer.Write(imgOffset); // ICONDIRENTRY.dwImageOffset

                    // Get picture and mask data, then copy them.
                    var nID = (IntPtr)BitConverter.ToUInt16(srcBuf, sICONDIR + sGRPICONDIRENTRY * i + 12);
                    // GRPICONDIRENTRY.nID
                    byte[] imgBuf = GetResourceData(_hModule, nID, RT_ICON);

                    writer.BaseStream.Seek(imgOffset, SeekOrigin.Begin);
                    writer.Write(imgBuf, 0, imgBuf.Length);

                    imgOffset += imgBuf.Length;
                }

                destStream.Seek(0, SeekOrigin.Begin);
                return new Icon(destStream);
            }
        }

        private byte[] GetResourceData(IntPtr hModule, IntPtr lpName, int lpType)
        {
            // Get binary image of the specified resource.

            IntPtr hResInfo = FindResource(hModule, lpName, lpType);
            if (hResInfo == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            IntPtr hResData = LoadResource(hModule, hResInfo);
            if (hResData == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            IntPtr hGlobal = LockResource(hResData);
            if (hGlobal == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            int resSize = SizeofResource(hModule, hResInfo);
            if (resSize == 0)
            {
                throw new Win32Exception();
            }

            var buf = new byte[resSize];
            Marshal.Copy(hGlobal, buf, 0, buf.Length);

            return buf;
        }

        private byte[] GetResourceData(IntPtr hModule, ResourceName name, int lpType)
        {
            try
            {
                IntPtr lpName = name.GetValue();
                return GetResourceData(hModule, lpName, lpType);
            }
            finally
            {
                name.Free();
            }
        }

        #endregion
    }
}
